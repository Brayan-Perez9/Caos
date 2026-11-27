using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Caos.Player;
using Caos.Net;

namespace Caos.UI
{
    /// <summary>
    /// Pantalla de inicio (escena Menu): el personaje girando a la izquierda,
    /// la paleta de colores a la derecha y el campo de nombre abajo.
    /// Al pulsar JUGAR se guarda la personalización y se carga el tablero.
    /// </summary>
    public class CustomizationMenu : MonoBehaviour
    {
        const float BottomHeight = 124f;
        const string GameSceneName = "Sandbox";
        const string MissionSceneName = "ChaosExpress";
        const string ArenaSceneName = "ArenaCaos";
        const string DungeonSceneName = "Dungeon";
        const string LobbySceneName = "Lobby";

        CharacterCustomization _character;
        Transform _preview;
        int _tab;

        GUIStyle _titleStyle;
        GUIStyle _sectionStyle;
        GUIStyle _fieldStyle;
        GUIStyle _playStyle;

        void Start()
        {
            _character = CharacterCustomization.Instance != null
                ? CharacterCustomization.Instance
                : FindFirstObjectByType<CharacterCustomization>();

            // Atajo de línea de comandos: -host crea sala, -code=... entra con código.
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument == "-host" ||
                    (argument != null && argument.StartsWith("-code=", StringComparison.OrdinalIgnoreCase)))
                {
                    OpenLobby();
                    return;
                }
            }

            if (_character != null) _preview = _character.transform;

            // Ratón libre (no hay juego detrás todavía).
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (CaosShell.IsBlocked) return;

            // Vitrina: el personaje gira despacio.
            if (_preview != null)
                _preview.Rotate(0f, 24f * Time.deltaTime, 0f, Space.World);
        }

        void Play() => LeaveTo(GameSceneName, "Preparando el tablero...",
            "Abriendo el mundo de Arena Caos...", CaosFrontendArt.Slot.Startup);

        void PlayMission() => LeaveTo(MissionSceneName, "Preparando la misión...",
            "Generando el caos...", CaosFrontendArt.Slot.Arena);

        void PlayArena() => LeaveTo(ArenaSceneName, "Preparando las arenas...",
            "Calentando a los campeones...", CaosFrontendArt.Slot.Arena);

        void PlayDungeon() => LeaveTo(DungeonSceneName, "Preparando la mazmorra...",
            "Generando el caos...", CaosFrontendArt.Slot.Dungeon);

        void OpenLobby()
        {
            if (_character != null) _character.Save();
            CaosSession.ResetForMenu();
            CaosSession.LocalName = _character != null ? _character.PlayerName : "Jugador";
            CaosSceneLoader.Begin(LobbySceneName, "ARENA CAOS",
                "Conectando con la sala...", "Abriendo el lobby multijugador...",
                CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup));
        }

        void LeaveTo(string sceneName, string subtitle, string detail,
            CaosFrontendArt.Slot slot)
        {
            if (CaosFrontend.Active) return;
            if (_character != null) _character.Save();
            CaosSession.ResetForMenu();
            CaosSession.LocalName = _character != null
                ? _character.PlayerName
                : PlayerPrefs.GetString("Caos.PlayerName", "Jugador");
            CaosSceneLoader.Begin(sceneName, "ARENA CAOS", subtitle, detail,
                CaosFrontendArt.Get(slot));
        }

        // ---------------------------------------------------------- dibujo

        void OnGUI()
        {
            // Legacy: el armazón nueva tiene su propia escena de personalización.
            if (CaosShell.IsBlocked) return;

            EnsureStyles();

            float panelWidth = Mathf.Clamp(Screen.width * 0.42f, 420f, 560f);
            float panelX = Screen.width - panelWidth;
            float panelHeight = Screen.height - BottomHeight;

            // -------------------------------------------- panel derecho
            var panel = new Rect(panelX, 0f, panelWidth, panelHeight);
            DrawRect(panel, new Color(0.05f, 0.06f, 0.09f, 0.72f));

            GUI.Label(new Rect(panelX, 34f, panelWidth, 40f),
                "PERSONAJE", _titleStyle);

            _tab = OutfitUI.Draw(
                new Rect(panelX + 20f, 84f, panelWidth - 40f, panelHeight - 200f),
                _character, _tab);

            // -------------------------------------------- barra inferior
            var bottom = new Rect(0f, panelHeight, Screen.width, BottomHeight);
            DrawRect(bottom, new Color(0.04f, 0.05f, 0.08f, 0.88f));

            float fieldY = panelHeight + (BottomHeight - 42f) * 0.5f;
            var playRect = new Rect(panelX + 30f, panelHeight + 30f, panelWidth - 60f, 50f);

            GUI.Label(new Rect(40f, fieldY + 4f, 130f, 30f), "NOMBRE", _sectionStyle);

            if (_character != null)
            {
                float fieldWidth = Mathf.Clamp(playRect.x - 180f - 30f, 160f, 430f);
                string current = GUI.TextField(
                    new Rect(180f, fieldY, fieldWidth, 42f),
                    _character.PlayerName, 16, _fieldStyle);

                if (current != _character.PlayerName) _character.SetName(current);
            }

            const float gap = 8f;
            float quarter = (playRect.width - gap * 3f) / 4f;
            var missionRect = new Rect(playRect.x, playRect.y, quarter, playRect.height);
            var arenaRect = new Rect(playRect.x + quarter + gap, playRect.y, quarter, playRect.height);
            var dungeonRect = new Rect(playRect.x + (quarter + gap) * 2f, playRect.y, quarter, playRect.height);
            var sandboxRect = new Rect(playRect.x + (quarter + gap) * 3f, playRect.y, quarter, playRect.height);

            if (GUI.Button(missionRect, "MISIÓN", _playStyle)) PlayMission();
            if (GUI.Button(arenaRect, "ARENA", _playStyle)) PlayArena();
            if (GUI.Button(dungeonRect, "MAZMORRA", _playStyle)) PlayDungeon();
            if (GUI.Button(sandboxRect, "JUGAR", _playStyle)) Play();
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
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
                fontSize = 15,
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

            _playStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
            };
        }
    }
}
