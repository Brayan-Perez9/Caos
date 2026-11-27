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
        public float bossHealth = 1250f;
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
        public float roomRadius = 16f;
        public int roomClearCoins = 8;

        [Header("Dificultad")]
        public int casterEvery = 2;
        public int eliteWaveCount = 2;
        public int bossAdds = 4;
        public float bossAddInterval = 4.2f;
        public float superBossHealth = 3200f;
        public float superBossSpeed = 3.3f;
        public float superBossDamage = 44f;

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
                    Def(DungeonRoomKind.Exploracion, "RUINAS DE LOS COFRES", "Recoge 5 cofres y encuentra la salida",
                        0f, 5, ArenaWeaponKind.Pistola, ArenaElement.Normal),
                    Def(DungeonRoomKind.Supervivencia, "SOBREVIVE AL CAOS", "Resiste a trampas, oleadas y lanzadores",
                        48f, 0, ArenaWeaponKind.Escopeta, ArenaElement.Fuego),
                    Def(DungeonRoomKind.Carrera, "CARRERA MORTAL", "Pasa 7 controles antes de que acabe el tiempo",
                        58f, 7, ArenaWeaponKind.Rifle, ArenaElement.Aire),
                    Def(DungeonRoomKind.Oleadas, "OLEADAS", "Limpia 6 oleadas y esquiva los rayos y cañones",
                        0f, 0, ArenaWeaponKind.Escopeta, ArenaElement.Rayo, 6, 8),
                    Def(DungeonRoomKind.Recompensa, "CÁMARA DEL TESORO", "Elige una recompensa (1 / 2 / 3)",
                        0f, 0, ArenaWeaponKind.Pistola, ArenaElement.Normal),
                    Def(DungeonRoomKind.Jefe, "JEFE FINAL", "Derrota al guardián y a su forma suprema",
                        0f, 0, ArenaWeaponKind.Rifle, ArenaElement.Fuego, 0, 0,
                        ArenaBossKind.Tormenta, 1900f, 2.9f, 34f)
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
            roomRadius = Mathf.Max(16f, roomRadius);
            casterEvery = Mathf.Max(2, casterEvery);
            eliteWaveCount = Mathf.Max(2, eliteWaveCount);
            bossAdds = Mathf.Max(4, bossAdds);
            bossAddInterval = Mathf.Max(4.2f, bossAddInterval);
            superBossHealth = Mathf.Max(3200f, superBossHealth);
            superBossSpeed = Mathf.Max(1f, superBossSpeed);
            superBossDamage = Mathf.Max(44f, superBossDamage);

            if (rooms == null) return;
            for (int i = 0; i < rooms.Length; i++)
            {
                DungeonRoomDefinition room = rooms[i];
                if (room == null) continue;

                if (room.kind == DungeonRoomKind.Exploracion)
                {
                    room.goal = Mathf.Max(5, room.goal);
                    room.objective = $"Recoge {room.goal} cofres y encuentra la salida";
                }
                else if (room.kind == DungeonRoomKind.Carrera)
                {
                    room.goal = Mathf.Max(7, room.goal);
                    room.duration = Mathf.Max(58f, room.duration);
                    room.objective = $"Pasa {room.goal} controles antes de que acabe el tiempo";
                }
                else if (room.kind == DungeonRoomKind.Oleadas)
                {
                    room.waveCount = Mathf.Max(6, room.waveCount);
                    room.enemiesPerWave = Mathf.Max(8, room.enemiesPerWave);
                    room.objective = $"Limpia {room.waveCount} oleadas y esquiva los rayos y cañones";
                }
                else if (room.kind == DungeonRoomKind.Supervivencia)
                {
                    room.duration = Mathf.Max(48f, room.duration);
                    room.objective = "Resiste a trampas, oleadas, lanzadores y cañones";
                }
                else if (room.kind == DungeonRoomKind.Jefe)
                {
                    room.bossHealth = Mathf.Max(1900f, room.bossHealth);
                    room.bossDamage = Mathf.Max(34f, room.bossDamage);
                    room.objective = "Derrota al guardián y a su forma suprema";
                }
            }
        }

        static DungeonRoomDefinition Def(DungeonRoomKind kind, string title, string objective,
            float duration = 0f, int goal = 0, ArenaWeaponKind weapon = ArenaWeaponKind.Pistola,
            ArenaElement element = ArenaElement.Normal, int waves = 6, int perWave = 8,
            ArenaBossKind boss = ArenaBossKind.Martillo, float bossHp = 1900f,
            float bossSpeed = 2.9f, float bossDamage = 34f)
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
