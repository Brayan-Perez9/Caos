using UnityEngine;
using Caos.Maps;

namespace Caos.Dungeon
{
    /// <summary>
    /// Paleta de la mazmorra. Materiales cacheados para no crear uno por objeto
    /// (y para que nada salga magenta: todos pasan por ArenaMaterial).
    /// </summary>
    public static class DungeonPalette
    {
        static Material _wall;
        static Material _corridor;
        static Material _exitClosed;
        static Material _exitOpen;
        static Material _checkpoint;
        static Material _checkpointDone;
        static Material _hazard;
        static Material _coin;
        static Material _chest;
        static Material _heart;
        static Material _weapon;
        static readonly Material[] Relics = new Material[3];
        static readonly Material[] Floors = new Material[7];

        public static readonly Color UiYellow = new Color(1f, 0.82f, 0.34f);
        public static readonly Color UiCyan = new Color(0.30f, 0.82f, 1f);
        public static readonly Color UiCoral = new Color(1f, 0.42f, 0.42f);
        public static readonly Color UiViolet = new Color(0.58f, 0.46f, 1f);
        public static readonly Color UiPanel = new Color(0.05f, 0.07f, 0.11f, 0.84f);
        public static readonly Color UiText = new Color(0.96f, 0.98f, 1f);

        public static Material Floor(DungeonRoomKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= Floors.Length) index = 0;
            if (Floors[index] == null)
            {
                switch (kind)
                {
                    case DungeonRoomKind.Entrada:
                        Floors[index] = Make(new Color(0.22f, 0.44f, 0.58f));
                        break;
                    case DungeonRoomKind.Exploracion:
                        Floors[index] = Make(new Color(0.28f, 0.52f, 0.38f));
                        break;
                    case DungeonRoomKind.Supervivencia:
                        Floors[index] = Make(new Color(0.48f, 0.24f, 0.34f));
                        break;
                    case DungeonRoomKind.Carrera:
                        Floors[index] = Make(new Color(0.32f, 0.38f, 0.62f));
                        break;
                    case DungeonRoomKind.Oleadas:
                        Floors[index] = Make(new Color(0.52f, 0.38f, 0.20f));
                        break;
                    case DungeonRoomKind.Recompensa:
                        Floors[index] = Make(new Color(0.46f, 0.32f, 0.62f));
                        break;
                    default:
                        Floors[index] = Make(new Color(0.24f, 0.20f, 0.34f));
                        break;
                }
            }
            return Floors[index];
        }

        public static Material Wall => _wall ??= Make(new Color(0.16f, 0.19f, 0.28f));
        public static Material Corridor => _corridor ??= Make(new Color(0.20f, 0.24f, 0.34f));
        public static Material ExitClosed => _exitClosed ??= Make(new Color(0.35f, 0.38f, 0.44f));
        public static Material ExitOpen => _exitOpen ??= Make(new Color(0.34f, 1f, 0.62f), 0.4f);
        public static Material Checkpoint => _checkpoint ??= Make(new Color(0.45f, 0.82f, 1f), 0.4f);
        public static Material CheckpointDone => _checkpointDone ??= Make(new Color(0.36f, 1f, 0.54f), 0.5f);
        public static Material Hazard => _hazard ??= Make(new Color(1f, 0.30f, 0.28f), 0.35f);
        public static Material Coin => _coin ??= Make(new Color(1f, 0.78f, 0.18f), 0.5f);
        public static Material Chest => _chest ??= Make(new Color(0.95f, 0.50f, 0.16f), 0.35f);
        public static Material Heart => _heart ??= Make(new Color(1f, 0.30f, 0.44f), 0.4f);
        public static Material Weapon => _weapon ??= Make(new Color(0.42f, 0.90f, 1f), 0.5f);

        public static Material Relic(int index)
        {
            if (index < 0) index = 0;
            if (index > 2) index = 2;
            if (Relics[index] == null)
            {
                Color color = index == 0 ? UiYellow : index == 1 ? UiCyan : UiViolet;
                Relics[index] = Make(color, 0.55f);
            }
            return Relics[index];
        }

        static Material Make(Color color, float smoothness = 0.25f) =>
            ArenaMaterial.Create(color, 0f, smoothness);
    }
}
