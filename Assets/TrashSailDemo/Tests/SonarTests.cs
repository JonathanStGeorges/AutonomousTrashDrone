using System;
using NUnit.Framework;
using UnityEngine;

namespace TrashSailDemo.Tests
{
    public sealed class SonarTests
    {
        [Test] public void BeamEndsAndRangeBinCentresMatchDeclaredCoverage()
        {
            Assert.That(DemoSonarProcessing.BearingAt(0, 5, 100), Is.EqualTo(-50));
            Assert.That(DemoSonarProcessing.BearingAt(2, 5, 100), Is.Zero);
            Assert.That(DemoSonarProcessing.BearingAt(4, 5, 100), Is.EqualTo(50));
            Assert.That(DemoSonarProcessing.RangeAt(0, 10, 1, 11), Is.EqualTo(1.5f));
            Assert.That(DemoSonarProcessing.RangeAt(9, 10, 1, 11), Is.EqualTo(10.5f));
        }

        [Test] public void BearingProjectionUsesSensorYawAndDoesNotInventElevation()
        {
            Vector3 projected = DemoSonarProcessing.Project(4, 0, new Vector3(2, -0.5f, 3), Quaternion.Euler(0, 90, 0));
            Assert.That(projected.x, Is.EqualTo(6).Within(0.001));
            Assert.That(projected.z, Is.EqualTo(3).Within(0.001));
            Assert.That(projected.y, Is.Zero);
        }

        [Test] public void NoiseIsSeededAndPreviewEncodesTheSameOperationalIntensity()
        {
            var pixels = new[] { new Color32(0, 0, 0, 255), new Color32(128, 128, 128, 255), new Color32(255, 255, 255, 255) };
            float[] first = DemoSonarProcessing.Degrade(pixels, 0.2f, new System.Random(17));
            float[] second = DemoSonarProcessing.Degrade(pixels, 0.2f, new System.Random(17));
            Assert.That(first, Is.EqualTo(second));
            Color32[] preview = DemoSonarProcessing.Preview(first);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(first[i], Is.InRange(0f, 1f));
                Assert.That(preview[i].r / 255f, Is.EqualTo(first[i]).Within(1f / 255));
            }
        }

        [Test] public void ExtractionUsesIntensityAndKeepsSeparatedReturnsDistinct()
        {
            SonarFrame frame = Fixture();
            frame.Intensity[2 * 5 + 2] = 1;
            frame.Intensity[7 * 5 + 2] = 1;
            var echoes = DemoSonarProcessing.Extract(frame, 0.12f);
            Assert.That(echoes.Count, Is.EqualTo(2));
            Assert.That(echoes[0].Range, Is.EqualTo(3.5f).Within(0.001));
            Assert.That(echoes[1].Range, Is.EqualTo(8.5f).Within(0.001));
            frame.Intensity[2 * 5 + 2] = 0.05f;
            Assert.That(DemoSonarProcessing.Extract(frame, 0.12f).Count, Is.EqualTo(1));
        }

        [Test] public void ExtendedEchoCarriesGreaterUncertaintyThanPointEcho()
        {
            SonarFrame frame = Fixture();
            frame.Intensity[5 * 5 + 2] = 1;
            float point = DemoSonarProcessing.Extract(frame, 0.12f)[0].Uncertainty;
            frame.Intensity[5 * 5 + 1] = 1;
            frame.Intensity[5 * 5 + 3] = 1;
            Assert.That(DemoSonarProcessing.Extract(frame, 0.12f)[0].Uncertainty, Is.GreaterThan(point));
        }

        [Test] public void LostContactPersistsWithGrowingUncertaintyUntilMemoryExpires()
        {
            var tracker = new DemoSonarTracker();
            tracker.Observe(new[] { new DemoSonarProcessing.Echo { Position = new Vector3(0, 0, 4), Range = 4, Uncertainty = 0.2f } }, 10, 10, 3);
            int id = tracker.Tracks[0].Id;
            tracker.Age(11, 3);
            Assert.That(tracker.Tracks.Count, Is.EqualTo(1));
            Assert.That(tracker.Tracks[0].Uncertainty, Is.GreaterThan(0.2f));
            tracker.Observe(new[] { new DemoSonarProcessing.Echo { Position = new Vector3(0.1f, 0, 4), Range = 4, Uncertainty = 0.2f } }, 11, 11, 3);
            Assert.That(tracker.Tracks[0].Id, Is.EqualTo(id));
            Assert.That(tracker.Tracks[0].Hits, Is.EqualTo(2));
            tracker.Age(14.1, 3);
            Assert.That(tracker.Tracks.Count, Is.Zero);
        }

        [Test] public void ResetClearsContactMemoryAndRestartsPerceptionIds()
        {
            var tracker = new DemoSonarTracker();
            var echo = new DemoSonarProcessing.Echo { Position = new Vector3(0, 0, 4), Range = 4, Uncertainty = 0.2f };
            tracker.Observe(new[] { echo }, 0, 0, 3);
            tracker.Reset();
            Assert.That(tracker.Tracks.Count, Is.Zero);
            tracker.Observe(new[] { echo }, 2, 2, 3);
            Assert.That(tracker.Tracks[0].Id, Is.EqualTo(1));
            Assert.That(tracker.Tracks[0].Hits, Is.EqualTo(1));
        }

        [Test] public void MeasuredExtentSurvivesTrackingWithoutGrowingWhenOnlyUncertaintyAges()
        {
            var tracker = new DemoSonarTracker();
            var echo = new DemoSonarProcessing.Echo
            {
                Position = new Vector3(0, 0, 4), Range = 4, Uncertainty = 0.4f,
                LateralExtent = 0.3f, RangeExtent = 0.15f
            };
            tracker.Observe(new[] { echo }, 0, 0, 3);
            tracker.Age(1, 3);
            Assert.That(tracker.Tracks[0].LateralExtent, Is.EqualTo(0.3f));
            Assert.That(tracker.Tracks[0].RangeExtent, Is.EqualTo(0.15f));
            Assert.That(tracker.Tracks[0].Uncertainty, Is.GreaterThan(0.4f));
        }

        [Test] public void OneMissingRangeBinJoinsFragmentsAndPreservesCombinedExtent()
        {
            var frame = new SonarFrame
            {
                Bearings = 9, RangeBins = 128, MinRange = 0, MaxRange = 16,
                HorizontalFov = 80, VerticalFov = 30, Rotation = Quaternion.identity,
                Intensity = new float[9 * 128], Valid = true
            };
            frame.Intensity[40 * 9 + 4] = 1;
            float pointWidth = DemoSonarProcessing.Extract(frame, 0.12f)[0].LateralExtent;
            frame.Intensity[42 * 9 + 5] = 1; // One missing row, neighbouring bearing.
            var echoes = DemoSonarProcessing.Extract(frame, 0.12f);
            Assert.That(echoes.Count, Is.EqualTo(1));
            Assert.That(echoes[0].RangeExtent, Is.EqualTo(3 * 0.125f / 2).Within(0.0001));
            Assert.That(echoes[0].LateralExtent, Is.GreaterThan(pointWidth));
            Assert.That(echoes[0].Range, Is.EqualTo(41.5f * 0.125f).Within(0.0001));
            frame.Intensity[45 * 9 + 5] = 1; // Two missing rows must remain separate.
            frame.Intensity[40 * 9 + 1] = 1; // Separated bearing must remain separate.
            Assert.That(DemoSonarProcessing.Extract(frame, 0.12f).Count, Is.EqualTo(3));
        }

        static SonarFrame Fixture() => new SonarFrame
        {
            Bearings = 5, RangeBins = 10, MinRange = 1, MaxRange = 11, HorizontalFov = 60,
            VerticalFov = 30, Rotation = Quaternion.identity, Intensity = new float[50], Valid = true
        };
    }
}
