using System;
using UnityEngine;

namespace TrashSailDemo
{
    public sealed class DemoDashboard : MonoBehaviour
    {
        public DemoRuntime Runtime;
        [Tooltip("Evaluator-only score display; never read by mission control.")]
        public int RubbishCaptured = -1, Bycatch = -1, CollisionCount = -1;
        public bool CameraOverlays = true, SonarOverlays = true, MapOverlays = true;
        string seedText = "17";
        DemoScenario selectedScenario;
        bool initialized;
        GUIStyle title, section, body, small, status, button;
        static readonly Color Navy = new Color(0.025f, 0.055f, 0.085f, 0.97f);
        static readonly Color Panel = new Color(0.055f, 0.095f, 0.13f, 0.96f);
        static readonly Color Accent = new Color(0.22f, 0.85f, 0.83f);

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            section = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            status = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            foreach (GUIStyle style in new[] { title, section, body, small, status }) style.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (Runtime == null || Runtime.Config == null || Runtime.Mission == null || Runtime.OwnState == null) return;
            Styles();
            if (!initialized) { seedText = Runtime.Config.Seed.ToString(); selectedScenario = Runtime.Config.Scenario; initialized = true; }
            // Ground-truth labels are display-only evaluator output; these values
            // are never passed back into Runtime.Mission or the sensor pipeline.
            var evaluation = Runtime.GetComponent<DemoEvaluation>();
            if (evaluation != null)
            {
                RubbishCaptured = evaluation.CorrectCaptures;
                Bycatch = evaluation.Bycatch;
                CollisionCount = evaluation.CollisionEvents;
            }
            var acceptance = Runtime.GetComponent<DemoAcceptance>();
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1600f, Screen.height / 900f, 1));
            GUI.color = Color.white;
            Fill(new Rect(0, 0, 1600, 56), Navy);
            GUI.Label(new Rect(16, 7, 600, 38), "TRASH SAIL / MARUS engineering demo", title);
            bool motor = Runtime.Config.MotorAssistance;
            Label(new Rect(730, 14, 310, 30), motor ? "MOTOR ASSIST ENABLED" : "SAIL ONLY / MOTOR DISABLED", motor ? new Color(1, .73f, .25f) : Accent, section);
            GUI.Label(new Rect(1230, 16, 350, 28), $"seed {Runtime.Config.Seed}  •  {Runtime.Config.Scenario}", body);

            Fill(new Rect(12, 66, 1006, 100), new Color(.025f, .055f, .085f, .88f));
            GUI.Label(new Rect(24, 73, 980, 29), Runtime.Mission.Status.Phase.ToString().Replace('_', ' '), status);
            GUI.Label(new Rect(24, 104, 980, 34), Runtime.Mission.Status.Reason, body);
            if (acceptance != null) Label(new Rect(24, 143, 980, 21), acceptance.SuiteStatus, Accent, small);
            if (Runtime.Paused) Label(new Rect(350, 270, 460, 60), "SIMULATION PAUSED", Color.yellow, title);

            DrawSensors();
            DrawControls();
            DrawMap(new Rect(12, 662, 400, 226));
            DrawTelemetry(new Rect(424, 662, 594, 226));
            GUI.matrix = before;
            GUI.color = Color.white;
        }

        void DrawSensors()
        {
            Fill(new Rect(1030, 64, 558, 350), Panel);
            GUI.Label(new Rect(1042, 71, 540, 25), "ONBOARD RGB  •  synthetic-demo colour heuristic", section);
            var camera = Runtime.CameraPerception;
            Rect image = new Rect(1042, 102, 534, 270);
            if (camera != null)
            {
                if (CameraOverlays) camera.DrawOverlay(image);
                else if (camera.PreviewTexture != null) GUI.DrawTexture(image, camera.PreviewTexture, ScaleMode.StretchToFill, false);
                else GUI.Label(image, "Waiting for actual rendered image", body);
                Label(new Rect(1042, 377, 534, 32), camera.Health + $"  |  {camera.ProcessingHz:0.0} sim Hz", camera.IsHealthy ? Accent : Color.yellow, small);
            }
            Fill(new Rect(1030, 424, 558, 464), Panel);
            GUI.Label(new Rect(1042, 432, 534, 26), "SUBMERGED SONAR  •  measured range / bearing", section);
            Rect sonarPanel = new Rect(1042, 465, 534, 365);
            var sonar = Runtime.SonarPerception;
            if (sonar != null)
            {
                if (SonarOverlays) sonar.DrawOverlay(sonarPanel);
                else if (sonar.PreviewTexture != null) GUI.DrawTexture(sonarPanel, sonar.PreviewTexture, ScaleMode.StretchToFill, false);
                else GUI.Label(sonarPanel, "Waiting for measured sonar image", body);
                string age = double.IsInfinity(sonar.FrameAge) ? "none" : $"{sonar.FrameAge:0.00}s";
                string health=sonar.FreezeFeed?"FROZEN / STALE":Runtime.Paused?"PAUSED / FROZEN":sonar.IsHealthy?"LIVE":"STALE";
                Label(new Rect(1042, 838, 534, 22), $"{health}  •  age {age}  •  {sonar.ProcessingHz:0.0} sim Hz", sonar.IsHealthy ? Accent : Color.yellow, body);
                GUI.Label(new Rect(1042, 862, 534, 23), "H = unknown contact hazard. No fish classifier or truth labels.", small);
            }
        }

        void DrawControls()
        {
            Fill(new Rect(12, 548, 1006, 103), Navy);
            if (GUI.Button(new Rect(22, 557, 107, 33), "Start mission", button)) Runtime.StartMission();
            if (GUI.Button(new Rect(137, 557, 107, 33), "Stop mission", button)) Runtime.StopMission();
            if (GUI.Button(new Rect(252, 557, 105, 33), Runtime.Paused ? "Resume time" : "Pause time", button)) Runtime.SetPaused(!Runtime.Paused);
            if (GUI.Button(new Rect(365, 557, 116, 33), Runtime.Mission.Manual ? "Autonomous" : "Manual W/A/D", button))
            {
                if (Runtime.Mission.Manual) Runtime.StartMission();
                else { Runtime.StopMission(); Runtime.Mission.Manual = true; }
            }
            if (GUI.Button(new Rect(489, 557, 102, 33), "Switch view", button)) Runtime.ViewMode = (Runtime.ViewMode + 1) % 2;
            if (GUI.Button(new Rect(599, 557, 30, 33), "<", button)) ChangeScenario(-1);
            GUI.Label(new Rect(635, 564, 171, 26), selectedScenario.ToString(), small);
            if (GUI.Button(new Rect(805, 557, 30, 33), ">", button)) ChangeScenario(1);
            seedText = GUI.TextField(new Rect(842, 558, 60, 31), seedText, 9);
            if (GUI.Button(new Rect(910, 557, 97, 33), "Reset seed", button))
            {
                int seed = int.TryParse(seedText, out int parsed) ? parsed : Runtime.Config.Seed;
                Runtime.ResetScenario(selectedScenario, seed, false);
            }
            bool motor = GUI.Toggle(new Rect(22, 603, 126, 25), Runtime.Config.MotorAssistance, "Motor assist");
            if (motor != Runtime.Config.MotorAssistance) Runtime.Config.MotorAssistance = motor;
            CameraOverlays = GUI.Toggle(new Rect(156, 603, 120, 25), CameraOverlays, "RGB overlays");
            SonarOverlays = GUI.Toggle(new Rect(279, 603, 124, 25), SonarOverlays, "Sonar overlays");
            MapOverlays = GUI.Toggle(new Rect(410, 603, 95, 25), MapOverlays, "Local map");
            if (Runtime.CameraPerception != null) Runtime.CameraPerception.FreezeFeed = GUI.Toggle(new Rect(510, 603, 123, 25), Runtime.CameraPerception.FreezeFeed, "Freeze camera");
            if (Runtime.SonarPerception != null) Runtime.SonarPerception.FreezeFeed = GUI.Toggle(new Rect(640, 603, 119, 25), Runtime.SonarPerception.FreezeFeed, "Freeze sonar");
            GUI.Label(new Rect(770, 595, 230, 22), $"Sonar noise {Runtime.Config.SonarNoise:0.000}", small);
            Runtime.Config.SonarNoise = GUI.HorizontalSlider(new Rect(770, 623, 228, 18), Runtime.Config.SonarNoise, 0, 0.15f);
        }

        void ChangeScenario(int change)
        {
            int count = Enum.GetValues(typeof(DemoScenario)).Length;
            selectedScenario = (DemoScenario)(((int)selectedScenario + count + change) % count);
        }

        void DrawTelemetry(Rect panel)
        {
            Fill(panel, Panel);
            var state = Runtime.OwnState.State;
            var command = Runtime.Vessel.LastCommand;
            var target = Runtime.Mission.SelectedTarget;
            var collector = Runtime.Vessel.Collector;
            GUI.Label(new Rect(panel.x + 12, panel.y + 8, panel.width - 20, 26), "VEHICLE / DECISION TELEMETRY", section);
            string targetText = target == null ? "none" : $"#{target.Id} {target.Label} H={target.Score:0.00} age={target.Age(Time.timeAsDouble):0.00}s";
            string clearance = float.IsInfinity(Runtime.Mission.Status.ClosestClearance) ? "no contact" : $"{Runtime.Mission.Status.ClosestClearance:0.00} m";
            string scoring = RubbishCaptured < 0 ? "Evaluator score pending" : $"Rubbish {RubbishCaptured} / bycatch {Bycatch} / collisions {CollisionCount}";
            GUI.Label(new Rect(panel.x + 12, panel.y + 39, panel.width - 24, 178),
                $"Target: {targetText}\n" +
                $"Speed {DemoMath.Flat(state.Velocity).magnitude:0.00} m/s   Heading {DemoMath.Heading(state.Forward):0}°   Wind {state.WindTo.magnitude:0.0} m/s to {DemoMath.Heading(state.WindTo):0}°\n" +
                $"Rudder {command.Rudder:+0.00;-0.00;0.00}   Sail {command.SailTrim:0.00}   Motor {command.Motor:0.00}   {(Runtime.Mission.Status.Tacking ? "TACKING" : "")}\n" +
                $"Intake {(collector.Active ? "OPEN" : "CLOSED")}   Storage {collector.CaptureCount}/{Runtime.Config.Capacity:0} ({collector.Fill * 100:0}%)   Clearance {clearance}\n" +
                $"{scoring}\n" +
                $"Render {Runtime.RenderingFps:0.0} FPS   RGB inference {Runtime.CameraPerception.InferenceMilliseconds:0.00} ms\n" +
                $"Own-state: {(state.Ideal ? "IDEAL (declared)" : "NOISY / DELAYED")}  •  Stop depowers; Pause freezes physics.", body);
        }

        void DrawMap(Rect panel)
        {
            Fill(panel, Panel);
            GUI.Label(new Rect(panel.x + 10, panel.y + 7, panel.width - 20, 25), "ESTIMATED LOCAL MAP  •  metres", section);
            if (!MapOverlays) { GUI.Label(new Rect(panel.x + 10, panel.y + 60, 300, 25), "Map overlay disabled", body); return; }
            Rect plot = new Rect(panel.x + 40, panel.y + 37, panel.width - 75, panel.height - 66);
            float half = Runtime.Config.BoundaryHalfWidth, length = Runtime.Config.BoundaryLength;
            Vector2 Map(Vector3 world) => new Vector2(plot.x + Mathf.Clamp01((world.x + half) / (2 * half)) * plot.width,
                plot.yMax - Mathf.Clamp01((world.z + 6) / (length + 6)) * plot.height);
            Border(plot, new Color(.35f, .5f, .6f));
            Vector2 vessel = Map(Runtime.OwnState.State.Position);
            if (Runtime.SonarPerception != null && Runtime.SonarPerception.LatestFrame != null)
            {
                SonarFrame frame = Runtime.SonarPerception.LatestFrame;
                Vector2 origin = Map(frame.Origin);
                foreach (float bearing in new[] { -frame.HorizontalFov / 2, frame.HorizontalFov / 2 })
                    Line(origin, Map(frame.Origin + frame.Rotation * (Quaternion.Euler(0, bearing, 0) * Vector3.forward) * frame.MaxRange), new Color(.15f, .45f, .5f), 1);
                foreach (ContactTrack contact in Runtime.SonarPerception.Tracks)
                {
                    Vector2 p = Map(contact.Position);
                    Label(new Rect(p.x - 7, p.y - 9, 35, 19), "H" + contact.Id, Color.yellow, small);
                }
            }
            if (Runtime.CameraPerception != null)
                foreach (VisualTrack track in Runtime.CameraPerception.Tracks)
                {
                    if (!track.RangeValid) continue;
                    Vector2 p = Map(track.Position);
                    Label(new Rect(p.x - 7, p.y - 9, 40, 20), "T" + track.Id, track.Id == Runtime.Mission.Status.TargetId ? Color.yellow : Accent, small);
                }
            Vector2 goal = Map(Runtime.Mission.Status.Goal);
            Line(vessel, goal, Accent, 1);
            Fill(new Rect(goal.x - 3, goal.y - 3, 6, 6), Accent);
            Vector2 nose = Map(Runtime.OwnState.State.Position + Runtime.OwnState.State.Forward * 2.5f);
            Line(vessel, nose, Color.white, 3);
            Fill(new Rect(vessel.x - 3, vessel.y - 3, 6, 6), Color.white);
            GUI.Label(new Rect(panel.x + 10, panel.yMax - 23, panel.width - 20, 20), "T = RGB track   H = sonar unknown   line = active goal", small);
        }

        static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = before;
        }
        static void Label(Rect rect, string text, Color color, GUIStyle style)
        {
            Color before = GUI.color; GUI.color = color; GUI.Label(rect, text, style); GUI.color = before;
        }
        static void Border(Rect rect, Color color)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 1), color); Fill(new Rect(rect.x, rect.yMax, rect.width, 1), color);
            Fill(new Rect(rect.x, rect.y, 1, rect.height), color); Fill(new Rect(rect.xMax, rect.y, 1, rect.height), color);
        }
        static void Line(Vector2 from, Vector2 to, Color color, float thickness)
        {
            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg, from);
            Fill(new Rect(from.x, from.y - thickness / 2, Vector2.Distance(from, to), thickness), color);
            GUI.matrix = before;
        }
    }
}
