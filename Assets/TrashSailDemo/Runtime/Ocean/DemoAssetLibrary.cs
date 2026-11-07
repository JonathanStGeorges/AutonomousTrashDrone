using System;
using UnityEngine;

namespace TrashSailDemo
{
    /// <summary>Simulation-only visual asset lookup. Never an operational perception input.</summary>
    public static class DemoAssetLibrary
    {
        public static DemoActor CreateActor(string key, string kind, Vector3 position, Transform parent)
        {
            int layer = kind == "fish" ? 9 : kind == "boat" ? 11 : 10;
            var root = new GameObject(key + " simulation actor") { layer = layer };
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var visual = InstantiateVisual(key, root.transform, layer);
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = new Vector3(Mathf.Max(.08f, bounds.size.x), Mathf.Max(.08f, bounds.size.y), Mathf.Max(.08f, bounds.size.z));
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = kind == "boat" ? 120 : .35f;
            body.isKinematic = kind == "fish" || kind == "boat";
            body.useGravity = !body.isKinematic;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = body.isKinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
            var actor = root.AddComponent<DemoActor>();
            actor.Body = body;
            actor.Kind = kind;
            actor.AssetKey = key;
            actor.InitialPosition = position;
            actor.Floating = kind == "trash" || kind == "unknown";
            actor.SpawnTime = Time.timeAsDouble;
            return actor;
        }

        public static GameObject InstantiateVisual(string key, Transform parent, int layer)
        {
            var prefab = Resources.Load<GameObject>("TrashSailModels/" + key);
            if (prefab == null) throw new InvalidOperationException("Missing imported model " + key + ". Run Trash Sail Demo/Create or Update Imported Assets before building the scene.");
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = key + " imported visual";
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            return instance;
        }
    }
}
