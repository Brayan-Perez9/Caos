using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Caos.Maps;
using Caos.Net;
using Caos.Player;

namespace Caos.Dungeon
{
    /// <summary>
    /// Director de la mazmorra. Genera una ruta con semilla, crea las salas
    /// conectadas, gestiona transiciones, recompensas y victoria/derrota.
    /// Reutiliza el combate de Arena a través de DungeonCombatBridge.
    /// </summary>
    public class DungeonDirector : MonoBehaviour
    {
        [Header("Generación")]
        [SerializeField] DungeonConfig config;
        [SerializeField] int seedOverride = -1;
        [SerializeField] int extraRooms = 0;

        [Header("Jugador")]
        [SerializeField] float startHealth = 130f;

        DungeonCombatBridge _bridge;
        DungeonHud _hud;
        DungeonNetSync _netSync;
        CaosNetPlayers _netPlayers;
        Caos.UI.CaosPauseMenu _pauseMenu;
        Caos.UI.CustomizationScreen _customizationScreen;
        PlayerController _player;
        ChaosHealth _health;
        Transform _spawnPoint;

        DungeonRunState _state;
        DungeonRoom[] _rooms;
        System.Random _rng;

        int _currentIndex;
        int _pendingRoomIndex = -1;
        int _seed;
        bool _finished;
        bool _victory;
        bool _transition;
        float _transitionTimer;
        float _bannerTime;
        float _damageFlash;
        string _bannerText = "";

        public bool Finished => _finished;
        public bool Victory => _victory;
        public bool TransitionActive => _transition;
        public bool Paused => _pauseMenu != null && _pauseMenu.IsOpen;
        public bool CanPause => !_finished && !_transition;
        public bool InputBlockedByUi => Paused ||
            (_customizationScreen != null && _customizationScreen.IsOpen);
        bool RemoteClient => _netSync != null && _netSync.Connected && _netSync.IsClient;

        public DungeonConfig Config => config;
        public DungeonRunState State => _state;
        public DungeonCombatBridge Bridge => _bridge;
        /// <summary>Alias usado por las salas heredadas de la primera versión.</summary>
        public DungeonCombatBridge Combat => _bridge;
        public DungeonNetSync NetSync => _netSync;
        /// <summary>Jugadores presentes en la partida (local + remotos visibles).</summary>
        public int PartySize => Mathf.Clamp(1 + (_netPlayers != null ? _netPlayers.RemoteCount : 0), 1, 4);
        /// <summary>Multiplicador de densidad, vida y daño según el tamaño del grupo.</summary>
        public float DifficultyScale => 1f + 0.45f * (PartySize - 1);
        public PlayerController Player => _player;
        public ChaosHealth Health => _health;
        public DungeonRoom[] Rooms => _rooms;
        public int CurrentRoomNumber => _currentIndex + 1;
        public int TotalRooms => _rooms != null ? _rooms.Length : 0;
        public int Seed => _seed;

        public DungeonRoom CurrentRoom => _rooms != null && _currentIndex >= 0
            && _currentIndex < _rooms.Length ? _rooms[_currentIndex] : null;

        public string BannerText => _bannerTime > 0f ? _bannerText : "";
        public float DamageFlash => Mathf.Clamp01(_damageFlash / 0.35f);

        public void SetPlayer(PlayerController player) => _player = player;
        public void SetHud(DungeonHud hud) => _hud = hud;

        void Start()
        {
            // Clonamos el asset: la elección aleatoria del jefe no debe
            // persistirse en el .asset compartido entre partidas.
            config = config != null
                ? Instantiate(config)
                : ScriptableObject.CreateInstance<DungeonConfig>();
            config.ApplyDefaults();

            // En partida en red el anfitrión reparte la semilla del lobby.
            int seed = CaosSession.DungeonSeed != 0 ? CaosSession.DungeonSeed
                : seedOverride >= 0 ? seedOverride : config.seed;
            _seed = seed;
            _rng = new System.Random(seed);
            _state = new DungeonRunState();

            // El guardián final sale del pool de jefes de Arena; la elección
            // depende de la semilla para que la partida sea reproducible.
            DungeonRoomDefinition bossDefinition = config.Find(DungeonRoomKind.Jefe);
            if (bossDefinition != null)
            {
                ArenaBossKind[] bossPool =
                {
                    ArenaBossKind.Martillo, ArenaBossKind.Canion, ArenaBossKind.Tormenta
                };
                var bossRng = new System.Random(unchecked(seed ^ 0x4a6b2c));
                bossDefinition.bossKind = bossPool[bossRng.Next(bossPool.Length)];
            }

            _player = _player != null ? _player : FindFirstObjectByType<PlayerController>();
            _health = _player != null
                ? _player.GetComponent<ChaosHealth>() : FindFirstObjectByType<ChaosHealth>();
            _bridge = GetComponent<DungeonCombatBridge>();
            if (_bridge == null) _bridge = FindFirstObjectByType<DungeonCombatBridge>();
            _hud = _hud != null ? _hud : FindFirstObjectByType<DungeonHud>();
            _netSync = FindFirstObjectByType<DungeonNetSync>();
            if (_netSync != null) _netSync.Configure(this);
            _netPlayers = FindFirstObjectByType<CaosNetPlayers>();
            _pauseMenu = FindFirstObjectByType<Caos.UI.CaosPauseMenu>();
            _customizationScreen = FindFirstObjectByType<Caos.UI.CustomizationScreen>();

            if (_bridge == null)
            {
                GameObject bridgeObject = new GameObject("DungeonCombatBridge");
                bridgeObject.transform.SetParent(transform, false);
                _bridge = bridgeObject.AddComponent<DungeonCombatBridge>();
            }

            _bridge.Configure(this, _player, _health, _state);

            // En Arena el aim controller encuentra al director solo; aquí no
            // existe ArenaDirector, así que le inyectamos el puente de combate.
            ArenaAimController aim = _player != null
                ? _player.GetComponent<ArenaAimController>()
                : FindFirstObjectByType<ArenaAimController>();
            if (aim != null) aim.Configure(_bridge);

            if (_health != null) _health.Configure(Mathf.Max(30f, startHealth), true);
            if (_hud != null) _hud.Configure(this);

            CreateSpawnPoint();
            WireCamera();

            // Tres salas extra son parte del recorrido base: la mazmorra no se
            // acorta aunque la escena venga de una versión anterior del builder.
            List<DungeonRoomKind> route = DungeonRoute.Build(seed, Mathf.Max(3, extraRooms));
            if (!DungeonRoute.IsValid(route))
                route = new List<DungeonRoomKind>(DungeonRoute.Required);

            _rooms = DungeonWorld.Build(this, config, route);
            BeginRoom(0);
        }

        void CreateSpawnPoint()
        {
            if (_player == null) return;
            GameObject spawn = new GameObject("DungeonSpawn");
            spawn.transform.SetParent(transform, false);
            _spawnPoint = spawn.transform;
            _player.SetSpawnPoint(_spawnPoint);
        }

        void WireCamera()
        {
            if (_player == null || Camera.main == null) return;
            PlayerCamera follow = Camera.main.GetComponent<PlayerCamera>();
            if (follow != null) follow.SetTarget(_player.transform);
            _player.SetCamera(Camera.main.transform);
        }

        void BeginRoom(int index)
        {
            if (_rooms == null || index < 0 || index >= _rooms.Length) return;

            _currentIndex = index;
            DungeonRoom room = _rooms[index];

            if (_player != null && _spawnPoint != null)
            {
                _spawnPoint.position = room.EntryPoint + Vector3.up * 1.2f;
                _player.Respawn(_spawnPoint.position);
            }

            _bridge.SetWeapon(room.Definition.weapon, room.Definition.element);
            room.Begin();
            Debug.Log($"[Caos] SALA {index + 1}/{_rooms.Length}: {room.Title}");
            ShowBanner($"SALA {index + 1}/{_rooms.Length} — {room.Title}", 2.8f);
        }

        void Update()
        {
            float unscaled = Time.unscaledDeltaTime;
            if (_bannerTime > 0f) _bannerTime -= unscaled;
            if (_damageFlash > 0f) _damageFlash -= unscaled;

            if (_finished)
            {
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (Paused) return;

            if (_transition)
            {
                _transitionTimer -= unscaled;
                if (_transitionTimer <= 0f) FinishTransition();
                return;
            }

            DungeonRoom room = CurrentRoom;
            if (room == null) return;

            room.Tick(Time.deltaTime);
            if (_finished || _transition) return;

            if (room.Completed && _player != null &&
                Vector3.Distance(_player.transform.position, room.ExitPoint) < 2.2f)
            {
                AdvanceRoom();
            }
        }

        void AdvanceRoom()
        {
            int next = _currentIndex + 1;
            if (next >= _rooms.Length)
            {
                // En red el host decide la victoria; el invitado espera su mensaje.
                bool authority = _netSync == null || !_netSync.Connected || _netSync.IsHost;
                if (authority) FinishRun(true);
                return;
            }

            CurrentRoom?.End();
            _bridge.ClearEnemies();
            _pendingRoomIndex = next;
            _transition = true;
            _transitionTimer = Mathf.Max(0.2f, config.roomTransitionSeconds);
            if (_player != null) _player.InputEnabled = false;
            _netSync?.NotifyRoom(next);
            ShowBanner("ABRIENDO LA SIGUIENTE PUERTA…", _transitionTimer);
        }

        void FinishTransition()
        {
            int next = _pendingRoomIndex;
            _pendingRoomIndex = -1;
            _transition = false;
            BeginRoom(next);
            if (_player != null && !InputBlockedByUi) _player.InputEnabled = true;
        }

        void FinishRun(bool won)
        {
            if (_finished) return;
            _finished = true;
            _victory = won;
            _netSync?.NotifyFinish(won);

            CurrentRoom?.End();
            _bridge.ClearEnemies();
            if (_player != null) _player.InputEnabled = false;

            if (won)
            {
                int bonus = _state.AddCoins(40);
                int runCoins = _state.Coins;
                int saved = CaosProfile.AddCoins(runCoins);
                Debug.Log($"[Caos] PROGRESO: run completada, +{bonus} monedas, banco={saved}");
                ShowBanner($"¡MAZMORRA COMPLETADA! +{bonus} monedas · " +
                    $"{runCoins} guardadas ({saved} en total) · R para repetir", 8f);
                ArenaAudio.Play(ArenaAudio.Sound.Upgrade, transform.position, 1f);
            }
            else
            {
                int rescued = Mathf.RoundToInt(_state.Coins * 0.5f);
                int saved = CaosProfile.AddCoins(rescued);
                Debug.Log($"[Caos] PROGRESO: derrota, {rescued} monedas rescatadas, banco={saved}");
                ShowBanner($"DERROTA — {rescued} monedas rescatadas ({saved} en total) · R para repetir",
                    8f);
                ArenaAudio.Play(ArenaAudio.Sound.Hurt, transform.position, 0.9f);
            }
        }

        // ------------------------------------------------ eventos de combate

        public void OnEnemyKilled(Vector3 position)
        {
            if (_finished) return;
            _state.AddCoins(2);
            ChaosFx.Burst(position + Vector3.up, DungeonPalette.UiYellow, 6);
            CurrentRoom?.NotifyEnemyKilled(position);
        }

        public void OnBossDefeated(ArenaBoss boss)
        {
            if (_finished) return;
            _state.AddCoins(30);
            ChaosFx.Burst(boss != null ? boss.transform.position : transform.position,
                DungeonPalette.UiYellow, 28);
            ArenaJuice.Current.AddTrauma(0.5f);
            CurrentRoom?.NotifyBossDefeated();
        }

        public void OnPlayerDamaged(float amount) => _damageFlash = 0.35f;

        public void OnPlayerDefeated()
        {
            if (_finished) return;
            DungeonRoom room = CurrentRoom;
            if (room != null) room.Fail("Has caído en combate");
            else FinishRun(false);
        }

        public void OnRoomCompleted(DungeonRoom room)
        {
            if (_finished) return;
            _state.RoomsCleared++;
            int gained = _state.AddCoins(config.roomClearCoins);
            // La ruta es más larga y los cañones castigan quedarse quieto: cada
            // sala limpia devuelve un poco de vida para que la run sea exigente
            // pero no una carrera contra la muerte.
            _health?.Heal(16);
            ArenaAudio.Play(ArenaAudio.Sound.Upgrade, room.Center, 0.55f);
            ShowBanner($"{room.Title} completada · +{gained} monedas · +16 vida", 2.4f);
            if (RemoteClient) _netSync.SendGoal(_currentIndex);
        }

        public void OnRoomFailed(DungeonRoom room, string reason)
        {
            if (_finished) return;
            ShowBanner($"FALLO: {reason}", 3f);
            if (RemoteClient)
            {
                _netSync.SendDefeat(_currentIndex);
                if (_player != null) _player.InputEnabled = false;
                return; // el host difunde la derrota confirmada
            }
            FinishRun(false);
        }

        // ------------------------------------------------------- utilidades

        public void SetWeapon(ArenaWeaponKind kind, ArenaElement element) =>
            _bridge?.SetWeapon(kind, element);

        public Vector3 ClampToRoom(Vector3 point)
        {
            DungeonRoom room = CurrentRoom;
            return room != null ? room.Clamp(point) : point;
        }

        public ArenaMonster SpawnMonster(Vector3 position, float hitPoints,
            float speed, float damage)
        {
            if (_bridge == null) return null;
            DungeonRoom room = CurrentRoom;
            position = room != null ? room.Clamp(position) : new Vector3(position.x, 0.95f, position.z);
            return _bridge.SpawnMonster(position, hitPoints, speed, damage);
        }

        public ArenaMonster SpawnVariant(Vector3 position, DungeonEnemyKind kind,
            float hitPoints, float speed, float damage)
        {
            if (_bridge == null) return null;
            DungeonRoom room = CurrentRoom;
            position = room != null ? room.Clamp(position) : new Vector3(position.x, 0.95f, position.z);
            return _bridge.SpawnVariant(position, kind, hitPoints, speed, damage);
        }

        public ArenaBoss SpawnBoss(Vector3 position, ArenaBossKind kind, int index,
            float hitPoints, float speed, float damage)
        {
            if (_bridge == null) return null;
            DungeonRoom room = CurrentRoom;
            position = room != null ? room.Clamp(position) : new Vector3(position.x, 0.95f, position.z);
            return _bridge.SpawnBoss(position, kind, index, hitPoints, speed, damage);
        }

        public ArenaBoss SpawnSuperBoss(Vector3 position, ArenaBossKind kind, int index,
            float hitPoints, float speed, float damage)
        {
            if (_bridge == null) return null;
            DungeonRoom room = CurrentRoom;
            position = room != null ? room.Clamp(position) : new Vector3(position.x, 0.95f, position.z);
            return _bridge.SpawnSuperBoss(position, kind, index, hitPoints, speed, damage);
        }

        public void DamagePlayer(float amount, Vector3 source)
        {
            if (_bridge == null || _finished) return;
            _bridge.DamagePlayer(amount * (_state != null
                ? _state.EnemyDamageMultiplier : 1f), source);
        }

        public void CollectPickup(DungeonPickup pickup)
        {
            if (pickup == null || _finished) return;

            switch (pickup.Kind)
            {
                case DungeonPickupKind.Coin:
                case DungeonPickupKind.Chest:
                    int gained = _state.AddCoins(Mathf.Max(1, pickup.Value));
                    ShowBanner($"+{gained} monedas", 1.4f);
                    break;
                case DungeonPickupKind.Heart:
                    _health?.Heal(pickup.Value);
                    ShowBanner($"+{pickup.Value} de vida", 1.6f);
                    break;
                case DungeonPickupKind.Weapon:
                    _bridge.SetWeapon(pickup.Weapon, pickup.Element);
                    ShowBanner($"ARMA: {pickup.Weapon} · {ArenaElementInfo.Label(pickup.Element)}", 2.2f);
                    break;
            }

            CurrentRoom?.NotifyPickup(pickup);
            Destroy(pickup.gameObject);
        }

        public void ApplyRelic(DungeonRelicDefinition relic)
        {
            if (relic == null) return;
            _state.ApplyRelic(relic);

            if (relic.effect == DungeonRelicEffect.Vida)
                _health?.IncreaseMax(relic.magnitude);
            else if (relic.effect == DungeonRelicEffect.Velocidad && _player != null)
                _player.SpeedMultiplier = _state.SpeedMultiplier;
        }

        public void ShowBanner(string text, float duration = 2f)
        {
            _bannerText = text;
            _bannerTime = Mathf.Max(0f, duration);
        }

        public float RandomRange(float min, float max)
        {
            if (_rng == null) return Random.Range(min, max);
            return min + (float)_rng.NextDouble() * (max - min);
        }

        public int RandomInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            if (_rng == null) return Random.Range(minInclusive, maxExclusive);
            return _rng.Next(minInclusive, maxExclusive);
        }

        // ------------------------------------------- compatibilidad y red

        /// <summary>Usado por las puertas de la generación anterior de salas.</summary>
        public void CompleteRoom()
        {
            if (_finished) return;
            CurrentRoom?.Complete();
        }

        public void FailRoom(string reason)
        {
            if (_finished) return;
            CurrentRoom?.Fail(reason);
        }

        public void TryUseGate()
        {
            if (_finished || _transition) return;
            if (CurrentRoom != null && CurrentRoom.Completed) AdvanceRoom();
        }

        /// <summary>El anfitrión manda la sala; el invitado la alcanza.</summary>
        public void NetworkRoom(int roomIndex)
        {
            if (_rooms == null || roomIndex < 0 || roomIndex >= _rooms.Length ||
                _finished || (roomIndex == _currentIndex && !_transition)) return;

            // Un mensaje del host cancela cualquier transición local pendiente.
            _transition = false;
            _transitionTimer = 0f;
            _pendingRoomIndex = -1;

            CurrentRoom?.End();
            _bridge?.ClearEnemies();
            BeginRoom(roomIndex);
            if (_player != null && !InputBlockedByUi) _player.InputEnabled = true;
        }

        public void NetworkFinish(bool won) => FinishRun(won);

        public void OnRemoteGoal(int roomIndex)
        {
            if (_finished || _rooms == null || roomIndex < _currentIndex) return;
            CurrentRoom?.Complete();

            // La última sala no tiene transición: el host cierra la partida.
            if (!_transition && _currentIndex == _rooms.Length - 1 &&
                CurrentRoom != null && CurrentRoom.Completed)
            {
                AdvanceRoom();
            }
        }

        public void OnRemoteDefeat()
        {
            if (!_finished) FinishRun(false);
        }

        void OnDestroy()
        {
            // Nunca dejamos el control bloqueado al salir de la escena.
            if (_player != null) _player.InputEnabled = true;
        }
    }
}
