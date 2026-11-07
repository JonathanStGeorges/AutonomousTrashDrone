using UnityEngine;

namespace TrashSailDemo
{
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
