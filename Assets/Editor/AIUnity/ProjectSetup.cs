// Creates a ready-to-use starter scene for the OpenCode unity-game-dev skill.
// Installed to <project>/Assets/Editor/AIUnity/ProjectSetup.cs and invoked in batch mode:
//   Unity.exe -batchmode -quit -projectPath <project> -executeMethod AIUnityTools.ProjectSetup.Setup -logFile -

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIUnityTools
{
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("AI/Create Starter Scene")]
        public static void CreateStarterScene()
        {
            Setup();
        }

        public static void Setup()
        {
            try
            {
                string projectName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
                PlayerSettings.productName = projectName;
                PlayerSettings.companyName = "OpenCode AI";
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.runInBackground = true;
                PlayerSettings.resizableWindow = true;

                EnsureFolder("Assets/Scenes");
                EnsureFolder("Assets/Scripts");
                EnsureFolder("Assets/Materials");
                EnsureFolder("Assets/Editor/AIUnity");

                WriteRuntimeAsmdef();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                cameraGo.transform.position = new Vector3(0f, 4.2f, -9f);
                cameraGo.transform.LookAt(new Vector3(0f, 0.8f, 0f));
                var camera = cameraGo.AddComponent<Camera>();
                camera.fieldOfView = 55f;
                camera.clearFlags = CameraClearFlags.Skybox;
                cameraGo.AddComponent<AudioListener>();

                var lightGo = new GameObject("Directional Light");
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.9f);
                light.intensity = 1.15f;
                light.shadows = LightShadows.Soft;

                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = new Vector3(4f, 1f, 4f);
                ground.GetComponent<Renderer>().sharedMaterial = MaterialAsset("Ground", new Color(0.28f, 0.33f, 0.3f));

                var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player";
                player.transform.position = new Vector3(0f, 1.1f, 0f);
                player.GetComponent<Renderer>().sharedMaterial = MaterialAsset("Player", new Color(0.2f, 0.55f, 0.95f));

                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * 2f / 5f;
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Prop_" + (i + 1);
                    cube.transform.position = new Vector3(Mathf.Cos(angle) * 4f, 0.5f, Mathf.Sin(angle) * 4f);
                    cube.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                    cube.GetComponent<Renderer>().sharedMaterial = MaterialAsset("Prop_" + (i + 1), Color.HSVToRGB(i / 5f, 0.55f, 0.9f));
                }

                var boot = new GameObject("GameBootstrap");
                var probeType = Type.GetType("Game.GameProbe, Game.Runtime");
                if (probeType != null) boot.AddComponent(probeType);

                EnsureFolder("Assets/Scenes");
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("AI_SETUP_OK scene=" + ScenePath + " project=" + projectName);
            }
            catch (Exception e)
            {
                Debug.LogError("AI_SETUP_FAILED " + e);
                throw;
            }
        }

        static void WriteRuntimeAsmdef()
        {
            string path = "Assets/Scripts/Game.Runtime.asmdef";
            if (File.Exists(path)) return;
            File.WriteAllText(path,
                "{\n" +
                "  \"name\": \"Game.Runtime\",\n" +
                "  \"rootNamespace\": \"Game\",\n" +
                "  \"references\": [],\n" +
                "  \"includePlatforms\": [],\n" +
                "  \"excludePlatforms\": [],\n" +
                "  \"allowUnsafeCode\": false,\n" +
                "  \"overrideReferences\": false,\n" +
                "  \"precompiledReferences\": [],\n" +
                "  \"autoReferenced\": true,\n" +
                "  \"defineConstraints\": [],\n" +
                "  \"versionDefines\": [],\n" +
                "  \"noEngineReferences\": false\n" +
                "}\n");
        }

        static Material MaterialAsset(string name, Color color)
        {
            EnsureFolder("Assets/Materials");
            string path = "Assets/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindLitShader());
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Shader FindLitShader()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                if (pipeline.GetType().Name.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
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
            return fallback != null ? fallback : Shader.Find("Sprites/Default");
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
