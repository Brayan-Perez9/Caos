using System.Collections.Generic;

namespace Caos.Dungeon
{
    /// <summary>
    /// Genera el orden de salas de una partida. Usa un RNG propio con semilla:
    /// la misma semilla produce siempre la misma ruta y las pruebas pueden
    /// comprobar que la entrada va primero y el jefe al final.
    /// </summary>
    public static class DungeonRoute
    {
        public static readonly DungeonRoomKind[] Required =
        {
            DungeonRoomKind.Entrada,
            DungeonRoomKind.Exploracion,
            DungeonRoomKind.Supervivencia,
            DungeonRoomKind.Carrera,
            DungeonRoomKind.Oleadas,
            DungeonRoomKind.Recompensa,
            DungeonRoomKind.Jefe
        };

        /// <summary>Ruta jugable: entrada, salas intermedias barajadas, jefe final.</summary>
        public static List<DungeonRoomKind> Build(int seed, int extraRooms = 0)
        {
            var rng = new System.Random(seed);
            var middle = new List<DungeonRoomKind>
            {
                DungeonRoomKind.Exploracion,
                DungeonRoomKind.Supervivencia,
                DungeonRoomKind.Carrera,
                DungeonRoomKind.Oleadas,
                DungeonRoomKind.Recompensa
            };

            Shuffle(middle, rng);

            // Las salas extra repiten desafíos ya implementados, nunca sustituyen
            // a las obligatorias. Se añaden antes del jefe.
            DungeonRoomKind[] extras =
            {
                DungeonRoomKind.Supervivencia,
                DungeonRoomKind.Carrera,
                DungeonRoomKind.Oleadas
            };
            for (int i = 0; i < extraRooms && i < extras.Length; i++)
            {
                int insert = rng.Next(middle.Count + 1);
                middle.Insert(insert, extras[rng.Next(extras.Length)]);
            }

            var route = new List<DungeonRoomKind> { DungeonRoomKind.Entrada };
            route.AddRange(middle);
            route.Add(DungeonRoomKind.Jefe);
            return route;
        }

        static void Shuffle(List<DungeonRoomKind> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                DungeonRoomKind swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

        /// <summary>Comprueba que una ruta no pierda las salas obligatorias.</summary>
        public static bool IsValid(IList<DungeonRoomKind> route)
        {
            if (route == null || route.Count < Required.Length) return false;
            if (route[0] != DungeonRoomKind.Entrada) return false;
            if (route[route.Count - 1] != DungeonRoomKind.Jefe) return false;

            for (int i = 0; i < Required.Length; i++)
                if (!route.Contains(Required[i])) return false;

            return true;
        }
    }
}
