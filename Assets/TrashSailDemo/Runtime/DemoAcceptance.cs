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
        public string SuiteStatus="Interactive demonstration";
        [Serializable] public sealed class Result
        {
            public string scenario,status,detail,propulsion;
            public int seed,trash,bycatch,collisions,fishCollisions,avoidances,tacks,duplicates;
            public float duration,minimumHazardSeparation,upwindProgress,motorSeconds,meanFps,meanCameraHz,meanSonarHz;
            public bool cameraLive,sonarLive,unsafeCollection;
            public bool resetClean,traceEquivalent=true;
            public List<string> decisions;
            public List<float> captureTimes;
            public List<float> captureInitialZ;
        }
        [Serializable] public sealed class SuiteReport { public string revision="0f9ec3f8 + TrashSailDemo v1",unity;public List<Result> results=new List<Result>(); }
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
            SuiteRunning=true;
            var report=new SuiteReport{unity=Application.unityVersion};
            DemoScenario[] scenarios={DemoScenario.ClearCollection,DemoScenario.CrossingFish,DemoScenario.UnderwaterHazard,DemoScenario.OccludedTarget,DemoScenario.AmbiguousAssociation,DemoScenario.CameraFailure,DemoScenario.SonarFailure,DemoScenario.Upwind,DemoScenario.LowWind,DemoScenario.MetadataVariant,DemoScenario.ClearCollection};
            if(!string.IsNullOrWhiteSpace(selected))
            {var list=new List<DemoScenario>();foreach(string name in selected.Split(','))if(Enum.TryParse(name.Trim(),out DemoScenario scenario))list.Add(scenario);scenarios=list.ToArray();}
            foreach(var scenario in scenarios)
            {
                SuiteStatus="RUNNING "+scenario;
                Runtime.ResetScenario(scenario,17,true);Time.timeScale=3;
                bool resetClean=Runtime.Vessel.Collector.CaptureCount==0 && Runtime.CameraPerception.Tracks.Count==0 && Runtime.SonarPerception.Tracks.Count==0
                    && Runtime.CameraPerception.LatestFrame==null && Runtime.SonarPerception.LatestFrame==null && Runtime.Mission.Status.Phase==MissionPhase.IDLE
                    && !Runtime.Vessel.LastCommand.CollectorEnabled && Runtime.Vessel.Collector.OverspeedCaptureCount==0;
                double began=Time.timeAsDouble;bool screenshot=false,freeze=false,degradedAfterFailure=false,occludedLeak=false,unknownCollected=false;
                int avoidanceBefore=0;float budget=scenario==DemoScenario.ClearCollection || scenario==DemoScenario.MetadataVariant?300:scenario==DemoScenario.Upwind?100:scenario==DemoScenario.LowWind?16:scenario==DemoScenario.CrossingFish?75:24;
                while(Time.timeAsDouble-began<budget)
                {
                    double t=Time.timeAsDouble-began;
                    if((scenario==DemoScenario.CameraFailure || scenario==DemoScenario.SonarFailure) && t>=6 && t<12)
                    {freeze=true;if(scenario==DemoScenario.CameraFailure)Runtime.CameraPerception.FreezeFeed=true;else Runtime.SonarPerception.FreezeFeed=true;if(t>7 && Runtime.Mission.Status.Phase==MissionPhase.SENSOR_DEGRADED)degradedAfterFailure=true;}
                    if(freeze && t>=12){Runtime.CameraPerception.FreezeFeed=false;Runtime.SonarPerception.FreezeFeed=false;}
                    if(scenario==DemoScenario.OccludedTarget && t<3)
                    {
                        foreach(var track in Runtime.CameraPerception.Tracks)
                            if(track.RangeValid && Vector3.Distance(track.Position,new Vector3(0,0,11))<1.5f)occludedLeak=true;
                    }
                    if(Evaluation.Bycatch>0)unknownCollected=true;
                    if(!screenshot && t>7){Evaluation.Capture("dashboard");screenshot=true;}
                    if(Runtime.Mission.Status.AvoidanceCount>avoidanceBefore){avoidanceBefore=Runtime.Mission.Status.AvoidanceCount;Evaluation.Capture("avoidance-"+avoidanceBefore);}
                    if((scenario==DemoScenario.ClearCollection || scenario==DemoScenario.MetadataVariant) && Evaluation.CorrectCaptures>=3)break;
                    yield return null;
                }
                Runtime.Mission.StopMission();yield return new WaitForEndOfFrame();Evaluation.Capture("final");
                var result=new Result{scenario=scenario.ToString(),seed=17,propulsion=Runtime.Config.MotorAssistance?"MOTOR ASSIST + approximate sail":"SAIL ONLY",
                    trash=Evaluation.CorrectCaptures,bycatch=Evaluation.Bycatch,collisions=Evaluation.CollisionEvents,fishCollisions=Evaluation.FishCollisions,duplicates=Evaluation.Duplicates,
                    avoidances=Runtime.Mission.Status.AvoidanceCount,tacks=Evaluation.MeasuredTacks,duration=(float)(Time.timeAsDouble-began),
                    minimumHazardSeparation=float.IsInfinity(Evaluation.MinimumHazardSeparation)?999:Evaluation.MinimumHazardSeparation,upwindProgress=Evaluation.MaximumUpwindProgress,
                    motorSeconds=Runtime.Vessel.MotorUseSeconds,cameraLive=Evaluation.SawFreshCamera,sonarLive=Evaluation.SawFreshSonar,unsafeCollection=Evaluation.UnsafeCollection,resetClean=resetClean,
                    meanFps=Evaluation.FpsSum/Mathf.Max(1,Evaluation.MetricSamples),meanCameraHz=Evaluation.CameraHzSum/Mathf.Max(1,Evaluation.MetricSamples),meanSonarHz=Evaluation.SonarHzSum/Mathf.Max(1,Evaluation.MetricSamples),
                    decisions=new List<string>(Evaluation.Decisions),captureTimes=new List<float>(Evaluation.CaptureTimes),captureInitialZ=new List<float>(Evaluation.CaptureInitialZ)};
                bool common=result.resetClean && result.cameraLive && result.sonarLive && !result.unsafeCollection && result.duplicates==0;
                bool pass=false;
                switch(scenario)
                {
                    case DemoScenario.ClearCollection:case DemoScenario.MetadataVariant:pass=common && result.trash==3 && result.bycatch==0 && result.collisions==0;result.detail="Three RGB-detected physical captures <=300simsec; mode explicit; no intervention";break;
                    case DemoScenario.CrossingFish:pass=common && Evaluation.SawAvoidance && Evaluation.SawCollectorDisabledOnHazard && result.bycatch==0 && result.fishCollisions==0 && result.minimumHazardSeparation>0 && result.trash>0;result.detail="Crossing path independent of boat; sonar abort/depower; clear envelope and resume capture";break;
                    case DemoScenario.UnderwaterHazard:pass=common && Evaluation.SawAvoidance && result.bycatch==0 && result.fishCollisions==0 && result.minimumHazardSeparation>0;result.detail="Submerged layer excluded from RGB; operational unknown sonar contact changes course";break;
                    case DemoScenario.OccludedTarget:pass=common && !occludedLeak && !unknownCollected && result.trash==0;result.detail="Initial occluder hides pixels; uncertain appearance never captured";break;
                    case DemoScenario.AmbiguousAssociation:pass=common && result.trash==0 && result.bycatch==0 && Evaluation.SawAvoidance;result.detail="Target-adjacent unresolved/conflicting sonar echo blocks pickup";break;
                    case DemoScenario.CameraFailure:case DemoScenario.SonarFailure:pass=common && degradedAfterFailure && result.collisions==0;result.detail="Freeze6–12simsec; stale feed degraded, collection interlocked, depowered fault latch requires Start mission after recovery; no collision";break;
                    case DemoScenario.Upwind:pass=common && result.motorSeconds==0 && result.upwindProgress>5 && result.tacks>=2;result.detail="Declared initial close-hauled55deg; wind5m/s, no motor, alternating physical tack legs";break;
                    case DemoScenario.LowWind:pass=common && result.motorSeconds==0 && result.upwindProgress<0.6f && Evaluation.SawDegraded;result.detail="0.05m/s wind, motor disabled, visible stalled/deferred outcome";break;
                }
                Result baseline=report.results.Find(r=>r.scenario==DemoScenario.ClearCollection.ToString());
                if(baseline!=null && (scenario==DemoScenario.MetadataVariant || scenario==DemoScenario.ClearCollection))
                {
                    result.traceEquivalent=result.captureInitialZ.Count==baseline.captureInitialZ.Count;
                    for(int i=0;i<Math.Min(result.captureInitialZ.Count,baseline.captureInitialZ.Count);i++)
                        result.traceEquivalent&=Mathf.Abs(result.captureInitialZ[i]-baseline.captureInitialZ[i])<0.01f && Mathf.Abs(result.captureTimes[i]-baseline.captureTimes[i])<Mathf.Max(3,baseline.captureTimes[i]*0.25f);
                    result.traceEquivalent&=result.decisions.Contains("COLLECT")==baseline.decisions.Contains("COLLECT") && result.bycatch==baseline.bycatch && result.collisions==baseline.collisions;
                    pass&=result.traceEquivalent;result.detail+="; same-seed capture order/timing and decision-state milestones compared (25%/3s timing tolerance)";
                }
                result.status=pass?"PASS":"FAIL";report.results.Add(result);
                File.WriteAllText(Path.Combine(Evaluation.OutputDirectory,"acceptance-results.json"),JsonUtility.ToJson(report,true));
                yield return null;
            }
            Time.timeScale=1;SuiteRunning=false;SuiteStatus="COMPLETE: "+report.results.FindAll(r=>r.status=="PASS").Count+"/"+report.results.Count+" passed";
            File.WriteAllText(Path.Combine(Evaluation.OutputDirectory,"suite-complete.txt"),SuiteStatus);
            Debug.Log("[TrashSailDemo] "+SuiteStatus);
        }
    }
}
