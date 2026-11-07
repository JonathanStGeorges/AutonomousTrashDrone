using System;
using System.Collections.Generic;
using Marus.Sensors;
using UnityEngine;

namespace TrashSailDemo
{
    [DefaultExecutionOrder(-120)]
    public sealed class DemoSonarPerception : MonoBehaviour, ISonarFrameSource
    {
        public DemoConfig Config;
        public Sonar3D Source;
        [Tooltip("Optional IOwnStateSource; absent means explicitly ideal sensor pose.")]
        public MonoBehaviour OwnStateProvider;
        public bool FreezeFeed;
        [Range(0.01f, 1)] public float DetectionThreshold = 0.12f;
        public SonarFrame LatestFrame { get; private set; }
        public IReadOnlyList<ContactTrack> Tracks => tracker.Tracks;
        public Texture2D PreviewTexture { get; private set; }
        public float ProcessingHz { get; private set; }
        public int CompletedFrames { get; private set; }
        public int DroppedFrames { get; private set; }
        public double LastDeliveryRealtime { get; private set; }
        public bool IsHealthy => LatestFrame != null && LatestFrame.Valid && Config != null
            && DemoMath.Fresh(Time.timeAsDouble, LatestFrame.CaptureTime, Config.SonarMaxAge);
        public double FrameAge => LatestFrame == null ? double.PositiveInfinity : Math.Max(0, Time.timeAsDouble - LatestFrame.CaptureTime);
        public string PoseMode => OwnStateProvider is IOwnStateSource ? "estimated own-state pose" : "ideal sensor pose";

        readonly DemoSonarTracker tracker = new DemoSonarTracker();
        System.Random noiseRandom;
        uint lastUpdateCount;
        Texture2D observedTexture;
        long sequence;
        SonarFrame pending;
        double pendingDelivery, ignoreUntil, lastDeliverySimulation;
        Vector3 previousPosition;
        Quaternion previousRotation;
        double previousTime;
        bool initialized;

        void Awake()
        {
            if (Config != null) ConfigureSource();
        }

        void Start()
        {
            if (Config == null) { Debug.LogError("Trash Sail sonar requires a DemoConfig.", this); enabled = false; return; }
            ConfigureSource();
            ResetSensor(Config.Seed);
        }

        public void ConfigureSource()
        {
            if (Config == null) return;
            if (Source == null) Source = GetComponent<Sonar3D>();
            if (Source == null) Source = gameObject.AddComponent<Sonar3D>();
            // Configure before Sonar3D.Start allocates ray arrays/LUTs. Geometry stays fixed until scene reload.
            Source.WidthRes = Mathf.Max(3, Config.SonarBearings);
            Source.HeightRes = Mathf.Max(3, Config.SonarVerticalRays);
            Source.imageHeight = Mathf.Max(8, Config.SonarRangeBins);
            Source.CartesianXRes = 96; Source.CartesianYRes = 128;
            Source.MinDistance = Mathf.Max(0.05f, Config.SonarMinRange);
            Source.MaxDistance = Mathf.Max(Source.MinDistance + 1, Config.SonarMaxRange);
            Source.HorizontalFieldOfView = Config.SonarFov;
            Source.VerticalFieldOfView = Config.SonarVerticalFov;
            Source.SampleFrequency = Mathf.Clamp(Config.SonarHz, 1, 20);
            Source.sonarRayDistribution = Sonar3D.RayDistribution.Equiangular;
            Source.sonarConfig = Sonar3D.SonarConfiguration.Custom;
            Source.AddNoise = false; Source.Grid = false; Source.DisplayImages = false; Source.SaveImages = false;
            Source.RayIntensity = 12;
            Source.ImageSavePath = System.IO.Path.Combine(Application.temporaryCachePath, "TrashSailDemoSonar");
        }

        public void ResetSensor(int seed)
        {
            noiseRandom = new System.Random(unchecked(seed * 7919 + 37));
            tracker.Reset(); LatestFrame = null; pending = null; sequence = 0;
            CompletedFrames = 0; DroppedFrames = 0; ProcessingHz = 0;
            LastDeliveryRealtime = 0; lastDeliverySimulation = 0; FreezeFeed = false;
            observedTexture = Source != null ? Source.sonarImage : null;
            lastUpdateCount = observedTexture != null ? observedTexture.updateCount : 0;
            EstimatedSensorPose(out previousPosition, out previousRotation); previousTime = Time.timeAsDouble;
            // Drop any source result scheduled before a world reset. No asynchronous adapter callbacks survive.
            ignoreUntil = Time.timeAsDouble + 2.0 / Mathf.Max(1, Config != null ? Config.SonarHz : 8);
            initialized = true;
            if (PreviewTexture != null)
            {
                var pixels = new Color32[PreviewTexture.width * PreviewTexture.height];
                PreviewTexture.SetPixels32(pixels); PreviewTexture.Apply(false, false);
            }
        }

        void LateUpdate()
        {
            if (!initialized || Config == null || Source == null) return;
            double now = Time.timeAsDouble;
            tracker.Age(now, Config.TrackMemory);
            if (FreezeFeed) pending = null;
            if (!FreezeFeed && pending != null && now >= pendingDelivery)
            {
                Deliver(pending, now);
                pending = null;
            }
            Texture2D image = Source.sonarImage;
            if (image != null)
            {
                if (observedTexture != image)
                {
                    observedTexture = image;
                    lastUpdateCount = image.updateCount; // Allocation is not a completed observation.
                }
                else if (image.updateCount != lastUpdateCount)
                {
                    lastUpdateCount = image.updateCount;
                    if (FreezeFeed || now < ignoreUntil || pending != null) DroppedFrames++;
                    else if (noiseRandom.NextDouble() < Mathf.Clamp01(Config.SonarDropout)) DroppedFrames++;
                    else CaptureCompletedImage(image, now);
                }
            }
            EstimatedSensorPose(out previousPosition, out previousRotation); previousTime = now;
        }

        void EstimatedSensorPose(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position; rotation = transform.rotation;
            if (!(OwnStateProvider is IOwnStateSource source)) return;
            VehicleState state = source.State;
            // Mount transform is calibration. Only the vehicle's estimated pose enters the observation.
            Vector3 mountOffset = OwnStateProvider.transform.InverseTransformPoint(transform.position);
            Quaternion mountRotation = Quaternion.Inverse(OwnStateProvider.transform.rotation) * transform.rotation;
            position = state.Position + state.Rotation * mountOffset;
            rotation = state.Rotation * mountRotation;
        }

        void CaptureCompletedImage(Texture2D image, double now)
        {
            Color32[] pixels = image.GetPixels32();
            if (pixels.Length != image.width * image.height) return;
            // MARUS emits no scheduling timestamp. Its coroutine commits at end-of-frame.
            // Conservatively date the sample one full requested interval before observation;
            // the prior rendered pose is the closest exposed pose and motion enters uncertainty.
            double samplePeriod = 1.0 / Mathf.Max(1, Source.SampleFrequency);
            double captured = Math.Max(0, Math.Min(previousTime, now - samplePeriod));
            pending = new SonarFrame
            {
                Sequence = ++sequence, CaptureTime = captured, CompletionTime = now,
                Bearings = image.width, RangeBins = image.height,
                MinRange = Source.MinDistance, MaxRange = Source.MaxDistance,
                HorizontalFov = Source.HorizontalFieldOfView, VerticalFov = Source.VerticalFieldOfView,
                Origin = previousPosition, Rotation = previousRotation,
                Intensity = DemoSonarProcessing.Degrade(pixels, Mathf.Clamp01(Config.SonarNoise), noiseRandom),
                Valid = true
            };
            pendingDelivery = now + Mathf.Max(0, Config.SonarLatency);
        }

        void Deliver(SonarFrame frame, double now)
        {
            LatestFrame = frame;
            if (PreviewTexture == null || PreviewTexture.width != frame.Bearings || PreviewTexture.height != frame.RangeBins)
            {
                if (PreviewTexture != null) Destroy(PreviewTexture);
                PreviewTexture = new Texture2D(frame.Bearings, frame.RangeBins, TextureFormat.RGBA32, false)
                { name = "Operational sonar: degraded range-bearing", filterMode = FilterMode.Point };
            }
            PreviewTexture.SetPixels32(DemoSonarProcessing.Preview(frame.Intensity));
            PreviewTexture.Apply(false, false);
            EstimatedSensorPose(out Vector3 currentPosition, out _);
            float motionUncertainty = DemoMath.Flat(currentPosition - frame.Origin).magnitude;
            if (OwnStateProvider is IOwnStateSource ownState)
                motionUncertainty += Config.StateNoise * 2 + DemoMath.Flat(ownState.State.Velocity).magnitude * Mathf.Max(0, Config.StateLatency);
            var echoes = DemoSonarProcessing.Extract(frame, DetectionThreshold, motionUncertainty);
            tracker.Observe(echoes, frame.CaptureTime, now, Config.TrackMemory);
            if (lastDeliverySimulation > 0 && now > lastDeliverySimulation)
                ProcessingHz = Mathf.Lerp(ProcessingHz > 0 ? ProcessingHz : (float)(1 / (now - lastDeliverySimulation)),
                    (float)(1 / (now - lastDeliverySimulation)), 0.2f);
            lastDeliverySimulation = now; LastDeliveryRealtime = Time.realtimeSinceStartupAsDouble;
            CompletedFrames++;
        }

        public void DrawOverlay(Rect panel)
        {
            GUI.BeginGroup(panel);panel=new Rect(0,0,panel.width,panel.height);
            Color previous = GUI.color;
            GUI.color = new Color(0.02f, 0.04f, 0.07f, 1); GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            Rect imageRect = new Rect(panel.x + 40, panel.y + 38, panel.width - 46, panel.height - 60);
            if (PreviewTexture != null) GUI.DrawTexture(imageRect, PreviewTexture, ScaleMode.StretchToFill);
            string age = LatestFrame == null ? "no sample" : FrameAge.ToString("F2") + " s";
            string health=FreezeFeed?"FROZEN / STALE ":Time.timeScale==0?"PAUSED / FROZEN ":IsHealthy?"LIVE ":"STALE ";
            GUI.Label(new Rect(panel.x + 6, panel.y + 4, panel.width - 12, 22), "SONAR • unknown contacts • " + health + age);
            if (LatestFrame != null)
            {
                GUI.Label(new Rect(panel.x + 2, imageRect.y, 40, 20), LatestFrame.MaxRange.ToString("F0") + "m");
                GUI.Label(new Rect(panel.x + 2, imageRect.yMax - 20, 40, 20), LatestFrame.MinRange.ToString("F1"));
                GUI.Label(new Rect(imageRect.x, imageRect.yMax, imageRect.width, 20), "port −" + (LatestFrame.HorizontalFov / 2).ToString("F0") + "°       0°       +starboard");
                GUI.color = new Color(0.3f, 0.6f, 0.8f, 0.7f);
                GUI.DrawTexture(new Rect(imageRect.center.x, imageRect.y, 1, imageRect.height), Texture2D.whiteTexture);
                foreach (ContactTrack contact in Tracks)
                {
                    Vector3 delta = contact.Position - LatestFrame.Origin;
                    float bearing = DemoMath.Bearing(delta, LatestFrame.Rotation);
                    float range = DemoMath.Flat(delta).magnitude;
                    if (Mathf.Abs(bearing) > LatestFrame.HorizontalFov * 0.5f || range > LatestFrame.MaxRange || range < LatestFrame.MinRange) continue;
                    float x = imageRect.x + (bearing / LatestFrame.HorizontalFov + 0.5f) * imageRect.width;
                    float y = imageRect.yMax - (range - LatestFrame.MinRange) / (LatestFrame.MaxRange - LatestFrame.MinRange) * imageRect.height;
                    GUI.color = contact.Age(Time.timeAsDouble) > Config.SonarMaxAge ? Color.gray : (range < 6 ? Color.red : Color.yellow);
                    GUI.DrawTexture(new Rect(x - 4, y - 4, 8, 8), Texture2D.whiteTexture);
                    GUI.Label(new Rect(x + 6, y - 10, 100, 20), "U" + contact.Id + " ±" + contact.Uncertainty.ToString("F1"));
                }
            }
            GUI.color = previous;
            GUI.EndGroup();
        }

        void OnDestroy()
        {
            pending = null;
            if (PreviewTexture != null) Destroy(PreviewTexture);
        }
    }
}
