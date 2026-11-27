using UnityEngine;
using UnityEngine.SceneManagement;

namespace Caos.UI
{
    /// <summary>
    /// Pantalla de carga dedicada. Vive sola en la escena Loading: recibe la
    /// petición estática de CaosSceneLoader, muestra el progreso real de la
    /// carga de escena y de los recursos, y solo entonces activa la escena
    /// destino. No hay HUD, personaje ni menús superpuestos aquí.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CaosLoadingScreen : MonoBehaviour
    {
        const float MinimumVisibleSeconds = 1.1f;

        // Unity deja el progreso justo por debajo de 0.9 cuando la escena ya
        // está lista (0.8999999...). Comparar contra 0.9 exacto bloquea la
        // activación para siempre. Usamos 0.85 y un margen para evitarlo.
        const float ActivationThreshold = 0.85f;
        const float ProgressEpsilon = 0.001f;
        const float ActivationFallbackSeconds = 8f;

        AsyncOperation _operation;
        float _elapsed;
        float _activationTime = -1f;
        bool _activationRequested;

        float _scale = 1f;
        float _offsetX;
        float _offsetY;

        CaosSceneLoader.Request Request => CaosSceneLoader.Current;

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void Start()
        {
            CaosFrontendArt.BeginPreload();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var request = Request;
            if (request == null || string.IsNullOrEmpty(request.SceneName))
            {
                SceneManager.LoadScene(CaosShell.MenuScene);
                return;
            }

            _operation = SceneManager.LoadSceneAsync(request.SceneName);
            if (_operation == null)
            {
                Debug.LogError($"[Caos] No se pudo cargar '{request.SceneName}'.");
                CaosSceneLoader.Finish();
                SceneManager.LoadScene(CaosShell.MenuScene);
                return;
            }

            // Mantenemos la escena Loading visible hasta que el progreso real
            // llegue al final y se haya podido leer el texto de destino.
            _operation.allowSceneActivation = false;
        }

        void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            if (_operation != null && !_activationRequested)
            {
                bool ready = _operation.progress >= ActivationThreshold - ProgressEpsilon;
                if (ready && _elapsed >= MinimumVisibleSeconds)
                {
                    Activate();
                }
            }

            // Si Unity no llega a activar la escena, forzamos la entrada para
            // que la pantalla de carga nunca se quede colgada.
            if (_activationRequested && _activationTime >= 0f &&
                Time.unscaledTime - _activationTime >= ActivationFallbackSeconds &&
                SceneManager.GetActiveScene().name == CaosShell.LoadingScene)
            {
                var request = Request;
                if (request != null && !string.IsNullOrEmpty(request.SceneName))
                {
                    Debug.LogWarning($"[Caos] La activación asíncrona no respondió; cargando '{request.SceneName}' directamente.");
                    string target = request.SceneName;
                    CaosSceneLoader.Finish();
                    enabled = false;
                    SceneManager.LoadScene(target);
                }
            }
        }

        void Activate()
        {
            string target = Request?.SceneName;

            // La escena ya está lista: limpiamos el estado global aquí mismo.
            // No depender de sceneLoaded evita que el lobby crea que sigue
            // cargando y se quede sin dibujar.
            CaosSceneLoader.Finish();

            _activationRequested = true;
            _activationTime = Time.unscaledTime;
            _operation.allowSceneActivation = true;
            Debug.Log($"[Caos] Activando escena '{target}' desde la carga.");
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == gameObject.scene.name) return;
            CaosSceneLoader.Finish();
        }

        void OnGUI()
        {
            var request = Request;
            if (request == null) return;

            var styles = CaosShellUI.GetStyles();
            var full = new Rect(0f, 0f, CaosShellUI.ReferenceWidth,
                CaosShellUI.ReferenceHeight);

            CaosShellUI.ComputeScale(out _scale, out _offsetX, out _offsetY);
            if (_scale <= 0f) return;

            Matrix4x4 previous = CaosShellUI.Begin(_scale, _offsetX, _offsetY);
            try
            {
                Texture2D art = request.Art != null
                    ? request.Art
                    : CaosFrontendArt.Get(CaosFrontendArt.Slot.Dungeon);

                CaosShellUI.DrawCover(full, art, 1f);
                CaosShellUI.DrawRect(full, new Color(0.01f, 0.02f, 0.06f, 0.36f));
                CaosShellUI.DrawRect(new Rect(0f, 700f, CaosShellUI.ReferenceWidth, 380f),
                    new Color(0.01f, 0.02f, 0.06f, 0.84f));

                CaosShellUI.DrawShadowLabel(new Rect(0f, 264f,
                        CaosShellUI.ReferenceWidth, 140f),
                    string.IsNullOrEmpty(request.Title) ? "ARENA CAOS" : request.Title,
                    styles.Title, new Color(0f, 0f, 0f, 0.82f));

                GUI.Label(new Rect(0f, 424f, CaosShellUI.ReferenceWidth, 54f),
                    request.Subtitle, styles.Subtitle);
                GUI.Label(new Rect(0f, 488f, CaosShellUI.ReferenceWidth, 40f),
                    request.Detail, styles.Hint);

                float progress = Progress();
                CaosShellUI.DrawBar(new Rect(560f, 800f, 800f, 22f), progress);
                GUI.Label(new Rect(560f, 752f, 800f, 34f),
                    $"{Mathf.RoundToInt(progress * 100f)}%", styles.Kicker);

                GUI.Label(new Rect(28f, 1032f, 320f, 34f), "omar hpta", styles.Credit);
            }
            finally
            {
                CaosShellUI.Restore(previous);
            }
        }

        float Progress()
        {
            float sceneProgress = _operation == null ? 0f :
                Mathf.Clamp01(_operation.progress / ActivationThreshold);
            float assetProgress = CaosFrontendArt.Progress;
            float progress = Mathf.Clamp01((sceneProgress + assetProgress) * 0.5f);

            if (_operation != null && _operation.progress >= ActivationThreshold)
                progress = Mathf.Max(progress, 0.98f);
            if (_activationRequested) progress = 1f;

            return progress;
        }
    }
}
