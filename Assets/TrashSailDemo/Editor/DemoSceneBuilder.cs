using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace TrashSailDemo.Editor
{
    public static class DemoSceneBuilder
    {
        public const string ScenePath="Assets/TrashSailDemo/Scenes/TrashSailDemo.unity";
        [MenuItem("Trash Sail Demo/Create or Update Demo")]
        public static void CreateOrUpdate()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode before generating assets.");
            // Preserve unsaved user scene before switching. Do not overwrite source scenes.
            var old=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(old.isDirty && !old.path.Equals(ScenePath,StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory("Assets/TrashSailDemo/LocalPreservation");
                EditorSceneManager.SaveScene(old,"Assets/TrashSailDemo/LocalPreservation/"+old.name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity",true);
            }
            Directory.CreateDirectory("Assets/TrashSailDemo/Scenes");Directory.CreateDirectory("Assets/TrashSailDemo/Prefabs");Directory.CreateDirectory("Assets/TrashSailDemo/Generated");
            var config=AssetDatabase.LoadAssetAtPath<DemoConfig>("Assets/TrashSailDemo/DemoConfiguration.asset");
            if(config==null){config=ScriptableObject.CreateInstance<DemoConfig>();AssetDatabase.CreateAsset(config,"Assets/TrashSailDemo/DemoConfiguration.asset");}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var runtime=new GameObject("Trash Sail Demo — integrated system").AddComponent<DemoRuntime>();runtime.Config=config;runtime.SoftwareRevision=SourceRevision();
            var world=new GameObject("Scenario generator (truth boundary)").AddComponent<DemoWorld>();world.Config=config;
            var vessel=DemoWorld.BuildVessel(config);world.Vessel=vessel;
            var waterSampler=new GameObject("MARUS calm-water height adapter").AddComponent<Marus.Ocean.WaterHeightSampler>();vessel.WaterSampler=waterSampler;
            new GameObject("MARUS Sensor Sampler").AddComponent<Marus.Core.SensorSampler>();
            vessel.CameraMount.localRotation=Quaternion.Euler(38,0,0);
            var onboard=vessel.CameraMount.gameObject.AddComponent<Camera>();onboard.fieldOfView=68;onboard.enabled=false;
            var vision=vessel.CameraMount.gameObject.AddComponent<DemoCameraPerception>();vision.Config=config;vision.SensorCamera=onboard;
            var sonar=vessel.SonarMount.gameObject.AddComponent<DemoSonarPerception>();sonar.Config=config;sonar.ConfigureSource();
            PersistVesselAssets(vessel);
            PrefabUtility.SaveAsPrefabAssetAndConnect(vessel.gameObject,"Assets/TrashSailDemo/Prefabs/TrashSailDrone.prefab",InteractionMode.AutomatedAction);
            runtime.Vessel=vessel;runtime.OwnState=vessel.GetComponent<DemoOwnState>();runtime.CameraPerception=vision;runtime.SonarPerception=sonar;runtime.World=world;
            vision.OwnStateProvider=runtime.OwnState;
            var mission=runtime.gameObject.AddComponent<DemoMission>();mission.Config=config;mission.Vessel=vessel;mission.OwnState=runtime.OwnState;mission.CameraPerception=vision;mission.SonarPerception=sonar;runtime.Mission=mission;
            var main=new GameObject("Main Chase Camera").AddComponent<Camera>();main.tag="MainCamera";main.fieldOfView=52;main.nearClipPlane=0.1f;main.farClipPlane=120;main.rect=new Rect(0,0,1,1);
            var hd=main.gameObject.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=new Color(0.26f,0.46f,0.61f);hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.None;
            main.transform.position=new Vector3(-8,10,-11);main.transform.LookAt(new Vector3(0,0,4));runtime.MainCamera=main;
            main.gameObject.AddComponent<AudioListener>();
            Surface("Calm water — visual only",new Vector3(0,-0.035f,17),new Vector3(70,0.04f,80),new Color(0.035f,0.22f,0.31f));
            float w=config.BoundaryHalfWidth,l=config.BoundaryLength;
            Surface("Boundary port",new Vector3(-w,0.015f,l*.5f-3),new Vector3(.08f,.03f,l+6),Color.cyan);
            Surface("Boundary starboard",new Vector3(w,0.015f,l*.5f-3),new Vector3(.08f,.03f,l+6),Color.cyan);
            Surface("Boundary far",new Vector3(0,0.015f,l),new Vector3(w*2,.03f,.08f),Color.cyan);
            Surface("Boundary home",new Vector3(0,0.015f,-6),new Vector3(w*2,.03f,.08f),Color.cyan);
            Surface("Home marker",new Vector3(0,0.025f,-3),new Vector3(2,.03f,.1f),Color.white);
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;sun.transform.rotation=Quaternion.Euler(45,-30,0);
            var evaluation=runtime.gameObject.AddComponent<DemoEvaluation>();evaluation.Runtime=runtime;
            var acceptance=runtime.gameObject.AddComponent<DemoAcceptance>();acceptance.Runtime=runtime;acceptance.Evaluation=evaluation;
            var dashboard=runtime.gameObject.AddComponent<DemoDashboard>();dashboard.Runtime=runtime;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            File.WriteAllText("Evidence/TrashSailDemo/setup-success.txt",DateTime.UtcNow.ToString("O")+" "+ScenePath);
            Selection.activeGameObject=runtime.gameObject;
        }
        static void Surface(string name,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var material=DemoWorld.MakeMaterial(name,color);string path="Assets/TrashSailDemo/Generated/"+name.Replace(' ','_').Replace('—','-')+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing==null){AssetDatabase.CreateAsset(material,path);existing=material;}else UnityEngine.Object.DestroyImmediate(material);
            go.GetComponent<Renderer>().sharedMaterial=existing;
        }
        static string SourceRevision()
        {
            var files=Directory.GetFiles("Assets/TrashSailDemo","*.cs",SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/TrashSailDemo","*.asmdef",SearchOption.AllDirectories))
                .Concat(new[]{"Assets/TrashSailDemo/DemoConfiguration.asset","Assets/marus2-modules/marus2-sensors/Runtime/Sonar/Sonar3D.cs"}).OrderBy(x=>x,StringComparer.Ordinal);
            var manifest=new StringBuilder();using(var sha=SHA256.Create())
            {
                foreach(string file in files)manifest.Append(file.Replace('\\','/')).Append(' ').Append(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant()).Append('\n');
                string hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(manifest.ToString()))).Replace("-","").ToLowerInvariant();
                Directory.CreateDirectory("Evidence/TrashSailDemo");File.WriteAllText("Evidence/TrashSailDemo/source-manifest.sha256",manifest.ToString());
                return "MARUS2 0f9ec3f8; TrashSail source sha256="+hash;
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
            foreach(var filter in vessel.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;if(mesh==null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))continue;
                string path="Assets/TrashSailDemo/Generated/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);old=mesh;}filter.sharedMesh=old;
            }
        }
        [MenuItem("Trash Sail Demo/Run Acceptance Suite")]
        public static void RunSuite(){PlayerPrefs.SetInt("TrashSailDemo.RunSuite",1);EditorApplication.isPlaying=true;}
        [MenuItem("Trash Sail Demo/Build Windows Player")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/TrashSailDemo");
            var result=BuildPipeline.BuildPlayer(new[]{ScenePath},"Builds/TrashSailDemo/TrashSailDemo.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed: "+result.summary.result);
        }
    }
}
