using System;
using System.Collections;
using UnityEngine;

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
        public Camera MainCamera;
        public bool Paused;
        public int ViewMode;
        public float RenderingFps;
        public bool AutoStart=true;
        public string SoftwareRevision="Unversioned local demo";
        public event Action ScenarioReset;
        Coroutine startRoutine;
        bool previousQueryTriggers;
        void Start()
        {
            Application.runInBackground=true;
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
            SonarPerception.OwnStateProvider=OwnState;
            previousQueryTriggers=Physics.queriesHitTriggers;
            Physics.queriesHitTriggers=false; // MARUS rays must not intersect the collector trigger.
            Time.fixedDeltaTime=0.02f; QualitySettings.vSyncCount=0; Application.targetFrameRate=90;
            ResetScenario(Config.Scenario,Config.Seed,AutoStart);
        }
        public void ResetScenario(DemoScenario scenario,int seed,bool start=true)
        {
            if(startRoutine!=null)StopCoroutine(startRoutine);
            SetPaused(false); Mission.StopMission();
            Config.CameraDropout=0; Config.SonarDropout=0.02f;
            World.ResetWorld(scenario,seed);
            OwnState.ResetState(seed); CameraPerception.ResetSensor(seed); SonarPerception.ResetSensor(seed);
            Mission.ResetMission(); ScenarioReset?.Invoke();
            if(start)startRoutine=StartCoroutine(StartWhenReady());
        }
        IEnumerator StartWhenReady()
        {
            yield return new WaitForSeconds(1.2f); Mission.StartMission(); startRoutine=null;
        }
        public void StopMission(){if(startRoutine!=null){StopCoroutine(startRoutine);startRoutine=null;}Mission.StopMission();}
        public void StartMission(){if(startRoutine!=null){StopCoroutine(startRoutine);startRoutine=null;}Mission.StartMission();}
        void OnDestroy(){Physics.queriesHitTriggers=previousQueryTriggers;Time.timeScale=1;}
        public void SetPaused(bool pause) { Paused=pause; Time.timeScale=pause?0:1; }
        void LateUpdate()
        {
            RenderingFps=Mathf.Lerp(RenderingFps,1/Mathf.Max(0.001f,Time.unscaledDeltaTime),0.03f);
            if(MainCamera==null || Vessel==null)return;
            Vector3 follow=Vessel.transform.position;
            Vector3 offset=ViewMode%2==0?new Vector3(-8,10,-11):new Vector3(0,25,-3);
            MainCamera.transform.position=follow+offset;
            MainCamera.transform.LookAt(follow+Vector3.forward*4);
        }
    }
}
