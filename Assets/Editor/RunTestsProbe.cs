using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Прогон NUnit-тестов прямо из открытого редактора, чтобы не закрывать его
// ради -runTests. Мост зовёт Start() (старт прогона), затем Poll(), пока
// результат не появится в TestArtifacts/test-results-editmode.json.
// EditMode-тесты гоняем только вне Play Mode: в Play Mode API их не запускает.
public static class RunTestsProbe
{
    const string ResultPath = "TestArtifacts/test-results-editmode.json";

    [Serializable]
    class Result
    {
        public string state;
        public int passed;
        public int failed;
        public int skipped;
        public string[] failedTests;
    }

    sealed class Collector : ICallbacks
    {
        public bool Finished;
        public int Passed;
        public int Failed;
        public int Skipped;
        public readonly List<string> FailedTests = new List<string>();

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.TestStatus == TestStatus.Failed)
            {
                Failed++;
                FailedTests.Add(result.FullName + ": " + result.Message);
            }
            else if (result.TestStatus == TestStatus.Passed) Passed++;
            else Skipped++;
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            Finished = true;
            Write(result.PassCount > 0 || result.FailCount > 0
                ? new Result { state = "done", passed = result.PassCount, failed = result.FailCount, skipped = result.SkipCount, failedTests = FailedTests.ToArray() }
                : new Result { state = "done", passed = Passed, failed = Failed, skipped = Skipped, failedTests = FailedTests.ToArray() });
        }
    }

    static TestRunnerApi _api;
    static Collector _active;

    static void Write(Result result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
        File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
    }

    // filter — необязательная группа/имя (groupNames), например "^Game\\.Tests\\.".
    public static string Start(string filter = "")
    {
        _active = new Collector();
        if (_api == null) _api = ScriptableObject.CreateInstance<TestRunnerApi>();
        _api.RegisterCallbacks(_active, 1);

        var f = new Filter { testMode = TestMode.EditMode };
        if (!string.IsNullOrEmpty(filter)) f.groupNames = new[] { filter };

        _api.Execute(new ExecutionSettings(f));

        Write(new Result { state = "running", failedTests = new string[0] });
        return "started";
    }

    public static string Poll()
    {
        if (_active == null) return "not-started";
        return _active.Finished
            ? string.Format("done passed={0} failed={1} skipped={2}", _active.Passed, _active.Failed, _active.Skipped)
            : "running";
    }
}