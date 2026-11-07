using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashSailDemo.Editor
{
    // Local, project-scoped automation; serializes setup/tests/build in the already-open Editor.
    [InitializeOnLoad] public static class DemoEditorBridge
    {
        [Serializable] sealed class Request { public string command; public string argument,id; }
        [Serializable] sealed class Response { public string command, status, detail,id; public string unity; }
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Evidence => Path.Combine(Root, "Evidence/TrashSailDemo");
        static string RequestPath => Path.Combine(Root, "Temp/TrashSailDemo.command.json");
        static bool busy;
        static TestRunnerApi api;
        static string requestId;
        static readonly DateTime DomainLoadedAt=DateTime.UtcNow;
        static DemoEditorBridge() { EditorApplication.update += Tick; EditorApplication.delayCall += Ready; }
        static void Ready() { Directory.CreateDirectory(Evidence); Write("ready", "PASS", "Editor domain compiled; bridge available"); }
        static void Write(string command, string status, string detail)
        {
            Directory.CreateDirectory(Evidence);
            string json=JsonUtility.ToJson(new Response { command=command,status=status,detail=detail,unity=Application.unityVersion,id=requestId }, true);
            File.WriteAllText(Path.Combine(Evidence, "editor-response.json"),json);
            File.WriteAllText(Path.Combine(Evidence,"editor-response-"+command+".json"),json);
        }
        static void Tick()
        {
            if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
            // File requests can arrive before Unity's next focus/asset refresh. Never
            // run a setup/test against an older loaded assembly after source edits.
            foreach(string script in Directory.GetFiles(Path.Combine(Application.dataPath,"TrashSailDemo"),"*.cs",SearchOption.AllDirectories))
                if(File.GetLastWriteTimeUtc(script)>DomainLoadedAt){AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);return;}
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath)); File.Delete(RequestPath);
            requestId=request.id;
            try
            {
                if (request.command == "baseline") Write(request.command, "PASS", "Custom assembly loaded; no compilation failure prevented domain load");
                else if(request.command=="status")Write(request.command,"PASS","isPlaying="+EditorApplication.isPlaying+"; scene="+SceneManager.GetActiveScene().path);
                else if(request.command=="capture")
                {var evaluation=UnityEngine.Object.FindFirstObjectByType<DemoEvaluation>();if(evaluation==null)throw new InvalidOperationException("Run the demo first");evaluation.Capture(string.IsNullOrEmpty(request.argument)?"inspection":request.argument);Write(request.command,"PASS",evaluation.OutputDirectory);}
                else if(request.command=="performance" || request.command=="worldreset")
                {
                    var runtime=UnityEngine.Object.FindFirstObjectByType<DemoRuntime>();
                    if(!EditorApplication.isPlaying || runtime==null)throw new InvalidOperationException("Run the ocean scene first");
                    runtime.Config.LargeOcean=true;runtime.ResetScenario(DemoScenario.ClearCollection,17,true);
                    if(request.command=="performance")
                    {
                        // Evaluator-only initial conditions, never targets supplied to autonomy.
                        // The other 369 world records retain their seeded distribution.
                        PlacePerformanceActor(runtime,"fish",new Vector3(6,-.7f,12),new Vector3(-.12f,0,0));
                        PlacePerformanceActor(runtime,"boat",new Vector3(20,0,40),Vector3.zero);
                        runtime.GetComponent<DemoAcceptance>().SuiteStatus="Large performance fixture: seed17 + configured near-field fish/boat";
                        runtime.GetComponent<DemoEvaluation>().RecordEvent("performance_fixture","Observer/evaluator setup: first fish=(6,-.7,12), velocity=(-.12,0,0); first boat=(20,0,40); other seeded records unchanged; no truth to planner");
                    }
                    else runtime.GetComponent<DemoAcceptance>().SuiteStatus="Interactive production demonstration";
                    Write(request.command,"PASS","Large ocean reset; "+request.command+"; sensors reset and mission starts only after fresh observations");
                }
                else if(request.command=="startmission" || request.command=="stopmission" || request.command=="view")
                {
                    var runtime=UnityEngine.Object.FindFirstObjectByType<DemoRuntime>();if(runtime==null)throw new InvalidOperationException("Run the demo first");
                    if(request.command=="startmission")runtime.StartMission();
                    else if(request.command=="stopmission")runtime.StopMission();
                    else runtime.Presentation.Maximize(int.Parse(request.argument));
                    Write(request.command,"PASS","mode="+runtime.Presentation.ViewMode+"; onboard="+runtime.CameraPerception.SensorCamera.GetEntityId()+"; FOV="+runtime.CameraPerception.SensorCamera.fieldOfView+"; mount="+runtime.CameraPerception.SensorCamera.transform.localPosition+"; profile="+runtime.CameraPerception.ActiveProfile+"; dimensions="+runtime.Config.CameraWidth+"x"+runtime.Config.CameraHeight);
                }
                else if(request.command=="probe")
                {
                    var runtime=UnityEngine.Object.FindFirstObjectByType<DemoRuntime>();if(runtime==null)throw new InvalidOperationException("Demo runtime unavailable");
                    var acceptance=runtime.GetComponent<DemoAcceptance>();
                    Write(request.command,"PASS","phase="+runtime.Mission.Status.Phase+"; reason="+runtime.Mission.Status.Reason+"; model="+runtime.CameraPerception.ServiceStatus+"; camera="+runtime.CameraPerception.IsHealthy+"; sonar="+runtime.SonarPerception.IsHealthy+"; tracks="+runtime.CameraPerception.Tracks.Count+"; pose="+runtime.Vessel.transform.position+"; captures="+runtime.Vessel.Collector.CaptureCount+"; fps="+runtime.RenderingFps+"; activeFish="+runtime.Population.ActiveActors.Count(a=>a.Kind=="fish")+"; activeBoats="+runtime.Population.ActiveActors.Count(a=>a.Kind=="boat")+"; suite="+(acceptance!=null?acceptance.SuiteStatus:"none"));
                }
                else if (request.command == "preserve") Preserve();
                else if (request.command == "setup" || request.command == "ocean" || request.command == "build")
                {
                    if (EditorApplication.isPlaying) { Write(request.command, "BLOCKED", "Stop Play Mode before asset operations"); return; }
                    var builder = Type.GetType("TrashSailDemo.Editor.DemoSceneBuilder, TrashSailDemo.Editor");
                    if (builder == null) throw new InvalidOperationException("Scene builder not compiled yet");
                    builder.GetMethod(request.command=="ocean"?"CreateOcean":request.command == "setup" ? "CreateOrUpdate" : "Build", BindingFlags.Public|BindingFlags.Static).Invoke(null, null);
                    Write(request.command, "PASS", "Completed");
                }
                else if (request.command == "play") {var game=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));game.maximized=true; EditorApplication.isPlaying = true; Write(request.command,"PASS","Entering Play Mode"); }
                else if (request.command == "suite") { PlayerPrefs.SetInt("TrashSailDemo.RunSuite",1); PlayerPrefs.SetString("TrashSailDemo.Scenarios",request.argument ?? ""); var game=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")); game.maximized=true; EditorApplication.isPlaying = true; Write(request.command,"PASS","Entering rendered acceptance suite"); }
                else if (request.command == "stop") { EditorApplication.isPlaying = false; Write(request.command,"PASS","Leaving Play Mode"); }
                else if (request.command == "tests") RunTests();
                else Write(request.command,"FAIL","Unknown command");
            }
            catch(Exception ex) { Debug.LogException(ex); Write(request.command,"FAIL",ex.ToString()); busy=false; }
        }
        static void PlacePerformanceActor(DemoRuntime runtime,string kind,Vector3 position,Vector3 velocity)
        {
            var record=runtime.Population.Records.First(r=>r.Kind==kind);
            record.Position=record.InitialPosition=position;record.Velocity=velocity;record.Rotation=Quaternion.identity;
            var actor=runtime.Population.ActiveActors.FirstOrDefault(a=>a.WorldRecordId==record.Id);
            if(actor==null)return; // normal streaming activates the new initial record before sensor range
            actor.transform.SetPositionAndRotation(position,Quaternion.identity);actor.Body.position=position;actor.Body.rotation=Quaternion.identity;
            actor.InitialPosition=position;actor.DriftVelocity=velocity;actor.SpawnTime=Time.timeAsDouble;
            Physics.SyncTransforms();
        }
        static void Preserve()
        {
            var active = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying) { Write("preserve","BLOCKED","Play Mode is active; stop before preserving edit-time scene"); return; }
            if (!active.isDirty) { Write("preserve","PASS","Original scene has no unsaved changes"); return; }
            Directory.CreateDirectory(Path.Combine(Application.dataPath,"TrashSailDemo/LocalPreservation"));
            string path="Assets/TrashSailDemo/LocalPreservation/"+active.name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity";
            if (!EditorSceneManager.SaveScene(active,path,true)) throw new IOException("Could not preserve unsaved scene");
            File.WriteAllText(Path.Combine(Evidence,"preserved-scene.txt"),path);
            Write("preserve","PASS",path+" (copy; original scene untouched)");
        }
        static void RunTests()
        {
            if (EditorApplication.isPlaying) { Write("tests","BLOCKED","EditMode tests require leaving Play Mode"); return; }
            busy=true; api=ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results());
            api.Execute(new ExecutionSettings(new Filter { testMode=TestMode.EditMode, assemblyNames=new[]{"TrashSailDemo.Tests"} }));
        }
        [Serializable] sealed class TestSummary { public int passed,failed,skipped; public List<string> failures=new List<string>(); }
        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { Write("tests","RUNNING","Unity EditMode tests"); }
            public void TestStarted(ITestAdaptor test) {}
            public void TestFinished(ITestResultAdaptor result) {}
            public void RunFinished(ITestResultAdaptor result)
            {
                var summary=new TestSummary { passed=result.PassCount,failed=result.FailCount,skipped=result.SkipCount };
                AddFailures(result,summary);
                File.WriteAllText(Path.Combine(Evidence,"editmode-results.json"),JsonUtility.ToJson(summary,true));
                TestRunnerApi.SaveResultToFile(result, Path.Combine(Evidence,"editmode-results.xml"));
                Write("tests",result.FailCount==0 && result.PassCount>0 ? "PASS":"FAIL",JsonUtility.ToJson(summary)); busy=false;
            }
            static void AddFailures(ITestResultAdaptor result,TestSummary summary)
            { if(result.TestStatus==TestStatus.Failed) summary.failures.Add(result.FullName+": "+result.Message); foreach(var child in result.Children) AddFailures(child,summary); }
        }
    }
}

