using UnityEngine;
using Caos.Net;
using Caos.Player;

namespace Caos.UI
{
    /// <summary>
    /// Entrada independiente al lobby en la escena Menu. Queda oculta mientras
    /// la nueva pantalla de entrada está activa.
    /// </summary>
    public class CaosLobbyEntry : MonoBehaviour
    {
        GUIStyle _buttonStyle;

        void OnGUI()
        {
            if (CaosFrontend.Active || CaosSceneLoader.IsLoading) return;

            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                };
            }

            if (GUI.Button(new Rect(24f, 24f, 240f, 46f), "LOBBY MULTIJUGADOR", _buttonStyle))
            {
                var character = CharacterCustomization.Instance != null
                    ? CharacterCustomization.Instance
                    : FindFirstObjectByType<CharacterCustomization>();
                character?.Save();

                CaosSession.ResetForMenu();
                CaosSession.LocalName = PlayerPrefs.GetString("Caos.PlayerName", "Jugador");
                CaosSceneLoader.Begin("Lobby", "ARENA CAOS",
                    "Conectando con la sala...", "Abriendo el lobby multijugador...",
                    CaosFrontendArt.Get(CaosFrontendArt.Slot.Startup));
            }
        }
    }
}
