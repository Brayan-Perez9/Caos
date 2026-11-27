using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Caos.UI;

namespace Caos.EditorTools
{
    /// <summary>
    /// Crea la escena Customize como módulo separado: su propio escenario, su
    /// cámara y el personaje real. No incluye lobby, carga ni HUD de juego.
    /// </summary>
    public static class CustomizeSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Customize.unity";

        [MenuItem("Caos/Create Customize Scene")]
        public static void CreateCustomizeScene()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            CreateCustomizeSceneInternal();
        }

        public static void CreateCustomizeSceneInternal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SandboxSceneBuilder.SetupLighting();

            var root = new GameObject("Customize");
            MenuSceneBuilder.BuildStage(root.transform);
            MenuSceneBuilder.CreatePreviewCharacter(root.transform);
            MenuSceneBuilder.CreateCamera(root.transform);

            new GameObject("CaosCustomizeScreen").AddComponent<CaosCustomizeScreen>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Caos] Personalización creada en {ScenePath}.");
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
