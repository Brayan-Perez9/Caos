using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Caos.EditorTools
{
    /// <summary>
    /// Build del juego para Windows (64 bits).
    ///
    /// Uso desde el menú: Caos → Build Windows (exe).
    /// Uso por lotes:
    ///   Unity.exe -batchmode -projectPath . -executeMethod Caos.EditorTools.BuildCaos.BuildWindows
    /// </summary>
    public static class BuildCaos
    {
        const string OutputPath = "Builds/Windows/Caos.exe";

        [MenuItem("Caos/Build Windows (exe)")]
        public static void BuildWindowsMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Caos",
                    $"Se compilará el juego en:\n{OutputPath}\n\n¿Continuar?",
                    "Compilar",
                    "Cancelar"))
            {
                return;
            }

            BuildWindows();
        }

        /// <summary>Punto de entrada para -executeMethod (build por lotes).</summary>
        public static void BuildWindows()
        {
            try
            {
                EnsureSceneExists();
                ConfigureScenes();

                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes
                        .Where(scene => scene.enabled)
                        .Select(scene => scene.path)
                        .ToArray(),
                    locationPathName = OutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                };

                if (options.scenes.Length == 0)
                    throw new System.InvalidOperationException("No hay escenas habilitadas en Build Settings.");

                Debug.Log($"[Caos] Compilando {options.scenes.Length} escena(s) → {OutputPath}");

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[Caos] Build correcto: {OutputPath} " +
                              $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s)");
                    Exit(0);
                }
                else
                {
                    Debug.LogError($"[Caos] Build fallido: {summary.result} " +
                                   $"({summary.totalErrors} error(es)).");
                    Exit(1);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Caos] Excepción durante el build:\n{exception}");
                Exit(1);
            }
        }

        static void EnsureSceneExists()
        {
            string scenePath = SandboxSceneBuilder.ScenePathPublic;
            if (!File.Exists(scenePath))
            {
                Debug.Log($"[Caos] La escena no existe; creando Sandbox…");
                SandboxSceneBuilder.CreateSandboxSceneInternal();
            }

            if (!File.Exists(MenuSceneBuilder.ScenePath))
            {
                Debug.Log("[Caos] La escena de menú no existe; creando Menu…");
                MenuSceneBuilder.CreateMenuSceneInternal();
            }

            if (!File.Exists(LobbySceneBuilder.ScenePath))
            {
                Debug.Log("[Caos] La escena del lobby no existe; creando Lobby…");
                LobbySceneBuilder.CreateLobbySceneInternal();
            }

            if (!File.Exists(ChaosExpressBuilder.ScenePath))
            {
                Debug.Log("[Caos] La escena de misión no existe; creando Chaos Express…");
                ChaosExpressBuilder.CreateMapInternal();
            }
        }

        static void ConfigureScenes()
        {
            // Menú, lobby, misión corta y por último el tablero.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuSceneBuilder.ScenePath, true),
                new EditorBuildSettingsScene(LobbySceneBuilder.ScenePath, true),
                new EditorBuildSettingsScene(ChaosExpressBuilder.ScenePath, true),
                new EditorBuildSettingsScene(SandboxSceneBuilder.ScenePathPublic, true),
            };

            PlayerSettings.productName = "Caos";
            PlayerSettings.companyName = "Caos";
        }

        static void Exit(int code)
        {
            // En modo menú (editor abierto) no cerramos el editor.
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
