using UnityEngine;
using Caos.Player;

namespace Caos.UI
{
    /// <summary>
    /// Ayuda de prototipo: controles, FPS y velocidad actual.
    /// Sustituir por un HUD de uGUI cuando haya partida real.
    /// </summary>
    public class PrototypeHud : MonoBehaviour
    {
        [SerializeField] string[] helpLines =
        {
            "WASD  moverte      Espacio  saltar      Shift  correr",
            "Clic izq.  empujar      Rueda del ratón  acercar/alejar      P  personalizar",
        };

        PlayerController _player;
        GUIStyle _style;
        float _fps;
        int _frames;
        float _elapsed;

        void Start()
        {
            _player = FindFirstObjectByType<PlayerController>();
        }

        void Update()
        {
            _frames++;
            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed >= 0.5f)
            {
                _fps = _frames / _elapsed;
                _frames = 0;
                _elapsed = 0f;
            }
        }

        void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.UpperLeft,
                };
                _style.normal.textColor = Color.white;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 560f, 220f));
            GUILayout.Label("PROYECTO CAOS · Sandbox de física", _style);
            GUILayout.Space(6f);

            if (helpLines != null)
            {
                foreach (var line in helpLines) GUILayout.Label(line, _style);
            }

            GUILayout.Space(12f);

            string speed = _player != null ? $"{_player.HorizontalSpeed:0.0} m/s" : "—";
            string state = _player != null && _player.IsGrounded ? "suelo" : "aire";
            GUILayout.Label($"FPS {_fps:0}   ·   Velocidad {speed}   ·   {state}", _style);

            GUILayout.EndArea();
        }
    }
}
