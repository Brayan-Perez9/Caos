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
                    Def(DungeonRoomKind.Supervivencia, "SOBREVIVE AL CAOS", "Resiste 30 s a martillos y bolas",
                        30f, 0, ArenaWeaponKind.Escopeta, ArenaElement.Fuego),
                    Def(DungeonRoomKind.Carrera, "CARRERA MORTAL", "Pasa 5 controles antes de que acabe el tiempo",
                        42f, 5, ArenaWeaponKind.Rifle, ArenaElement.Aire),
                    Def(DungeonRoomKind.Oleadas, "OLEADAS", "Limpia 3 oleadas de monstruos",
                        0f, 0, ArenaWeaponKind.Escopeta, ArenaElement.Rayo, 3, 4),
                    Def(DungeonRoomKind.Recompensa, "CÁMARA DEL TESORO", "Elige una recompensa (1 / 2 / 3)",
                        0f, 0, ArenaWeaponKind.Pistola, ArenaElement.Normal),
                    Def(DungeonRoomKind.Jefe, "JEFE FINAL", "Derrota al guardián de la mazmorra",
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
        }

        static DungeonRoomDefinition Def(DungeonRoomKind kind, string title, string objective,
            float duration = 0f, int goal = 0, ArenaWeaponKind weapon = ArenaWeaponKind.Pistola,
            ArenaElement element = ArenaElement.Normal, int waves = 3, int perWave = 4,
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
