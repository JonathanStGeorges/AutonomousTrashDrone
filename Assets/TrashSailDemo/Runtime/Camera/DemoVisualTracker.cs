using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    public sealed class DemoVisualTracker
    {
        readonly List<VisualTrack> tracks = new List<VisualTrack>();
        readonly HashSet<int> matched = new HashSet<int>();
        int nextId = 1;
        double lastAgeUpdate;
        public IReadOnlyList<VisualTrack> Tracks => tracks;
        public void Reset() { tracks.Clear(); matched.Clear(); nextId = 1; lastAgeUpdate = 0; }

        public void Update(CameraFrame frame, IReadOnlyList<PixelDetection> detections, float waterLevel, float memory)
        {
            matched.Clear();
            for (int d = 0; d < detections.Count; d++)
            {
                PixelDetection detection = detections[d];
                bool valid = DemoCameraGeometry.TryWaterIntersection(frame, detection.WaterContactPixel, waterLevel,
                    out Vector3 measured, out float uncertainty);
                VisualTrack best = null;
                float bestDistance = float.PositiveInfinity;
                foreach (VisualTrack old in tracks)
                {
                    if (matched.Contains(old.Id) || old.Age(frame.CaptureTime) > memory) continue;
                    float separation;
                    if (valid && old.RangeValid)
                    {
                        Vector3 predicted = old.Position + old.Velocity * (float)old.Age(frame.CaptureTime);
                        separation = DemoMath.Flat(measured - predicted).magnitude;
                        if (separation > Mathf.Clamp(old.Uncertainty + uncertainty + 0.5f, 0.6f, 2.5f)) continue;
                    }
                    else
                    {
                        separation = Vector2.Distance(old.PixelRect.center, detection.Bounds.center) / 30f;
                        if (separation > 1 || old.Age(frame.CaptureTime) > 0.3f) continue;
                    }
                    if (separation < bestDistance) { bestDistance = separation; best = old; }
                }
                if (best == null)
                {
                    best = new VisualTrack { Id = nextId++, Position = measured, LastSeen = frame.CaptureTime };
                    tracks.Add(best);
                }
                double elapsed = frame.CaptureTime - best.LastSeen;
                if (valid && best.RangeValid && elapsed > 0.025 && elapsed < 1)
                {
                    Vector3 observedVelocity = (measured - best.Position) / (float)elapsed;
                    best.Velocity = Vector3.Lerp(best.Velocity, Vector3.ClampMagnitude(observedVelocity, 1.5f), 0.15f);
                }
                else if (!valid) best.Velocity = Vector3.zero;
                best.Position = measured;
                best.RangeValid = valid;
                best.PixelRect = detection.Bounds;
                best.Label = detection.Label;
                best.Score = detection.Score;
                best.Uncertainty = valid ? uncertainty : 5f;
                best.Hits++;
                best.LastSeen = frame.CaptureTime;
                matched.Add(best.Id);
            }
            Age(frame.CompletionTime, memory);
        }

        public void Age(double now, float memory)
        {
            float elapsed = lastAgeUpdate > 0 ? Mathf.Clamp((float)(now - lastAgeUpdate), 0, 1) : 0;
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                VisualTrack track = tracks[i];
                if (track.Age(now) > memory) tracks.RemoveAt(i);
                else if (track.Age(now) > 0.2) track.Uncertainty = Mathf.Min(5f, track.Uncertainty + 0.3f * elapsed);
            }
            lastAgeUpdate = now;
        }
    }
}
