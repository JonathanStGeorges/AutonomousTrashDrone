using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrashSailDemo.Tests
{
    public sealed class OceanTests
    {
        GameObject root;
        DemoConfig config;
        DemoVessel vessel;
        OceanPopulation population;
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp()
        {
            foreach (string key in new[] { "marus_boat", "plastic_bottle_gallon", "cleaner_tin_01", "trashbag", "multi_cleaner_bottle", "BarramundiFish" })
                Assert.That(Resources.Load<GameObject>("TrashSailModels/" + key), Is.Not.Null, "Imported fixture missing: run imported asset setup first: " + key);
            config = ScriptableObject.CreateInstance<DemoConfig>();
            config.LargeOcean = true;
            config.OceanRubbishCount = 8;
            config.OceanFishCount = 1;
            config.OceanBoatCount = 1;
            config.ReachableDemoSeed = true;
            config.StreamingRadius = 100;
            config.MissionWidth = config.MissionLength = 1000;
            root = new GameObject("Ocean persistence test fixture");
            vessel = DemoWorld.BuildVessel(config, root.transform);
            population = root.AddComponent<OceanPopulation>();
            population.Config = config;
            population.Vessel = vessel;
            population.ResetPopulation(17);
        }

        [TearDown] public void TearDown()
        {
            if (population != null) population.ClearPopulation();
            if (root != null) Object.DestroyImmediate(root);
            if (config != null) Object.DestroyImmediate(config);
        }

        void Invoke(string method)
        {
            MethodInfo function = typeof(OceanPopulation).GetMethod(method, PrivateInstance);
            Assert.That(function, Is.Not.Null, "Test requires the actual streaming/update entry point");
            function.Invoke(population, null);
        }
        DemoActor Active(int id) => population.ActiveActors.FirstOrDefault(actor => actor.WorldRecordId == id);
        void MoveObserver(Vector3 position)
        {
            // Evaluator setup, not autonomy: changes which persistent region is streamed.
            vessel.ResetPose(position, Quaternion.identity);
            Physics.SyncTransforms();
            Invoke("Stream");
        }
        bool Capture(DemoActor actor)
        {
            var intake = vessel.Collector;
            Vector3 centre = intake.CaptureVolume.transform.TransformPoint(intake.CaptureVolume.center);
            actor.transform.position += centre - actor.Body.worldCenterOfMass;
            actor.Body.position = actor.transform.position;
            Physics.SyncTransforms();
            intake.Active = true;
            return intake.TryCapture(actor.GetComponent<Collider>());
        }

        [Test] public void UnloadedRecordDriftsAndReloadsAtPersistedPositionWithImportedMesh()
        {
            OceanRecord record = population.Records[3];
            MoveObserver(record.Position);
            DemoActor actor = Active(record.Id);
            Assert.That(actor, Is.Not.Null);
            Assert.That(actor.GetComponentsInChildren<MeshFilter>().Any(filter => filter.sharedMesh != null), Is.True);
            Vector3 drift = record.Velocity;
            Assert.That(drift.sqrMagnitude, Is.GreaterThan(0));
            Vector3 physicalPosition = record.Position + new Vector3(.25f, 0, .1f);
            actor.transform.position = physicalPosition;
            actor.Body.position = physicalPosition;
            actor.Body.linearVelocity = drift;
            Physics.SyncTransforms();
            Invoke("FixedUpdate");
            MoveObserver(physicalPosition + Vector3.right * 400);
            Assert.That(Active(record.Id), Is.Null);
            Assert.That(actor.gameObject.activeSelf, Is.False);
            Vector3 unloadedAt = record.Position;
            for (int i = 0; i < 5; i++) Invoke("FixedUpdate");
            Vector3 expected = unloadedAt + drift * Time.fixedDeltaTime * 5;
            Assert.That(Vector3.Distance(record.Position, expected), Is.LessThan(.0002f));
            MoveObserver(record.Position);
            DemoActor reloaded = Active(record.Id);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(Vector3.Distance(reloaded.transform.position, expected), Is.LessThan(.0002f));
            Assert.That(Vector3.Distance(reloaded.Body.linearVelocity, drift), Is.LessThan(.0002f));
            Assert.That(record.Collected, Is.False);
            Assert.That(population.Records.Count, Is.EqualTo(10));
        }

        [Test] public void PhysicalCaptureNeverRespawnsDuringStreamingAndSeededResetRestoresOneCallback()
        {
            Vector3[] initial = population.Records.Select(record => record.InitialPosition).ToArray();
            string[] keys = population.Records.Select(record => record.AssetKey).ToArray();
            var actor = Active(0);
            Assert.That(actor, Is.Not.Null);
            int captureEvents = 0;
            Action<GameObject> count = ignored => captureEvents++;
            vessel.Collector.Captured += count;
            try
            {
                Assert.That(Capture(actor), Is.True);
                Assert.That(population.Records[0].Collected, Is.True);
                Assert.That(actor.Captured, Is.True);
                Assert.That(vessel.Collector.TryCapture(actor.GetComponent<Collider>()), Is.False);
                Assert.That(captureEvents, Is.EqualTo(1));
                // Move only the observer without clearing the physical collector history.
                vessel.transform.position = new Vector3(400, 0, 400);vessel.Body.position = vessel.transform.position;Physics.SyncTransforms();Invoke("Stream");
                vessel.transform.position = Vector3.zero;vessel.Body.position = Vector3.zero;Physics.SyncTransforms();Invoke("Stream");
                Assert.That(population.Records[0].Collected, Is.True);
                Assert.That(population.ActiveActors.Count(item => item.WorldRecordId == 0 && !item.Captured), Is.Zero);
                Assert.That(vessel.Collector.CaptureCount, Is.EqualTo(1));
                Assert.That(actor.transform.parent, Is.EqualTo(vessel.Collector.Storage));

                population.ResetPopulation(17);
                population.ResetPopulation(17); // Repeated setup must not accumulate subscribers.
                Assert.That(population.Records.Select(record => record.InitialPosition), Is.EqualTo(initial));
                Assert.That(population.Records.Select(record => record.AssetKey), Is.EqualTo(keys));
                Assert.That(population.Records.All(record => !record.Collected), Is.True);
                Assert.That(vessel.Collector.CaptureCount, Is.Zero);
                Assert.That(vessel.Collector.Active, Is.False);
                var resetActor = Active(0);
                Assert.That(resetActor.GetComponent<Collider>().enabled, Is.True);
                Assert.That(resetActor.Captured, Is.False);
                FieldInfo callbackField = typeof(DemoCollector).GetField("Captured", PrivateInstance);
                Assert.That(callbackField, Is.Not.Null);
                var callbacks = (Action<GameObject>)callbackField.GetValue(vessel.Collector);
                Assert.That(callbacks.GetInvocationList().Count(callback => ReferenceEquals(callback.Target, population)), Is.EqualTo(1));
                Assert.That(Capture(resetActor), Is.True);
                Assert.That(captureEvents, Is.EqualTo(2));
                Assert.That(vessel.Collector.CaptureCount, Is.EqualTo(1));
                Assert.That(population.Records[0].Collected, Is.True);
            }
            finally { vessel.Collector.Captured -= count; }
        }

        [Test] public void ImportedFishEnteringOpenIntakeIsCapturedAndPersistedAsBycatchTruth()
        {
            OceanRecord fish = population.Records.First(record => record.Kind == "fish");
            MoveObserver(fish.Position);
            DemoActor actor = Active(fish.Id);
            Assert.That(actor, Is.Not.Null, "Fish region must activate after production ResetPose synchronizes its Transform");
            Assert.That(actor.GetComponentsInChildren<MeshFilter>().Any(filter => filter.sharedMesh != null), Is.True);
            Assert.That(Capture(actor), Is.True);
            Assert.That(fish.Collected, Is.True);
            Assert.That(actor.Kind, Is.EqualTo("fish"));
            Assert.That(actor.Captured, Is.True);
            Assert.That(vessel.Collector.CaptureCount, Is.EqualTo(1));
            Assert.That(vessel.Collector.TryCapture(actor.GetComponent<Collider>()), Is.False);
        }
    }
}
