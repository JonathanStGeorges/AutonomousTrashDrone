using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    // This is the simulated GNSS/IMU/wind sensor boundary, allowed to sample own truth.
    [DefaultExecutionOrder(-100)]
    public sealed class DemoOwnState : MonoBehaviour, IOwnStateSource
    {
        public DemoConfig Config;
        public DemoVessel Vessel;
        public VehicleState State { get; private set; }
        readonly Queue<VehicleState> samples = new Queue<VehicleState>();
        System.Random random = new System.Random(17);

        void Awake() { if (Vessel == null) Vessel = GetComponent<DemoVessel>(); ResetState(Config != null ? Config.Seed : 17); }
        public void ResetState(int seed)
        {
            random = new System.Random(seed ^ 0x52AD);
            samples.Clear();
            State = Measure();
        }
        float Noise() => Config == null ? 0 : ((float)random.NextDouble() * 2 - 1) * Config.StateNoise;
        VehicleState Measure()
        {
            Transform pose = Vessel != null ? Vessel.transform : transform;
            return new VehicleState
            {
                Position = pose.position + new Vector3(Noise(), 0, Noise()),
                Rotation = Quaternion.AngleAxis(Noise(), Vector3.up) * pose.rotation,
                Velocity = Vessel != null && Vessel.Body != null ? Vessel.Body.linearVelocity + new Vector3(Noise(), 0, Noise()) : Vector3.zero,
                WindTo = Config != null ? Config.WindTo + new Vector3(Noise(), 0, Noise()) : Vector3.zero,
                Timestamp = Time.timeAsDouble,
                WaterLevel = 0,
                Ideal = Config == null || (Config.StateNoise == 0 && Config.StateLatency == 0)
            };
        }
        void FixedUpdate()
        {
            samples.Enqueue(Measure());
            double cutoff = Time.timeAsDouble - (Config != null ? Config.StateLatency : 0);
            while (samples.Count > 0 && samples.Peek().Timestamp <= cutoff) State = samples.Dequeue();
            while (samples.Count > 256) samples.Dequeue();
        }
    }
}
