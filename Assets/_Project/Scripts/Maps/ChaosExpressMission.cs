using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Caos.Net;
using Caos.Player;
using Caos.UI;

namespace Caos.Maps
{
    /// <summary>
    /// Caos Express: tumbar dianas, recoger paquetes y matar al jefe con el
    /// martillo, con monstruos each vez más rápidos y eventos aleatorios.
    /// En multijugador el host manda el progreso y las posturas por TCP.
    /// </summary>
    public class ChaosExpressMission : MonoBehaviour
    {
        enum Phase { Playing, Won, Lost }

        [Header("Partida")]
        [SerializeField] float matchSeconds = 180f;
        [SerializeField] float eventInterval = 20f;

        [Header("Objetivos")]
        [SerializeField] int targetGoal = 3;
        [SerializeField] int parcelGoal = 5;
        [SerializeField] int duckGoal = 3;
        [SerializeField] int bossHitPoints = 180;

        [Header("Referencias")]
        [SerializeField] PlayerController player;
        [SerializeField] Renderer finishRenderer;

        Phase _phase;
        float _timeLeft;
        float _nextEvent;
        float _nextMonster;
        float _eventUntil;
        int _activeEvent = -1;
        int _targets;
        int _parcels;
        int _ducks;
        int _monstersKilled;
        bool _weaponReady;
        bool _weaponSpawned;
        bool _weaponPicked;
        bool _bossAlive;
        string _banner = string.Empty;
        float _bannerUntil;
        string _loseReason = string.Empty;
        Vector3 _originalGravity;

        ChaosHealth _playerHealth;
        Transform _targetMarker;
        Transform _parcelMarker;
        Transform _bossMarker;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Material> _spawnedMaterials = new List<Material>();

        CaosNetPeer _peer;
        CaosNetPlayers _netPlayers;
        float _nextSnapshot;
        float _nextPose;
        readonly Dictionary<string, ChaosRemotePlayer> _remotes =
            new Dictionary<string, ChaosRemotePlayer>();

        GUIStyle _titleStyle;
        GUIStyle _sectionStyle;
        GUIStyle _textStyle;
        GUIStyle _timerStyle;
        GUIStyle _objectiveStyle;
        GUIStyle _doneStyle;
        GUIStyle _markerStyle;
        GUIStyle _bannerStyle;
        GUIStyle _resultStyle;
        GUIStyle _buttonStyle;
        GUIStyle _buttonLabelStyle;

        static readonly Color DarkButton = new Color32(70, 72, 90, 255);
        static readonly Color ReadyButton = new Color32(255, 196, 64, 255);

        static readonly Color PanelColor = new Color32(11, 27, 43, 209);      // #0B1B2B al 82 %
        static readonly Color TextColor = new Color32(247, 251, 255, 255);    // #F7FBFF
        static readonly Color AccentYellow = new Color32(255, 209, 102, 255); // #FFD166
        static readonly Color HealthyGreen = new Color32(143, 227, 136, 255); // #8FE388
        static readonly Color LowRed = new Color32(255, 61, 61, 255);
        static readonly Color Cyan = new Color32(53, 208, 255, 255);
        static readonly Color Coral = new Color32(255, 107, 107, 255);
        static readonly Color Violet = new Color32(123, 97, 255, 255);

        static Texture2D _hudRoundTexture;
        static GUIStyle _hudPanelStyle;
        static float _hudScale = -1f;

        bool IsNetHost => CaosSession.NetworkGame && _peer != null && _peer.IsHost;
        bool IsNetClient => CaosSession.NetworkGame && _peer != null && _peer.IsClient;

        /// <summary>El menú de ESC solo se abre mientras la partida está en curso.</summary>
        public bool CanPause => _phase == Phase.Playing;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player != null) _playerHealth = player.GetComponent<ChaosHealth>();

            if (finishRenderer == null)
            {
                var button = GameObject.Find("FinishButton");
                if (button != null) finishRenderer = button.GetComponent<Renderer>();
            }

            var targets = GameObject.Find("TargetBackdrop");
            var parcels = GameObject.Find("ParcelPlatform");
            if (targets != null) _targetMarker = targets.transform;
            if (parcels != null) _parcelMarker = parcels.transform;

            _peer = CaosNetPeer.Instance;
            if (_peer != null)
            {
                _peer.MessageReceived -= OnNetMessage;
                _peer.MessageReceived += OnNetMessage;
            }

            // El componente compartido dibuja ya a todos los jugadores: evitar
            // cápsulas duplicadas con el protocolo Pose antiguo.
            _netPlayers = FindFirstObjectByType<CaosNetPlayers>();

            _originalGravity = Physics.gravity;
            _timeLeft = matchSeconds;
            _nextEvent = Time.time + eventInterval;
            _nextMonster = Time.time + 14f;
            _phase = Phase.Playing;

            if (player != null) player.ShoveEnabled = false;

            SetButtonColor(DarkButton);
            SpawnMonsters(new[]
            {
                new Vector3(-10f, 0.55f, 3f),
                new Vector3(10f, 0.55f, 3f),
                new Vector3(9f, 0.55f, -7f),
                new Vector3(-9f, 0.55f, -7f),
                new Vector3(4f, 0.55f, 8f),
                new Vector3(-4f, 0.55f, 8f),
            });

            ShowBanner(IsNetHost || IsNetClient
                ? "MULTIJUGADOR · CLIC IZQ.: COGER Y LANZAR · SIGUE EL HUD"
                : "CLIC IZQ.: COGER Y LANZAR · SIGUE EL HUD");
        }

        void OnDestroy()
        {
            if (_peer != null) _peer.MessageReceived -= OnNetMessage;
            Physics.gravity = _originalGravity;
            if (player != null)
            {
                player.SpeedMultiplier = 1f;
                player.JumpMultiplier = 1f;
            }

            foreach (var material in _spawnedMaterials)
            {
                if (material != null) Destroy(material);
            }
        }

        void Update()
        {
            if (!CaosSession.NetworkGame && Keyboard.current != null &&
                Keyboard.current.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            if (IsNetHost) SendSnapshot();
            if (_netPlayers == null) SendPlayerPose();

            if (_phase != Phase.Playing) return;

            if (_playerHealth != null && _playerHealth.IsDead && !IsNetClient)
            {
                End(false, "TE MATARON. MÁS CUIDADO CON LOS MONSTRUOS.");
                return;
            }

            if (!IsNetClient)
            {
                _timeLeft -= Time.deltaTime;
                if (_timeLeft <= 0f)
                {
                    _timeLeft = 0f;
                    End(false, "SE ACABÓ EL TIEMPO.");
                    return;
                }

                if (_activeEvent >= 0 && Time.time >= _eventUntil) EndEvent();
                if (Time.time >= _nextEvent)
                {
                    StartRandomEvent();
                    _nextEvent = Time.time + Mathf.Max(8f, eventInterval + Random.Range(-5f, 5f));
                }

                if (Time.time >= _nextMonster)
                {
                    _nextMonster = Time.time + 14f;
                    var alive = FindObjectsByType<ChaosMonster>(FindObjectsSortMode.None).Length;
                    if (alive < 14)
                    {
                        SpawnMonsters(new[]
                        {
                            new Vector3(Random.Range(-11f, 11f), 0.55f, Random.Range(-8f, 9f)),
                        });
                        ShowBanner("¡OTRO MONSTRUO! CORRE MÁS RÁPIDO");
                    }
                }
            }
        }

        // ---------------------------------------------------------- progreso

        public void TargetDown()
        {
            if (IsNetClient)
            {
                _peer.Send(CaosMessageKind.Goal, "target");
                return;
            }

            _targets++;
            ShowBanner($"¡DIANA CAÍDA!  {_targets}/{targetGoal}");
            CheckFinish();
        }

        public void ParcelCollected()
        {
            if (IsNetClient)
            {
                _peer.Send(CaosMessageKind.Goal, "parcel");
                return;
            }

            _parcels++;
            ShowBanner($"¡PAQUETE RECUPERADO!  {_parcels}/{parcelGoal}");
            CheckFinish();
        }

        public void DuckFound()
        {
            if (IsNetClient)
            {
                _peer.Send(CaosMessageKind.Goal, "duck");
                return;
            }

            _ducks++;
            ShowBanner(_ducks >= duckGoal
                ? "¡LOS TRES PATITOS!  BONO SECRETO"
                : $"¡PATITO SECRETO!  {_ducks}/{duckGoal}");
        }

        public void MonsterKilled()
        {
            if (IsNetClient)
            {
                _peer.Send(CaosMessageKind.Goal, "monster");
                return;
            }

            _monstersKilled++;
            ShowBanner($"¡MONSTRUO ELIMINADO!  {_monstersKilled}");
        }

        public void ApplyBossDamage(float damage)
        {
            if (IsNetClient)
            {
                _peer.Send(CaosMessageKind.BossHit, damage.ToString("F0"));
                return;
            }

            var boss = GameObject.Find("ChaosBoss");
            boss?.GetComponent<ChaosHealth>()?.TakeDamage(damage, "martillazo");
        }

        public void ReachFinish(PlayerController playerController)
        {
            if (_phase != Phase.Playing || playerController == null) return;

            if (!_weaponReady)
            {
                ShowBanner("FALTAN PEDIDOS: MIRA EL HUD (1 y 2)");
                return;
            }

            if (!_weaponSpawned && !IsNetClient)
            {
                SpawnWeapon();
                ShowBanner("¡MARTILLO CONSEGUIDO! GOLPEA AL JEFE CON CLIC IZQ.");
                return;
            }

            if (!_weaponPicked) ShowBanner("EL MARTILLO ESTÁ EN EL ARCO AMARILLO");
            else ShowBanner("¡YA TIENES EL MARTILLO! AL JEFE CON CLIC IZQ.");
        }

        public void WeaponPickedUp()
        {
            if (_weaponPicked) return;

            if (IsNetClient)
            {
                _weaponPicked = true;
                _peer.Send(CaosMessageKind.Weapon, "pick");
                ShowBanner("¡MARTILLO! PREPARANDO JEFE…");
                return;
            }

            _weaponPicked = true;
            SpawnBoss();
            ShowBanner("¡JEFE! GOLPÉALO CON CLIC IZQ. HASTA MATARLO");
        }

        public void BossDefeated()
        {
            if (IsNetClient)
            {
                _peer.Send(CaosMessageKind.BossHit, "9999");
                return;
            }

            End(true);
        }

        public void ShowBanner(string text)
        {
            _banner = text;
            _bannerUntil = Time.time + 2.8f;
        }

        void CheckFinish()
        {
            if (_weaponReady) return;
            if (_targets < targetGoal || _parcels < parcelGoal) return;

            _weaponReady = true;
            SetButtonColor(ReadyButton);
            ShowBanner("PEDIDOS LISTOS: VE AL ARCO Y COGE EL MARTILLO");
        }

        void SetButtonColor(Color color)
        {
            if (finishRenderer == null) return;
            var material = finishRenderer.material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        void End(bool won, string reason = "")
        {
            if (_phase != Phase.Playing) return;
            _phase = won ? Phase.Won : Phase.Lost;
            _loseReason = reason;
            if (!IsNetClient) EndEvent();
            if (player != null) player.InputEnabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ---------------------------------------------------------- enemigos

        void SpawnMonsters(Vector3[] positions)
        {
            foreach (var position in positions)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "ChaosMonster";
                go.transform.position = position;
                go.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                go.GetComponent<Renderer>().material.color = new Color(0.62f, 0.28f, 1f);
                go.GetComponent<Collider>().isTrigger = true;

                for (int i = 0; i < 2; i++)
                {
                    var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    eye.name = "Eye";
                    eye.transform.SetParent(go.transform, false);
                    eye.transform.localPosition = new Vector3(i == 0 ? -0.22f : 0.22f, 0.28f, 0.48f);
                    eye.transform.localScale = Vector3.one * 0.28f;
                    eye.GetComponent<Renderer>().material.color = Color.white;
                    Destroy(eye.GetComponent<Collider>());
                }

                var health = go.AddComponent<ChaosHealth>();
                health.Configure(45f, false);

                var monster = go.AddComponent<ChaosMonster>();
                monster.Configure(6.2f + Random.Range(0f, 1.4f), 16f);
            }
        }

        void SpawnWeapon()
        {
            _weaponSpawned = true;

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "ChaosHammer";
            head.transform.position = new Vector3(0f, 1.5f, 11f);
            head.transform.localScale = new Vector3(0.8f, 0.35f, 0.35f);
            head.GetComponent<Renderer>().material.color = new Color(1f, 0.82f, 0.3f);
            head.GetComponent<Collider>().isTrigger = true;

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(head.transform, false);
            handle.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            handle.transform.localScale = new Vector3(0.12f, 0.9f, 0.12f);
            handle.GetComponent<Renderer>().material.color = new Color(0.45f, 0.25f, 0.15f);
            Destroy(handle.GetComponent<Collider>());

            head.AddComponent<ChaosWeapon>();
        }

        void SpawnBoss()
        {
            _bossAlive = true;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ChaosBoss";
            body.transform.position = new Vector3(0f, 1.3f, 12.5f);
            body.transform.localScale = new Vector3(2.2f, 2.6f, 1.6f);
            body.GetComponent<Renderer>().material.color = new Color(0.85f, 0.2f, 0.25f);
            body.GetComponent<Collider>().isTrigger = true;

            for (int i = 0; i < 2; i++)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = "Eye";
                eye.transform.SetParent(body.transform, false);
                eye.transform.localPosition = new Vector3(i == 0 ? -0.5f : 0.5f, 0.4f, 0.55f);
                eye.transform.localScale = Vector3.one * 0.3f;
                eye.GetComponent<Renderer>().material.color = Color.white;
                Destroy(eye.GetComponent<Collider>());
            }

            var health = body.AddComponent<ChaosHealth>();
            health.Configure(bossHitPoints, false);

            var boss = body.AddComponent<ChaosBoss>();
            boss.Configure(bossHitPoints, this);

            _bossMarker = body.transform;
        }

        // ---------------------------------------------------------- eventos

        void StartRandomEvent()
        {
            _activeEvent = Random.Range(0, 8);
            _eventUntil = Time.time + 9f;

            switch (_activeEvent)
            {
                case 0:
                    ShowBanner("EVENTO: ¡LLUVIA DE CAJAS!");
                    SpawnRain(PrimitiveType.Cube, 6, new Color32(230, 150, 70, 255), 16f);
                    break;
                case 1:
                    ShowBanner("EVENTO: ¡BOLAS RODANTES!");
                    SpawnRain(PrimitiveType.Sphere, 5, new Color32(90, 220, 240, 255), 16f);
                    break;
                case 2:
                    Physics.gravity = new Vector3(0f, -9f, 0f);
                    if (player != null) player.JumpMultiplier = 1.4f;
                    ShowBanner("EVENTO: ¡GRAVEDAD LIGERA!");
                    break;
                case 3:
                    if (player != null) player.SpeedMultiplier = 1.7f;
                    ShowBanner("EVENTO: ¡TURBO!");
                    break;
                case 4:
                    ShowBanner("EVENTO: ¡OLEADA DE MONSTRUOS!");
                    SpawnMonsters(RandomPositions(2));
                    break;
                case 5:
                    ShowBanner("EVENTO: ¡TORMENTA DE METEOROS!");
                    SpawnRain(PrimitiveType.Sphere, 7, new Color32(255, 90, 50, 255), 22f);
                    break;
                case 6:
                    if (player != null)
                    {
                        player.SpeedMultiplier = 0.55f;
                        player.JumpMultiplier = 0.7f;
                    }
                    ShowBanner("EVENTO: ¡PIES DE PLOMO!");
                    break;
                default:
                    ShowBanner("EVENTO: ¡ENJAMBRE! CORRE");
                    SpawnMonsters(RandomPositions(5));
                    break;
            }
        }

        Vector3[] RandomPositions(int count)
        {
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
                positions[i] = new Vector3(
                    Random.Range(-11f, 11f), 0.55f, Random.Range(-8f, 9f));
            return positions;
        }

        void EndEvent()
        {
            if (_activeEvent == 2)
            {
                Physics.gravity = _originalGravity;
                if (player != null) player.JumpMultiplier = 1f;
            }
            else if (_activeEvent == 3 && player != null)
            {
                player.SpeedMultiplier = 1f;
            }
            else if (_activeEvent == 6 && player != null)
            {
                player.SpeedMultiplier = 1f;
                player.JumpMultiplier = 1f;
            }

            _activeEvent = -1;
        }

        void SpawnRain(PrimitiveType type, int count, Color color, float damage)
        {
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = "EventProp";
                go.transform.position = new Vector3(
                    Random.Range(-9f, 9f), 8f + i * 0.7f, Random.Range(-9f, 9f));
                go.transform.localScale = Vector3.one * (type == PrimitiveType.Cube ? 0.8f : 0.7f);

                var body = go.AddComponent<Rigidbody>();
                body.mass = 0.7f;
                body.linearVelocity = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));

                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var material = new Material(shader);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                go.GetComponent<Renderer>().sharedMaterial = material;
                var crush = go.AddComponent<ChaosCrush>();
                crush.Configure(damage);

                _spawned.Add(go);
                _spawnedMaterials.Add(material);
            }
        }

        // ---------------------------------------------------------- red

        void SendSnapshot()
        {
            if (Time.time < _nextSnapshot) return;
            _nextSnapshot = Time.time + 0.12f;

            var boss = GameObject.Find("ChaosBoss");
            float hp = boss != null && boss.TryGetComponent(out ChaosHealth health)
                ? health.Current : 0f;

            string payload = CaosNetMessages.Join(
                _timeLeft.ToString("F1"),
                _targets.ToString(), _parcels.ToString(), _ducks.ToString(),
                _monstersKilled.ToString(), ((int)_phase).ToString(),
                _weaponReady ? "1" : "0", _weaponSpawned ? "1" : "0",
                _weaponPicked ? "1" : "0", _bossAlive ? "1" : "0",
                hp.ToString("F0"), _activeEvent.ToString());
            _peer.Broadcast(CaosMessageKind.Snapshot, payload);
        }

        void SendPlayerPose()
        {
            if (_peer == null || !CaosSession.NetworkGame || player == null) return;
            if (Time.time < _nextPose) return;
            _nextPose = Time.time + 0.08f;

            string payload = CaosNetMessages.Join(
                CaosSession.LocalName,
                player.transform.position.x.ToString("F2"),
                player.transform.position.y.ToString("F2"),
                player.transform.position.z.ToString("F2"),
                player.transform.eulerAngles.y.ToString("F1"),
                _playerHealth != null ? _playerHealth.Current.ToString("F0") : "100");

            if (IsNetHost) _peer.Broadcast(CaosMessageKind.Pose, payload);
            else _peer.Send(CaosMessageKind.Pose, payload);
        }

        void OnNetMessage(CaosMessageKind kind, string payload, string from)
        {
            if (!CaosSession.NetworkGame) return;

            switch (kind)
            {
                case CaosMessageKind.Snapshot:
                    if (!IsNetClient) break;
                    ApplySnapshot(payload);
                    break;

                case CaosMessageKind.Goal:
                    if (!IsNetHost) break;
                    switch (payload)
                    {
                        case "target": TargetDown(); break;
                        case "parcel": ParcelCollected(); break;
                        case "duck": DuckFound(); break;
                        case "monster": MonsterKilled(); break;
                    }
                    break;

                case CaosMessageKind.BossHit:
                    if (!IsNetHost) break;
                    if (float.TryParse(payload, out float damage)) ApplyBossDamage(damage);
                    break;

                case CaosMessageKind.Weapon:
                    if (IsNetHost && payload == "pick") WeaponPickedUp();
                    break;

                case CaosMessageKind.Pose:
                    if (_netPlayers == null) ApplyPose(payload);
                    break;

                case CaosMessageKind.Chat:
                    ShowBanner($"{from}: {payload}");
                    break;
            }
        }

        void ApplySnapshot(string payload)
        {
            string[] parts = CaosNetMessages.Split(payload);
            if (parts.Length < 12) return;

            if (float.TryParse(parts[0], out float time)) _timeLeft = time;
            Int(parts[1], ref _targets);
            Int(parts[2], ref _parcels);
            Int(parts[3], ref _ducks);
            Int(parts[4], ref _monstersKilled);

            if (int.TryParse(parts[5], out int phase))
            {
                var incoming = (Phase)phase;
                if (incoming != Phase.Playing && _phase == Phase.Playing)
                    End(incoming == Phase.Won,
                        incoming == Phase.Won ? "" : "EL ANFITRIÓN TERMINÓ LA PARTIDA.");
            }

            bool weaponReady = parts[6] == "1";
            bool weaponSpawned = parts[7] == "1";
            bool weaponPicked = parts[8] == "1";
            bool bossAlive = parts[9] == "1";

            if (weaponReady && !_weaponReady) _weaponReady = true;

            if (weaponSpawned && !_weaponSpawned) SpawnWeapon();
            if (weaponPicked && !_weaponPicked) _weaponPicked = true;

            if (bossAlive && !_bossAlive) SpawnBoss();
            if (!bossAlive && _bossAlive)
            {
                _bossAlive = false;
                var boss = GameObject.Find("ChaosBoss");
                if (boss != null) Destroy(boss);
            }

            if (int.TryParse(parts[10], out int hp))
            {
                var bossObject = GameObject.Find("ChaosBoss");
                bossObject?.GetComponent<ChaosHealth>()?.SetCurrent(hp);
            }

            Int(parts[11], ref _activeEvent);
        }

        static void Int(string text, ref int target)
        {
            if (int.TryParse(text, out int value)) target = value;
        }

        void ApplyPose(string payload)
        {
            string[] parts = CaosNetMessages.Split(payload);
            if (parts.Length < 5) return;

            string name = parts[0];
            if (string.Equals(name, CaosSession.LocalName, System.StringComparison.OrdinalIgnoreCase))
                return;
            if (!float.TryParse(parts[1], out float x) || !float.TryParse(parts[2], out float y) ||
                !float.TryParse(parts[3], out float z) || !float.TryParse(parts[4], out float yaw))
                return;

            if (!_remotes.TryGetValue(name, out var remote))
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"RemotePlayer_{name}";
                go.transform.position = new Vector3(x, y, z);
                go.transform.localScale = new Vector3(0.55f, 0.8f, 0.55f);
                Destroy(go.GetComponent<Collider>());
                remote = go.AddComponent<ChaosRemotePlayer>();
                remote.Configure(name, RemoteColor(name));
                _remotes.Add(name, remote);
            }

            remote.SetPose(new Vector3(x, y, z), yaw);
        }

        static Color RemoteColor(string name)
        {
            float hue = 0f;
            foreach (char character in name) hue += character * 0.07f;
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), 0.65f, 0.95f);
        }

        void ReturnToLobby()
        {
            _peer?.Shutdown();
            CaosSession.ResetForMenu();
            CaosSceneLoader.Begin(CaosShell.LobbyScene, "ARENA CAOS",
                "Volviendo al lobby...", "Preparando el caos...",
                CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup));
        }

        // ---------------------------------------------------------- interfaz

        void OnGUI()
        {
            float scale = HudScale();
            EnsureStyles(scale);

            float margin = S(16f, scale);
            float width = Mathf.Min(S(680f, scale), Screen.width - margin * 2f);
            float height = S(288f, scale);
            var panel = new Rect(margin, margin, width, height);
            DrawHudPanel(panel, scale, PanelColor);

            float inset = S(14f, scale);
            GUILayout.BeginArea(new Rect(panel.x + inset, panel.y + S(8f, scale),
                panel.width - inset * 2f, panel.height - S(16f, scale)));
            GUILayout.Label("CAOS EXPRESS · PARTIDA RÁPIDA", _titleStyle);
            string room = IsNetHost || IsNetClient ? "  ·  MULTIJUGADOR" : string.Empty;
            GUILayout.Label($"TIEMPO  {FormatTime(_timeLeft)}{room}", _timerStyle);
            GUILayout.Space(S(4f, scale));

            float hp = _playerHealth != null ? _playerHealth.Current / _playerHealth.Max : 0f;
            Color previous = GUI.color;
            GUI.color = hp <= 0.35f ? Coral : TextColor;
            GUILayout.Label(_playerHealth != null
                ? $"VIDA  {Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(_playerHealth.Max)}"
                : "VIDA  --", _textStyle);
            GUI.color = previous;

            var bar = GUILayoutUtility.GetRect(S(120f, scale), S(16f, scale), GUILayout.ExpandWidth(true));
            DrawHudBar(bar, scale, hp, hp <= 0.35f ? LowRed : HealthyGreen);

            GUILayout.Space(S(6f, scale));
            GUILayout.Label("OBJETIVOS", _sectionStyle);
            GUILayout.Label(Objective(1, "Tumbar 3 dianas", _targets, targetGoal),
                _targets >= targetGoal ? _doneStyle : _objectiveStyle);
            GUILayout.Label(Objective(2, "Recoger 5 paquetes", _parcels, parcelGoal),
                _parcels >= parcelGoal ? _doneStyle : _objectiveStyle);
            GUILayout.Label(FinalObjective(), _bossAlive || (_weaponReady && !_weaponSpawned)
                ? _objectiveStyle : (_weaponPicked && !_bossAlive ? _doneStyle : _textStyle));
            GUILayout.Label($"MONSTRUOS ELIMINADOS  {_monstersKilled}   ·   " +
                $"en el mapa {FindObjectsByType<ChaosMonster>(FindObjectsSortMode.None).Length}", _textStyle);
            GUILayout.Label("Clic izq.: coger / lanzar · con martillo: golpear", _textStyle);
            GUILayout.EndArea();

            DrawMarker(_targetMarker, "1 · DIANAS", _targets < targetGoal, scale);
            DrawMarker(_parcelMarker, "2 · PAQUETES", _parcels < parcelGoal, scale);
            DrawMarker(_bossMarker, "JEFE", _bossAlive, scale);
            DrawRemoteNames(scale);

            if (Time.time < _bannerUntil)
            {
                float bannerHeight = S(46f, scale);
                var rect = new Rect(0f, Screen.height * 0.34f, Screen.width, bannerHeight);
                DrawRoundHud(rect, new Color(0.04f, 0.10f, 0.16f, 0.82f), _hudPanelStyle);
                GUI.Label(rect, _banner, _bannerStyle);
            }

            if (_phase == Phase.Playing) return;

            float panelWidth = Mathf.Min(S(540f, scale), Screen.width - margin * 2f);
            float panelHeight = S(260f, scale);
            var result = new Rect((Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
            DrawHudPanel(result, scale, PanelColor);

            float contentX = result.x + S(20f, scale);
            float contentWidth = result.width - S(40f, scale);
            GUI.Label(new Rect(contentX, result.y + S(16f, scale), contentWidth, S(36f, scale)),
                _phase == Phase.Won ? "¡JEFE DERROTADO! MISIÓN COMPLETA" : "MISIÓN FALLIDA",
                _resultStyle);
            if (!string.IsNullOrEmpty(_loseReason))
                GUI.Label(new Rect(contentX, result.y + S(58f, scale), contentWidth, S(28f, scale)),
                    _loseReason, _bannerStyle);
            GUI.Label(new Rect(contentX, result.y + S(92f, scale), contentWidth, S(40f, scale)),
                $"Dianas {_targets}/{targetGoal} · Paquetes {_parcels}/{parcelGoal} · " +
                $"Patitos {_ducks}/{duckGoal} · Monstruos {_monstersKilled}", _textStyle);

            var action = new Rect(contentX, result.y + S(150f, scale), contentWidth, S(48f, scale));
            if (CaosSession.NetworkGame)
            {
                DrawHudButtonBackground(action, scale, Cyan);
                if (GUI.Button(action, "VOLVER AL LOBBY", _buttonLabelStyle)) ReturnToLobby();
            }
            else
            {
                DrawHudButtonBackground(action, scale, AccentYellow);
                if (GUI.Button(action, "REPETIR PARTIDA", _buttonLabelStyle))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                GUI.Label(new Rect(contentX, action.yMax + S(4f, scale), contentWidth, S(26f, scale)),
                    "o pulsa R", _textStyle);
            }
        }

        static string Objective(int number, string label, int current, int goal)
        {
            return current >= goal
                ? $"✔ {number}. {label}  ({current}/{goal})"
                : $"{number}. {label}  — {current}/{goal}";
        }

        string FinalObjective()
        {
            if (_weaponReady && !_weaponSpawned)
                return "3. Ve al arco amarillo y coge el MARTILLO";
            if (_weaponSpawned && !_weaponPicked)
                return "3. El MARTILLO está en el arco amarillo";
            if (_weaponPicked && _bossAlive)
                return "3. MATA AL JEFE con CLIC IZQUIERDO";
            if (_weaponPicked && !_bossAlive)
                return "✔ 3. Jefe derrotado";
            return "3. Martillo y jefe  (termina los pedidos 1-2)";
        }

        void DrawMarker(Transform marker, string text, bool active, float scale)
        {
            if (!active || marker == null || Camera.main == null) return;

            Vector3 screen = Camera.main.WorldToScreenPoint(marker.position + Vector3.up * 2f);
            if (screen.z <= 0f) return;

            float x = screen.x;
            float y = Screen.height - screen.y;
            float edgeX = S(80f, scale);
            float edgeY = S(60f, scale);
            if (x < edgeX || x > Screen.width - edgeX || y < edgeY || y > Screen.height - edgeY) return;

            var rect = new Rect(x - S(80f, scale), y - S(15f, scale), S(160f, scale), S(30f, scale));
            DrawHudPanel(rect, scale, PanelColor);
            GUI.Label(rect, text, _markerStyle);
        }

        void DrawRemoteNames(float scale)
        {
            if (Camera.main == null) return;
            foreach (var remote in _remotes.Values)
            {
                if (remote == null) continue;
                Vector3 screen = Camera.main.WorldToScreenPoint(remote.transform.position + Vector3.up * 1.6f);
                if (screen.z <= 0f) continue;
                var rect = new Rect(screen.x - S(70f, scale), Screen.height - screen.y - S(12f, scale),
                    S(140f, scale), S(24f, scale));
                DrawRoundHud(rect, new Color(0.04f, 0.10f, 0.16f, 0.72f), _hudPanelStyle);
                GUI.Label(rect, remote.PlayerName, _markerStyle);
            }
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        static float HudScale()
        {
            return Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.8f, 1.5f);
        }

        static float S(float pixels, float scale) => pixels * scale;

        static void DrawHudPanel(Rect rect, float scale, Color color)
        {
            DrawRoundHud(new Rect(rect.x + S(2f, scale), rect.y + S(3f, scale), rect.width, rect.height),
                new Color(0f, 0f, 0f, 0.35f), _hudPanelStyle);
            DrawRoundHud(rect, color, _hudPanelStyle);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(new Rect(rect.x + S(10f, scale), rect.y + S(2f, scale),
                Mathf.Max(0f, rect.width - S(20f, scale)), S(2f, scale)), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawHudButtonBackground(Rect rect, float scale, Color accent)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Color baseColor = Color.Lerp(PanelColor, accent, 0.16f);
            if (hover) baseColor = Color.Lerp(baseColor, Color.white, 0.12f);
            DrawHudPanel(rect, scale, baseColor);
            DrawRoundHud(new Rect(rect.x + S(8f, scale), rect.yMax - S(4f, scale),
                Mathf.Max(0f, rect.width - S(16f, scale)), S(3f, scale)), accent, _hudPanelStyle);
        }

        static void DrawHudBar(Rect rect, float scale, float ratio, Color fill)
        {
            DrawRoundHud(rect, new Color(0.05f, 0.07f, 0.11f, 0.95f), _hudPanelStyle);

            float clamped = Mathf.Clamp01(ratio);
            if (clamped <= 0f) return;

            var fillRect = new Rect(rect.x + S(2f, scale), rect.y + S(2f, scale),
                Mathf.Max(S(2f, scale), (rect.width - S(4f, scale)) * clamped),
                rect.height - S(4f, scale));
            DrawRoundHud(fillRect, fill, _hudPanelStyle);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            GUI.DrawTexture(new Rect(fillRect.x + S(3f, scale), fillRect.y + S(2f, scale),
                Mathf.Max(0f, fillRect.width - S(6f, scale)), S(2f, scale)), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawRoundHud(Rect rect, Color color, GUIStyle style)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, style);
            GUI.color = previous;
        }

        static void EnsureHudTextures()
        {
            if (_hudRoundTexture != null) return;

            _hudRoundTexture = MakeRoundedHudTexture();
            _hudPanelStyle = new GUIStyle
            {
                normal = new GUIStyleState { background = _hudRoundTexture },
                border = new RectOffset(14, 14, 14, 14),
            };
        }

        static Texture2D MakeRoundedHudTexture()
        {
            const int size = 64;
            const float radius = 13f;
            const float feather = 2.5f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Caos Express UI Rounded",
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

        void EnsureStyles(float scale)
        {
            EnsureHudTextures();
            if (_titleStyle != null && Mathf.Abs(_hudScale - scale) < 0.001f) return;
            _hudScale = scale;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20f * scale), fontStyle = FontStyle.Bold,
            };
            _titleStyle.normal.textColor = AccentYellow;

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(14f * scale)), fontStyle = FontStyle.Bold,
            };
            _sectionStyle.normal.textColor = AccentYellow;

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(14f * scale)),
            };
            _textStyle.normal.textColor = TextColor;

            _timerStyle = new GUIStyle(_textStyle) { fontStyle = FontStyle.Bold };
            _timerStyle.normal.textColor = Cyan;

            _objectiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(15f * scale)), fontStyle = FontStyle.Bold,
            };
            _objectiveStyle.normal.textColor = TextColor;

            _doneStyle = new GUIStyle(_objectiveStyle);
            _doneStyle.normal.textColor = HealthyGreen;

            _markerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(14f * scale)), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _markerStyle.normal.textColor = TextColor;

            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(23f * scale), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            _bannerStyle.normal.textColor = AccentYellow;

            _resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(27f * scale), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _resultStyle.normal.textColor = AccentYellow;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(18f * scale), fontStyle = FontStyle.Bold,
            };

            _buttonLabelStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(18f * scale)), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _buttonLabelStyle.normal.textColor = TextColor;
            _buttonLabelStyle.hover.textColor = TextColor;
            _buttonLabelStyle.active.textColor = TextColor;
            _buttonLabelStyle.focused.textColor = TextColor;
        }
    }
}
