using UnityEngine;
using Caos.Maps;

namespace Caos.Dungeon
{
    public enum DungeonRoomKind
    {
        Entrada,
        Exploracion,
        Supervivencia,
        Carrera,
        Oleadas,
        Recompensa,
        Jefe
    }

    public enum DungeonRelicEffect
    {
        Fuego,
        Velocidad,
        Escudo,
        MonedaMaldita,
        Danio,
        Vida
    }

    /// <summary>Variantes de enemigo que aparecen en la mazmorra.</summary>
    public enum DungeonEnemyKind
    {
        Normal,
        Lanzador,
        Elite,
        Superboss
    }

    /// <summary>Una sala tal como la edita el diseñador en el Inspector.</summary>
    [System.Serializable]
    public class DungeonRoomDefinition
    {
        public DungeonRoomKind kind;
        public string title = "Sala";
        public string objective = "Completa la sala";
        public float duration = 0f;
        public int goal = 3;
        public int waveCount = 3;
        public int enemiesPerWave = 4;
        public ArenaWeaponKind weapon = ArenaWeaponKind.Pistola;
        public ArenaElement element = ArenaElement.Normal;
        public ArenaBossKind bossKind = ArenaBossKind.Martillo;
        public float bossHealth = 900f;
        public float bossSpeed = 2.8f;
        public float bossDamage = 26f;
    }

    /// <summary>Reliquia con efecto pasivo durante la partida.</summary>
    [System.Serializable]
    public class DungeonRelicDefinition
    {
        public string id = "reliquia";
        public string title = "Reliquia";
        public string description = "Efecto especial";
        public DungeonRelicEffect effect;
        public float magnitude = 1f;
    }

    /// <summary>Configuración de la mazmorra. El builder crea el asset por defecto.</summary>
    [CreateAssetMenu(fileName = "DungeonConfig", menuName = "Caos/Dungeon Config")]
    public class DungeonConfig : ScriptableObject
    {
        public int seed = 20261127;
        public float roomTransitionSeconds = 1.6f;
        public float roomRadius = 12f;
        public int roomClearCoins = 8;

        [Header("Dificultad")]
        public int casterEvery = 3;
        public int eliteWaveCount = 1;
        public int bossAdds = 3;
        public float bossAddInterval = 5.5f;
        public float superBossHealth = 1500f;
        public float superBossSpeed = 3.2f;
        public float superBossDamage = 34f;

        public DungeonRoomDefinition[] rooms;
        public DungeonRelicDefinition[] relics;

        public DungeonRoomDefinition Find(DungeonRoomKind kind)
        {
            if (rooms == null) return null;
            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i] != null && rooms[i].kind == kind) return rooms[i];
            return null;
        }

        /// <summary>Valores por defecto para que la mazmorra funcione sin editar el asset.</summary>
        public void ApplyDefaults()
        {
            if (rooms == null || rooms.Length == 0)
            {
                rooms = new[]
                {
                    Def(DungeonRoomKind.Entrada, "ENTRADA DEL CAOS", "Llega a la puerta luminosa"),
                    Def(DungeonRoomKind.Exploracion, "RUINAS DE LOS COFRES", "Recoge 3 cofres y encuentra la salida",
                        0f, 3, ArenaWeaponKind.Pistola, ArenaElement.Normal),
                    Def(DungeonRoomKind.Supervivencia, "SOBREVIVE AL CAOS", "Resiste a trampas, oleadas y lanzadores",
                        30f, 0, ArenaWeaponKind.Escopeta, ArenaElement.Fuego),
                    Def(DungeonRoomKind.Carrera, "CARRERA MORTAL", "Pasa 5 controles antes de que acabe el tiempo",
                        42f, 5, ArenaWeaponKind.Rifle, ArenaElement.Aire),
                    Def(DungeonRoomKind.Oleadas, "OLEADAS", "Limpia 4 oleadas y esquiva los rayos",
                        0f, 0, ArenaWeaponKind.Escopeta, ArenaElement.Rayo, 4, 6),
                    Def(DungeonRoomKind.Recompensa, "CÁMARA DEL TESORO", "Elige una recompensa (1 / 2 / 3)",
                        0f, 0, ArenaWeaponKind.Pistola, ArenaElement.Normal),
                    Def(DungeonRoomKind.Jefe, "JEFE FINAL", "Derrota al guardián y a su forma suprema",
                        0f, 0, ArenaWeaponKind.Rifle, ArenaElement.Fuego, 0, 0,
                        ArenaBossKind.Tormenta, 900f, 2.8f, 26f)
                };
            }

            if (relics == null || relics.Length == 0)
            {
                relics = new[]
                {
                    Relic("corazon_fuego", "CORAZÓN DE FUEGO", "Tus disparos queman", DungeonRelicEffect.Fuego, 1f),
                    Relic("botas_caos", "BOTAS DEL CAOS", "Velocidad +18 %", DungeonRelicEffect.Velocidad, 1.18f),
                    Relic("escudo_ancestral", "ESCUDO ANCESTRAL", "Bloquea un golpe mortal", DungeonRelicEffect.Escudo, 1f),
                    Relic("moneda_maldita", "MONEDA MALDITA", "Recompensas +50 %, enemigos más duros", DungeonRelicEffect.MonedaMaldita, 1.5f),
                    Relic("diente_caos", "DIENTE DEL CAOS", "Daño +25 %", DungeonRelicEffect.Danio, 1.25f),
                    Relic("vitalidad", "VITALIDAD", "Vida máxima +30", DungeonRelicEffect.Vida, 30f)
                };
            }

            NormalizeDifficulty();
        }

        /// <summary>
        /// El asset puede venir de una versión anterior con menos enemigos.
        /// Actualizamos los mínimos sin sobrescribir lo que ya haya editado
        /// el diseñador en el Inspector.
        /// </summary>
        void NormalizeDifficulty()
        {
            casterEvery = Mathf.Max(2, casterEvery);
            eliteWaveCount = Mathf.Max(1, eliteWaveCount);
            bossAdds = Mathf.Max(1, bossAdds);
            bossAddInterval = Mathf.Max(3f, bossAddInterval);
            superBossHealth = Mathf.Max(600f, superBossHealth);
            superBossSpeed = Mathf.Max(1f, superBossSpeed);
            superBossDamage = Mathf.Max(8f, superBossDamage);

            if (rooms == null) return;
            for (int i = 0; i < rooms.Length; i++)
            {
                DungeonRoomDefinition room = rooms[i];
                if (room == null) continue;

                if (room.kind == DungeonRoomKind.Oleadas)
                {
                    room.waveCount = Mathf.Max(4, room.waveCount);
                    room.enemiesPerWave = Mathf.Max(6, room.enemiesPerWave);
                    room.objective = $"Limpia {room.waveCount} oleadas y esquiva los rayos";
                }
                else if (room.kind == DungeonRoomKind.Supervivencia)
                {
                    room.duration = Mathf.Max(32f, room.duration);
                    room.objective = "Resiste a trampas, oleadas y lanzadores";
                }
                else if (room.kind == DungeonRoomKind.Jefe)
                {
                    room.objective = "Derrota al guardián y a su forma suprema";
                }
            }
        }

        static DungeonRoomDefinition Def(DungeonRoomKind kind, string title, string objective,
            float duration = 0f, int goal = 0, ArenaWeaponKind weapon = ArenaWeaponKind.Pistola,
            ArenaElement element = ArenaElement.Normal, int waves = 4, int perWave = 6,
            ArenaBossKind boss = ArenaBossKind.Martillo, float bossHp = 900f,
            float bossSpeed = 2.8f, float bossDamage = 26f)
        {
            return new DungeonRoomDefinition
            {
                kind = kind,
                title = title,
                objective = objective,
                duration = duration,
                goal = goal,
                waveCount = waves,
                enemiesPerWave = perWave,
                weapon = weapon,
                element = element,
                bossKind = boss,
                bossHealth = bossHp,
                bossSpeed = bossSpeed,
                bossDamage = bossDamage
            };
        }

        static DungeonRelicDefinition Relic(string id, string title, string description,
            DungeonRelicEffect effect, float magnitude)
        {
            return new DungeonRelicDefinition
            {
                id = id,
                title = title,
                description = description,
                effect = effect,
                magnitude = magnitude
            };
        }
    }
}
