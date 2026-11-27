using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Caos.UI;

namespace Caos.EditorTools
{
    /// <summary>
    /// Crea la escena Loading como módulo independiente. Solo tiene una
    /// cámara y CaosLoadingScreen: ningún otro componente puede dibujar encima.
    /// </summary>
    public static class LoadingSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Loading.unity";

        [MenuItem("Caos/Create Loading Scene")]
        public static void CreateLoadingScene()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            CreateLoadingSceneInternal();
        }

        public static void CreateLoadingSceneInternal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("LoadingCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
            camera.allowHDR = false;
            cameraObject.AddComponent<AudioListener>();

            new GameObject("CaosLoadingScreen").AddComponent<CaosLoadingScreen>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Caos] Pantalla de carga creada en {ScenePath}.");
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(entry => entry.path == ScenePath)) return;

            int menu = scenes.FindIndex(entry => entry.path == MenuSceneBuilder.ScenePath);
            scenes.Insert(menu >= 0 ? menu + 1 : 0,
                new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
