using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    [Serializable]
    public sealed class OceanRecord
    {
        public int Id;
        public Vector3 InitialPosition, Position, Velocity;
        public bool Collected;
        public string Kind, AssetKey;
        public Quaternion Rotation;
    }

    /// <summary>Simulation truth for streaming, evaluator and explicitly labelled spectator views only.</summary>
    public sealed class OceanPopulation : MonoBehaviour
    {
        public DemoConfig Config;
        public DemoVessel Vessel;
        public IReadOnlyList<OceanRecord> Records => records;
        public int ActiveCount => active.Count;
        public IEnumerable<DemoActor> ActiveActors => active.Values; // Privileged evaluator only.
        public float ActivationRadius { get; private set; }
        readonly List<OceanRecord> records = new List<OceanRecord>();
        readonly Dictionary<int, DemoActor> active = new Dictionary<int, DemoActor>();
        readonly Dictionary<string, Stack<DemoActor>> pools = new Dictionary<string, Stack<DemoActor>>();
        readonly List<int> retire = new List<int>();
        float nextStreaming;
        Transform populationRoot;
        static readonly string[] RubbishKeys = { "plastic_bottle_gallon", "cleaner_tin_01", "trashbag", "multi_cleaner_bottle" };

        public void ResetPopulation(int seed)
        {
            ClearPopulation();
            if (Config == null || Vessel == null) return;
            Config.Seed = seed;
            if (populationRoot == null)
            {
                populationRoot = new GameObject("Full simulated ocean population - truth only").transform;
                populationRoot.SetParent(transform, false);
            }
            Vessel.ResetPose(Vector3.zero, Quaternion.identity);
            Vessel.GetComponent<DemoOwnState>()?.ResetState(seed);
            Vessel.Collector.Captured -= OnCaptured;
            Vessel.Collector.Captured += OnCaptured;
            var random = new System.Random(seed);
            Vector3 cluster = Vector3.zero;
            for (int i = 0; i < Config.OceanRubbishCount; i++)
            {
                if (i % 7 == 0) cluster = RandomPosition(random, 0, 12);
                Vector3 position = i % 7 < 3 ? cluster + new Vector3(Signed(random) * 3, 0, Signed(random) * 3) : RandomPosition(random, 0, 12);
                position.x = Mathf.Clamp(position.x, -Config.MissionWidth * .5f + 2, Config.MissionWidth * .5f - 2);
                position.z = Mathf.Clamp(position.z, 5, Config.MissionLength - 2);
                // Static seeded placements, independent of camera and vessel motion.
                if (Config.ReachableDemoSeed && i < 3) position = new Vector3(0, 0, 35 + i * 35);
                Vector3 drift = i < 3 && Config.ReachableDemoSeed ? Vector3.zero : new Vector3(Signed(random) * .015f, 0, Signed(random) * .015f);
                AddRecord("trash", RubbishKeys[i % RubbishKeys.Length], position, drift, (float)random.NextDouble() * 360);
            }
            for (int i = 0; i < Config.OceanFishCount; i++)
                AddRecord("fish", "BarramundiFish", RandomPosition(random, -.45f - (float)random.NextDouble() * 1.1f, 18), new Vector3(.25f + (float)random.NextDouble() * .3f, 0, 0), 0);
            for (int i = 0; i < Config.OceanBoatCount; i++)
                AddRecord("boat", "marus_boat", RandomPosition(random, 0, 35), Vector3.zero, (float)random.NextDouble() * 360);
            nextStreaming = 0;
            Stream();
            Physics.SyncTransforms();
        }

        Vector3 RandomPosition(System.Random random, float y, float margin)
            => new Vector3(Signed(random) * Mathf.Max(2, Config.MissionWidth * .5f - margin), y, margin + (float)random.NextDouble() * Mathf.Max(1, Config.MissionLength - margin * 2));
        static float Signed(System.Random random) => (float)random.NextDouble() * 2 - 1;
        void AddRecord(string kind, string key, Vector3 position, Vector3 velocity, float yaw)
            => records.Add(new OceanRecord { Id = records.Count, Kind = kind, AssetKey = key, InitialPosition = position, Position = position, Velocity = velocity, Rotation = Quaternion.Euler(0, yaw, 0) });

        void FixedUpdate()
        {
            if (Config == null || !Config.LargeOcean || Vessel == null) return;
            foreach (OceanRecord record in records)
            {
                if (record.Collected) continue;
                if (active.TryGetValue(record.Id, out DemoActor actor) && actor != null)
                {
                    record.Position = actor.transform.position;
                    record.Rotation = actor.transform.rotation;
                    if (actor.Body != null && !actor.Body.isKinematic) record.Velocity = DemoMath.Flat(actor.Body.linearVelocity);
                }
                else record.Position += record.Velocity * Time.fixedDeltaTime;
                // Boundary bounce is scenario motion, never fish evasion of the vessel.
                if (Mathf.Abs(record.Position.x) > Config.MissionWidth * .5f - 1)
                    record.Velocity.x = -Mathf.Sign(record.Position.x) * Mathf.Abs(record.Velocity.x);
                if (record.Position.z < 1) record.Velocity.z = Mathf.Abs(record.Velocity.z);
                if (record.Position.z > Config.MissionLength - 1) record.Velocity.z = -Mathf.Abs(record.Velocity.z);
                if (actor != null) actor.DriftVelocity = record.Velocity;
            }
            if (Time.time >= nextStreaming) { nextStreaming = Time.time + .25f; Stream(); }
        }

        void Stream()
        {
            // Includes sensor range, motion until the next stream pass and turning clearance.
            ActivationRadius = Mathf.Max(Config.StreamingRadius, Config.SonarMaxRange + Config.VesselLength + Config.SafeMargin + Config.MaxSearchSpeed * 8 + 8);
            Vector3 origin = Vessel.transform.position;
            retire.Clear();
            foreach (OceanRecord record in records)
            {
                if (record.Collected) continue;
                float distance = DemoMath.Flat(record.Position - origin).magnitude;
                if (active.ContainsKey(record.Id))
                {
                    if (distance > ActivationRadius + 20) retire.Add(record.Id);
                }
                else if (distance <= ActivationRadius) Activate(record);
            }
            foreach (int id in retire)
            {
                DemoActor actor = active[id];
                active.Remove(id);
                ReturnToPool(actor);
            }
        }

        void Activate(OceanRecord record)
        {
            DemoActor actor;
            if (pools.TryGetValue(record.AssetKey, out Stack<DemoActor> pool) && pool.Count > 0) actor = pool.Pop();
            else actor = DemoAssetLibrary.CreateActor(record.AssetKey, record.Kind, record.Position, populationRoot);
            actor.transform.SetParent(populationRoot, false);
            actor.transform.SetPositionAndRotation(record.Position, record.Rotation);
            actor.InitialPosition = record.InitialPosition;
            actor.InitialPosition.y = record.Position.y;
            actor.Captured = false;
            actor.WorldRecordId = record.Id;
            actor.Kind = record.Kind;
            actor.Crossing = false;
            actor.DriftVelocity = record.Velocity;
            actor.SpawnTime = Time.timeAsDouble;
            actor.Body.isKinematic = record.Kind == "fish" || record.Kind == "boat";
            if (!actor.Body.isKinematic) { actor.Body.linearVelocity = record.Velocity; actor.Body.angularVelocity = Vector3.zero; }
            foreach (Collider collider in actor.GetComponentsInChildren<Collider>()) collider.enabled = true;
            actor.gameObject.SetActive(true);
            active.Add(record.Id, actor);
        }

        void OnCaptured(GameObject body)
        {
            DemoActor actor = body.GetComponent<DemoActor>();
            if (actor == null || actor.WorldRecordId < 0 || actor.WorldRecordId >= records.Count) return;
            OceanRecord record = records[actor.WorldRecordId];
            if (!active.TryGetValue(record.Id, out DemoActor expected) || expected != actor) return;
            record.Collected = true;
            record.Position = actor.transform.position;
            record.Velocity = Vector3.zero;
            actor.Captured = true;
            // Keep captured actor until reset; never return it to the streaming pool.
        }

        void ReturnToPool(DemoActor actor)
        {
            if (actor == null) return;
            actor.gameObject.SetActive(false);
            actor.transform.SetParent(populationRoot, false);
            if (!pools.TryGetValue(actor.AssetKey, out Stack<DemoActor> pool)) pools[actor.AssetKey] = pool = new Stack<DemoActor>();
            pool.Push(actor);
        }

        public void ClearPopulation()
        {
            if (Vessel != null && Vessel.Collector != null) Vessel.Collector.Captured -= OnCaptured;
            foreach (DemoActor actor in active.Values) ReturnToPool(actor);
            active.Clear();
            records.Clear();
            nextStreaming = 0;
        }

        void OnDestroy()
        {
            if (Vessel != null && Vessel.Collector != null) Vessel.Collector.Captured -= OnCaptured;
        }
    }
}

