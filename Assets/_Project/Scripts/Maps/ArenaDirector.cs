using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Caos.Net;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Director de la arena: oleadas rápidas, dificultad, tres jefes, armas
    /// elementales, mejoras por bajas y daño/sangre del jugador. Todo se genera
    /// en runtime, sin assets externos.
    /// </summary>
    public class ArenaDirector : MonoBehaviour, IArenaCombatDirector
    {
        static readonly Color BloodColor = new Color(0.58f, 0.02f, 0.03f);

        const int UpgradeHealth = 0;
        const int UpgradeFireRate = 1;
        const int UpgradeDamage = 2;
        const int UpgradeSpeed = 3;
        const int UpgradeArmor = 4;
        const int TotalBosses = 3;

        [Header("Oleadas")]
        [SerializeField] float firstSpawnDelay = 1f;
        [SerializeField] float normalInterval = 1.35f;
        [SerializeField] float hardInterval = 0.8f;
        [SerializeField] float bossInterval = 0.5f;
        [SerializeField] float hardTime = 30f;
        [SerializeField] float bossTime = 90f;
        [SerializeField] float secondBossTime = 120f;
        [SerializeField] float thirdBossTime = 150f;
        [SerializeField] int normalCap = 18;
        [SerializeField] int hardCap = 26;
        [SerializeField] int bossCap = 36;
        [SerializeField] int initialMonsters = 8;

        PlayerController _player;
        ChaosHealth _playerHealth;
        CaosNetPlayers _netPlayers;

        CaosNetPlayers NetPlayers => _netPlayers != null
            ? _netPlayers
            : _netPlayers = FindFirstObjectByType<CaosNetPlayers>();

        readonly List<ArenaMonster> _monsters = new List<ArenaMonster>();
        readonly List<ArenaBoss> _bosses = new List<ArenaBoss>();
        int _spawnedBossCount;
        int _bossesDefeated;

        ArenaWeaponKind _weapon = ArenaWeaponKind.Pistola;
        ArenaElement _element = ArenaElement.Normal;
        bool _hasWeapon;
        int _kills;
        int _tier = 1;
        float _nextFire;
        float _fireRateBoost = 1f;
        float _damageBoost = 1f;
        float _damageTakenMultiplier = 1f;

        float _elapsed;
        float _nextSpawn;
        bool _hard;
        bool _finished;
        bool _victory;
        bool _upgradePending;

        string _banner = "";
        float _bannerTimer;
        float _bloodTimer;
        readonly Vector2[] _bloodSpots = new Vector2[8];

        readonly string[] _upgradeTitles = new string[3];
        readonly string[] _upgradeDescriptions = new string[3];
        readonly int[] _upgradeChoices = new int[3];

        public bool UpgradePending => _upgradePending;
        public bool Finished => _finished;
        public bool Victory => _victory;
        public bool CanPause => !_finished && !_upgradePending;
        public float Elapsed => _elapsed;
        public int Kills => _kills;
        public int WeaponTier => _tier;
        public bool HasWeapon => _hasWeapon;
        public ArenaWeaponKind Weapon => _weapon;
        public ArenaElement Element => _element;
        public string ElementLabel => ArenaElementInfo.Label(_element);
        public string ElementEffect => ArenaElementInfo.Effect(_element);
        public Color ElementColor => ArenaElementInfo.Color(_element);
        public float WeaponInterval => FireInterval();
        public float WeaponDamage => FireDamage();
        public int WeaponPellets => PelletCount();
        public string Banner => _bannerTimer > 0f ? _banner : "";
        public Vector2[] BloodSpots => _bloodSpots;
        public float BloodAlpha => _bloodTimer;
        public float Health01 => _playerHealth != null && _playerHealth.Max > 0f
            ? Mathf.Clamp01(_playerHealth.Current / _playerHealth.Max) : 0f;
        public float HealthCurrent => _playerHealth != null ? _playerHealth.Current : 0f;
        public float HealthMax => _playerHealth != null ? _playerHealth.Max : 1f;
        public int BossesTotal => TotalBosses;
        public int BossesDefeated => _bossesDefeated;
        public bool BossActive => CurrentBoss() != null;
        public int CurrentBossIndex => CurrentBoss() != null ? CurrentBoss().BossIndex : 0;
        public string CurrentBossTitle => CurrentBoss() != null ? BossName(CurrentBoss().Kind) : "";
        public string CurrentBossPower => CurrentBoss() != null ? BossPower(CurrentBoss().Kind) : "";
        public float BossHealth01
        {
            get
            {
                ArenaBoss boss = CurrentBoss();
                if (boss == null) return 0f;
                ChaosHealth health = boss.GetComponent<ChaosHealth>();
                return health != null && health.Max > 0f
                    ? Mathf.Clamp01(health.Current / health.Max) : 0f;
            }
        }
        public string[] UpgradeTitles => _upgradeTitles;
        public string[] UpgradeDescriptions => _upgradeDescriptions;
        public bool HardMode => _hard;
        public float BossCountdown
        {
            get
            {
                if (_spawnedBossCount == 0) return Mathf.Max(0f, bossTime - _elapsed);
                if (_spawnedBossCount == 1) return Mathf.Max(0f, secondBossTime - _elapsed);
                return Mathf.Max(0f, thirdBossTime - _elapsed);
            }
        }

        void Awake()
        {
            _player = FindFirstObjectByType<PlayerController>();
            if (_player != null)
            {
                _playerHealth = _player.GetComponent<ChaosHealth>();
                _player.ShoveEnabled = false;
            }

            for (int i = 0; i < _bloodSpots.Length; i++)
                _bloodSpots[i] = new Vector2(Random.value, Random.value);

            _nextSpawn = firstSpawnDelay;

            // Pistola normal inicial para que siempre se pueda disparar.
            _hasWeapon = true;
            _weapon = ArenaWeaponKind.Pistola;
            _element = ArenaElement.Normal;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowBanner("Pistola lista. Recoge un arma de color para cambiar de elemento.", 3.5f);
        }

        void Update()
        {
            if (_bannerTimer > 0f) _bannerTimer -= Time.unscaledDeltaTime;
            if (_bloodTimer > 0f) _bloodTimer -= Time.unscaledDeltaTime;

            if (Keyboard.current != null)
            {
                if (_upgradePending)
                {
                    if (Keyboard.current.digit1Key.wasPressedThisFrame) ChooseUpgrade(0);
                    else if (Keyboard.current.digit2Key.wasPressedThisFrame) ChooseUpgrade(1);
                    else if (Keyboard.current.digit3Key.wasPressedThisFrame) ChooseUpgrade(2);
                    return;
                }

                if (_finished && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    Restart();
                    return;
                }
            }

            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player == null) return;
            if (!_player.InputEnabled || _upgradePending || _finished) return;
            if (_playerHealth != null && _playerHealth.IsDead)
            {
                End(false);
                return;
            }

            _elapsed += Time.deltaTime;
            if (_player.transform.position.y < -14f) _player.Respawn();

            if (!_hard && _elapsed >= hardTime)
            {
                _hard = true;
                ShowBanner("¡EL CAOS SE DESATA! Monstruos más rápidos", 3f);
                ArenaAudio.Play(ArenaAudio.Sound.Boss, _player.transform.position, 0.55f);
            }

            if (_spawnedBossCount == 0 && _elapsed >= bossTime) SpawnBoss(1);
            else if (_spawnedBossCount == 1 && _elapsed >= secondBossTime) SpawnBoss(2);
            else if (_spawnedBossCount == 2 && _elapsed >= thirdBossTime) SpawnBoss(3);

            _monsters.RemoveAll(monster => monster == null);
            _bosses.RemoveAll(boss => boss == null);

            if (_elapsed >= _nextSpawn && _monsters.Count < MonsterCap())
            {
                SpawnMonster();
                _nextSpawn = _elapsed + SpawnInterval();
            }
        }

        void Start()
        {
            for (int i = 0; i < initialMonsters; i++) SpawnMonster();
        }

        // ------------------------------------------------------------- armas

        /// <summary>Equipa un arma recogida y elimina el pedestal.</summary>
        public void EquipWeapon(ArenaWeaponPickup pickup, ArenaWeaponKind kind, ArenaElement element)
        {
            if (pickup == null) return;

            _weapon = kind;
            _element = element;
            _hasWeapon = true;
            _nextFire = Time.time + 0.15f;
            ShowBanner($"ARMA: {WeaponLabel()} · {ArenaElementInfo.Label(element)} — {ArenaElementInfo.Effect(element)}", 2.6f);
            ArenaAudio.Play(ArenaAudio.Sound.Upgrade, pickup.transform.position, 0.55f);
            ArenaJuice.Current.Pickup(pickup.transform.position);
            Destroy(pickup.gameObject);
        }

        /// <summary>Llamado por ArenaAimController mientras el botón está pulsado.</summary>
        public void TryFire(Transform shooter, Vector3 aimPoint)
        {
            if (!_hasWeapon || _finished || _upgradePending) return;
            if (Time.time < _nextFire) return;

            float interval = FireInterval();
            if (_element == ArenaElement.Aire) interval *= 0.82f;
            _nextFire = Time.time + interval;

            Vector3 origin = shooter.position + Vector3.up * 0.95f + shooter.forward * 0.5f;
            Vector3 direction = aimPoint - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = shooter.forward;
            direction.Normalize();

            float speed = _weapon == ArenaWeaponKind.Rifle ? 44f
                : _weapon == ArenaWeaponKind.Escopeta ? 31f : 36f;
            if (_element == ArenaElement.Aire) speed *= 1.2f;

            float spread = _weapon == ArenaWeaponKind.Escopeta ? 11f
                : _weapon == ArenaWeaponKind.Rifle ? 2.5f : 0f;
            float damage = FireDamage() / (_weapon == ArenaWeaponKind.Escopeta ? 1.9f : 1f);
            int pellets = PelletCount();

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shotDirection = direction;
                if (spread > 0f)
                {
                    float yaw = Random.Range(-spread, spread);
                    shotDirection = Quaternion.Euler(0f, yaw, 0f) * direction;
                }
                SpawnProjectile(origin, shotDirection, speed, damage);
            }

            ArenaAudio.Play(ArenaAudio.Sound.Shoot, origin,
                _weapon == ArenaWeaponKind.Escopeta ? 0.65f : 0.4f);
            // Tier pequeño: apenas un toque, para no castigar disparos rápidos.
            ArenaJuice.Current.AddTrauma(_weapon == ArenaWeaponKind.Escopeta ? 0.09f : 0.045f);
        }

        void SpawnProjectile(Vector3 origin, Vector3 direction, float speed, float damage)
        {
            GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "ArenaBullet";
            shot.transform.position = origin;
            shot.transform.localScale = Vector3.one * (_element == ArenaElement.Rayo ? 0.24f : 0.19f);
            shot.GetComponent<Renderer>().sharedMaterial = ArenaMaterial.Create(
                ArenaElementInfo.Color(_element), 0f, 0.6f);

            var projectile = shot.AddComponent<ArenaProjectile>();
            projectile.Configure(this, direction, speed, damage, _element);
            NetPlayers?.NotifyShot(origin, direction, speed, damage, _element);
            Destroy(shot, 3.2f);
        }

        public void SpawnEnemyProjectile(Vector3 origin, Vector3 direction,
            float speed, float damage, Color color)
        {
            GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "BossBullet";
            shot.transform.position = origin;
            shot.transform.localScale = Vector3.one * 0.34f;
            shot.GetComponent<Renderer>().sharedMaterial = ArenaMaterial.Create(color);

            var projectile = shot.AddComponent<ArenaEnemyProjectile>();
            projectile.Configure(this, direction, speed, damage);
            Destroy(shot, 4.2f);
        }

        /// <summary>El rayo salta al enemigo más cercano que no sea el objetivo.</summary>
        public void ChainLightning(Vector3 origin, float damage, Component exclude)
        {
            Component best = null;
            float bestDistance = 7f;

            for (int i = 0; i < _monsters.Count; i++)
            {
                ArenaMonster monster = _monsters[i];
                if (monster == null || monster.IsDead) continue;
                if (exclude != null && monster.gameObject == exclude.gameObject) continue;

                float distance = Vector3.Distance(origin, monster.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = monster;
                }
            }

            for (int i = 0; i < _bosses.Count; i++)
            {
                ArenaBoss boss = _bosses[i];
                if (boss == null || boss.IsDead) continue;
                if (exclude != null && boss.gameObject == exclude.gameObject) continue;

                float distance = Vector3.Distance(origin, boss.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = boss;
                }
            }

            if (best == null) return;

            Vector3 point = best.transform.position + Vector3.up * 0.6f;
            ChaosFx.Burst(point, ArenaElementInfo.Color(ArenaElement.Rayo), 8);
            ArenaAudio.Play(ArenaAudio.Sound.Hit, point, 0.35f);

            if (best is ArenaMonster chainedMonster)
                chainedMonster.TakeDamage(damage, point, Vector3.forward, ArenaElement.Normal, false);
            else if (best is ArenaBoss chainedBoss)
                chainedBoss.TakeDamage(damage, point, Vector3.forward, ArenaElement.Normal, false);
        }

        float FireInterval()
        {
            float baseInterval = _weapon == ArenaWeaponKind.Escopeta ? 0.62f
                : _weapon == ArenaWeaponKind.Rifle ? 0.12f : 0.26f;
            return Mathf.Max(0.06f,
                baseInterval * _fireRateBoost * Mathf.Pow(0.91f, _tier - 1));
        }

        float FireDamage()
        {
            float baseDamage = _weapon == ArenaWeaponKind.Escopeta ? 13f
                : _weapon == ArenaWeaponKind.Rifle ? 12f : 24f;
            float elementMultiplier = _element == ArenaElement.Fuego ? 1.05f
                : _element == ArenaElement.Hielo ? 0.95f
                : _element == ArenaElement.Veneno ? 0.9f : 1f;
            return baseDamage * _damageBoost * elementMultiplier
                * (1f + 0.2f * (_tier - 1));
        }

        int PelletCount()
        {
            if (_weapon != ArenaWeaponKind.Escopeta) return 1;
            return 4 + Mathf.Max(0, _tier - 2);
        }

        public string WeaponLabel() => _weapon.ToString().ToUpperInvariant();

        // --------------------------------------------------------- monstruos

        int MonsterCap()
        {
            if (_spawnedBossCount > 0) return bossCap;
            return _hard ? hardCap : normalCap;
        }

        float SpawnInterval()
        {
            if (_spawnedBossCount > 0) return bossInterval;
            return _hard ? hardInterval : normalInterval;
        }

        Vector3 SpawnPosition()
        {
            // centro del CharacterController: suelo ≈ 0.95, piso alto ≈ 5.15
            Vector3[] ground =
            {
                new Vector3(-20f, 0.95f, -16f),
                new Vector3(20f, 0.95f, -14f),
                new Vector3(-18f, 0.95f, 17f),
                new Vector3(18f, 0.95f, 18f),
                new Vector3(0f, 0.95f, 21f),
                new Vector3(-21f, 0.95f, 6f),
                new Vector3(21f, 0.95f, 7f),
            };
            Vector3[] upper =
            {
                new Vector3(-10f, 5.15f, -6f),
                new Vector3(10f, 5.15f, 5f),
                new Vector3(0f, 5.15f, 14f),
            };

            Vector3 position = _hard && Random.value < 0.3f
                ? upper[Random.Range(0, upper.Length)]
                : ground[Random.Range(0, ground.Length)];

            if (_player != null && Vector3.Distance(position, _player.transform.position) < 6f)
                position += new Vector3(5f, 0f, 0f);

            return position;
        }

        void SpawnMonster(Vector3? forcedPosition = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "ArenaMonster";
            go.transform.position = forcedPosition ?? SpawnPosition();
            go.transform.localScale = new Vector3(0.82f, 0.9f, 0.82f);

            var health = go.AddComponent<ChaosHealth>();
            health.Configure(52f + Mathf.Min(_elapsed * 0.45f, 90f), false);

            var monster = go.AddComponent<ArenaMonster>();
            monster.Configure(3.1f + Random.Range(0f, 0.9f), 15f, this);
            _monsters.Add(monster);
            ArenaJuice.Current.Spawn(go.transform.position);
            ArenaJuice.Current.Punch(go.transform, 1.22f);
        }

        public void SummonMinions(int count, Vector3 origin)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 position = origin + new Vector3(
                    Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                if (_player != null) position.y = _player.transform.position.y;
                SpawnMonster(position);
            }
        }

        ArenaBoss CurrentBoss()
        {
            for (int i = _bosses.Count - 1; i >= 0; i--)
            {
                if (_bosses[i] != null && !_bosses[i].IsDead) return _bosses[i];
            }
            return null;
        }

        static string BossName(ArenaBossKind kind)
        {
            if (kind == ArenaBossKind.Canion) return "CAÑÓN";
            if (kind == ArenaBossKind.Tormenta) return "TORMENTA";
            return "MARTILLO";
        }

        static string BossPower(ArenaBossKind kind)
        {
            if (kind == ArenaBossKind.Canion) return "RAFAGA DE 3 DISPAROS";
            if (kind == ArenaBossKind.Tormenta) return "TELEPORTE + CLONES";
            return "PISOTÓN";
        }

        void SpawnBoss(int index)
        {
            ArenaBossKind kind = index == 1 ? ArenaBossKind.Martillo
                : index == 2 ? ArenaBossKind.Canion : ArenaBossKind.Tormenta;

            float hitPoints = index == 1 ? 1400f : index == 2 ? 2200f : 3200f;
            float speed = index == 1 ? 2.4f : index == 2 ? 2.8f : 3.2f;
            float damage = index == 1 ? 26f : index == 2 ? 30f : 34f;
            float scale = index == 1 ? 1.25f : index == 2 ? 1.32f : 1.4f;
            bool playerUpper = _player != null
                && ArenaNavigation.IsUpper(_player.transform.position);
            float y = playerUpper ? ArenaNavigation.UpperSurfaceY + 0.9f : 0.95f;
            float z = playerUpper ? 14f : 20f; // z=20 no existe en el piso alto

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "ArenaBoss_" + index + "_" + kind;
            go.transform.position = new Vector3(0f, y, z);
            go.transform.localScale = new Vector3(scale, scale * 0.92f, scale);

            var health = go.AddComponent<ChaosHealth>();
            health.Configure(hitPoints, false);

            var boss = go.AddComponent<ArenaBoss>();
            boss.Configure(speed, damage, this, kind, index);
            _bosses.Add(boss);
            _spawnedBossCount = index;

            ShowBanner($"JEFE {index}/3: {BossName(kind)} — {BossPower(kind)}", 4f);
            ArenaAudio.Play(ArenaAudio.Sound.Boss, go.transform.position, 0.95f);
            ArenaJuice.Current.AddTrauma(0.5f);
            ArenaJuice.Current.Spawn(go.transform.position, true);
            ArenaJuice.Current.Impact(go.transform.position, 0.9f, go.transform);
        }

        /// <summary>La arena no limita los poderes del jefe: su mapa ya está acotado.</summary>
        public Vector3 ClampWorldPosition(Vector3 point) => point;

        public float MonsterSpeed(float baseSpeed)
        {
            float factor = 1f;
            if (_hard) factor *= 1.18f;
            if (_spawnedBossCount > 0) factor *= 1.1f;
            factor *= 1f + Mathf.Min(0.15f, Mathf.Max(0f, _elapsed - hardTime) / 260f);
            return baseSpeed * factor;
        }

        public float MonsterDamage(float baseDamage)
        {
            float factor = 1f;
            if (_hard) factor *= 1.35f;
            if (_spawnedBossCount > 0) factor *= 1.2f;
            return baseDamage * factor;
        }

        public void MonsterKilled(ArenaMonster monster)
        {
            _monsters.Remove(monster);
            _kills++;
            _tier = Mathf.Clamp(1 + _kills / 6, 1, 5);

            if (monster != null)
                ArenaAudio.Play(ArenaAudio.Sound.Death, monster.transform.position, 0.5f);

            if (_kills % 5 == 0 && !_upgradePending && !_finished) OfferUpgrade();
        }

        public void BossDefeated(ArenaBoss boss)
        {
            _bosses.Remove(boss);
            _bossesDefeated++;
            if (_playerHealth != null) _playerHealth.Heal(25f);

            ShowBanner($"JEFE {boss.BossIndex}/3 DERROTADO — +25 VIDA", 3f);
            ArenaAudio.Play(ArenaAudio.Sound.Victory, boss.transform.position, 0.7f);

            if (_spawnedBossCount >= TotalBosses && _bossesDefeated >= TotalBosses) End(true);
        }

        // ----------------------------------------------------- daño y mejoras

        public void DamagePlayer(float amount, Vector3 source)
        {
            if (_finished || _upgradePending) return;
            if (_playerHealth == null || _playerHealth.IsDead) return;

            float finalDamage = amount * _damageTakenMultiplier;
            _playerHealth.TakeDamage(finalDamage, "golpe");

            _bloodTimer = 0.7f;
            for (int i = 0; i < _bloodSpots.Length; i++)
                _bloodSpots[i] = new Vector2(Random.value, Random.value);

            if (_player != null)
            {
                ChaosFx.Burst(_player.transform.position + Vector3.up, BloodColor, 7);
                Vector3 away = _player.transform.position - source;
                away.y = 0f;
                if (away.sqrMagnitude > 0.01f) _player.AddImpulse(away.normalized * 3.5f);
                ArenaAudio.Play(ArenaAudio.Sound.Hurt, _player.transform.position, 0.7f);
                ArenaJuice.Current.PlayerHurt(_player.transform.position);
            }

            ShowBanner($"¡AUCH! -{finalDamage:0}", 1.1f);
            if (_playerHealth.IsDead) End(false);
        }

        void OfferUpgrade()
        {
            _upgradePending = true;
            Time.timeScale = 0f;

            var pool = new List<int> { UpgradeHealth, UpgradeFireRate, UpgradeDamage, UpgradeSpeed, UpgradeArmor };
            for (int i = 0; i < 3; i++)
            {
                int index = Random.Range(0, pool.Count);
                _upgradeChoices[i] = pool[index];
                pool.RemoveAt(index);

                switch (_upgradeChoices[i])
                {
                    case UpgradeHealth:
                        _upgradeTitles[i] = "CORAZÓN GRANDE";
                        _upgradeDescriptions[i] = "+20 vida máxima y curas 35";
                        break;
                    case UpgradeFireRate:
                        _upgradeTitles[i] = "GATILLO RÁPIDO";
                        _upgradeDescriptions[i] = "Disparas un 18% más rápido";
                        break;
                    case UpgradeDamage:
                        _upgradeTitles[i] = "BALAS PESADAS";
                        _upgradeDescriptions[i] = "+22% de daño";
                        break;
                    case UpgradeSpeed:
                        _upgradeTitles[i] = "PIERNAS LIGERAS";
                        _upgradeDescriptions[i] = "Te mueves más rápido";
                        break;
                    default:
                        _upgradeTitles[i] = "SANGRE FRÍA";
                        _upgradeDescriptions[i] = "Recibes un 18% menos de daño";
                        break;
                }
            }

            Vector3 position = _player != null ? _player.transform.position : transform.position;
            ArenaAudio.Play(ArenaAudio.Sound.Upgrade, position, 0.65f);
        }

        public void ChooseUpgrade(int index)
        {
            if (!_upgradePending || index < 0 || index > 2) return;

            string title = _upgradeTitles[index];
            switch (_upgradeChoices[index])
            {
                case UpgradeHealth:
                    if (_playerHealth != null) _playerHealth.IncreaseMax(20f);
                    _playerHealth?.Heal(35f);
                    break;
                case UpgradeFireRate:
                    _fireRateBoost *= 0.82f;
                    break;
                case UpgradeDamage:
                    _damageBoost *= 1.22f;
                    break;
                case UpgradeSpeed:
                    if (_player != null) _player.SpeedMultiplier += 0.16f;
                    break;
                case UpgradeArmor:
                    _damageTakenMultiplier *= 0.82f;
                    break;
            }

            _upgradePending = false;
            Time.timeScale = 1f;
            ShowBanner($"MEJORA: {title}", 2.2f);
        }

        // ----------------------------------------------------------- estado

        public void ShowBanner(string text, float duration = 2f)
        {
            _banner = text;
            _bannerTimer = duration;
        }

        void End(bool victory)
        {
            if (_finished) return;
            _finished = true;
            _victory = victory;
            _upgradePending = false;
            Time.timeScale = 1f;
            if (_player != null) _player.InputEnabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowBanner(victory ? "¡VICTORIA!" : "HAS MUERTO", 6f);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            CaosSession.ResetForMenu();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            CaosSession.ResetForMenu();
            SceneManager.LoadScene("Menu");
        }

        void OnDestroy() => Time.timeScale = 1f;
    }
}
