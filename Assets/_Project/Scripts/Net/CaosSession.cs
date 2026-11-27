namespace Caos.Net
{
    /// <summary>Datos efímeros que se comparten entre el lobby y la misión.</summary>
    public static class CaosSession
    {
        public static bool NetworkGame;
        public static bool IsHost;
        public static string LocalName = "Jugador";
        public static string RoomCode = string.Empty;

        /// <summary>Semilla de la mazmorra; 0 significa «elegir una nueva».</summary>
        public static int DungeonSeed;

        public static void ResetForMenu()
        {
            NetworkGame = false;
            IsHost = false;
            RoomCode = string.Empty;
            DungeonSeed = 0;
        }

        public static void Prepare(bool host, string name)
        {
            NetworkGame = true;
            IsHost = host;
            LocalName = string.IsNullOrWhiteSpace(name) ? "Jugador" : name.Trim();
        }
    }
}
