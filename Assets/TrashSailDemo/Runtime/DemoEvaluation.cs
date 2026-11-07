using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace TrashSailDemo
{
    // PRIVILEGED EVALUATOR: scoring/telemetry only, never operational observations.
    public sealed class DemoEvaluation : MonoBehaviour
    {
        public DemoRuntime Runtime;
        public string OutputDirectory;
        public int CorrectCaptures,Bycatch,CollisionEvents,FishCollisions,Duplicates;
        public float MinimumHazardSeparation=float.PositiveInfinity;
        public bool UnsafeCollection,SawFreshCamera,SawFreshSonar,SawAvoidance,SawCollectorDisabledOnHazard,SawDegraded,SawNeuralDetection;
        public float CameraHzSum,SonarHzSum,FpsSum,MaxFrameMilliseconds;
        public int MetricSamples,RenderedFrames,FrameSpikes50ms;
        public List<string> Decisions=new List<string>();
        public List<float> CaptureTimes=new List<float>(),CaptureInitialZ=new List<float>();
        public double Began;
        readonly HashSet<GameObject> captured=new HashSet<GameObject>();
        double nextLog;
        StreamWriter writer;
        [Serializable] sealed class LogRow
        {
            public string kind,scenario,mode="TWIN THRUSTERS (provisional geometry)",state,reason,associationGate,revision,profile,serviceStatus;
            public int seed,captures,bycatch,collisions,visualTracks,sonarTracks,target,coverageWaypoint,coverageWaypoints,activeWorldActors,renderedFrames,frameSpikes50ms;
            public bool coverageSuspended,actuatorHealthy,cameraHealthy,sonarHealthy,largeOcean;
            public double time,wallTime,cameraAge,sonarAge;
            public long cameraSequence,sonarSequence,allocatedMemoryBytes,reservedMemoryBytes,managedMemoryBytes;
            public double cameraCapture,cameraComplete,sonarCapture,sonarComplete;
            public Vector3 estimatedPosition,estimatedVelocity,wind;
            public ActuatorCommand actuators;
            public float portForceN,starboardForceN,clearance,evaluatorMinimumSeparation,cameraHz,sonarHz,fps,frameMilliseconds,maxFrameMilliseconds,inferenceMilliseconds;
            public List<TrackLog> camera=new List<TrackLog>(),sonar=new List<TrackLog>();
        }
        [Serializable] sealed class TrackLog
        {
            public int id,hits;public string label,category,provenance,profile;
            public bool eligible,conflict,rangeValid;
            public Vector3 position,velocity;
            public float uncertainty,score,lateralExtent,rangeExtent;
            public double age,resultWallAge;
            public ModelEvidence[] evidence;
        }
        void Start()
        {
            if(string.IsNullOrEmpty(OutputDirectory) || OutputDirectory.Replace('\\','/').EndsWith("/interactive"))OutputDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/TrashSailDemo/production"));
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-trashOutput")OutputDirectory=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(OutputDirectory);
            Runtime.Vessel.Collector.Captured+=OnCaptured;Runtime.Vessel.Collided+=OnCollision;
            Runtime.Mission.Decision+=OnDecision;Runtime.ScenarioReset+=ResetEvaluation;
            ResetEvaluation();
        }
        public void ResetEvaluation()
        {
            if(string.IsNullOrEmpty(OutputDirectory))return;
            writer?.Dispose();Directory.CreateDirectory(OutputDirectory);
            string name=Runtime.Config.Scenario+"-"+Runtime.Config.Seed+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            writer=new StreamWriter(Path.Combine(OutputDirectory,name+".jsonl")){AutoFlush=true};
            CorrectCaptures=Bycatch=CollisionEvents=FishCollisions=Duplicates=0;captured.Clear();CaptureTimes.Clear();CaptureInitialZ.Clear();Decisions.Clear();
            MinimumHazardSeparation=float.PositiveInfinity;UnsafeCollection=false;
            SawFreshCamera=SawFreshSonar=SawAvoidance=SawCollectorDisabledOnHazard=SawDegraded=SawNeuralDetection=false;
            CameraHzSum=SonarHzSum=FpsSum=MaxFrameMilliseconds=0;MetricSamples=RenderedFrames=FrameSpikes50ms=0;Began=Time.timeAsDouble;nextLog=0;
            Write("configuration",Runtime.SoftwareRevision+"; production contract v2; physics nondeterministic; seed="+Runtime.Config.Seed);
            File.WriteAllText(Path.Combine(OutputDirectory,name+"-configuration.json"),JsonUtility.ToJson(Runtime.Config,true));
        }
        void OnCaptured(GameObject body)
        {
            if(!captured.Add(body)){Duplicates++;return;}
            var truth=body.GetComponent<DemoActor>();
            if(truth!=null && truth.Kind=="trash")CorrectCaptures++;else Bycatch++;
            CaptureInitialZ.Add(truth!=null?truth.InitialPosition.z:-999);
            CaptureTimes.Add((float)(Time.timeAsDouble-Began));Write("physical_capture",truth!=null?truth.Kind:"unlabelled body");Capture("capture-"+CorrectCaptures);
        }
        void OnCollision(Collision collision)
        {CollisionEvents++;var truth=collision.gameObject.GetComponentInParent<DemoActor>();if(truth!=null && truth.Kind=="fish")FishCollisions++;Write("collision",truth!=null?truth.Kind:"unlabelled collider");}
        void OnDecision(string state,string reason){Decisions.Add(state);Write("state_change",reason);}
        public void RecordEvent(string kind,string reason)=>Write(kind,reason);
        public void Capture(string label)=>ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory,Runtime.Config.Scenario+"-"+Runtime.Config.Seed+"-"+label+"-"+DateTime.UtcNow.ToString("HHmmssfff")+".png"));
        void Update()
        {
            if(Runtime==null || Runtime.Config==null || writer==null)return;
            double now=Time.timeAsDouble;
            RenderedFrames++;float frameMs=Time.unscaledDeltaTime*1000;MaxFrameMilliseconds=Mathf.Max(MaxFrameMilliseconds,frameMs);if(frameMs>50)FrameSpikes50ms++;
            if(Runtime.Vessel.Collector.OverspeedCaptureCount>0)UnsafeCollection=true;
            SawFreshCamera|=Runtime.CameraPerception.IsHealthy;SawFreshSonar|=Runtime.SonarPerception.IsHealthy;
            foreach(var track in Runtime.CameraPerception.Tracks)if(track.Evidence!=null && track.Evidence.Length>0)SawNeuralDetection=true;
            if(Runtime.Mission.Status.Phase==MissionPhase.AVOID_OR_REPLAN){SawAvoidance=true;SawCollectorDisabledOnHazard|=!Runtime.Vessel.Collector.Active;}
            SawDegraded|=Runtime.Mission.Status.Phase==MissionPhase.SENSOR_DEGRADED;
            if(Runtime.Vessel.Collector.Active && (!Runtime.CameraPerception.IsHealthy || !Runtime.SonarPerception.IsHealthy))UnsafeCollection=true;
            if(Runtime.Config.LargeOcean && Runtime.Population!=null)
                foreach(var actor in Runtime.Population.ActiveActors)MeasureClearance(actor);
            else if(Runtime.World!=null)foreach(var actor in Runtime.World.Actors)MeasureClearance(actor);
            if(now>=nextLog){nextLog=now+.5;CameraHzSum+=Runtime.CameraPerception.ProcessingHz;SonarHzSum+=Runtime.SonarPerception.ProcessingHz;FpsSum+=Runtime.RenderingFps;MetricSamples++;Write("telemetry",Runtime.Mission.Status.Reason);}
        }
        void MeasureClearance(DemoActor actor)
        {
            if(actor==null || actor.Captured || (actor.Kind!="fish" && actor.Kind!="obstacle" && actor.Kind!="boat"))return;
            Collider collider=actor.GetComponent<Collider>();if(collider==null || !collider.enabled)return;
            Vector3 local=Runtime.Vessel.transform.InverseTransformPoint(collider.bounds.center);
            // Conservative projected collider AABB radius against hull/intake/thruster envelope.
            float radius=new Vector2(collider.bounds.extents.x,collider.bounds.extents.z).magnitude;
            float dx=Mathf.Max(0,Mathf.Abs(local.x)-Runtime.Config.EnvelopeHalfWidth);
            float dz=Mathf.Max(0,Mathf.Max(Runtime.Config.EnvelopeRear-local.z,local.z-Runtime.Config.EnvelopeFront));
            MinimumHazardSeparation=Mathf.Min(MinimumHazardSeparation,Mathf.Sqrt(dx*dx+dz*dz)-radius);
        }
        void Write(string kind,string reason)
        {
            if(writer==null || Runtime==null)return;
            var state=Runtime.OwnState.State;double now=Time.timeAsDouble;var camera=Runtime.CameraPerception;var sonar=Runtime.SonarPerception;var coverage=Runtime.Coverage;
            var row=new LogRow{kind=kind,scenario=Runtime.Config.Scenario.ToString(),seed=Runtime.Config.Seed,time=now-Began,wallTime=Time.realtimeSinceStartupAsDouble,revision=Runtime.SoftwareRevision,
                state=Runtime.Mission.Status.Phase.ToString(),reason=reason,associationGate=Runtime.Mission.Status.AssociationGate,profile=camera.ActiveProfile,serviceStatus=camera.ServiceStatus,
                captures=CorrectCaptures,bycatch=Bycatch,collisions=CollisionEvents,visualTracks=camera.Tracks.Count,sonarTracks=sonar.Tracks.Count,
                target=Runtime.Mission.Status.TargetId,estimatedPosition=state.Position,estimatedVelocity=state.Velocity,wind=state.WindTo,actuators=Runtime.Vessel.LastCommand,
                portForceN=Runtime.Vessel.PortAppliedForce,starboardForceN=Runtime.Vessel.StarboardAppliedForce,actuatorHealthy=Runtime.Vessel.ActuatorHealthy,
                cameraAge=camera.LatestFrame==null?999:now-camera.LatestFrame.CaptureTime,sonarAge=sonar.LatestFrame==null?999:now-sonar.LatestFrame.CaptureTime,
                cameraSequence=camera.LatestFrame?.Sequence??0,sonarSequence=sonar.LatestFrame?.Sequence??0,
                cameraCapture=camera.LatestFrame?.CaptureTime??-1,cameraComplete=camera.LatestFrame?.CompletionTime??-1,sonarCapture=sonar.LatestFrame?.CaptureTime??-1,sonarComplete=sonar.LatestFrame?.CompletionTime??-1,
                cameraHealthy=camera.IsHealthy,sonarHealthy=sonar.IsHealthy,inferenceMilliseconds=camera.InferenceMilliseconds,
                clearance=float.IsInfinity(Runtime.Mission.Status.ClosestClearance)?999:Runtime.Mission.Status.ClosestClearance,evaluatorMinimumSeparation=float.IsInfinity(MinimumHazardSeparation)?999:MinimumHazardSeparation,
                cameraHz=camera.ProcessingHz,sonarHz=sonar.ProcessingHz,fps=Runtime.RenderingFps,frameMilliseconds=Time.unscaledDeltaTime*1000,maxFrameMilliseconds=MaxFrameMilliseconds,renderedFrames=RenderedFrames,frameSpikes50ms=FrameSpikes50ms,
                allocatedMemoryBytes=Profiler.GetTotalAllocatedMemoryLong(),reservedMemoryBytes=Profiler.GetTotalReservedMemoryLong(),managedMemoryBytes=GC.GetTotalMemory(false),
                largeOcean=Runtime.Config.LargeOcean,activeWorldActors=Runtime.Population!=null?Runtime.Population.ActiveCount:0,coverageWaypoint=coverage!=null?coverage.WaypointIndex:0,coverageWaypoints=coverage!=null?coverage.Route.Count:0,coverageSuspended=coverage!=null && coverage.Suspended};
            foreach(var track in camera.Tracks)row.camera.Add(new TrackLog{id=track.Id,hits=track.Hits,label=track.Label,category=track.Category.ToString(),eligible=track.Eligible,conflict=track.Conflict,rangeValid=track.RangeValid,provenance=track.Provenance,profile=track.Profile,evidence=track.Evidence,resultWallAge=Math.Max(0,Time.realtimeSinceStartupAsDouble-track.ResultWallTime),position=track.Position,velocity=track.Velocity,uncertainty=track.Uncertainty,score=track.Score,age=track.Age(now)});
            foreach(var track in sonar.Tracks)row.sonar.Add(new TrackLog{id=track.Id,hits=track.Hits,label="unknown",position=track.Position,velocity=track.Velocity,uncertainty=track.Uncertainty,lateralExtent=track.LateralExtent,rangeExtent=track.RangeExtent,age=track.Age(now)});
            writer.WriteLine(JsonUtility.ToJson(row));
        }
        void OnDestroy()
        {
            if(Runtime!=null){if(Runtime.Vessel!=null){Runtime.Vessel.Collector.Captured-=OnCaptured;Runtime.Vessel.Collided-=OnCollision;}if(Runtime.Mission!=null)Runtime.Mission.Decision-=OnDecision;Runtime.ScenarioReset-=ResetEvaluation;}
            writer?.Dispose();
        }
    }
}
