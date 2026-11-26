using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Player;

namespace Caos.UI
{
    /// <summary>
    /// Pantalla de personalización: nombre y color del personaje.
    /// Se abre al inicio y con la tecla P. Mientras está abierta, el jugador
    /// no puede moverse ni atacar (la cámara sigue viva detrás del panel).
    /// </summary>
    [DefaultExecutionOrder(500)] // se dibuja después que el HUD
    public class CustomizationScreen : MonoBehaviour
    {
        const float PanelWidth = 520f;
        const float PanelHeight = 470f;

        bool _open = false; // la personalización inicial ya ocurre en el menú
        CharacterCustomization _character;
        PlayerController _controller;
        int _tab;

        GUIStyle _titleStyle;
        GUIStyle _sectionStyle;
        GUIStyle _fieldStyle;
        GUIStyle _buttonStyle;
        GUIStyle _hintStyle;

        void Start()
        {
            _character = CharacterCustomization.Instance != null
                ? CharacterCustomization.Instance
                : FindFirstObjectByType<CharacterCustomization>();
            _controller = FindFirstObjectByType<PlayerController>();

            ApplyLock();
        }

        void Update()
        {
            if (_open) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.pKey.wasPressedThisFrame) Open();
        }

        void Open()
        {
            _open = true;
            ApplyLock();
        }

        public void Close()
        {
            if (_character != null) _character.Save();
            _open = false;
            ApplyLock();
        }

        void ApplyLock()
        {
            if (_controller != null) _controller.InputEnabled = !_open;
        }

        // ---------------------------------------------------------- dibujo

        void OnGUI()
        {
            if (!_open) return;

            EnsureStyles();

            // Fondo oscurecido
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previous;

            float x = (Screen.width - PanelWidth) * 0.5f;
            float y = (Screen.height - PanelHeight) * 0.5f;
            var panel = new Rect(x, y, PanelWidth, PanelHeight);

            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(x, y + 18f, PanelWidth, 36f),
                "TU PERSONAJE", _titleStyle);

            // -------------------------------------------------- nombre
            GUI.Label(new Rect(x + 30f, y + 68f, 200f, 22f),
                "NOMBRE", _sectionStyle);

            if (_character != null)
            {
                string current = GUI.TextField(
                    new Rect(x + 30f, y + 92f, PanelWidth - 60f, 34f),
                    _character.PlayerName, 16, _fieldStyle);

                if (current != _character.PlayerName) _character.SetName(current);
            }

            // -------------------------------------------------- atuendo
            _tab = OutfitUI.Draw(
                new Rect(x + 20f, y + 136f, PanelWidth - 40f, PanelHeight - 210f),
                _character, _tab);

            // -------------------------------------------------- botón
            var buttonRect = new Rect(
                x + (PanelWidth - 200f) * 0.5f, y + PanelHeight - 74f, 200f, 44f);

            if (GUI.Button(buttonRect, "LISTO", _buttonStyle)) Close();

            GUI.Label(new Rect(x, y + PanelHeight - 26f, PanelWidth, 20f),
                "P para volver a personalizar", _hintStyle);
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _sectionStyle.normal.textColor = new Color(0.85f, 0.87f, 0.92f);

            _fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
            };
            _hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        }
    }
}
