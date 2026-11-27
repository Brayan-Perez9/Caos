using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Caos.Net;
using Caos.UI;

namespace Caos.EditorTools
{
    /// <summary>Crea la escena Lobby (sala privada con código CAOS-XXXXXXXXXX).</summary>
    public static class LobbySceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Lobby.unity";

        [MenuItem("Caos/Create Lobby Scene")]
        public static void CreateLobbyScene()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            CreateLobbySceneInternal();
        }

        public static void CreateLobbySceneInternal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SandboxSceneBuilder.SetupLighting();

            var cameraObject = new GameObject("LobbyCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.11f);
            cameraObject.transform.position = new Vector3(0f, 2.7f, -8.5f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.25f, 0.6f));
            cameraObject.AddComponent<AudioListener>();
            SandboxSceneBuilder.EnablePostProcessing(cameraObject);

            new GameObject("CaosNetPeer").AddComponent<CaosNetPeer>();
            new GameObject("NetworkPlayers").AddComponent<CaosNetPlayers>().ConfigureLobby();
            new GameObject("CaosLobby").AddComponent<CaosLobby>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Caos] Lobby creado en {ScenePath}.");
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(entry => entry.path == ScenePath)) return;

            int menu = scenes.FindIndex(entry => entry.path == MenuSceneBuilder.ScenePath);
            scenes.Insert(menu >= 0 ? menu + 1 : 0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
