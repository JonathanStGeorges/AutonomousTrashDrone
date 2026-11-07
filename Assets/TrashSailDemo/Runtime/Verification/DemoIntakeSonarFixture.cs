using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TrashSailDemo
{
    // EVALUATOR ONLY: geometry truth scores measured sonar; never supplies operational detections.
    public sealed class DemoIntakeSonarFixture : MonoBehaviour
    {
        public DemoRuntime Runtime;
        public DemoEvaluation Evaluation;
        public int Cases,Passed,Blocked;
        public string ReportPath;
        [Serializable] public sealed class ContactObservation
        {
            public int id,hits;public Vector3 position;public float range,bearing,uncertainty,lateralExtent,rangeExtent,errorXZ;public double age;
        }
        [Serializable] public sealed class Measurement
        {
            public string status,previewPath;
            public float forwardDistance,nearestFreshErrorXZ=999,horizontalFov,verticalFov,minRange,maxRange;
            public int bearings,rangeBins,verticalRays,completedFrames,droppedFrames;
            public long sequence;
            public double captureTime,completionTime,age;
            public Vector3 truthPosition,sensorPosition,vesselPosition;
            public Quaternion sensorRotation;
            public Bounds truthColliderBounds;
            public bool healthy;
            public List<ContactObservation> contacts=new List<ContactObservation>();
        }
        [Serializable] public sealed class Report
        {
            public string revision,method="Static imported gallon; actual degraded MARUS intensity/contact tracking. Match threshold0.45mXZ and configured sensor freshness. Truth is evaluation-only.";
            public int seed;
            public List<Measurement> measurements=new List<Measurement>();
        }
        public IEnumerator Run()
        {
            Cases=Passed=Blocked=0;
            var config=Runtime.Config;var sonar=Runtime.SonarPerception;
            bool oldLarge=config.LargeOcean,oldEnabled=Runtime.Mission.enabled;
            Vector3 oldWind=config.WindTo,oldCurrent=config.CurrentVelocity;
            RigidbodyConstraints oldConstraints=Runtime.Vessel.Body.constraints;
            Runtime.StopMission();Runtime.Mission.enabled=false;Runtime.World.ClearWorld();Runtime.Population?.ClearPopulation();
            config.LargeOcean=false;config.WindTo=config.CurrentVelocity=Vector3.zero;
            Runtime.Vessel.ResetPose(Vector3.zero,Quaternion.identity);
            Runtime.Vessel.Body.constraints=RigidbodyConstraints.FreezeAll;
            Runtime.OwnState.ResetState(config.Seed);
            Time.timeScale=1;
            string directory=Path.Combine(Evaluation.OutputDirectory,"intake-sonar-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(directory);
            ReportPath=Path.Combine(directory,"measurements.json");
            var report=new Report{revision=Runtime.SoftwareRevision,seed=config.Seed};
            DemoActor actor=null;
            try
            {
                foreach(float distance in new[]{3.5f,3f,2.6f,2.3f,2.2f})
                {
                    if(actor!=null){actor.gameObject.SetActive(false);Destroy(actor.gameObject);}
                    Vector3 position=new Vector3(0,0,distance); // Prefab already contains its declared waterline offset.
                    actor=DemoAssetLibrary.CreateActor("plastic_bottle_gallon","trash",position,transform);
                    actor.Body.isKinematic=true;actor.Body.useGravity=false;actor.DriftVelocity=Vector3.zero;
                    Physics.SyncTransforms();Runtime.OwnState.ResetState(config.Seed);
                    sonar.ResetSensor(config.Seed+Cases);Runtime.CameraPerception.ResetSensor(config.Seed+Cases);
                    double began=Time.timeAsDouble,wall=Time.realtimeSinceStartupAsDouble;
                    while(Time.timeAsDouble-began<2 && Time.realtimeSinceStartupAsDouble-wall<6)
                    {Runtime.Vessel.ApplyCommand(ActuatorCommand.Safe(Time.timeAsDouble));yield return null;}
                    SonarFrame frame=sonar.LatestFrame;
                    var measurement=new Measurement{forwardDistance=distance,truthPosition=actor.transform.position,truthColliderBounds=actor.GetComponent<Collider>().bounds,
                        sensorPosition=sonar.transform.position,sensorRotation=sonar.transform.rotation,vesselPosition=Runtime.Vessel.transform.position,
                        verticalRays=sonar.Source!=null?Convert.ToInt32(sonar.Source.HeightRes):config.SonarVerticalRays,completedFrames=sonar.CompletedFrames,droppedFrames=sonar.DroppedFrames};
                    measurement.healthy=sonar.IsHealthy && frame!=null && frame.CaptureTime>=began+.5;
                    if(frame!=null)
                    {
                        measurement.sequence=frame.Sequence;measurement.captureTime=frame.CaptureTime;measurement.completionTime=frame.CompletionTime;
                        measurement.age=Time.timeAsDouble-frame.CaptureTime;measurement.horizontalFov=frame.HorizontalFov;measurement.verticalFov=frame.VerticalFov;
                        measurement.minRange=frame.MinRange;measurement.maxRange=frame.MaxRange;measurement.bearings=frame.Bearings;measurement.rangeBins=frame.RangeBins;
                    }
                    foreach(var contact in sonar.Tracks)
                    {
                        float error=DemoMath.Flat(contact.Position-position).magnitude;
                        double age=contact.Age(Time.timeAsDouble);
                        measurement.contacts.Add(new ContactObservation{id=contact.Id,hits=contact.Hits,position=contact.Position,range=contact.Range,bearing=contact.Bearing,
                            uncertainty=contact.Uncertainty,lateralExtent=contact.LateralExtent,rangeExtent=contact.RangeExtent,errorXZ=error,age=age});
                        if(age<=config.SonarMaxAge)measurement.nearestFreshErrorXZ=Mathf.Min(measurement.nearestFreshErrorXZ,error);
                    }
                    measurement.status=!measurement.healthy?"BLOCKED":measurement.nearestFreshErrorXZ<=.45f?"PASS":"FAIL";
                    if(measurement.status=="PASS")Passed++;if(measurement.status=="BLOCKED")Blocked++;Cases++;
                    if(sonar.PreviewTexture!=null)
                    {
                        measurement.previewPath=Path.Combine(directory,"sonar-"+distance.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+"m.png");
                        File.WriteAllBytes(measurement.previewPath,sonar.PreviewTexture.EncodeToPNG());
                    }
                    report.measurements.Add(measurement);File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
                    Evaluation.RecordEvent("intake_sonar_coverage",distance+"m: "+measurement.status+" nearestFreshError="+measurement.nearestFreshErrorXZ+" verticalFov="+measurement.verticalFov+" rays="+measurement.verticalRays);
                }
            }
            finally
            {
                if(actor!=null){actor.gameObject.SetActive(false);Destroy(actor.gameObject);}
                Runtime.Vessel.ApplyCommand(ActuatorCommand.Safe(Time.timeAsDouble));Runtime.Vessel.Body.constraints=oldConstraints;
                Runtime.Mission.enabled=oldEnabled;config.LargeOcean=oldLarge;config.WindTo=oldWind;config.CurrentVelocity=oldCurrent;
                Runtime.CameraPerception.ResetSensor(config.Seed);sonar.ResetSensor(config.Seed);
            }
        }
    }
}
