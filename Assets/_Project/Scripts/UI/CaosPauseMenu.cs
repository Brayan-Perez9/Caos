using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Caos.Maps;
using Caos.Net;
using Caos.Player;

namespace Caos.UI
{
    /// <summary>
    /// Menú de pausa con ESC: reanudar, volver al lobby o salir del juego.
    /// Se añade a las escenas jugables y no depende de la personalización.
    /// </summary>
    public class CaosPauseMenu : MonoBehaviour
    {
        static readonly Color PanelColor = new Color32(11, 27, 43, 209);      // #0B1B2B al 82 %
        static readonly Color TextColor = new Color32(247, 251, 255, 255);    // #F7FBFF
        static readonly Color AccentYellow = new Color32(255, 209, 102, 255); // #FFD166
        static readonly Color Cyan = new Color32(53, 208, 255, 255);
        static readonly Color Coral = new Color32(255, 107, 107, 255);
        static readonly Color Violet = new Color32(123, 97, 255, 255);

        bool _open;
        PlayerController _player;
        GUIStyle _titleStyle;
        GUIStyle _buttonStyle;
        GUIStyle _textStyle;
        GUIStyle _buttonLabelStyle;

        static Texture2D _roundTexture;
        static GUIStyle _panelStyle;
        static float _uiScale = -1f;

        void Awake()
        {
            _player = FindFirstObjectByType<PlayerController>();
        }

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

            var mission = FindFirstObjectByType<ChaosExpressMission>();
            if (mission != null && !mission.CanPause) return;

            var arena = FindFirstObjectByType<ArenaDirector>();
            if (arena != null && !arena.CanPause) return;

            SetOpen(!_open);
        }

        void SetOpen(bool value)
        {
            _open = value;
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player != null) _player.InputEnabled = !value;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDestroy()
        {
            // Si el jugador sale de la escena mientras estaba pausado,
            // no dejamos el control bloqueado para la siguiente escena.
            if (_player != null) _player.InputEnabled = true;
        }

        void ReturnToLobby()
        {
            CaosNetPeer.Instance?.Shutdown();
            CaosSession.ResetForMenu();
            SceneManager.LoadScene("Lobby");
        }

        void QuitGame()
        {
            Application.Quit();
            Debug.Log("[Caos] Salir del juego.");
        }

        void OnGUI()
        {
            if (!_open) return;
            float scale = UiScale();
            EnsureStyles(scale);

            float margin = S(16f, scale);
            float width = Mathf.Min(S(420f, scale), Screen.width - margin * 2f);
            float height = S(410f, scale);
            var panel = new Rect((Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f, width, height);
            DrawPanel(panel, scale, PanelColor);

            float x = panel.x + S(22f, scale);
            float w = panel.width - S(44f, scale);
            float y = panel.y + S(18f, scale);

            GUI.Label(new Rect(x, y, w, S(38f, scale)), "PAUSA", _titleStyle);
            y += S(44f, scale);
            GUI.Label(new Rect(x, y, w, S(24f, scale)), "ESC para volver al juego", _textStyle);
            y += S(36f, scale);

            var resume = new Rect(x, y, w, S(46f, scale));
            DrawButtonBackground(resume, scale, Cyan);
            if (GUI.Button(resume, "REANUDAR", _buttonLabelStyle)) SetOpen(false);
            y += S(54f, scale);

            var camera = Camera.main != null ? Camera.main.GetComponent<PlayerCamera>() : null;
            bool reduceMotion = camera != null && camera.ReduceMotion;
            var cameraButton = new Rect(x, y, w, S(38f, scale));
            DrawButtonBackground(cameraButton, scale, reduceMotion ? AccentYellow : Violet, reduceMotion);
            if (GUI.Button(cameraButton, reduceMotion ? "CÁMARA: MENOS MOVIMIENTO ✓"
                    : "CÁMARA: MENOS MOVIMIENTO", _buttonLabelStyle))
            {
                camera?.ToggleReduceMotion();
            }
            y += S(44f, scale);

            bool reduceFlashing = ArenaJuice.ReduceFlashing;
            var flashButton = new Rect(x, y, w, S(38f, scale));
            DrawButtonBackground(flashButton, scale, reduceFlashing ? AccentYellow : Violet, reduceFlashing);
            if (GUI.Button(flashButton, reduceFlashing ? "DESTELLOS: REDUCIDOS ✓"
                    : "DESTELLOS: REDUCIDOS", _buttonLabelStyle))
            {
                ArenaJuice.ReduceFlashing = !ArenaJuice.ReduceFlashing;
            }
            y += S(50f, scale);

            var lobby = new Rect(x, y, w, S(46f, scale));
            DrawButtonBackground(lobby, scale, Coral);
            if (GUI.Button(lobby, "VOLVER AL LOBBY", _buttonLabelStyle)) ReturnToLobby();
            y += S(54f, scale);

            var quit = new Rect(x, y, w, S(46f, scale));
            DrawButtonBackground(quit, scale, Coral);
            if (GUI.Button(quit, "SALIR DEL JUEGO", _buttonLabelStyle)) QuitGame();
        }

        static float UiScale()
        {
            return Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.8f, 1.5f);
        }

        static float S(float pixels, float scale) => pixels * scale;

        static void DrawPanel(Rect rect, float scale, Color color)
        {
            DrawRoundRect(new Rect(rect.x + S(2f, scale), rect.y + S(3f, scale), rect.width, rect.height),
                new Color(0f, 0f, 0f, 0.35f), _panelStyle);
            DrawRoundRect(rect, color, _panelStyle);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(new Rect(rect.x + S(10f, scale), rect.y + S(2f, scale),
                Mathf.Max(0f, rect.width - S(20f, scale)), S(2f, scale)), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawButtonBackground(Rect rect, float scale, Color accent, bool selected = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Color baseColor = Color.Lerp(PanelColor, accent, selected ? 0.28f : 0.16f);
            if (hover) baseColor = Color.Lerp(baseColor, Color.white, 0.12f);
            DrawPanel(rect, scale, baseColor);
            DrawRoundRect(new Rect(rect.x + S(8f, scale), rect.yMax - S(4f, scale),
                Mathf.Max(0f, rect.width - S(16f, scale)), S(3f, scale)), accent, _panelStyle);
        }

        static void DrawRoundRect(Rect rect, Color color, GUIStyle style)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, style);
            GUI.color = previous;
        }

        void EnsureStyles(float scale)
        {
            EnsureUiTextures();
            if (_titleStyle != null && Mathf.Abs(_uiScale - scale) < 0.001f) return;
            _uiScale = scale;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(28f * scale), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = AccentYellow;

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(14f * scale)),
                alignment = TextAnchor.MiddleCenter,
            };
            _textStyle.normal.textColor = TextColor;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(18f * scale)), fontStyle = FontStyle.Bold,
            };

            _buttonLabelStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(18f * scale)), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            _buttonLabelStyle.normal.textColor = TextColor;
            _buttonLabelStyle.hover.textColor = TextColor;
            _buttonLabelStyle.active.textColor = TextColor;
            _buttonLabelStyle.focused.textColor = TextColor;
        }

        static void EnsureUiTextures()
        {
            if (_roundTexture != null) return;

            _roundTexture = MakeRoundedTexture();
            _panelStyle = new GUIStyle
            {
                normal = new GUIStyleState { background = _roundTexture },
                border = new RectOffset(14, 14, 14, 14),
            };
        }

        static Texture2D MakeRoundedTexture()
        {
            const int size = 64;
            const float radius = 13f;
            const float feather = 2.5f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Caos Pause UI Rounded",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                    var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) -
                            new Vector2(size * 0.5f - radius, size * 0.5f - radius);
                    float distance = Mathf.Min(Mathf.Max(q.x, q.y), 0f) +
                                     new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude -
                                     radius;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance / feather) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
