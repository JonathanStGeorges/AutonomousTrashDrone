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
        [HideInInspector] public bool MotorAssistance;
        [HideInInspector] public float MaxMotorForce = 110, MaxSailForce = 90, RudderTorque = 100;
        public Vector3 WindTo = new Vector3(5, 0, 0);
        [HideInInspector] public float NoGoAngle = 40;
        public float CommandTimeout = 0.5f;
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
        [Header("Production pretrained inference")]
        public string InferenceUrl="http://127.0.0.1:8765/infer",InferenceProfile="combined";
        public float InferenceWallMaxAge=3,InferenceRequestHz=3,ModelScoreThreshold=.25f;
        [Header("Provisional twin thrusters — not confirmed hardware")]
        public Vector3 PortThrusterPosition=new Vector3(-.85f,-.2f,-1.1f),StarboardThrusterPosition=new Vector3(.85f,-.2f,-1.1f);
        public Vector3 PortThrusterEuler=Vector3.zero,StarboardThrusterEuler=Vector3.zero;
        public float MaxThrusterForce=80,ThrusterResponseRate=2,MaxSearchSpeed=.65f,ApproachSpeed=.3f,TurnRateDegrees=28;
        public bool ReverseThrustEnabled=true;
        public Vector3 CurrentVelocity=Vector3.zero;
        [Header("Coverage and ocean")]
        public bool LargeOcean;
        public float MissionWidth=1000,MissionLength=1000,LaneSpacing=6,UsefulDetectionRange=5,StreamingRadius=100;
        public int OceanRubbishCount=350,OceanFishCount=16,OceanBoatCount=5;
        public int StopAfterCaptures=0;
        public bool ReachableDemoSeed=true;
        public float CoverageTurnRadius=3,WaypointArrivalRadius=1,WindDisturbanceCoefficient=0;
        public float EnvelopeHalfWidth=>Mathf.Max(VesselWidth*.5f,Mathf.Abs(PortThrusterPosition.x)+.15f,Mathf.Abs(StarboardThrusterPosition.x)+.15f);
        public float EnvelopeRear=>Mathf.Min(-VesselLength*.5f,PortThrusterPosition.z-.2f,StarboardThrusterPosition.z-.2f);
        public float EnvelopeFront=>Mathf.Max(VesselLength*.5f+.7f,PortThrusterPosition.z+.2f,StarboardThrusterPosition.z+.2f);
    }
}
