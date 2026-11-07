using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    public sealed class DemoCollector : MonoBehaviour
    {
        public DemoConfig Config;
        public DemoVessel Vessel;
        public Transform Storage;
        public BoxCollider CaptureVolume;
        public Transform GateVisual;
        public int CaptureCount { get; private set; }
        public int OverspeedCaptureCount { get; private set; }
        public float Fill => Config == null ? 0 : CaptureCount / Mathf.Max(1, Config.Capacity);
        public bool Active;
        // Privileged physical body reference is for scoring only, never planning.
        public event Action<GameObject> Captured;
        readonly HashSet<Rigidbody> capturedBodies = new HashSet<Rigidbody>();

        public static bool CaptureAllowed(bool active, int count, float capacity, float relativeSpeed, float maxSpeed, bool centreInside)
            => active && count < capacity && relativeSpeed <= maxSpeed && centreInside;

        void Update()
        {
            if (GateVisual != null) GateVisual.localScale = new Vector3(1, Active ? 0.12f : 1, 1);
        }
        void OnTriggerEnter(Collider other) { TryCapture(other); }
        void OnTriggerStay(Collider other) { TryCapture(other); }
        public bool TryCapture(Collider other)
        {
            if (other == null || Config == null || Vessel == null || CaptureVolume == null) return false;
            Rigidbody body = other.attachedRigidbody;
            if (body == null || body == Vessel.Body || capturedBodies.Contains(body)) return false;
            Vector3 centre = CaptureVolume.transform.InverseTransformPoint(body.worldCenterOfMass) - CaptureVolume.center;
            Vector3 extent = CaptureVolume.size * 0.5f;
            bool inside = Mathf.Abs(centre.x) <= extent.x && Mathf.Abs(centre.y) <= extent.y && Mathf.Abs(centre.z) <= extent.z;
            float relativeSpeed = DemoMath.Flat(body.linearVelocity - Vessel.Body.linearVelocity).magnitude;
            // An open physical intake cannot selectively let a fast fish pass through
            // without consequence. Policy should prevent overspeed, but ingress still
            // captures the body and records the unsafe event for independent scoring.
            if (!Active || CaptureCount >= Config.Capacity || !inside) return false;
            if (relativeSpeed > Config.MaxCollectionSpeed) OverspeedCaptureCount++;
            capturedBodies.Add(body);
            CaptureCount++;
            Active = CaptureCount < Config.Capacity && Active;
            // The physical event happens before storage relocation; no truth class filter.
            Captured?.Invoke(body.gameObject);
            foreach (Collider collider in body.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            if (Storage != null)
            {
                body.transform.SetParent(Storage, true);
                int slot = CaptureCount - 1;
                body.transform.localPosition = new Vector3((slot % 3 - 1) * 0.32f, 0.12f + slot / 3 * 0.16f, 0);
                body.transform.localRotation = Quaternion.identity;
            }
            return true;
        }
        public void ResetCollector()
        {
            Active = false;
            capturedBodies.Clear();
            CaptureCount = 0;
            OverspeedCaptureCount = 0;
        }
    }
}
