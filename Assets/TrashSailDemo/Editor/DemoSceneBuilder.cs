using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace TrashSailDemo.Editor
{
    public static class DemoSceneBuilder
    {
        public const string ScenePath="Assets/TrashSailDemo/Scenes/TrashSailDemo.unity";
        public const string OceanScenePath="Assets/TrashSailDemo/Scenes/TrashOceanDemo.unity";
        [MenuItem("Trash Sail Demo/Create or Update Compact Demo")]
        public static void CreateOrUpdate()=>Create(false);
        [MenuItem("Trash Sail Demo/Create or Update Production Ocean")]
        public static void CreateOcean()=>Create(true);
        static void Create(bool large)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode before generating assets.");
            var old=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(old.isDirty && old.path!=ScenePath && old.path!=OceanScenePath)
            {
                Directory.CreateDirectory("Assets/TrashSailDemo/LocalPreservation");
                EditorSceneManager.SaveScene(old,"Assets/TrashSailDemo/LocalPreservation/"+old.name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity",true);
            }
            Directory.CreateDirectory("Assets/TrashSailDemo/Scenes");Directory.CreateDirectory("Assets/TrashSailDemo/Prefabs");Directory.CreateDirectory("Assets/TrashSailDemo/Generated");
            DemoAssetSetup.CreateOrUpdateAssets();
            string configPath=large?"Assets/TrashSailDemo/OceanConfiguration.asset":"Assets/TrashSailDemo/DemoConfiguration.asset";
            var config=AssetDatabase.LoadAssetAtPath<DemoConfig>(configPath);
            if(config==null){config=ScriptableObject.CreateInstance<DemoConfig>();AssetDatabase.CreateAsset(config,configPath);}
            config.LargeOcean=large;config.CameraWidth=640;config.CameraHeight=384;config.CameraHz=3;config.InferenceRequestHz=3;
            config.MotorAssistance=false;config.StopAfterCaptures=0;config.MaxSearchSpeed=.55f;config.ApproachSpeed=.25f;
            config.MaxCollectionSpeed=.65f;config.TimeBudget=large?3600:300;config.Capacity=large?30:8;
            EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var runtime=new GameObject("Trash Ocean — integrated sensor-to-thruster system").AddComponent<DemoRuntime>();runtime.Config=config;
            var world=new GameObject("Compact scenario generator — evaluator truth").AddComponent<DemoWorld>();world.Config=config;
            var vessel=DemoWorld.BuildVessel(config);world.Vessel=vessel;vessel.ConfigureThrusters();
            var waterSampler=new GameObject("MARUS mean calm-water height adapter").AddComponent<Marus.Ocean.WaterHeightSampler>();vessel.WaterSampler=waterSampler;
            new GameObject("MARUS Sensor Sampler").AddComponent<Marus.Core.SensorSampler>();
            vessel.CameraMount.localRotation=Quaternion.Euler(38,0,0);
            var onboard=vessel.CameraMount.gameObject.AddComponent<Camera>();onboard.fieldOfView=68;onboard.enabled=false;
            var vision=vessel.CameraMount.gameObject.AddComponent<DemoCameraPerception>();vision.Config=config;vision.SensorCamera=onboard;
            var sonar=vessel.SonarMount.gameObject.AddComponent<DemoSonarPerception>();sonar.Config=config;sonar.ConfigureSource();
            PersistVesselAssets(vessel);
            PrefabUtility.SaveAsPrefabAssetAndConnect(vessel.gameObject,"Assets/TrashSailDemo/Prefabs/TrashSailDrone.prefab",InteractionMode.AutomatedAction);
            runtime.Vessel=vessel;runtime.OwnState=vessel.GetComponent<DemoOwnState>();runtime.CameraPerception=vision;runtime.SonarPerception=sonar;runtime.World=world;
            vision.OwnStateProvider=runtime.OwnState;
            var coverage=runtime.gameObject.AddComponent<CoveragePlanner>();coverage.Config=config;runtime.Coverage=coverage;
            var population=runtime.gameObject.AddComponent<OceanPopulation>();population.Config=config;population.Vessel=vessel;runtime.Population=population;
            var mission=runtime.gameObject.AddComponent<DemoMission>();mission.Config=config;mission.Vessel=vessel;mission.OwnState=runtime.OwnState;
            mission.CameraPerception=vision;mission.SonarPerception=sonar;mission.Coverage=coverage;runtime.Mission=mission;
            var main=new GameObject("Display background camera").AddComponent<Camera>();main.tag="MainCamera";main.fieldOfView=55;main.nearClipPlane=.1f;main.farClipPlane=3000;main.rect=new Rect(0,0,1,1);
            var hd=main.gameObject.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=Color.black;
            hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.None;runtime.MainCamera=main;main.gameObject.AddComponent<AudioListener>();
            var presentation=runtime.gameObject.AddComponent<DemoPresentation>();presentation.Runtime=runtime;runtime.Presentation=presentation;
            runtime.ScenePipeline=CreateEnvironment();
            var evaluation=runtime.gameObject.AddComponent<DemoEvaluation>();evaluation.Runtime=runtime;
            var acceptance=runtime.gameObject.AddComponent<DemoAcceptance>();acceptance.Runtime=runtime;acceptance.Evaluation=evaluation;
            var dashboard=runtime.gameObject.AddComponent<DemoDashboard>();dashboard.Runtime=runtime;
            runtime.SoftwareRevision=SourceRevision();
            string path=large?OceanScenePath:ScenePath;
            EditorSceneManager.SaveScene(scene,path);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Evidence/TrashSailDemo");
            File.WriteAllText("Evidence/TrashSailDemo/setup-success.txt",DateTime.UtcNow.ToString("O")+" "+path);
            Selection.activeGameObject=runtime.gameObject;
        }
        static HDRenderPipelineAsset CreateEnvironment()
        {
            const string pipelinePath="Assets/TrashSailDemo/Generated/TrashOceanHDRP.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(pipelinePath);
            if(pipeline==null)
            {
                var source=GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
                if(source==null)throw new InvalidOperationException("Installed HDRP pipeline unavailable.");
                pipeline=UnityEngine.Object.Instantiate(source);pipeline.name="Trash Ocean HDRP (isolated clone)";AssetDatabase.CreateAsset(pipeline,pipelinePath);
            }
            var settings=pipeline.currentPlatformRenderPipelineSettings;settings.supportWater=true;settings.waterSimulationResolution=WaterSimulationResolution.Medium128;pipeline.currentPlatformRenderPipelineSettings=settings;EditorUtility.SetDirty(pipeline);
            const string volumePath="Assets/TrashSailDemo/Generated/TrashOceanVolume.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
            if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,volumePath);}
            if(!profile.TryGet<WaterRendering>(out var rendering))rendering=profile.Add<WaterRendering>();rendering.enable.Override(true);
            if(!profile.TryGet<VisualEnvironment>(out var environment))environment=profile.Add<VisualEnvironment>();
            environment.skyType.Override((int)SkyType.Gradient);environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            if(!profile.TryGet<GradientSky>(out var sky))sky=profile.Add<GradientSky>();
            sky.top.Override(new Color(.12f,.32f,.58f));sky.middle.Override(new Color(.65f,.77f,.85f));sky.bottom.Override(new Color(.25f,.36f,.43f));sky.gradientDiffusion.Override(1);
            // Gradient colours are unit radiance; match the outdoor fixed exposure
            // rather than leaving the ambient sky thousands of times too dark.
            sky.skyIntensityMode.Override(SkyIntensityMode.Multiplier);sky.multiplier.Override(5000);
            if(!profile.TryGet<Exposure>(out var exposure))exposure=profile.Add<Exposure>();
            exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(12);
            if(!profile.TryGet<Tonemapping>(out var tonemapping))tonemapping=profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);
            EditorUtility.SetDirty(profile);
            foreach(var component in profile.components)if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,profile);
            var volume=new GameObject("Ocean sky and water volume").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;volume.priority=10;
            var ocean=new GameObject("Open ocean — HDRP infinite water, near-mean waves").AddComponent<WaterSurface>();
            ocean.surfaceType=WaterSurfaceType.OceanSeaLake;ocean.geometryType=WaterGeometryType.Infinite;ocean.largeWindSpeed=15;ocean.largeBand0Multiplier=.05f;ocean.largeBand1Multiplier=.05f;
            ocean.ripples=false;ocean.foam=false;ocean.caustics=false;ocean.scriptInteractions=false;ocean.maxTessellationFactor=1;ocean.absorptionDistance=1.5f;
            ocean.scatteringColor=new Color(.02f,.16f,.26f);ocean.refractionColor=new Color(.03f,.3f,.45f);
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.lightUnit=UnityEngine.Rendering.LightUnit.Lux;sun.intensity=50000;sun.transform.rotation=Quaternion.Euler(45,-30,0);
            return pipeline;
        }
        static string SourceRevision()
        {
            var files=Directory.GetFiles("Assets/TrashSailDemo","*.cs",SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/TrashSailDemo","*.asmdef",SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Inference","*.py",SearchOption.AllDirectories))
                .Concat(new[]{"Assets/TrashSailDemo/DemoConfiguration.asset","Assets/marus2-modules/marus2-sensors/Runtime/Sonar/Sonar3D.cs"}).OrderBy(x=>x,StringComparer.Ordinal);
            var manifest=new StringBuilder();using(var sha=SHA256.Create())
            {
                foreach(string file in files)manifest.Append(file.Replace('\\','/')).Append(' ').Append(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant()).Append('\n');
                string hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(manifest.ToString()))).Replace("-","").ToLowerInvariant();
                File.WriteAllText("Evidence/TrashSailDemo/source-manifest.sha256",manifest.ToString());
                return "MARUS2 + TrashOcean v2 source sha256="+hash;
            }
        }
        static void PersistVesselAssets(DemoVessel vessel)
        {
            foreach(var renderer in vessel.GetComponentsInChildren<Renderer>())
            {
                var mat=renderer.sharedMaterial;if(mat==null || AssetDatabase.Contains(mat))continue;
                string path="Assets/TrashSailDemo/Generated/"+mat.name+".mat";
                var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old==null){AssetDatabase.CreateAsset(mat,path);old=mat;}renderer.sharedMaterial=old;
            }
        }
        [MenuItem("Trash Sail Demo/Run Production Acceptance Suite")]
        public static void RunSuite(){PlayerPrefs.SetInt("TrashSailDemo.RunSuite",1);EditorApplication.isPlaying=true;}
        [MenuItem("Trash Sail Demo/Build Windows Player")]
        public static void Build()
        {
            if(!File.Exists(OceanScenePath))throw new FileNotFoundException("Generate production ocean first.");
            Directory.CreateDirectory("Builds/TrashOceanDemo");
            var previous=QualitySettings.renderPipeline;
            try
            {
                QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/TrashSailDemo/Generated/TrashOceanHDRP.asset");
                var result=BuildPipeline.BuildPlayer(new[]{OceanScenePath},"Builds/TrashOceanDemo/TrashOceanDemo.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
                if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed: "+result.summary.result);
            }
            finally{QualitySettings.renderPipeline=previous;}
        }
    }
}
