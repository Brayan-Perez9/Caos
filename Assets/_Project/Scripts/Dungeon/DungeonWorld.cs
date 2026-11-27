using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Caos.Dungeon
{
    /// <summary>
    /// Construye LA mazmorra en runtime como un único mapa continuo: salas
    /// unidas por pasillos y escaleras, tramos de cueva con bajadas y subidas,
    /// y atajos laterales que se abren al superar cada sala. La geometría es
    /// determinista: depende solo de la ruta y del orden de la sala.
    /// No hay assets externos ni escenas que editar.
    /// </summary>
    public static class DungeonWorld
    {
        const float CorridorLength = 6f;
        const float WallHeight = 3f;
        const float DoorGap = 3.6f;
        const float CaveDrop = 4f;

        // 4 m repartidos en 16 peldaños: 0,25 m por escalón, por debajo del
        // stepOffset de 0,35 m del CharacterController del jugador.
        const int StairSteps = 16;

        static bool _caveCeilingLogged;

        /// <summary>Altura del suelo de una sala dentro del mapa único.</summary>
        public static float HeightFor(int index, int total)
        {
            if (total <= 1) return 0f;

            float t = index / (float)(total - 1);
            if (t < 0.28f) return 0f;             // superficie
            if (t < 0.42f) return -CaveDrop;      // bajada a la cueva
            if (t < 0.62f) return -CaveDrop * 2f; // fondo de cueva
            if (t < 0.78f) return -CaveDrop;      // subida
            return 0f;                            // cima y jefe
        }

        public static bool IsCave(int index, int total) =>
            HeightFor(index, total) < -0.01f;

        /// <summary>Salas 1, 4, 7... tienen un camino lateral alternativo.</summary>
        public static bool HasShortcut(int index) => index % 3 == 1;

        public static string ZoneLabel(int index, int total)
        {
            float height = HeightFor(index, total);
            if (height < -0.01f)
            {
                float next = HeightFor(Mathf.Min(index + 1, total - 1), total);
                if (next < height) return "BAJADA";
                if (next > height) return "SUBIDA";
                return "CUEVA";
            }
            return index > 0 && IsCave(index - 1, total) ? "CIMA" : "SUPERFICIE";
        }

        /// <summary>Símbolo del mapa superior: descenso, ascenso o cueva.</summary>
        public static string ZoneMark(int index, int total)
        {
            float height = HeightFor(index, total);
            float next = HeightFor(Mathf.Min(index + 1, total - 1), total);
            if (next < height) return "▼";
            if (next > height) return "▲";
            if (height < -0.01f) return "•";
            return "";
        }

        public static DungeonRoom[] Build(DungeonDirector director, DungeonConfig config,
            IList<DungeonRoomKind> route)
        {
            GameObject root = new GameObject("DungeonWorld");
            root.transform.SetParent(director.transform, false);

            float radius = Mathf.Max(9f, config.roomRadius);
            float spacing = radius * 2f + CorridorLength;
            int total = route.Count;
            var rooms = new DungeonRoom[total];

            DungeonConfig fallback = null;
            int shortcuts = 0;
            int caves = 0;

            for (int i = 0; i < total; i++)
            {
                DungeonRoomKind kind = route[i];
                Vector3 center = new Vector3(0f, HeightFor(i, total), i * spacing);

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

                GameObject roomObject = new GameObject($"Room_{i:00}_{kind}");
                roomObject.transform.SetParent(root.transform, false);
                roomObject.transform.position = center;

                DungeonRoom room = AddRoomComponent(roomObject, kind);
                room.Configure(director, definition, i, center, radius);

                bool shortcut = HasShortcut(i) && i < total - 1;
                BuildRoomGeometry(roomObject.transform, center, radius, kind,
                    i == 0, i == total - 1, shortcut, room);

                if (shortcut) shortcuts++;
                if (IsCave(i, total))
                {
                    BuildCaveDressing(roomObject.transform, center, radius);
                    caves++;
                }

                if (i < total - 1)
                {
                    Vector3 next = new Vector3(0f, HeightFor(i + 1, total), (i + 1) * spacing);
                    BuildConnector(root.transform, center, next, radius);
                }

                rooms[i] = room;
            }

            Debug.Log($"[Caos] Mapa único: {total} salas conectadas, " +
                $"atajos={shortcuts}, salas de cueva={caves}, semilla={director.Seed}");
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
            DungeonRoomKind kind, bool first, bool last, bool shortcut, DungeonRoom room)
        {
            DungeonVisuals.Box("Floor", center + Vector3.down * 0.3f,
                new Vector3(radius * 2f, 0.6f, radius * 2f),
                DungeonPalette.Floor(kind), parent);

            if (shortcut)
            {
                BuildSideWallWithGaps(parent, center, radius, room);
                BuildShortcutLane(parent, center, radius);
            }
            else
            {
                DungeonVisuals.Box("Wall_East", center + Vector3.right * radius
                    + Vector3.up * (WallHeight * 0.5f),
                    new Vector3(0.6f, WallHeight, radius * 2f), DungeonPalette.Wall, parent);
            }

            DungeonVisuals.Box("Wall_West", center + Vector3.left * radius
                + Vector3.up * (WallHeight * 0.5f),
                new Vector3(0.6f, WallHeight, radius * 2f), DungeonPalette.Wall, parent);

            BuildEndWall(parent, center, radius, radius, last);
            BuildEndWall(parent, center, -radius, radius, first);
        }

        /// <summary>Muro este con dos huecos: entrada y salida del atajo.</summary>
        static void BuildSideWallWithGaps(Transform parent, Vector3 center,
            float radius, DungeonRoom room)
        {
            float gapHalf = DoorGap * 0.4f;
            float z1 = center.z - radius * 0.45f;
            float z2 = center.z + radius * 0.55f;
            float start = center.z - radius;

            for (int i = 0; i < 2; i++)
            {
                float gap = i == 0 ? z1 : z2;
                BuildEastSegment(parent, center, radius, start, gap - gapHalf);

                GameObject gate = DungeonVisuals.Box($"ShortcutGate_{i}",
                    new Vector3(center.x + radius, center.y + 1.4f, gap),
                    new Vector3(0.6f, 2.8f, gapHalf * 2f),
                    DungeonPalette.ExitClosed, parent);
                room.RegisterActivation(gate.GetComponent<Renderer>(),
                    gate.GetComponent<BoxCollider>());

                start = gap + gapHalf;
            }
            BuildEastSegment(parent, center, radius, start, center.z + radius);
        }

        static void BuildEastSegment(Transform parent, Vector3 center, float radius,
            float zFrom, float zTo)
        {
            float length = zTo - zFrom;
            if (length <= 0.1f) return;

            DungeonVisuals.Box("Wall_East",
                new Vector3(center.x + radius, center.y + WallHeight * 0.5f,
                    (zFrom + zTo) * 0.5f),
                new Vector3(0.6f, WallHeight, length), DungeonPalette.Wall, parent);
        }

        /// <summary>Camino lateral que vuelve a unirse a la salida de la sala.</summary>
        static void BuildShortcutLane(Transform parent, Vector3 center, float radius)
        {
            float z1 = center.z - radius * 0.45f;
            float z2 = center.z + radius * 0.55f;
            float innerX = center.x + radius;
            float laneX = innerX + 3.2f;
            float depth = z2 - z1 + DoorGap * 0.8f;

            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? z1 : z2;
                DungeonVisuals.Box($"ShortcutLink_{i}",
                    new Vector3((innerX + laneX) * 0.5f, center.y - 0.3f, z),
                    new Vector3(laneX - innerX + 0.6f, 0.6f, DoorGap * 0.8f),
                    DungeonPalette.Corridor, parent);
            }

            DungeonVisuals.Box("ShortcutLane_Floor",
                new Vector3(laneX, center.y - 0.3f, (z1 + z2) * 0.5f),
                new Vector3(3.2f, 0.6f, depth), DungeonPalette.Corridor, parent);
            DungeonVisuals.Box("ShortcutLane_Wall",
                new Vector3(laneX + 1.8f, center.y + WallHeight * 0.5f,
                    (z1 + z2) * 0.5f),
                new Vector3(0.4f, WallHeight, depth), DungeonPalette.Wall, parent);
        }

        /// <summary>Techo y rocas decorativas de los tramos subterráneos.</summary>
        static void BuildCaveDressing(Transform parent, Vector3 center, float radius)
        {
            GameObject ceiling = DungeonVisuals.Box("CaveCeiling",
                center + Vector3.up * (WallHeight + 0.35f),
                new Vector3(radius * 2f, 0.7f, radius * 2f),
                DungeonPalette.CaveCeiling, parent, false);
            // No proyecta sombra: así el tramo inferior sigue iluminado y se ve
            // a través del techo sin añadir colliders ni assets externos.
            Renderer ceilingRenderer = ceiling.GetComponent<Renderer>();
            if (ceilingRenderer != null) ceilingRenderer.shadowCastingMode = ShadowCastingMode.Off;
            if (!_caveCeilingLogged)
            {
                Debug.Log("[Caos] Techo de cueva translúcido: se ve el nivel inferior");
                _caveCeilingLogged = true;
            }

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                float ring = radius * 0.72f;
                Vector3 rock = center + new Vector3(Mathf.Cos(angle) * ring,
                    0.7f, Mathf.Sin(angle) * ring);
                DungeonVisuals.Cylinder($"CaveRock_{i:00}", rock,
                    Vector3.one * 1.4f, DungeonPalette.Wall, parent, false);

                Vector3 spike = center + new Vector3(Mathf.Cos(angle) * ring * 0.6f,
                    WallHeight - 0.2f, Mathf.Sin(angle) * ring * 0.6f);
                DungeonVisuals.Cylinder($"CaveSpike_{i:00}", spike,
                    new Vector3(0.35f, 0.9f, 0.35f), DungeonPalette.Hazard, parent, false);
            }
        }

        /// <summary>
        /// Une dos salas con suelo plano o con peldaños cuando cambia la cota.
        /// Los peldaños son geometría con colisionador; las huellas y barandas
        /// de encima son solo decoración.
        /// </summary>
        static void BuildConnector(Transform parent, Vector3 from, Vector3 to, float radius)
        {
            float z0 = from.z + radius;
            float z1 = to.z - radius;
            float length = z1 - z0;
            float rise = to.y - from.y;
            float middleZ = (z0 + z1) * 0.5f;

            if (Mathf.Abs(rise) < 0.01f)
            {
                DungeonVisuals.Box("Corridor_Floor",
                    new Vector3(from.x, from.y - 0.3f, middleZ),
                    new Vector3(4.2f, 0.6f, length), DungeonPalette.Corridor, parent);

                for (int side = -1; side <= 1; side += 2)
                {
                    DungeonVisuals.Box(side < 0 ? "Corridor_West" : "Corridor_East",
                        new Vector3(from.x + side * 2.1f,
                            from.y + WallHeight * 0.5f, middleZ),
                        new Vector3(0.4f, WallHeight, length), DungeonPalette.Wall, parent);
                }
                return;
            }

            string prefix = rise < 0f ? "CaveLower_" : "CaveUpper_";
            float depth = length / StairSteps;

            for (int i = 0; i < StairSteps; i++)
            {
                float y = Mathf.Lerp(from.y, to.y, (i + 1f) / StairSteps);
                float z = z0 + depth * (i + 0.5f);

                DungeonVisuals.Box($"{prefix}{i:00}",
                    new Vector3(from.x, y - 0.3f, z),
                    new Vector3(4.2f, 0.6f, depth + 0.08f),
                    DungeonPalette.Corridor, parent);
                DungeonVisuals.Box($"CaveStair_{i:00}",
                    new Vector3(from.x, y + 0.012f, z),
                    new Vector3(4.0f, 0.024f, depth * 0.8f),
                    DungeonPalette.Checkpoint, parent, false);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                DungeonVisuals.Box(side < 0 ? "CaveRail_W" : "CaveRail_E",
                    new Vector3(from.x + side * 2.1f,
                        (from.y + to.y) * 0.5f + WallHeight * 0.5f, middleZ),
                    new Vector3(0.4f, WallHeight + Mathf.Abs(rise), length),
                    DungeonPalette.Wall, parent);
            }
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
    }
}
