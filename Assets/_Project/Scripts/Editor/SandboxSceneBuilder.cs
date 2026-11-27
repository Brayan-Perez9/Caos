using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Interactions;
using Caos.Player;
using Caos.UI;

namespace Caos.EditorTools
{
    /// <summary>
    /// Construye la escena de pruebas: suelo, escaleras, rampas, plataformas
    /// mÃ³viles, cajas empujables, jugador con cÃ¡mara y zona de caÃ­da.
    /// MenÃº: Caos â†’ Create Sandbox Scene.
    /// </summary>
    public static class SandboxSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
        const string MaterialFolder = "Assets/_Project/Materials/";
        const string InputActionsPath = "Assets/_Project/InputSystem_Actions.inputactions";

        const float ArenaHalf = 15f;

        /// <summary>Ruta de la escena (la usa el script de build).</summary>
        public static string ScenePathPublic => ScenePath;

        [MenuItem("Caos/Create Sandbox Scene")]
        public static void CreateSandboxScene()
        {
            if (!EditorUtility.DisplayDialog(
                    "Caos",
                    "Se crearÃ¡ la escena Sandbox. La escena abierta actual se reemplazarÃ¡ (se te preguntarÃ¡ si guardarla).",
                    "Crear",
                    "Cancelar"))
            {
                return;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            CreateSandboxSceneInternal();
        }

        /// <summary>
        /// VersiÃ³n sin diÃ¡logo: la usa el build por lotes (-executeMethod).
        /// </summary>
        public static void CreateSandboxSceneInternal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildSandbox();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Caos] Sandbox creado en {ScenePath}.");
        }

        static void BuildSandbox()
        {
            SetupLighting();

            var root = new GameObject("Sandbox");

            BuildArena(root.transform);
            BuildProps(root.transform);
            BuildStairsAndHighPlatform(root.transform);
            BuildMovingPlatforms(root.transform);

            var spawn = new GameObject("SpawnPoint");
            spawn.transform.position = new Vector3(0f, 0.2f, 0f);

            CreateFallZone(spawn.transform);
            CreatePlayerAndCamera(root.transform, spawn.transform);
        }

        // ---------------------------------------------------------- entorno

        public static void SetupLighting()
        {
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.74f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.74f, 0.73f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.33f, 0.31f);
            RenderSettings.ambientIntensity = 0.85f;

            // Niebla suave: da profundidad al mapa grande sin ocultar la ruta.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.08f, 0.10f, 0.16f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 95f;
        }

        static void BuildArena(Transform parent)
        {
            // Suelo: el plano de Unity mide 10x10 con escala 1, asÃ­ que escala 3 => 30x30.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            ground.transform.SetParent(parent, true);
            SetMaterial(ground, "Sand");

            // Tres muros; el lado sur queda abierto para poder caerse.
            CreateBox("Wall_N", new Vector3(0f, 1.5f, ArenaHalf), new Vector3(31f, 3f, 1f), "Wall", parent);
            CreateBox("Wall_E", new Vector3(ArenaHalf, 1.5f, 0f), new Vector3(1f, 3f, 31f), "Wall", parent);
            CreateBox("Wall_W", new Vector3(-ArenaHalf, 1.5f, 0f), new Vector3(1f, 3f, 31f), "Wall", parent);
        }

        static void BuildProps(Transform parent)
        {
            // Torre de cajas
            for (int column = 0; column < 3; column++)
            {
                for (int row = 0; row < 3; row++)
                {
                    // SeparaciÃ³n exacta de 1.0 en Y para que las cajas queden apoyadas.
                    var position = new Vector3(-5f + column * 1.05f, 0.5f + row * 1.0f, -7f);
                    CreateCrate(position, parent);
                }
            }

            // Pelota con rebote
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "BouncyBall";
            ball.transform.position = new Vector3(4f, 1.2f, -8f);
            ball.transform.localScale = Vector3.one * 0.9f;
            ball.transform.SetParent(parent, true);
            SetMaterial(ball, "Ball");

            var ballBody = ball.AddComponent<Rigidbody>();
            ballBody.mass = 0.8f;
            ballBody.interpolation = RigidbodyInterpolation.Interpolate;
            ball.GetComponent<Collider>().sharedMaterial = GetPhysicsMaterial("Bouncy", 0.4f, 0.9f);

            // Rampa
            var ramp = CreateBox("Ramp", new Vector3(6f, 1.5f, -2f), new Vector3(6f, 0.5f, 8f), "Ramp", parent);
            ramp.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
        }

        static void BuildStairsAndHighPlatform(Transform parent)
        {
            var stairs = new GameObject("Stairs");
            stairs.transform.SetParent(parent, false);
            stairs.transform.position = new Vector3(-9f, 0f, 0f);

            const int stepCount = 12;
            const float stepHeight = 0.3f;
            const float stepDepth = 0.9f;

            for (int i = 0; i < stepCount; i++)
            {
                var step = CreateBox($"Step_{i + 1}", Vector3.zero, new Vector3(4f, stepHeight, stepDepth), "Ramp", stairs.transform);
                step.transform.localPosition = new Vector3(0f, stepHeight * (i + 0.5f), stepDepth * i);
            }

            // La plataforma arranca exactamente donde acaba la Ãºltima escalera.
            CreateBox("Platform_High", new Vector3(-9f, 3.25f, 12.5f), new Vector3(5f, 0.7f, 5f), "Platform", parent);
        }

        static void BuildMovingPlatforms(Transform parent)
        {
            CreateMovingPlatform(
                "Platform_MoveH",
                new Vector3(-6f, 1.6f, -10f),
                Vector3.zero,
                new Vector3(6f, 0f, 0f),
                1.2f,
                parent);

            CreateMovingPlatform(
                "Platform_MoveV",
                new Vector3(10f, 1f, -5f),
                Vector3.zero,
                new Vector3(0f, 4f, 0f),
                1f,
                parent);
        }

        static void CreateMovingPlatform(string name, Vector3 position, Vector3 offsetA, Vector3 offsetB, float speed, Transform parent)
        {
            var platform = CreateBox(name, position, new Vector3(4f, 0.6f, 4f), "Platform", parent);

            // Trigger que detecta al jugador encima (los triggers necesitan un Rigidbody).
            var zone = new GameObject("RiderZone");
            zone.transform.SetParent(platform.transform, false);
            var zoneCollider = zone.AddComponent<BoxCollider>();
            zoneCollider.isTrigger = true;
            zoneCollider.size = new Vector3(1.15f, 1.6f, 1.15f);

            platform.AddComponent<MovingPlatform>().Configure(offsetA, offsetB, speed);
        }

        static void CreateFallZone(Transform respawnPoint)
        {
            var fallZone = new GameObject("FallZone");
            fallZone.transform.position = new Vector3(0f, -12f, 0f);

            var trigger = fallZone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(200f, 6f, 200f);

            var body = fallZone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            fallZone.AddComponent<FallZone>().SetRespawnPoint(respawnPoint);
        }

        // ---------------------------------------------------------- jugador

        static void CreatePlayerAndCamera(Transform parent, Transform spawn)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(parent, false);
            player.transform.position = spawn.position + Vector3.up * 1.2f;

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.08f;

            CreatePlayerVisual(player.transform);

            var pusher = new GameObject("Pusher");
            pusher.transform.SetParent(player.transform, false);
            pusher.transform.localPosition = new Vector3(0f, 0.7f, 0.55f);
            var pusherCollider = pusher.AddComponent<SphereCollider>();
            pusherCollider.isTrigger = true;
            pusherCollider.radius = 0.55f;

            var input = player.AddComponent<PlayerInputReader>();
            input.SetActions(AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));

            // El orden importa: PlayerPusher exige PlayerController y este exige el lector
            // de input. Si se aÃ±ade al revÃ©s, RequireComponent duplicarÃ­a el componente.
            player.AddComponent<PlayerController>().SetSpawnPoint(spawn);
            player.AddComponent<PlayerPusher>();

            // PersonalizaciÃ³n: nombre + color (se dibuja el nombre sobre la cabeza).
            player.AddComponent<CharacterCustomization>();

            var cameraObject = new GameObject("PlayerCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.15f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<PlayerCamera>().SetTarget(player.transform);

            new GameObject("PrototypeHud").AddComponent<PrototypeHud>();
            new GameObject("PauseMenu").AddComponent<CaosPauseMenu>();
            new GameObject("CustomizationScreen").AddComponent<CustomizationScreen>();
        }

        public static void CreatePlayerVisual(Transform player)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            SetMaterial(body, "PlayerRed");

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(player, false);
            head.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            head.transform.localScale = Vector3.one * 0.55f;
            UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());
            SetMaterial(head, "PlayerRed");

            CreateEye(player, new Vector3(-0.12f, 1.95f, 0.24f));
            CreateEye(player, new Vector3(0.12f, 1.95f, 0.24f));
        }

        static void CreateEye(Transform player, Vector3 localPosition)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(player, false);
            eye.transform.localPosition = localPosition;
            eye.transform.localScale = Vector3.one * 0.13f;
            UnityEngine.Object.DestroyImmediate(eye.GetComponent<Collider>());
            SetMaterial(eye, "Eye");
        }

        // ---------------------------------------------------------- utilidades

        static GameObject CreateBox(string name, Vector3 position, Vector3 scale, string material, Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;

            if (parent != null) box.transform.SetParent(parent, true);

            SetMaterial(box, material);
            return box;
        }

        static void CreateCrate(Vector3 position, Transform parent)
        {
            var crate = CreateBox("Crate", position, Vector3.one, "Crate", parent);

            var body = crate.AddComponent<Rigidbody>();
            body.mass = 1.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            crate.GetComponent<Collider>().sharedMaterial = GetPhysicsMaterial("CratePhys", 0.15f, 0.15f);
        }

        public static void SetMaterial(GameObject target, string materialName)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = GetMaterial(materialName);
        }

        /// <summary>
        /// Suelo procedural URP: rejilla en coordenadas de mundo, pulso y
        /// penumbra. No usa texturas ni assets de arte externos.
        /// </summary>
        public static Material GetNeonGridMaterial()
        {
            const string path = MaterialFolder + "NeonGrid.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Caos/NeonGrid");
            if (shader == null)
            {
                Debug.LogWarning("[Caos] No se encontró el shader Caos/NeonGrid; se usa MenuFloor.");
                return GetMaterial("MenuFloor");
            }

            material = new Material(shader);
            material.SetColor("_BaseColor", new Color(0.028f, 0.045f, 0.075f, 1f));
            material.SetColor("_GridColor", new Color(0.10f, 0.78f, 1f, 1f));
            material.SetColor("_AccentColor", new Color(1f, 0.30f, 0.62f, 1f));
            material.SetFloat("_GridScale", 0.5f);
            material.SetFloat("_LineWidth", 0.03f);
            material.SetFloat("_PulseSpeed", 1.4f);
            material.SetFloat("_FadeStart", 18f);
            material.SetFloat("_FadeEnd", 34f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Material GetMaterial(string materialName)
        {
            if (materialName == "NeonGrid") return GetNeonGridMaterial();

            string path = MaterialFolder + materialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Caos] No se encontrÃ³ el shader URP Lit; usando el por defecto.");
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            material.SetColor("_BaseColor", Palette(materialName));

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static PhysicsMaterial GetPhysicsMaterial(string materialName, float friction, float bounciness)
        {
            string path = MaterialFolder + materialName + ".physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material != null) return material;

            material = new PhysicsMaterial
            {
                dynamicFriction = friction,
                staticFriction = friction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum,
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Color Palette(string materialName)
        {
            switch (materialName)
            {
                case "Sand": return new Color(0.85f, 0.79f, 0.64f);
                case "Wall": return new Color(0.72f, 0.65f, 0.55f);
                case "Crate": return new Color(0.89f, 0.61f, 0.30f);
                case "Platform": return new Color(0.50f, 0.66f, 0.85f);
                case "Ramp": return new Color(0.56f, 0.75f, 0.56f);
                case "Ball": return new Color(0.89f, 0.34f, 0.18f);
                case "PlayerRed": return new Color(0.89f, 0.34f, 0.18f);
                case "Eye": return new Color(0.13f, 0.12f, 0.12f);
                case "MenuFloor": return new Color(0.13f, 0.14f, 0.18f);
                case "Backdrop": return new Color(0.09f, 0.10f, 0.14f);
                case "Pedestal": return new Color(0.28f, 0.30f, 0.38f);
                case "Candy": return new Color(0.98f, 0.62f, 0.76f);
                case "Mint": return new Color(0.48f, 0.90f, 0.74f);
                case "Lemon": return new Color(1f, 0.86f, 0.36f);
                case "Neon": return new Color(0.33f, 0.84f, 1f);
                case "Lava": return new Color(1f, 0.38f, 0.26f);
                default: return Color.magenta;
            }
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(entry => entry.path == ScenePath)) return;

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
