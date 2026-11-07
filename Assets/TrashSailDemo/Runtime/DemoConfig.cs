using UnityEngine;
namespace TrashSailDemo
{
    [CreateAssetMenu(menuName = "Trash Sail Demo/Configuration")]
    public sealed class DemoConfig : ScriptableObject
    {
        public int Seed = 17;
        public DemoScenario Scenario = DemoScenario.ClearCollection;
        public float VesselLength = 3f, VesselWidth = 2.2f, VesselMass = 110f;
        public Vector3 CentreOfMass = new Vector3(0, -0.15f, 0);
        public float IntakeWidth = 1.6f, Capacity = 8;
        public bool MotorAssistance = true;
        public float MaxMotorForce = 110, MaxSailForce = 90, RudderTorque = 100;
        public Vector3 WindTo = new Vector3(5, 0, 0);
        public float NoGoAngle = 40, CommandTimeout = 0.5f;
        public int CameraWidth = 320, CameraHeight = 192;
        public float CameraHz = 10, CameraLatency = 0.04f, CameraDropout;
        public float SonarHz = 8, SonarMinRange = 0.18f, SonarMaxRange = 16, SonarFov = 110, SonarVerticalFov = 35;
        public int SonarBearings = 96, SonarVerticalRays = 9, SonarRangeBins = 128;
        public float SonarNoise = 0.01f, SonarDropout = 0.02f, SonarLatency = 0.08f;
        public float CameraMaxAge = 0.8f, SonarMaxAge = 0.7f, TrackMemory = 8f;
        public float StateNoise = 0, StateLatency = 0;
        public float BoundaryHalfWidth = 18, BoundaryLength = 40;
        public float SafeMargin = 1.2f, ClearancePeriod = 2f, MaxCollectionSpeed = 1.3f;
        public float TimeBudget = 300;
    }
}
