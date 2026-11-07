using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashSailDemo
{
    [DefaultExecutionOrder(-300)] public sealed class DemoRuntime : MonoBehaviour
    {
        public DemoConfig Config;
        public DemoVessel Vessel;
        public DemoOwnState OwnState;
        public DemoCameraPerception CameraPerception;
        public DemoSonarPerception SonarPerception;
        public DemoMission Mission;
        public DemoWorld World;
        public OceanPopulation Population;
        public CoveragePlanner Coverage;
        public DemoPresentation Presentation;
        public RenderPipelineAsset ScenePipeline;
        public Camera MainCamera;
        public bool Paused;
        public int ViewMode;
        public float RenderingFps;
        public bool AutoStart=true;
        public string SoftwareRevision="Unversioned local demo";
        public event Action ScenarioReset;
        Coroutine startRoutine;
        bool previousQueryTriggers;
        RenderPipelineAsset previousPipeline;
        bool resumeAfterPause;
        void Start()
        {
            Application.runInBackground=true;
            previousPipeline=QualitySettings.renderPipeline;
            if(ScenePipeline!=null)QualitySettings.renderPipeline=ScenePipeline;
            // The installed optional visualization bridge auto-starts before scenes.
            // This dedicated offline scene does not export ROS streams. Disable only
            // those communication components, leaving sensing and physics active.
            foreach(var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(component.GetType().Name=="VisualizationGrpc" || component is Marus.Networking.RosConnection)
                {component.StopAllCoroutines();component.gameObject.SetActive(false);}
            Config=Instantiate(Config); Config.name="Runtime configuration (asset unchanged)";
            Vessel.Config=Config; Vessel.Collector.Config=Config; OwnState.Config=Config;
            CameraPerception.Config=Config; SonarPerception.Config=Config; Mission.Config=Config; World.Config=Config;
            CameraPerception.OwnStateProvider=OwnState;
            CameraPerception.ActiveProfile=Config.InferenceProfile;CameraPerception.MaxResultWallAge=Config.InferenceWallMaxAge;
            CameraPerception.ServiceUrl=new Uri(Config.InferenceUrl).GetLeftPart(UriPartial.Authority);
            SonarPerception.OwnStateProvider=OwnState;
            if(Population==null)Population=GetComponent<OceanPopulation>();
            if(Population!=null){Population.Config=Config;Population.Vessel=Vessel;}
            if(Coverage==null)Coverage=GetComponent<CoveragePlanner>();
            if(Coverage==null)Coverage=gameObject.AddComponent<CoveragePlanner>();
            Coverage.TurningRadius=Config.CoverageTurnRadius;Coverage.ArrivalRadius=Config.WaypointArrivalRadius;Mission.Coverage=Coverage;
            if(Presentation==null)Presentation=GetComponent<DemoPresentation>();
            if(Presentation==null)Presentation=gameObject.AddComponent<DemoPresentation>();
            Presentation.Initialize(this);
            DemoOceanRendering.Configure(Presentation.OverviewCamera);DemoOceanRendering.Configure(Presentation.ChaseCamera);DemoOceanRendering.Configure(CameraPerception.SensorCamera);DemoOceanRendering.Configure(MainCamera,false);
            // Clear the physical display behind spectator render textures and IMGUI.
            if(MainCamera!=null){MainCamera.enabled=true;MainCamera.cullingMask=0;MainCamera.clearFlags=CameraClearFlags.SolidColor;MainCamera.backgroundColor=Color.black;}
            previousQueryTriggers=Physics.queriesHitTriggers;
            Physics.queriesHitTriggers=false; // MARUS rays must not intersect the collector trigger.
            Time.fixedDeltaTime=0.02f; QualitySettings.vSyncCount=0; Application.targetFrameRate=90;
            ResetScenario(Config.Scenario,Config.Seed,AutoStart);
        }
        public void ResetScenario(DemoScenario scenario,int seed,bool start=true)
        {
            if(startRoutine!=null)StopCoroutine(startRoutine);
            resumeAfterPause=false;
            Paused=false;Time.timeScale=1; Mission.StopMission();
            Config.CameraDropout=0; Config.SonarDropout=0.02f;
            Config.Scenario=scenario;Config.Seed=seed;
            if(Config.LargeOcean && Population!=null){World.ClearWorld();Population.ResetPopulation(seed);}
            else{Population?.ClearPopulation();World.ResetWorld(scenario,seed);}
            Coverage.Initialize(Config,OwnState.State.Position);
            OwnState.ResetState(seed); CameraPerception.ResetSensor(seed); SonarPerception.ResetSensor(seed);
            Mission.ResetMission(); ScenarioReset?.Invoke();
            if(start)startRoutine=StartCoroutine(StartWhenReady());
        }
        IEnumerator StartWhenReady()
        {
            yield return new WaitForSeconds(.5f);double deadline=Time.timeAsDouble+15;
            while((!CameraPerception.IsHealthy || !SonarPerception.IsHealthy) && Time.timeAsDouble<deadline)yield return null;
            Mission.StartMission(); startRoutine=null;
        }
        public void StopMission(){resumeAfterPause=false;if(startRoutine!=null){StopCoroutine(startRoutine);startRoutine=null;}Mission.StopMission();}
        public void StartMission(){if(startRoutine!=null){StopCoroutine(startRoutine);startRoutine=null;}Mission.StartMission();}
        void OnDestroy(){Physics.queriesHitTriggers=previousQueryTriggers;if(ScenePipeline!=null && QualitySettings.renderPipeline==ScenePipeline)QualitySettings.renderPipeline=previousPipeline;Time.timeScale=1;}
        public void SetPaused(bool pause)
        {
            if(pause){bool wasRunning=Mission.Running;StopMission();resumeAfterPause=wasRunning;Paused=true;Time.timeScale=0;}
            else{Paused=false;Time.timeScale=1;if(resumeAfterPause){resumeAfterPause=false;startRoutine=StartCoroutine(StartWhenReady());}}
        }
        void LateUpdate()
        {
            RenderingFps=Mathf.Lerp(RenderingFps,1/Mathf.Max(0.001f,Time.unscaledDeltaTime),0.03f);
            if(Presentation!=null || MainCamera==null || Vessel==null)return;
            Vector3 follow=Vessel.transform.position;
            Vector3 offset=ViewMode%2==0?new Vector3(-8,10,-11):new Vector3(0,25,-3);
            MainCamera.transform.position=follow+offset;
            MainCamera.transform.LookAt(follow+Vector3.forward*4);
        }
    }
}
