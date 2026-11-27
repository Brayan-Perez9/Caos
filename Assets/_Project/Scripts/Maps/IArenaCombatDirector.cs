using UnityEngine;

namespace Caos.Maps
{
    /// <summary>
    /// Contrato mínimo que necesitan los monstruos, jefes y proyectiles de Arena.
    /// Permite que ArenaDirector y el director de mazmorras compartan las mismas
    /// criaturas sin que éstas dependan de una clase concreta.
    /// </summary>
    public interface IArenaCombatDirector
    {
        bool Finished { get; }
        bool UpgradePending { get; }

        float MonsterSpeed(float baseSpeed);
        float MonsterDamage(float baseDamage);

        /// <summary>Disparo del jugador, compartido por arena y mazmorra.</summary>
        void TryFire(Transform shooter, Vector3 aimPoint);

        /// <summary>Límite del mapa activo. La arena devuelve el punto tal cual.</summary>
        Vector3 ClampWorldPosition(Vector3 point);

        void DamagePlayer(float amount, Vector3 source);
        void ChainLightning(Vector3 origin, float damage, Component exclude);

        void MonsterKilled(ArenaMonster monster);
        void BossDefeated(ArenaBoss boss);

        void ShowBanner(string text, float duration = 2f);

        void SpawnEnemyProjectile(Vector3 origin, Vector3 direction,
            float speed, float damage, Color color);

        void SummonMinions(int count, Vector3 origin);
    }
}
