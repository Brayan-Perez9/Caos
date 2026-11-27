using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Caos.Net;

namespace Caos.UI
{
    /// <summary>
    /// Lobby de la sala: CREAR SALA genera un código CAOS-XXXXXXXXXX para dar
    /// al amigo; UNIRSE usa ese código. El host decide cuándo arranca la misión.
    /// </summary>
    public class CaosLobby : MonoBehaviour
    {
        const string MissionSceneName = "ChaosExpress";
        const string DungeonSceneName = "Dungeon";
        const string MenuSceneName = "Menu";

        CaosNetPeer _peer;
        string _joinText = string.Empty;
        string _name = "Jugador";
        string _status = "Crea una sala o escribe el código/IP de tu amigo.";
        string _chatText = string.Empty;
        readonly List<string> _chat = new List<string>();
        bool _started;
        bool _autoStart;
        bool _autoDungeon;

        GUIStyle _titleStyle;
        GUIStyle _labelStyle;
        GUIStyle _buttonStyle;
        GUIStyle _smallStyle;

        void Start()
        {
            _peer = CaosNetPeer.Instance != null
                ? CaosNetPeer.Instance
                : FindFirstObjectByType<CaosNetPeer>();
            if (_peer == null)
            {
                var go = new GameObject("CaosNetPeer");
                _peer = go.AddComponent<CaosNetPeer>();
            }

            _name = string.IsNullOrWhiteSpace(CaosSession.LocalName) ? "Jugador" : CaosSession.LocalName;
            _peer.DisplayName = _name;
            _peer.MessageReceived -= OnMessage;
            _peer.MessageReceived += OnMessage;

            var args = Environment.GetCommandLineArgs();
            _autoStart = Array.IndexOf(args, "-autostart") >= 0;
            _autoDungeon = Array.IndexOf(args, "-dungeon") >= 0;

            if (Array.IndexOf(args, "-host") >= 0) CreateRoom();

            if (CaosRoomCode.TryParseCommandLine(out string host, out int port))
            {
                _joinText = CaosRoomCode.Build(host, port);
                Join(host, port);
            }
        }

        void OnDestroy()
        {
            if (_peer != null) _peer.MessageReceived -= OnMessage;
        }

        void OnMessage(CaosMessageKind kind, string payload, string from)
        {
            switch (kind)
            {
                case CaosMessageKind.Hello:
                    if (_peer.IsHost && _peer.PeerNames.Count > 0)
                    {
                        _peer.Broadcast(CaosMessageKind.PeerJoined, payload);
                        Debug.Log($"[Caos] Jugador unido: {payload}");
                        if (_autoStart) StartMission();
                        if (_autoDungeon) StartDungeon();
                    }
                    break;
                case CaosMessageKind.PeerJoined:
                    _chat.Add($"→ {payload} se unió a la sala");
                    TrimChat();
                    break;
                case CaosMessageKind.PeerLeft:
                    _chat.Add($"← {payload} salió de la sala");
                    TrimChat();
                    break;
                case CaosMessageKind.Chat:
                    _chat.Add($"{from}: {payload}");
                    TrimChat();
                    break;
                case CaosMessageKind.Start:
                    if (_peer.IsClient && !_started)
                    {
                        _started = true;
                        CaosSession.Prepare(false, _name);
                        LoadScene(MissionSceneName, "Preparando la misión...",
                            "Generando el caos...", CaosFrontendArt.Slot.Arena);
                    }
                    break;
                case CaosMessageKind.DungeonStart:
                    if (_peer.IsClient && !_started)
                    {
                        _started = true;
                        string[] fields = CaosNetMessages.Split(payload);
                        string playerName = fields.Length > 1 ? fields[1] : _name;
                        if (fields.Length > 0 && int.TryParse(fields[0], out int seed))
                            CaosSession.DungeonSeed = seed;
                        CaosSession.Prepare(false, playerName);
                        LoadScene(DungeonSceneName, "Preparando la mazmorra...",
                            "Generando el caos...", CaosFrontendArt.Slot.Dungeon);
                    }
                    break;
            }
        }

        void TrimChat()
        {
            while (_chat.Count > 6) _chat.RemoveAt(0);
        }

        void CreateRoom()
        {
            if (_peer.StartHost(out string error))
            {
                _peer.DisplayName = _name;
                _status = "Sala creada. Da este código a tu amigo.";
                _chat.Add("Sala creada. Esperando invitados…");
                Debug.Log($"[Caos] Código de sala: {CaosRoomCode.Build(CaosRoomCode.LocalAddress(), _peer.Port)}");
            }
            else
            {
                _status = error;
            }
        }

        void Join(string host, int port)
        {
            if (_peer.Connect(host, port, out string error))
            {
                _status = $"Conectado a la sala de {host}:{port}.";
                _chat.Add("Conectado. Esperando al anfitrión…");
                Debug.Log($"[Caos] Conectado a {host}:{port}");
            }
            else
            {
                _status = $"No se pudo entrar en {host}:{port}. {error}";
                Debug.LogWarning($"[Caos] Fallo al unirse a {host}:{port}: {error}");
            }
        }

        void StartMission()
        {
            if (!_peer.IsHost) return;
            _started = true;
            CaosSession.Prepare(true, _name);
            _peer.Broadcast(CaosMessageKind.Start, _name);
            LoadScene(MissionSceneName, "Preparando la misión...",
                "Generando el caos...", CaosFrontendArt.Slot.Arena);
        }

        void StartDungeon()
        {
            if (!_peer.IsHost) return;
            _started = true;
            int seed = System.Environment.TickCount;
            CaosSession.Prepare(true, _name);
            CaosSession.DungeonSeed = seed;
            _peer.Broadcast(CaosMessageKind.DungeonStart,
                CaosNetMessages.Join(seed.ToString(), _name));
            LoadScene(DungeonSceneName, "Preparando la mazmorra...",
                "Generando el caos...", CaosFrontendArt.Slot.Dungeon);
        }

        void LoadScene(string sceneName, string subtitle, string detail,
            CaosFrontendArt.Slot slot)
        {
            CaosSceneLoader.Begin(sceneName, "ARENA CAOS", subtitle, detail,
                CaosFrontendArt.Get(slot));
        }

        void OnGUI()
        {
            if (CaosSceneLoader.IsLoading) return;

            EnsureStyles();

            float width = Mathf.Min(700f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            float y = 40f;

            GUI.Label(new Rect(x, y, width, 46f), "LOBBY CAOS · SALA PRIVADA", _titleStyle);
            y += 58f;

            GUI.Label(new Rect(x, y, 120f, 30f), "NOMBRE", _labelStyle);
            string newName = GUI.TextField(new Rect(x + 120f, y, 220f, 32f), _name, 16);
            if (newName != _name)
            {
                _name = newName;
                _peer.DisplayName = _name;
            }

            string role = _peer.IsHost ? "ANFITRIÓN" : (_peer.IsClient ? "INVITADO" : "SIN SALA");
            GUI.Label(new Rect(x + 370f, y, width - 370f, 32f), $"ESTADO: {role}", _labelStyle);
            y += 44f;

            if (GUI.Button(new Rect(x, y, 200f, 42f), "CREAR SALA", _buttonStyle)) CreateRoom();

            if (_peer.IsHost)
            {
                string code = CaosRoomCode.Build(CaosRoomCode.LocalAddress(), _peer.Port);
                GUI.Label(new Rect(x + 210f, y - 4f, 150f, 26f), "CÓDIGO DE SALA", _labelStyle);
                GUI.Label(new Rect(x + 210f, y + 18f, 260f, 30f), code, _titleStyle);
                if (GUI.Button(new Rect(x + width - 160f, y, 160f, 42f), "COPIAR CÓDIGO"))
                {
                    GUIUtility.systemCopyBuffer = code;
                    _status = "Código copiado. Envíalo a tu amigo por chat.";
                }
            }
            else
            {
                _joinText = GUI.TextField(new Rect(x + 210f, y, width - 410f, 42f), _joinText);
                if (GUI.Button(new Rect(x + width - 190f, y, 190f, 42f), "UNIRSE"))
                {
                    if (CaosRoomCode.TryParse(_joinText, out string host, out int port)) Join(host, port);
                    else _status = "Dato no válido. Usa CAOS-XXXXXXXXXX o IP:PUERTO (ej. 192.168.1.5:7777).";
                }
            }
            y += 54f;

            GUI.Label(new Rect(x, y, width, 44f), _status, _labelStyle);
            y += 48f;

            if (_peer.IsHost)
            {
                var addresses = CaosRoomCode.LocalAddresses();
                string list = string.Empty;
                for (int i = 0; i < addresses.Count; i++)
                {
                    if (i > 0) list += " · ";
                    list += addresses[i] + ":" + _peer.Port;
                }

                GUI.Label(new Rect(x, y, width, 42f),
                    $"IP del anfitrión: {list}", _smallStyle);
                y += 42f;
            }

            GUI.Label(new Rect(x, y, width, 28f),
                "Hasta 4 jugadores. Si falla, usa IP:PUERTO y permite Caos.exe en el firewall.",
                _smallStyle);
            y += 30f;

            int partySize = _peer.IsHost ? _peer.PeerNames.Count + 1 : (_peer.IsClient ? 2 : 1);
            GUI.Label(new Rect(x, y, width, 28f),
                $"JUGADORES EN LA SALA: {partySize}/4", _labelStyle);
            y += 32f;

            string names = _peer.IsHost
                ? _name + (_peer.PeerNames.Count > 0 ? " · " + string.Join(" · ", _peer.PeerNames) : "")
                : _name + " · Anfitrión";
            GUI.Box(new Rect(x, y, width, 46f), names, _labelStyle);
            y += 58f;

            foreach (string line in _chat)
            {
                GUI.Label(new Rect(x, y, width, 22f), line, _smallStyle);
                y += 22f;
            }
            y += 6f;

            if (_peer.IsHost)
            {
                float third = (width - 8f) / 3f;
                if (GUI.Button(new Rect(x, y, third, 46f), "INICIAR MISIÓN", _buttonStyle))
                    StartMission();
                if (GUI.Button(new Rect(x + third + 4f, y, third, 46f), "INICIAR MAZMORRA", _buttonStyle))
                    StartDungeon();
                if (GUI.Button(new Rect(x + (third + 4f) * 2f, y, third, 46f), "ENVIAR MENSAJE"))
                {
                    if (!string.IsNullOrWhiteSpace(_chatText))
                    {
                        _peer.Broadcast(CaosMessageKind.Chat, _chatText);
                        _chat.Add($"{_name}: {_chatText}");
                        _chatText = string.Empty;
                        TrimChat();
                    }
                }
            }
            else
            {
                _chatText = GUI.TextField(new Rect(x, y, width - 150f, 40f), _chatText);
                if (GUI.Button(new Rect(x + width - 140f, y, 140f, 40f), "ENVIAR") &&
                    !string.IsNullOrWhiteSpace(_chatText))
                {
                    _peer.Send(CaosMessageKind.Chat, _chatText);
                    _chat.Add($"{_name}: {_chatText}");
                    _chatText = string.Empty;
                    TrimChat();
                }
            }
            y += 54f;

            if (GUI.Button(new Rect(x, y, 200f, 38f), "VOLVER AL MENÚ"))
            {
                _peer.Shutdown();
                CaosSession.ResetForMenu();
                SceneManager.LoadScene(MenuSceneName);
            }
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _labelStyle.normal.textColor = Color.white;

            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _smallStyle.normal.textColor = new Color(0.85f, 0.87f, 0.95f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
            };
        }
    }
}
