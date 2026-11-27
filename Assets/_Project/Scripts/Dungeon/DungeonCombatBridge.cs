using System.Collections.Generic;
using UnityEngine;
using Caos.Maps;
using Caos.Net;
using Caos.Player;

namespace Caos.Dungeon
{
    /// <summary>
    /// Traduce las llamadas de combate de los monstruos/jefes de Arena al director
    /// de la mazmorra. Así reutilizamos ArenaMonster, ArenaBoss y los proyectiles
    /// sin duplicar su lógica ni depender de ArenaDirector.
    /// </summary>
    public class DungeonCombatBridge : MonoBehaviour, IArenaCombatDirector
    {
        static readonly Color BloodColor = new Color(0.58f, 0.02f, 0.03f);
        static readonly Dictionary<ArenaElement, Material> BulletMaterials =
            new Dictionary<ArenaElement, Material>();
        static readonly Dictionary<Color, Material> EnemyBulletMaterials =
            new Dictionary<Color, Material>();

        [SerializeField] ArenaWeaponKind weapon = ArenaWeaponKind.Pistola;
        [SerializeField] ArenaElement element = ArenaElement.Normal;
        [SerializeField] float weaponDamage = 24f;
        [SerializeField] float fireInterval = 0.24f;

        DungeonDirector _director;
        PlayerController _player;
        ChaosHealth _health;
        DungeonRunState _state;
        CaosNetPlayers _netPlayers;

        CaosNetPlayers NetPlayers => _netPlayers != null
            ? _netPlayers
            : _netPlayers = FindFirstObjectByType<CaosNetPlayers>();

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<ArenaMonster> _monsters = new List<ArenaMonster>();
        readonly List<ArenaBoss> _bosses = new List<ArenaBoss>();

        float _nextFire;
        float _nextTrapHit;

        public bool CanFire => _director != null && !_director.Finished &&
            !_director.TransitionActive && _player != null && _player.InputEnabled;

        public bool UpgradePending => false;

        public Vector3 ClampWorldPosition(Vector3 point) =>
            _director != null ? _director.ClampToRoom(point) : point;

        public ArenaElement Element => _state != null && _state.BurnOnHit &&
            element == ArenaElement.Normal ? ArenaElement.Fuego : element;

        public Color ElementColor => ArenaElementInfo.Color(Element);

        public int ActiveEnemyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _monsters.Count; i++)
                    if (_monsters[i] != null && !_monsters[i].IsDead) count++;
                for (int i = 0; i < _bosses.Count; i++)
                    if (_bosses[i] != null && !_bosses[i].IsDead) count++;
                return count;
            }
        }

        public bool BossAlive
        {
            get
            {
                for (int i = 0; i < _bosses.Count; i++)
                    if (_bosses[i] != null && !_bosses[i].IsDead) return true;
                return false;
            }
        }

        public float BossHealth01
        {
            get
            {
                for (int i = 0; i < _bosses.Count; i++)
                {
                    if (_bosses[i] == null || _bosses[i].IsDead) continue;
                    ChaosHealth health = _bosses[i].GetComponent<ChaosHealth>();
                    return health != null && health.Max > 0f
                        ? Mathf.Clamp01(health.Current / health.Max) : 1f;
                }
                return 0f;
            }
        }

        public Vector3 BossPosition
        {
            get
            {
                for (int i = 0; i < _bosses.Count; i++)
                    if (_bosses[i] != null && !_bosses[i].IsDead)
                        return _bosses[i].transform.position;
                return transform.position;
            }
        }

        public void Configure(DungeonDirector director, PlayerController player,
            ChaosHealth health, DungeonRunState state)
        {
            _director = director;
            _player = player;
            _health = health;
            _state = state;
        }

        public void SetWeapon(ArenaWeaponKind kind, ArenaElement newElement)
        {
            weapon = kind;
            element = newElement;
            _nextFire = Time.time + 0.12f;
        }

        bool IArenaCombatDirector.Finished => _director != null && _director.Finished;

        // ------------------------------------------------------------ dispara

        public void TryFire(Transform shooter, Vector3 aimPoint)
        {
            if (!CanFire || Time.time < _nextFire) return;

            float interval = fireInterval;
            if (element == ArenaElement.Aire) interval *= 0.82f;
            _nextFire = Time.time + interval;

            Vector3 origin = shooter.position + Vector3.up * 0.95f + shooter.forward * 0.45f;
            Vector3 direction = aimPoint - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = shooter.forward;
            direction.Normalize();

            float speed = weapon == ArenaWeaponKind.Rifle ? 44f
                : weapon == ArenaWeaponKind.Escopeta ? 31f : 36f;
            float spread = weapon == ArenaWeaponKind.Escopeta ? 11f
                : weapon == ArenaWeaponKind.Rifle ? 2.5f : 0f;
            float damage = weaponDamage * (_state != null ? _state.DamageMultiplier : 1f);
            if (weapon == ArenaWeaponKind.Escopeta) damage /= 1.8f;
            int pellets = weapon == ArenaWeaponKind.Escopeta ? 4 : 1;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 shotDirection = direction;
                if (spread > 0f)
                {
                    float yaw = Random.Range(-spread, spread);
                    shotDirection = Quaternion.Euler(0f, yaw, 0f) * direction;
                }
                SpawnBullet(origin, shotDirection, speed, damage, Element);
            }

            ArenaAudio.Play(ArenaAudio.Sound.Shoot, origin, weapon == ArenaWeaponKind.Escopeta ? 0.6f : 0.4f);
            ArenaJuice.Current.AddTrauma(weapon == ArenaWeaponKind.Escopeta ? 0.08f : 0.04f);
        }

        void SpawnBullet(Vector3 origin, Vector3 direction, float speed,
            float damage, ArenaElement bulletElement)
        {
            GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "DungeonBullet";
            shot.transform.position = origin;
            shot.transform.localScale = Vector3.one * (bulletElement == ArenaElement.Rayo ? 0.24f : 0.19f);
            shot.GetComponent<Renderer>().sharedMaterial = BulletMaterial(bulletElement);

            var projectile = shot.AddComponent<ArenaProjectile>();
            projectile.Configure((IArenaCombatDirector)this, direction, speed, damage, bulletElement);
            NetPlayers?.NotifyShot(origin, direction, speed, damage, bulletElement);
            Track(shot, 3.2f);
        }

        // ---------------------------------------------------------- criaturas

        public ArenaMonster SpawnMonster(Vector3 position, float hitPoints,
            float speed, float damage)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "DungeonMonster";
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.82f, 0.9f, 0.82f);

            var health = go.AddComponent<ChaosHealth>();
            health.Configure(hitPoints, false);

            var monster = go.AddComponent<ArenaMonster>();
            monster.Configure(speed, damage, (IArenaCombatDirector)this);
            _monsters.Add(monster);
            Track(go, 0f);

            ArenaJuice.Current.Spawn(go.transform.position);
            ArenaJuice.Current.Punch(go.transform, 1.2f);
            return monster;
        }

        public ArenaBoss SpawnBoss(Vector3 position, ArenaBossKind kind, int index,
            float hitPoints, float speed, float damage)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "DungeonBoss_" + index + "_" + kind;
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.3f, 1.2f, 1.3f);

            var health = go.AddComponent<ChaosHealth>();
            health.Configure(hitPoints, false);

            var boss = go.AddComponent<ArenaBoss>();
            boss.Configure(speed, damage, (IArenaCombatDirector)this, kind, index);
            _bosses.Add(boss);
            Track(go, 0f);

            ArenaAudio.Play(ArenaAudio.Sound.Boss, position, 0.9f);
            ArenaJuice.Current.AddTrauma(0.5f);
            ArenaJuice.Current.Spawn(position, true);
            return boss;
        }

        static Material BulletMaterial(ArenaElement element)
        {
            if (!BulletMaterials.TryGetValue(element, out Material material))
            {
                material = ArenaMaterial.Create(ArenaElementInfo.Color(element), 0f, 0.6f);
                BulletMaterials.Add(element, material);
            }
            return material;
        }

        static Material EnemyBulletMaterial(Color color)
        {
            if (!EnemyBulletMaterials.TryGetValue(color, out Material material))
            {
                material = ArenaMaterial.Create(color);
                EnemyBulletMaterials.Add(color, material);
            }
            return material;
        }

        void Track(GameObject go, float lifetime)
        {
            _spawned.RemoveAll(item => item == null);
            _spawned.Add(go);
            if (lifetime > 0f) Destroy(go, lifetime);
        }

        public void ClearEnemies()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);

            _spawned.Clear();
            _monsters.Clear();
            _bosses.Clear();
        }

        // ------------------------------------------- IArenaCombatDirector

        float IArenaCombatDirector.MonsterSpeed(float baseSpeed) =>
            baseSpeed * (_state != null ? _state.EnemySpeedMultiplier : 1f);

        float IArenaCombatDirector.MonsterDamage(float baseDamage) =>
            baseDamage * (_state != null ? _state.EnemyDamageMultiplier : 1f);

        void IArenaCombatDirector.DamagePlayer(float amount, Vector3 source) =>
            DamagePlayer(amount, source);

        public void DamagePlayer(float amount, Vector3 source)
        {
            if (_director == null || _director.Finished || _director.Paused ||
                _health == null || _health.IsDead) return;
            if (Time.time < _nextTrapHit && amount < 20f) return;

            // Las criaturas ya escalan su daño en MonsterDamage; aquí llega el
            // valor final (o el de una trampa, escalado por el director).
            float finalDamage = amount;
            bool lethal = _health.Current - finalDamage <= 0f;

            if (lethal && _state != null && _state.LethalShield)
            {
                _state.LethalShield = false;
                _health.SetCurrent(1f);
                ShowBanner("¡ESCUDO ANCESTRAL! GOLPE BLOQUEADO", 2.4f);
                ChaosFx.Burst(_player != null ? _player.transform.position : source,
                    new Color(0.45f, 0.78f, 1f), 22);
                ArenaAudio.Play(ArenaAudio.Sound.Upgrade, source, 0.7f);
                return;
            }

            _nextTrapHit = Time.time + 0.35f;
            _health.TakeDamage(finalDamage, "golpe");
            _director.OnPlayerDamaged(finalDamage);

            if (_player != null)
            {
                ChaosFx.Burst(_player.transform.position + Vector3.up, BloodColor, 7);
                Vector3 away = _player.transform.position - source;
                away.y = 0f;
                if (away.sqrMagnitude > 0.01f) _player.AddImpulse(away.normalized * 3.5f);
                ArenaJuice.Current.PlayerHurt(_player.transform.position);
                ArenaAudio.Play(ArenaAudio.Sound.Hurt, _player.transform.position, 0.7f);
            }

            if (_health.IsDead) _director.OnPlayerDefeated();
        }

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

            if (best is ArenaMonster targetMonster)
                targetMonster.TakeDamage(damage, origin, Vector3.up, ArenaElement.Rayo, false);
            else if (best is ArenaBoss targetBoss)
                targetBoss.TakeDamage(damage, origin, Vector3.up, ArenaElement.Rayo, false);
        }

        void IArenaCombatDirector.MonsterKilled(ArenaMonster monster)
        {
            if (monster == null) return;
            _monsters.Remove(monster);
            if (_director != null) _director.OnEnemyKilled(monster.transform.position);
        }

        void IArenaCombatDirector.BossDefeated(ArenaBoss boss)
        {
            if (boss == null) return;
            _bosses.Remove(boss);
            if (_director != null) _director.OnBossDefeated(boss);
        }

        public void ShowBanner(string text, float duration = 2f)
        {
            if (_director != null) _director.ShowBanner(text, duration);
        }

        void IArenaCombatDirector.ShowBanner(string text, float duration) => ShowBanner(text, duration);

        void IArenaCombatDirector.SpawnEnemyProjectile(Vector3 origin, Vector3 direction,
            float speed, float damage, Color color)
        {
            GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "DungeonBossBullet";
            shot.transform.position = origin;
            shot.transform.localScale = Vector3.one * 0.34f;
            shot.GetComponent<Renderer>().sharedMaterial = EnemyBulletMaterial(color);

            var projectile = shot.AddComponent<ArenaEnemyProjectile>();
            projectile.Configure((IArenaCombatDirector)this, direction, speed, damage);
            Track(shot, 4.2f);
        }

        void IArenaCombatDirector.SummonMinions(int count, Vector3 origin)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 position = origin + new Vector3(
                    Random.Range(-3.5f, 3.5f), 0f, Random.Range(-3.5f, 3.5f));
                if (_director != null)
                {
                    position = _director.ClampToRoom(position);
                    if (_director.CurrentRoom != null)
                        position.y = _director.CurrentRoom.Center.y + 0.95f;
                }
                SpawnMonster(position, 60f, 3.6f, 14f);
            }
        }
    }
}
