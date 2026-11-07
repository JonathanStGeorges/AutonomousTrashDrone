using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
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
                else if (request.command == "preserve") Preserve();
                else if (request.command == "setup" || request.command == "build")
                {
                    if (EditorApplication.isPlaying) { Write(request.command, "BLOCKED", "Stop Play Mode before asset operations"); return; }
                    var builder = Type.GetType("TrashSailDemo.Editor.DemoSceneBuilder, TrashSailDemo.Editor");
                    if (builder == null) throw new InvalidOperationException("Scene builder not compiled yet");
                    builder.GetMethod(request.command == "setup" ? "CreateOrUpdate" : "Build", BindingFlags.Public|BindingFlags.Static).Invoke(null, null);
                    Write(request.command, "PASS", "Completed");
                }
                else if (request.command == "play") { EditorApplication.isPlaying = true; Write(request.command,"PASS","Entering Play Mode"); }
                else if (request.command == "suite") { PlayerPrefs.SetInt("TrashSailDemo.RunSuite",1); PlayerPrefs.SetString("TrashSailDemo.Scenarios",request.argument ?? ""); var game=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")); game.maximized=true; EditorApplication.isPlaying = true; Write(request.command,"PASS","Entering rendered acceptance suite"); }
                else if (request.command == "stop") { EditorApplication.isPlaying = false; Write(request.command,"PASS","Leaving Play Mode"); }
                else if (request.command == "tests") RunTests();
                else Write(request.command,"FAIL","Unknown command");
            }
            catch(Exception ex) { Debug.LogException(ex); Write(request.command,"FAIL",ex.ToString()); busy=false; }
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
