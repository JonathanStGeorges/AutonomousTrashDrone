using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    /// <summary>Processing of the operational intensity image; no scene access.</summary>
    public static class DemoSonarProcessing
    {
        public struct Echo
        {
            public Vector3 Position;
            public float Range, Bearing, Uncertainty;
            public float LateralExtent, RangeExtent; // Measured cluster half-size, metres.
        }

        public static float BearingAt(int column, int columns, float fov)
            => columns <= 1 ? 0 : column * fov / (columns - 1) - fov * 0.5f;

        public static float RangeAt(int row, int rows, float min, float max)
            => min + (row + 0.5f) * (max - min) / Mathf.Max(1, rows);

        public static Vector3 Project(float range, float bearing, Vector3 origin, Quaternion rotation)
        {
            // The imaging sonar does not resolve elevation. Project onto estimated XZ plane.
            Vector3 direction = rotation * new Vector3(Mathf.Sin(bearing * Mathf.Deg2Rad), 0, Mathf.Cos(bearing * Mathf.Deg2Rad));
            direction = DemoMath.Flat(direction).normalized;
            return new Vector3(origin.x, 0, origin.z) + direction * range;
        }

        public static float[] Degrade(Color32[] pixels, float noise, System.Random random)
        {
            var result = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                // Bounded zero-mean noise: no hidden clean range array reaches tracking.
                float disturbance = noise > 0 ? ((float)random.NextDouble() * 2 - 1) * noise : 0;
                result[i] = Mathf.Clamp01(pixels[i].r / 255f + disturbance);
            }
            return result;
        }

        public static Color32[] Preview(float[] intensity)
        {
            var pixels = new Color32[intensity.Length];
            for (int i = 0; i < intensity.Length; i++)
            {
                float value = Mathf.Clamp01(intensity[i]);
                pixels[i] = new Color32((byte)(value * 255), (byte)(value * 225), (byte)(value * 90), 255);
            }
            return pixels;
        }

        public static List<Echo> Extract(SonarFrame frame, float threshold, float motionUncertainty = 0)
        {
            var echoes = new List<Echo>();
            if (frame == null || !frame.Valid || frame.Intensity == null || frame.Bearings < 1 || frame.RangeBins < 1
                || frame.Intensity.Length != frame.Bearings * frame.RangeBins) return echoes;

            var visited = new bool[frame.Intensity.Length];
            var queue = new int[frame.Intensity.Length];
            float rangeStep = (frame.MaxRange - frame.MinRange) / frame.RangeBins;
            for (int index = 0; index < frame.Intensity.Length; index++)
            {
                if (visited[index] || frame.Intensity[index] < threshold) continue;
                int head = 0, tail = 0;
                queue[tail++] = index;
                visited[index] = true;
                float totalWeight = 0, rangeSum = 0, bearingSum = 0;
                int minX = frame.Bearings, maxX = -1, minY = frame.RangeBins, maxY = -1;
                while (head < tail)
                {
                    int cell = queue[head++], x = cell % frame.Bearings, y = cell / frame.Bearings;
                    float weight = frame.Intensity[cell];
                    totalWeight += weight;
                    rangeSum += RangeAt(y, frame.RangeBins, frame.MinRange, frame.MaxRange) * weight;
                    bearingSum += BearingAt(x, frame.Bearings, frame.HorizontalFov) * weight;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    // Bridge one missing range bin on adjacent beams: curved surfaces can
                    // fragment after intensity thresholding. Bound the bridge to 0.25 m;
                    // unresolved returns retain their full combined extent and uncertainty.
                    int rangeNeighbours = rangeStep * 2 <= 0.25f ? 2 : 1;
                    for (int dy = -rangeNeighbours; dy <= rangeNeighbours; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= frame.Bearings || ny < 0 || ny >= frame.RangeBins) continue;
                        int next = ny * frame.Bearings + nx;
                        if (visited[next] || frame.Intensity[next] < threshold) continue;
                        visited[next] = true;
                        queue[tail++] = next;
                    }
                }
                float range = rangeSum / totalWeight, bearing = bearingSum / totalWeight;
                float angularSpan = (maxX - minX + 1) * frame.HorizontalFov / Mathf.Max(1, frame.Bearings - 1);
                float lateralExtent = range * Mathf.Sin(0.5f * angularSpan * Mathf.Deg2Rad);
                float rangeExtent = (maxY - minY + 1) * rangeStep * 0.5f;
                float verticalAmbiguity = range * (1 - Mathf.Cos(frame.VerticalFov * 0.5f * Mathf.Deg2Rad));
                echoes.Add(new Echo
                {
                    Position = Project(range, bearing, frame.Origin, frame.Rotation), Range = range, Bearing = bearing,
                    LateralExtent = lateralExtent, RangeExtent = rangeExtent,
                    Uncertainty = Mathf.Max(lateralExtent, rangeExtent) + rangeStep * 0.5f + verticalAmbiguity + 0.06f + motionUncertainty
                });
            }
            return echoes;
        }
    }

    /// <summary>Constant-velocity, nearest-neighbour contact tracker with explicit lost memory.</summary>
    public sealed class DemoSonarTracker
    {
        readonly List<ContactTrack> tracks = new List<ContactTrack>();
        readonly Dictionary<int, float> measurementUncertainty = new Dictionary<int, float>();
        int nextId = 1;
        public IReadOnlyList<ContactTrack> Tracks => tracks;

        public void Reset()
        {
            tracks.Clear(); measurementUncertainty.Clear(); nextId = 1;
        }

        public void Observe(IReadOnlyList<DemoSonarProcessing.Echo> echoes, double captured, double now, float memory)
        {
            Age(now, memory);
            var assigned = new HashSet<int>();
            foreach (var echo in echoes)
            {
                ContactTrack best = null;
                float bestDistance = float.PositiveInfinity;
                foreach (ContactTrack candidate in tracks)
                {
                    if (assigned.Contains(candidate.Id)) continue;
                    float dt = (float)Math.Max(0, captured - candidate.LastSeen);
                    Vector3 prediction = candidate.Position + candidate.Velocity * Mathf.Min(dt, 1);
                    float distance = DemoMath.Flat(prediction - echo.Position).magnitude;
                    float gate = Mathf.Clamp(candidate.Uncertainty + echo.Uncertainty + 0.45f + dt * 0.6f, 0.65f, 2f);
                    if (distance < gate && distance < bestDistance) { best = candidate; bestDistance = distance; }
                }
                if (best == null)
                {
                    best = new ContactTrack { Id = nextId++, Position = echo.Position, LastSeen = captured };
                    tracks.Add(best);
                }
                else
                {
                    float dt = (float)(captured - best.LastSeen);
                    if (dt > 0.02f)
                    {
                        Vector3 measuredVelocity = Vector3.ClampMagnitude(DemoMath.Flat(echo.Position - best.Position) / dt, 3);
                        best.Velocity = Vector3.Lerp(best.Velocity, measuredVelocity, 0.35f);
                    }
                }
                best.Position = echo.Position; best.Range = echo.Range; best.Bearing = echo.Bearing;
                best.LateralExtent = echo.LateralExtent; best.RangeExtent = echo.RangeExtent;
                best.LastSeen = captured; best.Hits++; best.Uncertainty = echo.Uncertainty;
                measurementUncertainty[best.Id] = echo.Uncertainty;
                assigned.Add(best.Id);
            }
            Age(now, memory);
        }

        public void Age(double now, float memory)
        {
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                ContactTrack track = tracks[i];
                double age = track.Age(now);
                if (age > memory) { measurementUncertainty.Remove(track.Id); tracks.RemoveAt(i); continue; }
                if (measurementUncertainty.TryGetValue(track.Id, out float baseline))
                    track.Uncertainty = baseline + (float)age * 0.35f;
            }
        }
    }
}
