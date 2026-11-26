using UnityEngine;

namespace Caos.Maps
{
    /// <summary>HUD de la arena: vida, bajas, arma, jefe, sangre y mejoras.</summary>
    public class ArenaHud : MonoBehaviour
    {
        ArenaDirector _director;

        GUIStyle _titleStyle;
        GUIStyle _textStyle;
        GUIStyle _smallStyle;
        GUIStyle _bannerStyle;
        GUIStyle _cardStyle;
        GUIStyle _buttonStyle;
        Texture2D _bloodTexture;

        void Awake()
        {
            _director = FindFirstObjectByType<ArenaDirector>();
        }

        void OnGUI()
        {
            if (_director == null)
            {
                _director = FindFirstObjectByType<ArenaDirector>();
                return;
            }

            EnsureStyles();
            DrawStatus();
            DrawBoss();
            DrawBanner();
            DrawBlood();

            if (_director.UpgradePending) DrawUpgrade();
            if (_director.Finished) DrawEnd();
        }

        void DrawStatus()
        {
            float width = Mathf.Min(380f, Screen.width - 40f);
            var panel = new Rect(20f, 20f, width, 122f);
            DrawRect(panel, new Color(0.04f, 0.05f, 0.08f, 0.72f));

            GUI.Label(new Rect(36f, 28f, width - 32f, 26f), "ARENA CAOS", _titleStyle);

            float barWidth = width - 32f;
            DrawRect(new Rect(36f, 62f, barWidth, 15f), new Color(0.20f, 0.05f, 0.07f, 0.95f));
            DrawRect(new Rect(36f, 62f, barWidth * _director.Health01, 15f),
                new Color(0.86f, 0.13f, 0.16f, 0.98f));

            GUI.Label(new Rect(36f, 84f, barWidth, 22f),
                $"VIDA {_director.HealthCurrent:0}/{_director.HealthMax:0}    " +
                $"BAJAS {_director.Kills}    ARMA {_director.WeaponLabel()} Nv{_director.WeaponTier}",
                _smallStyle);

            string state = _director.BossActive
                ? "JEFE FINAL ACTIVO"
                : _director.HardMode
                    ? "MODO DIFÍCIL"
                    : $"JEFE EN {Countdown()}";
            GUI.Label(new Rect(36f, 106f, barWidth, 22f), state, _smallStyle);
        }

        string Countdown()
        {
            int seconds = Mathf.CeilToInt(_director.BossCountdown);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        void DrawBoss()
        {
            if (!_director.BossActive) return;

            float width = Mathf.Min(520f, Screen.width - 80f);
            float x = (Screen.width - width) * 0.5f;
            GUI.Label(new Rect(x, 18f, width, 24f), "JEFE FINAL — MARTILLO", _bannerStyle);

            DrawRect(new Rect(x, 46f, width, 16f), new Color(0.18f, 0.04f, 0.06f, 0.95f));
            DrawRect(new Rect(x, 46f, width * _director.BossHealth01, 16f),
                new Color(0.95f, 0.20f, 0.18f, 0.98f));
        }

        void DrawBanner()
        {
            string text = _director.Banner;
            if (string.IsNullOrEmpty(text)) return;

            float height = 44f;
            float y = Screen.height * 0.18f;
            DrawRect(new Rect(0f, y, Screen.width, height), new Color(0.05f, 0.03f, 0.05f, 0.62f));
            GUI.Label(new Rect(10f, y, Screen.width - 20f, height), text, _bannerStyle);
        }

        void DrawBlood()
        {
            if (_director.BloodAlpha <= 0f) return;
            if (_bloodTexture == null) _bloodTexture = MakeBloodTexture();

            Color previous = GUI.color;
            GUI.color = new Color(0.78f, 0.02f, 0.02f,
                Mathf.Clamp01(_director.BloodAlpha) * 0.6f);

            Vector2[] spots = _director.BloodSpots;
            for (int i = 0; i < spots.Length; i++)
            {
                float size = 70f + (i % 3) * 46f;
                float x = spots[i].x * Screen.width - size * 0.5f;
                float y = spots[i].y * Screen.height - size * 0.5f;
                GUI.DrawTexture(new Rect(x, y, size, size), _bloodTexture,
                    ScaleMode.StretchToFill, true);
            }

            GUI.color = previous;
        }

        void DrawUpgrade()
        {
            float width = Mathf.Min(780f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.5f - 130f;
            float height = 260f;

            DrawRect(new Rect(x, y, width, height), new Color(0.03f, 0.04f, 0.07f, 0.94f));
            GUI.Label(new Rect(x, y + 14f, width, 34f), "ELIGE UNA MEJORA  (1 / 2 / 3)", _titleStyle);

            const float gap = 12f;
            float cardWidth = (width - 40f - gap * 2f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var rect = new Rect(x + 20f + i * (cardWidth + gap), y + 62f,
                    cardWidth, 160f);
                string label = $"{i + 1}\n\n{_director.UpgradeTitles[i]}\n\n{_director.UpgradeDescriptions[i]}";
                if (GUI.Button(rect, label, _cardStyle)) _director.ChooseUpgrade(i);
            }
        }

        void DrawEnd()
        {
            float width = Mathf.Min(460f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.5f - 130f;
            float height = 260f;

            DrawRect(new Rect(x, y, width, height), new Color(0.03f, 0.03f, 0.06f, 0.95f));
            GUI.Label(new Rect(x, y + 22f, width, 42f),
                _director.Victory ? "¡VICTORIA!" : "HAS MUERTO", _titleStyle);
            GUI.Label(new Rect(x, y + 72f, width, 30f),
                $"BAJAS: {_director.Kills}    ARMA Nv{_director.WeaponTier}", _textStyle);
            GUI.Label(new Rect(x, y + 104f, width, 26f),
                "Pulsa R para reintentar", _smallStyle);

            var retry = new Rect(x + 40f, y + 150f, width - 80f, 42f);
            var menu = new Rect(x + 40f, y + 202f, width - 80f, 42f);
            if (GUI.Button(retry, "REINTENTAR", _buttonStyle)) _director.Restart();
            if (GUI.Button(menu, "VOLVER AL MENÚ", _buttonStyle)) _director.ReturnToMenu();
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter,
            };
            _textStyle.normal.textColor = Color.white;

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
            };
            _smallStyle.normal.textColor = new Color(0.92f, 0.92f, 0.96f);

            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            };
            _bannerStyle.normal.textColor = new Color(1f, 0.72f, 0.30f);

            _cardStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
            };
            _cardStyle.normal.textColor = Color.white;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17, fontStyle = FontStyle.Bold,
            };
        }

        static Texture2D MakeBloodTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                    float dy = (y - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
    }
}
