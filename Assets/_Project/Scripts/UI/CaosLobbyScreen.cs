using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Net;

namespace Caos.UI
{
    /// <summary>
    /// Lobby principal. Módulo independiente en la escena FrontendLobby.
    /// El personaje se renderiza a una textura y se dibuja desde OnGUI antes
    /// que las tarjetas: por eso comparte el mismo orden de dibujo que los
    /// demás elementos y no queda "de fondo" detrás de la interfaz.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class CaosLobbyScreen : MonoBehaviour
    {
        int _focus;
        float _scale = 1f;
        float _offsetX;
        float _offsetY;

        Transform _preview;
        Camera _portraitCamera;
        RenderTexture _portrait;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var previewObject = GameObject.Find("PreviewCharacter");
            if (previewObject != null) _preview = previewObject.transform;

            var cameraObject = GameObject.Find("PortraitCamera");
            _portraitCamera = cameraObject != null
                ? cameraObject.GetComponent<Camera>()
                : Camera.main;

            if (_portraitCamera != null)
            {
                _portrait = new RenderTexture(720, 1080, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CaosLobbyPortrait",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _portrait.Create();
                _portraitCamera.targetTexture = _portrait;
                _portraitCamera.allowHDR = false;
            }
        }

        void OnDestroy()
        {
            if (_portraitCamera != null) _portraitCamera.targetTexture = null;
            if (_portrait != null)
            {
                _portrait.Release();
                Destroy(_portrait);
                _portrait = null;
            }
        }

        void Update()
        {
            if (CaosSceneLoader.IsLoading) return;

            if (_preview != null)
                _preview.Rotate(0f, 24f * Time.unscaledDeltaTime, 0f, Space.World);

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            int move = 0;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame ||
                    keyboard.aKey.wasPressedThisFrame ||
                    keyboard.upArrowKey.wasPressedThisFrame ||
                    keyboard.wKey.wasPressedThisFrame) move--;
                if (keyboard.rightArrowKey.wasPressedThisFrame ||
                    keyboard.dKey.wasPressedThisFrame ||
                    keyboard.downArrowKey.wasPressedThisFrame ||
                    keyboard.sKey.wasPressedThisFrame) move++;
            }

            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame ||
                    gamepad.dpad.up.wasPressedThisFrame) move--;
                if (gamepad.dpad.right.wasPressedThisFrame ||
                    gamepad.dpad.down.wasPressedThisFrame) move++;
            }

            if (move != 0) _focus = (_focus + move + 4) % 4;

            bool confirm = (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

            if (confirm) Activate(_focus);
        }

        void Activate(int option)
        {
            if (CaosSceneLoader.IsLoading) return;

            switch (option)
            {
                case 0:
                    Go("Dungeon", "Preparando la mazmorra...", "Generando el caos...",
                        CaosFrontendArt.Slot.Dungeon);
                    break;
                case 1:
                    Go("ArenaCaos", "Preparando las arenas...", "Calentando a los campeones...",
                        CaosFrontendArt.Slot.Arena);
                    break;
                case 2:
                    Go(CaosShell.CustomizeScene, "Preparando la personalización...",
                        "Abriendo a tu campeón...", CaosFrontendArt.Slot.Customization);
                    break;
                default:
                    Go(CaosShell.MultiplayerScene, "Conectando con la sala...",
                        "Abriendo la sala multijugador...", CaosFrontendArt.Slot.Startup);
                    break;
            }
        }

        void Go(string sceneName, string subtitle, string detail, CaosFrontendArt.Slot slot)
        {
            CaosSession.ResetForMenu();
            CaosSession.LocalName = PlayerPrefs.GetString("Caos.PlayerName", "Jugador");
            CaosSceneLoader.Begin(sceneName, "ARENA CAOS", subtitle, detail,
                CaosFrontendArt.Get(slot));
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
                // 1) Retrato del campeón: se dibuja aquí, en el mismo orden que
                //    el resto de la interfaz. No es un fondo detrás del HUD.
                var portraitRect = new Rect(0f, 0f, 780f, CaosShellUI.ReferenceHeight);
                CaosShellUI.DrawRect(portraitRect, new Color(0.03f, 0.05f, 0.09f, 1f));
                if (_portrait != null)
                    GUI.DrawTexture(portraitRect, _portrait, ScaleMode.ScaleAndCrop, true);
                CaosShellUI.DrawOutline(portraitRect, CaosShellUI.Cyan, 4f);

                // 2) Arte y panel exclusivamente en la mitad derecha.
                var right = new Rect(780f, 0f, 1140f, CaosShellUI.ReferenceHeight);
                CaosShellUI.DrawCover(right,
                    CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup), 0.78f);
                CaosShellUI.DrawRect(right, new Color(0.02f, 0.03f, 0.07f, 0.32f));

                CaosShellUI.DrawShadowLabel(new Rect(820f, 34f, 1000f, 112f),
                    "ARENA CAOS", styles.Title, new Color(0f, 0f, 0f, 0.78f));
                GUI.Label(new Rect(830f, 146f, 960f, 46f), "ELIGE TU CAOS", styles.Subtitle);
                GUI.Label(new Rect(832f, 190f, 940f, 32f),
                    "Tu campeón está listo. Elige dónde se arma el desmadre.", styles.Hint);

                Vector2 mouse = CaosShellUI.ToReference(
                    Event.current != null ? Event.current.mousePosition : Vector2.zero,
                    _scale, _offsetX, _offsetY);

                DrawCard(0, new Rect(980f, 236f, 880f, 204f),
                    CaosFrontendArt.Get(CaosFrontendArt.Slot.Dungeon), CaosShellUI.Violet,
                    "MODO 01", "MAZMORRA",
                    "Salas conectadas, recompensas y un guardián distinto en cada partida.",
                    mouse, styles);
                DrawCard(1, new Rect(980f, 456f, 880f, 204f),
                    CaosFrontendArt.Get(CaosFrontendArt.Slot.Arena), CaosShellUI.Coral,
                    "MODO 02", "ARENAS",
                    "Oleadas, jefes y caos directo con la arena que ya conoces.",
                    mouse, styles);
                DrawCard(2, new Rect(980f, 676f, 880f, 204f),
                    CaosFrontendArt.Get(CaosFrontendArt.Slot.Customization), CaosShellUI.Cyan,
                    "Tu campeón", "PERSONALIZAR PERSONAJE",
                    "Mira tu personaje actual y prepara futuras opciones de apariencia.",
                    mouse, styles);

                if (GUI.Button(new Rect(830f, 922f, 340f, 58f),
                        "SALA MULTIJUGADOR", styles.Button)) Activate(3);

                GUI.Label(new Rect(832f, 996f, 940f, 30f),
                    "Flechas/WASD + ENTER · Ratón: clic · Mando: cruceta + A", styles.Kicker);

                GUI.Label(new Rect(40f, 960f, 420f, 40f), "TU CAMPEÓN", styles.Kicker);
                GUI.Label(new Rect(28f, 1032f, 320f, 34f), "omar hpta", styles.Credit);
            }
            finally
            {
                CaosShellUI.Restore(previous);
            }
        }

        void DrawCard(int index, Rect rect, Texture2D art, Color accent,
            string kicker, string title, string body, Vector2 mouse,
            CaosShellUI.Styles styles)
        {
            if (rect.Contains(mouse)) _focus = index;
            bool focused = _focus == index;

            CaosShellUI.DrawRect(new Rect(rect.x + 6f, rect.y + 8f,
                rect.width, rect.height), new Color(0f, 0f, 0f, 0.34f));
            GUI.Box(rect, GUIContent.none, styles.Card);

            var artRect = new Rect(rect.x + 10f, rect.y + 10f, 190f, rect.height - 20f);
            CaosShellUI.DrawCover(artRect, art, 0.95f);
            CaosShellUI.DrawRect(new Rect(artRect.x, artRect.y, 5f, artRect.height), accent);

            float textX = rect.x + 222f;
            GUI.Label(new Rect(textX, rect.y + 24f, rect.width - 246f, 30f),
                kicker, styles.Kicker);
            GUI.Label(new Rect(textX, rect.y + 58f, rect.width - 246f, 58f),
                title, styles.CardTitle);
            GUI.Label(new Rect(textX, rect.y + 122f, rect.width - 246f, 76f),
                body, styles.CardBody);

            if (focused)
            {
                CaosShellUI.DrawOutline(rect, accent, 4f);
                CaosShellUI.DrawRect(new Rect(rect.x + 10f, rect.y + rect.height - 14f,
                    rect.width - 20f, 4f), accent);
            }

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) Activate(index);
        }
    }
}
