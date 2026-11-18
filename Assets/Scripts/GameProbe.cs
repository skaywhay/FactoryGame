// Служебный «пробник» для автотестов: запускается с ключом -probe,
// делает скриншоты, собирает ошибки и метрики, пишет report.json.
// В обычной игре не активен, на производительность не влияет.
//
// Пример запуска:  Factory Prototype.exe -probe -probe-seconds 15 -probe-out TestArtifacts/probe
// Из игрового кода: GameProbe.Metric("money", Sim.Money); GameProbe.Check("жив", true);

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game
{
    public class GameProbe : MonoBehaviour
    {
        public static GameProbe Instance { get; private set; }

        static readonly Dictionary<string, double> MetricsMap = new Dictionary<string, double>();
        static readonly List<string> Checks = new List<string>();
        static readonly List<string> Failures = new List<string>();
        static readonly List<string> Errors = new List<string>();
        static readonly List<string> Screenshots = new List<string>();
        static bool _reportWritten;
        static string _reportPath;

        float _seconds = 10f;
        float _interval = 2f;
        int _width;
        int _height;
        bool _noQuit;
        bool _overlay = true;
        int _frames;
        float _elapsed;
        float _minFps = float.MaxValue;
        int _screenshotIndex;
        string _outDir;
        bool _running;

        public static bool Active
        {
            get { return Instance != null || HasArg("-probe"); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (Instance != null || !HasArg("-probe")) return;
            var go = new GameObject("GameProbe");
            go.AddComponent<GameProbe>();
        }

        // -probe-seconds — сколько всего крутиться, -probe-interval — как часто снимать кадр.
        // -probe-width/-probe-height задают окно (только для билда), -probe-noquit оставляет игру открытой.
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _seconds = ArgFloat("-probe-seconds", 10f);
            _interval = Mathf.Max(0.25f, ArgFloat("-probe-interval", 2f));
            _width = (int)ArgFloat("-probe-width", 0f);
            _height = (int)ArgFloat("-probe-height", 0f);
            _noQuit = HasArg("-probe-noquit");
            _overlay = !HasArg("-probe-nooverlay");

            string custom = Arg("-probe-out", null);
            if (!string.IsNullOrEmpty(custom))
            {
                _outDir = Path.IsPathRooted(custom) ? custom : Path.Combine(ProjectRoot(), custom);
            }
            else
            {
                _outDir = Path.Combine(ProjectRoot(), "TestArtifacts", "probe-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            }
            try { Directory.CreateDirectory(_outDir); } catch { }
            _reportPath = Path.Combine(_outDir, "report.json");

            if (_width > 0 && _height > 0 && !Application.isEditor)
                Screen.SetResolution(_width, _height, FullScreenMode.Windowed);

            Application.logMessageReceived += OnLog;
            Application.quitting += WriteReport;
            _running = true;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            Application.quitting -= WriteReport;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_running) return;
            _frames++;
            _elapsed += Time.unscaledDeltaTime;
            float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
            if (_elapsed > 0.5f && fps < _minFps) _minFps = fps;
        }

        // Основной цикл: стартовый кадр, потом кадры раз в interval, в конце отчёт.
        // Ждём реальное время, чтобы работало и на ускоренном Time.timeScale.
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(0.75f);
            yield return Capture("start");

            float next = Time.realtimeSinceStartup + _interval;
            while (Time.realtimeSinceStartup < _seconds)
            {
                if (Time.realtimeSinceStartup >= next)
                {
                    next += _interval;
                    yield return Capture("t" + Mathf.RoundToInt(Time.realtimeSinceStartup));
                }
                yield return null;
            }

            yield return Capture("end");
            _running = false;
            WriteReport();

            if (!_noQuit)
            {
                yield return new WaitForSecondsRealtime(0.25f);
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit(Failures.Count == 0 ? 0 : 1);
#endif
            }
        }

        // Скриншот пишется асинхронно, поэтому ждём появления файла,
        // а не просто делаем yield return null. На медленном диске бывает долго.
        IEnumerator Capture(string label)
        {
            string file = Path.Combine(_outDir, string.Format("shot-{0:00}-{1}.png", _screenshotIndex++, label));
            try { if (File.Exists(file)) File.Delete(file); } catch { }
            try { ScreenCapture.CaptureScreenshot(file); } catch (Exception e) { Errors.Add("Screenshot failed: " + e.Message); }
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!File.Exists(file) && Time.realtimeSinceStartup < deadline) yield return null;
            if (File.Exists(file) && new FileInfo(file).Length > 0) Screenshots.Add(file);
            else Errors.Add("Screenshot was not written: " + file);
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string entry = type + ": " + message + (string.IsNullOrEmpty(stack) ? "" : "\n" + stack);
            if (Errors.Count < 100) Errors.Add(entry);
        }

        // ---------------------------------------------------------- public API

        public static void Metric(string name, double value)
        {
            MetricsMap[name] = value;
        }

        public static void Check(string name, bool condition, string detail = null)
        {
            string line = (condition ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? "" : " - " + detail);
            Checks.Add(line);
            if (!condition) Failures.Add(line);
        }

        public static void Fail(string name, string detail = null)
        {
            Check(name, false, detail);
        }

        public static string ReportPath { get { return _reportPath; } }

        // -------------------------------------------------------------- report

        void WriteReport()
        {
            if (_reportWritten || string.IsNullOrEmpty(_outDir)) return;
            _reportWritten = true;
            try
            {
                Directory.CreateDirectory(_outDir);
                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"ok\": ").Append(Failures.Count == 0 && Errors.Count == 0 ? "true" : "false").Append(",\n");
                sb.Append("  \"unity\": \"").Append(Esc(Application.unityVersion)).Append("\",\n");
                sb.Append("  \"product\": \"").Append(Esc(Application.productName)).Append("\",\n");
                sb.Append("  \"scene\": \"").Append(Esc(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)).Append("\",\n");
                sb.Append("  \"durationSeconds\": ").Append(Num(_elapsed)).Append(",\n");
                sb.Append("  \"frames\": ").Append(_frames).Append(",\n");
                sb.Append("  \"averageFps\": ").Append(Num(_elapsed > 0f ? _frames / _elapsed : 0f)).Append(",\n");
                sb.Append("  \"minFps\": ").Append(Num(_minFps == float.MaxValue ? 0f : _minFps)).Append(",\n");
                sb.Append("  \"screen\": { \"width\": ").Append(Screen.width).Append(", \"height\": ").Append(Screen.height).Append(" },\n");
                sb.Append("  \"checks\": ").Append(JsonList(Checks)).Append(",\n");
                sb.Append("  \"failures\": ").Append(JsonList(Failures)).Append(",\n");
                sb.Append("  \"errors\": ").Append(JsonList(Errors)).Append(",\n");
                sb.Append("  \"screenshots\": ").Append(JsonList(Screenshots)).Append(",\n");
                sb.Append("  \"metrics\": {");
                bool first = true;
                foreach (var pair in MetricsMap)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append("\"").Append(Esc(pair.Key)).Append("\": ").Append(Num(pair.Value));
                }
                sb.Append("}\n}\n");
                File.WriteAllText(_reportPath, sb.ToString(), Encoding.UTF8);
                Debug.Log("[GameProbe] report: " + _reportPath + " ok=" + (Failures.Count == 0 && Errors.Count == 0));
            }
            catch (Exception e)
            {
                Debug.LogError("[GameProbe] Could not write report: " + e.Message);
            }
        }

        void OnGUI()
        {
            if (!_overlay || !_running) return;
            var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
            float fps = _elapsed > 0 ? _frames / _elapsed : 0f;
            GUI.Box(new Rect(10, 10, 330, 78),
                "GameProbe  " + fps.ToString("0") + " fps  " + _elapsed.ToString("0.0") + "s\n" +
                "errors: " + Errors.Count + "   failures: " + Failures.Count + "\n" +
                (Errors.Count > 0 ? Errors[0].Split('\n')[0] : "running..."), style);
        }

        // ------------------------------------------------------------- helpers

        static string ProjectRoot()
        {
            if (Application.isEditor) return Directory.GetCurrentDirectory();
            return Directory.GetParent(Application.dataPath).FullName;
        }

        static bool HasArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return fallback;
        }

        static float ArgFloat(string name, float fallback)
        {
            float value;
            return float.TryParse(Arg(name, null), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        static string Num(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static string JsonList(List<string> values)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"").Append(Esc(values[i])).Append("\"");
            }
            return sb.Append(']').ToString();
        }

        static string Esc(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
