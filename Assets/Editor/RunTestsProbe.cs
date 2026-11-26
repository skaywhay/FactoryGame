using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    // ------------------------------------------------------ UI: реальные координаты

    // Возвращает десктопную точку внутри объекта: u,v — нормализованные координаты
    // его RectTransform (0,0 — левый низ, 1,1 — правый верх). Нужна, чтобы щёлкнуть
    // настоящей мышью и проверить дохождение клика через рейкаст и EventSystem.
    // Путь: parent, затем дети через '/'. Последний в пути — цель.
    public static string UiScreenPoint(string path, float u, float v)
    {
        string[] parts = path.Split('/');
        Transform t = FindDeep(parts[0]);
        if (t == null) return "not-found:" + parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var child = t.Find(parts[i]);
            if (child == null) return "not-found:" + path;
            t = child;
        }

        var rt = t as RectTransform;
        if (rt == null) return "no-rect:" + path;

        Vector2 local = new Vector2(
            Mathf.Lerp(rt.rect.xMin, rt.rect.xMax, Mathf.Clamp01(u)),
            Mathf.Lerp(rt.rect.yMin, rt.rect.yMax, Mathf.Clamp01(v)));
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(local));

        Rect? gv = GameViewDesktopRect();
        if (gv == null) return "no-gameview";

        // Окно Game View выше самой картинки на полосу тулбара — проверено кликом,
        // поэтому добавляем эту разницу, иначе клик уходит мимо.
        float strip = gv.Value.height - (float)Screen.height;
        float x = gv.Value.x + screen.x;
        float y = gv.Value.y + gv.Value.height + strip - screen.y;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", x, y);
    }

    // Что реально лежит под точкой объекта (нормализованные координаты его RectTransform).
    // Если под точкой не наш объект — значит, клик уходит другому элементу.
    public static string UiHitTest(string name, float u, float v)
    {
        var go = GameObject.Find(name);
        if (go == null) return "not-found:" + name;
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return "no-rect:" + name;

        Vector2 local = new Vector2(
            Mathf.Lerp(rt.rect.xMin, rt.rect.xMax, Mathf.Clamp01(u)),
            Mathf.Lerp(rt.rect.yMin, rt.rect.yMax, Mathf.Clamp01(v)));
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(local));

        var data = new PointerEventData(EventSystem.current) { position = screen };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        if (hits.Count == 0) return "nothing under point";
        var names = hits.Take(4).Select(h => h.gameObject.transform.parent != null && h.gameObject.transform.root != h.gameObject.transform
            ? h.gameObject.transform.parent.name + "/" + h.gameObject.name
            : h.gameObject.name).ToArray();
        return string.Join(" | ", names);
    }

    // Что лежит под десктопной точкой (экранные координаты окна редактора).
    // Так проверяем, куда реально уходит настоящий клик мышью.
    public static string UiHitTestPoint(int desktopX, int desktopY)
    {
        Rect? gv = GameViewDesktopRect();
        if (gv == null) return "no-gameview";
        Vector2 screen = new Vector2(desktopX - gv.Value.x, gv.Value.y + gv.Value.height - desktopY);

        var data = new PointerEventData(EventSystem.current) { position = screen };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        if (hits.Count == 0) return "nothing under point";
        var names = hits.Take(4).Select(h =>
            h.gameObject.transform.parent != null && h.gameObject.transform.root != h.gameObject.transform
                ? h.gameObject.transform.parent.name + "/" + h.gameObject.name
                : h.gameObject.name).ToArray();
        return string.Join(" | ", names);
    }

    // Нажатие и перетаскивание через EventSystem в координатах канваса — так же,
    // как это делает реальная мышь (raycast -> pointerDown -> drag -> pointerUp).
    public static string UiSimClick(string path)
    {
        Transform t = FindDeep(path.Split('/')[0]);
        if (t == null) return "not-found:" + path;
        var go = path.Contains("/") ? FindPath(t, path.Substring(path.IndexOf('/') + 1)) : t.gameObject;
        if (go == null) return "not-found:" + path;
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        data.position = RenderPoint(go.GetComponent<RectTransform>(), 0.5f, 0.5f);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
        return go.name + " active=" + go.activeInHierarchy;
    }

    // Перетаскивание слайдера: от fromU до toU по ширине объекта.
    public static string UiSimDrag(string path, float fromU, float toU)
    {
        var go = FindPath(FindDeep(path.Split('/')[0]), path.Substring(path.IndexOf('/') + 1));
        if (go == null) return "not-found:" + path;
        var rt = go.GetComponent<RectTransform>();
        var slider = go.GetComponent<Slider>();
        if (rt == null || slider == null) return "no-slider:" + path;

        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        data.position = RenderPoint(rt, fromU, 0.5f);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
        for (int i = 1; i <= 5; i++)
        {
            data.position = RenderPoint(rt, fromU + (toU - fromU) * i / 5f, 0.5f);
            ExecuteEvents.Execute(go, data, ExecuteEvents.dragHandler);
        }
        data.position = RenderPoint(rt, toU, 0.5f);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerUpHandler);
        return slider.value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    static GameObject FindPath(Transform root, string rest)
    {
        if (root == null) return null;
        var t = root;
        foreach (var part in rest.Split('/'))
        {
            if (t == null) return null;
            t = t.Find(part);
        }
        return t == null ? null : t.gameObject;
    }

    static Vector2 RenderPoint(RectTransform rt, float u, float v)
    {
        Vector2 local = new Vector2(
            Mathf.Lerp(rt.rect.xMin, rt.rect.xMax, Mathf.Clamp01(u)),
            Mathf.Lerp(rt.rect.yMin, rt.rect.yMax, Mathf.Clamp01(v)));
        return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(local));
    }

    // Активность объекта и уровень громкости музыки — для проверки после клика.
    public static string UiState(string name)
    {
        var go = GameObject.Find(name);
        string modal = go == null ? "gone" : go.activeInHierarchy.ToString();

        var game = GameObject.Find("FactoryGame");
        var factory = game != null ? game.GetComponent<Game.Factory.FactoryGame>() : null;
        string volume = factory == null || factory.MusicSource == null
            ? "no-source"
            : factory.MusicSource.volume.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var slider = go != null ? go.GetComponentInChildren<Slider>(true) : null;
        string sliderValue = slider == null ? "-" : slider.value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        return string.Format("{0}: active={1} slider={2} musicVolume={3}", name, modal, sliderValue, volume);
    }

    // Клик по UI напрямую через EventSystem (без координат): проверяет проводку кнопки.
    public static string UiClick(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) return "not-found:" + name;
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
        return go.name + " active=" + go.activeInHierarchy;
    }

    // Рекурсивный поиск по имени, включая неактивные объекты и под-канвасы HUD.
    static Transform FindDeep(string name)
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root.name == name) return root.transform;
            foreach (Transform c in root.transform)
            {
                var found = FindDeepIn(c, name);
                if (found != null) return found;
            }
        }
        return null;
    }

    static Transform FindDeepIn(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform c in parent)
        {
            var found = FindDeepIn(c, name);
            if (found != null) return found;
        }
        return null;
    }

    static Rect? GameViewDesktopRect()
    {
        var asm = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "UnityEditor");
        if (asm == null) return null;
        var type = asm.GetType("UnityEditor.GameView");
        if (type == null) return null;
        foreach (var w in Resources.FindObjectsOfTypeAll(type))
        {
            var win = w as EditorWindow;
            if (win == null) continue;
            Rect p = win.position;
            if (p.width > 0f && p.height > 0f) return p;
        }
        return null;
    }

    // Размер Game View на экране — чтобы понять, куда реально уходят клики.
    public static string UiGameView()
    {
        Rect? gv = GameViewDesktopRect();
        if (gv == null) return "no-gameview";
        var c = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "gv={0:0.##},{1:0.##},{2:0.##},{3:0.##} screen={4}x{5}",
            gv.Value.x, gv.Value.y, gv.Value.width, gv.Value.height, Screen.width, Screen.height);
    }
}