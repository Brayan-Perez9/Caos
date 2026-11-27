using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Caos.Interactions;
using Caos.Maps;
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
            CaosAmbience.Ensure(root.transform, 20261128, AmbientBiome.Pradera,
                new Vector2(13.5f, 13.5f), 0f, false, GetAmbientMaterial(), GetBlueGrassMaterial());
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
            light.color = new Color(1f, 0.93f, 0.80f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.72f;
            sun.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            // Relleno frío sin sombras: separa siluetas sin competir con el sol.
            var fillObject = new GameObject("FillLight");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.80f, 1f);
            fill.intensity = 0.20f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(28f, 142f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.72f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(1f, 0.80f, 0.60f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.34f, 0.24f);
            RenderSettings.ambientIntensity = 0.85f;

            // Sin niebla global: lavaba la imagen y ocultaba la lectura del mapa.
            // La profundidad la dan ahora las montañas y nubes alejadas.
            RenderSettings.fog = false;

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.profile = GetCaosVolumeProfile();
        }

        /// <summary>Activa el postprocesado URP en una cámara del build.</summary>
        public static void EnablePostProcessing(GameObject cameraObject)
        {
            if (cameraObject == null) return;
            var data = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }

        /// <summary>Cielo pastel fijo: evita el fondo blanco cuando no hay skybox.</summary>
        public static void SetPartySky(Camera camera)
        {
            if (camera == null) return;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.47f, 0.76f, 0.96f, 1f);
        }

        static void BuildArena(Transform parent)
        {
            // Suelo: el plano de Unity mide 10x10 con escala 1, asÃ­ que escala 3 => 30x30.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            ground.transform.SetParent(parent, true);
            SetMaterial(ground, "Grass");

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
            var ramp = CreateBox("Ramp", new Vector3(6f, 1.5f, -2f), new Vector3(6f, 0.5f, 8f), "Dirt", parent);
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
                var step = CreateBox($"Step_{i + 1}", Vector3.zero, new Vector3(4f, stepHeight, stepDepth), "Dirt", stairs.transform);
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
            SetPartySky(camera);
            cameraObject.AddComponent<AudioListener>();
            EnablePostProcessing(cameraObject);
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
            RoundBox(box, scale);
            return box;
        }

        /// <summary>Vista biselada; el BoxCollider original conserva su tamaño.</summary>
        public static void RoundBox(GameObject target, Vector3 size, float radiusFraction = 0.18f)
        {
            if (target == null) return;
            var source = target.GetComponent<Renderer>();
            if (source == null || source.sharedMaterial == null) return;
            float thickness = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float radius = Mathf.Clamp(thickness * radiusFraction, 0.008f, thickness * 0.45f);
            CaosVisualKit.ApplyRoundedVisual(target, size, radius, source.sharedMaterial, 3);
        }

        /// <summary>Vista cilíndrica redondeada; el collider original no se toca.</summary>
        public static void RoundCylinder(GameObject target, Vector3 scale)
        {
            if (target == null) return;
            var source = target.GetComponent<Renderer>();
            if (source == null || source.sharedMaterial == null) return;
            CaosVisualKit.ApplyCylinderVisual(target, scale, source.sharedMaterial);
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

        /// <summary>
        /// Material base del ambiente (hierba, lluvia y luciérnagas). Se guarda
        /// como asset y se referencia desde la escena para que el build no
        /// elimine el shader Caos/Ambient por stripping.
        /// </summary>
        public static Material GetAmbientMaterial()
        {
            const string path = MaterialFolder + "Ambient.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Caos/Ambient");
            if (shader == null)
            {
                Debug.LogWarning("[Caos] No se encontró el shader Caos/Ambient.");
                return GetMaterial("Neon");
            }

            material = new Material(shader);
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Pasto procedural con viento: suelo de todos los mapas.</summary>
        public static Material GetGrassMaterial()
        {
            const string path = MaterialFolder + "Grass.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Caos/Grass");
            if (shader == null)
            {
                Debug.LogWarning("[Caos] No se encontró el shader Caos/Grass.");
                return material != null ? material : GetMaterial("Ramp");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;

            material.SetColor("_BaseColor", new Color(0.118f, 0.420f, 0.227f, 1f));
            material.SetColor("_BladeColor", new Color(0.208f, 0.659f, 0.361f, 1f));
            material.SetColor("_SoilColor", new Color(0.420f, 0.290f, 0.184f, 1f));
            material.SetFloat("_Scale", 2.2f);
            material.SetFloat("_WindSpeed", 0.9f);
            material.SetFloat("_WindStrength", 0.055f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Tierra procedural para peldaños y rampas.</summary>
        public static Material GetDirtMaterial()
        {
            const string path = MaterialFolder + "Dirt.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Caos/Dirt");
            if (shader == null)
            {
                Debug.LogWarning("[Caos] No se encontró el shader Caos/Dirt.");
                return material != null ? material : GetMaterial("Ramp");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;

            material.SetColor("_BaseColor", new Color(0.420f, 0.290f, 0.184f, 1f));
            material.SetColor("_LightColor", new Color(0.651f, 0.486f, 0.322f, 1f));
            material.SetColor("_DeepColor", new Color(0.227f, 0.165f, 0.110f, 1f));
            material.SetFloat("_Scale", 2.6f);
            material.SetFloat("_Strata", 7f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Follaje azul con la misma textura de pasto procedural.</summary>
        public static Material GetBlueGrassMaterial()
        {
            const string path = MaterialFolder + "BlueGrass.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Caos/Grass");
            if (shader == null)
            {
                Debug.LogWarning("[Caos] No se encontró el shader Caos/Grass.");
                return material != null ? material : GetMaterial("Neon");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;

            material.SetColor("_BaseColor", new Color(0.165f, 0.482f, 0.847f, 1f));
            material.SetColor("_BladeColor", new Color(0.404f, 0.784f, 1f, 1f));
            material.SetColor("_SoilColor", new Color(0.078f, 0.192f, 0.361f, 1f));
            material.SetFloat("_Scale", 3.0f);
            material.SetFloat("_WindSpeed", 1.05f);
            material.SetFloat("_WindStrength", 0.075f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Material stylized con rim suave; se guarda como asset referenciado.</summary>
        public static Material GetStylizedMaterial(string variant)
        {
            if (string.IsNullOrEmpty(variant)) variant = "Cyan";
            string path = MaterialFolder + "Stylized_" + variant + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Caos/ToonStylized");
            if (shader == null)
            {
                Debug.LogWarning("[Caos] No se encontró el shader Caos/ToonStylized.");
                return material != null ? material : GetMaterial("Platform");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;

            material.SetColor("_BaseColor", StylizedPalette(variant));
            material.SetColor("_ShadowColor", new Color(0.14f, 0.24f, 0.34f, 1f));
            material.SetColor("_RimColor", new Color(0.35f, 0.82f, 1f, 1f));
            material.SetFloat("_RimPower", 3.2f);
            material.SetFloat("_RimStrength", 0.24f);
            material.SetFloat("_Emission", variant == "Coral" || variant == "Cyan" ? 0.12f : 0.03f);
            material.SetFloat("_Smoothness", 0.32f);
            material.SetFloat("_NoiseScale", 1.5f);
            material.SetColor("_LightDir", new Vector4(0.45f, 0.85f, 0.30f, 0f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Color StylizedPalette(string variant)
        {
            switch (variant)
            {
                case "Coral": return new Color(1f, 0.42f, 0.42f, 1f);
                case "Lemon": return new Color(1f, 0.82f, 0.40f, 1f);
                case "Violet": return new Color(0.48f, 0.38f, 1f, 1f);
                case "Mint": return new Color(0.42f, 0.89f, 0.68f, 1f);
                case "Platform": return new Color(0.38f, 0.72f, 0.92f, 1f);
                default: return new Color(0.21f, 0.82f, 1f, 1f);
            }
        }

        /// <summary>Perfil URP moderado: bloom suave, tonemap y vignette ligero.</summary>
        public static VolumeProfile GetCaosVolumeProfile()
        {
            const string path = "Assets/Settings/CaosVolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            bool created = profile == null;

            if (created)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "CaosVolumeProfile";
            }

            // Se reescriben los valores incluso si el asset ya existía: así una
            // regeneración corrige perfiles creados por versiones anteriores.
            var bloom = GetOrAdd<Bloom>(profile, true);
            bloom.intensity.Override(0.10f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.40f);
            bloom.tint.Override(new Color(1f, 0.97f, 0.92f));

            var tonemapping = GetOrAdd<Tonemapping>(profile, true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var vignette = GetOrAdd<Vignette>(profile, true);
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.50f);
            vignette.rounded.Override(true);

            var color = GetOrAdd<ColorAdjustments>(profile, true);
            color.postExposure.Override(0f);
            color.contrast.Override(6f);
            color.saturation.Override(10f);

            if (created) AssetDatabase.CreateAsset(profile, path);
            AddSubAssetIfNeeded(bloom, profile);
            AddSubAssetIfNeeded(tonemapping, profile);
            AddSubAssetIfNeeded(vignette, profile);
            AddSubAssetIfNeeded(color, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static T GetOrAdd<T>(VolumeProfile profile, bool overrides) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out T component) && component != null) return component;
            return profile.Add<T>(overrides);
        }

        static void AddSubAssetIfNeeded(UnityEngine.Object child, UnityEngine.Object parent)
        {
            if (child == null || AssetDatabase.Contains(child)) return;
            AssetDatabase.AddObjectToAsset(child, parent);
        }

        public static Material GetMaterial(string materialName)
        {
            if (materialName == "NeonGrid") return GetNeonGridMaterial();
            if (materialName.StartsWith("Stylized_")) return GetStylizedMaterial(materialName.Substring("Stylized_".Length));
            if (materialName == "Grass") return GetGrassMaterial();
            if (materialName == "Dirt") return GetDirtMaterial();
            if (materialName == "BlueGrass") return GetBlueGrassMaterial();

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
