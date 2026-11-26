// Unity Editor bridge for the OpenCode "unity-game-dev" skill.
// Copy to <project>/Assets/Editor/AIUnity/UnityBridge.cs (done automatically by the skill scripts).
// Listens on 127.0.0.1 and executes JSON commands on the Unity main thread.
// No external packages required. Compatible with Unity 2021.3+ (tested on 6000.2).

// В Unity 6.6 старые GetInstanceID/InstanceIDToObject стали ошибками компиляции.
// Мост для прототипа, поэтому просто глушим предупреждение, а не переписываем API.
#pragma warning disable 0619

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace AIUnityTools
{
    [InitializeOnLoad]
    public static class UnityBridge
    {
        const int BasePort = 17890;
        const int PortRange = 20;
        const int LogLimit = 4000;

        static volatile bool _running;
        static TcpListener _listener;
        static Thread _acceptThread;
        static int _port;
        static string _portDir;
        static readonly ConcurrentQueue<Action> _mainQueue = new ConcurrentQueue<Action>();
        static readonly object _logLock = new object();
        static readonly List<Dictionary<string, object>> _logs = new List<Dictionary<string, object>>();
        static long _nextLogId = 1;

        static UnityBridge()
        {
            _running = true;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update += Pump;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            StartServer();
        }

        // ------------------------------------------------------------------ server

        static void StartServer()
        {
            int preferred = BasePort;
            string env = Environment.GetEnvironmentVariable("UNITY_BRIDGE_PORT");
            int parsed;
            if (!string.IsNullOrEmpty(env) && int.TryParse(env, out parsed)) preferred = parsed;

            for (int offset = 0; offset < PortRange && _running; offset++)
            {
                int port = preferred + offset;
                try
                {
                    var listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    listener.Start(8);
                    _listener = listener;
                    _port = port;
                    break;
                }
                catch (SocketException)
                {
                    // port busy, try the next one
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[AIUnityBridge] Failed to start on port " + port + ": " + e.Message);
                }
            }

            if (_listener == null)
            {
                Debug.LogError("[AIUnityBridge] Could not bind any port in " + BasePort + ".." + (BasePort + PortRange - 1));
                return;
            }

            try
            {
                _portDir = Path.Combine(Directory.GetCurrentDirectory(), "Library", "AIUnityBridge");
                Directory.CreateDirectory(_portDir);
                File.WriteAllText(Path.Combine(_portDir, "port.txt"), _port.ToString(CultureInfo.InvariantCulture));
                File.WriteAllText(Path.Combine(_portDir, "bridge.json"), MiniJson.Serialize(new Dictionary<string, object>
                {
                    { "port", _port },
                    { "pid", Process.GetCurrentProcess().Id },
                    { "project", Directory.GetCurrentDirectory() },
                    { "unity", Application.unityVersion },
                    { "started", DateTime.UtcNow.ToString("o") }
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AIUnityBridge] Could not write port file: " + e.Message);
            }

            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "AIUnityBridge" };
            _acceptThread.Start();
            Debug.Log("[AIUnityBridge] Listening on 127.0.0.1:" + _port + " for " + Directory.GetCurrentDirectory());
        }

        static void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    break;
                }
                ThreadPool.QueueUserWorkItem(HandleClient, client);
            }
        }

        static void HandleClient(object state)
        {
            var client = (TcpClient)state;
            try
            {
                client.NoDelay = true;
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var response = new Dictionary<string, object>();
                        string action = null;
                        try
                        {
                            var parsed = MiniJson.Parse(line) as Dictionary<string, object>;
                            if (parsed == null) throw new Exception("Request must be a JSON object");
                            action = Str(parsed, "action", Str(parsed, "cmd", null));
                            if (string.IsNullOrEmpty(action)) throw new Exception("Missing 'action'");

                            Dictionary<string, object> p = null;
                            object rawParams;
                            if (parsed.TryGetValue("params", out rawParams)) p = rawParams as Dictionary<string, object>;
                            if (p == null) p = parsed;

                            response["ok"] = true;
                            response["action"] = action;
                            response["data"] = Dispatch(action, p);
                        }
                        catch (Exception ex)
                        {
                            response.Clear();
                            response["ok"] = false;
                            response["action"] = action;
                            response["error"] = ex.Message;
                            response["type"] = ex.GetType().Name;
                            response["stack"] = Trim(ex.StackTrace, 4000);
                        }
                        writer.WriteLine(MiniJson.Serialize(response));
                    }
                }
            }
            catch (Exception)
            {
                // client disconnected
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        static void Stop()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); } catch { }
            try
            {
                if (!string.IsNullOrEmpty(_portDir))
                {
                    string portFile = Path.Combine(_portDir, "port.txt");
                    if (File.Exists(portFile) && File.ReadAllText(portFile).Trim() == _port.ToString(CultureInfo.InvariantCulture))
                        File.Delete(portFile);
                }
            }
            catch { }
        }

        // ------------------------------------------------------------ main thread

        static void Pump()
        {
            Action action;
            while (_mainQueue.TryDequeue(out action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        static object OnMain(Func<object> fn, int timeoutMs)
        {
            object result = null;
            Exception error = null;
            using (var done = new ManualResetEventSlim(false))
            {
                _mainQueue.Enqueue(delegate
                {
                    try { result = fn(); }
                    catch (Exception e) { error = e; }
                    finally { done.Set(); }
                });
                if (!done.Wait(timeoutMs))
                    throw new TimeoutException("Unity main thread did not process the command within " + timeoutMs + " ms (compiling/building?)");
            }
            if (error != null) throw new Exception(error.Message, error);
            return result;
        }

        static void RunAfterFrames(int frames, Action action)
        {
            if (frames <= 0) { action(); return; }
            int remaining = frames;
            EditorApplication.CallbackFunction cb = null;
            cb = delegate
            {
                remaining--;
                if (remaining > 0) return;
                EditorApplication.update -= cb;
                action();
            };
            EditorApplication.update += cb;
        }

        // -------------------------------------------------------------- dispatch

        static object Dispatch(string action, Dictionary<string, object> p)
        {
            action = action.ToLowerInvariant();
            switch (action)
            {
                case "ping":
                case "status":
                    return OnMain(Ping, 15000);
                case "console":
                case "logs":
                    return OnMain(delegate { return ConsoleDump(p); }, 15000);
                case "compile":
                case "refresh":
                    return Compile();
                case "play":
                    return OnMain(delegate { EditorApplication.isPlaying = true; return Ping(); }, 15000);
                case "stop":
                    return OnMain(delegate { EditorApplication.isPlaying = false; EditorApplication.isPaused = false; return Ping(); }, 15000);
                case "pause":
                    return OnMain(delegate { EditorApplication.isPaused = !EditorApplication.isPaused; return Ping(); }, 15000);
                case "scene":
                    return OnMain(delegate { return SceneAction(p); }, 120000);
                case "create":
                    return OnMain(delegate { return CreateObject(p); }, 30000);
                case "update":
                    return OnMain(delegate { return UpdateObject(p); }, 30000);
                case "component":
                    return OnMain(delegate { return ComponentAction(p); }, 30000);
                case "delete":
                    return OnMain(delegate { return DeleteObject(p); }, 30000);
                case "find":
                    return OnMain(delegate { return FindObjects(p); }, 30000);
                case "camera":
                    return OnMain(delegate { return CameraAction(p); }, 30000);
                case "light":
                    return OnMain(delegate { return LightAction(p); }, 30000);
                case "ui":
                    return OnMain(delegate { return UIAction(p); }, 30000);
                case "screenshot":
                    return HandleScreenshot(p);
                case "frame":
                    return OnMain(delegate { return FrameObject(p); }, 15000);
                case "menu":
                    return OnMain(delegate { return RunMenu(p); }, 120000);
                case "invoke":
                    return OnMain(delegate { return InvokeMethod(p); }, 120000);
                case "packages":
                    return OnMain(delegate { return PackageList(); }, 30000);
                case "add_package":
                    return AddPackage(p);
                case "build":
                    return OnMain(delegate { return BuildAction(p); }, 3600000);
                case "raycast":
                    return OnMain(delegate { return RaycastAction(p); }, 30000);
                case "stats":
                    return OnMain(delegate { return Stats(); }, 15000);
                case "save":
                    return OnMain(delegate { return SaveAll(p); }, 60000);
                case "quit":
                    return Quit();
                default:
                    throw new Exception("Unknown action '" + action + "'. See unity_status / the skill reference for supported actions.");
            }
        }

        // ------------------------------------------------------------------ info

        static Dictionary<string, object> Ping()
        {
            var scene = SceneManager.GetActiveScene();
            return D(
                "unity", Application.unityVersion,
                "project", Directory.GetCurrentDirectory(),
                "port", _port,
                "pid", Process.GetCurrentProcess().Id,
                "playing", EditorApplication.isPlaying,
                "paused", EditorApplication.isPaused,
                "compiling", EditorApplication.isCompiling,
                "updating", EditorApplication.isUpdating,
                "scene", scene.path,
                "sceneName", scene.name,
                "dirty", scene.isDirty,
                "buildTarget", EditorUserBuildSettings.activeBuildTarget.ToString(),
                "renderPipeline", GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.GetType().Name : "Built-in",
                "platform", Application.platform.ToString(),
                "screen", D("width", Screen.width, "height", Screen.height),
                "playMode", EditorApplication.isPlaying ? (EditorApplication.isPaused ? "paused" : "playing") : "edit",
                "time", DateTime.UtcNow.ToString("o"));
        }

        static Dictionary<string, object> Stats()
        {
            return D(
                "unity", Application.unityVersion,
                "targetFrameRate", Application.targetFrameRate,
                "timeScale", Time.timeScale,
                "vSync", QualitySettings.vSyncCount,
                "quality", QualitySettings.names.Length > QualitySettings.GetQualityLevel() ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "",
                "screen", D("width", Screen.width, "height", Screen.height, "fullscreen", Screen.fullScreen),
                "frameCount", Time.frameCount,
                "realtimeSinceStartup", Time.realtimeSinceStartup,
                "system", D(
                    "os", SystemInfo.operatingSystem,
                    "cpu", SystemInfo.processorType,
                    "gpu", SystemInfo.graphicsDeviceName,
                    "memoryMB", SystemInfo.systemMemorySize),
                "paths", D(
                    "project", Directory.GetCurrentDirectory(),
                    "assets", Application.dataPath,
                    "persistent", Application.persistentDataPath));
        }

        static Dictionary<string, object> SaveAll(Dictionary<string, object> p)
        {
            AssetDatabase.SaveAssets();
            if (GetBool(p, "scenes", true)) EditorSceneManager.SaveOpenScenes();
            AssetDatabase.Refresh();
            return D("saved", true);
        }

        // ---------------------------------------------------------------- scenes

        static object SceneAction(Dictionary<string, object> p)
        {
            string op = Str(p, "op", "info").ToLowerInvariant();
            if (op == "list")
            {
                var scenes = AssetDatabase.FindAssets("t:Scene")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(s => !s.StartsWith("Packages/"))
                    .OrderBy(s => s)
                    .ToList();
                return D("scenes", scenes);
            }
            if (op == "new")
            {
                if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveOpenScenes();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string path = Str(p, "path", null);
                if (!string.IsNullOrEmpty(path))
                {
                    path = ProjectPath(path);
                    EnsureAssetFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                    EditorSceneManager.SaveScene(scene, path);
                }
                return D("scene", scene.path, "name", scene.name);
            }
            if (op == "open")
            {
                string path = Str(p, "path", null);
                if (string.IsNullOrEmpty(path)) throw new Exception("'path' is required for scene open");
                path = ProjectPath(path);
                if (!File.Exists(path)) throw new Exception("Scene not found: " + path);
                if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveOpenScenes();
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                return D("scene", scene.path, "name", scene.name);
            }
            if (op == "save")
            {
                string path = Str(p, "path", null);
                var scene = SceneManager.GetActiveScene();
                bool ok;
                if (!string.IsNullOrEmpty(path))
                {
                    path = ProjectPath(path);
                    EnsureAssetFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                    ok = EditorSceneManager.SaveScene(scene, path);
                }
                else
                {
                    if (string.IsNullOrEmpty(scene.path))
                        throw new Exception("Active scene has no path. Pass 'path' or use op='save_as'.");
                    ok = EditorSceneManager.SaveScene(scene);
                }
                return D("saved", ok, "scene", scene.path);
            }
            var active = SceneManager.GetActiveScene();
            return D("scene", active.path, "name", active.name, "dirty", active.isDirty);
        }

        static object CreateObject(Dictionary<string, object> p)
        {
            string name = Str(p, "name", "GameObject");
            string primitive = Str(p, "primitive", "empty").ToLowerInvariant();
            GameObject go;
            if (primitive == "empty" || primitive == "none")
            {
                go = new GameObject(name);
            }
            else
            {
                PrimitiveType type;
                if (!TryPrimitive(primitive, out type)) throw new Exception("Unknown primitive '" + primitive + "'");
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                if (!GetBool(p, "collider", true))
                {
                    var collider = go.GetComponent<Collider>();
                    if (collider != null) Object.DestroyImmediate(collider);
                }
            }
            Undo.RegisterCreatedObjectUndo(go, "AI Create " + name);

            string parent = Str(p, "parent", null);
            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = ResolveObject(parent);
                if (parentGo == null) throw new Exception("Parent not found: " + parent);
                go.transform.SetParent(parentGo.transform, true);
            }

            ApplyTransform(go.transform, p, true);
            string tag = Str(p, "tag", null);
            if (!string.IsNullOrEmpty(tag))
            {
                try { go.tag = tag; } catch { }
            }
            int? layer = GetInt(p, "layer", null);
            if (layer.HasValue) go.layer = layer.Value;
            if (GetBool(p, "static", false)) go.isStatic = true;
            string color = Str(p, "color", null);
            if (!string.IsNullOrEmpty(color))
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null) throw new Exception("Object has no Renderer to color");
                renderer.sharedMaterial = GetMaterial(color, name);
            }

            MarkDirty();
            return Info(go);
        }

        static object UpdateObject(Dictionary<string, object> p)
        {
            var go = RequireObject(p);
            Undo.RecordObject(go.transform, "AI Update");
            string newName = Str(p, "name", null);
            if (!string.IsNullOrEmpty(newName)) go.name = newName;
            ApplyTransform(go.transform, p, GetBool(p, "local", false));
            bool? active = GetBoolOrNull(p, "active");
            if (active.HasValue) go.SetActive(active.Value);
            string tag = Str(p, "tag", null);
            if (!string.IsNullOrEmpty(tag)) { try { go.tag = tag; } catch { } }
            int? layer = GetInt(p, "layer", null);
            if (layer.HasValue) go.layer = layer.Value;
            bool? isStatic = GetBoolOrNull(p, "static");
            if (isStatic.HasValue) go.isStatic = isStatic.Value;
            string parent = Str(p, "parent", null);
            if (parent != null)
            {
                if (parent == "" || parent == "null") go.transform.SetParent(null, true);
                else
                {
                    var parentGo = ResolveObject(parent);
                    if (parentGo == null) throw new Exception("Parent not found: " + parent);
                    go.transform.SetParent(parentGo.transform, true);
                }
            }
            string color = Str(p, "color", null);
            if (!string.IsNullOrEmpty(color))
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null) throw new Exception("Object has no Renderer to color");
                renderer.sharedMaterial = GetMaterial(color, go.name);
            }
            MarkDirty();
            return Info(go);
        }

        static object DeleteObject(Dictionary<string, object> p)
        {
            var go = RequireObject(p);
            string path = PathOf(go);
            Undo.DestroyObjectImmediate(go);
            MarkDirty();
            return D("deleted", path);
        }

        static object FindObjects(Dictionary<string, object> p)
        {
            string name = Str(p, "name", null);
            string component = Str(p, "component", Str(p, "type", null));
            string tag = Str(p, "tag", null);
            int limit = GetInt(p, "limit", 100) ?? 100;
            Type componentType = null;
            if (!string.IsNullOrEmpty(component)) componentType = ResolveType(component);

            var results = new List<Dictionary<string, object>>();
            foreach (var root in AllRoots())
            {
                foreach (var go in Enumerate(root))
                {
                    if (results.Count >= limit) break;
                    if (!string.IsNullOrEmpty(name) && go.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!string.IsNullOrEmpty(tag) && !string.Equals(go.tag, tag, StringComparison.OrdinalIgnoreCase)) continue;
                    if (componentType != null && go.GetComponent(componentType) == null) continue;
                    results.Add(Info(go));
                }
            }
            return D("count", results.Count, "objects", results);
        }

        static object ComponentAction(Dictionary<string, object> p)
        {
            var go = RequireObject(p);
            string op = Str(p, "op", "add").ToLowerInvariant();
            string typeName = Str(p, "type", null);
            if (string.IsNullOrEmpty(typeName)) throw new Exception("'type' is required");
            var type = ResolveType(typeName);

            if (op == "add")
            {
                if (go.GetComponent(type) != null) return D("added", false, "alreadyPresent", true, "object", Info(go));
                var comp = Undo.AddComponent(go, type);
                var props = GetDict(p, "props");
                if (props != null) foreach (var kv in props) SetMember(comp, kv.Key, kv.Value);
                MarkDirty();
                return D("added", true, "type", type.Name, "object", Info(go));
            }
            var existing = go.GetComponent(type);
            if (existing == null) throw new Exception("Component " + type.Name + " not found on " + go.name);
            if (op == "remove")
            {
                Undo.DestroyObjectImmediate(existing);
                MarkDirty();
                return D("removed", type.Name);
            }
            if (op == "set")
            {
                var props = GetDict(p, "props");
                if (props == null) throw new Exception("'props' object is required for set");
                var applied = new Dictionary<string, object>();
                foreach (var kv in props)
                {
                    SetMember(existing, kv.Key, kv.Value);
                    applied[kv.Key] = kv.Value;
                }
                MarkDirty();
                return D("type", type.Name, "applied", applied);
            }
            if (op == "get")
            {
                return D("type", type.Name, "object", Info(go));
            }
            throw new Exception("Unknown component op '" + op + "' (add/remove/set/get)");
        }

        // ---------------------------------------------------------------- camera

        static object CameraAction(Dictionary<string, object> p)
        {
            string target = Str(p, "target", Str(p, "name", null));
            GameObject go = null;
            if (!string.IsNullOrEmpty(target)) go = ResolveObject(target);
            Camera cam = go != null ? go.GetComponent<Camera>() : null;

            bool create = GetBool(p, "create", true);
            if (cam == null && create)
            {
                if (go == null)
                {
                    go = new GameObject(Str(p, "name", "Main Camera"));
                    Undo.RegisterCreatedObjectUndo(go, "AI Camera");
                }
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            if (cam == null) throw new Exception("No camera found. Set create=true.");

            ApplyTransform(cam.transform, p, false);
            string lookAt = Str(p, "lookAt", null);
            if (!string.IsNullOrEmpty(lookAt))
            {
                var lookTarget = ResolveObject(lookAt);
                Vector3 point = lookTarget != null ? lookTarget.transform.position : ParseVector3(lookAt, Vector3.zero);
                cam.transform.LookAt(point);
            }
            float? fov = GetFloat(p, "fov", null);
            if (fov.HasValue) cam.fieldOfView = fov.Value;
            bool? ortho = GetBoolOrNull(p, "orthographic");
            if (ortho.HasValue) cam.orthographic = ortho.Value;
            float? size = GetFloat(p, "orthographicSize", null);
            if (size.HasValue) cam.orthographicSize = size.Value;
            string bg = Str(p, "background", null);
            if (!string.IsNullOrEmpty(bg))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = ParseColor(bg, cam.backgroundColor);
            }
            float? near = GetFloat(p, "near", null);
            if (near.HasValue) cam.nearClipPlane = near.Value;
            float? far = GetFloat(p, "far", null);
            if (far.HasValue) cam.farClipPlane = far.Value;
            MarkDirty();
            return Info(cam.gameObject);
        }

        static object LightAction(Dictionary<string, object> p)
        {
            var go = new GameObject(Str(p, "name", "Light"));
            Undo.RegisterCreatedObjectUndo(go, "AI Light");
            var light = go.AddComponent<Light>();
            string type = Str(p, "type", "directional").ToLowerInvariant();
            light.type = type == "point" ? LightType.Point : type == "spot" ? LightType.Spot : LightType.Directional;
            ApplyTransform(go.transform, p, false);
            string color = Str(p, "color", null);
            if (!string.IsNullOrEmpty(color)) light.color = ParseColor(color, light.color);
            float? intensity = GetFloat(p, "intensity", null);
            if (intensity.HasValue) light.intensity = intensity.Value;
            float? range = GetFloat(p, "range", null);
            if (range.HasValue) light.range = range.Value;
            bool? shadows = GetBoolOrNull(p, "shadows");
            if (shadows.HasValue) light.shadows = shadows.Value ? LightShadows.Soft : LightShadows.None;
            MarkDirty();
            return Info(go);
        }

        // ------------------------------------------------------------------- ui

        static object UIAction(Dictionary<string, object> p)
        {
            string kind = Str(p, "kind", Str(p, "type", "panel")).ToLowerInvariant();
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (kind == "canvas")
            {
                if (canvas != null && GetBool(p, "reuse", true)) return Info(canvas.gameObject);
                return Info(CreateCanvas(Str(p, "name", "Canvas")).gameObject);
            }
            if (canvas == null) canvas = CreateCanvas("Canvas");

            string parentName = Str(p, "parent", null);
            GameObject parent = null;
            if (!string.IsNullOrEmpty(parentName))
            {
                parent = ResolveObject(parentName);
                if (parent == null) throw new Exception("UI parent not found: " + parentName);
            }
            if (parent == null) parent = canvas.gameObject;

            var go = new GameObject(Str(p, "name", kind), typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "AI UI " + kind);
            var rect = (RectTransform)go.transform;
            ApplyRect(rect, p);

            if (kind == "text" || kind == "label")
            {
                var textType = ResolveType("UnityEngine.UI.Text");
                if (textType == null) throw new Exception("UI Text unavailable. Add package com.unity.ugui.");
                var text = go.AddComponent(textType);
                SetMember(text, "text", Str(p, "text", "Text"));
                SetMember(text, "font", GetDefaultFont());
                SetMember(text, "fontSize", GetInt(p, "fontSize", 32) ?? 32);
                SetMember(text, "color", ParseColor(Str(p, "color", "#FFFFFF"), Color.white));
                SetMember(text, "alignment", Str(p, "alignment", "MiddleCenter"));
                SetMember(text, "raycastTarget", false);
            }
            else if (kind == "button")
            {
                var imageType = ResolveType("UnityEngine.UI.Image");
                var buttonType = ResolveType("UnityEngine.UI.Button");
                if (imageType == null || buttonType == null) throw new Exception("UI Button unavailable. Add package com.unity.ugui.");
                var image = go.AddComponent(imageType);
                SetMember(image, "color", ParseColor(Str(p, "color", "#3C7DD9"), Color.white));
                var button = go.AddComponent(buttonType);
                SetMember(button, "targetGraphic", image);

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                var textType = ResolveType("UnityEngine.UI.Text");
                var text = labelGo.AddComponent(textType);
                SetMember(text, "text", Str(p, "text", "Button"));
                SetMember(text, "font", GetDefaultFont());
                SetMember(text, "fontSize", GetInt(p, "fontSize", 28) ?? 28);
                SetMember(text, "color", ParseColor(Str(p, "textColor", "#FFFFFF"), Color.white));
                SetMember(text, "alignment", "MiddleCenter");
            }
            else if (kind == "image" || kind == "panel")
            {
                var imageType = ResolveType("UnityEngine.UI.Image");
                if (imageType == null) throw new Exception("UI Image unavailable. Add package com.unity.ugui.");
                var image = go.AddComponent(imageType);
                SetMember(image, "color", ParseColor(Str(p, "color", kind == "panel" ? "#00000080" : "#FFFFFF"), Color.white));
                string sprite = Str(p, "sprite", null);
                if (!string.IsNullOrEmpty(sprite))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectPath(sprite));
                    if (asset != null) SetMember(image, "sprite", asset);
                }
            }
            else
            {
                throw new Exception("Unknown UI kind '" + kind + "' (canvas/text/button/image/panel)");
            }
            MarkDirty();
            return Info(go);
        }

        static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scalerType = ResolveType("UnityEngine.UI.CanvasScaler");
            if (scalerType != null)
            {
                var scaler = go.AddComponent(scalerType);
                SetMember(scaler, "uiScaleMode", "ScaleWithScreenSize");
                SetMember(scaler, "referenceResolution", new Vector2(1920, 1080));
                SetMember(scaler, "screenMatchMode", "MatchWidthOrHeight");
                SetMember(scaler, "matchWidthOrHeight", 0.5f);
            }
            var raycasterType = ResolveType("UnityEngine.UI.GraphicRaycaster");
            if (raycasterType != null) go.AddComponent(raycasterType);
            Undo.RegisterCreatedObjectUndo(go, "AI Canvas");
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) { }
            MarkDirty();
            return canvas;
        }

        static void ApplyRect(RectTransform rect, Dictionary<string, object> p)
        {
            rect.anchorMin = ParseVector2(p, "anchorMin", new Vector2(0.5f, 0.5f));
            rect.anchorMax = ParseVector2(p, "anchorMax", rect.anchorMin);
            rect.pivot = ParseVector2(p, "pivot", new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = ParseVector2(p, "position", Vector2.zero);
            float? width = GetFloat(p, "width", null);
            float? height = GetFloat(p, "height", null);
            if (width.HasValue && height.HasValue) rect.sizeDelta = new Vector2(width.Value, height.Value);
            Vector2 fixedSize = ParseVector2(p, "size", Vector2.zero);
            if (fixedSize != Vector2.zero) rect.sizeDelta = fixedSize;
            rect.offsetMin = ParseVector2(p, "offsetMin", rect.offsetMin);
            rect.offsetMax = ParseVector2(p, "offsetMax", rect.offsetMax);
            rect.localScale = Vector3.one;
        }

        // ---------------------------------------------------------------- screen

        static object HandleScreenshot(Dictionary<string, object> p)
        {
            string source = Str(p, "source", "camera").ToLowerInvariant();
            int width = GetInt(p, "width", 1280) ?? 1280;
            int height = GetInt(p, "height", 720) ?? 720;
            int frames = GetInt(p, "waitFrames", source == "screen" ? 2 : 0) ?? 0;
            int timeout = GetInt(p, "timeoutMs", 20000) ?? 20000;

            string path = Str(p, "path", null);
            if (string.IsNullOrEmpty(path))
                path = Path.Combine("TestArtifacts", "screenshots", "shot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            path = ProjectPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);

            if (source == "scene")
            {
                var view = SceneView.lastActiveSceneView;
                if (view == null || view.camera == null) throw new Exception("No active Scene view to capture");
                OnMain(delegate { CaptureCamera(view.camera, path, width, height); return null; }, 30000);
            }
            else if (source == "screen" || (source == "game" && EditorApplication.isPlaying))
            {
                var done = new ManualResetEventSlim(false);
                Exception error = null;
                OnMain(delegate
                {
                    RunAfterFrames(frames, delegate
                    {
                        try { ScreenCapture.CaptureScreenshot(path, 1); }
                        catch (Exception e) { error = e; }
                        finally { done.Set(); }
                    });
                    return null;
                }, 30000);
                if (!done.Wait(timeout)) throw new TimeoutException("Screenshot timed out (is the editor paused?)");
                if (error != null) throw error;

                var sw = Stopwatch.StartNew();
                long last = -1;
                int stable = 0;
                while (sw.ElapsedMilliseconds < timeout)
                {
                    if (File.Exists(path))
                    {
                        long len = new FileInfo(path).Length;
                        if (len > 0)
                        {
                            if (len == last) stable++; else stable = 0;
                            last = len;
                            if (stable >= 2) break;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            else
            {
                var done = new ManualResetEventSlim(false);
                Exception error = null;
                OnMain(delegate
                {
                    RunAfterFrames(frames, delegate
                    {
                        try
                        {
                            var cam = FindCamera(p);
                            if (cam == null) throw new Exception("No camera in the scene");
                            CaptureCamera(cam, path, width, height);
                        }
                        catch (Exception e) { error = e; }
                        finally { done.Set(); }
                    });
                    return null;
                }, 30000);
                if (!done.Wait(timeout)) throw new TimeoutException("Screenshot timed out");
                if (error != null) throw error;
            }

            if (!File.Exists(path)) throw new Exception("Screenshot file was not created: " + path + " (headless editor?)");
            return D("path", path, "width", width, "height", height, "source", source, "bytes", new FileInfo(path).Length);
        }

        static void CaptureCamera(Camera cam, string path, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var oldTarget = cam.targetTexture;
            var oldActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static Camera FindCamera(Dictionary<string, object> p)
        {
            string target = Str(p, "camera", Str(p, "target", null));
            if (!string.IsNullOrEmpty(target))
            {
                var go = ResolveObject(target);
                if (go != null)
                {
                    var cam = go.GetComponent<Camera>();
                    if (cam != null) return cam;
                }
            }
            if (Camera.main != null) return Camera.main;
            var cams = Object.FindObjectsOfType<Camera>();
            return cams.FirstOrDefault(c => c.isActiveAndEnabled) ?? cams.FirstOrDefault();
        }

        // ---------------------------------------------------------------- misc

        static object FrameObject(Dictionary<string, object> p)
        {
            var go = RequireObject(p);
            Selection.activeGameObject = go;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
                SceneView.lastActiveSceneView.Repaint();
            }
            return Info(go);
        }

        static object RunMenu(Dictionary<string, object> p)
        {
            string path = Str(p, "path", null);
            if (string.IsNullOrEmpty(path)) throw new Exception("'path' (menu item) is required");
            bool ok = EditorApplication.ExecuteMenuItem(path);
            if (!ok) throw new Exception("Menu item not found or disabled: " + path);
            return D("executed", path);
        }

        static object InvokeMethod(Dictionary<string, object> p)
        {
            string typeName = Str(p, "type", null);
            string methodName = Str(p, "method", null);
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
                throw new Exception("'type' and 'method' are required");
            var type = ResolveType(typeName);
            object instance = null;
            string target = Str(p, "target", null);
            if (!string.IsNullOrEmpty(target) && !GetBool(p, "static", false))
            {
                var targetGo = ResolveObject(target);
                if (targetGo == null) throw new Exception("Object not found: " + target);
                instance = targetGo.GetComponent(type);
                if (instance == null) instance = targetGo.GetComponentInChildren(type, true);
                if (instance == null) throw new Exception("Component not found: " + type.Name + " on " + target);
            }
            var argList = new List<object>();
            object rawArgs;
            if (p.TryGetValue("args", out rawArgs) && rawArgs is IEnumerable && !(rawArgs is string))
                foreach (var a in (IEnumerable)rawArgs) argList.Add(a);

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.Name == methodName && m.GetParameters().Length == argList.Count)
                .ToList();
            if (methods.Count == 0) throw new Exception("Method not found: " + typeName + "." + methodName + " with " + argList.Count + " args");

            Exception last = null;
            foreach (var method in methods)
            {
                try
                {
                    var parameters = method.GetParameters();
                    var converted = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++) converted[i] = ConvertValue(argList[i], parameters[i].ParameterType);
                    var result = method.Invoke(method.IsStatic ? null : instance, converted);
                    return D("result", Simplify(result), "method", methodName, "type", type.Name);
                }
                catch (Exception e)
                {
                    last = e;
                }
            }
            throw new Exception("Invoke failed: " + (last != null ? last.Message : "unknown"), last);
        }

        static object PackageList()
        {
            var list = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .Select(pi => D("name", pi.name, "version", pi.version))
                .OrderBy(d => (string)d["name"])
                .ToList();
            return D("count", list.Count, "packages", list);
        }

        static object AddPackage(Dictionary<string, object> p)
        {
            string name = Str(p, "name", null);
            if (string.IsNullOrEmpty(name)) throw new Exception("'name' is required");
            EditorApplication.delayCall += delegate
            {
                try
                {
                    var request = UnityEditor.PackageManager.Client.Add(name);
                    Debug.Log("[AIUnityBridge] Package add requested: " + name + " (" + request.Status + ")");
                }
                catch (Exception e) { Debug.LogError("[AIUnityBridge] Package add failed: " + e.Message); }
            };
            return D("requested", name, "hint", "Wait for compile/import, then read console");
        }

        static object BuildAction(Dictionary<string, object> p)
        {
            BuildTarget target = ParseBuildTarget(Str(p, "target", "Win64"));
            string output = NormalizeBuildOutput(Str(p, "output", null), target);
            string[] scenes = GetStringList(p, "scenes");
            if (scenes.Length == 0)
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new Exception("No scenes to build. Add a scene to build settings or pass 'scenes'.");

            var options = BuildOptions.None;
            if (GetBool(p, "development", false)) options |= BuildOptions.Development;
            if (GetBool(p, "allowDebugging", false)) options |= BuildOptions.AllowDebugging;
            if (GetBool(p, "connectProfiler", false)) options |= BuildOptions.ConnectWithProfiler;

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(scenes, output, target, options);
            var summary = report.summary;
            var messages = new List<string>();
            foreach (var step in report.steps)
                foreach (var msg in step.messages)
                    if (msg.type == LogType.Error || msg.type == LogType.Exception || msg.type == LogType.Warning)
                        if (messages.Count < 40) messages.Add(msg.type + ": " + Trim(msg.content, 600));

            return D(
                "result", summary.result.ToString(),
                "output", output,
                "totalSize", summary.totalSize,
                "totalTimeSeconds", summary.totalTime.TotalSeconds,
                "errors", summary.totalErrors,
                "warnings", summary.totalWarnings,
                "target", target.ToString(),
                "scenes", scenes,
                "messages", messages);
        }

        static object RaycastAction(Dictionary<string, object> p)
        {
            Vector3 origin = ParseVector3(p, "origin", Vector3.zero);
            Vector3 direction = ParseVector3(p, "direction", Vector3.forward);
            float distance = GetFloat(p, "maxDistance", 1000f) ?? 1000f;
            RaycastHit hit;
            bool ok = Physics.Raycast(origin, direction.normalized, out hit, distance);
            if (!ok) return D("hit", false);
            return D(
                "hit", true,
                "point", Vec(hit.point),
                "normal", Vec(hit.normal),
                "distance", hit.distance,
                "object", Info(hit.collider.gameObject));
        }

        static object Quit()
        {
            try { EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets(); } catch { }
            new Thread(delegate ()
            {
                Thread.Sleep(900);
                EditorApplication.Exit(0);
            }) { IsBackground = true }.Start();
            return D("quitting", true);
        }

        static object Compile()
        {
            EditorApplication.delayCall += delegate
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();
            };
            return D("refresh", "scheduled", "hint", "Poll ping until compiling=false");
        }

        // ---------------------------------------------------------------- console

        static void OnLog(string message, string stack, LogType type)
        {
            try
            {
                lock (_logLock)
                {
                    if (_logs.Count >= LogLimit) _logs.RemoveRange(0, _logs.Count - LogLimit + 100);
                    _logs.Add(new Dictionary<string, object>
                    {
                        { "id", (double)(Interlocked.Increment(ref _nextLogId) - 1) },
                        { "time", DateTime.UtcNow.ToString("HH:mm:ss.fff") },
                        { "type", type.ToString() },
                        { "message", Trim(message, 4000) },
                        { "stack", Trim(stack, 3000) }
                    });
                }
            }
            catch { }
        }

        static object ConsoleDump(Dictionary<string, object> p)
        {
            int limit = GetInt(p, "limit", 100) ?? 100;
            long since = (long)(GetFloat(p, "sinceId", 0) ?? 0);
            string contains = Str(p, "contains", null);
            var types = new HashSet<string>(GetStringList(p, "types").Select(t => t.ToLowerInvariant()));
            bool clear = GetBool(p, "clear", false);

            List<Dictionary<string, object>> selected;
            long lastId;
            lock (_logLock)
            {
                selected = _logs.Where(e =>
                {
                    long id = Convert.ToInt64(e["id"], CultureInfo.InvariantCulture);
                    if (id < since) return false;
                    if (types.Count > 0 && !types.Contains(((string)e["type"]).ToLowerInvariant())) return false;
                    if (!string.IsNullOrEmpty(contains) && ((string)e["message"]).IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0) return false;
                    return true;
                }).ToList();
                lastId = _nextLogId - 1;
                if (clear) _logs.Clear();
            }
            if (selected.Count > limit) selected = selected.Skip(selected.Count - limit).ToList();
            return D("count", selected.Count, "lastId", lastId, "cleared", clear, "entries", selected);
        }

        // ---------------------------------------------------------------- helpers

        public static Dictionary<string, object> D(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[Convert.ToString(kv[i], CultureInfo.InvariantCulture)] = kv[i + 1];
            return d;
        }

        static Dictionary<string, object> Info(GameObject go)
        {
            var t = go.transform;
            var comps = go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToList();
            var info = D(
                "name", go.name,
                "path", PathOf(go),
                // В Unity 6.6 GetInstanceID() стал ошибкой компиляции.
                // Для прототипа ID не нужен: объекты ищем по имени/пути.
                "instanceId", 0,
                "active", go.activeSelf,
                "activeInHierarchy", go.activeInHierarchy,
                "tag", go.tag,
                "layer", go.layer,
                "position", Vec(t.position),
                "rotation", Vec(t.eulerAngles),
                "localScale", Vec(t.localScale),
                "components", comps,
                "scene", go.scene.path);
            var rect = t as RectTransform;
            if (rect != null)
            {
                info["rect"] = D(
                    "anchoredPosition", Vec2(rect.anchoredPosition),
                    "sizeDelta", Vec2(rect.sizeDelta),
                    "anchorMin", Vec2(rect.anchorMin),
                    "anchorMax", Vec2(rect.anchorMax),
                    "pivot", Vec2(rect.pivot));
            }
            return info;
        }

        static string PathOf(GameObject go)
        {
            var parts = new List<string>();
            var t = go.transform;
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts.ToArray());
        }

        static IEnumerable<GameObject> AllRoots()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects()) yield return root;
            }
        }

        static IEnumerable<GameObject> Enumerate(GameObject root)
        {
            yield return root;
            foreach (Transform child in root.transform)
                foreach (var go in Enumerate(child.gameObject))
                    yield return go;
        }

        static GameObject ResolveObject(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            // Раньше здесь был поиск по InstanceID, но в Unity 6.6 этот API убрали.
            // Ищем по имени/пути — для нашей отладки этого хватает.
            var direct = GameObject.Find(target);
            if (direct != null) return direct;
            foreach (var root in AllRoots())
                foreach (var go in Enumerate(root))
                    if (string.Equals(go.name, target, StringComparison.OrdinalIgnoreCase)) return go;
            return null;
        }

        static GameObject RequireObject(Dictionary<string, object> p)
        {
            string target = Str(p, "target", Str(p, "path", Str(p, "name", null)));
            if (string.IsNullOrEmpty(target)) throw new Exception("'target' (name, hierarchy path or instance id) is required");
            var go = ResolveObject(target);
            if (go == null) throw new Exception("Object not found: " + target);
            return go;
        }

        static void ApplyTransform(Transform t, Dictionary<string, object> p, bool local)
        {
            bool useLocal = local || GetBool(p, "local", false);
            object raw;
            if (p.TryGetValue("position", out raw))
            {
                var v = ParseVector3(raw, useLocal ? t.localPosition : t.position);
                if (useLocal) t.localPosition = v; else t.position = v;
            }
            if (p.TryGetValue("rotation", out raw))
            {
                var v = ParseVector3(raw, useLocal ? t.localEulerAngles : t.eulerAngles);
                if (useLocal) t.localEulerAngles = v; else t.eulerAngles = v;
            }
            if (p.TryGetValue("scale", out raw))
            {
                t.localScale = ParseVector3(raw, t.localScale);
            }
            else if (p.TryGetValue("localScale", out raw))
            {
                t.localScale = ParseVector3(raw, t.localScale);
            }
            if (p.TryGetValue("lookAt", out raw) && raw is string)
            {
                var targetGo = ResolveObject((string)raw);
                if (targetGo != null) t.LookAt(targetGo.transform);
                else t.LookAt(ParseVector3(raw, Vector3.zero));
            }
        }

        static void MarkDirty()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        }

        static bool TryPrimitive(string name, out PrimitiveType type)
        {
            switch (name)
            {
                case "cube": type = PrimitiveType.Cube; return true;
                case "sphere": type = PrimitiveType.Sphere; return true;
                case "capsule": type = PrimitiveType.Capsule; return true;
                case "cylinder": type = PrimitiveType.Cylinder; return true;
                case "plane": type = PrimitiveType.Plane; return true;
                case "quad": type = PrimitiveType.Quad; return true;
                default: type = PrimitiveType.Cube; return false;
            }
        }

        static Material GetMaterial(string color, string name)
        {
            string hex = color.Trim();
            if (!hex.StartsWith("#")) hex = "#" + hex;
            string folder = "Assets/AI/Materials";
            EnsureAssetFolder(folder);
            string file = folder + "/" + Sanitize(name) + "_" + Sanitize(hex.Replace("#", "")) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(file);
            if (material == null)
            {
                material = new Material(FindLitShader());
                AssetDatabase.CreateAsset(material, file);
            }
            material.color = ParseColor(hex, Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Shader FindLitShader()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                string pipelineName = pipeline.GetType().Name;
                if (pipelineName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var urp = Shader.Find("Universal Render Pipeline/Lit");
                    if (urp != null) return urp;
                }
                var hdrp = Shader.Find("HDRP/Lit");
                if (hdrp != null) return hdrp;
            }
            var standard = Shader.Find("Standard");
            if (standard != null) return standard;
            var fallback = Shader.Find("Universal Render Pipeline/Lit");
            if (fallback != null) return fallback;
            return Shader.Find("Sprites/Default");
        }

        static Font GetDefaultFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            return font;
        }

        static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            var result = sb.ToString();
            return result.Length > 40 ? result.Substring(0, 40) : result;
        }

        static string ProjectPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return Directory.GetCurrentDirectory();
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        static string Trim(string s, int max)
        {
            if (s == null) return null;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        static object Simplify(object value)
        {
            if (value == null) return null;
            if (value is GameObject) return Info((GameObject)value);
            if (value is Component) return Info(((Component)value).gameObject);
            if (value is Vector3) return Vec((Vector3)value);
            if (value is Vector2) return Vec2((Vector2)value);
            if (value is Color) return "#" + ColorUtility.ToHtmlStringRGBA((Color)value);
            if (value is UnityEngine.Object) return ((UnityEngine.Object)value).name;
            if (value.GetType().IsPrimitive || value is string) return value;
            return value.ToString();
        }

        // ------------------------------------------------------------ reflection

        static Type ResolveType(string name)
        {
            var type = Type.GetType(name);
            if (type != null) return type;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { type = asm.GetType(name); } catch { }
                if (type != null) return type;
            }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = asm.GetTypes().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                }
                catch (ReflectionTypeLoadException e)
                {
                    type = e.Types.Where(t => t != null).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                }
                catch { }
                if (type != null) return type;
            }
            throw new Exception("Type not found: " + name);
        }

        static void SetMember(object target, string key, object value)
        {
            if (target == null) throw new Exception("Cannot set '" + key + "' on null");
            var type = target.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var field = type.GetField(key, flags);
            if (field != null)
            {
                field.SetValue(target, ConvertValue(value, field.FieldType));
                return;
            }
            var property = type.GetProperty(key, flags);
            if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(target, ConvertValue(value, property.PropertyType), null);
                return;
            }
            var serialized = new SerializedObject((UnityEngine.Object)target);
            var prop = serialized.FindProperty(key);
            if (prop != null)
            {
                SetSerialized(prop, value);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            throw new Exception("Member '" + key + "' not found on " + type.Name);
        }

        static void SetSerialized(SerializedProperty prop, object value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                    prop.intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = Convert.ToBoolean(value, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Float:
                    prop.floatValue = Convert.ToSingle(value, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.String:
                    prop.stringValue = Convert.ToString(value, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Color:
                    prop.colorValue = ParseColor(Convert.ToString(value, CultureInfo.InvariantCulture), prop.colorValue); break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = ParseVector2Value(value, prop.vector2Value); break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = ParseVector3(value, prop.vector3Value); break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = ParseVector4(value, prop.vector4Value); break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = Convert.ToInt32(value, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.ObjectReference:
                    var go = value is string ? ResolveObject((string)value) : null;
                    prop.objectReferenceValue = go != null ? (UnityEngine.Object)go.GetComponent<Component>() : null; break;
                default:
                    throw new Exception("Unsupported serialized property type: " + prop.propertyType);
            }
        }

        static object ConvertValue(object value, Type target)
        {
            if (value == null)
            {
                if (target.IsValueType) return Activator.CreateInstance(target);
                return null;
            }
            if (target.IsInstanceOfType(value)) return value;
            if (target == typeof(string)) return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (target == typeof(bool)) return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            if (target == typeof(int)) return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (target == typeof(float)) return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            if (target == typeof(double)) return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (target == typeof(long)) return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            if (target == typeof(Vector2)) return ParseVector2Value(value, Vector2.zero);
            if (target == typeof(Vector3)) return ParseVector3(value, Vector3.zero);
            if (target == typeof(Vector4)) return ParseVector4(value, Vector4.zero);
            if (target == typeof(Color)) return ParseColor(Convert.ToString(value, CultureInfo.InvariantCulture), Color.white);
            if (target.IsEnum) return Enum.Parse(target, Convert.ToString(value, CultureInfo.InvariantCulture), true);
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        static Dictionary<string, object> GetDict(Dictionary<string, object> p, string key)
        {
            object raw;
            if (!p.TryGetValue(key, out raw)) return null;
            return raw as Dictionary<string, object>;
        }

        static string Str(Dictionary<string, object> p, string key, string fallback)
        {
            object v;
            if (p != null && p.TryGetValue(key, out v) && v != null) return Convert.ToString(v, CultureInfo.InvariantCulture);
            return fallback;
        }

        static float? GetFloat(Dictionary<string, object> p, string key, float? fallback)
        {
            object v;
            if (p != null && p.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); } catch { }
            }
            return fallback;
        }

        static int? GetInt(Dictionary<string, object> p, string key, int? fallback)
        {
            object v;
            if (p != null && p.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch { }
            }
            return fallback;
        }

        static bool GetBool(Dictionary<string, object> p, string key, bool fallback)
        {
            var value = GetBoolOrNull(p, key);
            return value.HasValue ? value.Value : fallback;
        }

        static bool? GetBoolOrNull(Dictionary<string, object> p, string key)
        {
            object v;
            if (p != null && p.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToBoolean(v, CultureInfo.InvariantCulture); } catch { }
            }
            return null;
        }

        static string[] GetStringList(Dictionary<string, object> p, string key)
        {
            object raw;
            if (p == null || !p.TryGetValue(key, out raw) || raw == null) return new string[0];
            if (raw is string)
                return ((string)raw).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            if (raw is IEnumerable)
            {
                var list = new List<string>();
                foreach (var item in (IEnumerable)raw) if (item != null) list.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
                return list.ToArray();
            }
            return new[] { Convert.ToString(raw, CultureInfo.InvariantCulture) };
        }

        static Vector2 ParseVector2(Dictionary<string, object> p, string key, Vector2 fallback)
        {
            object raw;
            if (p != null && p.TryGetValue(key, out raw)) return ParseVector2Value(raw, fallback);
            return fallback;
        }

        static Vector2 ParseVector2Value(object raw, Vector2 fallback)
        {
            if (raw == null) return fallback;
            var d = raw as Dictionary<string, object>;
            if (d != null) return new Vector2(F(d, "x", fallback.x), F(d, "y", fallback.y));
            if (raw is IEnumerable && !(raw is string))
            {
                var values = ((IEnumerable)raw).Cast<object>().Select(v => Convert.ToSingle(v, CultureInfo.InvariantCulture)).ToArray();
                if (values.Length >= 2) return new Vector2(values[0], values[1]);
            }
            var parts = Convert.ToString(raw, CultureInfo.InvariantCulture).Split(',');
            if (parts.Length >= 2)
                return new Vector2(ParseFloat(parts[0], fallback.x), ParseFloat(parts[1], fallback.y));
            return fallback;
        }

        static Vector3 ParseVector3(Dictionary<string, object> p, string key, Vector3 fallback)
        {
            object raw;
            if (p != null && p.TryGetValue(key, out raw)) return ParseVector3(raw, fallback);
            return fallback;
        }

        static Vector3 ParseVector3(object raw, Vector3 fallback)
        {
            if (raw == null) return fallback;
            var d = raw as Dictionary<string, object>;
            if (d != null) return new Vector3(F(d, "x", fallback.x), F(d, "y", fallback.y), F(d, "z", fallback.z));
            if (raw is IEnumerable && !(raw is string))
            {
                var values = ((IEnumerable)raw).Cast<object>().Select(v => Convert.ToSingle(v, CultureInfo.InvariantCulture)).ToArray();
                if (values.Length >= 3) return new Vector3(values[0], values[1], values[2]);
            }
            var parts = Convert.ToString(raw, CultureInfo.InvariantCulture).Split(',');
            if (parts.Length >= 3)
                return new Vector3(ParseFloat(parts[0], fallback.x), ParseFloat(parts[1], fallback.y), ParseFloat(parts[2], fallback.z));
            return fallback;
        }

        static Vector4 ParseVector4(object raw, Vector4 fallback)
        {
            if (raw == null) return fallback;
            var d = raw as Dictionary<string, object>;
            if (d != null) return new Vector4(F(d, "x", fallback.x), F(d, "y", fallback.y), F(d, "z", fallback.z), F(d, "w", fallback.w));
            var parts = Convert.ToString(raw, CultureInfo.InvariantCulture).Split(',');
            if (parts.Length >= 4)
                return new Vector4(ParseFloat(parts[0], fallback.x), ParseFloat(parts[1], fallback.y), ParseFloat(parts[2], fallback.z), ParseFloat(parts[3], fallback.w));
            return fallback;
        }

        static float F(Dictionary<string, object> d, string key, float fallback)
        {
            object v;
            if (d.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); } catch { }
            }
            return fallback;
        }

        static float ParseFloat(string s, float fallback)
        {
            float value;
            return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            value = value.Trim();
            if (!value.StartsWith("#") && (value.Length == 6 || value.Length == 8)) value = "#" + value;
            Color color;
            if (ColorUtility.TryParseHtmlString(value, out color)) return color;
            var parts = value.Split(',');
            if (parts.Length >= 3)
            {
                float r = ParseFloat(parts[0], fallback.r);
                float g = ParseFloat(parts[1], fallback.g);
                float b = ParseFloat(parts[2], fallback.b);
                float a = parts.Length > 3 ? ParseFloat(parts[3], 1f) : 1f;
                if (r > 1f || g > 1f || b > 1f) { r /= 255f; g /= 255f; b /= 255f; }
                return new Color(r, g, b, a);
            }
            return fallback;
        }

        static Dictionary<string, object> Vec(Vector3 v) { return D("x", v.x, "y", v.y, "z", v.z); }
        static Dictionary<string, object> Vec2(Vector2 v) { return D("x", v.x, "y", v.y); }

        static BuildTarget ParseBuildTarget(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "win64":
                case "windows":
                case "win":
                case "standalonewindows64":
                    return BuildTarget.StandaloneWindows64;
                case "win32":
                case "standalonewindows":
                    return BuildTarget.StandaloneWindows;
                case "linux":
                case "linux64":
                case "standalonelinux64":
                    return BuildTarget.StandaloneLinux64;
                case "mac":
                case "osx":
                case "standaloneosx":
                    return BuildTarget.StandaloneOSX;
                case "webgl":
                    return BuildTarget.WebGL;
                case "android":
                    return BuildTarget.Android;
                case "ios":
                    return BuildTarget.iOS;
                default:
                    throw new Exception("Unknown build target '" + value + "' (Win64, Win32, Linux64, OSX, WebGL, Android, iOS)");
            }
        }

        static string NormalizeBuildOutput(string output, BuildTarget target)
        {
            if (string.IsNullOrEmpty(output))
                output = "Builds/" + target;
            if (!Path.IsPathRooted(output)) output = Path.Combine(Directory.GetCurrentDirectory(), output);
            output = Path.GetFullPath(output);
            bool standalone = target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows
                || target == BuildTarget.StandaloneLinux64 || target == BuildTarget.StandaloneOSX;
            if (standalone)
            {
                string extension = Path.GetExtension(output);
                bool looksLikeFile = extension == ".exe" || extension == ".x86_64" || extension == ".app";
                if (!looksLikeFile)
                {
                    string product = PlayerSettings.productName;
                    if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
                        output = Path.Combine(output, product + ".exe");
                    else if (target == BuildTarget.StandaloneLinux64)
                        output = Path.Combine(output, product + ".x86_64");
                    else
                        output = Path.Combine(output, product + ".app");
                }
            }
            return output;
        }
    }

    // Minimal JSON reader/writer (no external dependencies).
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            int index = 0;
            var value = ParseValue(json, ref index);
            SkipWhitespace(json, ref index);
            if (index < json.Length) throw new Exception("Unexpected trailing JSON characters at " + index);
            return value;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new Exception("Unexpected end of JSON");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>();
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new Exception("Expected ':' at " + i);
                i++;
                result[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new Exception("Unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return result; }
                throw new Exception("Expected ',' or '}' at " + i);
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new Exception("Unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return result; }
                throw new Exception("Expected ',' or ']' at " + i);
            }
        }

        static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new Exception("Expected string at " + i);
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char esc = s[i++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new Exception("Bad unicode escape");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(esc); break;
                }
            }
            throw new Exception("Unterminated string");
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            double value;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new Exception("Bad number at " + start);
            return value;
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static void Expect(string s, ref int i, string token)
        {
            if (i + token.Length > s.Length || s.Substring(i, token.Length) != token)
                throw new Exception("Expected '" + token + "' at " + i);
            i += token.Length;
        }

        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            if (value is string) { WriteString(sb, (string)value); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }
            if (value is IDictionary)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in (IDictionary)value)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                    sb.Append(':');
                    Write(sb, entry.Value);
                }
                sb.Append('}');
                return;
            }
            if (value is IEnumerable)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in (IEnumerable)value)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, item);
                }
                sb.Append(']');
                return;
            }
            if (value is float) { sb.Append(((float)value).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (value is double) { sb.Append(((double)value).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (value is decimal) { sb.Append(((decimal)value).ToString(CultureInfo.InvariantCulture)); return; }
            if (value is IConvertible) { sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return; }
            WriteString(sb, value.ToString());
        }

        static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
