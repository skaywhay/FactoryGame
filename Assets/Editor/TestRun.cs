using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class TestRun
{
    const string Folder = "TestArtifacts";

    static TestRunnerApi _api;
    static Report _report;

    [MenuItem("Tools/Run EditMode Tests")]
    public static void RunEditMode()
    {
        Run(TestMode.EditMode, "editmode");
    }

    [MenuItem("Tools/Run PlayMode Tests")]
    public static void RunPlayMode()
    {
        Run(TestMode.PlayMode, "playmode");
    }

    static void Run(TestMode mode, string name)
    {
        if (_api == null) _api = ScriptableObject.CreateInstance<TestRunnerApi>();
        _report = new Report(name);
        _api.RegisterCallbacks(_report);
        _api.Execute(new ExecutionSettings(new Filter { testMode = mode }));
    }

    class Report : ICallbacks
    {
        readonly string _name;
        readonly List<string> _lines = new List<string>();

        public Report(string name)
        {
            _name = name;
        }

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite)
            {
                if (result.FailCount > 0)
                    _lines.Add("SUITE FAIL " + result.Name + " " + result.FailCount + "/" + (result.PassCount + result.FailCount));
                return;
            }

            _lines.Add(result.TestStatus + " " + result.FullName);
            if (result.TestStatus != TestStatus.Passed)
                _lines.Add("    " + (result.Message ?? "").Replace("\n", "\n    "));
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            var text = new StringBuilder();
            text.AppendLine("status " + result.TestStatus);
            text.AppendLine("pass " + result.PassCount + " fail " + result.FailCount + " skip " + result.SkipCount);
            foreach (string line in _lines) text.AppendLine(line);

            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Folder);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, _name + "-results.txt"), text.ToString());
            Debug.Log("[Tests] " + _name + ": pass " + result.PassCount + " fail " + result.FailCount);
        }
    }
}
