using UnityEngine;

namespace TrashSailDemo
{
    // Truth and scripted actor motion belong to simulation/evaluation only.
    // Perception and mission code must not read this component.
    public sealed class DemoActor : MonoBehaviour
    {
        public string Kind = "trash";
        public string AssetKey;
        public int WorldRecordId = -1;
        public bool Captured;
        public Vector3 InitialPosition;
        public Rigidbody Body;
        public bool Floating, Crossing;
        public float CrossingSpeed = 0.65f, CrossingHalfWidth = 6;
        public bool RepeatCrossing;
        public float CrossingEndX = 12;
        public Vector3 DriftVelocity;
        public double SpawnTime;
        public float MotionPhase;

        void Awake() { Body = GetComponent<Rigidbody>(); }
        void FixedUpdate()
        {
            if (Captured || Body == null) return;
            if (Crossing)
            {
                float elapsed = (float)(Time.timeAsDouble - SpawnTime);
                float travelled = InitialPosition.x + CrossingHalfWidth + elapsed * CrossingSpeed;
                float x = RepeatCrossing ? -CrossingHalfWidth + Mathf.PingPong(travelled, CrossingHalfWidth * 2)
                    : Mathf.Min(CrossingEndX, InitialPosition.x + elapsed * CrossingSpeed);
                // Configured crossing path, independent of vessel position.
                Body.MovePosition(new Vector3(x, InitialPosition.y, InitialPosition.z));
                return;
            }
            if (Floating && !Body.isKinematic)
            {
                float verticalForce = Body.mass * Physics.gravity.magnitude
                    + (InitialPosition.y - Body.position.y) * Body.mass * 35 - Body.linearVelocity.y * Body.mass * 10;
                Body.AddForce(Vector3.up * verticalForce, ForceMode.Force);
                Body.AddForce((DriftVelocity - DemoMath.Flat(Body.linearVelocity)) * Body.mass * 2, ForceMode.Force);
            }
            else if (Body.isKinematic && DriftVelocity.sqrMagnitude > 0)
                Body.MovePosition(Body.position + DriftVelocity * Time.fixedDeltaTime);
        }
    }
}
