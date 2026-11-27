using UnityEngine;
using Caos.Maps;

namespace Caos.Dungeon
{
    /// <summary>
    /// Jefe final en dos fases: reutiliza ArenaBoss, y al caer el guardián
    /// aparece su forma suprema (superjefe) con más vida, tamaño y rayos.
    /// Durante la pelea llegan refuerzos para que no sea un duelo solitario.
    /// </summary>
    public class DungeonBossRoom : DungeonRoom
    {
        const int PhasePendingBoss = 0;
        const int PhaseBoss = 1;
        const int PhasePendingSuper = 2;
        const int PhaseSuper = 3;

        int _phase;
        float _retry;
        float _nextAdds;

        public override string ProgressText
        {
            get
            {
                if (Completed) return "Guardián derrotado";
                if (_phase == PhasePendingSuper) return "El guardián se levanta…";
                if (Director.Bridge != null && Director.Bridge.BossAlive)
                    return (_phase == PhaseSuper ? "SUPERJEFE " : "Guardián ") +
                        $"{Director.Bridge.BossHealth01 * 100f:0}%";
                return "Preparando al guardián…";
            }
        }

        protected override void OnBegin()
        {
            _phase = PhasePendingBoss;
            _retry = 0f;
            DungeonConfig config = Director.Config;
            _nextAdds = Time.time + (config != null ? Mathf.Max(3f, config.bossAddInterval) : 5.5f);
            Director.SetWeapon(Definition.weapon, Definition.element);
            TrySpawnBoss();
            Director.ShowBanner("JEFE FINAL — " + Title, 3.4f);
        }

        void TrySpawnBoss()
        {
            if (_phase != PhasePendingBoss) return;

            float scale = Mathf.Lerp(1f, Director.DifficultyScale, 0.6f);
            Vector3 position = Center + Vector3.forward * (Radius * 0.42f);
            ArenaBoss boss = Director.SpawnBoss(position, Definition.bossKind,
                BossIndex(Definition.bossKind),
                Mathf.Max(200f, Definition.bossHealth) * scale,
                Mathf.Max(1f, Definition.bossSpeed),
                Mathf.Max(5f, Definition.bossDamage) * scale);
            if (boss != null) _phase = PhaseBoss;
        }

        void TrySpawnSuperBoss()
        {
            if (_phase != PhasePendingSuper) return;

            DungeonConfig config = Director.Config;
            float scale = Mathf.Lerp(1f, Director.DifficultyScale, 0.6f);
            float health = config != null && config.superBossHealth > 0f
                ? config.superBossHealth : Definition.bossHealth * 1.8f;
            float speed = config != null && config.superBossSpeed > 0f
                ? config.superBossSpeed : 3.2f;
            float damage = config != null && config.superBossDamage > 0f
                ? config.superBossDamage : Definition.bossDamage * 1.3f;

            Vector3 position = Center + Vector3.back * (Radius * 0.36f);
            ArenaBoss boss = Director.SpawnSuperBoss(position, Definition.bossKind,
                BossIndex(Definition.bossKind), health * scale, speed, damage * scale);
            if (boss != null)
            {
                _phase = PhaseSuper;
                Director.ShowBanner("SUPERJEFE — " + BossLabel(Definition.bossKind), 3f);
                ArenaAudio.Play(ArenaAudio.Sound.Boss, position, 1f);
            }
        }

        protected override void OnTick(float deltaTime)
        {
            if (_phase == PhasePendingBoss)
            {
                _retry -= deltaTime;
                if (_retry <= 0f)
                {
                    _retry = 0.8f;
                    TrySpawnBoss();
                }
                return;
            }

            if (_phase == PhasePendingSuper)
            {
                _retry -= deltaTime;
                if (_retry <= 0f)
                {
                    _retry = 0.7f;
                    TrySpawnSuperBoss();
                }
                return;
            }

            if (Time.time < _nextAdds) return;

            DungeonConfig config = Director.Config;
            _nextAdds = Time.time + (config != null ? Mathf.Max(3f, config.bossAddInterval) : 5.5f);
            int cap = Mathf.Min(10, 5 + Director.PartySize + (_phase == PhaseSuper ? 2 : 0));
            if (Director.Bridge.ActiveEnemyCount >= cap) return;

            int adds = Mathf.Max(1, config != null ? config.bossAdds : 3);
            for (int i = 0; i < adds; i++)
            {
                DungeonEnemyKind kind = i == 0 ? DungeonEnemyKind.Lanzador : DungeonEnemyKind.Normal;
                Director.SpawnVariant(RandomPoint(4f), kind,
                    54f * Director.DifficultyScale, 3.4f, 14f * Director.DifficultyScale);
            }
            Director.ShowBanner("EL JEFE INVOCA REFUERZOS", 1.6f);
        }

        protected override void OnBossDefeated()
        {
            if (_phase == PhaseBoss)
            {
                _phase = PhasePendingSuper;
                _retry = 1.8f;
                Director.State?.AddCoins(35);
                Director.ShowBanner("¡GUARDIÁN CAÍDO! VIENE SU FORMA SUPREMA", 3f);
                return;
            }

            if (_phase == PhaseSuper)
            {
                Director.State?.AddCoins(60);
                Complete();
            }
        }

        static string BossLabel(ArenaBossKind kind)
        {
            if (kind == ArenaBossKind.Canion) return "CAÑÓN";
            if (kind == ArenaBossKind.Tormenta) return "TORMENTA";
            return "MARTILLO";
        }

        static int BossIndex(ArenaBossKind kind)
        {
            if (kind == ArenaBossKind.Tormenta) return 2;
            if (kind == ArenaBossKind.Canion) return 1;
            return 0;
        }
    }
}
