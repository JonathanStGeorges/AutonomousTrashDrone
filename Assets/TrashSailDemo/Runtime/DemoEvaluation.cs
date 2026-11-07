using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TrashSailDemo
{
    // PRIVILEGED EVALUATOR. Never supplies truth to mission/perception/actuators.
    public sealed class DemoEvaluation : MonoBehaviour
    {
        public DemoRuntime Runtime;
        public string OutputDirectory;
        public int CorrectCaptures, Bycatch, CollisionEvents, FishCollisions, Duplicates;
        public float MinimumHazardSeparation=float.PositiveInfinity, MaximumUpwindProgress;
        public bool UnsafeCollection, SawFreshCamera, SawFreshSonar, SawAvoidance, SawCollectorDisabledOnHazard, SawDegraded;
        public float CameraHzSum,SonarHzSum,FpsSum;
        public int MetricSamples;
        public List<string> Decisions=new List<string>();
        public List<float> CaptureTimes=new List<float>();
        public List<float> CaptureInitialZ=new List<float>();
        public int MeasuredTacks;
        public double Began;
        readonly HashSet<GameObject> captured=new HashSet<GameObject>();
        double nextLog;
        StreamWriter writer;
        int lastTackSide;
        [Serializable] sealed class LogRow
        {
            public string kind,scenario,mode,state,reason,associationGate,revision;
            public int seed,captures,bycatch,visualTracks,sonarTracks,target;
            public double time,cameraAge,sonarAge;
            public long cameraSequence,sonarSequence;
            public double cameraCapture,cameraComplete,sonarCapture,sonarComplete;
            public Vector3 estimatedPosition,estimatedVelocity,wind;
            public ActuatorCommand actuators;
            public float clearance,cameraHz,sonarHz,fps;
            public List<TrackLog> camera=new List<TrackLog>(),sonar=new List<TrackLog>();
        }
        [Serializable] sealed class TrackLog { public int id,hits;public string label;public Vector3 position,velocity;public float uncertainty,score,lateralExtent,rangeExtent;public double age; }
        void Start()
        {
            if(string.IsNullOrEmpty(OutputDirectory))OutputDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/TrashSailDemo/interactive"));
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-trashOutput")OutputDirectory=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(OutputDirectory);
            Runtime.Vessel.Collector.Captured+=OnCaptured; Runtime.Vessel.Collided+=OnCollision;
            Runtime.Mission.Decision+=OnDecision; Runtime.ScenarioReset+=ResetEvaluation;
            ResetEvaluation();
        }
        public void ResetEvaluation()
        {
            writer?.Dispose(); Directory.CreateDirectory(OutputDirectory);
            writer=new StreamWriter(Path.Combine(OutputDirectory,Runtime.Config.Scenario+"-"+Runtime.Config.Seed+"-"+DateTime.UtcNow.ToString("HHmmssfff")+".jsonl")){AutoFlush=true};
            CorrectCaptures=Bycatch=CollisionEvents=FishCollisions=Duplicates=0;captured.Clear();CaptureTimes.Clear();CaptureInitialZ.Clear();Decisions.Clear();MeasuredTacks=0;lastTackSide=0;
            MinimumHazardSeparation=float.PositiveInfinity;MaximumUpwindProgress=0;UnsafeCollection=false;
            SawFreshCamera=SawFreshSonar=SawAvoidance=SawCollectorDisabledOnHazard=SawDegraded=false;
            CameraHzSum=SonarHzSum=FpsSum=0;MetricSamples=0; Began=Time.timeAsDouble;nextLog=0;
            Write("configuration",Runtime.SoftwareRevision+"; contract v1; physics nondeterministic; seed="+Runtime.Config.Seed);
            File.WriteAllText(Path.Combine(OutputDirectory,"configuration-"+Runtime.Config.Scenario+".json"),JsonUtility.ToJson(Runtime.Config,true));
        }
        void OnCaptured(GameObject body)
        {
            if(!captured.Add(body)){Duplicates++;return;}
            var truth=body.GetComponent<DemoActor>();
            if(truth!=null && truth.Kind=="trash")CorrectCaptures++;else Bycatch++;
            CaptureInitialZ.Add(truth!=null?truth.InitialPosition.z:-999);
            CaptureTimes.Add((float)(Time.timeAsDouble-Began)); Write("physical_capture",truth!=null?truth.Kind:"unlabelled body");
            Capture("capture-"+CorrectCaptures);
        }
        void OnCollision(Collision collision)
        { CollisionEvents++;var truth=collision.gameObject.GetComponentInParent<DemoActor>();if(truth!=null && truth.Kind=="fish")FishCollisions++;Write("collision",truth!=null?truth.Kind:"unlabelled collider"); }
        void OnDecision(string state,string reason)
        { Decisions.Add(state);Write("state_change",reason); }
        public void Capture(string label)
        { ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory,Runtime.Config.Scenario+"-"+Runtime.Config.Seed+"-"+label+".png")); }
        void Update()
        {
            if(Runtime==null || Runtime.Config==null || writer==null)return;
            double now=Time.timeAsDouble;
            if(Runtime.Vessel.Collector.OverspeedCaptureCount>0)UnsafeCollection=true;
            float measuredHeading=Vector3.SignedAngle(-DemoMath.Flat(Runtime.OwnState.State.WindTo),DemoMath.Flat(Runtime.OwnState.State.Forward),Vector3.up);
            if(Mathf.Abs(measuredHeading)>20 && Mathf.Abs(measuredHeading)<100)
            {int side=measuredHeading>0?1:-1;if(lastTackSide!=0 && side!=lastTackSide)MeasuredTacks++;lastTackSide=side;}
            SawFreshCamera|=Runtime.CameraPerception.IsHealthy;SawFreshSonar|=Runtime.SonarPerception.IsHealthy;
            if(Runtime.Mission.Status.Phase==MissionPhase.AVOID_OR_REPLAN)
            {SawAvoidance=true;SawCollectorDisabledOnHazard|=!Runtime.Vessel.Collector.Active;}
            SawDegraded|=Runtime.Mission.Status.Phase==MissionPhase.SENSOR_DEGRADED;
            if(Runtime.Vessel.Collector.Active && (!Runtime.CameraPerception.IsHealthy || !Runtime.SonarPerception.IsHealthy))UnsafeCollection=true;
            MaximumUpwindProgress=Mathf.Max(MaximumUpwindProgress,Runtime.Vessel.transform.position.z);
            foreach(var actor in Runtime.World.Actors)
            {
                if(actor==null || actor.Captured || (actor.Kind!="fish" && actor.Kind!="obstacle"))continue;
                Collider collider=actor.GetComponent<Collider>();if(collider==null)continue;
                Vector3 local=Runtime.Vessel.transform.InverseTransformPoint(collider.bounds.center);
                // Conservative horizontal hull+intake OBB envelope; contact radius uses
                // horizontal AABB circumscription, so clearance is not centre distance.
                float radius=new Vector2(collider.bounds.extents.x,collider.bounds.extents.z).magnitude;
                float dx=Mathf.Max(0,Mathf.Abs(local.x)-Runtime.Config.VesselWidth*0.5f);
                float dz=Mathf.Max(0,Mathf.Max(-Runtime.Config.VesselLength*0.5f-local.z,local.z-(Runtime.Config.VesselLength*0.5f+0.7f)));
                MinimumHazardSeparation=Mathf.Min(MinimumHazardSeparation,Mathf.Sqrt(dx*dx+dz*dz)-radius);
            }
            if(now>=nextLog)
            {nextLog=now+0.5;CameraHzSum+=Runtime.CameraPerception.ProcessingHz;SonarHzSum+=Runtime.SonarPerception.ProcessingHz;FpsSum+=Runtime.RenderingFps;MetricSamples++;Write("telemetry",Runtime.Mission.Status.Reason);}
        }
        void Write(string kind,string reason)
        {
            if(writer==null || Runtime==null)return;
            var state=Runtime.OwnState.State;double now=Time.timeAsDouble;
            var row=new LogRow{kind=kind,scenario=Runtime.Config.Scenario.ToString(),seed=Runtime.Config.Seed,time=now-Began,revision=Runtime.SoftwareRevision,
                mode=Runtime.Config.MotorAssistance?"MOTOR ASSIST + approximate sail":"SAIL ONLY",state=Runtime.Mission.Status.Phase.ToString(),reason=reason,associationGate=Runtime.Mission.Status.AssociationGate,
                captures=CorrectCaptures,bycatch=Bycatch,visualTracks=Runtime.CameraPerception.Tracks.Count,sonarTracks=Runtime.SonarPerception.Tracks.Count,
                target=Runtime.Mission.Status.TargetId,estimatedPosition=state.Position,estimatedVelocity=state.Velocity,wind=state.WindTo,actuators=Runtime.Vessel.LastCommand,
                cameraAge=Runtime.CameraPerception.LatestFrame==null?999:now-Runtime.CameraPerception.LatestFrame.CaptureTime,
                sonarAge=Runtime.SonarPerception.LatestFrame==null?999:now-Runtime.SonarPerception.LatestFrame.CaptureTime,
                cameraSequence=Runtime.CameraPerception.LatestFrame?.Sequence??0,sonarSequence=Runtime.SonarPerception.LatestFrame?.Sequence??0,
                cameraCapture=Runtime.CameraPerception.LatestFrame?.CaptureTime??-1,cameraComplete=Runtime.CameraPerception.LatestFrame?.CompletionTime??-1,
                sonarCapture=Runtime.SonarPerception.LatestFrame?.CaptureTime??-1,sonarComplete=Runtime.SonarPerception.LatestFrame?.CompletionTime??-1,
                clearance=float.IsInfinity(Runtime.Mission.Status.ClosestClearance)?999:Runtime.Mission.Status.ClosestClearance,
                cameraHz=Runtime.CameraPerception.ProcessingHz,sonarHz=Runtime.SonarPerception.ProcessingHz,fps=Runtime.RenderingFps};
            foreach(var track in Runtime.CameraPerception.Tracks)row.camera.Add(new TrackLog{id=track.Id,label=track.Label,position=track.Position,velocity=track.Velocity,uncertainty=track.Uncertainty,score=track.Score,age=track.Age(now)});
            foreach(var track in Runtime.SonarPerception.Tracks)row.sonar.Add(new TrackLog{id=track.Id,hits=track.Hits,label="unknown",position=track.Position,velocity=track.Velocity,uncertainty=track.Uncertainty,lateralExtent=track.LateralExtent,rangeExtent=track.RangeExtent,age=track.Age(now)});
            writer.WriteLine(JsonUtility.ToJson(row));
        }
        void OnDestroy(){writer?.Dispose();}
    }
}
