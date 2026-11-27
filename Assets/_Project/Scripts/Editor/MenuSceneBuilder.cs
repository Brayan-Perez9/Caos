using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Caos.Player;
using Caos.UI;

namespace Caos.EditorTools
{
    /// <summary>
    /// Construye la escena Menu como módulo de entrada: arte de bienvenida,
    /// título y precarga. La personalización vive en su propia escena y el
    /// lobby en otra, para que nada se superponga.
    /// Menú: Caos → Create Menu Scene.
    /// </summary>
    public static class MenuSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Menu.unity";

        [MenuItem("Caos/Create Menu Scene")]
        public static void CreateMenuScene()
        {
            if (!EditorUtility.DisplayDialog(
                    "Caos",
                    "Se creará la escena Menu (pantalla de personalización inicial).",
                    "Crear",
                    "Cancelar"))
            {
                return;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            CreateMenuSceneInternal();
        }

        /// <summary>Versión sin diálogo (la usa el build por lotes).</summary>
        public static void CreateMenuSceneInternal()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildMenu();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Caos] Menú creado en {ScenePath}.");
        }

        static void BuildMenu()
        {
            SandboxSceneBuilder.SetupLighting();

            var root = new GameObject("Menu");

            BuildStage(root.transform);
            CreateCamera(root.transform);

            // Solo el módulo de entrada. Ni personaje, ni personalización,
            // ni actualizador superpuestos.
            new GameObject("CaosStartupScreen").AddComponent<CaosStartupScreen>();
        }

        internal static void BuildStage(Transform parent)
        {
            // Suelo oscuro
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
            floor.transform.SetParent(parent, true);
            SandboxSceneBuilder.SetMaterial(floor, "MenuFloor");

            // Pedestal del personaje
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.position = new Vector3(-1.7f, 0.1f, 0f);
            pedestal.transform.localScale = new Vector3(1.8f, 0.1f, 1.8f);
            pedestal.transform.SetParent(parent, true);
            SandboxSceneBuilder.SetMaterial(pedestal, "Pedestal");

            // Muro de fondo
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "Backdrop";
            backdrop.transform.position = new Vector3(0f, 4f, 4f);
            backdrop.transform.localScale = new Vector3(24f, 8f, 0.5f);
            backdrop.transform.SetParent(parent, true);
            SandboxSceneBuilder.SetMaterial(backdrop, "Backdrop");
        }

        internal static void CreatePreviewCharacter(Transform parent)
        {
            // Personaje solo visual (sin controlador): gira despacio en el menú.
            var preview = new GameObject("PreviewCharacter");
            preview.transform.SetParent(parent, false);
            preview.transform.position = new Vector3(-1.7f, 0.2f, 0f);

            SandboxSceneBuilder.CreatePlayerVisual(preview.transform);
            preview.AddComponent<CharacterCustomization>();
        }

        internal static void CreateCamera(Transform parent)
        {
            // Cámara horizontal: el personaje (x=-1.7) queda en el tercio
            // izquierdo; la mitad derecha queda libre para la interfaz.
            var cameraObject = new GameObject("MenuCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(0.4f, 1.5f, -4.6f);
            cameraObject.transform.rotation = Quaternion.identity;

            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.11f);

            cameraObject.AddComponent<AudioListener>();
            SandboxSceneBuilder.EnablePostProcessing(cameraObject);
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
