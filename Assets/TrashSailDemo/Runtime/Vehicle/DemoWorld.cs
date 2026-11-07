using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    public sealed class DemoWorld : MonoBehaviour
    {
        public DemoConfig Config;
        public DemoVessel Vessel;
        public IReadOnlyList<DemoActor> Actors => actors;
        readonly List<DemoActor> actors = new List<DemoActor>();
        readonly List<Material> actorMaterials = new List<Material>();
        Transform actorRoot;
        Vector3 initialWind;
        bool initialMotor, capturedDefaults;

        public static Material MakeMaterial(string label, Color color)
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = "TrashSail_" + label };
            if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        static GameObject Primitive(string label, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, int layer, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = label;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Collider col = go.GetComponent<Collider>();
            if (!collider && col != null)
            {
                col.enabled = false;
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            }
            return go;
        }

        static Transform Mount(string name, Transform parent, Vector3 position)
        {
            Transform result = new GameObject(name).transform;
            result.SetParent(parent, false);
            result.localPosition = position;
            return result;
        }

        public static DemoVessel BuildVessel(DemoConfig config, Transform parent = null)
        {
            GameObject root = new GameObject("Trash Sail Drone — physics root");
            root.layer = 8;
            root.transform.SetParent(parent, false);
            Rigidbody body = root.AddComponent<Rigidbody>();
            DemoVessel vessel = root.AddComponent<DemoVessel>();
            vessel.Config = config;
            vessel.Body = body;
            vessel.ConfigureBody();
            Transform visual = Mount("Visual Model — replace with CAD", root.transform, Vector3.zero);
            vessel.VisualRoot = visual;
            Transform collisionRoot = Mount("Collision Geometry", root.transform, Vector3.zero);
            float length = config.VesselLength;
            float width = config.VesselWidth;
            Material hullMaterial = MakeMaterial("Hull", new Color(0.12f, 0.55f, 0.6f));
            Material deckMaterial = MakeMaterial("Deck", new Color(0.1f, 0.15f, 0.22f));
            Material white = MakeMaterial("Sail", new Color(0.9f, 0.93f, 0.95f));
            Material dark = MakeMaterial("Hardware", new Color(0.04f, 0.07f, 0.1f));
            Material gateMaterial = MakeMaterial("Collector", new Color(0.1f, 0.75f, 0.65f));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * width * 0.4f;
                GameObject hull = Primitive(side < 0 ? "Port Hull" : "Starboard Hull", PrimitiveType.Capsule, visual,
                    new Vector3(x, 0, 0), new Vector3(0.42f, length * 0.5f, 0.42f), hullMaterial, 8, false);
                hull.transform.localRotation = Quaternion.Euler(90, 0, 0);
                GameObject hullCollider = new GameObject(side < 0 ? "Port Hull Collider" : "Starboard Hull Collider");
                hullCollider.layer = 8;
                hullCollider.transform.SetParent(collisionRoot, false);
                hullCollider.transform.localPosition = new Vector3(x, 0, 0);
                CapsuleCollider capsule = hullCollider.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = 0.21f;
                capsule.height = length;
            }
            Primitive("Deck", PrimitiveType.Cube, visual, new Vector3(0, 0.4f, -0.2f), new Vector3(width, 0.18f, length * 0.6f), deckMaterial, 8, false);
            Primitive("Mast", PrimitiveType.Cylinder, visual, new Vector3(0, 1.5f, -0.25f), new Vector3(0.07f, 1.3f, 0.07f), dark, 8, false);
            Transform sail = Mount("Sail trim pivot", visual, new Vector3(0, 0.7f, -0.25f));
            GameObject cloth = new GameObject("Triangular Sail");
            cloth.layer = 8;
            cloth.transform.SetParent(sail, false);
            Mesh mesh = new Mesh { name = "TrashSail_PlaceholderSail" };
            mesh.vertices = new[] { Vector3.zero, new Vector3(0, 2.1f, 0), new Vector3(0, 0, -1.25f) };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            cloth.AddComponent<MeshFilter>().sharedMesh = mesh;
            cloth.AddComponent<MeshRenderer>().sharedMaterial = white;
            vessel.SailVisual = sail;
            vessel.RudderVisual = Primitive("Rudder", PrimitiveType.Cube, visual, new Vector3(0, -0.25f, -length * 0.5f),
                new Vector3(0.08f, 0.65f, 0.45f), dark, 8, false).transform;
            Transform basket = Mount("Collection Basket", visual, new Vector3(0, 0.58f, -0.65f));
            Primitive("Basket Floor", PrimitiveType.Cube, basket, Vector3.zero, new Vector3(1.15f, 0.08f, 0.65f), gateMaterial, 8, false);
            for (int side = -1; side <= 1; side += 2)
                Primitive("Basket Rail", PrimitiveType.Cube, basket, new Vector3(side * 0.57f, 0.2f, 0), new Vector3(0.04f, 0.4f, 0.65f), gateMaterial, 8, false);
            Transform intake = Mount("Physical Intake", root.transform, new Vector3(0, -0.08f, length * 0.5f + 0.3f));
            intake.gameObject.layer = 8;
            BoxCollider volume = intake.gameObject.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(config.IntakeWidth, 0.9f, 0.8f);
            DemoCollector collector = intake.gameObject.AddComponent<DemoCollector>();
            collector.Config = config;
            collector.Vessel = vessel;
            collector.CaptureVolume = volume;
            collector.Storage = basket;
            vessel.Collector = collector;
            for (int side = -1; side <= 1; side += 2)
                Primitive("Intake Side Guide", PrimitiveType.Cube, visual, new Vector3(side * config.IntakeWidth * 0.5f, 0.04f, length * 0.5f + 0.15f),
                    new Vector3(0.05f, 0.18f, 0.9f), gateMaterial, 8, false);
            Transform gate = Mount("Collector Gate", visual, new Vector3(0, 0.08f, length * 0.5f + 0.3f));
            Primitive("Gate Slat", PrimitiveType.Cube, gate, Vector3.zero, new Vector3(config.IntakeWidth, 0.16f, 0.06f), gateMaterial, 8, false);
            collector.GateVisual = gate;
            vessel.CameraMount = Mount("Camera Mount", root.transform, new Vector3(0, 1.65f, length * 0.45f));
            vessel.CameraMount.localRotation = Quaternion.Euler(32, 0, 0);
            vessel.SonarMount = Mount("Submerged Sonar Mount", root.transform, new Vector3(0, -0.38f, length * 0.5f + 0.15f));
            Primitive("Camera Housing", PrimitiveType.Cube, visual, vessel.CameraMount.localPosition - Vector3.up * 0.1f, new Vector3(0.2f, 0.12f, 0.15f), dark, 8, false);
            Primitive("Sonar Housing", PrimitiveType.Cube, visual, vessel.SonarMount.localPosition, new Vector3(0.2f, 0.16f, 0.12f), dark, 8, false);
            DemoOwnState state = root.AddComponent<DemoOwnState>();
            state.Config = config;
            state.Vessel = vessel;
            return vessel;
        }

        public void ResetWorld(DemoScenario scenario, int seed)
        {
            if (Config == null) return;
            if (!capturedDefaults) { initialWind = Config.WindTo; initialMotor = Config.MotorAssistance; capturedDefaults = true; }
            Config.Scenario = scenario;
            Config.Seed = seed;
            Config.WindTo = scenario == DemoScenario.Upwind ? new Vector3(0, 0, -5) : scenario == DemoScenario.LowWind ? new Vector3(0.05f, 0, 0) : initialWind;
            Config.MotorAssistance = scenario == DemoScenario.Upwind || scenario == DemoScenario.LowWind ? false : initialMotor;
            foreach (DemoActor actor in actors)
            {
                if (actor == null) continue;
                actor.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(actor.gameObject); else DestroyImmediate(actor.gameObject);
            }
            actors.Clear();
            foreach (Material material in actorMaterials)
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            actorMaterials.Clear();
            if (actorRoot == null) actorRoot = Mount("Scenario Actors — truth only", transform, Vector3.zero);
            if (Vessel != null)
            {
                Vessel.ResetPose(Vector3.zero, scenario == DemoScenario.Upwind ? Quaternion.Euler(0, 55, 0) : Quaternion.identity);
                Vessel.GetComponent<DemoOwnState>()?.ResetState(seed);
                Vessel.Collector.Captured -= OnCaptured;
                Vessel.Collector.Captured += OnCaptured;
            }
            System.Random random = new System.Random(seed);
            if (scenario != DemoScenario.Upwind && scenario != DemoScenario.LowWind)
            {
                if (scenario == DemoScenario.OccludedTarget)
                {
                    CreateTrash("Bottle", new Vector3(0, 0, 11), 0);
                    CreateObstacle(new Vector3(0, 0.5f, 7), new Vector3(4, 2.5f, 0.7f), "Occluder");
                    CreateTrash("Uncertain Muted Package", new Vector3(-4, 0, 8), 2, true);
                }
                else
                {
                    CreateTrash("Bottle", new Vector3(0, 0, 8), 0);
                    CreateTrash("Can", new Vector3(0, 0, 15), 1);
                    CreateTrash("Packaging", new Vector3(0, 0, 22), 2);
                }
            }
            CreateObstacle(new Vector3(7, 0, 17), new Vector3(1.8f, 2.5f, 1.8f), "Fixed navigation obstacle");
            if (scenario == DemoScenario.CrossingFish)
                CreateFish(new Vector3(-4, -0.3f, 7), true, 0.62f + (float)random.NextDouble() * 0.06f);
            if (scenario == DemoScenario.UnderwaterHazard)
                CreateFish(new Vector3(0, -0.7f, 6.2f), false, 0);
            if (scenario == DemoScenario.AmbiguousAssociation)
                CreateFish(new Vector3(0.45f, -0.35f, 7.8f), false, 0);
            if (scenario == DemoScenario.MetadataVariant)
                foreach (DemoActor actor in actors) actor.gameObject.name = "Renamed_" + random.Next(100000, 999999);
            Physics.SyncTransforms();
        }

        DemoActor CreateTrash(string label, Vector3 position, int shape, bool uncertain = false)
        {
            GameObject root = new GameObject(label);
            root.layer = 10;
            root.transform.SetParent(actorRoot, false);
            root.transform.position = position;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, -0.05f, 0);
            collider.size = new Vector3(shape == 2 ? 0.65f : 0.38f, 0.6f, shape == 2 ? 0.55f : 0.38f);
            Material material = MakeMaterial(label, uncertain ? new Color(180f / 255, 140f / 255, 110f / 255) : new Color(1, 0.18f, 0.025f));
            actorMaterials.Add(material);
            if (shape == 0)
            {
                Primitive("Bottle body", PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.14f, 0), new Vector3(0.32f, 0.14f, 0.32f), material, 10, false);
                Primitive("Bottle neck", PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.34f, 0), new Vector3(0.14f, 0.07f, 0.14f), material, 10, false);
            }
            else if (shape == 1) Primitive("Can", PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.15f, 0), new Vector3(0.35f, 0.15f, 0.35f), material, 10, false);
            else Primitive("Floating packaging", PrimitiveType.Cube, root.transform, new Vector3(0, 0.1f, 0), new Vector3(0.6f, 0.2f, 0.5f), material, 10, false);
            DemoActor actor = root.AddComponent<DemoActor>();
            actor.Body = body;
            actor.Kind = uncertain ? "unknown" : "trash";
            actor.InitialPosition = position;
            actor.Floating = true;
            actor.SpawnTime = Time.timeAsDouble;
            actors.Add(actor);
            return actor;
        }

        DemoActor CreateFish(Vector3 position, bool crossing, float speed)
        {
            GameObject root = new GameObject("Fish — evaluator truth only");
            root.layer = 9;
            root.transform.SetParent(actorRoot, false);
            root.transform.position = position;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.8f, 0.38f, 0.35f);
            Material material = MakeMaterial("Fish", new Color(0.55f, 0.7f, 0.72f));
            actorMaterials.Add(material);
            Primitive("Fish body", PrimitiveType.Sphere, root.transform, Vector3.zero, new Vector3(0.8f, 0.3f, 0.32f), material, 9, false);
            Primitive("Fish tail", PrimitiveType.Cube, root.transform, new Vector3(-0.45f, 0, 0), new Vector3(0.2f, 0.3f, 0.06f), material, 9, false);
            DemoActor actor = root.AddComponent<DemoActor>();
            actor.Body = body;
            actor.Kind = "fish";
            actor.InitialPosition = position;
            actor.Crossing = crossing;
            actor.CrossingSpeed = speed;
            actor.SpawnTime = Time.timeAsDouble;
            actors.Add(actor);
            return actor;
        }

        DemoActor CreateObstacle(Vector3 position, Vector3 size, string label)
        {
            Material material = MakeMaterial("Obstacle", new Color(0.42f, 0.46f, 0.5f));
            actorMaterials.Add(material);
            GameObject root = Primitive(label, PrimitiveType.Cube, actorRoot, position, size, material, 11, true);
            DemoActor actor = root.AddComponent<DemoActor>();
            actor.Kind = "obstacle";
            actor.InitialPosition = position;
            actors.Add(actor);
            return actor;
        }
        void OnCaptured(GameObject body)
        {
            DemoActor actor = body.GetComponent<DemoActor>();
            if (actor != null) actor.Captured = true;
        }
        void OnDestroy()
        {
            if (Vessel != null && Vessel.Collector != null) Vessel.Collector.Captured -= OnCaptured;
            foreach (Material material in actorMaterials)
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
