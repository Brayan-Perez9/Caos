using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Interactions;
using Caos.Maps;
using Caos.Player;
using Caos.UI;

namespace Caos.EditorTools
{
    /// <summary>
    /// Construye "Caos Express": un mapa solitario de partida corta con tres
    /// minijuegos, eventos aleatorios, trampas y objetivos secretos.
    /// Menú: Caos → Create Chaos Express (solo).
    /// </summary>
    public static class ChaosExpressBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/ChaosExpress.unity";
        const string InputActionsPath = "Assets/_Project/InputSystem_Actions.inputactions";

        [MenuItem("Caos/Create Chaos Express (solo)")]
        public static void CreateMap()
        {
            if (!EditorUtility.DisplayDialog(
                    "Caos",
                    "Se creará el mapa Caos Express. La escena abierta se reemplazará.",
                    "Crear", "Cancelar"))
            {
                return;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            CreateMapInternal();
        }

        /// <summary>Entrada por lotes, sin diálogos.</summary>
        public static void CreateMapInternal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Caos] Caos Express creado en {ScenePath}.");
        }

        static void Build()
        {
            SandboxSceneBuilder.SetupLighting();

            var root = new GameObject("ChaosExpress");
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.position = new Vector3(0f, 0.2f, -12f);

            BuildArena(root.transform);
            CaosAmbience.Ensure(root.transform, 20261127, AmbientBiome.Candy,
                new Vector2(13.5f, 13.5f), 0f, true,
                SandboxSceneBuilder.GetAmbientMaterial(),
                SandboxSceneBuilder.GetBlueGrassMaterial());
            BuildCargoStation(root.transform);
            BuildTargetStation(root.transform);
            BuildParcelStation(root.transform);
            BuildCenterPlaza(root.transform);
            BuildSecretDucks(root.transform);
            BuildFinish(root.transform);
            BuildFallZone(root.transform, spawn.transform);
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
            Transform parent, bool trigger = false)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.position = position;
            cylinder.transform.localScale = scale;
            cylinder.transform.SetParent(parent, true);
            cylinder.GetComponent<Renderer>().sharedMaterial = Mat(material);
            if (trigger) cylinder.GetComponent<Collider>().isTrigger = true;
            SandboxSceneBuilder.RoundCylinder(cylinder, scale);
            return cylinder;
        }

        /// <summary>Franja de guía sin colisión: marca la ruta sin cambiar la física.</summary>
        static GameObject Trail(string name, Vector3 position, Vector3 scale, string material,
            Transform parent)
        {
            var trail = Box(name, position, scale, material, parent, false, false);
            UnityEngine.Object.DestroyImmediate(trail.GetComponent<Collider>());
            return trail;
        }

        static void Beacon(string name, Vector3 position, Color color, Transform parent,
            float range = 10f)
        {
            Cylinder(name + "_Pole", position + Vector3.up * 1.4f,
                new Vector3(0.14f, 1.4f, 0.14f), "Stylized_Mint", parent);
            var lamp = new GameObject(name + "_Light");
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = position + Vector3.up * 2.6f;
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 2.2f;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        static Rigidbody Body(GameObject go, float mass = 1f)
        {
            var body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            return body;
        }

        static GameObject Pickup(string name, Vector3 position, bool secret, Transform parent)
        {
            var duck = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            duck.name = name;
            duck.transform.position = position;
            duck.transform.localScale = new Vector3(0.28f, 0.22f, 0.28f);
            duck.transform.SetParent(parent, true);
            duck.GetComponent<Renderer>().sharedMaterial = Mat("Lemon");
            duck.GetComponent<Collider>().isTrigger = true;
            var pickup = duck.AddComponent<ChaosPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("secret").boolValue = secret;
            so.ApplyModifiedPropertiesWithoutUndo();
            return duck;
        }

        static void Spinner(string name, Vector3 position, float speed, string material, Transform parent)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = position;
            var spin = pivot.AddComponent<ChaosSpinner>();
            var so = new SerializedObject(spin);
            so.FindProperty("degreesPerSecond").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();

            var bar = Box("SweeperBar", position, new Vector3(0.25f, 0.25f, 3.4f),
                material, parent, true);
            bar.transform.SetParent(pivot.transform, true);
            var barBody = bar.AddComponent<Rigidbody>();
            barBody.isKinematic = true; // el trigger en movimiento avisa al CharacterController
            barBody.useGravity = false;
            var hazard = bar.AddComponent<ChaosHazard>();
            var hazardSo = new SerializedObject(hazard);
            hazardSo.FindProperty("damage").floatValue = 14f;
            hazardSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------- mapa

        static void BuildArena(Transform parent)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
            floor.transform.SetParent(parent, true);
            floor.GetComponent<Renderer>().sharedMaterial = Mat("Grass");

            Box("Wall_N", new Vector3(0f, 1.5f, 15f), new Vector3(31f, 3f, 1f), "Neon", parent);
            Box("Wall_E", new Vector3(15f, 1.5f, 0f), new Vector3(1f, 3f, 31f), "Mint", parent);
            Box("Wall_W", new Vector3(-15f, 1.5f, 0f), new Vector3(1f, 3f, 31f), "Mint", parent);

            // Sur con hueco central: caerse es parte de la gracia.
            Box("Wall_S_Left", new Vector3(-10f, 1.5f, -15f), new Vector3(11f, 3f, 1f), "Neon", parent);
            Box("Wall_S_Right", new Vector3(10f, 1.5f, -15f), new Vector3(11f, 3f, 1f), "Neon", parent);

            // Decoración: columnas de colores en las esquinas.
            foreach (var x in new[] { -12f, 12f })
            {
                foreach (var z in new[] { -12f, 12f })
                {
                    Cylinder("Pillar", new Vector3(x, 1.6f, z), new Vector3(0.7f, 1.6f, 0.7f),
                        "Lemon", parent);
                }
            }

            // Rutas: el spawn mira al norte, así que cada estación tiene una
            // franja que conecta con la siguiente. El hueco sur queda avisado.
            Trail("Guide_Cargo", new Vector3(-8f, 0.02f, -8f),
                new Vector3(0.4f, 0.03f, 8f), "Mint", parent);
            Trail("Guide_Targets", new Vector3(8f, 0.02f, -7f),
                new Vector3(0.4f, 0.03f, 8f), "Lava", parent);
            Trail("Guide_Parcels", new Vector3(0f, 0.02f, 2f),
                new Vector3(0.4f, 0.03f, 8f), "Lemon", parent);
            Trail("Guide_Finish", new Vector3(0f, 0.02f, 8.5f),
                new Vector3(0.4f, 0.03f, 5f), "Neon", parent);
            Trail("Warning_SouthGap", new Vector3(0f, 0.02f, -13.7f),
                new Vector3(9f, 0.03f, 0.5f), "Lava", parent);

            Beacon("Beacon_Cargo", new Vector3(-8f, 0f, -10f),
                new Color(0.35f, 1f, 0.7f), parent, 9f);
            Beacon("Beacon_Targets", new Vector3(8f, 0f, -10f),
                new Color(1f, 0.4f, 0.28f), parent, 9f);
            Beacon("Beacon_Parcels", new Vector3(0f, 2.5f, 6f),
                new Color(1f, 0.85f, 0.3f), parent, 9f);
            Beacon("Beacon_Finish", new Vector3(0f, 0f, 13f),
                new Color(0.3f, 0.9f, 1f), parent, 11f);
        }

        static void BuildCargoStation(Transform parent)
        {
            // Zona de cajas sueltas para lanzar: ya no hay objetivo de camión.
            Box("Cargo_Back", new Vector3(-8f, 0.7f, -9.5f), new Vector3(6f, 1.4f, 0.4f), "Mint", parent);
            Box("Cargo_Left", new Vector3(-11f, 0.7f, -6f), new Vector3(0.4f, 1.4f, 7f), "Mint", parent);
            Box("Cargo_Right", new Vector3(-5f, 0.7f, -6f), new Vector3(0.4f, 1.4f, 7f), "Mint", parent);

            var positions = new[]
            {
                new Vector3(-9.6f, 0.5f, -4.5f),
                new Vector3(-8.0f, 1.4f, -4.5f),
                new Vector3(-6.4f, 0.5f, -4.5f),
            };
            foreach (var position in positions)
            {
                var crate = Box("CargoCrate", position, Vector3.one, "Crate", parent);
                Body(crate, 1.2f);
                crate.AddComponent<ChaosThrowable>();
            }

            Box("CargoSign", new Vector3(-8f, 2.2f, -9.3f), new Vector3(3f, 0.5f, 0.2f), "Lava", parent);
        }

        static void BuildTargetStation(Transform parent)
        {
            var positions = new[]
            {
                new Vector3(6.5f, 1.9f, -5f),
                new Vector3(8.0f, 1.9f, -6.6f),
                new Vector3(9.5f, 1.9f, -8.2f),
            };

            foreach (var position in positions)
            {
                Cylinder("TargetStand", new Vector3(position.x, 0.8f, position.z),
                    new Vector3(0.7f, 0.8f, 0.7f), "Pedestal", parent);

                var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                target.name = "ChaosTarget";
                target.transform.position = position;
                target.transform.localScale = Vector3.one * 0.7f;
                target.transform.SetParent(parent, true);
                target.GetComponent<Renderer>().sharedMaterial = Mat("Lava");
                var body = Body(target, 0.5f);
                body.useGravity = false;
                body.linearDamping = 0.6f;
                target.AddComponent<ChaosTarget>();
                target.AddComponent<ChaosThrowable>();
            }

            Box("TargetBackdrop", new Vector3(8f, 2f, -10f),
                new Vector3(7f, 3f, 0.3f), "Lemon", parent);
        }

        static void BuildParcelStation(Transform parent)
        {
            Box("ParcelPlatform", new Vector3(0f, 2.25f, 6f), new Vector3(7f, 0.5f, 7f), "Platform", parent);

            var ramp = Box("ParcelRamp", new Vector3(0f, 1.15f, 2.6f),
                new Vector3(5f, 0.4f, 4.6f), "Dirt", parent);
            ramp.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);

            CreateMovingPlatform("ParcelLift", new Vector3(-5f, 1f, 4f),
                Vector3.zero, new Vector3(0f, 3f, 0f), 1.1f, parent);
            CreateMovingPlatform("ParcelSlide", new Vector3(5f, 2.5f, 7f),
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f), 1.4f, parent);

            var parcels = new[]
            {
                new Vector3(-2f, 2.8f, 4.8f),
                new Vector3(2f, 2.8f, 4.8f),
                new Vector3(-2f, 2.8f, 7.2f),
                new Vector3(2f, 2.8f, 7.2f),
                new Vector3(0f, 2.8f, 6f),
            };
            foreach (var position in parcels)
            {
                var parcel = Box("Parcel", position, new Vector3(0.5f, 0.5f, 0.5f), "Lemon", parent);
                parcel.GetComponent<Collider>().isTrigger = true;
                parcel.AddComponent<ChaosPickup>();
            }

            Spinner("ParcelSweeper", new Vector3(0f, 2.9f, 6f), 130f, "Lava", parent);
        }

        static void BuildCenterPlaza(Transform parent)
        {
            Cylinder("BouncePad", new Vector3(0f, 0.12f, 0f),
                new Vector3(1.6f, 0.12f, 1.6f), "Lava", parent, true)
                .AddComponent<ChaosBumper>();
            Cylinder("BouncePad", new Vector3(-4f, 0.12f, 1f),
                new Vector3(1.3f, 0.12f, 1.3f), "Lava", parent, true)
                .AddComponent<ChaosBumper>();
            Cylinder("BouncePad", new Vector3(4f, 0.12f, -1f),
                new Vector3(1.3f, 0.12f, 1.3f), "Lava", parent, true)
                .AddComponent<ChaosBumper>();

            Spinner("PlazaSweeper", new Vector3(-6f, 0.9f, 2f), -110f, "Neon", parent);
            Spinner("PlazaSweeper", new Vector3(6f, 0.9f, 1f), 95f, "Neon", parent);

            // Pila de cajas sueltas: física gratis.
            for (int i = 0; i < 3; i++)
            {
                var crate = Box("LooseCrate", new Vector3(1f + i * 0.15f, 0.5f + i, 2f),
                    Vector3.one, "Crate", parent);
                Body(crate, 0.9f);
                crate.AddComponent<ChaosThrowable>();
            }
        }

        static void BuildSecretDucks(Transform parent)
        {
            Pickup("SecretDuck", new Vector3(11f, 0.3f, -10f), true, parent);
            Pickup("SecretDuck", new Vector3(-11f, 0.3f, -10f), true, parent);
            Pickup("SecretDuck", new Vector3(0f, 0.3f, 9f), true, parent);
        }

        static void BuildFinish(Transform parent)
        {
            Cylinder("FinishPodium", new Vector3(0f, 0.25f, 11f),
                new Vector3(3f, 0.25f, 3f), "Mint", parent);

            var button = Box("FinishButton", new Vector3(0f, 0.9f, 11f),
                new Vector3(1.8f, 0.8f, 1.8f), "Neon", parent, true);
            button.AddComponent<ChaosFinish>();

            Box("FinishArch_L", new Vector3(-2f, 2f, 11f), new Vector3(0.4f, 4f, 0.4f), "Lemon", parent);
            Box("FinishArch_R", new Vector3(2f, 2f, 11f), new Vector3(0.4f, 4f, 0.4f), "Lemon", parent);
            Box("FinishArch_Top", new Vector3(0f, 4f, 11f), new Vector3(4.4f, 0.5f, 0.4f), "Lemon", parent);
        }

        static void CreateMovingPlatform(string name, Vector3 position, Vector3 offsetA,
            Vector3 offsetB, float speed, Transform parent)
        {
            var platform = Box(name, position, new Vector3(2.6f, 0.4f, 2.6f), "Platform", parent);
            platform.AddComponent<MovingPlatform>().Configure(offsetA, offsetB, speed);
        }

        static void BuildFallZone(Transform parent, Transform respawn)
        {
            var zone = new GameObject("FallZone");
            zone.transform.SetParent(parent, false);
            zone.transform.position = new Vector3(0f, -12f, 0f);

            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(200f, 6f, 200f);

            var body = zone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            zone.AddComponent<FallZone>().SetRespawnPoint(respawn);
        }

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

            var pusher = new GameObject("Pusher");
            pusher.transform.SetParent(player.transform, false);
            pusher.transform.localPosition = new Vector3(0f, 0.7f, 0.55f);
            var pusherCollider = pusher.AddComponent<SphereCollider>();
            pusherCollider.isTrigger = true;
            pusherCollider.radius = 0.55f;

            var input = player.AddComponent<PlayerInputReader>();
            input.SetActions(AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));

            var health = player.AddComponent<ChaosHealth>();
            health.Configure(100f, true);

            player.AddComponent<PlayerController>().SetSpawnPoint(spawn);
            player.AddComponent<PlayerPusher>();
            player.AddComponent<ChaosInteraction>();
            player.AddComponent<CharacterCustomization>();

            var cameraObject = new GameObject("PlayerCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.15f;
            SandboxSceneBuilder.SetPartySky(camera);
            cameraObject.AddComponent<AudioListener>();
            SandboxSceneBuilder.EnablePostProcessing(cameraObject);
            cameraObject.AddComponent<PlayerCamera>().SetTarget(player.transform);

            new GameObject("MissionDirector").AddComponent<ChaosExpressMission>();
            new GameObject("PauseMenu").AddComponent<CaosPauseMenu>();
            new GameObject("CustomizationScreen").AddComponent<CustomizationScreen>();
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
