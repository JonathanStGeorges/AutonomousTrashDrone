using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashSailDemo
{
    public struct PixelDetection
    {
        public Rect Bounds;
        public Vector2 WaterContactPixel;
        public string Label;
        public float Score;
        public int PixelCount;
    }

    // Synthetic-demo baseline: connected orange/red pixels, not a trained classifier.
    // Its only input is an RGB pixel buffer. Names, tags, annotations and scene objects
    // are deliberately absent from this API.
    public sealed class DemoImageDetector
    {
        int[] visited = Array.Empty<int>();
        int[] queue = Array.Empty<int>();
        int generation;
        public readonly List<PixelDetection> Detections = new List<PixelDetection>();

        public static bool IsCandidate(Color32 p)
        {
            return p.r >= 95 && p.r > p.g * 1.22f && p.r > p.b * 1.38f;
        }

        public IReadOnlyList<PixelDetection> Detect(Color32[] pixels, int width, int height, int minimumPixels = 4)
        {
            Detections.Clear();
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length != width * height) return Detections;
            if (visited.Length != pixels.Length)
            {
                visited = new int[pixels.Length];
                queue = new int[pixels.Length];
                generation = 0;
            }
            if (++generation == int.MaxValue) { Array.Clear(visited, 0, visited.Length); generation = 1; }
            for (int index = 0; index < pixels.Length; index++)
            {
                if (visited[index] == generation || !IsCandidate(pixels[index])) continue;
                int head = 0, tail = 1, count = 0, minX = width, maxX = 0, minY = height, maxY = 0;
                float saturationSum = 0;
                queue[0] = index;
                visited[index] = generation;
                while (head < tail)
                {
                    int current = queue[head++], x = current % width, y = current / width;
                    Color32 p = pixels[current];
                    count++;
                    saturationSum += (p.r - Mathf.Max(p.g, p.b)) / Mathf.Max(1f, p.r);
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    Add(current - 1, x > 0); Add(current + 1, x + 1 < width);
                    Add(current - width, y > 0); Add(current + width, y + 1 < height);
                }
                if (count < minimumPixels || count > pixels.Length * 0.25f) continue;
                // Use the lowest visible band, approximating where the floating object
                // touches the water. A cropped lower boundary has no observable contact.
                float contactX = 0; int contactCount = 0;
                for (int q = 0; q < tail; q++)
                {
                    int pixel = queue[q];
                    if (pixel / width <= minY + 1) { contactX += pixel % width + 0.5f; contactCount++; }
                }
                float saturation = saturationSum / count;
                float score = Mathf.Clamp01(0.4f + 0.6f * saturation);
                Detections.Add(new PixelDetection
                {
                    Bounds = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    WaterContactPixel = new Vector2(contactX / Mathf.Max(1, contactCount), minY + 0.2f),
                    Label = score >= 0.72f ? "rubbish" : "unknown",
                    Score = score,
                    PixelCount = count
                });

                void Add(int adjacent, bool inBounds)
                {
                    if (!inBounds || visited[adjacent] == generation || !IsCandidate(pixels[adjacent])) return;
                    visited[adjacent] = generation;
                    queue[tail++] = adjacent;
                }
            }
            return Detections;
        }
    }

    public static class DemoCameraGeometry
    {
        public static bool TryWaterIntersection(CameraFrame frame, Vector2 pixel, float waterLevel,
            out Vector3 position, out float uncertainty, float maximumRange = 35f)
        {
            position = default; uncertainty = float.PositiveInfinity;
            if (frame == null || !frame.Valid || frame.Width <= 0 || frame.Height <= 0 ||
                pixel.x < 0 || pixel.x >= frame.Width || pixel.y <= 0.5f || pixel.y >= frame.Height) return false;
            float tangent = Mathf.Tan(frame.VerticalFov * Mathf.Deg2Rad * 0.5f);
            float aspect = frame.Width / (float)frame.Height;
            Vector3 ray = new Vector3((pixel.x / frame.Width * 2 - 1) * aspect * tangent,
                (pixel.y / frame.Height * 2 - 1) * tangent, 1).normalized;
            Vector3 origin = frame.CameraToWorld.MultiplyPoint3x4(Vector3.zero);
            Vector3 direction = frame.CameraToWorld.MultiplyVector(ray).normalized;
            // A grazing ray magnifies single-pixel contact errors and is not usable.
            if (origin.y <= waterLevel + 0.05f || direction.y >= -0.045f) return false;
            float distance = (waterLevel - origin.y) / direction.y;
            if (distance <= 0 || distance > maximumRange) return false;
            position = origin + direction * distance;
            position.y = waterLevel;
            float pixelAngle = 2 * tangent / frame.Height;
            uncertainty = Mathf.Clamp(0.08f + distance * pixelAngle / Mathf.Abs(direction.y), 0.1f, 3f);
            return true;
        }

        public static Rect OverlayRect(Rect panel, Rect bottomLeftPixelRect, int width, int height)
        {
            return new Rect(panel.x + bottomLeftPixelRect.x / width * panel.width,
                panel.y + (1 - bottomLeftPixelRect.yMax / height) * panel.height,
                bottomLeftPixelRect.width / width * panel.width, bottomLeftPixelRect.height / height * panel.height);
        }
    }
}
