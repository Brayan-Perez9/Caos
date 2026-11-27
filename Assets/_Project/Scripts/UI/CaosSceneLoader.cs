using UnityEngine;
using UnityEngine.SceneManagement;

namespace Caos.UI
{
    /// <summary>
    /// Enrutador de pantallas. No dibuja nada: lleva la petición a la escena
    /// Loading, que es un módulo independiente y muestra el progreso real.
    /// </summary>
    public class CaosSceneLoader : MonoBehaviour
    {
        const string LoadingScene = "Loading";

        public class Request
        {
            public string SceneName;
            public string Title;
            public string Subtitle;
            public string Detail;
            public Texture2D Art;
        }

        static Request _request;
        static bool _loading;

        public static bool IsLoading => _loading;
        public static Request Current => _request;

        public static void Begin(string sceneName, string title, string subtitle,
            string detail, Texture2D art)
        {
            if (_loading || string.IsNullOrEmpty(sceneName)) return;

            CaosFrontendArt.BeginPreload();

            _request = new Request
            {
                SceneName = sceneName,
                Title = string.IsNullOrWhiteSpace(title) ? "ARENA CAOS" : title,
                Subtitle = subtitle ?? string.Empty,
                Detail = detail ?? string.Empty,
                Art = art,
            };
            _loading = true;

            AsyncOperation operation = SceneManager.LoadSceneAsync(LoadingScene);
            if (operation != null) return;

            // Fallback por si la escena Loading no estuviera en Build Settings.
            Debug.LogError($"[Caos] No se pudo cargar la escena '{LoadingScene}'.");
            string target = sceneName;
            _request = null;
            _loading = false;
            SceneManager.LoadScene(target);
        }

        public static void Finish()
        {
            _loading = false;
            _request = null;
        }
    }
}
