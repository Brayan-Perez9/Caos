using UnityEngine.SceneManagement;

namespace Caos.UI
{
    /// <summary>
    /// Identifica las escenas del armazón (entrada, lobby, personalización y
    /// carga). Ningún módulo del juego debe dibujar HUD ni ventanas legacy
    /// mientras una de estas escenas esté activa.
    /// </summary>
    public static class CaosShell
    {
        public const string MenuScene = "Menu";
        public const string LoadingScene = "Loading";
        public const string LobbyScene = "FrontendLobby";
        public const string CustomizeScene = "Customize";
        public const string MultiplayerScene = "Lobby";

        public static bool IsShellScene
        {
            get
            {
                string name = SceneManager.GetActiveScene().name;
                return name == MenuScene || name == LoadingScene ||
                       name == LobbyScene || name == CustomizeScene;
            }
        }

        public static bool IsBlocked => IsShellScene || CaosSceneLoader.IsLoading;
    }
}
