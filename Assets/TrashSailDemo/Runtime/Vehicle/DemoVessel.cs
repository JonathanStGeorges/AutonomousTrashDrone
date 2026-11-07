using Marus.Ocean;
using UnityEngine;

namespace TrashSailDemo
{
    // Adapted from MARUS Thruster's AddForceAtPosition model, with explicit mounts,
    // bounded response, local watchdog and no ROS/logger dependency.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DemoVessel : MonoBehaviour, IVehicleActuator
    {
        public DemoConfig Config;
        public Transform CameraMount,SonarMount,VisualRoot,SailVisual,RudderVisual;
        public Transform PortMount,StarboardMount;
        public DemoCollector Collector;
        public Rigidbody Body;
        public WaterHeightSampler WaterSampler;
        public ActuatorCommand LastCommand;
        public float ForwardSpeed {get;private set;}
        public float PortCommand=>LastCommand.PortThrust;
        public float StarboardCommand=>LastCommand.StarboardThrust;
        public float PortAppliedForce {get;private set;}
        public float StarboardAppliedForce {get;private set;}
        public bool ActuatorHealthy {get;private set;}
        public bool LayoutValid {get;private set;}=true;
        public float ThrusterUseSeconds {get;private set;}
        // Prototype serialization migration only; no sail propulsion is applied.
        public float MotorUseSeconds=>ThrusterUseSeconds;
        public float MotorDrive=>PortAppliedForce+StarboardAppliedForce;
        public float SailDrive=>0;
        public int CollisionCount {get;private set;}
        public event System.Action<Collision> Collided;
        double receivedAt;
        float portApplied,starboardApplied;
        void Awake(){Body=GetComponent<Rigidbody>();ConfigureBody();LastCommand=ActuatorCommand.Safe(Time.timeAsDouble);receivedAt=Time.timeAsDouble;}
        void Start(){ConfigureBody();ConfigureThrusters();}
        public void ConfigureBody()
        {
            if(Body==null)Body=GetComponent<Rigidbody>();if(Config==null)return;
            Body.mass=Mathf.Max(1,Config.VesselMass);Body.centerOfMass=Config.CentreOfMass;Body.useGravity=true;
            Body.linearDamping=0;Body.angularDamping=.3f;Body.interpolation=RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;Body.maxAngularVelocity=1.2f;
        }
        public void ConfigureThrusters()
        {
            if(Config==null)return;
            if(PortMount==null)PortMount=Mount("Port thruster force mount");
            if(StarboardMount==null)StarboardMount=Mount("Starboard thruster force mount");
            PortMount.localPosition=Config.PortThrusterPosition;PortMount.localRotation=Quaternion.Euler(Config.PortThrusterEuler);
            StarboardMount.localPosition=Config.StarboardThrusterPosition;StarboardMount.localRotation=Quaternion.Euler(Config.StarboardThrusterEuler);
            // Force authority is evaluated in the vessel frame.
            Vector3 p=PortMount.localRotation*Vector3.forward,s=StarboardMount.localRotation*Vector3.forward;
            float c=Vector3.Cross(Config.PortThrusterPosition-Config.CentreOfMass,p).y,d=Vector3.Cross(Config.StarboardThrusterPosition-Config.CentreOfMass,s).y;
            LayoutValid=Mathf.Abs(p.z*d-s.z*c)>=.001f;
        }
        Transform Mount(string label)
        {
            Transform mount=new GameObject(label).transform;mount.SetParent(transform,false);mount.gameObject.layer=8;
            GameObject housing=GameObject.CreatePrimitive(PrimitiveType.Cylinder);housing.name="Thruster housing (visual only)";housing.layer=8;
            housing.transform.SetParent(mount,false);housing.transform.localRotation=Quaternion.Euler(90,0,0);housing.transform.localScale=new Vector3(.22f,.18f,.22f);
            var collider=housing.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            housing.GetComponent<Renderer>().sharedMaterial=DemoWorld.MakeMaterial("Thruster housing",new Color(.12f,.16f,.19f));
            return mount;
        }
        public void ApplyCommand(ActuatorCommand command)
        {
            LastCommand=DemoMath.ClampCommand(command,Time.timeAsDouble,Config!=null?Config.CommandTimeout:.5f);
            if(Config!=null && !Config.ReverseThrustEnabled){LastCommand.PortThrust=Mathf.Max(0,LastCommand.PortThrust);LastCommand.StarboardThrust=Mathf.Max(0,LastCommand.StarboardThrust);}
            receivedAt=command.IssuedAt;if(Collector!=null && !LastCommand.CollectorEnabled)Collector.Active=false;
        }
        public static ActuatorCommand Allocate(float surge,float yaw,bool reverse,double now)
        {
            float minimum=reverse?-1:0;
            return new ActuatorCommand{PortThrust=Mathf.Clamp(surge+yaw,minimum,1),StarboardThrust=Mathf.Clamp(surge-yaw,minimum,1),IssuedAt=now};
        }
        public static ActuatorCommand AllocateConfigured(float surge,float yaw,DemoConfig config,double now)
        {
            Vector3 p=Quaternion.Euler(config.PortThrusterEuler)*Vector3.forward,s=Quaternion.Euler(config.StarboardThrusterEuler)*Vector3.forward;
            float a=p.z,b=s.z,c=Vector3.Cross(config.PortThrusterPosition-config.CentreOfMass,p).y,d=Vector3.Cross(config.StarboardThrusterPosition-config.CentreOfMass,s).y;
            float determinant=a*d-b*c;
            if(Mathf.Abs(determinant)<.001f)return ActuatorCommand.Safe(now); // layout cannot independently command surge and yaw
            float force=surge*(Mathf.Abs(a)+Mathf.Abs(b)),torque=yaw*(Mathf.Abs(c)+Mathf.Abs(d));
            float port=(force*d-b*torque)/determinant,starboard=(a*torque-force*c)/determinant;
            float scale=Mathf.Max(1,Mathf.Max(Mathf.Abs(port),Mathf.Abs(starboard)));
            float minimum=config.ReverseThrustEnabled?-1:0;
            return new ActuatorCommand{PortThrust=Mathf.Clamp(port/scale,minimum,1),StarboardThrust=Mathf.Clamp(starboard/scale,minimum,1),IssuedAt=now};
        }
        public static ActuatorCommand Control(VehicleState state,float desiredSpeed,float desiredHeading,DemoConfig config,double now)
        {
            float error=Mathf.DeltaAngle(DemoMath.Heading(state.Forward),desiredHeading);
            float turnRate=Mathf.Clamp(error*.035f,-config.TurnRateDegrees*Mathf.Deg2Rad,config.TurnRateDegrees*Mathf.Deg2Rad);
            float speedGoal=desiredSpeed*Mathf.Clamp01(1-Mathf.Abs(error)/75);
            float forward=Vector3.Dot(DemoMath.Flat(state.Velocity),state.Forward);
            float feed=speedGoal*(18+24*Mathf.Abs(speedGoal))/Mathf.Max(1,2*config.MaxThrusterForce);
            float surge=(speedGoal-forward)*.85f+feed;
            float yaw=Mathf.Clamp((turnRate-state.AngularVelocity.y)*.9f,-.65f,.65f);
            var result=AllocateConfigured(surge,yaw,config,now);result.RequestedSpeed=desiredSpeed;result.RequestedHeading=desiredHeading;return result;
        }
        void FixedUpdate()
        {
            if(Config==null || Body==null)return;
            if(PortMount==null || StarboardMount==null)ConfigureThrusters();
            ActuatorHealthy=LayoutValid && Time.timeAsDouble-receivedAt<=Config.CommandTimeout && Time.timeAsDouble>=receivedAt;
            if(!ActuatorHealthy)LastCommand=ActuatorCommand.Safe(Time.timeAsDouble);
            float minimum=Config.ReverseThrustEnabled?-1:0;
            float p=Mathf.Clamp(LastCommand.PortThrust,minimum,1),s=Mathf.Clamp(LastCommand.StarboardThrust,minimum,1);
            portApplied=Mathf.MoveTowards(portApplied,p,Config.ThrusterResponseRate*Time.fixedDeltaTime);
            starboardApplied=Mathf.MoveTowards(starboardApplied,s,Config.ThrusterResponseRate*Time.fixedDeltaTime);
            PortAppliedForce=portApplied*Config.MaxThrusterForce;StarboardAppliedForce=starboardApplied*Config.MaxThrusterForce;
            Body.AddForceAtPosition(PortMount.forward*PortAppliedForce,PortMount.position,ForceMode.Force);
            Body.AddForceAtPosition(StarboardMount.forward*StarboardAppliedForce,StarboardMount.position,ForceMode.Force);
            if(Mathf.Abs(PortAppliedForce)+Mathf.Abs(StarboardAppliedForce)>.01f)ThrusterUseSeconds+=Time.fixedDeltaTime;
            float halfWidth=Config.VesselWidth*.37f,halfLength=Config.VesselLength*.35f;
            for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)
            {
                Vector3 point=transform.TransformPoint(new Vector3(side*halfWidth,0,end*halfLength));
                float surface=WaterSampler!=null?WaterSampler.GetWaterLevel(point):0,weight=Body.mass*Physics.gravity.magnitude/4;
                float force=weight+(surface-point.y)*weight*5-Body.GetPointVelocity(point).y*Body.mass*.65f;
                Body.AddForceAtPosition(Vector3.up*Mathf.Clamp(force,0,weight*3),point,ForceMode.Force);
            }
            Vector3 local=transform.InverseTransformDirection(Body.linearVelocity-Config.CurrentVelocity);ForwardSpeed=Vector3.Dot(Body.linearVelocity,transform.forward);
            Body.AddRelativeForce(new Vector3(-local.x*(95+40*Mathf.Abs(local.x)),-local.y*15,-local.z*(18+24*Mathf.Abs(local.z))),ForceMode.Force);
            Body.AddTorque(Vector3.up*(-Body.angularVelocity.y*(110+50*Mathf.Abs(Body.angularVelocity.y))),ForceMode.Force);
            Body.AddForce(DemoMath.Flat(Config.WindTo)*Config.WindTo.magnitude*Config.WindDisturbanceCoefficient,ForceMode.Force);
            if(Collector!=null)Collector.Active=LastCommand.CollectorEnabled && ActuatorHealthy && DemoMath.Flat(Body.linearVelocity).magnitude<=Config.MaxCollectionSpeed;
        }
        public void ResetPose(Vector3 position,Quaternion rotation)
        {
            if(Body==null)Body=GetComponent<Rigidbody>();Body.position=position;Body.rotation=rotation;
            // Explicit scenario reset synchronizes the pose immediately for streaming
            // and capture-time calibration, even before the next interpolated step.
            transform.SetPositionAndRotation(position,rotation);Physics.SyncTransforms();
            Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;
            LastCommand=ActuatorCommand.Safe(Time.timeAsDouble);receivedAt=Time.timeAsDouble;portApplied=starboardApplied=PortAppliedForce=StarboardAppliedForce=0;
            ForwardSpeed=ThrusterUseSeconds=0;CollisionCount=0;if(Collector!=null)Collector.ResetCollector();
        }
        void OnCollisionEnter(Collision collision){CollisionCount++;Collided?.Invoke(collision);}
    }
}
