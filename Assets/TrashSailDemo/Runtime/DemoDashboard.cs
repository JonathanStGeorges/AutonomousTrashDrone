using System;
using UnityEngine;
namespace TrashSailDemo
{
    public sealed class DemoDashboard : MonoBehaviour
    {
        public DemoRuntime Runtime;
        [Tooltip("Evaluator-only scoring, never an autonomy input")]
        public int RubbishCaptured=-1,Bycatch=-1,CollisionCount=-1;
        public bool CameraOverlays=true,SonarOverlays=true,MapOverlays=true;
        string seedText="17";
        DemoScenario selectedScenario;
        bool initialized;
        GUIStyle title,body,small;
        DemoPresentation presentation;
        static readonly Color Navy=new Color(.025f,.055f,.085f,.98f),Panel=new Color(.055f,.095f,.13f,.97f),Accent=new Color(.22f,.85f,.83f);
        void OnGUI()
        {
            if(Runtime==null || Runtime.Config==null || Runtime.Mission==null || Runtime.Vessel==null || Runtime.OwnState==null)return;
            if(title==null)
            {
                title=new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold};
                body=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};
                small=new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true};
                foreach(var style in new[]{title,body,small})style.normal.textColor=Color.white;
            }
            if(!initialized){seedText=Runtime.Config.Seed.ToString();selectedScenario=Runtime.Config.Scenario;initialized=true;}
            if(presentation==null)presentation=Runtime.GetComponent<DemoPresentation>();
            Matrix4x4 saved=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1600f,Screen.height/900f,1));
            Fill(new Rect(0,0,1600,900),Navy);
            GUI.Label(new Rect(16,8,700,30),"MARUS / autonomous surface collection",title);
            GUI.Label(new Rect(760,12,810,24),"PRIMARY TWIN THRUSTERS  |  provisional actuator layout  |  seed "+Runtime.Config.Seed,body);
            GUI.Label(new Rect(16,45,1550,30),Runtime.Mission.Status.Phase+" — "+Runtime.Mission.Status.Reason,body);
            DrawViewButtons();
            int mode=presentation!=null?presentation.ViewMode:0;
            if(mode==0)
            {
                if(presentation!=null)presentation.DrawViews(new Rect(12,116,496,374),new Rect(518,116,496,374));
                DrawCamera(new Rect(1026,116,562,292));DrawSonar(new Rect(1026,420,562,326));
            }
            else if(mode==1 || mode==2)
            {
                if(presentation!=null)presentation.DrawViews(mode==1?new Rect(12,116,1576,630):Rect.zero,mode==2?new Rect(12,116,1576,630):Rect.zero);
            }
            else if(mode==3)DrawCamera(new Rect(12,116,1576,630));
            else DrawSonar(new Rect(12,116,1576,630));
            if(mode==0){DrawControls(new Rect(12,502,1002,110));DrawTelemetry(new Rect(12,622,1002,124));}
            else {DrawControls(new Rect(12,758,1002,130));DrawTelemetry(new Rect(1026,758,562,130));}
            if(mode==0)
            {
                DrawControlsLegend(new Rect(12,758,1002,130));
                var acceptance=Runtime.GetComponent<DemoAcceptance>();
                if(acceptance!=null)GUI.Label(new Rect(1038,764,540,110),acceptance.SuiteStatus,body);
            }
            if(Runtime.Paused){Fill(new Rect(600,370,400,50),Panel);GUI.Label(new Rect(620,380,380,35),"SIMULATION PAUSED — feeds frozen",title);}
            GUI.matrix=saved;GUI.color=Color.white;
        }
        void DrawViewButtons()
        {
            string[] names={"Split overview + chase","Overview","Chase","Onboard RGB","Sonar"};
            for(int i=0;i<names.Length;i++)if(GUI.Button(new Rect(12+i*166,80,158,29),names[i]) && presentation!=null)presentation.Maximize(i);
            if(GUI.Button(new Rect(852,80,110,29),"Reset views") && presentation!=null)presentation.ResetView();
            if(presentation!=null)
            {
                presentation.FollowOverview=GUI.Toggle(new Rect(978,85,140,25),presentation.FollowOverview,"Follow overview");
                presentation.ShowWorldTruth=GUI.Toggle(new Rect(1124,85,230,25),presentation.ShowWorldTruth,"Observer truth markers (GT)");
                presentation.ShowObservedTracks=GUI.Toggle(new Rect(1360,85,220,25),presentation.ShowObservedTracks,"Observed tracks T / H");
            }
        }
        void DrawCamera(Rect panel)
        {
            Fill(panel,Panel);var camera=Runtime.CameraPerception;if(camera==null)return;
            int bearingOnly=0,conflicts=0;
            foreach(var track in camera.Tracks)
            {
                if(track.Age(Time.timeAsDouble)>Runtime.Config.CameraMaxAge)continue;
                if(!track.RangeValid)bearingOnly++;
                if(track.Conflict)conflicts++;
            }
            GUI.Label(new Rect(panel.x+10,panel.y+7,panel.width-20,24),"ONBOARD RGB — actual rendered neural inference",body);
            Rect image=new Rect(panel.x+10,panel.y+36,panel.width-20,panel.height-122);
            if(camera.LatestFrame!=null)image=FitImage(image,camera.LatestFrame.Width/(float)Mathf.Max(1,camera.LatestFrame.Height));
            if(CameraOverlays)camera.DrawOverlay(image);
            else if(camera.PreviewTexture!=null)GUI.DrawTexture(image,camera.PreviewTexture,ScaleMode.ScaleToFit,false);
            GUI.Label(new Rect(panel.x+10,panel.yMax-83,panel.width-20,78),$"{camera.Health}\nActive profile: {camera.ActiveProfile}; display only: {camera.DisplayFilter}\n{camera.ModelStatus}\nAll models: {bearingOnly} bearing-only / unknown range; {conflicts} conflicts",small);
        }
        void DrawSonar(Rect panel)
        {
            Fill(panel,Panel);var sonar=Runtime.SonarPerception;if(sonar==null)return;
            GUI.Label(new Rect(panel.x+10,panel.y+7,panel.width-20,24),"SUBMERGED SONAR — measured echoes / unknown hazards",body);
            Rect image=new Rect(panel.x+10,panel.y+34,panel.width-20,panel.height-81);
            if(SonarOverlays)sonar.DrawOverlay(image);
            else if(sonar.PreviewTexture!=null)GUI.DrawTexture(image,sonar.PreviewTexture,ScaleMode.StretchToFill,false);
            string health=Runtime.Paused?"PAUSED / FROZEN":sonar.FreezeFeed?"FROZEN / STALE":sonar.IsHealthy?"LIVE":"STALE";
            GUI.Label(new Rect(panel.x+10,panel.yMax-43,panel.width-20,40),$"{health} | age {sonar.FrameAge:0.00}s | {sonar.ProcessingHz:0.0} sim Hz\nUnknown contacts remain hazards; no privileged fish classification.",small);
        }
        void DrawControls(Rect panel)
        {
            Fill(panel,Panel);float x=panel.x+10,y=panel.y+8;
            if(GUI.Button(new Rect(x,y,105,28),"Start mission"))Runtime.StartMission();
            if(GUI.Button(new Rect(x+112,y,105,28),"Stop mission"))Runtime.StopMission();
            if(GUI.Button(new Rect(x+224,y,105,28),Runtime.Paused?"Resume time":"Pause time"))Runtime.SetPaused(!Runtime.Paused);
            if(GUI.Button(new Rect(x+336,y,125,28),Runtime.Mission.Manual?"Autonomous":"Manual W/A/D"))
            {if(Runtime.Mission.Manual)Runtime.StartMission();else{Runtime.StopMission();Runtime.Mission.Manual=true;}}
            if(GUI.Button(new Rect(x+470,y,28,28),"<"))ChangeScenario(-1);
            GUI.Label(new Rect(x+503,y+4,165,24),selectedScenario.ToString(),small);
            if(GUI.Button(new Rect(x+673,y,28,28),">"))ChangeScenario(1);
            seedText=GUI.TextField(new Rect(x+710,y,70,28),seedText,9);
            if(GUI.Button(new Rect(x+790,y,110,28),"Reset seed"))Runtime.ResetScenario(selectedScenario,int.TryParse(seedText,out int parsed)?parsed:Runtime.Config.Seed,false);
            CameraOverlays=GUI.Toggle(new Rect(x,y+39,125,24),CameraOverlays,"RGB overlays");
            SonarOverlays=GUI.Toggle(new Rect(x+130,y+39,135,24),SonarOverlays,"Sonar overlays");
            if(presentation!=null)presentation.ShowRoutes=GUI.Toggle(new Rect(x+270,y+39,135,24),presentation.ShowRoutes,"Coverage routes");
            if(Runtime.CameraPerception!=null)Runtime.CameraPerception.FreezeFeed=GUI.Toggle(new Rect(x+410,y+39,140,24),Runtime.CameraPerception.FreezeFeed,"Freeze camera");
            if(Runtime.SonarPerception!=null)Runtime.SonarPerception.FreezeFeed=GUI.Toggle(new Rect(x+555,y+39,130,24),Runtime.SonarPerception.FreezeFeed,"Freeze sonar");
            GUI.Label(new Rect(x,y+70,240,23),"RGB display filter (autonomy unchanged):",small);
            string[] filters={"combined","YOLOE","TRASH"};
            for(int i=0;i<3;i++)if(GUI.Button(new Rect(x+245+i*95,y+68,88,25),filters[i]) && Runtime.CameraPerception!=null)Runtime.CameraPerception.DisplayFilter=filters[i];
            GUI.Label(new Rect(x+550,y+70,170,22),$"Sonar noise {Runtime.Config.SonarNoise:0.000}",small);
            Runtime.Config.SonarNoise=GUI.HorizontalSlider(new Rect(x+722,y+77,220,20),Runtime.Config.SonarNoise,0,.15f);
        }
        void ChangeScenario(int delta)
        {int count=Enum.GetValues(typeof(DemoScenario)).Length;selectedScenario=(DemoScenario)(((int)selectedScenario+delta+count)%count);}
        void DrawTelemetry(Rect panel)
        {
            Fill(panel,Panel);var state=Runtime.OwnState.State;var command=Runtime.Vessel.LastCommand;var target=Runtime.Mission.SelectedTarget;
            var evaluation=Runtime.GetComponent<DemoEvaluation>();
            if(evaluation!=null){RubbishCaptured=evaluation.CorrectCaptures;Bycatch=evaluation.Bycatch;CollisionCount=evaluation.CollisionEvents;}
            var coverage=Runtime.GetComponent<CoveragePlanner>();
            string route=coverage==null?"not initialized":$"{coverage.WaypointIndex}/{coverage.Route.Count} {(coverage.Suspended?"SUSPENDED":coverage.Complete?"COMPLETE":"ACTIVE")}";
            GUI.Label(new Rect(panel.x+10,panel.y+7,panel.width-20,panel.height-10),
                $"Speed {DemoMath.Flat(state.Velocity).magnitude:0.00} m/s | heading {DemoMath.Heading(state.Forward):0} deg | requested {command.RequestedSpeed:0.00} m/s @ {command.RequestedHeading:0} deg\n"+
                $"Port {Runtime.Vessel.PortCommand:+0.00;-0.00;0.00} ({Runtime.Vessel.PortAppliedForce:0.0} N) / starboard {Runtime.Vessel.StarboardCommand:+0.00;-0.00;0.00} ({Runtime.Vessel.StarboardAppliedForce:0.0} N) | {(Runtime.Vessel.ActuatorHealthy?"ACTUATORS OK":"ACTUATOR FAULT/STALE")} | intake {(Runtime.Vessel.Collector.Active?"OPEN":"CLOSED")} | fill {Runtime.Vessel.Collector.CaptureCount}/{Runtime.Config.Capacity:0}\n"+
                $"Target {(target==null?"none":"#"+target.Id+" "+target.Label+" score="+target.Score.ToString("0.00"))} | coverage {route}\n"+
                $"Evaluator: rubbish {RubbishCaptured}, bycatch {Bycatch}, collisions {CollisionCount} | {Runtime.RenderingFps:0.0} FPS | inference {Runtime.CameraPerception.InferenceMilliseconds:0.0} ms",small);
        }
        void DrawControlsLegend(Rect panel)
        {
            Fill(panel,Panel);var coverage=Runtime.GetComponent<CoveragePlanner>();
            GUI.Label(new Rect(panel.x+10,panel.y+8,panel.width-20,panel.height-16),
                "Observer views are independent of the onboard sensing camera. Drag overview to pan; wheel to zoom. Drag chase to orbit.\n"+
                "Ground-truth world dots are observer-only. T = RGB estimate, H = sonar contact. Display filtering never changes inference or safety decisions.\n"+
                "Blue = planned coverage; cyan = actual travelled route, including diversions. Travelled route does not establish full area observed.\n"+
                $"Coverage spacing {Runtime.Config.LaneSpacing:0.0} m; assumed useful detection range {Runtime.Config.UsefulDetectionRange:0.0} m. "+
                "Stop closes intake and commands physical stop; Pause freezes the simulation.",body);
        }
        static Rect FitImage(Rect available,float aspect)
        {float width=Mathf.Min(available.width,available.height*aspect),height=width/aspect;return new Rect(available.center.x-width*.5f,available.center.y-height*.5f,width,height);}
        static void Fill(Rect rect,Color color){Color saved=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=saved;}
    }
}
