using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Factory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests
{
    public class FactoryPlayModeTests
    {
        [UnityTest]
        public IEnumerator FactoryScene_PlaysProductionLoop()
        {
            var errors = new List<string>();
            Application.LogCallback handler = delegate (string message, string stack, LogType type)
            {
                if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                    errors.Add(type + ": " + message + (string.IsNullOrEmpty(stack) ? "" : "\n" + stack));
            };
            Application.logMessageReceived += handler;

            Texture2D texture = null;
            try
            {
                Assert.Greater(SceneManager.sceneCountInBuildSettings, 0, "No scenes in Build Settings.");
                string path = SceneUtility.GetScenePathByBuildIndex(0);
                string sceneName = Path.GetFileNameWithoutExtension(path);

                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                for (int i = 0; i < 30; i++) yield return null;

#if UNITY_2023_1_OR_NEWER
                var game = Object.FindAnyObjectByType<FactoryGame>();
#else
                var game = Object.FindObjectOfType<FactoryGame>();
#endif
                Assert.IsNotNull(game, "FactoryGame bootstrap is missing in " + sceneName);
                Assert.IsNotNull(game.Sim, "FactorySim was not created");
                Assert.AreEqual(ProductInfo.Count, game.Sim.Lines.Count, "Expected one line per product");
                Assert.IsTrue(game.Sim.Lines[0].Built, "The first line must be running");
                Assert.AreEqual(2, game.Sim.Generators.Count, "Expected 2 generators");
                Assert.IsTrue(game.Sim.EnergyOk, "Machines draw more energy than generators provide");

#if UNITY_2023_1_OR_NEWER
                var canvas = Object.FindAnyObjectByType<Canvas>();
#else
                var canvas = Object.FindObjectOfType<Canvas>();
#endif
                Assert.IsNotNull(canvas, "Runtime HUD canvas was not created");

#if UNITY_2023_1_OR_NEWER
                var camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
#else
                var camera = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
#endif
                Assert.IsNotNull(camera, "The scene has no camera.");

                yield return new WaitForSeconds(2f);
                Assert.Greater(game.Sim.TotalBought + game.Sim.TotalProduced + game.Sim.TotalSold, 0,
                    "Production chain did not run during the shift");

                string dir = Path.Combine(Directory.GetCurrentDirectory(), "TestArtifacts", "playmode");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "playmode-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                if (File.Exists(file)) File.Delete(file);

                ScreenCapture.CaptureScreenshot(file);
                float deadline = Time.realtimeSinceStartup + 10f;
                while (!File.Exists(file) && Time.realtimeSinceStartup < deadline) yield return null;

                if (!File.Exists(file))
                {
                    var rt = new RenderTexture(1280, 720, 24);
                    var prev = camera.targetTexture;
                    var prevActive = RenderTexture.active;
                    camera.targetTexture = rt;
                    camera.Render();
                    RenderTexture.active = rt;
                    var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    shot.Apply();
                    camera.targetTexture = prev;
                    RenderTexture.active = prevActive;
                    File.WriteAllBytes(file, shot.EncodeToPNG());
                    Object.DestroyImmediate(shot);
                    Object.DestroyImmediate(rt);
                }

                Assert.IsTrue(File.Exists(file), "ScreenCapture did not produce a screenshot (run with graphics enabled).");
                Assert.Greater(new FileInfo(file).Length, 1000, "Screenshot is suspiciously small.");

                texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(file));
                Assert.Greater(texture.width, 0);
                var pixels = texture.GetPixels32();
                var first = pixels[0];
                bool uniform = true;
                for (int i = 1; i < pixels.Length; i++)
                {
                    if (pixels[i].r != first.r || pixels[i].g != first.g || pixels[i].b != first.b)
                    {
                        uniform = false;
                        break;
                    }
                }
                Assert.IsFalse(uniform, "Rendered frame is a single flat color - camera or lighting is broken. See " + file);
                Assert.IsEmpty(errors, "Unity reported errors while playing:\n" + string.Join("\n", errors));

                File.WriteAllText(Path.Combine(dir, "playmode-last.txt"),
                    "passed\n" +
                    "money " + game.Sim.Money.ToString("0.0") + "\n" +
                    "day " + game.Sim.Day + "\n" +
                    "bought " + game.Sim.TotalBought + "\n" +
                    "produced " + game.Sim.TotalProduced + "\n" +
                    "sold " + game.Sim.TotalSold + "\n" +
                    "energy " + game.Sim.EnergyUsed + " / " + game.Sim.EnergyCapacity + "\n" +
                    "materials " + game.Sim.TotalMaterials + "\n" +
                    "products " + game.Sim.TotalProducts + "\n" +
                    "screenshot " + file);
            }
            finally
            {
                Application.logMessageReceived -= handler;
                if (texture != null) Object.Destroy(texture);
            }
        }
    }
}
