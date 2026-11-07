using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace TrashSailDemo
{
    // Adapts the MARUS rendered-camera approach for completed-frame ownership and
    // HDRP scheduling. Upstream CameraSensor does not expose completion sequence/pose;
    // its generic hasData is also set by SensorSampler before GPU completion.
    public sealed class DemoCameraPerception : MonoBehaviour, ICameraFrameSource
    {
        public DemoConfig Config;
        public Camera SensorCamera;
        [Tooltip("Optional IOwnStateSource component; absent means explicitly ideal camera pose.")]
        public MonoBehaviour OwnStateProvider;
        [Tooltip("Calibrated readback orientation override; the retained frame is always bottom-left.")]
        public bool FlipReadbackY;
        public bool FreezeFeed;
        public int SelectedTrackId = -1;
        public float ProcessingHz { get; private set; }
        public float InferenceMilliseconds { get; private set; }
        public int ReadbackErrors { get; private set; }
        public int DroppedFrames { get; private set; }
        public string Health { get; private set; } = "Waiting for rendered camera";
        public CameraFrame LatestFrame { get; private set; }
        public IReadOnlyList<VisualTrack> Tracks => tracker.Tracks;
        public Texture2D PreviewTexture { get; private set; }
        public event Action<CameraFrame> FrameProcessed;
        public bool IsHealthy => LatestFrame != null && LatestFrame.Valid && Config != null &&
            serverSuccess && !FreezeFeed && Time.realtimeSinceStartup-lastCaptureWall<=MaxResultWallAge &&
            DemoMath.Fresh(Time.timeAsDouble, LatestFrame.CaptureTime, Config.CameraMaxAge);

        public string ServiceUrl="http://127.0.0.1:8765";
        public string ActiveProfile="combined";
        public string DisplayFilter="combined";
        public string ServiceStatus {get;private set;}="Waiting for inference service";
        public string ModelStatus=>ServiceStatus;
        public float InferenceLatencyMs=>InferenceMilliseconds;
        public float MaxResultWallAge=3f;
        public bool SaveSourceFrames=true;
        string sessionId;
        bool inferenceInFlight, serverSuccess;
        UnityWebRequest activeRequest;
        float lastCaptureWall, capturedWall;
        int savedFrames, savedPositiveFrames;
        float lastPositiveSaveWall=float.NegativeInfinity;
        [Serializable] sealed class SourceCalibration
        {
            public string sessionId,cameraId,profile;
            public long sequence;
            public double captureTime,completionTime;
            public float captureWallTime,waterLevel,verticalFov;
            public int width,height;
            public Matrix4x4 cameraToWorld;
            public string pixelOrigin="bottom-left",resultOrigin="top-left";
        }
        string SaveSource(CameraFrame frame,byte[] png,string session,string profile,float water,float captureWall)
        {
            string directory=Path.Combine(Application.dataPath,"../Evidence/TrashSailDemo/sourceframes",session);
            Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,frame.Sequence.ToString("D6"));
            File.WriteAllBytes(path+".png",png);
            File.WriteAllText(path+".calibration.json",JsonUtility.ToJson(new SourceCalibration {
                sessionId=session,cameraId="onboard",profile=profile,sequence=frame.Sequence,
                captureTime=frame.CaptureTime,completionTime=frame.CompletionTime,captureWallTime=captureWall,
                waterLevel=water,verticalFov=frame.VerticalFov,width=frame.Width,height=frame.Height,cameraToWorld=frame.CameraToWorld
            },true));
            return path;
        }

        readonly DemoVisualTracker tracker = new DemoVisualTracker();
        readonly List<VisualTrack> overlayTracks = new List<VisualTrack>();
        RenderTexture target;
        CameraFrame pending;
        System.Random random;
        bool armed, inFlight;
        int generation;
        long sequence;
        double nextCapture, publishAt, capturedAt, previousPublishedAt;
        Matrix4x4 capturedPose;
        float capturedWater;
        float capturedFov;
        float lastPublishWallTime;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        void Start()
        {
            if (Config == null || SensorCamera == null) { Health = "Camera configuration missing"; enabled = false; return; }
            target = new RenderTexture(Mathf.Max(32, Config.CameraWidth), Mathf.Max(24, Config.CameraHeight), 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "TrashSail_OnboardRGB", filterMode = FilterMode.Bilinear, antiAliasing = 1,
                useMipMap = false, wrapMode = TextureWrapMode.Clamp
            };
            target.Create();
            SensorCamera.targetTexture = target;
            SensorCamera.aspect = target.width / (float)target.height;
            SensorCamera.allowHDR = false;
            SensorCamera.allowMSAA = false;
            SensorCamera.nearClipPlane = 0.08f;
            SensorCamera.farClipPlane = 80;
            SensorCamera.cullingMask = (SensorCamera.cullingMask | (1 << 9)) & ~((1 << 8) | (1 << 12) | (1 << 5));
            SensorCamera.enabled = false;
            HDAdditionalCameraData hd = SensorCamera.GetComponent<HDAdditionalCameraData>();
            if (hd == null) hd = SensorCamera.gameObject.AddComponent<HDAdditionalCameraData>();
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            hd.backgroundColorHDR = new Color(0.04f, 0.13f, 0.2f);
            hd.flipYMode = HDAdditionalCameraData.FlipYMode.Automatic;
            hd.customRenderingSettings = true;
            foreach(var field in new[]{FrameSettingsField.Water,FrameSettingsField.Refraction,FrameSettingsField.TransparentObjects})
            {
                hd.renderingPathCustomFrameSettings.SetEnabled(field,true);
                hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]=true;
            }
            hd.volumeLayerMask = 1;
            hd.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Postprocess, true);
            hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Postprocess] = true;
            ResetSensor(Config.Seed);
        }

        void LateUpdate()
        {
            if (Config == null || target == null) return;
            double now = Time.timeAsDouble;
            tracker.Age(now, Config.TrackMemory);
            if (pending != null && now >= publishAt && !FreezeFeed)
            {
                CameraFrame frame = pending; pending = null;
                Publish(frame);
            }
            if (!IsHealthy && LatestFrame != null) Health = "STALE — collection must be disabled";
            if (FreezeFeed || Time.timeScale == 0 || inFlight || inferenceInFlight || pending != null || armed || now < nextCapture) return;
            nextCapture = now + 1.0 / Mathf.Max(0.1f, Config.CameraHz);
            if (random == null) random = new System.Random(Config.Seed ^ 0x4167);
            if (random.NextDouble() < Config.CameraDropout) { DroppedFrames++; return; }
            armed = true;
            SensorCamera.enabled = true;
        }

        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != SensorCamera || !armed) return;
            capturedAt = Time.timeAsDouble;
            capturedWall=Time.realtimeSinceStartup;
            capturedFov = camera.fieldOfView;
            capturedWater = 0;
            capturedPose = Matrix4x4.TRS(camera.transform.position, camera.transform.rotation, Vector3.one);
            if (OwnStateProvider is IOwnStateSource source)
            {
                VehicleState state = source.State;
                Vector3 mount = OwnStateProvider.transform.InverseTransformPoint(camera.transform.position);
                Quaternion mountRotation = Quaternion.Inverse(OwnStateProvider.transform.rotation) * camera.transform.rotation;
                capturedPose = Matrix4x4.TRS(state.Position + state.Rotation * mount, state.Rotation * mountRotation, Vector3.one);
                capturedWater = state.WaterLevel;
            }
        }

        void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != SensorCamera || !armed || target == null) return;
            armed = false;
            SensorCamera.enabled = false;
            inFlight = true;
            int requestGeneration = generation;
            int width = target.width, height = target.height;
            long requestSequence = ++sequence;
            double capture = capturedAt;
            Matrix4x4 pose = capturedPose;
            float fov = capturedFov, water = capturedWater;
            AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
            {
                if (this == null || requestGeneration != generation) return;
                inFlight = false;
                if (!isActiveAndEnabled || FreezeFeed) return;
                if (request.hasError) { ReadbackErrors++; Health = "GPU readback error"; return; }
                var native = request.GetData<Color32>();
                if (native.Length != width * height) { ReadbackErrors++; Health = "Invalid RGB buffer length"; return; }
                var pixels = new Color32[native.Length];
                native.CopyTo(pixels);
                if (FlipReadbackY) FlipRows(pixels, width, height);
                pending = new CameraFrame
                {
                    Sequence = requestSequence, CaptureTime = capture, CompletionTime = Time.timeAsDouble,
                    Width = width, Height = height, Pixels = pixels, CameraToWorld = pose, VerticalFov = fov
                };
                capturedWater = water;
                publishAt = Math.Max(Time.timeAsDouble, capture + Config.CameraLatency);
            });
        }

        void Publish(CameraFrame frame)
        {
            StartCoroutine(Infer(frame,capturedWater,generation,capturedWall));
        }

        IEnumerator Infer(CameraFrame frame,float water,int requestGeneration,float captureWall)
        {
            inferenceInFlight=true;
            string requestSession=sessionId, profile=ActiveProfile;
            var texture=new Texture2D(frame.Width,frame.Height,TextureFormat.RGBA32,false,false);
            texture.SetPixels32(frame.Pixels);texture.Apply(false,false);
            byte[] png=texture.EncodeToPNG();Destroy(texture);
            string evidencePath=null;
            if(SaveSourceFrames && savedFrames<20)
            {
                evidencePath=SaveSource(frame,png,requestSession,profile,water,captureWall);savedFrames++;
            }
            var request=new InferenceRequest {sessionId=requestSession,cameraId="onboard",sequence=frame.Sequence,captureTime=frame.CaptureTime,
                width=frame.Width,height=frame.Height,profile=profile,imageBase64=Convert.ToBase64String(png)};
            var web=new UnityWebRequest(ServiceUrl+"/infer","POST");activeRequest=web;
            web.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));
            web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","application/json");web.timeout=15;
            ServiceStatus="Inference pending: "+profile;
            yield return web.SendWebRequest();
            if(requestGeneration!=generation) {web.Dispose();yield break;}
            inferenceInFlight=false;activeRequest=null;
            string responseBody=web.downloadHandler.text;
            InferenceResult result=null;
            string parseError=null;
            try {result=JsonUtility.FromJson<InferenceResult>(responseBody);}
            catch(Exception error) {parseError=error.Message;}
            if(evidencePath!=null) File.WriteAllText(evidencePath+".json",responseBody);
            if(web.result!=UnityWebRequest.Result.Success || result==null || !result.ok)
            {
                string detail=!string.IsNullOrWhiteSpace(result?.error) ? result.error : parseError ?? web.error ?? "Inference returned ok=false";
                serverSuccess=false;ServiceStatus="FAULT: "+detail;Health=ServiceStatus;web.Dispose();yield break;
            }
            web.Dispose();
            if(!DemoInferenceProtocol.Matches(result,requestSession,frame,profile) || profile!=ActiveProfile || FreezeFeed)
            {
                serverSuccess=false;ServiceStatus="FAULT: mismatched/session/profile result";Health=ServiceStatus;yield break;
            }
            if(SaveSourceFrames && evidencePath==null && result.detections.Length>0 && savedPositiveFrames<60 && Time.realtimeSinceStartup-lastPositiveSaveWall>=1f)
            {
                evidencePath=SaveSource(frame,png,requestSession,profile,water,captureWall);
                File.WriteAllText(evidencePath+".json",responseBody);
                savedPositiveFrames++;lastPositiveSaveWall=Time.realtimeSinceStartup;
            }
            InferenceMilliseconds=(float)result.wallInferenceMs;
            lastCaptureWall=captureWall;
            serverSuccess=true;
            ServiceStatus=$"{result.device} / {profile}: {(result.empty ? "successful empty frame" : result.detections.Length+" model detections")}";
            // Keep matched source pixels visible even when too old for control.
            bool fresh=Time.realtimeSinceStartup-captureWall<=MaxResultWallAge && DemoMath.Fresh(Time.timeAsDouble,frame.CaptureTime,Config.CameraMaxAge);
            if(fresh) tracker.UpdateModels(frame,result,water,Config.TrackMemory,Time.realtimeSinceStartupAsDouble);
            else {serverSuccess=false;ServiceStatus="STALE result rejected for control / "+ServiceStatus;}
            LatestFrame = frame;
            if (PreviewTexture == null || PreviewTexture.width != frame.Width || PreviewTexture.height != frame.Height)
            {
                if (PreviewTexture != null) Destroy(PreviewTexture);
                PreviewTexture = new Texture2D(frame.Width, frame.Height, TextureFormat.RGBA32, false, false)
                    { name = "TrashSail_CompletedCameraFrame", filterMode = FilterMode.Point };
            }
            PreviewTexture.SetPixels32(frame.Pixels);
            PreviewTexture.Apply(false, false);
            overlayTracks.Clear();
            foreach (VisualTrack track in Tracks)
                if (Math.Abs(track.LastSeen - frame.CaptureTime) < 0.0001)
                    overlayTracks.Add(new VisualTrack { Id = track.Id, Label = track.Label, PixelRect = track.PixelRect,
                        Score = track.Score, LastSeen = track.LastSeen, Hits = track.Hits, RangeValid = track.RangeValid,
                        Provenance=track.Provenance,Category=track.Category,Eligible=track.Eligible,Conflict=track.Conflict,Evidence=track.Evidence });

            if (previousPublishedAt > 0 && Time.timeAsDouble > previousPublishedAt)
                ProcessingHz = Mathf.Lerp(ProcessingHz, 1f / (float)(Time.timeAsDouble - previousPublishedAt), 0.3f);
            previousPublishedAt = Time.timeAsDouble;
            lastPublishWallTime = Time.realtimeSinceStartup;
            Health = IsHealthy ? "LIVE / "+ServiceStatus : "STALE result / "+ServiceStatus;
            FrameProcessed?.Invoke(frame);
        }

        public void ResetSensor(int seed)
        {
            generation++;
            if(activeRequest!=null) {activeRequest.Abort();activeRequest.Dispose();activeRequest=null;}
            StopAllCoroutines();inferenceInFlight=false;serverSuccess=false;sessionId=Guid.NewGuid().ToString("N");savedFrames=0;savedPositiveFrames=0;lastPositiveSaveWall=float.NegativeInfinity;
            random = new System.Random(seed ^ 0x4167);
            armed = false; inFlight = false; pending = null; LatestFrame = null;
            sequence = 0; nextCapture = 0; previousPublishedAt = 0;
            ProcessingHz = 0; ReadbackErrors = 0; DroppedFrames = 0; SelectedTrackId = -1;
            FreezeFeed = false; tracker.Reset(); overlayTracks.Clear();
            Health = "Waiting for completed RGB frame";
            if (SensorCamera != null) SensorCamera.enabled = false;
            if (PreviewTexture != null) { Destroy(PreviewTexture); PreviewTexture = null; }
        }

        public static void FlipRows(Color32[] pixels, int width, int height)
        {
            for (int y = 0; y < height / 2; y++)
                for (int x = 0; x < width; x++)
                {
                    int a = y * width + x, b = (height - y - 1) * width + x;
                    Color32 temporary = pixels[a]; pixels[a] = pixels[b]; pixels[b] = temporary;
                }
        }

        public void DrawOverlay(Rect panel)
        {
            if (PreviewTexture == null || LatestFrame == null) { GUI.Label(panel, "CAMERA: waiting for completed rendered frame"); return; }
            GUI.BeginGroup(panel);panel=new Rect(0,0,panel.width,panel.height);
            GUI.DrawTexture(panel, PreviewTexture, ScaleMode.StretchToFill, false);
            Color before = GUI.color;
            foreach (VisualTrack track in overlayTracks)
            {
                if(DisplayFilter!="combined" && DisplayFilter!=track.Provenance) continue;
                Rect box = DemoCameraGeometry.OverlayRect(panel, track.PixelRect, LatestFrame.Width, LatestFrame.Height);
                GUI.color = track.Id == SelectedTrackId ? Color.yellow : track.Conflict ? Color.red : track.Eligible ? Color.green : Color.white;
                DrawBorder(box, 2);
                GUI.Label(new Rect(box.x, Mathf.Max(panel.y, box.y - 20), 230, 20),
                    $"#{track.Id} {track.Label} [{track.Provenance} {track.Score:0.00}]"+(track.Conflict?" CONFLICT":"") + (track.Id == SelectedTrackId ? " [TARGET]" : "") + (!track.RangeValid ? " RANGE UNKNOWN" : ""));
            }
            GUI.color = Color.white;
            float wallAge = Time.realtimeSinceStartup - lastPublishWallTime;
            string state = !IsHealthy || FreezeFeed ? "STALE" : Time.timeScale == 0 ? "PAUSED / FROZEN" : "LIVE";
            GUI.Box(new Rect(panel.x, panel.yMax - 25, panel.width, 25),
                $"{state}  seq {LatestFrame.Sequence}  age {Time.timeAsDouble - LatestFrame.CaptureTime:0.00}s  wall {wallAge:0.0}s");
            GUI.color = before;
            GUI.EndGroup();
        }

        static void DrawBorder(Rect rect, float thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        void OnDisable()
        {
            generation++;
            if(activeRequest!=null) {activeRequest.Abort();activeRequest.Dispose();activeRequest=null;}
            StopAllCoroutines();inferenceInFlight=false;serverSuccess=false;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            if (SensorCamera != null) SensorCamera.enabled = false;
            armed = false; inFlight = false; pending = null;
        }
        void OnDestroy()
        {
            if (SensorCamera != null && SensorCamera.targetTexture == target) SensorCamera.targetTexture = null;
            if (target != null) { target.Release(); Destroy(target); }
            if (PreviewTexture != null) Destroy(PreviewTexture);
        }
    }
}
