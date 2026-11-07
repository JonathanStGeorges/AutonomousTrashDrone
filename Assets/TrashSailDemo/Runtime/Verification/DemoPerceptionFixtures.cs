using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TrashSailDemo
{
    // PRIVILEGED RENDERED EVALUATION ONLY. Fixture labels never enter inference or mission.
    public sealed class DemoPerceptionFixtures : MonoBehaviour
    {
        public DemoRuntime Runtime;
        public DemoEvaluation Evaluation;
        public int Cases,FramesSaved,HealthyCases,ExpectedCategoryCases;
        public string ReportPath;
        [Serializable] public sealed class Observation
        {
            public int trackId;public string label,category,provenance,profile;
            public float score;public bool eligible,conflict,rangeValid;
            public Rect pixelRect;public Vector3 estimatedPosition;
            public ModelEvidence[] evidence;
        }
        [Serializable] public sealed class FixtureResult
        {
            public string fixture,assetKey,expectedKind,status,serviceStatus,imagePath;
            public float distanceFromVesselMetres,yawDegrees;
            public Vector3 truthPosition,sunEuler;
            public bool cameraHealthy,expectedCategoryObserved,occluded,glareSetup;
            public long sequence;
            public double captureTime,completionTime;
            public int width,height;
            public Matrix4x4 cameraToWorld;
            public float verticalFov;
            public string pixelOrigin="bottom-left";
            public List<Observation> observations=new List<Observation>();
        }
        [Serializable] public sealed class Report
        {
            public string revision,scope="Rendered fixture acquisition; detection observations are not proof of operational collection",sourceFramesRoot;
            public int seed;
            public bool holdout;
            public List<FixtureResult> fixtures=new List<FixtureResult>();
        }
        sealed class Case { public string Key,Kind;public float Range,Yaw;public bool Occluded,Glare; }
        public IEnumerator Run(bool holdout=false)
        {
            Cases=FramesSaved=HealthyCases=ExpectedCategoryCases=0;
            var cfg=Runtime.Config;bool oldLarge=cfg.LargeOcean,oldEnabled=Runtime.Mission.enabled,oldSave=Runtime.CameraPerception.SaveSourceFrames;
            Vector3 wind=cfg.WindTo,current=cfg.CurrentVelocity;
            int originalSeed=cfg.Seed;
            if(holdout)cfg.Seed=41;
            Light sun=RenderSettings.sun;
            if(sun==null)foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional){sun=light;break;}
            Quaternion sunRotation=sun!=null?sun.transform.rotation:Quaternion.identity;
            Material occluderMaterial=null;
            Runtime.StopMission();Runtime.Mission.enabled=false;Runtime.World.ClearWorld();Runtime.Population?.ClearPopulation();
            cfg.LargeOcean=false;cfg.WindTo=cfg.CurrentVelocity=Vector3.zero;
            Runtime.Vessel.ResetPose(Vector3.zero,Quaternion.identity);Runtime.OwnState.ResetState(cfg.Seed);
            Runtime.CameraPerception.SaveSourceFrames=true;Time.timeScale=1;
            string directory=Path.Combine(Evaluation.OutputDirectory,(holdout?"perception-holdout-":"perception-fixtures-")+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(directory);
            ReportPath=Path.Combine(directory,"fixtures.json");
            var report=new Report{revision=Runtime.SoftwareRevision,seed=cfg.Seed,holdout=holdout,sourceFramesRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence/TrashSailDemo/sourceframes"))};
            var cases=new List<Case>();
            foreach(string key in new[]{"plastic_bottle_gallon","multi_cleaner_bottle","cleaner_tin_01","trashbag"})
                foreach(float range in holdout?new[]{2.5f,4f,7f}:new[]{3f,5f,8f})foreach(float yaw in holdout?new[]{45f,135f}:new[]{0f,90f})cases.Add(new Case{Key=key,Kind="trash",Range=range,Yaw=yaw});
            cases.Add(new Case{Key="BarramundiFish",Kind="fish",Range=3});
            cases.Add(new Case{Key="marus_boat",Kind="boat",Range=5});
            if(holdout)
            {
                cases.Add(new Case{Key="plastic_bottle_gallon",Kind="trash",Range=5,Occluded=true});
                cases.Add(new Case{Key="cleaner_tin_01",Kind="trash",Range=3,Glare=true});
            }
            cases.Add(new Case{Key="",Kind="empty",Range=0});
            GameObject fixtureRoot=null;
            try
            {
                foreach(Case test in cases)
                {
                    if(fixtureRoot!=null){fixtureRoot.SetActive(false);Destroy(fixtureRoot);}
                    fixtureRoot=new GameObject("Evaluator fixture truth only");
                    if(sun!=null)sun.transform.rotation=sunRotation;
                    Vector3 position=new Vector3(0,test.Kind=="fish"?-.1f:test.Kind=="boat"?0:.07f,test.Range);
                    if(test.Kind!="empty")
                    {
                        DemoActor actor=DemoAssetLibrary.CreateActor(test.Key,test.Kind,position,fixtureRoot.transform);
                        actor.transform.rotation=Quaternion.Euler(0,test.Yaw,0);
                        actor.Crossing=false;actor.DriftVelocity=Vector3.zero;
                        // A static calibration target, isolated from navigation and capture.
                        actor.Body.isKinematic=true;actor.Body.useGravity=false;
                    }
                    if(test.Occluded)
                    {
                        var occluder=GameObject.CreatePrimitive(PrimitiveType.Cube);
                        occluder.name="Evaluator opaque occluder";occluder.layer=11;occluder.transform.SetParent(fixtureRoot.transform,false);
                        occluder.transform.position=new Vector3(0,.9f,3);occluder.transform.localScale=new Vector3(1.5f,2.5f,.3f);
                        if(occluderMaterial==null)occluderMaterial=DemoWorld.MakeMaterial("FixtureOccluder",Color.gray);
                        occluder.GetComponent<Renderer>().sharedMaterial=occluderMaterial;
                    }
                    if(test.Glare && sun!=null)
                    {
                        // Reflection-angle setup; actual glare on curved geometry must be observed.
                        Vector3 view=(Runtime.CameraPerception.SensorCamera.transform.position-position).normalized;
                        sun.transform.rotation=Quaternion.LookRotation(Vector3.Reflect(view,Vector3.up),Vector3.forward);
                    }
                    Physics.SyncTransforms();Runtime.OwnState.ResetState(cfg.Seed);
                    Runtime.CameraPerception.ResetSensor(cfg.Seed+Cases);Runtime.SonarPerception.ResetSensor(cfg.Seed+Cases);
                    double start=Time.timeAsDouble,wall=Time.realtimeSinceStartupAsDouble;
                    while(Time.realtimeSinceStartupAsDouble-wall<6)
                    {
                        Runtime.Vessel.ApplyCommand(ActuatorCommand.Safe(Time.timeAsDouble));
                        CameraFrame candidate=Runtime.CameraPerception.LatestFrame;
                        if(candidate!=null && candidate.CaptureTime>=start+1 && Runtime.CameraPerception.IsHealthy)break;
                        yield return null;
                    }
                    CameraFrame frame=Runtime.CameraPerception.LatestFrame;
                    var result=new FixtureResult{fixture=Cases.ToString("D2")+"-"+(test.Key==""?"empty":test.Key)+"-"+test.Range+"m-"+test.Yaw+"deg"+(test.Occluded?"-occluded":test.Glare?"-glare":""),assetKey=test.Key,expectedKind=test.Occluded?"occludedtrash":test.Kind,
                        distanceFromVesselMetres=test.Range,yawDegrees=test.Yaw,truthPosition=position,occluded=test.Occluded,glareSetup=test.Glare && sun!=null,sunEuler=sun!=null?sun.transform.eulerAngles:Vector3.zero,cameraHealthy=Runtime.CameraPerception.IsHealthy,serviceStatus=Runtime.CameraPerception.ServiceStatus};
                    bool newFrame=frame!=null && frame.Valid && frame.CaptureTime>=start && frame.Pixels!=null;
                    if(newFrame)
                    {
                        result.sequence=frame.Sequence;result.captureTime=frame.CaptureTime;result.completionTime=frame.CompletionTime;result.width=frame.Width;result.height=frame.Height;result.cameraToWorld=frame.CameraToWorld;result.verticalFov=frame.VerticalFov;
                        var texture=new Texture2D(frame.Width,frame.Height,TextureFormat.RGBA32,false,false);texture.SetPixels32(frame.Pixels);texture.Apply(false,false);
                        result.imagePath=Path.Combine(directory,result.fixture+".png");File.WriteAllBytes(result.imagePath,texture.EncodeToPNG());Destroy(texture);FramesSaved++;
                        foreach(var track in Runtime.CameraPerception.Tracks)
                        {
                            if(Math.Abs(track.LastSeen-frame.CaptureTime)>.0001)continue;
                            result.observations.Add(new Observation{trackId=track.Id,label=track.Label,category=track.Category.ToString(),provenance=track.Provenance,profile=track.Profile,score=track.Score,eligible=track.Eligible,conflict=track.Conflict,rangeValid=track.RangeValid,pixelRect=track.PixelRect,estimatedPosition=track.Position,evidence=track.Evidence});
                            result.expectedCategoryObserved|=test.Kind=="trash"?(track.Category==CanonicalCategory.CollectableRubbish || track.Category==CanonicalCategory.OtherRubbish):test.Kind=="fish"?track.Category==CanonicalCategory.Wildlife:test.Kind=="boat" && track.Category==CanonicalCategory.Obstacle;
                        }
                        if(test.Kind=="empty")result.expectedCategoryObserved=result.observations.Count==0;
                        if(test.Occluded)result.expectedCategoryObserved=!result.observations.Exists(item=>item.category==CanonicalCategory.CollectableRubbish.ToString() || item.category==CanonicalCategory.OtherRubbish.ToString());
                        result.status=result.cameraHealthy?"CAPTURED":"STALE_RESULT";
                    }
                    else result.status="BLOCKED_NO_MATCHED_RENDERED_RESULT";
                    if(result.cameraHealthy && newFrame)HealthyCases++;
                    if(result.expectedCategoryObserved)ExpectedCategoryCases++;
                    Cases++;report.fixtures.Add(result);File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
                    Evaluation.RecordEvent("perception_fixture",result.fixture+" "+result.status+" observed="+result.observations.Count+" expectedCategory="+result.expectedCategoryObserved);
                }
            }
            finally
            {
                if(fixtureRoot!=null){fixtureRoot.SetActive(false);Destroy(fixtureRoot);}
                if(sun!=null)sun.transform.rotation=sunRotation;
                if(occluderMaterial!=null)Destroy(occluderMaterial);
                Runtime.Vessel.ApplyCommand(ActuatorCommand.Safe(Time.timeAsDouble));Runtime.Mission.enabled=oldEnabled;
                cfg.LargeOcean=oldLarge;cfg.WindTo=wind;cfg.CurrentVelocity=current;cfg.Seed=originalSeed;Runtime.CameraPerception.SaveSourceFrames=oldSave;
                Runtime.CameraPerception.ResetSensor(cfg.Seed);Runtime.SonarPerception.ResetSensor(cfg.Seed);
            }
        }
    }
}
