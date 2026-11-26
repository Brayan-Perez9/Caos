using UnityEngine;
using UnityEngine.SceneManagement;
using Caos.Net;
using Caos.Player;

namespace Caos.UI
{
    /// <summary>
    /// Entrada independiente al lobby en la escena Menu. No forma parte del
    /// panel de personalización: dibuja su propio botón arriba a la izquierda.
    /// </summary>
    public class CaosLobbyEntry : MonoBehaviour
    {
        GUIStyle _buttonStyle;

        void OnGUI()
        {
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
                SceneManager.LoadScene("Lobby");
            }
        }
    }
}
