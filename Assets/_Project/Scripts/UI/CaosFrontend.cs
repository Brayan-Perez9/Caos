using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Caos.Net;
using Caos.Player;

namespace Caos.UI
{
    public enum CaosFrontendScreen
    {
        Loading,
        Lobby,
        Customize
    }

    /// <summary>
    /// Entrada de Arena Caos: carga real de recursos, lobby con tres accesos y
    /// pantalla de personalización sobre el personaje existente. Se añade solo
    /// en la escena Menu, sin rehacer escenas ni sistemas de gameplay.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class CaosFrontend : MonoBehaviour
    {
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1080f;

        static readonly Color Gold = new Color32(255, 211, 96, 255);
        static readonly Color Cyan = new Color32(62, 220, 237, 255);
        static readonly Color Coral = new Color32(255, 112, 120, 255);
        static readonly Color Violet = new Color32(155, 118, 255, 255);
        static readonly Color Ink = new Color32(8, 20, 34, 238);

        public static CaosFrontend Instance { get; private set; }
        public static bool Active => Instance != null;

        public CaosFrontendScreen Screen => _screen;

        CaosFrontendScreen _screen;
        CharacterCustomization _character;
        Transform _preview;
        Transform _pedestal;
        float _previewHomeX;
        float _pedestalHomeX;
        bool _homesResolved;
        int _focus;
        int _tab;

        float _uiScale = 1f;
        float _uiOffsetX;
        float _uiOffsetY;

        GUIStyle _titleStyle;
        GUIStyle _subtitleStyle;
        GUIStyle _sectionStyle;
        GUIStyle _cardKickerStyle;
        GUIStyle _cardTitleStyle;
        GUIStyle _cardBodyStyle;
        GUIStyle _hintStyle;
        GUIStyle _fieldStyle;
        GUIStyle _buttonStyle;
        GUIStyle _panelStyle;
        GUIStyle _cardStyle;
        GUIStyle _creditStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != "Menu") return;
            if (FindFirstObjectByType<CaosFrontend>() != null) return;

            var holder = new GameObject("CaosFrontend");
            holder.AddComponent<CaosFrontend>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _screen = CaosFrontendScreen.Loading;
            CaosFrontendArt.BeginPreload();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => ResolveScene();

        void Update()
        {
            if (CaosSceneLoader.IsLoading) return;

            ResolveScene();
            AnimatePreview();

            if (_screen == CaosFrontendScreen.Loading)
            {
                if (CaosFrontendArt.PreloadFinished) _screen = CaosFrontendScreen.Lobby;
                return;
            }

            HandleInput();
        }

        void ResolveScene()
        {
            if (_character == null)
            {
                _character = CharacterCustomization.Instance != null
                    ? CharacterCustomization.Instance
                    : FindFirstObjectByType<CharacterCustomization>();
            }

            if (_preview == null)
            {
                var previewObject = GameObject.Find("PreviewCharacter");
                if (previewObject != null) _preview = previewObject.transform;
            }

            if (_pedestal == null)
            {
                var pedestalObject = GameObject.Find("Pedestal");
                if (pedestalObject != null) _pedestal = pedestalObject.transform;
            }

            if (!_homesResolved && _preview != null)
            {
                _previewHomeX = _preview.position.x;
                _pedestalHomeX = _pedestal != null ? _pedestal.position.x : _previewHomeX;
                _homesResolved = true;
            }
        }

        void AnimatePreview()
        {
            if (_preview == null) return;

            float targetX = _screen == CaosFrontendScreen.Customize
                ? 0.45f
                : _previewHomeX;
            float blend = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);

            Vector3 position = _preview.position;
            position.x = Mathf.Lerp(position.x, targetX, blend);
            _preview.position = position;

            float spin = _screen == CaosFrontendScreen.Customize ? 14f : 24f;
            _preview.Rotate(0f, spin * Time.unscaledDeltaTime, 0f, Space.World);

            if (_pedestal != null)
            {
                Vector3 pedestalPosition = _pedestal.position;
                pedestalPosition.x = Mathf.Lerp(pedestalPosition.x,
                    _screen == CaosFrontendScreen.Customize ? 0.45f : _pedestalHomeX, blend);
                _pedestal.position = pedestalPosition;
            }
        }

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (_screen == CaosFrontendScreen.Customize)
            {
                bool back = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                    (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
                if (back) BackToLobby();
                return;
            }

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

            if (move != 0) _focus = (_focus + move + 3) % 3;

            bool confirm = (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

            if (confirm) Activate(_focus);
        }

        void Activate(int option)
        {
            switch (option)
            {
                case 0:
                    StartScene("Dungeon", "Preparando la mazmorra...",
                        "Generando el caos...", CaosFrontendArt.Slot.Dungeon);
                    break;
                case 1:
                    StartScene("ArenaCaos", "Preparando las arenas...",
                        "Calentando a los campeones...", CaosFrontendArt.Slot.Arena);
                    break;
                case 2:
                    _screen = CaosFrontendScreen.Customize;
                    break;
            }
        }

        void StartScene(string sceneName, string subtitle, string detail,
            CaosFrontendArt.Slot slot)
        {
            if (CaosSceneLoader.IsLoading) return;

            SaveCharacter();
            CaosSession.ResetForMenu();
            CaosSession.LocalName = _character != null
                ? _character.PlayerName
                : PlayerPrefs.GetString("Caos.PlayerName", "Jugador");

            CaosSceneLoader.Begin(sceneName, "ARENA CAOS", subtitle, detail,
                CaosFrontendArt.Get(slot));
        }

        void OpenMultiplayerLobby()
        {
            if (CaosSceneLoader.IsLoading) return;

            SaveCharacter();
            CaosSession.ResetForMenu();
            CaosSession.LocalName = PlayerPrefs.GetString("Caos.PlayerName", "Jugador");

            CaosSceneLoader.Begin("Lobby", "ARENA CAOS",
                "Conectando con la sala...", "Abriendo el lobby multijugador...",
                CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup));
        }

        void BackToLobby()
        {
            SaveCharacter();
            _screen = CaosFrontendScreen.Lobby;
        }

        void SaveCharacter() => _character?.Save();

        void OnGUI()
        {
            if (CaosSceneLoader.IsLoading) return;

            EnsureStyles();

            _uiScale = Mathf.Min(UnityEngine.Screen.width / ReferenceWidth,
                UnityEngine.Screen.height / ReferenceHeight);
            if (_uiScale <= 0f) return;

            _uiOffsetX = (UnityEngine.Screen.width - ReferenceWidth * _uiScale) * 0.5f;
            _uiOffsetY = (UnityEngine.Screen.height - ReferenceHeight * _uiScale) * 0.5f;

            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(_uiOffsetX, _uiOffsetY, 0f),
                Quaternion.identity, new Vector3(_uiScale, _uiScale, 1f));

            try
            {
                switch (_screen)
                {
                    case CaosFrontendScreen.Loading:
                        DrawLoading();
                        break;
                    case CaosFrontendScreen.Customize:
                        DrawCustomize();
                        break;
                    default:
                        DrawLobby();
                        break;
                }
            }
            finally
            {
                GUI.matrix = previous;
            }
        }

        void DrawLoading()
        {
            var full = new Rect(0f, 0f, ReferenceWidth, ReferenceHeight);
            DrawCover(full, CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup), 1f);
            DrawRect(full, new Color(0.01f, 0.02f, 0.06f, 0.28f));
            DrawRect(new Rect(0f, 740f, ReferenceWidth, 340f),
                new Color(0.01f, 0.02f, 0.06f, 0.78f));

            DrawShadowLabel(new Rect(0f, 300f, ReferenceWidth, 130f),
                "ARENA CAOS", _titleStyle, new Color(0f, 0f, 0f, 0.8f));
            GUI.Label(new Rect(0f, 448f, ReferenceWidth, 56f),
                "CARGANDO EL CAOS", _subtitleStyle);
            GUI.Label(new Rect(0f, 524f, ReferenceWidth, 40f),
                "Cargando recursos visuales...", _hintStyle);

            float progress = CaosFrontendArt.Progress;
            DrawBar(new Rect(560f, 964f, 800f, 20f), progress);
            GUI.Label(new Rect(560f, 920f, 800f, 34f),
                $"{Mathf.RoundToInt(progress * 100f)}%", _cardKickerStyle);

            DrawCredit();
        }

        void DrawLobby()
        {
            var full = new Rect(0f, 0f, ReferenceWidth, ReferenceHeight);
            DrawCover(full, CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup), 0.62f);
            DrawRect(full, new Color(0.02f, 0.03f, 0.07f, 0.20f));
            DrawRect(new Rect(0f, 0f, 1120f, ReferenceHeight),
                new Color(0.02f, 0.03f, 0.07f, 0.24f));

            DrawShadowLabel(new Rect(72f, 44f, 1100f, 116f),
                "ARENA CAOS", _titleStyle, new Color(0f, 0f, 0f, 0.78f));
            GUI.Label(new Rect(82f, 160f, 940f, 48f),
                "ELIGE TU CAOS", _subtitleStyle);
            GUI.Label(new Rect(84f, 210f, 900f, 38f),
                "Tu campeón está listo. Elige dónde se arma el desmadre.", _hintStyle);

            Vector2 mouse = ToReference(Event.current != null
                ? Event.current.mousePosition
                : Vector2.zero);

            DrawCard(0, new Rect(1230f, 196f, 650f, 232f),
                CaosFrontendArt.Get(CaosFrontendArt.Slot.Dungeon), Violet,
                "MODO 01", "MAZMORRA",
                "Salas conectadas, recompensas y un guardián distinto en cada partida.", mouse);
            DrawCard(1, new Rect(1230f, 452f, 650f, 232f),
                CaosFrontendArt.Get(CaosFrontendArt.Slot.Arena), Coral,
                "MODO 02", "ARENAS",
                "Oleadas, jefes y caos directo con la arena que ya conoces.", mouse);
            DrawCard(2, new Rect(1230f, 708f, 650f, 232f),
                CaosFrontendArt.Get(CaosFrontendArt.Slot.Customization), Cyan,
                "TU CAMPEÓN", "PERSONALIZAR PERSONAJE",
                "Mira tu personaje actual y prepara futuras opciones de apariencia.", mouse);

            if (GUI.Button(new Rect(80f, 936f, 360f, 58f),
                    "LOBBY MULTIJUGADOR", _buttonStyle))
            {
                OpenMultiplayerLobby();
            }

            GUI.Label(new Rect(82f, 1000f, 900f, 30f),
                "Teclado: flechas/WASD + ENTER · Ratón: clic", _cardKickerStyle);
            DrawCredit();
        }

        void DrawCard(int index, Rect rect, Texture2D art, Color accent,
            string kicker, string title, string body, Vector2 mouse)
        {
            if (rect.Contains(mouse)) _focus = index;
            bool focused = _focus == index;

            DrawRect(new Rect(rect.x + 6f, rect.y + 8f, rect.width, rect.height),
                new Color(0f, 0f, 0f, 0.34f));
            GUI.Box(rect, GUIContent.none, _cardStyle);

            var artRect = new Rect(rect.x + 10f, rect.y + 10f, 208f, rect.height - 20f);
            DrawCover(artRect, art, 0.95f);
            DrawRect(new Rect(artRect.x, artRect.y, 5f, artRect.height), accent);

            float textX = rect.x + 244f;
            GUI.Label(new Rect(textX, rect.y + 28f, rect.width - 268f, 30f),
                kicker, _cardKickerStyle);
            GUI.Label(new Rect(textX, rect.y + 62f, rect.width - 268f, 64f),
                title, _cardTitleStyle);
            GUI.Label(new Rect(textX, rect.y + 132f, rect.width - 268f, 82f),
                body, _cardBodyStyle);

            if (focused)
            {
                DrawOutline(rect, accent, 4f);
                DrawRect(new Rect(rect.x + 10f, rect.y + rect.height - 14f,
                    rect.width - 20f, 4f), accent);
            }

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) Activate(index);
        }

        void DrawCustomize()
        {
            var full = new Rect(0f, 0f, ReferenceWidth, ReferenceHeight);
            DrawCover(full, CaosFrontendArt.Get(CaosFrontendArt.Slot.Customization), 0.70f);
            DrawRect(full, new Color(0.02f, 0.03f, 0.07f, 0.24f));

            DrawShadowLabel(new Rect(76f, 44f, 1100f, 110f),
                "PERSONALIZAR", _titleStyle, new Color(0f, 0f, 0f, 0.78f));
            GUI.Label(new Rect(84f, 152f, 1000f, 44f),
                "Tu campeón actual · próximamente más opciones", _subtitleStyle);

            var panel = new Rect(1290f, 148f, 550f, 790f);
            GUI.Box(panel, GUIContent.none, _panelStyle);

            GUI.Label(new Rect(panel.x + 28f, panel.y + 24f, panel.width - 56f, 32f),
                "NOMBRE DEL CAMPEÓN", _sectionStyle);

            if (_character != null)
            {
                string current = GUI.TextField(
                    new Rect(panel.x + 28f, panel.y + 62f, panel.width - 56f, 52f),
                    _character.PlayerName, 16, _fieldStyle);
                if (current != _character.PlayerName) _character.SetName(current);
            }

            _tab = OutfitUI.Draw(
                new Rect(panel.x + 24f, panel.y + 136f, panel.width - 48f, panel.height - 230f),
                _character, _tab);

            GUI.Label(new Rect(panel.x + 28f, panel.y + panel.height - 74f,
                    panel.width - 56f, 52f),
                "Más estilos, colores y atuendos llegarán pronto.", _hintStyle);

            if (GUI.Button(new Rect(80f, 940f, 320f, 58f), "VOLVER AL LOBBY", _buttonStyle))
                BackToLobby();

            DrawCredit();
        }

        void DrawCredit()
        {
            GUI.Label(new Rect(28f, 1028f, 320f, 34f), "omar hpta", _creditStyle);
        }

        void DrawBar(Rect rect, float value)
        {
            DrawRect(rect, new Color(1f, 1f, 1f, 0.14f));
            DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height),
                new Color(0.24f, 0.92f, 0.92f, 0.96f));
            DrawRect(new Rect(rect.x, rect.y + rect.height - 4f,
                rect.width * Mathf.Clamp01(value), 4f), Gold);
        }

        void DrawCover(Rect rect, Texture2D texture, float alpha)
        {
            if (texture == null) return;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop, true);
            GUI.color = previous;
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        static void DrawShadowLabel(Rect rect, string text, GUIStyle style, Color shadow)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = shadow;
            GUI.Label(new Rect(rect.x + 4f, rect.y + 5f, rect.width, rect.height),
                text, style);
            style.normal.textColor = previous;
            GUI.Label(rect, text, style);
        }

        Vector2 ToReference(Vector2 screenPoint)
        {
            if (_uiScale <= 0f) return Vector2.zero;
            return new Vector2(
                (screenPoint.x - _uiOffsetX) / _uiScale,
                (screenPoint.y - _uiOffsetY) / _uiScale);
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            var panelTexture = CaosFrontendArt.RoundedTexture(Ink, 22f);
            var cardTexture = CaosFrontendArt.RoundedTexture(
                new Color32(9, 22, 38, 244), 22f);
            var buttonTexture = CaosFrontendArt.RoundedTexture(
                new Color32(32, 74, 104, 248), 18f);
            var buttonHoverTexture = CaosFrontendArt.RoundedTexture(
                new Color32(46, 116, 146, 255), 18f);

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 92,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _titleStyle.normal.textColor = Gold;

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _subtitleStyle.normal.textColor = Cyan;

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _sectionStyle.normal.textColor = new Color(0.88f, 0.92f, 1f, 1f);

            _cardKickerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _cardKickerStyle.normal.textColor = Gold;

            _cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _cardTitleStyle.normal.textColor = Color.white;

            _cardBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _cardBodyStyle.normal.textColor = new Color(0.86f, 0.91f, 0.98f, 1f);

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _hintStyle.normal.textColor = new Color(0.78f, 0.86f, 0.96f, 0.88f);

            _fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(18, 18, 18, 18),
                normal = new GUIStyleState
                {
                    background = buttonTexture,
                    textColor = new Color(0.94f, 0.97f, 1f, 1f)
                },
                hover = new GUIStyleState
                {
                    background = buttonHoverTexture,
                    textColor = Color.white
                },
                active = new GUIStyleState
                {
                    background = buttonHoverTexture,
                    textColor = Gold
                }
            };
            _buttonStyle.focused.textColor = Gold;

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(22, 22, 22, 22),
                normal = new GUIStyleState { background = panelTexture }
            };

            _cardStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(22, 22, 22, 22),
                normal = new GUIStyleState { background = cardTexture }
            };

            _creditStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft
            };
            _creditStyle.normal.textColor = new Color(1f, 1f, 1f, 0.72f);
        }
    }
}
