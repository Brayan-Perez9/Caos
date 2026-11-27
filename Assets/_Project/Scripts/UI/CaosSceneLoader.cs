using UnityEngine;
using UnityEngine.SceneManagement;

namespace Caos.UI
{
    /// <summary>
    /// Overlay de carga real: usa LoadSceneAsync y muestra op.progress.
    /// Sobrevive al cambio de escena y se destruye cuando la carga termina.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class CaosSceneLoader : MonoBehaviour
    {
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1080f;

        static CaosSceneLoader _instance;

        AsyncOperation _operation;
        Texture2D _art;
        string _title;
        string _subtitle;
        string _detail;
        float _progress;
        bool _activating;
        bool _finished;

        GUIStyle _titleStyle;
        GUIStyle _subtitleStyle;
        GUIStyle _detailStyle;
        GUIStyle _percentStyle;
        GUIStyle _creditStyle;

        public static bool IsLoading => _instance != null && !_instance._finished;

        public static void Begin(string sceneName, string title, string subtitle,
            string detail, Texture2D art)
        {
            if (IsLoading) return;

            var holder = new GameObject("CaosSceneLoader");
            _instance = holder.AddComponent<CaosSceneLoader>();
            DontDestroyOnLoad(holder);
            _instance.StartLoad(sceneName, title, subtitle, detail, art);
        }

        void StartLoad(string sceneName, string title, string subtitle,
            string detail, Texture2D art)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "ARENA CAOS" : title;
            _subtitle = subtitle ?? string.Empty;
            _detail = detail ?? string.Empty;
            _art = art;
            _progress = 0f;
            _activating = false;
            _finished = false;

            _operation = SceneManager.LoadSceneAsync(sceneName);
            if (_operation == null)
            {
                Debug.LogError($"[Caos] No se pudo cargar la escena '{sceneName}'.");
                Finish();
                return;
            }

            _operation.allowSceneActivation = false;
        }

        void Update()
        {
            if (_operation == null || _finished) return;

            _progress = Mathf.Clamp01(_operation.progress / 0.9f);

            if (!_activating && _operation.progress >= 0.89f)
            {
                _activating = true;
                _operation.allowSceneActivation = true;
            }

            if (_activating && _operation.isDone) Finish();
        }

        void Finish()
        {
            _finished = true;
            _operation = null;
            if (_instance == this) _instance = null;
            Destroy(gameObject);
        }

        void OnGUI()
        {
            if (_finished) return;

            Event current = Event.current;
            if (current != null && current.type != EventType.Repaint &&
                current.type != EventType.Layout)
            {
                current.Use();
            }

            EnsureStyles();

            float scale = Mathf.Min(Screen.width / ReferenceWidth,
                Screen.height / ReferenceHeight);
            if (scale <= 0f) return;

            var offset = new Vector3(
                (Screen.width - ReferenceWidth * scale) * 0.5f,
                (Screen.height - ReferenceHeight * scale) * 0.5f,
                0f);

            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity,
                new Vector3(scale, scale, 1f));

            try
            {
                if (_art != null)
                {
                    GUI.DrawTexture(new Rect(0f, 0f, ReferenceWidth, ReferenceHeight),
                        _art, ScaleMode.ScaleAndCrop, true);
                }

                DrawRect(new Rect(0f, 0f, ReferenceWidth, ReferenceHeight),
                    new Color(0.02f, 0.03f, 0.08f, 0.34f));
                DrawRect(new Rect(0f, 720f, ReferenceWidth, 360f),
                    new Color(0.02f, 0.03f, 0.07f, 0.78f));

                DrawShadowLabel(new Rect(0f, 330f, ReferenceWidth, 130f),
                    _title, _titleStyle, new Color(0f, 0f, 0f, 0.75f));
                GUI.Label(new Rect(0f, 472f, ReferenceWidth, 58f),
                    _subtitle, _subtitleStyle);
                GUI.Label(new Rect(0f, 548f, ReferenceWidth, 44f),
                    _detail, _detailStyle);

                const float barX = 560f;
                const float barY = 968f;
                const float barWidth = 800f;
                const float barHeight = 18f;

                DrawRect(new Rect(barX, barY, barWidth, barHeight),
                    new Color(1f, 1f, 1f, 0.13f));
                DrawRect(new Rect(barX, barY, barWidth * _progress, barHeight),
                    new Color(0.24f, 0.92f, 0.92f, 0.96f));
                DrawRect(new Rect(barX, barY + barHeight - 3f,
                    barWidth * _progress, 3f), new Color(1f, 0.82f, 0.30f, 0.95f));

                GUI.Label(new Rect(barX, barY - 44f, barWidth, 34f),
                    $"{Mathf.RoundToInt(_progress * 100f)}%", _percentStyle);
                GUI.Label(new Rect(28f, 1028f, 320f, 34f), "omar hpta", _creditStyle);
            }
            finally
            {
                GUI.matrix = previous;
            }
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawShadowLabel(Rect rect, string text, GUIStyle style, Color shadow)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = shadow;
            GUI.Label(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height),
                text, style);
            style.normal.textColor = previous;
            GUI.Label(rect, text, style);
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 104,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(1f, 0.84f, 0.30f, 1f);

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 35,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _subtitleStyle.normal.textColor = new Color(0.95f, 0.98f, 1f, 1f);

            _detailStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                alignment = TextAnchor.MiddleCenter
            };
            _detailStyle.normal.textColor = new Color(0.68f, 0.94f, 1f, 1f);

            _percentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _percentStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);

            _creditStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft
            };
            _creditStyle.normal.textColor = new Color(1f, 1f, 1f, 0.68f);
        }
    }
}
