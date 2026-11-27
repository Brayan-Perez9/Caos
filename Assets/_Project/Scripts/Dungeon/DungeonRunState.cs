using System.Collections.Generic;
using UnityEngine;

namespace Caos.Dungeon
{
    /// <summary>
    /// Progreso de una partida de mazmorra. No se guarda entre partidas:
    /// es estado en memoria, como las mejoras de la arena.
    /// </summary>
    public class DungeonRunState
    {
        public int Coins;
        public int RoomsCleared;
        public float DamageMultiplier = 1f;
        public float SpeedMultiplier = 1f;
        public float RewardMultiplier = 1f;
        public float EnemyDamageMultiplier = 1f;
        public float EnemySpeedMultiplier = 1f;
        public bool BurnOnHit;
        public bool LethalShield;

        public readonly List<string> RelicIds = new List<string>();
        public readonly List<string> RelicTitles = new List<string>();

        public int AddCoins(int amount)
        {
            int final = Mathf.Max(0, Mathf.RoundToInt(amount * RewardMultiplier));
            Coins += final;
            return final;
        }

        public void ApplyRelic(DungeonRelicDefinition relic)
        {
            if (relic == null) return;

            switch (relic.effect)
            {
                case DungeonRelicEffect.Fuego:
                    BurnOnHit = true;
                    break;
                case DungeonRelicEffect.Velocidad:
                    SpeedMultiplier *= relic.magnitude;
                    break;
                case DungeonRelicEffect.Escudo:
                    LethalShield = true;
                    break;
                case DungeonRelicEffect.MonedaMaldita:
                    RewardMultiplier *= relic.magnitude;
                    EnemyDamageMultiplier *= 1.25f;
                    EnemySpeedMultiplier *= 1.12f;
                    break;
                case DungeonRelicEffect.Danio:
                    DamageMultiplier *= relic.magnitude;
                    break;
                case DungeonRelicEffect.Vida:
                    // La vida se aplica en el director, que tiene el ChaosHealth del jugador.
                    break;
            }

            RelicIds.Add(relic.id);
            RelicTitles.Add(relic.title);
        }

        public string RelicsText
        {
            get
            {
                if (RelicTitles.Count == 0) return "Sin reliquias";
                return string.Join(" · ", RelicTitles.ToArray());
            }
        }
    }
}
