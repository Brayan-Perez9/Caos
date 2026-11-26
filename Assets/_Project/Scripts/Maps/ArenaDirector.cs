using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Caos.Net;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Director de la arena: oleadas, dificultad, jefe, armas, mejoras por bajas
    /// y daño/sangre del jugador. Todo se genera en runtime.
    /// </summary>
    public class ArenaDirector : MonoBehaviour
    {
        static readonly Color BloodColor = new Color(0.58f, 0.02f, 0.03f);

        const int UpgradeHealth = 0;
        const int UpgradeFireRate = 1;
        const int UpgradeDamage = 2;
        const int UpgradeSpeed = 3;
        const int UpgradeArmor = 4;

        [Header("Oleadas")]
        [SerializeField] float firstSpawnDelay = 1.4f;
        [SerializeField] float normalInterval = 2.2f;
        [SerializeField] float hardInterval = 1.35f;
        [SerializeField] float bossInterval = 0.85f;
        [SerializeField] float hardTime = 30f;
        [SerializeField] float bossTime = 90f;
        [SerializeField] int normalCap = 12;
        [SerializeField] int hardCap = 18;
        [SerializeField] int bossCap = 24;
        [SerializeField] int initialMonsters = 5;

        PlayerController _player;
        ChaosHealth _playerHealth;

        readonly List<ArenaMonster> _monsters = new List<ArenaMonster>();
        ArenaBoss _boss;

        ArenaWeaponKind _weapon = ArenaWeaponKind.Pistola;
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
        bool _bossSpawned;
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
        public bool BossActive => _boss != null && !_boss.IsDead;
        public float BossHealth01 => _boss != null && _boss.IsDead == false
            ? Mathf.Clamp01(_boss.GetComponent<ChaosHealth>().Current / _boss.GetComponent<ChaosHealth>().Max)
            : 0f;
        public string[] UpgradeTitles => _upgradeTitles;
        public string[] UpgradeDescriptions => _upgradeDescriptions;
        public bool HardMode => _hard;
        public float BossCountdown => Mathf.Max(0f, bossTime - _elapsed);

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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowBanner("Acércate a un arma para recogerla. ¡Corre!", 3.5f);
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

            if (!_bossSpawned && _elapsed >= bossTime)
            {
                _bossSpawned = true;
                SpawnBoss();
                ShowBanner("¡EL JEFE FINAL HA LLEGADO!", 4f);
            }

            _monsters.RemoveAll(monster => monster == null);
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
        public void EquipWeapon(ArenaWeaponPickup pickup, ArenaWeaponKind kind)
        {
            if (pickup == null) return;

            _weapon = kind;
            _hasWeapon = true;
            _nextFire = Time.time + 0.15f;
            ShowBanner($"ARMA: {WeaponLabel()}", 2f);
            ArenaAudio.Play(ArenaAudio.Sound.Upgrade, pickup.transform.position, 0.55f);
            Destroy(pickup.gameObject);
        }

        /// <summary>Llamado por ArenaAimController mientras el botón está pulsado.</summary>
        public void TryFire(Transform shooter, Vector3 aimPoint)
        {
            if (!_hasWeapon || _finished || _upgradePending) return;
            if (Time.time < _nextFire) return;

            _nextFire = Time.time + FireInterval();

            Vector3 origin = shooter.position + Vector3.up * 1.2f + shooter.forward * 0.6f;
            Vector3 direction = aimPoint - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = shooter.forward;
            direction.Normalize();

            float speed = _weapon == ArenaWeaponKind.Rifle ? 44f
                : _weapon == ArenaWeaponKind.Escopeta ? 31f : 36f;
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
        }

        void SpawnProjectile(Vector3 origin, Vector3 direction, float speed, float damage)
        {
            GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "ArenaBullet";
            shot.transform.position = origin;
            shot.transform.localScale = Vector3.one * 0.19f;
            shot.GetComponent<Renderer>().material.color = BulletColor();

            var projectile = shot.AddComponent<ArenaProjectile>();
            projectile.Configure(this, direction, speed, damage);
            Destroy(shot, 3.2f);
        }

        Color BulletColor()
        {
            if (_weapon == ArenaWeaponKind.Escopeta) return new Color(1f, 0.42f, 0.22f);
            if (_weapon == ArenaWeaponKind.Rifle) return new Color(0.35f, 0.85f, 1f);
            return new Color(1f, 0.86f, 0.32f);
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
            return baseDamage * _damageBoost * (1f + 0.2f * (_tier - 1));
        }

        int PelletCount()
        {
            if (_weapon != ArenaWeaponKind.Escopeta) return 1;
            return 4 + Mathf.Max(0, _tier - 2);
        }

        public string WeaponLabel() => _weapon.ToString().ToUpperInvariant();

        // --------------------------------------------------------- monstruos

        int MonsterCap() => _bossSpawned ? bossCap : _hard ? hardCap : normalCap;

        float SpawnInterval()
        {
            if (_bossSpawned) return bossInterval;
            if (_hard) return hardInterval;
            return normalInterval;
        }

        Vector3 SpawnPosition()
        {
            Vector3[] ground =
            {
                new Vector3(-11f, 1f, -8f),
                new Vector3(11f, 1f, -7f),
                new Vector3(-9f, 1f, 8f),
                new Vector3(9f, 1f, 9f),
                new Vector3(0f, 1f, 13f),
            };
            Vector3[] upper =
            {
                new Vector3(-6f, 5.4f, -4f),
                new Vector3(6f, 5.4f, 4f),
            };

            Vector3 position = _hard && Random.value < 0.28f
                ? upper[Random.Range(0, upper.Length)]
                : ground[Random.Range(0, ground.Length)];

            if (_player != null && Vector3.Distance(position, _player.transform.position) < 5f)
                position += new Vector3(4f, 0f, 0f);

            return position;
        }

        void SpawnMonster()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "ArenaMonster";
            go.transform.position = SpawnPosition();
            go.transform.localScale = new Vector3(0.8f, 0.75f, 0.8f);
            go.GetComponent<Collider>().isTrigger = false;

            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var health = go.AddComponent<ChaosHealth>();
            health.Configure(46f + Mathf.Min(_elapsed * 0.35f, 70f), false);

            var monster = go.AddComponent<ArenaMonster>();
            monster.Configure(4.5f + Random.Range(0f, 1.3f), 14f, this);
            _monsters.Add(monster);
        }

        void SpawnBoss()
        {
            float y = _player != null && _player.transform.position.y > 3f ? 5.6f : 1.5f;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "ArenaBoss";
            go.transform.position = new Vector3(0f, y, 12f);
            go.transform.localScale = new Vector3(1.25f, 1.15f, 1.25f);

            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var health = go.AddComponent<ChaosHealth>();
            health.Configure(1600f, false);

            var boss = go.AddComponent<ArenaBoss>();
            boss.Configure(3.0f, 26f, this);
            _boss = boss;
        }

        public float MonsterSpeed(float baseSpeed)
        {
            float factor = 1f;
            if (_hard) factor *= 1.35f;
            if (_bossSpawned) factor *= 1.18f;
            factor *= 1f + Mathf.Min(0.35f, Mathf.Max(0f, _elapsed - hardTime) / 240f);
            return baseSpeed * factor;
        }

        public float MonsterDamage(float baseDamage)
        {
            float factor = 1f;
            if (_hard) factor *= 1.3f;
            if (_bossSpawned) factor *= 1.15f;
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

        public void BossDefeated()
        {
            _boss = null;
            End(true);
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
            ShowBanner(victory ? "¡JEFE DERROTADO!" : "HAS MUERTO", 6f);
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
