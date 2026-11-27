using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Player;

namespace Caos.UI
{
    /// <summary>
    /// Módulo de personalización. Vive solo en la escena Customize, con su
    /// propio fondo, su cámara y el personaje real girando. No comparte
    /// pantalla con el lobby ni con la carga.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class CaosCustomizeScreen : MonoBehaviour
    {
        CharacterCustomization _character;
        Transform _preview;
        Transform _pedestal;
        float _previewHomeX;
        float _pedestalHomeX;
        bool _homesResolved;
        int _tab;

        float _scale = 1f;
        float _offsetX;
        float _offsetY;

        void Start()
        {
            _character = CharacterCustomization.Instance != null
                ? CharacterCustomization.Instance
                : FindFirstObjectByType<CharacterCustomization>();

            var previewObject = GameObject.Find("PreviewCharacter");
            if (previewObject != null) _preview = previewObject.transform;

            var pedestalObject = GameObject.Find("Pedestal");
            if (pedestalObject != null) _pedestal = pedestalObject.transform;

            if (_preview != null)
            {
                _previewHomeX = _preview.position.x;
                _pedestalHomeX = _pedestal != null ? _pedestal.position.x : _previewHomeX;
                _homesResolved = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (CaosSceneLoader.IsLoading) return;

            ResolveCharacter();
            AnimatePreview();

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool back = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
            if (back) Back();
        }

        void ResolveCharacter()
        {
            if (_character != null) return;
            _character = CharacterCustomization.Instance != null
                ? CharacterCustomization.Instance
                : FindFirstObjectByType<CharacterCustomization>();
        }

        void AnimatePreview()
        {
            if (_preview == null) return;

            float blend = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
            Vector3 position = _preview.position;
            position.x = Mathf.Lerp(position.x, 0.45f, blend);
            _preview.position = position;
            _preview.Rotate(0f, 14f * Time.unscaledDeltaTime, 0f, Space.World);

            if (_pedestal != null)
            {
                Vector3 pedestalPosition = _pedestal.position;
                pedestalPosition.x = Mathf.Lerp(pedestalPosition.x, 0.45f, blend);
                _pedestal.position = pedestalPosition;
            }
        }

        void Back()
        {
            if (CaosSceneLoader.IsLoading) return;
            _character?.Save();

            CaosSceneLoader.Begin(CaosShell.LobbyScene, "ARENA CAOS",
                "Volviendo al lobby...", "Preparando el caos...",
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
                    CaosFrontendArt.Get(CaosFrontendArt.Slot.Customization), 0.70f);
                CaosShellUI.DrawRect(full, new Color(0.02f, 0.03f, 0.07f, 0.24f));

                CaosShellUI.DrawShadowLabel(new Rect(76f, 40f, 1100f, 110f),
                    "PERSONALIZAR", styles.Title, new Color(0f, 0f, 0f, 0.78f));
                GUI.Label(new Rect(84f, 148f, 1000f, 44f),
                    "Tu campeón actual · próximamente más opciones", styles.Subtitle);

                var panel = new Rect(1290f, 140f, 550f, 800f);
                GUI.Box(panel, GUIContent.none, styles.Panel);

                GUI.Label(new Rect(panel.x + 28f, panel.y + 24f,
                        panel.width - 56f, 32f),
                    "NOMBRE DEL CAMPEÓN", styles.Section);

                if (_character != null)
                {
                    string current = GUI.TextField(
                        new Rect(panel.x + 28f, panel.y + 62f, panel.width - 56f, 52f),
                        _character.PlayerName, 16, styles.Field);
                    if (current != _character.PlayerName) _character.SetName(current);
                }

                _tab = OutfitUI.Draw(
                    new Rect(panel.x + 24f, panel.y + 136f,
                        panel.width - 48f, panel.height - 230f),
                    _character, _tab);

                GUI.Label(new Rect(panel.x + 28f, panel.y + panel.height - 74f,
                        panel.width - 56f, 52f),
                    "Más estilos, colores y atuendos llegarán pronto.", styles.Hint);

                if (GUI.Button(new Rect(80f, 936f, 320f, 58f),
                        "VOLVER AL LOBBY", styles.Button)) Back();

                GUI.Label(new Rect(28f, 1028f, 320f, 34f), "omar hpta", styles.Credit);
            }
            finally
            {
                CaosShellUI.Restore(previous);
            }
        }
    }
}
