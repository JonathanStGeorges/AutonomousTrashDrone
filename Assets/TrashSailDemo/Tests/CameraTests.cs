using NUnit.Framework;
using UnityEngine;

namespace TrashSailDemo.Tests
{
    public sealed class CameraTests
    {
        static CameraFrame Fixture(double captured = 1)
        {
            return new CameraFrame
            {
                Width = 100, Height = 100, VerticalFov = 60, CaptureTime = captured,
                CompletionTime = captured + 0.04,
                CameraToWorld = Matrix4x4.TRS(new Vector3(0, 2, 0), Quaternion.Euler(30, 0, 0), Vector3.one)
            };
        }

        [Test] public void WaterProjectionUsesCameraAttitudeAndContactPixel()
        {
            CameraFrame frame = Fixture();
            Assert.That(DemoCameraGeometry.TryWaterIntersection(frame, new Vector2(50, 50), 0, out var point, out _), Is.True);
            Assert.That(point.y, Is.EqualTo(0).Within(0.001));
            Assert.That(point.z, Is.EqualTo(2 * Mathf.Sqrt(3)).Within(0.01));
            frame.CameraToWorld = Matrix4x4.TRS(new Vector3(4, 2, -2), Quaternion.Euler(30, 90, 0), Vector3.one);
            Assert.That(DemoCameraGeometry.TryWaterIntersection(frame, new Vector2(50, 50), 0, out point, out _), Is.True);
            Assert.That(point.x, Is.EqualTo(4 + 2 * Mathf.Sqrt(3)).Within(0.01));
            Assert.That(point.z, Is.EqualTo(-2).Within(0.01));
        }

        [Test] public void HorizonAndCroppedWaterContactDoNotGenerateRange()
        {
            CameraFrame frame = Fixture();
            frame.CameraToWorld = Matrix4x4.TRS(new Vector3(0, 2, 0), Quaternion.identity, Vector3.one);
            Assert.That(DemoCameraGeometry.TryWaterIntersection(frame, new Vector2(50, 50), 0, out _, out _), Is.False);
            Assert.That(DemoCameraGeometry.TryWaterIntersection(frame, new Vector2(50, 0.2f), 0, out _, out _), Is.False);
        }

        [Test] public void TrackerMaintainsIdAndExpiresWithoutNewObservations()
        {
            var tracker = new DemoVisualTracker();
            var result = new InferenceResult { profile="combined", detections=new[] { new ModelEvidence { model="YOLOE", classId=0, label="plastic bottle", category="CollectableRubbish", eligible=true, box=new[]{48f,47f,53f,55f}, score=.9f } } };
            tracker.UpdateModels(Fixture(1), result, 0, 3, 1);
            int firstId = tracker.Tracks[0].Id;
            tracker.UpdateModels(Fixture(1.1), result, 0, 3, 1.1);
            Assert.That(tracker.Tracks.Count, Is.EqualTo(1));
            Assert.That(tracker.Tracks[0].Id, Is.EqualTo(firstId));
            Assert.That(tracker.Tracks[0].Hits, Is.EqualTo(2));
            float initialUncertainty = tracker.Tracks[0].Uncertainty;
            tracker.Age(2, 3);
            Assert.That(tracker.Tracks[0].LastSeen, Is.EqualTo(1.1));
            Assert.That(tracker.Tracks[0].Uncertainty, Is.GreaterThan(initialUncertainty));
            tracker.Age(4.2, 3);
            Assert.That(tracker.Tracks.Count, Is.EqualTo(0));
        }

        [Test] public void OverlayUsesSameBottomLeftPixelCoordinates()
        {
            Rect mapped = DemoCameraGeometry.OverlayRect(new Rect(10, 20, 200, 100), new Rect(20, 10, 10, 20), 100, 50);
            Assert.That(mapped.x, Is.EqualTo(50));
            Assert.That(mapped.y, Is.EqualTo(60).Within(0.001f));
            Assert.That(mapped.width, Is.EqualTo(20));
            Assert.That(mapped.height, Is.EqualTo(40));
        }

        [Test] public void ResetDropsPriorTracksAndRestartsPerceptionIds()
        {
            var tracker = new DemoVisualTracker();
            var result = new InferenceResult { profile="combined", detections=new[] { new ModelEvidence { model="YOLOE", classId=0, label="plastic bottle", category="CollectableRubbish", eligible=true, box=new[]{48f,47f,53f,55f}, score=.9f } } };
            tracker.UpdateModels(Fixture(), result, 0, 3, 1);
            tracker.Reset();
            Assert.That(tracker.Tracks.Count, Is.Zero);
            tracker.UpdateModels(Fixture(10), result, 0, 3, 10);
            Assert.That(tracker.Tracks[0].Id, Is.EqualTo(1));
            Assert.That(tracker.Tracks[0].Hits, Is.EqualTo(1));
        }

        [Test] public void ProtocolRejectsOldSessionAndWrongFrameButAcceptsEmptySuccess()
        {
            CameraFrame frame=Fixture();frame.Sequence=7;
            var result=new InferenceResult {ok=true,empty=true,sessionId="reset-A",cameraId="onboard",sequence=7,captureTime=1,width=100,height=100,profile="combined",detections=System.Array.Empty<ModelEvidence>()};
            Assert.That(DemoInferenceProtocol.Matches(result,"reset-A",frame,"combined"),Is.True);
            Assert.That(DemoInferenceProtocol.Matches(result,"reset-B",frame,"combined"),Is.False);
            result.sequence=6;
            Assert.That(DemoInferenceProtocol.Matches(result,"reset-A",frame,"combined"),Is.False);
            result.sequence=7;result.ok=false;
            Assert.That(DemoInferenceProtocol.Matches(result,"reset-A",frame,"combined"),Is.False);
        }
        [Test] public void ModelCoordinatesAndConflictRemainDistinct()
        {
            var a=new ModelEvidence {model="YOLOE",classId=0,label="plastic bottle",category="CollectableRubbish",eligible=false,conflict=true,score=.8f,box=new[]{45f,40f,55f,60f}};
            var b=new ModelEvidence {model="TRASH",classId=2,label="animal_fish",category="Wildlife",eligible=false,conflict=true,score=.6f,box=new[]{45f,40f,55f,60f}};
            Rect box=DemoInferenceProtocol.BottomLeftBox(a,100,100);
            Assert.That(box.yMin,Is.EqualTo(40));
            var tracker=new DemoVisualTracker();
            tracker.UpdateModels(Fixture(),new InferenceResult {profile="combined",detections=new[]{a,b}},0,3,10);
            Assert.That(tracker.Tracks.Count,Is.EqualTo(2));
            Assert.That(tracker.Tracks[0].Eligible,Is.False);
            Assert.That(tracker.Tracks[1].RangeValid,Is.False,"Never water-plane range a wildlife observation");
            Assert.That(tracker.Tracks[1].Evidence[0].classId,Is.EqualTo(2));
            Assert.That(tracker.Tracks[0].Conflict,Is.True);
        }

    }
}
