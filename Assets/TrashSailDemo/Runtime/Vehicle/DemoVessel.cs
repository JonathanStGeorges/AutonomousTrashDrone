using Marus.Ocean;
using UnityEngine;

namespace TrashSailDemo
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DemoVessel : MonoBehaviour, IVehicleActuator
    {
        public DemoConfig Config;
        public Transform CameraMount, SonarMount, VisualRoot, SailVisual, RudderVisual;
        public DemoCollector Collector;
        public Rigidbody Body;
        public WaterHeightSampler WaterSampler;
        public ActuatorCommand LastCommand;
        public float ForwardSpeed { get; private set; }
        public float SailDrive { get; private set; }
        public float MotorDrive { get; private set; }
        public float MotorUseSeconds { get; private set; }
        public int CollisionCount { get; private set; }
        public event System.Action<Collision> Collided;

        void Awake()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            ConfigureBody();
            LastCommand = ActuatorCommand.Safe(Time.timeAsDouble);
        }

        public void ConfigureBody()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            if (Config == null) return;
            Body.mass = Mathf.Max(1, Config.VesselMass);
            Body.centerOfMass = Config.CentreOfMass;
            Body.useGravity = true;
            Body.linearDamping = 0;
            Body.angularDamping = 0.3f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 1.2f;
        }

        public void ApplyCommand(ActuatorCommand command)
        {
            LastCommand = DemoMath.ClampCommand(command, Time.timeAsDouble, Config != null ? Config.CommandTimeout : 0.5f);
            if (Config != null && !Config.MotorAssistance) LastCommand.Motor = 0;
            if (Collector != null && !LastCommand.CollectorEnabled) Collector.Active = false;
        }

        // A dimensionless, intentionally approximate true-wind polar. No forward
        // drive in the upwind cone. Reach is strongest; running is weaker.
        public static float SailPolar(float angleFromUpwindDegrees, float noGoDegrees)
        {
            float angle = Mathf.Abs(Mathf.DeltaAngle(0, angleFromUpwindDegrees));
            if (angle <= noGoDegrees) return 0;
            float exit = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(noGoDegrees, noGoDegrees + 18, angle));
            float reach = 0.55f + 0.45f * Mathf.Sin(angle * Mathf.Deg2Rad);
            return exit * reach;
        }

        void FixedUpdate()
        {
            if (Config == null || Body == null) return;
            LastCommand = DemoMath.ClampCommand(LastCommand, Time.timeAsDouble, Config.CommandTimeout);
            if (!Config.MotorAssistance) LastCommand.Motor = 0;
            float halfWidth = Config.VesselWidth * 0.37f;
            float halfLength = Config.VesselLength * 0.35f;
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                {
                    Vector3 point = transform.TransformPoint(new Vector3(side * halfWidth, 0, end * halfLength));
                    float surface = WaterSampler != null ? WaterSampler.GetWaterLevel(point) : 0;
                    float weight = Body.mass * Physics.gravity.magnitude / 4;
                    float force = weight + (surface - point.y) * weight * 5 - Body.GetPointVelocity(point).y * Body.mass * 0.65f;
                    Body.AddForceAtPosition(Vector3.up * Mathf.Clamp(force, 0, weight * 3), point, ForceMode.Force);
                }
            Vector3 localVelocity = transform.InverseTransformDirection(Body.linearVelocity);
            ForwardSpeed = localVelocity.z;
            Vector3 drag = new Vector3(-localVelocity.x * (95 + 40 * Mathf.Abs(localVelocity.x)),
                -localVelocity.y * 15, -localVelocity.z * (18 + 24 * Mathf.Abs(localVelocity.z)));
            Body.AddRelativeForce(drag, ForceMode.Force);
            Body.AddTorque(Vector3.up * (-Body.angularVelocity.y * (110 + 50 * Mathf.Abs(Body.angularVelocity.y))), ForceMode.Force);
            Vector3 wind = DemoMath.Flat(Config.WindTo);
            float windSpeed = wind.magnitude;
            float upwindAngle = windSpeed > 0.001f ? Vector3.Angle(DemoMath.Flat(transform.forward), -wind) : 0;
            SailDrive = windSpeed < 0.5f ? 0 : Config.MaxSailForce * SailPolar(upwindAngle, Config.NoGoAngle)
                * Mathf.Clamp(windSpeed * windSpeed / 25f, 0, 2) * LastCommand.SailTrim;
            MotorDrive = Config.MotorAssistance ? Config.MaxMotorForce * LastCommand.Motor : 0;
            Body.AddForce(DemoMath.Flat(transform.forward).normalized * (SailDrive + MotorDrive), ForceMode.Force);
            // Rudder effectiveness scales with actual water speed. The small sail-
            // induced floor represents flow across the control surface, not heading teleportation.
            float rudderAuthority = Mathf.Clamp(Mathf.Abs(ForwardSpeed) / 0.7f, 0, 1.2f);
            Body.AddTorque(Vector3.up * Config.RudderTorque * LastCommand.Rudder * rudderAuthority, ForceMode.Force);
            if (MotorDrive > 0.01f) MotorUseSeconds += Time.fixedDeltaTime;
            if (Collector != null) Collector.Active = LastCommand.CollectorEnabled && DemoMath.Flat(Body.linearVelocity).magnitude <= Config.MaxCollectionSpeed;
            if (SailVisual != null) SailVisual.localRotation = Quaternion.Euler(0, 10 + (1 - LastCommand.SailTrim) * 70, 0);
            if (RudderVisual != null) RudderVisual.localRotation = Quaternion.Euler(0, LastCommand.Rudder * 30, 0);
        }

        public void ResetPose(Vector3 position, Quaternion rotation)
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            Body.position = position;
            Body.rotation = rotation;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            LastCommand = ActuatorCommand.Safe(Time.timeAsDouble);
            ForwardSpeed = SailDrive = MotorDrive = MotorUseSeconds = 0;
            CollisionCount = 0;
            if (Collector != null) Collector.ResetCollector();
        }

        void OnCollisionEnter(Collision collision)
        {
            CollisionCount++;
            Collided?.Invoke(collision);
        }
    }
}
