using UnityEngine;
namespace TrashSailDemo
{
    // Observer-only truth access. No data from this component is consumed by autonomy.
    public sealed class DemoPresentation : MonoBehaviour
    {
        public DemoRuntime Runtime;
        public Camera OverviewCamera,ChaseCamera;
        public int ViewMode; // 0 split, 1 overview, 2 chase, 3 RGB, 4 sonar
        public bool ShowWorldTruth=true,ShowObservedTracks=true,ShowRoutes=true,FollowOverview;
        public float OverviewZoom=1,ChaseDistance=13,ChaseYaw=-35,ChaseElevation=18;
        public Vector3 OverviewCentre;
        RenderTexture overviewTexture,chaseTexture;
        Texture2D routeOverlay;
        Color32[] routePixels;
        Vector2 routePanelSize;
        bool rasterizing;
        Rect overviewInput,chaseInput;
        public RenderTexture OverviewTexture=>overviewTexture;
        public RenderTexture ChaseTexture=>chaseTexture;
        public void Initialize(DemoRuntime runtime)
        {
            Runtime=runtime;
            if(OverviewCamera==null)OverviewCamera=CreateCamera("Observer overview camera");
            if(ChaseCamera==null)ChaseCamera=CreateCamera("Independent chase camera");
            overviewTexture=MakeTexture(overviewTexture,"Observer overview output");
            chaseTexture=MakeTexture(chaseTexture,"Observer chase output");
            OverviewCamera.targetTexture=overviewTexture;ChaseCamera.targetTexture=chaseTexture;
            OverviewCamera.orthographic=true;OverviewCamera.farClipPlane=4000;
            ChaseCamera.orthographic=false;ChaseCamera.fieldOfView=55;
            if(Runtime.MainCamera!=null && Runtime.MainCamera!=OverviewCamera && Runtime.MainCamera!=ChaseCamera)Runtime.MainCamera.enabled=false;
            ResetView();
        }
        Camera CreateCamera(string label)
        {
            var root=new GameObject(label);root.transform.SetParent(transform,false);
            var camera=root.AddComponent<Camera>();
            if(Runtime.MainCamera!=null)camera.CopyFrom(Runtime.MainCamera);
            camera.enabled=true;camera.rect=new Rect(0,0,1,1);camera.depth=-5;
            return camera;
        }
        RenderTexture MakeTexture(RenderTexture previous,string label)
        {
            if(previous!=null)return previous;
            var texture=new RenderTexture(960,600,24,RenderTextureFormat.ARGB32){name=label};texture.Create();return texture;
        }
        public void Maximize(int view){ViewMode=Mathf.Clamp(view,0,4);}
        public void ResetView()
        {
            ViewMode=0;OverviewZoom=1;FollowOverview=false;ChaseDistance=13;ChaseYaw=-35;ChaseElevation=18;
            if(Runtime!=null && Runtime.Config!=null)OverviewCentre=new Vector3(0,0,(Runtime.Config.LargeOcean?Runtime.Config.MissionLength:Runtime.Config.BoundaryLength)*.5f);
        }
        void LateUpdate()
        {
            if(Runtime==null || Runtime.Vessel==null || OverviewCamera==null)return;
            Vector3 ship=Runtime.Vessel.transform.position;
            if(FollowOverview)OverviewCentre=ship;
            var config=Runtime.Config;
            float width=config.LargeOcean?config.MissionWidth:config.BoundaryHalfWidth*2;
            float length=config.LargeOcean?config.MissionLength:config.BoundaryLength;
            OverviewCamera.orthographicSize=Mathf.Max(length*.55f,width*.55f/OverviewCamera.aspect)/OverviewZoom;
            OverviewCamera.transform.SetPositionAndRotation(OverviewCentre+Vector3.up*1500,Quaternion.Euler(90,0,0));
            Quaternion orbit=Quaternion.Euler(ChaseElevation,ChaseYaw+Runtime.Vessel.transform.eulerAngles.y,0);
            ChaseCamera.transform.position=ship+orbit*new Vector3(0,0,-ChaseDistance);
            ChaseCamera.transform.LookAt(ship+Vector3.up*.5f);
        }
        public void DrawViews(Rect overview,Rect chase)
        {
            if(OverviewCamera==null && Runtime!=null)Initialize(Runtime);
            overview=FitView(overview);chase=FitView(chase);
            overviewInput=overview;chaseInput=chase;
            if(overview.width>0 && overviewTexture!=null){GUI.DrawTexture(overview,overviewTexture,ScaleMode.StretchToFill,false);DrawOverviewOverlay(overview);}
            if(chase.width>0 && chaseTexture!=null){GUI.DrawTexture(chase,chaseTexture,ScaleMode.StretchToFill,false);GUI.Label(new Rect(chase.x+8,chase.y+8,chase.width-16,25),"Chase view — drag to orbit / wheel to zoom");}
            HandleInput();
        }
        static Rect FitView(Rect panel)
        {
            if(panel.width<=0 || panel.height<=0)return Rect.zero;
            const float aspect=960f/600;
            float width=Mathf.Min(panel.width,panel.height*aspect),height=width/aspect;
            return new Rect(panel.center.x-width*.5f,panel.center.y-height*.5f,width,height);
        }
        void HandleInput()
        {
            Event e=Event.current;if(e==null)return;
            if(overviewInput.Contains(e.mousePosition))
            {
                if(e.type==EventType.ScrollWheel){OverviewZoom=Mathf.Clamp(OverviewZoom*Mathf.Exp(-e.delta.y*.1f),.6f,80);e.Use();}
                if(e.type==EventType.MouseDrag && e.button==0){FollowOverview=false;float scale=OverviewCamera.orthographicSize*2/overviewInput.height;OverviewCentre+=new Vector3(-e.delta.x*scale,0,e.delta.y*scale);e.Use();}
            }
            if(chaseInput.Contains(e.mousePosition))
            {
                if(e.type==EventType.ScrollWheel){ChaseDistance=Mathf.Clamp(ChaseDistance+e.delta.y,4,60);e.Use();}
                if(e.type==EventType.MouseDrag && e.button==0){ChaseYaw+=e.delta.x*.4f;ChaseElevation=Mathf.Clamp(ChaseElevation-e.delta.y*.4f,8,80);e.Use();}
            }
        }
        public void DrawOverviewOverlay(Rect panel)
        {
            if(Runtime==null || OverviewCamera==null)return;
            Matrix4x4 savedMatrix=GUI.matrix;
            Color savedColor=GUI.color;
            GUI.BeginGroup(panel);
            try
            {
            BeginRouteRaster(panel);
            Vector2 Map(Vector3 p){Vector3 v=OverviewCamera.WorldToViewportPoint(p);return new Vector2(v.x*panel.width,(1-v.y)*panel.height);}
            var cfg=Runtime.Config;float half=cfg.LargeOcean?cfg.MissionWidth*.5f:cfg.BoundaryHalfWidth,length=cfg.LargeOcean?cfg.MissionLength:cfg.BoundaryLength;
            Vector3[] corners={new Vector3(-half,0,0),new Vector3(half,0,0),new Vector3(half,0,length),new Vector3(-half,0,length)};
            if(rasterizing)for(int i=0;i<4;i++)Line(Map(corners[i]),Map(corners[(i+1)%4]),Color.white,1);
            var coverage=Runtime.GetComponent<CoveragePlanner>();
            if(rasterizing && ShowRoutes && coverage!=null)
            {
                for(int i=1;i<coverage.Route.Count;i++)Line(Map(coverage.Route[i-1]),Map(coverage.Route[i]),new Color(.25f,.6f,.9f,.6f),1);
                for(int i=1;i<coverage.TravelledRoute.Count;i++)Line(Map(coverage.TravelledRoute[i-1]),Map(coverage.TravelledRoute[i]),Color.cyan,2);
            }
            if(rasterizing){routeOverlay.SetPixels32(routePixels);routeOverlay.Apply(false,false);}
            GUI.DrawTexture(new Rect(0,0,panel.width,panel.height),routeOverlay,ScaleMode.StretchToFill,true);
            var population=Runtime.GetComponent<OceanPopulation>();
            if(ShowWorldTruth && population!=null)foreach(var record in population.Records)
            {
                if(record.Collected)continue;
                Color color=record.Kind=="trash"?new Color(1,.65f,.15f):record.Kind=="fish"?Color.magenta:Color.gray;
                Dot(Map(record.Position),color,record.Kind=="trash"?3:4);
            }
            if(ShowObservedTracks)
            {
                if(Runtime.CameraPerception!=null)foreach(var track in Runtime.CameraPerception.Tracks)if(track.RangeValid){Vector2 p=Map(track.Position);GUI.Label(new Rect(p.x,p.y,70,20),"T"+track.Id);}
                if(Runtime.SonarPerception!=null)foreach(var track in Runtime.SonarPerception.Tracks){Vector2 p=Map(track.Position);GUI.Label(new Rect(p.x,p.y,70,20),"H"+track.Id);}
            }
            int bearingOnly=0;
            if(ShowObservedTracks && Runtime.CameraPerception!=null)
                foreach(var track in Runtime.CameraPerception.Tracks)
                    if(!track.RangeValid && track.Age(Time.timeAsDouble)<=Runtime.Config.CameraMaxAge)bearingOnly++;
            if(bearingOnly>0)GUI.Label(new Rect(8,34,panel.width-16,40),bearingOnly+" optical observations have no range; see RGB (not placed on map)");
            if(Runtime.Vessel!=null){Vector2 p=Map(Runtime.Vessel.transform.position);Dot(p,Color.white,7);GUI.Label(new Rect(p.x+7,p.y-10,90,22),"DRONE");}
            GUI.Label(new Rect(8,8,panel.width-16,24),"Observer overview — full simulated world");
            GUI.Label(new Rect(8,panel.height-46,panel.width-16,44),$"N ↑  {2*OverviewCamera.orthographicSize:0} m vertical span\nDots = world truth; T/H = observations; cyan = travelled, blue = planned");
            }
            finally {GUI.matrix=savedMatrix;GUI.color=savedColor;GUI.EndGroup();}
        }
        static void Dot(Vector2 p,Color c,float size){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(p.x-size*.5f,p.y-size*.5f,size,size),Texture2D.whiteTexture);GUI.color=old;}
        void BeginRouteRaster(Rect panel)
        {
            routePanelSize=panel.size;
            int width=Mathf.Clamp(Mathf.CeilToInt(panel.width),1,1024),height=Mathf.Clamp(Mathf.CeilToInt(panel.height),1,768);
            if(routeOverlay==null || routeOverlay.width!=width || routeOverlay.height!=height)
            {
                if(routeOverlay!=null)Destroy(routeOverlay);
                routeOverlay=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Observer clipped route overlay"};
                routePixels=new Color32[width*height];
            }
            rasterizing=Event.current.type==EventType.Repaint;
            if(rasterizing)System.Array.Clear(routePixels,0,routePixels.Length);
        }
        // Liang-Barsky clipping before rasterization. No rotated GUI primitives:
        // all line pixels are bounded by the texture and one panel-sized draw.
        void Line(Vector2 a,Vector2 b,Color c,float width)
        {
            if(!rasterizing)return;
            Vector2 delta=b-a;float enter=0,leave=1;
            if(!Clip(-delta.x,a.x,ref enter,ref leave) || !Clip(delta.x,routePanelSize.x-a.x,ref enter,ref leave)
                || !Clip(-delta.y,a.y,ref enter,ref leave) || !Clip(delta.y,routePanelSize.y-a.y,ref enter,ref leave))return;
            b=a+delta*leave;a+=delta*enter;
            a=new Vector2(a.x/routePanelSize.x*(routeOverlay.width-1),a.y/routePanelSize.y*(routeOverlay.height-1));
            b=new Vector2(b.x/routePanelSize.x*(routeOverlay.width-1),b.y/routePanelSize.y*(routeOverlay.height-1));
            int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x-a.x),Mathf.Abs(b.y-a.y))));
            int radius=width>1?1:0;Color32 pixel=c;
            for(int step=0;step<=steps;step++)
            {
                Vector2 p=Vector2.Lerp(a,b,step/(float)steps);int x=Mathf.RoundToInt(p.x),y=routeOverlay.height-1-Mathf.RoundToInt(p.y);
                for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
                    if(x+dx>=0 && x+dx<routeOverlay.width && y+dy>=0 && y+dy<routeOverlay.height)routePixels[(y+dy)*routeOverlay.width+x+dx]=pixel;
            }
        }
        static bool Clip(float p,float q,ref float enter,ref float leave)
        {
            if(Mathf.Abs(p)<.000001f)return q>=0;
            float t=q/p;
            if(p<0){if(t>leave)return false;enter=Mathf.Max(enter,t);}
            else{if(t<enter)return false;leave=Mathf.Min(leave,t);}
            return true;
        }
        void OnDestroy()
        {
            if(overviewTexture!=null){overviewTexture.Release();Destroy(overviewTexture);}
            if(chaseTexture!=null){chaseTexture.Release();Destroy(chaseTexture);}
            if(routeOverlay!=null)Destroy(routeOverlay);
        }
    }
}
