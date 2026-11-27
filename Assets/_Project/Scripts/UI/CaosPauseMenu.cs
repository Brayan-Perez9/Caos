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
        bool _open;
        PlayerController _player;
        GUIStyle _titleStyle;
        GUIStyle _buttonStyle;
        GUIStyle _textStyle;

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
            EnsureStyles();

            float width = Mathf.Min(420f, Screen.width - 40f);
            float height = 410f;
            var panel = new Rect((Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Space(24f);
            GUILayout.Label("PAUSA", _titleStyle);
            GUILayout.Space(8f);
            GUILayout.Label("ESC para volver al juego", _textStyle);
            GUILayout.Space(24f);

            if (GUILayout.Button("REANUDAR", _buttonStyle, GUILayout.Height(46f))) SetOpen(false);
            GUILayout.Space(8f);

            var camera = Camera.main != null ? Camera.main.GetComponent<PlayerCamera>() : null;
            bool reduceMotion = camera != null && camera.ReduceMotion;
            if (GUILayout.Button(reduceMotion ? "CÁMARA: MENOS MOVIMIENTO ✓"
                    : "CÁMARA: MENOS MOVIMIENTO", _buttonStyle, GUILayout.Height(38f)))
            {
                camera?.ToggleReduceMotion();
            }
            GUILayout.Space(6f);
            if (GUILayout.Button(ArenaJuice.ReduceFlashing ? "DESTELLOS: REDUCIDOS ✓"
                    : "DESTELLOS: REDUCIDOS", _buttonStyle, GUILayout.Height(38f)))
            {
                ArenaJuice.ReduceFlashing = !ArenaJuice.ReduceFlashing;
            }
            GUILayout.Space(10f);

            if (GUILayout.Button("VOLVER AL LOBBY", _buttonStyle, GUILayout.Height(46f))) ReturnToLobby();
            GUILayout.Space(8f);
            if (GUILayout.Button("SALIR DEL JUEGO", _buttonStyle, GUILayout.Height(46f))) QuitGame();
            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleCenter,
            };
            _textStyle.normal.textColor = Color.white;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
            };
        }
    }
}
