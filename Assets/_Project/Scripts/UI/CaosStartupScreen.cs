using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Net;

namespace Caos.UI
{
    /// <summary>
    /// Módulo de entrada. Vive solo en la escena Menu: muestra el arte de
    /// bienvenida, precarga recursos y lleva al lobby dedicado. No dibuja el
    /// personaje ni ningún panel de personalización.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class CaosStartupScreen : MonoBehaviour
    {
        float _scale = 1f;
        float _offsetX;
        float _offsetY;

        void Start()
        {
            CaosFrontendArt.BeginPreload();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Atajos de línea de comandos heredados: -host crea sala, -code=... entra.
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument == "-host" ||
                    (argument != null && argument.StartsWith("-code=", StringComparison.OrdinalIgnoreCase)))
                {
                    OpenMultiplayer();
                    return;
                }
            }
        }

        void Update()
        {
            if (CaosSceneLoader.IsLoading) return;

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool confirm = (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

            if (confirm) Enter();
        }

        void Enter()
        {
            if (CaosSceneLoader.IsLoading) return;

            CaosSceneLoader.Begin(CaosShell.LobbyScene, "ARENA CAOS",
                "Preparando el lobby...", "Abriendo el caos...",
                CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup));
        }

        void OpenMultiplayer()
        {
            if (CaosSceneLoader.IsLoading) return;

            CaosSession.ResetForMenu();
            CaosSession.LocalName = PlayerPrefs.GetString("Caos.PlayerName", "Jugador");
            CaosSceneLoader.Begin(CaosShell.MultiplayerScene, "ARENA CAOS",
                "Conectando con la sala...", "Abriendo el lobby multijugador...",
                CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup));
        }

        void OnGUI()
        {
            if (CaosSceneLoader.IsLoading) return;

            var styles = CaosShellUI.GetStyles();
            var full = new Rect(0f, 0f, CaosShellUI.ReferenceWidth,
                CaosShellUI.ReferenceHeight);

            CaosShellUI.ComputeScale(out _scale, out _offsetX, out _offsetY);
            if (_scale <= 0f) return;

            Matrix4x4 previous = CaosShellUI.Begin(_scale, _offsetX, _offsetY);
            try
            {
                CaosShellUI.DrawCover(full,
                    CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup), 1f);
                CaosShellUI.DrawRect(full, new Color(0.01f, 0.02f, 0.06f, 0.32f));
                CaosShellUI.DrawRect(new Rect(0f, 700f, CaosShellUI.ReferenceWidth, 380f),
                    new Color(0.01f, 0.02f, 0.06f, 0.80f));

                CaosShellUI.DrawShadowLabel(new Rect(0f, 268f,
                        CaosShellUI.ReferenceWidth, 140f),
                    "ARENA CAOS", styles.Title, new Color(0f, 0f, 0f, 0.82f));
                GUI.Label(new Rect(0f, 420f, CaosShellUI.ReferenceWidth, 54f),
                    "BIENVENIDO AL DESMADRE", styles.Subtitle);

                float progress = CaosFrontendArt.Progress;
                CaosShellUI.DrawBar(new Rect(660f, 800f, 600f, 18f), progress);
                GUI.Label(new Rect(660f, 756f, 600f, 34f),
                    $"Cargando recursos visuales... {Mathf.RoundToInt(progress * 100f)}%",
                    styles.Kicker);

                if (GUI.Button(new Rect(760f, 880f, 400f, 72f),
                        "ENTRAR AL CAOS", styles.Button))
                {
                    Enter();
                }

                GUI.Label(new Rect(0f, 1010f, CaosShellUI.ReferenceWidth, 32f),
                    "Pulsa ENTER o haz clic para continuar", styles.Hint);
                GUI.Label(new Rect(28f, 1032f, 320f, 34f), "omar hpta", styles.Credit);
            }
            finally
            {
                CaosShellUI.Restore(previous);
            }
        }
    }
}
