using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Caos.UI;

namespace Caos.EditorTools
{
    /// <summary>
    /// Crea el lobby principal como escena propia: personaje a la izquierda
    /// y tarjetas de MAZMORRA, ARENAS y PERSONALIZAR PERSONAJE a la derecha.
    /// El personaje se renderiza a textura para que CaosLobbyScreen lo dibuje
    /// en el mismo orden que el resto de elementos de la interfaz.
    /// </summary>
    public static class FrontendLobbySceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/FrontendLobby.unity";

        [MenuItem("Caos/Create Frontend Lobby Scene")]
        public static void CreateFrontendLobbyScene()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            CreateFrontendLobbySceneInternal();
        }

        public static void CreateFrontendLobbySceneInternal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SandboxSceneBuilder.SetupLighting();

            var root = new GameObject("FrontendLobby");
            MenuSceneBuilder.BuildStage(root.transform);
            MenuSceneBuilder.CreatePreviewCharacter(root.transform);

            var cameraObject = new GameObject("PortraitCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(-1.7f, 1.5f, -4.6f);
            cameraObject.transform.rotation = Quaternion.identity;

            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 0f);
            camera.allowHDR = false;
            cameraObject.AddComponent<AudioListener>();

            new GameObject("CaosLobbyScreen").AddComponent<CaosLobbyScreen>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Caos] Lobby principal creado en {ScenePath}.");
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
