using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Dungeon;
using Caos.Maps;
using Caos.Player;
using Caos.UI;

namespace Caos.EditorTools
{
    /// <summary>
    /// Construye "Dungeon": una escena ligera con jugador, cámara, director,
    /// puente de combate, HUD y sincronización. Las salas y sus pasillos los
    /// genera DungeonWorld en tiempo de ejecución a partir de una semilla.
    /// Menú: Caos → Create Dungeon.
    /// </summary>
    public static class DungeonSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dungeon.unity";
        const string ConfigPath = "Assets/_Project/Resources/DungeonConfig.asset";
        const string InputActionsPath = "Assets/_Project/InputSystem_Actions.inputactions";

        [MenuItem("Caos/Create Dungeon")]
        public static void CreateDungeonScene()
        {
            if (!EditorUtility.DisplayDialog(
                    "Caos",
                    "Se creará la mazmorra. La escena abierta se reemplazará.",
                    "Crear", "Cancelar"))
            {
                return;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            CreateDungeonSceneInternal();
        }

        /// <summary>Entrada por lotes, sin diálogos: la usa BuildCaos.</summary>
        public static void CreateDungeonSceneInternal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Caos] Mazmorra creada en {ScenePath}.");
        }

        static void Build()
        {
            SandboxSceneBuilder.SetupLighting();

            var root = new GameObject("DungeonCaos");
            var spawn = new GameObject("DungeonSpawn");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.position = new Vector3(0f, 1.2f, -8f);

            PlayerController player = BuildPlayer(root.transform, spawn.transform);

            var bridge = new GameObject("DungeonCombatBridge").AddComponent<DungeonCombatBridge>();
            var director = new GameObject("DungeonDirector").AddComponent<DungeonDirector>();
            var serializedDirector = new SerializedObject(director);
            serializedDirector.FindProperty("config").objectReferenceValue = EnsureConfig();
            serializedDirector.ApplyModifiedPropertiesWithoutUndo();

            director.SetPlayer(player);

            var hud = new GameObject("DungeonHud").AddComponent<DungeonHud>();
            hud.Configure(director);
            director.SetHud(hud);

            new GameObject("DungeonNetSync").AddComponent<DungeonNetSync>();
            new GameObject("PauseMenu").AddComponent<CaosPauseMenu>();
            new GameObject("CustomizationScreen").AddComponent<CustomizationScreen>();

            bridge.transform.SetParent(root.transform, true);
            director.transform.SetParent(root.transform, true);
            hud.transform.SetParent(root.transform, true);
        }

        static PlayerController BuildPlayer(Transform parent, Transform spawn)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.SetParent(parent, false);
            player.transform.position = spawn.position;

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.08f;

            SandboxSceneBuilder.CreatePlayerVisual(player.transform);

            var input = player.AddComponent<PlayerInputReader>();
            input.SetActions(AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));

            var health = player.AddComponent<ChaosHealth>();
            health.Configure(130f, true);

            // El orden importa: PlayerController exige PlayerInputReader.
            PlayerController movement = player.AddComponent<PlayerController>();
            movement.SetSpawnPoint(spawn);
            player.AddComponent<CharacterCustomization>();
            player.AddComponent<ArenaAimController>();

            var cameraObject = new GameObject("PlayerCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 56f;
            camera.nearClipPlane = 0.15f;
            SandboxSceneBuilder.SetPartySky(camera);
            cameraObject.AddComponent<AudioListener>();
            SandboxSceneBuilder.EnablePostProcessing(cameraObject);

            PlayerCamera follow = cameraObject.AddComponent<PlayerCamera>();
            follow.SetTarget(player.transform);
            var serialized = new SerializedObject(follow);
            serialized.FindProperty("distance").floatValue = 20f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return movement;
        }

        static DungeonConfig EnsureConfig()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                AssetDatabase.CreateFolder("Assets/_Project", "Resources");

            var config = AssetDatabase.LoadAssetAtPath<DungeonConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DungeonConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.ApplyDefaults();
            EditorUtility.SetDirty(config);
            return config;
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(entry => entry.path == ScenePath)) return;

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
