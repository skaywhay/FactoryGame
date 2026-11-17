using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    public static class FactorySetup
    {
        public const string ScenePath = "Assets/Scenes/Factory.unity";

        [MenuItem("Factory/Setup Scene")]
        public static void Setup()
        {
            PlayerSettings.productName = "Factory Prototype";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("FactoryGame");
            root.AddComponent<Factory.FactoryGame>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FACTORY_SETUP_OK " + ScenePath);
        }
    }
}
