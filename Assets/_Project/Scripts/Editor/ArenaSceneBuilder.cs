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
    /// Construye "Arena Caos": mapa grande de dos pisos con escalera, armas
    /// elementales por color, jefes y HUD. Menú: Caos → Create Arena Caos.
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
            spawn.transform.position = new Vector3(0f, -0.25f, -18f);

            BuildFloors(root.transform);
            CaosAmbience.Ensure(root.transform, 20261126, AmbientBiome.Neon,
                new Vector2(21f, 21f), 0f, false,
                SandboxSceneBuilder.GetAmbientMaterial(),
                SandboxSceneBuilder.GetBlueGrassMaterial());
            BuildCover(root.transform);
            BuildWeapons(root.transform);
            BuildPlayer(root.transform, spawn.transform);
        }

        // ---------------------------------------------------------- helpers

        static Material Mat(string name) => SandboxSceneBuilder.GetMaterial(name);

        static GameObject Box(string name, Vector3 position, Vector3 scale, string material,
            Transform parent, bool trigger = false, bool rounded = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.transform.SetParent(parent, true);
            box.GetComponent<Renderer>().sharedMaterial = Mat(material);
            if (trigger) box.GetComponent<Collider>().isTrigger = true;
            if (rounded) SandboxSceneBuilder.RoundBox(box, scale);
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
            SandboxSceneBuilder.RoundCylinder(cylinder, scale);
            return cylinder;
        }

        /// <summary>Franja guía: solo visual, no estorba al caminar ni a la IA.</summary>
        static GameObject Trail(string name, Vector3 position, Vector3 scale, string material,
            Transform parent)
        {
            var trail = Box(name, position, scale, material, parent, false, false);
            UnityEngine.Object.DestroyImmediate(trail.GetComponent<Collider>());
            return trail;
        }

        /// <summary>Hito visible desde lejos: bombilla de color y luz muy suave.</summary>
        static void Beacon(string name, Vector3 position, Color color, string bulbMaterial,
            Transform parent, float range = 8f)
        {
            Cylinder(name + "_Pole", position + Vector3.up * 1.5f,
                new Vector3(0.16f, 1.5f, 0.16f), "Stylized_Mint", parent);
            GlowBulb(name + "_Bulb", position + Vector3.up * 2.85f,
                bulbMaterial, parent, 0.42f);

            var lamp = new GameObject(name + "_Light");
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = position + Vector3.up * 2.8f;
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 1f;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>Bombilla decorativa sin collider: da color sin lavar la imagen.</summary>
        static void GlowBulb(string name, Vector3 position, string materialName,
            Transform parent, float size)
        {
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = name;
            bulb.transform.SetParent(parent, false);
            bulb.transform.position = position;
            bulb.transform.localScale = Vector3.one * size;
            bulb.GetComponent<Renderer>().sharedMaterial = Mat(materialName);
            var collider = bulb.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }

        // ------------------------------------------------------------ pisos

        static void BuildFloors(Transform parent)
        {
            Box("GroundFloor", new Vector3(0f, -0.3f, 0f),
                new Vector3(48f, 0.6f, 48f), "Grass", parent);

            // Piso alto en forma de U: deja el hueco central para la escalera.
            Box("UpperFloor_W", new Vector3(-9f, 4f, -2f),
                new Vector3(8f, 0.5f, 20f), "Stylized_Platform", parent);
            Box("UpperFloor_E", new Vector3(9f, 4f, -2f),
                new Vector3(8f, 0.5f, 20f), "Stylized_Platform", parent);
            Box("UpperFloor_S", new Vector3(0f, 4f, 13f),
                new Vector3(26f, 0.5f, 8f), "Stylized_Platform", parent);

            // Meseta y puentes: unen el último peldaño con el piso alto para
            // que los enemigos no se queden parados en el borde.
            Box("StairLanding", new Vector3(0f, 4f, 8.6f),
                new Vector3(6f, 0.5f, 3.6f), "Dirt", parent);
            Box("UpperBridge_W", new Vector3(-9f, 4f, 8.5f),
                new Vector3(8f, 0.5f, 2f), "Stylized_Platform", parent);
            Box("UpperBridge_E", new Vector3(9f, 4f, 8.5f),
                new Vector3(8f, 0.5f, 2f), "Stylized_Platform", parent);

            // 15 peldaños de 0.28 m: por debajo del stepOffset del jugador.
            const int steps = 15;
            const float stepDepth = 1.3f;
            const float stepRise = 0.28f;
            for (int i = 0; i < steps; i++)
            {
                float top = stepRise * (i + 1);
                float z = -11f + i * stepDepth;
                Box("Stair_" + i.ToString("00"), new Vector3(0f, top * 0.5f, z),
                    new Vector3(6f, top, stepDepth), "Dirt", parent);
            }

            // Barandillas del piso alto y de la escalera.
            Box("Rail_N_L", new Vector3(-9f, 5.05f, -12f),
                new Vector3(8f, 0.8f, 0.28f), "Stylized_Cyan", parent);
            Box("Rail_N_R", new Vector3(9f, 5.05f, -12f),
                new Vector3(8f, 0.8f, 0.28f), "Stylized_Cyan", parent);
            Box("Rail_S", new Vector3(0f, 5.05f, 17f),
                new Vector3(26f, 0.8f, 0.28f), "Stylized_Cyan", parent);
            Box("Rail_W", new Vector3(-13f, 5.05f, 2.5f),
                new Vector3(0.28f, 0.8f, 29f), "Stylized_Cyan", parent);
            Box("Rail_E", new Vector3(13f, 5.05f, 2.5f),
                new Vector3(0.28f, 0.8f, 29f), "Stylized_Cyan", parent);
            Box("StairRail_L", new Vector3(-3.4f, 5.05f, -1.9f),
                new Vector3(0.22f, 0.8f, 18.2f), "Stylized_Cyan", parent);
            Box("StairRail_R", new Vector3(3.4f, 5.05f, -1.9f),
                new Vector3(0.22f, 0.8f, 18.2f), "Stylized_Cyan", parent);

            // Guiado sin paredes: el suelo apunta a la escalera y al jefe.
            Trail("Guide_SpawnToStair", new Vector3(0f, 0.02f, -15f),
                new Vector3(0.5f, 0.03f, 7f), "Neon", parent);
            Trail("Guide_StairToBoss", new Vector3(0f, 4.28f, 12f),
                new Vector3(0.5f, 0.03f, 8f), "Neon", parent);
            Trail("Guide_LowerWest", new Vector3(-5.5f, 0.02f, -6f),
                new Vector3(0.28f, 0.03f, 11f), "Lemon", parent);
            Trail("Guide_LowerEast", new Vector3(5.5f, 0.02f, -6f),
                new Vector3(0.28f, 0.03f, 11f), "Candy", parent);

            // Landmarks: se ven desde el spawn y desde el piso alto.
            Beacon("Beacon_Spawn", new Vector3(0f, 0f, -13f),
                new Color(0.25f, 0.85f, 1f), "Stylized_Cyan", parent, 8f);
            Beacon("Beacon_Landing", new Vector3(0f, 4.25f, 9.4f),
                new Color(1f, 0.35f, 0.7f), "Stylized_Violet", parent, 9f);
            Beacon("Beacon_Boss", new Vector3(0f, 0f, 20f),
                new Color(1f, 0.45f, 0.2f), "Stylized_Coral", parent, 10f);

            // Muros perimetrales del piso bajo.
            Box("Wall_N", new Vector3(0f, 1.6f, -24f),
                new Vector3(48f, 3.2f, 0.5f), "Wall", parent);
            Box("Wall_S", new Vector3(0f, 1.6f, 24f),
                new Vector3(48f, 3.2f, 0.5f), "Wall", parent);
            Box("Wall_W", new Vector3(-24f, 1.6f, 0f),
                new Vector3(0.5f, 3.2f, 48f), "Wall", parent);
            Box("Wall_E", new Vector3(24f, 1.6f, 0f),
                new Vector3(0.5f, 3.2f, 48f), "Wall", parent);

            // Columnas: obstáculos sólidos para que los enemigos esquiven.
            Vector3[] pillars =
            {
                new Vector3(-16f, 2f, -6f), new Vector3(16f, 2f, -6f),
                new Vector3(-16f, 2f, 10f), new Vector3(16f, 2f, 10f),
                new Vector3(-8f, 2f, 18f), new Vector3(8f, 2f, 18f),
            };
            for (int i = 0; i < pillars.Length; i++)
                Box("Pillar_" + i, pillars[i], new Vector3(0.6f, 4f, 0.6f),
                    "Stylized_Violet", parent);

            // Franjas decorativas de "lava".
            Box("LavaStrip_L", new Vector3(-21f, 0.04f, 0f),
                new Vector3(2f, 0.08f, 20f), "Lava", parent);
            Box("LavaStrip_R", new Vector3(21f, 0.04f, 2f),
                new Vector3(2f, 0.08f, 20f), "Lava", parent);
        }

        static void BuildCover(Transform parent)
        {
            Vector3[] crates =
            {
                new Vector3(-12f, 0.7f, 2f), new Vector3(12f, 0.7f, 2f),
                new Vector3(-10f, 0.7f, 14f), new Vector3(10f, 0.7f, 14f),
                new Vector3(-19f, 0.7f, -13f), new Vector3(19f, 0.7f, -13f),
                new Vector3(-4f, 0.7f, 20f), new Vector3(4f, 0.7f, 20f),
            };
            for (int i = 0; i < crates.Length; i++)
                Box("Cover_" + i, crates[i], new Vector3(2.4f, 1.4f, 2.4f),
                    "Stylized_Coral", parent);

            Box("UpperCover_A", new Vector3(-9f, 4.9f, 3f),
                new Vector3(2f, 1.3f, 2f), "Stylized_Lemon", parent);
            Box("UpperCover_B", new Vector3(9f, 4.9f, -5f),
                new Vector3(2f, 1.3f, 2f), "Stylized_Lemon", parent);
            Box("UpperCover_C", new Vector3(0f, 4.9f, 11f),
                new Vector3(2.2f, 1.3f, 2.2f), "Stylized_Lemon", parent);
        }

        // ----------------------------------------------------------- armas

        static void BuildWeapons(Transform parent)
        {
            WeaponPedestal("PistolaInicio", new Vector3(0f, 1f, -14f),
                ArenaWeaponKind.Pistola, ArenaElement.Normal, "Lemon", parent);
            WeaponPedestal("EscopetaFuego", new Vector3(-18f, 1f, -8f),
                ArenaWeaponKind.Escopeta, ArenaElement.Fuego, "Lava", parent);
            WeaponPedestal("RifleAire", new Vector3(18f, 1f, -8f),
                ArenaWeaponKind.Rifle, ArenaElement.Aire, "Neon", parent);
            WeaponPedestal("PistolaHielo", new Vector3(-10f, 5.2f, 2f),
                ArenaWeaponKind.Pistola, ArenaElement.Hielo, "Platform", parent);
            WeaponPedestal("EscopetaRayo", new Vector3(10f, 5.2f, 2f),
                ArenaWeaponKind.Escopeta, ArenaElement.Rayo, "Candy", parent);
            WeaponPedestal("RifleVeneno", new Vector3(0f, 5.2f, 14f),
                ArenaWeaponKind.Rifle, ArenaElement.Veneno, "Lemon", parent);
        }

        static void WeaponPedestal(string name, Vector3 position, ArenaWeaponKind kind,
            ArenaElement element, string material, Transform parent)
        {
            Cylinder(name + "Base", position + Vector3.down * 0.7f,
                new Vector3(1.2f, 0.3f, 1.2f), "Pedestal", parent);

            GameObject weapon = Box(name, position, new Vector3(0.9f, 0.28f, 0.3f),
                material, parent, true);
            weapon.AddComponent<ArenaWeaponPickup>().Configure(kind, element);

            // Cada elemento tiene su color: bombilla + luz tenue, sin lavar el suelo.
            Vector3 glowPosition = position + Vector3.up * 0.8f;
            GlowBulb(name + "_Bulb", glowPosition, ElementMaterial(element), parent, 0.24f);

            var lamp = new GameObject(name + "_Glow");
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = glowPosition;
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ArenaElementInfo.Color(element);
            light.intensity = 0.55f;
            light.range = 4.5f;
            light.shadows = LightShadows.None;
        }

        static string ElementMaterial(ArenaElement element)
        {
            switch (element)
            {
                case ArenaElement.Fuego: return "Stylized_Coral";
                case ArenaElement.Aire: return "Stylized_Cyan";
                case ArenaElement.Hielo: return "Stylized_Platform";
                case ArenaElement.Rayo: return "Stylized_Lemon";
                case ArenaElement.Veneno: return "Stylized_Mint";
                default: return "Stylized_Lemon";
            }
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
            SandboxSceneBuilder.SetPartySky(camera);
            cameraObject.AddComponent<AudioListener>();
            SandboxSceneBuilder.EnablePostProcessing(cameraObject);

            PlayerCamera follow = cameraObject.AddComponent<PlayerCamera>();
            follow.SetTarget(player.transform);
            var serialized = new SerializedObject(follow);
            serialized.FindProperty("distance").floatValue = 20f;
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
