using UnityEngine;
using Caos.Maps;

namespace Caos.Dungeon
{
    /// <summary>Jefe final: reutiliza ArenaBoss y sus poderes sin cambiar su lógica.</summary>
    public class DungeonBossRoom : DungeonRoom
    {
        bool _spawned;

        public override string ProgressText => Completed
            ? "Guardián derrotado"
            : _spawned && Director.Bridge.BossAlive
                ? $"Guardián {Director.Bridge.BossHealth01 * 100f:0}%"
                : "Preparando al guardián…";

        protected override void OnBegin()
        {
            _spawned = false;
            Director.SetWeapon(Definition.weapon, Definition.element);

            Vector3 position = Center + Vector3.forward * (Radius * 0.42f);
            Director.SpawnBoss(position, Definition.bossKind, BossIndex(Definition.bossKind),
                Definition.bossHealth > 0f ? Definition.bossHealth : 720f,
                Definition.bossSpeed > 0f ? Definition.bossSpeed : 2.8f,
                Definition.bossDamage > 0f ? Definition.bossDamage : 24f);
            _spawned = true;

            Director.ShowBanner("JEFE FINAL — " + Title, 3.4f);
        }

        protected override void OnTick(float deltaTime)
        {
            if (_spawned && !Director.Bridge.BossAlive) Complete();
        }

        protected override void OnBossDefeated() => Complete();

        static int BossIndex(ArenaBossKind kind)
        {
            if (kind == ArenaBossKind.Tormenta) return 2;
            if (kind == ArenaBossKind.Canion) return 1;
            return 0;
        }
    }
}
