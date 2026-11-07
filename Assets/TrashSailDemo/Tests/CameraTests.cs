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

        [Test] public void PixelDetectorFindsSeparatedOrangeComponentsAndRejectsBlue()
        {
            var pixels = new Color32[40 * 24];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(10, 70, 120, 255);
            Paint(pixels, 40, 5, 4, 4, 5, new Color32(255, 55, 10, 255));
            Paint(pixels, 40, 25, 12, 5, 4, new Color32(245, 30, 15, 255));
            var found = new DemoImageDetector().Detect(pixels, 40, 24);
            Assert.That(found.Count, Is.EqualTo(2));
            Assert.That(found[0].Label, Is.EqualTo("rubbish"));
            Assert.That(found[0].WaterContactPixel.y, Is.LessThan(found[0].Bounds.center.y));
            Assert.That(found[0].Bounds.xMin, Is.EqualTo(5));
        }

        [Test] public void MutedCandidateRemainsUnknown()
        {
            var pixels = new Color32[20 * 20];
            Paint(pixels, 20, 6, 6, 6, 6, new Color32(180, 140, 110, 255));
            var found = new DemoImageDetector().Detect(pixels, 20, 20);
            Assert.That(found.Count, Is.EqualTo(1));
            Assert.That(found[0].Label, Is.EqualTo("unknown"));
            Assert.That(found[0].Score, Is.LessThan(0.72f));
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
            var detection = new PixelDetection { Bounds = new Rect(48, 45, 5, 8), WaterContactPixel = new Vector2(50, 45), Label = "rubbish", Score = 0.9f };
            tracker.Update(Fixture(1), new[] { detection }, 0, 3);
            int firstId = tracker.Tracks[0].Id;
            tracker.Update(Fixture(1.1), new[] { detection }, 0, 3);
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
            var detection = new PixelDetection { Bounds = new Rect(48, 45, 5, 8), WaterContactPixel = new Vector2(50, 45), Label = "rubbish", Score = 0.9f };
            tracker.Update(Fixture(), new[] { detection }, 0, 3);
            tracker.Reset();
            Assert.That(tracker.Tracks.Count, Is.Zero);
            tracker.Update(Fixture(10), new[] { detection }, 0, 3);
            Assert.That(tracker.Tracks[0].Id, Is.EqualTo(1));
            Assert.That(tracker.Tracks[0].Hits, Is.EqualTo(1));
        }

        static void Paint(Color32[] pixels, int width, int x, int y, int w, int h, Color32 color)
        {
            for (int row = y; row < y + h; row++)
                for (int col = x; col < x + w; col++) pixels[row * width + col] = color;
        }
    }
}
