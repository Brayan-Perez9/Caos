using UnityEngine;

namespace Caos.Maps
{
    /// <summary>HUD de la arena: vida, bajas, arma, jefe, sangre y mejoras.</summary>
    public class ArenaHud : MonoBehaviour
    {
        static readonly Color PanelColor = new Color32(11, 27, 43, 209);      // #0B1B2B al 82 %
        static readonly Color TextColor = new Color32(247, 251, 255, 255);    // #F7FBFF
        static readonly Color AccentYellow = new Color32(255, 209, 102, 255); // #FFD166
        static readonly Color HealthyGreen = new Color32(143, 227, 136, 255); // #8FE388
        static readonly Color LowRed = new Color32(255, 61, 61, 255);
        static readonly Color Cyan = new Color32(53, 208, 255, 255);
        static readonly Color Coral = new Color32(255, 107, 107, 255);
        static readonly Color Violet = new Color32(123, 97, 255, 255);

        ArenaDirector _director;

        GUIStyle _titleStyle;
        GUIStyle _textStyle;
        GUIStyle _smallStyle;
        GUIStyle _bannerStyle;
        GUIStyle _cardStyle;
        GUIStyle _buttonLabelStyle;
        Texture2D _bloodTexture;

        static Texture2D _roundTexture;
        static GUIStyle _panelStyle;
        static float _uiScale = -1f;

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

            float scale = UiScale();
            EnsureStyles(scale);
            DrawStatus(scale);
            DrawBoss(scale);
            DrawBanner(scale);
            DrawBlood(scale);

            if (_director.UpgradePending) DrawUpgrade(scale);
            if (_director.Finished) DrawEnd(scale);
        }

        static float UiScale()
        {
            return Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.8f, 1.5f);
        }

        static float S(float pixels, float scale) => pixels * scale;

        void DrawStatus(float scale)
        {
            float margin = S(18f, scale);
            float width = Mathf.Min(S(430f, scale), Screen.width - margin * 2f);
            float height = S(146f, scale);
            var panel = new Rect(margin, margin, width, height);
            DrawPanel(panel, scale, PanelColor);

            float pad = S(16f, scale);
            float x = panel.x + pad;
            float textWidth = panel.width - pad * 2f;
            float y = panel.y + S(6f, scale);

            GUI.Label(new Rect(x, y, textWidth, S(24f, scale)), "ARENA CAOS", _titleStyle);

            y += S(26f, scale);
            bool lowHealth = _director.Health01 <= 0.35f;
            DrawBar(new Rect(x, y, textWidth, S(14f, scale)), scale, _director.Health01,
                lowHealth ? LowRed : HealthyGreen);

            y += S(18f, scale);
            string vitals = $"VIDA {_director.HealthCurrent:0}/{_director.HealthMax:0}    " +
                            $"BAJAS {_director.Kills}    JEFES {_director.BossesDefeated}/{_director.BossesTotal}";
            DrawTintedLabel(new Rect(x, y, textWidth, S(20f, scale)), vitals, _smallStyle,
                lowHealth ? Coral : TextColor);

            y += S(23f, scale);
            string weapon = $"ARMA {_director.WeaponLabel()} · {_director.ElementLabel} " +
                            $"<color=#FFD166>Nv{_director.WeaponTier}</color> — " +
                            (_director.UsesMelee ? "CLIC GOLPEA" : _director.ElementEffect);
            GUI.Label(new Rect(x, y, textWidth, S(20f, scale)), weapon, _smallStyle);

            y += S(24f, scale);
            string state = _director.BossActive
                ? $"JEFE {_director.CurrentBossIndex}/3 ACTIVO — {_director.CurrentBossTitle}"
                : _director.HardMode
                    ? "MODO DIFÍCIL"
                    : $"JEFE EN {Countdown()}";
            Color stateColor = _director.BossActive ? Coral : _director.HardMode ? Violet : Cyan;
            DrawTintedLabel(new Rect(x, y, textWidth, S(20f, scale)), state, _smallStyle, stateColor);
        }

        string Countdown()
        {
            int seconds = Mathf.CeilToInt(_director.BossCountdown);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        void DrawBoss(float scale)
        {
            if (!_director.BossActive) return;

            float margin = S(18f, scale);
            float width = Mathf.Min(S(560f, scale), Screen.width - margin * 2f);
            float x = (Screen.width - width) * 0.5f;
            float y = margin;
            float height = S(92f, scale);
            DrawPanel(new Rect(x, y, width, height), scale, PanelColor);

            float pad = S(14f, scale);
            float textWidth = width - pad * 2f;
            GUI.Label(new Rect(x + pad, y + S(7f, scale), textWidth, S(24f, scale)),
                $"JEFE {_director.CurrentBossIndex}/3 — {_director.CurrentBossTitle}", _bannerStyle);
            DrawTintedLabel(new Rect(x + pad, y + S(33f, scale), textWidth, S(20f, scale)),
                _director.CurrentBossPower, _textStyle, Violet);

            DrawBar(new Rect(x + pad, y + S(61f, scale), textWidth, S(15f, scale)),
                scale, _director.BossHealth01, Coral, true);
        }

        void DrawBanner(float scale)
        {
            string text = _director.Banner;
            if (string.IsNullOrEmpty(text)) return;

            float height = S(44f, scale);
            float y = Screen.height * 0.18f;
            var rect = new Rect(0f, y, Screen.width, height);
            DrawRoundRect(rect, new Color(0.04f, 0.10f, 0.16f, 0.82f), _panelStyle);
            GUI.Label(new Rect(S(10f, scale), y, Screen.width - S(20f, scale), height), text, _bannerStyle);
        }

        void DrawBlood(float scale)
        {
            if (_director.BloodAlpha <= 0f) return;
            if (_bloodTexture == null) _bloodTexture = MakeBloodTexture();

            Color previous = GUI.color;
            GUI.color = new Color(0.78f, 0.02f, 0.02f,
                Mathf.Clamp01(_director.BloodAlpha) * 0.6f);

            Vector2[] spots = _director.BloodSpots;
            for (int i = 0; i < spots.Length; i++)
            {
                float size = (70f + (i % 3) * 46f) * scale;
                float x = spots[i].x * Screen.width - size * 0.5f;
                float y = spots[i].y * Screen.height - size * 0.5f;
                GUI.DrawTexture(new Rect(x, y, size, size), _bloodTexture,
                    ScaleMode.StretchToFill, true);
            }

            GUI.color = previous;
        }

        void DrawUpgrade(float scale)
        {
            float margin = S(18f, scale);
            float width = Mathf.Min(S(780f, scale), Screen.width - margin * 2f);
            float height = S(270f, scale);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.5f - height * 0.5f;
            DrawPanel(new Rect(x, y, width, height), scale, PanelColor);

            GUI.Label(new Rect(x, y + S(12f, scale), width, S(34f, scale)),
                "ELIGE UNA MEJORA  (1 / 2 / 3)", _titleStyle);

            float pad = S(20f, scale);
            float gap = S(12f, scale);
            float cardWidth = (width - pad * 2f - gap * 2f) / 3f;
            Color[] accents = { Cyan, Violet, AccentYellow };
            for (int i = 0; i < 3; i++)
            {
                var rect = new Rect(x + pad + i * (cardWidth + gap), y + S(62f, scale),
                    cardWidth, S(172f, scale));
                DrawButtonBackground(rect, scale, accents[i]);
                string label = $"{i + 1}\n\n{_director.UpgradeTitles[i]}\n\n{_director.UpgradeDescriptions[i]}";
                if (GUI.Button(rect, label, _cardStyle)) _director.ChooseUpgrade(i);
            }
        }

        void DrawEnd(float scale)
        {
            float width = Mathf.Min(S(460f, scale), Screen.width - S(36f, scale));
            float height = S(260f, scale);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.5f - height * 0.5f;
            DrawPanel(new Rect(x, y, width, height), scale, PanelColor);

            GUI.Label(new Rect(x, y + S(20f, scale), width, S(42f, scale)),
                _director.Victory ? "¡VICTORIA!" : "HAS MUERTO", _titleStyle);
            GUI.Label(new Rect(x, y + S(70f, scale), width, S(30f, scale)),
                $"BAJAS: {_director.Kills}    JEFES: {_director.BossesDefeated}/3    " +
                $"ARMA Nv{_director.WeaponTier}", _textStyle);
            GUI.Label(new Rect(x, y + S(102f, scale), width, S(26f, scale)),
                "Pulsa R para reintentar", _smallStyle);

            float pad = S(40f, scale);
            float buttonWidth = width - pad * 2f;
            var retry = new Rect(x + pad, y + S(148f, scale), buttonWidth, S(42f, scale));
            var menu = new Rect(x + pad, y + S(200f, scale), buttonWidth, S(42f, scale));
            DrawButtonBackground(retry, scale, HealthyGreen);
            DrawButtonBackground(menu, scale, Cyan);
            if (GUI.Button(retry, "REINTENTAR", _buttonLabelStyle)) _director.Restart();
            if (GUI.Button(menu, "VOLVER AL MENÚ", _buttonLabelStyle)) _director.ReturnToMenu();
        }

        static void DrawPanel(Rect rect, float scale, Color color)
        {
            DrawRoundRect(new Rect(rect.x + S(2f, scale), rect.y + S(3f, scale), rect.width, rect.height),
                new Color(0f, 0f, 0f, 0.35f), _panelStyle);
            DrawRoundRect(rect, color, _panelStyle);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(new Rect(rect.x + S(10f, scale), rect.y + S(2f, scale),
                Mathf.Max(0f, rect.width - S(20f, scale)), S(2f, scale)), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawButtonBackground(Rect rect, float scale, Color accent, bool selected = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Color baseColor = Color.Lerp(PanelColor, accent, selected ? 0.28f : 0.16f);
            if (hover) baseColor = Color.Lerp(baseColor, Color.white, 0.12f);
            DrawPanel(rect, scale, baseColor);
            DrawRoundRect(new Rect(rect.x + S(8f, scale), rect.yMax - S(4f, scale),
                Mathf.Max(0f, rect.width - S(16f, scale)), S(3f, scale)), accent, _panelStyle);
        }

        static void DrawBar(Rect rect, float scale, float ratio, Color fill, bool shimmer = false)
        {
            DrawRoundRect(rect, new Color(0.05f, 0.07f, 0.11f, 0.95f), _panelStyle);

            float clamped = Mathf.Clamp01(ratio);
            if (clamped <= 0f) return;

            var fillRect = new Rect(rect.x + S(2f, scale), rect.y + S(2f, scale),
                Mathf.Max(S(2f, scale), (rect.width - S(4f, scale)) * clamped),
                rect.height - S(4f, scale));
            DrawRoundRect(fillRect, fill, _panelStyle);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            GUI.DrawTexture(new Rect(fillRect.x + S(3f, scale), fillRect.y + S(2f, scale),
                Mathf.Max(0f, fillRect.width - S(6f, scale)), S(2f, scale)), Texture2D.whiteTexture);

            // Brillo lento y no flashing: se apaga con la opción de accesibilidad.
            if (shimmer && !ArenaJuice.ReduceFlashing)
            {
                float span = Mathf.Min(fillRect.width, Mathf.Max(S(24f, scale), fillRect.width * 0.35f));
                float t = Mathf.Repeat(Time.unscaledTime * 0.3f, 1f);
                float x = Mathf.Lerp(fillRect.x, fillRect.xMax - span, t);
                GUI.color = new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(new Rect(x, fillRect.y + S(2f, scale), span,
                    Mathf.Max(0f, fillRect.height - S(4f, scale))), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        static void DrawRoundRect(Rect rect, Color color, GUIStyle style)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, style);
            GUI.color = previous;
        }

        static void DrawTintedLabel(Rect rect, string text, GUIStyle style, Color tint)
        {
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        static void SetLabelColors(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
        }

        void EnsureStyles(float scale)
        {
            EnsureUiTextures();
            if (_titleStyle != null && Mathf.Abs(_uiScale - scale) < 0.001f) return;
            _uiScale = scale;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(22f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
            };
            _titleStyle.normal.textColor = AccentYellow;

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16f * scale),
                alignment = TextAnchor.MiddleCenter,
            };
            _textStyle.normal.textColor = TextColor;

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(12, Mathf.RoundToInt(13f * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true,
            };
            _smallStyle.normal.textColor = TextColor;

            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(26f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
            };
            _bannerStyle.normal.textColor = AccentYellow;

            _cardStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(16f * scale)),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
            };
            SetLabelColors(_cardStyle, TextColor);

            _buttonLabelStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(14, Mathf.RoundToInt(17f * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            SetLabelColors(_buttonLabelStyle, TextColor);
        }

        static void EnsureUiTextures()
        {
            if (_roundTexture != null) return;

            _roundTexture = MakeRoundedTexture();
            _panelStyle = new GUIStyle
            {
                normal = new GUIStyleState { background = _roundTexture },
                border = new RectOffset(14, 14, 14, 14),
            };
        }

        static Texture2D MakeRoundedTexture()
        {
            const int size = 64;
            const float radius = 13f;
            const float feather = 2.5f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Caos UI Rounded",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                    var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) -
                            new Vector2(size * 0.5f - radius, size * 0.5f - radius);
                    float distance = Mathf.Min(Mathf.Max(q.x, q.y), 0f) +
                                     new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude -
                                     radius;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance / feather) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
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
