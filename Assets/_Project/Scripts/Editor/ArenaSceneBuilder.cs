using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Maps;
using Caos.Player;
using Caos.UI;

namespace Caos.EditorTools
{
    /// <summary>
    /// Construye "Arena Caos": dos pisos, armas que se apuntan con el cursor,
    /// oleadas, mejoras por bajas y jefe final con martillo.
    /// Menú: Caos → Create Arena Caos.
    /// </summary>
    public static class ArenaSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/ArenaCaos.unity";
        const string InputActionsPath = "Assets/_Project/InputSystem_Actions.inputactions";

        [MenuItem("Caos/Create Arena Caos")]
        public static void CreateArenaScene()
        {
            if (!EditorUtility.DisplayDialog(
                    "Caos",
                    "Se creará la arena con dos pisos. La escena abierta se reemplazará.",
                    "Crear", "Cancelar"))
            {
                return;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            CreateArenaSceneInternal();
        }

        /// <summary>Entrada por lotes, sin diálogos.</summary>
        public static void CreateArenaSceneInternal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Caos] Arena Caos creada en {ScenePath}.");
        }

        static void Build()
        {
            SandboxSceneBuilder.SetupLighting();

            var root = new GameObject("ArenaCaos");
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.position = new Vector3(0f, 0.2f, -13f);

            BuildFloors(root.transform);
            BuildCover(root.transform);
            BuildWeapons(root.transform);
            BuildPlayer(root.transform, spawn.transform);
        }

        // ---------------------------------------------------------- helpers

        static Material Mat(string name) => SandboxSceneBuilder.GetMaterial(name);

        static GameObject Box(string name, Vector3 position, Vector3 scale, string material,
            Transform parent, bool trigger = false)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.transform.SetParent(parent, true);
            box.GetComponent<Renderer>().sharedMaterial = Mat(material);
            if (trigger) box.GetComponent<Collider>().isTrigger = true;
            return box;
        }

        static GameObject Cylinder(string name, Vector3 position, Vector3 scale, string material,
            Transform parent)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.position = position;
            cylinder.transform.localScale = scale;
            cylinder.transform.SetParent(parent, true);
            cylinder.GetComponent<Renderer>().sharedMaterial = Mat(material);
            return cylinder;
        }

        // ------------------------------------------------------------ pisos

        static void BuildFloors(Transform parent)
        {
            Box("GroundFloor", new Vector3(0f, -0.3f, 0f),
                new Vector3(34f, 0.6f, 34f), "MenuFloor", parent);

            // El piso alto se parte dejando un hueco central para la escalera.
            // Así el jugador no choca con la cara inferior de la plataforma.
            Box("UpperFloor_W", new Vector3(-6f, 4f, 0.5f),
                new Vector3(6f, 0.5f, 11f), "Platform", parent);
            Box("UpperFloor_E", new Vector3(6f, 4f, 0.5f),
                new Vector3(6f, 0.5f, 11f), "Platform", parent);
            Box("UpperFloor_S", new Vector3(0f, 4f, 9.5f),
                new Vector3(18f, 0.5f, 7f), "Platform", parent);

            // Escalera de 16 peldaños: 0.28 m de subida, por debajo del stepOffset.
            const int steps = 16;
            const float stepDepth = 0.92f;
            const float stepRise = 0.28f;
            for (int i = 0; i < steps; i++)
            {
                float top = stepRise * (i + 1);
                float z = -7.4f + i * stepDepth;
                Box("Stair_" + i.ToString("00"), new Vector3(0f, top * 0.5f, z),
                    new Vector3(4.6f, top, stepDepth), "Ramp", parent);
            }

            // Barandillas: partidas delante para no tapar la escalera.
            Box("Rail_N_L", new Vector3(-6f, 5.05f, -5f),
                new Vector3(6f, 0.8f, 0.28f), "Neon", parent);
            Box("Rail_N_R", new Vector3(6f, 5.05f, -5f),
                new Vector3(6f, 0.8f, 0.28f), "Neon", parent);
            Box("Rail_S", new Vector3(0f, 5.05f, 13f),
                new Vector3(18f, 0.8f, 0.28f), "Neon", parent);
            Box("Rail_W", new Vector3(-9f, 5.05f, 4f),
                new Vector3(0.28f, 0.8f, 18f), "Neon", parent);
            Box("Rail_E", new Vector3(9f, 5.05f, 4f),
                new Vector3(0.28f, 0.8f, 18f), "Neon", parent);
            Box("StairRail_L", new Vector3(-3.1f, 5.05f, 0.5f),
                new Vector3(0.22f, 0.8f, 11f), "Neon", parent);
            Box("StairRail_R", new Vector3(3.1f, 5.05f, 0.5f),
                new Vector3(0.22f, 0.8f, 11f), "Neon", parent);

            // Puntales y muros perimetrales del piso bajo.
            Box("Wall_N", new Vector3(0f, 1.6f, -17f),
                new Vector3(34f, 3.2f, 0.5f), "Wall", parent);
            Box("Wall_S", new Vector3(0f, 1.6f, 17f),
                new Vector3(34f, 3.2f, 0.5f), "Wall", parent);
            Box("Wall_W", new Vector3(-17f, 1.6f, 0f),
                new Vector3(0.5f, 3.2f, 34f), "Wall", parent);
            Box("Wall_E", new Vector3(17f, 1.6f, 0f),
                new Vector3(0.5f, 3.2f, 34f), "Wall", parent);

            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -7.5f : 7.5f;
                float z = i < 2 ? -1f : 9f;
                Box("Pillar", new Vector3(x, 2f, z),
                    new Vector3(0.5f, 4f, 0.5f), "Pedestal", parent);
            }

            // Franjas de "lava" decorativa para que se note el peligro del suelo.
            Box("LavaStrip_L", new Vector3(-13f, 0.04f, 0f),
                new Vector3(2f, 0.08f, 12f), "Lava", parent);
            Box("LavaStrip_R", new Vector3(13f, 0.04f, 3f),
                new Vector3(2f, 0.08f, 12f), "Lava", parent);
        }

        static void BuildCover(Transform parent)
        {
            Box("Cover_A", new Vector3(-8f, 0.7f, -3f),
                new Vector3(2.4f, 1.4f, 2.4f), "Crate", parent);
            Box("Cover_B", new Vector3(8f, 0.7f, -2f),
                new Vector3(2.4f, 1.4f, 2.4f), "Crate", parent);
            Box("Cover_C", new Vector3(-7f, 0.7f, 11f),
                new Vector3(2.4f, 1.4f, 2.4f), "Crate", parent);
            Box("Cover_D", new Vector3(6f, 0.7f, 12f),
                new Vector3(2.4f, 1.4f, 2.4f), "Crate", parent);

            Box("UpperCover_A", new Vector3(-4.5f, 4.9f, 7f),
                new Vector3(1.8f, 1.3f, 1.8f), "Candy", parent);
            Box("UpperCover_B", new Vector3(4.5f, 4.9f, -1f),
                new Vector3(1.8f, 1.3f, 1.8f), "Candy", parent);
        }

        // ----------------------------------------------------------- armas

        static void BuildWeapons(Transform parent)
        {
            WeaponPedestal("PistolaInicio", new Vector3(0f, 1f, -9f),
                ArenaWeaponKind.Pistola, "Lemon", parent);
            WeaponPedestal("EscopetaPisoAlto", new Vector3(-6f, 5f, 2f),
                ArenaWeaponKind.Escopeta, "Lava", parent);
            WeaponPedestal("RiflePisoAlto", new Vector3(6f, 5f, 10f),
                ArenaWeaponKind.Rifle, "Neon", parent);
            WeaponPedestal("PistolaRespaldo", new Vector3(11f, 1f, 7f),
                ArenaWeaponKind.Pistola, "Lemon", parent);
        }

        static void WeaponPedestal(string name, Vector3 position, ArenaWeaponKind kind,
            string material, Transform parent)
        {
            Cylinder(name + "Base", position + Vector3.down * 0.7f,
                new Vector3(1.2f, 0.3f, 1.2f), "Pedestal", parent);

            GameObject weapon = Box(name, position, new Vector3(0.9f, 0.28f, 0.3f),
                material, parent, true);
            weapon.AddComponent<ArenaWeaponPickup>().Configure(kind);
        }

        // ---------------------------------------------------------- jugador

        static void BuildPlayer(Transform parent, Transform spawn)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.SetParent(parent, false);
            player.transform.position = spawn.position + Vector3.up * 1.2f;

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
            health.Configure(100f, true);

            // El orden importa: PlayerController exige PlayerInputReader.
            player.AddComponent<PlayerController>().SetSpawnPoint(spawn);
            player.AddComponent<CharacterCustomization>();
            player.AddComponent<ArenaAimController>();

            var cameraObject = new GameObject("PlayerCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.15f;
            cameraObject.AddComponent<AudioListener>();

            PlayerCamera follow = cameraObject.AddComponent<PlayerCamera>();
            follow.SetTarget(player.transform);
            var serialized = new SerializedObject(follow);
            serialized.FindProperty("distance").floatValue = 18f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("ArenaDirector").AddComponent<ArenaDirector>();
            new GameObject("ArenaHud").AddComponent<ArenaHud>();
            new GameObject("PauseMenu").AddComponent<CaosPauseMenu>();
            new GameObject("CustomizationScreen").AddComponent<CustomizationScreen>();
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
