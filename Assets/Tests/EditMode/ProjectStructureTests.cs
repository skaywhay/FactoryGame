using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Editor-only sanity checks for the project structure.</summary>
    public class ProjectStructureTests
    {
        [Test]
        public void BuildSettings_HasAtLeastOneEnabledScene()
        {
            var enabled = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) enabled.Add(scene.path);

            Assert.Greater(enabled.Count, 0, "Add a scene to File > Build Settings.");
            foreach (var path in enabled)
            {
                Assert.IsTrue(File.Exists(path), "Scene file is missing: " + path);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), "Not a valid scene: " + path);
            }
        }

        [Test]
        public void Scenes_HaveNoMissingScripts()
        {
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;

                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                int missing = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                EditorSceneManager.CloseScene(scene, true);

                Assert.AreEqual(0, missing, "Missing MonoBehaviour scripts in " + entry.path);
            }
        }

        [Test]
        public void PlayerSettings_HaveProductName()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(PlayerSettings.productName));
        }
    }
}
