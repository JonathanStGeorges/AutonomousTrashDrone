using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TrashSailDemo
{
    [DefaultExecutionOrder(50)] public sealed class DemoAcceptance : MonoBehaviour
    {
        public DemoRuntime Runtime;
        public DemoEvaluation Evaluation;
        public bool SuiteRunning;
        public string SuiteStatus="Interactive production demonstration";
        [Serializable] public sealed class Result
        {
            public string scenario,status,detail,propulsion="TWIN THRUSTERS (provisional geometry)";
            public int seed,trash,bycatch,collisions,fishCollisions,avoidances,duplicates,frameSpikes50ms;
            public float duration,minimumHazardSeparation,thrusterSeconds,meanFps,meanCameraHz,meanSonarHz,maxFrameMilliseconds;
            public bool cameraLive,sonarLive,neuralDetection,unsafeCollection,resetClean,traceEquivalent=true;
            public float stoppingDistance,endSpeed,measuredTurnDegrees; public int coverageWaypoint;
            public List<string> decisions;
            public List<float> captureTimes,captureInitialZ;
        }
        [Serializable] public sealed class SuiteReport { public string revision,unity;public List<Result> results=new List<Result>(); }
        IEnumerator Start()
        {
            bool requested=PlayerPrefs.GetInt("TrashSailDemo.RunSuite",0)==1;
            var args=Environment.GetCommandLineArgs();foreach(string arg in args)if(arg=="-trashSuite")requested=true;
            if(!requested)yield break;
            PlayerPrefs.DeleteKey("TrashSailDemo.RunSuite");
            string selected=PlayerPrefs.GetString("TrashSailDemo.Scenarios","");PlayerPrefs.DeleteKey("TrashSailDemo.Scenarios");
            yield return null;yield return RunSuite(selected);
        }
        IEnumerator RunSuite(string selected)
        {
            SuiteRunning=true;Time.timeScale=1;
            var report=new SuiteReport{unity=Application.unityVersion,revision=Runtime.SoftwareRevision+"; production contract v2"};
            string[] names=string.IsNullOrWhiteSpace(selected)?new[]{"ThrusterBench","ClearCollection","CrossingFish","UnderwaterHazard","OccludedTarget","AmbiguousAssociation","CameraFailure","SonarFailure","MetadataVariant","ClearCollection"}:selected.Split(',');
            bool wasLarge=Runtime.Config.LargeOcean;Runtime.Config.LargeOcean=false;
            foreach(string raw in names)
            {
                string name=raw.Trim();SuiteStatus="RUNNING "+name;
                if(name=="PerceptionFixtures" || name=="PerceptionHoldout")
                {
                    var fixture=GetComponent<DemoPerceptionFixtures>();if(fixture==null)fixture=gameObject.AddComponent<DemoPerceptionFixtures>();
                    fixture.Runtime=Runtime;fixture.Evaluation=Evaluation;yield return fixture.Run(name=="PerceptionHoldout");
                    report.results.Add(new Result{scenario=name,status=fixture.HealthyCases==fixture.Cases && fixture.Cases>0?"PASS":"BLOCKED",detail="Acquisition only: "+fixture.FramesSaved+"/"+fixture.Cases+" real frames; "+fixture.ExpectedCategoryCases+" expected canonical category observations; inspect per-model evidence in "+fixture.ReportPath});
                }
                else if(name=="CoverageTurns")yield return CoverageTurns(report);
                else if(name=="ThrusterBench" || name=="ThrusterCalibration")yield return ThrusterBench(report);
                else if(Enum.TryParse(name,out DemoScenario scenario))yield return SensorScenario(scenario,report);
                else report.results.Add(new Result{scenario=name,status="BLOCKED",detail="Unknown production acceptance selector"});
                Save(report);yield return null;
            }
            Runtime.StopMission();Runtime.Config.LargeOcean=wasLarge;Time.timeScale=1;SuiteRunning=false;
            SuiteStatus="COMPLETE: "+report.results.FindAll(r=>r.status=="PASS").Count+"/"+report.results.Count+" passed; "+report.results.FindAll(r=>r.status=="BLOCKED").Count+" blocked";
            File.WriteAllText(Path.Combine(Evaluation.OutputDirectory,"production-suite-complete.txt"),SuiteStatus);Debug.Log("[TrashSailDemo] "+SuiteStatus);
        }
        void Save(SuiteReport report)=>File.WriteAllText(Path.Combine(Evaluation.OutputDirectory,"production-acceptance-results.json"),JsonUtility.ToJson(report,true));
        IEnumerator ThrusterBench(SuiteReport report)
        {
            Runtime.ResetScenario(DemoScenario.ThrusterCalibration,17,false);Runtime.StopMission();
            bool enabled=Runtime.Mission.enabled,reverse=Runtime.Config.ReverseThrustEnabled;
            Vector3 wind=Runtime.Config.WindTo,current=Runtime.Config.CurrentVelocity;
            Runtime.Mission.enabled=false;Runtime.Config.ReverseThrustEnabled=true;Runtime.Config.WindTo=Runtime.Config.CurrentVelocity=Vector3.zero;
            string[] cases={"forward","left","right","reverse","coast","watchdog","stopping"};
            try
            {
                foreach(string test in cases)
                {
                    // Evaluator setup only; operational navigation never resets poses.
                    Runtime.Vessel.ResetPose(Vector3.zero,Quaternion.identity);Runtime.OwnState.ResetState(17);
                    double began=Time.timeAsDouble;
                    while(Time.timeAsDouble-began<.5){Runtime.Vessel.ApplyCommand(ActuatorCommand.Safe(Time.timeAsDouble));yield return new WaitForFixedUpdate();}
                    began=Time.timeAsDouble;float port=test=="left"?-.5f:test=="right"?.5f:test=="reverse"?-1:1;
                    float starboard=test=="left"?.5f:test=="right"?-.5f:port;
                    double driveEnd=began+2;float drivenSpeed=0,drivenZ=0;
                    while(Time.timeAsDouble<driveEnd)
                    {
                        Runtime.Vessel.ApplyCommand(new ActuatorCommand{PortThrust=port,StarboardThrust=starboard,IssuedAt=Time.timeAsDouble});
                        yield return new WaitForFixedUpdate();
                    }
                    drivenSpeed=Runtime.Vessel.Body.linearVelocity.z;drivenZ=Runtime.Vessel.Body.position.z;
                    if(test=="coast" || test=="watchdog" || test=="stopping")
                    {
                        double end=Time.timeAsDouble+(test=="stopping"?20:2);
                        while(Time.timeAsDouble<end)
                        {
                            if(test=="coast" || test=="stopping")Runtime.Vessel.ApplyCommand(ActuatorCommand.Safe(Time.timeAsDouble));
                            yield return new WaitForFixedUpdate();
                        }
                    }
                    Vector3 velocity=Runtime.Vessel.Body.linearVelocity;float heading=DemoMath.Heading(Runtime.Vessel.transform.forward),yaw=Runtime.Vessel.Body.angularVelocity.y;
                    bool pass=test=="forward"?drivenZ>.2f && drivenSpeed>.1f:
                        test=="reverse"?drivenZ<-.2f && drivenSpeed<-.1f:
                        test=="left"?heading<-2 && yaw<-.02f:
                        test=="right"?heading>2 && yaw>.02f:
                        test=="coast"?Runtime.Vessel.Body.position.z>drivenZ && velocity.z>=0 && velocity.z<drivenSpeed:
                        test=="stopping"?Runtime.Vessel.Body.position.z>drivenZ && DemoMath.Flat(velocity).magnitude<.08f && Mathf.Abs(Runtime.Vessel.PortAppliedForce)+Mathf.Abs(Runtime.Vessel.StarboardAppliedForce)<.1f:
                        !Runtime.Vessel.ActuatorHealthy && Mathf.Abs(Runtime.Vessel.PortAppliedForce)+Mathf.Abs(Runtime.Vessel.StarboardAppliedForce)<.1f && !Runtime.Vessel.Collector.Active;
                    string detail=$"Measured Rigidbody z={Runtime.Vessel.Body.position.z:F3}m vz={velocity.z:F3}m/s heading={heading:F2}deg yawRate={yaw:F3}rad/s forces={Runtime.Vessel.PortAppliedForce:F2}/{Runtime.Vessel.StarboardAppliedForce:F2}N; no mission commands; reset pose only between cases";
                    report.results.Add(new Result{scenario="ThrusterBench/"+test,status=pass?"PASS":"FAIL",detail=detail,seed=17,duration=(float)(Time.timeAsDouble-began),stoppingDistance=Runtime.Vessel.Body.position.z-drivenZ,endSpeed=DemoMath.Flat(velocity).magnitude,resetClean=true});
                    Evaluation.RecordEvent("thruster_bench",test+": "+(pass?"PASS":"FAIL")+" "+detail);Save(report);
                    Runtime.Vessel.ApplyCommand(ActuatorCommand.Safe(Time.timeAsDouble));
                }
                Evaluation.Capture("thruster-bench");
            }
            finally
            {
                Runtime.Vessel.ApplyCommand(ActuatorCommand.Safe(Time.timeAsDouble));Runtime.Mission.enabled=enabled;
                Runtime.Config.ReverseThrustEnabled=reverse;Runtime.Config.WindTo=wind;Runtime.Config.CurrentVelocity=current;
            }
        }
        IEnumerator CoverageTurns(SuiteReport report)
        {
            var config=Runtime.Config;float oldLength=config.BoundaryLength,oldWidth=config.BoundaryHalfWidth,oldSpacing=config.LaneSpacing;
            float oldRadius=Runtime.Coverage.TurningRadius,oldArrival=Runtime.Coverage.ArrivalRadius;
            DemoScenario oldScenario=config.Scenario;int oldSeed=config.Seed;bool oldLarge=config.LargeOcean;
            config.LargeOcean=false;config.BoundaryLength=20;config.BoundaryHalfWidth=8;config.LaneSpacing=4;
            Runtime.Coverage.TurningRadius=1;Runtime.Coverage.ArrivalRadius=.8f;
            try
            {
                Runtime.ResetScenario(DemoScenario.ThrusterCalibration,17,false);
                Runtime.Coverage.Initialize(config,Runtime.OwnState.State.Position);
                double began=Time.timeAsDouble,readyDeadline=began+15;
                while((!Runtime.CameraPerception.IsHealthy || !Runtime.SonarPerception.IsHealthy) && Time.timeAsDouble<readyDeadline)yield return null;
                bool ready=Runtime.CameraPerception.IsHealthy && Runtime.SonarPerception.IsHealthy;
                float maximumTurn=0;
                if(ready)Runtime.StartMission();
                while(ready && Time.timeAsDouble-began<80)
                {
                    maximumTurn=Mathf.Max(maximumTurn,Mathf.Abs(Mathf.DeltaAngle(0,DemoMath.Heading(Runtime.Vessel.transform.forward))));
                    if(Runtime.Coverage.WaypointIndex>=9 && maximumTurn>100)break;
                    yield return null;
                }
                Runtime.StopMission();
                bool pass=ready && Runtime.Coverage.WaypointIndex>=9 && maximumTurn>100 && Evaluation.CollisionEvents==0;
                report.results.Add(new Result{scenario="CoverageTurns",seed=17,status=!ready?"BLOCKED":pass?"PASS":"FAIL",duration=(float)(Time.timeAsDouble-began),
                    cameraLive=Evaluation.SawFreshCamera,sonarLive=Evaluation.SawFreshSonar,coverageWaypoint=Runtime.Coverage.WaypointIndex,measuredTurnDegrees=maximumTurn,
                    detail="Empty20x16m region; boundary-only route,4m lanes/1m turn radius/.8m arrival; actual heading turn="+maximumTurn+"deg, waypoint="+Runtime.Coverage.WaypointIndex+"; no object-supplied waypoints"});
                Evaluation.RecordEvent("coverage_turns",report.results[report.results.Count-1].detail);Evaluation.Capture("coverage-turns");
            }
            finally
            {
                Runtime.StopMission();config.BoundaryLength=oldLength;config.BoundaryHalfWidth=oldWidth;config.LaneSpacing=oldSpacing;config.LargeOcean=oldLarge;
                Runtime.Coverage.TurningRadius=oldRadius;Runtime.Coverage.ArrivalRadius=oldArrival;
                Runtime.ResetScenario(oldScenario,oldSeed,false);
            }
        }
        IEnumerator SensorScenario(DemoScenario scenario,SuiteReport report)
        {
            Runtime.ResetScenario(scenario,17,true);Time.timeScale=1;
            bool clean=Runtime.Vessel.Collector.CaptureCount==0 && Runtime.CameraPerception.Tracks.Count==0 && Runtime.SonarPerception.Tracks.Count==0 && Runtime.CameraPerception.LatestFrame==null && Runtime.SonarPerception.LatestFrame==null && !Runtime.Vessel.LastCommand.CollectorEnabled;
            double began=Time.timeAsDouble;bool frozen=false,degraded=false,occlusionLeak=false,screenshot=false;
            float budget=scenario==DemoScenario.ClearCollection || scenario==DemoScenario.MetadataVariant?300:scenario==DemoScenario.CrossingFish?75:30;
            while(Time.timeAsDouble-began<budget)
            {
                double t=Time.timeAsDouble-began;
                if((scenario==DemoScenario.CameraFailure || scenario==DemoScenario.SonarFailure) && t>=8 && t<16)
                {
                    frozen=true;if(scenario==DemoScenario.CameraFailure)Runtime.CameraPerception.FreezeFeed=true;else Runtime.SonarPerception.FreezeFeed=true;
                    if(t>10 && Runtime.Mission.Status.Phase==MissionPhase.SENSOR_DEGRADED && !Runtime.Vessel.Collector.Active)degraded=true;
                }
                if(frozen && t>=16){Runtime.CameraPerception.FreezeFeed=false;Runtime.SonarPerception.FreezeFeed=false;}
                if(scenario==DemoScenario.OccludedTarget && t<3)foreach(var track in Runtime.CameraPerception.Tracks)if(track.RangeValid && Vector3.Distance(track.Position,new Vector3(0,0,11))<1.5f)occlusionLeak=true;
                if(!screenshot && t>10){Evaluation.Capture("production-dashboard");screenshot=true;}
                if((scenario==DemoScenario.ClearCollection || scenario==DemoScenario.MetadataVariant) && Evaluation.CorrectCaptures>=3)break;
                // A service/render failure cannot be called a detector-negative result.
                if(t>=35 && !Evaluation.SawFreshCamera)break;
                yield return null;
            }
            Runtime.CameraPerception.FreezeFeed=false;Runtime.SonarPerception.FreezeFeed=false;Runtime.StopMission();
            yield return new WaitForEndOfFrame();Evaluation.Capture("production-final");
            var r=new Result{scenario=scenario.ToString(),seed=17,trash=Evaluation.CorrectCaptures,bycatch=Evaluation.Bycatch,collisions=Evaluation.CollisionEvents,fishCollisions=Evaluation.FishCollisions,duplicates=Evaluation.Duplicates,
                avoidances=Runtime.Mission.Status.AvoidanceCount,duration=(float)(Time.timeAsDouble-began),minimumHazardSeparation=float.IsInfinity(Evaluation.MinimumHazardSeparation)?999:Evaluation.MinimumHazardSeparation,
                thrusterSeconds=Runtime.Vessel.ThrusterUseSeconds,cameraLive=Evaluation.SawFreshCamera,sonarLive=Evaluation.SawFreshSonar,neuralDetection=Evaluation.SawNeuralDetection,unsafeCollection=Evaluation.UnsafeCollection,resetClean=clean,
                meanFps=Evaluation.FpsSum/Mathf.Max(1,Evaluation.MetricSamples),meanCameraHz=Evaluation.CameraHzSum/Mathf.Max(1,Evaluation.MetricSamples),meanSonarHz=Evaluation.SonarHzSum/Mathf.Max(1,Evaluation.MetricSamples),maxFrameMilliseconds=Evaluation.MaxFrameMilliseconds,frameSpikes50ms=Evaluation.FrameSpikes50ms,
                decisions=new List<string>(Evaluation.Decisions),captureTimes=new List<float>(Evaluation.CaptureTimes),captureInitialZ=new List<float>(Evaluation.CaptureInitialZ)};
            bool common=clean && r.cameraLive && r.sonarLive && !r.unsafeCollection && r.duplicates==0,pass=false;
            switch(scenario)
            {
                case DemoScenario.ClearCollection:case DemoScenario.MetadataVariant:pass=common && r.neuralDetection && r.trash==3 && r.bycatch==0 && r.collisions==0;r.detail="Three neural-pixel detections and physical captures within300simsec at timescale1";break;
                case DemoScenario.CrossingFish:pass=common && r.neuralDetection && Evaluation.SawAvoidance && Evaluation.SawCollectorDisabledOnHazard && r.bycatch==0 && r.fishCollisions==0 && r.minimumHazardSeparation>0 && r.trash>0;r.detail="Independent crossing, sensor avoidance, collector closed, positive envelope clearance and resumed capture";break;
                case DemoScenario.UnderwaterHazard:pass=common && Evaluation.SawAvoidance && r.bycatch==0 && r.fishCollisions==0 && r.minimumHazardSeparation>0;r.detail="Unknown submerged sonar contact changes navigation; no fish classification assumption";break;
                case DemoScenario.OccludedTarget:r.status="BLOCKED";r.detail="Occlusion observation="+(!occlusionLeak)+"; old uncertain fixture differs only by truth label, so it cannot establish neural uncertainty handling. Requires controlled conflicting/uncertain neural evidence fixture.";break;
                case DemoScenario.AmbiguousAssociation:pass=common && r.neuralDetection && r.trash==0 && r.bycatch==0 && Evaluation.SawAvoidance;r.detail="Neural candidate with ambiguous sonar association blocks pickup";break;
                case DemoScenario.CameraFailure:case DemoScenario.SonarFailure:pass=common && degraded && r.collisions==0;r.detail="Freeze8â€“16simsec after warmup; stale feed degrades, closes collector; no collision";break;
                case DemoScenario.NoWind:pass=common && Runtime.Vessel.Body.position.z>2 && Runtime.Vessel.ThrusterUseSeconds>0;r.detail="No-wind twin-thruster navigation; no sail prerequisite";break;
                default:r.status="BLOCKED";r.detail="No production acceptance criterion for this selector";break;
            }
            if(!r.cameraLive || !r.sonarLive){r.status="BLOCKED";r.detail+="; required rendered/inference sensor unavailable: "+Runtime.CameraPerception.ServiceStatus;}
            if(r.status==null)r.status=pass?"PASS":"FAIL";
            if(r.cameraLive && !r.neuralDetection)r.detail+="; inference successfully delivered but no neural detections (not a service blocker)";
            Result baseline=report.results.Find(item=>item.scenario=="ClearCollection");
            if(baseline!=null && (scenario==DemoScenario.MetadataVariant || scenario==DemoScenario.ClearCollection))
            {
                r.traceEquivalent=r.captureInitialZ.Count==baseline.captureInitialZ.Count;
                for(int i=0;i<Math.Min(r.captureInitialZ.Count,baseline.captureInitialZ.Count);i++)r.traceEquivalent&=Mathf.Abs(r.captureInitialZ[i]-baseline.captureInitialZ[i])<.01f && Mathf.Abs(r.captureTimes[i]-baseline.captureTimes[i])<Mathf.Max(3,baseline.captureTimes[i]*.25f);
                r.traceEquivalent&=r.bycatch==baseline.bycatch && r.collisions==baseline.collisions && r.decisions.Contains("COLLECT")==baseline.decisions.Contains("COLLECT");
                if(!r.traceEquivalent && r.status=="PASS")r.status="FAIL";
                r.detail+="; same-seed capture order/timing compared (25%/3s timing tolerance)";
            }
            report.results.Add(r);
        }
    }
}

