using System.Collections.Generic;
using UnityEngine;

namespace Caos.Dungeon
{
    /// <summary>
    /// Construye la mazmorra en runtime: salas alineadas en Z, con suelos,
    /// muros, puertas y pasillos. No hay assets externos ni escenas que editar.
    /// </summary>
    public static class DungeonWorld
    {
        const float CorridorLength = 6f;
        const float WallHeight = 3f;
        const float DoorGap = 3.6f;

        public static DungeonRoom[] Build(DungeonDirector director, DungeonConfig config,
            IList<DungeonRoomKind> route)
        {
            GameObject root = new GameObject("DungeonWorld");
            root.transform.SetParent(director.transform, false);

            float radius = Mathf.Max(9f, config.roomRadius);
            float spacing = radius * 2f + CorridorLength;
            var rooms = new DungeonRoom[route.Count];

            DungeonConfig fallback = null;
            for (int i = 0; i < route.Count; i++)
            {
                DungeonRoomKind kind = route[i];
                Vector3 center = new Vector3(0f, 0f, i * spacing);

                GameObject roomObject = new GameObject($"Room_{i:00}_{kind}");
                roomObject.transform.SetParent(root.transform, false);
                roomObject.transform.position = center;

                BuildRoomGeometry(roomObject.transform, center, radius, kind,
                    i == 0, i == route.Count - 1);

                if (i < route.Count - 1)
                    BuildCorridor(root.transform, center, radius);

                DungeonRoomDefinition definition = config.Find(kind);
                if (definition == null)
                {
                    if (fallback == null)
                    {
                        fallback = ScriptableObject.CreateInstance<DungeonConfig>();
                        fallback.ApplyDefaults();
                    }
                    definition = fallback.Find(kind);
                }

                DungeonRoom room = AddRoomComponent(roomObject, kind);
                room.Configure(director, definition, i, center, radius);
                rooms[i] = room;
            }

            return rooms;
        }

        static DungeonRoom AddRoomComponent(GameObject go, DungeonRoomKind kind)
        {
            switch (kind)
            {
                case DungeonRoomKind.Entrada:
                    return go.AddComponent<DungeonEntranceRoom>();
                case DungeonRoomKind.Exploracion:
                    return go.AddComponent<DungeonExplorationRoom>();
                case DungeonRoomKind.Supervivencia:
                    return go.AddComponent<DungeonSurvivalRoom>();
                case DungeonRoomKind.Carrera:
                    return go.AddComponent<DungeonRaceRoom>();
                case DungeonRoomKind.Oleadas:
                    return go.AddComponent<DungeonWavesRoom>();
                case DungeonRoomKind.Recompensa:
                    return go.AddComponent<DungeonRewardRoom>();
                case DungeonRoomKind.Jefe:
                    return go.AddComponent<DungeonBossRoom>();
                default:
                    return go.AddComponent<DungeonEntranceRoom>();
            }
        }

        static void BuildRoomGeometry(Transform parent, Vector3 center, float radius,
            DungeonRoomKind kind, bool first, bool last)
        {
            DungeonVisuals.Box("Floor", center + Vector3.down * 0.3f,
                new Vector3(radius * 2f, 0.6f, radius * 2f),
                DungeonPalette.Floor(kind), parent);

            DungeonVisuals.Box("Wall_East", center + Vector3.right * radius
                + Vector3.up * (WallHeight * 0.5f),
                new Vector3(0.6f, WallHeight, radius * 2f), DungeonPalette.Wall, parent);
            DungeonVisuals.Box("Wall_West", center + Vector3.left * radius
                + Vector3.up * (WallHeight * 0.5f),
                new Vector3(0.6f, WallHeight, radius * 2f), DungeonPalette.Wall, parent);

            BuildEndWall(parent, center, radius, radius, last);
            BuildEndWall(parent, center, -radius, radius, first);
        }

        /// <summary>Muro con hueco central y marco, o muro lleno en los extremos.</summary>
        static void BuildEndWall(Transform parent, Vector3 center, float zOffset,
            float radius, bool solid)
        {
            Vector3 wallCenter = center + Vector3.forward * zOffset
                + Vector3.up * (WallHeight * 0.5f);

            if (solid)
            {
                DungeonVisuals.Box("Wall_Solid", wallCenter,
                    new Vector3(radius * 2f, WallHeight, 0.6f), DungeonPalette.Wall, parent);
                return;
            }

            float segment = Mathf.Max(0.5f, radius - DoorGap * 0.5f);
            float offset = DoorGap * 0.5f + segment * 0.5f;

            DungeonVisuals.Box("Wall_Left", wallCenter + Vector3.left * offset,
                new Vector3(segment, WallHeight, 0.6f), DungeonPalette.Wall, parent);
            DungeonVisuals.Box("Wall_Right", wallCenter + Vector3.right * offset,
                new Vector3(segment, WallHeight, 0.6f), DungeonPalette.Wall, parent);

            DungeonVisuals.Box("Door_Post_L", wallCenter + Vector3.left * (DoorGap * 0.5f),
                new Vector3(0.42f, WallHeight, 0.5f), DungeonPalette.ExitClosed, parent);
            DungeonVisuals.Box("Door_Post_R", wallCenter + Vector3.right * (DoorGap * 0.5f),
                new Vector3(0.42f, WallHeight, 0.5f), DungeonPalette.ExitClosed, parent);
            DungeonVisuals.Box("Door_Lintel", wallCenter + Vector3.up * (WallHeight * 0.5f),
                new Vector3(DoorGap + 0.8f, 0.42f, 0.5f), DungeonPalette.ExitClosed, parent);
        }

        static void BuildCorridor(Transform parent, Vector3 center, float radius)
        {
            Vector3 middle = center + Vector3.forward * (radius + CorridorLength * 0.5f);

            DungeonVisuals.Box("Corridor_Floor", middle + Vector3.down * 0.3f,
                new Vector3(4.2f, 0.6f, CorridorLength), DungeonPalette.Corridor, parent);
            DungeonVisuals.Box("Corridor_East", middle + Vector3.right * 2.1f
                + Vector3.up * (WallHeight * 0.5f),
                new Vector3(0.4f, WallHeight, CorridorLength), DungeonPalette.Wall, parent);
            DungeonVisuals.Box("Corridor_West", middle + Vector3.left * 2.1f
                + Vector3.up * (WallHeight * 0.5f),
                new Vector3(0.4f, WallHeight, CorridorLength), DungeonPalette.Wall, parent);
        }
    }
}
