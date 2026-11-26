using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Caos.Net;
using Caos.Player;

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
        float _nextSnapshot;
        float _nextPose;
        readonly Dictionary<string, ChaosRemotePlayer> _remotes =
            new Dictionary<string, ChaosRemotePlayer>();

        GUIStyle _titleStyle;
        GUIStyle _sectionStyle;
        GUIStyle _textStyle;
        GUIStyle _objectiveStyle;
        GUIStyle _doneStyle;
        GUIStyle _markerStyle;
        GUIStyle _bannerStyle;
        GUIStyle _resultStyle;
        GUIStyle _buttonStyle;

        static readonly Color DarkButton = new Color32(70, 72, 90, 255);
        static readonly Color ReadyButton = new Color32(255, 196, 64, 255);

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
            SendPlayerPose();

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
                    ApplyPose(payload);
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
            SceneManager.LoadScene("Lobby");
        }

        // ---------------------------------------------------------- interfaz

        void OnGUI()
        {
            EnsureStyles();

            float width = Mathf.Min(680f, Screen.width - 32f);
            GUILayout.BeginArea(new Rect(16f, 16f, width, 288f));
            GUILayout.Label("CAOS EXPRESS · PARTIDA RÁPIDA", _titleStyle);
            string room = IsNetHost || IsNetClient ? "  ·  MULTIJUGADOR" : string.Empty;
            GUILayout.Label($"TIEMPO  {FormatTime(_timeLeft)}{room}", _textStyle);
            GUILayout.Space(4f);

            float hp = _playerHealth != null ? _playerHealth.Current / _playerHealth.Max : 0f;
            GUILayout.Label(_playerHealth != null
                ? $"VIDA  {Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(_playerHealth.Max)}"
                : "VIDA  --", _textStyle);
            var bar = GUILayoutUtility.GetRect(width - 20f, 16f);
            GUI.Box(bar, GUIContent.none);
            var fill = new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * Mathf.Clamp01(hp), bar.height - 4f);
            GUI.color = new Color(0.35f, 0.95f, 0.55f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.Space(6f);
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

            DrawMarker(_targetMarker, "1 · DIANAS", _targets < targetGoal);
            DrawMarker(_parcelMarker, "2 · PAQUETES", _parcels < parcelGoal);
            DrawMarker(_bossMarker, "JEFE", _bossAlive);
            DrawRemoteNames();

            if (Time.time < _bannerUntil)
            {
                var rect = new Rect(0f, Screen.height * 0.34f, Screen.width, 46f);
                GUI.Label(rect, _banner, _bannerStyle);
            }

            if (_phase == Phase.Playing) return;

            float panelWidth = Mathf.Min(540f, Screen.width - 40f);
            var panel = new Rect((Screen.width - panelWidth) * 0.5f,
                (Screen.height - 260f) * 0.5f, panelWidth, 260f);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Space(18f);
            GUILayout.Label(_phase == Phase.Won
                ? "¡JEFE DERROTADO! MISIÓN COMPLETA"
                : "MISIÓN FALLIDA", _resultStyle);
            GUILayout.Space(10f);
            if (!string.IsNullOrEmpty(_loseReason)) GUILayout.Label(_loseReason, _bannerStyle);
            GUILayout.Label($"Dianas {_targets}/{targetGoal} · Paquetes {_parcels}/{parcelGoal} · " +
                $"Patitos {_ducks}/{duckGoal} · Monstruos {_monstersKilled}", _textStyle);
            GUILayout.Space(16f);

            if (CaosSession.NetworkGame)
            {
                if (GUILayout.Button("VOLVER AL LOBBY", _buttonStyle, GUILayout.Height(48f)))
                    ReturnToLobby();
            }
            else
            {
                if (GUILayout.Button("REPETIR PARTIDA", _buttonStyle, GUILayout.Height(48f)))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                GUILayout.Label("o pulsa R", _textStyle);
            }
            GUILayout.EndArea();
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

        void DrawMarker(Transform marker, string text, bool active)
        {
            if (!active || marker == null || Camera.main == null) return;

            Vector3 screen = Camera.main.WorldToScreenPoint(marker.position + Vector3.up * 2f);
            if (screen.z <= 0f) return;

            float x = screen.x;
            float y = Screen.height - screen.y;
            if (x < 80f || x > Screen.width - 80f || y < 60f || y > Screen.height - 60f) return;

            var rect = new Rect(x - 80f, y - 15f, 160f, 30f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, text, _markerStyle);
        }

        void DrawRemoteNames()
        {
            if (Camera.main == null) return;
            foreach (var remote in _remotes.Values)
            {
                if (remote == null) continue;
                Vector3 screen = Camera.main.WorldToScreenPoint(remote.transform.position + Vector3.up * 1.6f);
                if (screen.z <= 0f) continue;
                var rect = new Rect(screen.x - 70f, Screen.height - screen.y - 12f, 140f, 24f);
                GUI.Label(rect, remote.PlayerName, _markerStyle);
            }
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold,
            };
            _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
            };
            _sectionStyle.normal.textColor = new Color(1f, 0.75f, 0.4f);

            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _textStyle.normal.textColor = Color.white;

            _objectiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
            };
            _objectiveStyle.normal.textColor = new Color(0.95f, 0.95f, 1f);

            _doneStyle = new GUIStyle(_objectiveStyle);
            _doneStyle.normal.textColor = new Color(0.4f, 1f, 0.6f);

            _markerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _markerStyle.normal.textColor = Color.white;

            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 23, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            _bannerStyle.normal.textColor = new Color(1f, 0.92f, 0.5f);

            _resultStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 27, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _resultStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
            };
        }
    }
}
