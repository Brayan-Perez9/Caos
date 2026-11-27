using UnityEngine;
using Caos.Maps;
using Caos.Net;
using Caos.Player;

namespace Caos.Dungeon
{
    /// <summary>HUD IMGUI de la mazmorra: mismo estilo y escala que ArenaHud.</summary>
    public class DungeonHud : MonoBehaviour
    {
        DungeonDirector _director;
        CaosNetPlayers _netPlayers;
        bool _partyLogged;
        GUIStyle _titleStyle;
        GUIStyle _labelStyle;
        GUIStyle _smallStyle;
        GUIStyle _progressStyle;
        GUIStyle _bannerStyle;
        GUIStyle _resultStyle;
        float _uiScale = -1f;

        static Texture2D _roundTexture;
        static GUIStyle _panelStyle;

        public void Configure(DungeonDirector director) => _director = director;

        void Start()
        {
            _netPlayers = FindFirstObjectByType<CaosNetPlayers>();
        }

        void OnGUI()
        {
            if (_director == null || _director.State == null || _director.Bridge == null) return;

            float scale = UiScale();
            EnsureStyles(scale);

            DrawRoomPanel(scale);
            DrawStatusPanel(scale);
            DrawPartyPanel(scale);
            DrawRoute(scale);
            DrawBanner(scale);
            DrawDamageFlash();

            if (_director.Finished) DrawResult(scale);
        }

        void DrawRoomPanel(float scale)
        {
            DungeonRoom room = _director.CurrentRoom;
            if (room == null) return;

            var panel = new Rect(S(20f, scale), S(18f, scale),
                S(520f, scale), S(158f, scale));
            DrawPanel(panel, DungeonPalette.UiPanel);

            float x = panel.x + S(18f, scale);
            float width = panel.width - S(36f, scale);
            float y = panel.y + S(10f, scale);

            GUI.Label(new Rect(x, y, width, S(24f, scale)),
                $"MAZMORRA · SALA {_director.CurrentRoomNumber}/{_director.TotalRooms} · " +
                $"{DungeonWorld.ZoneLabel(_director.CurrentRoomNumber - 1, _director.TotalRooms)} · " +
                $"SEMILLA {_director.Seed}", _smallStyle);
            y += S(26f, scale);

            GUI.Label(new Rect(x, y, width, S(34f, scale)), room.Title, _titleStyle);
            y += S(38f, scale);

            GUI.Label(new Rect(x, y, width, S(40f, scale)), room.Objective, _labelStyle);
            y += S(42f, scale);

            GUI.Label(new Rect(x, y, width, S(24f, scale)), room.ProgressText, _progressStyle);

            float remaining = room.TimeRemaining;
            if (remaining > 0f && room.Definition.duration > 0f)
            {
                var bar = new Rect(x, panel.yMax - S(12f, scale), width, S(6f, scale));
                DrawBar(bar, Mathf.Clamp01(remaining / room.Definition.duration), DungeonPalette.UiCyan);
            }
        }

        void DrawStatusPanel(float scale)
        {
            var panel = new Rect(Screen.width - S(330f, scale), S(18f, scale),
                S(310f, scale), S(116f, scale));
            DrawPanel(panel, DungeonPalette.UiPanel);

            float x = panel.x + S(16f, scale);
            float width = panel.width - S(32f, scale);
            float y = panel.y + S(8f, scale);

            GUI.Label(new Rect(x, y, width, S(22f, scale)),
                $"MONEDAS  {_director.State.Coins}", _titleStyle);
            y += S(28f, scale);

            DungeonRoom room = _director.CurrentRoom;
            string weapon = room != null && room.Definition != null
                ? $"{room.Definition.weapon} · {ArenaElementInfo.Label(_director.Bridge.Element)}"
                : "SANTO Y SEÑA";
            GUI.Label(new Rect(x, y, width, S(22f, scale)), "ARMA  " + weapon, _smallStyle);
            y += S(24f, scale);

            GUI.Label(new Rect(x, y, width, S(34f, scale)),
                "RELIQUIAS  " + _director.State.RelicsText, _smallStyle);
        }

        /// <summary>Vida abajo a la izquierda: jugador local y compañeros al lado.</summary>
        void DrawPartyPanel(float scale)
        {
            ChaosHealth health = _director.Health;
            if (health == null) return;

            int friends = _netPlayers != null ? _netPlayers.RemoteCount : 0;
            float rowHeight = S(46f, scale);
            float panelHeight = S(34f, scale) + rowHeight * (1 + friends);
            var panel = new Rect(S(20f, scale),
                Screen.height - panelHeight - S(18f, scale),
                S(470f, scale), panelHeight);
            DrawPanel(panel, DungeonPalette.UiPanel);

            float x = panel.x + S(14f, scale);
            float width = panel.width - S(28f, scale);
            float y = panel.y + S(8f, scale);

            string localName = _netPlayers != null
                ? _netPlayers.LocalDisplayName
                : CharacterCustomization.Instance != null
                    ? CharacterCustomization.Instance.PlayerName : "Jugador";

            DrawHealthRow(new Rect(x, y, width, rowHeight),
                "TÚ · " + localName, health.Current, Mathf.Max(1f, health.Max),
                DungeonPalette.UiYellow, scale);
            y += rowHeight;

            if (_netPlayers != null)
            {
                for (int i = 0; i < friends; i++)
                {
                    if (_netPlayers.GetRemoteHealth(i, out string name,
                            out float current, out float max))
                    {
                        DrawHealthRow(new Rect(x, y, width, rowHeight),
                            "AMIGO · " + name, current, max, DungeonPalette.UiCyan, scale);
                    }
                    y += rowHeight;
                }
            }

            if (!_partyLogged)
            {
                Debug.Log($"[Caos] HUD vida mazmorra: {localName} " +
                    $"{health.Current:0}/{health.Max:0} · amigos={friends}");
                _partyLogged = true;
            }
        }

        void DrawHealthRow(Rect rect, string label, float current, float max,
            Color color, float scale)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, S(20f, scale)),
                $"{label}   {Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}",
                _smallStyle);
            DrawBar(new Rect(rect.x, rect.y + S(22f, scale), rect.width, S(14f, scale)),
                Mathf.Clamp01(current / Mathf.Max(1f, max)), color);
        }

        void DrawRoute(float scale)
        {
            int count = _director.TotalRooms;
            if (count <= 0) return;

            float cell = S(42f, scale);
            float gap = S(5f, scale);
            float width = count * cell + (count - 1) * gap;
            float x = (Screen.width - width) * 0.5f;
            float y = S(18f, scale);

            for (int i = 0; i < count; i++)
            {
                Color color = i < _director.CurrentRoomNumber - 1
                    ? new Color(0.36f, 1f, 0.54f)
                    : i == _director.CurrentRoomNumber - 1
                        ? DungeonPalette.UiYellow
                        : new Color(0.24f, 0.28f, 0.36f, 0.9f);

                var rect = new Rect(x + i * (cell + gap), y, cell, cell);
                DrawPanel(rect, color);

                string mark = DungeonWorld.ZoneMark(i, count);
                if (DungeonWorld.HasShortcut(i)) mark += "↔";
                GUI.Label(rect, (i + 1).ToString() + mark, _smallStyle);
            }
        }

        void DrawBanner(float scale)
        {
            string text = _director.BannerText;
            if (string.IsNullOrEmpty(text)) return;

            float width = Mathf.Min(S(760f, scale), Screen.width - S(40f, scale));
            var rect = new Rect((Screen.width - width) * 0.5f, S(198f, scale),
                width, S(58f, scale));
            DrawPanel(rect, new Color(0.06f, 0.08f, 0.13f, 0.86f));
            GUI.Label(rect, text, _bannerStyle);
        }

        void DrawDamageFlash()
        {
            if (_director.DamageFlash <= 0f || ArenaJuice.ReduceFlashing) return;

            Color previous = GUI.color;
            GUI.color = new Color(0.85f, 0.05f, 0.08f, _director.DamageFlash * 0.14f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void DrawResult(float scale)
        {
            var shade = new Rect(0f, 0f, Screen.width, Screen.height);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.58f);
            GUI.DrawTexture(shade, Texture2D.whiteTexture);
            GUI.color = previous;

            var panel = new Rect((Screen.width - S(560f, scale)) * 0.5f,
                (Screen.height - S(250f, scale)) * 0.5f,
                S(560f, scale), S(250f, scale));
            DrawPanel(panel, new Color(0.06f, 0.08f, 0.13f, 0.96f));

            float x = panel.x + S(22f, scale);
            float width = panel.width - S(44f, scale);
            float y = panel.y + S(20f, scale);

            Color titleColor = _director.Victory ? DungeonPalette.UiYellow : DungeonPalette.UiCoral;
            Color old = GUI.color;
            GUI.color = titleColor;
            GUI.Label(new Rect(x, y, width, S(48f, scale)),
                _director.Victory ? "¡MAZMORRA COMPLETADA!" : "DERROTA", _resultStyle);
            GUI.color = old;
            y += S(56f, scale);

            GUI.Label(new Rect(x, y, width, S(26f, scale)),
                $"Salas {_director.State.RoomsCleared}/{_director.TotalRooms} · Monedas {_director.State.Coins}",
                _labelStyle);
            y += S(32f, scale);

            GUI.Label(new Rect(x, y, width, S(54f, scale)),
                "Reliquias: " + _director.State.RelicsText, _smallStyle);
            y += S(62f, scale);

            GUI.Label(new Rect(x, y, width, S(26f, scale)),
                "Pulsa R para volver a entrar", _progressStyle);
        }

        static float UiScale() =>
            Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.8f, 1.5f);

        static float S(float pixels, float scale) => pixels * scale;

        static void DrawPanel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, _panelStyle);
            GUI.color = previous;
        }

        static void DrawBar(Rect rect, float fill, Color color)
        {
            DrawPanel(rect, new Color(0.1f, 0.12f, 0.17f, 0.9f));
            var fillRect = new Rect(rect.x + S(2f, 1f), rect.y + S(1f, 1f),
                Mathf.Max(0f, (rect.width - S(4f, 1f)) * fill), rect.height - S(2f, 1f));
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void EnsureStyles(float scale)
        {
            EnsureTextures();
            if (_titleStyle != null && Mathf.Abs(_uiScale - scale) < 0.001f) return;
            _uiScale = scale;

            _titleStyle = Style(GUI.skin.label, 26f, scale, FontStyle.Bold,
                DungeonPalette.UiYellow, TextAnchor.MiddleLeft);
            _labelStyle = Style(GUI.skin.label, 16f, scale, FontStyle.Normal,
                DungeonPalette.UiText, TextAnchor.MiddleLeft);
            _labelStyle.wordWrap = true;
            _smallStyle = Style(GUI.skin.label, 13f, scale, FontStyle.Bold,
                new Color(0.78f, 0.84f, 0.92f), TextAnchor.MiddleLeft);
            _smallStyle.wordWrap = true;
            _progressStyle = Style(GUI.skin.label, 17f, scale, FontStyle.Bold,
                DungeonPalette.UiCyan, TextAnchor.MiddleLeft);
            _bannerStyle = Style(GUI.skin.label, 23f, scale, FontStyle.Bold,
                DungeonPalette.UiYellow, TextAnchor.MiddleCenter);
            _resultStyle = Style(GUI.skin.label, 34f, scale, FontStyle.Bold,
                DungeonPalette.UiYellow, TextAnchor.MiddleCenter);
        }

        static GUIStyle Style(GUIStyle source, float size, float scale, FontStyle fontStyle,
            Color color, TextAnchor alignment)
        {
            var style = new GUIStyle(source)
            {
                fontSize = Mathf.Max(12, Mathf.RoundToInt(size * scale)),
                fontStyle = fontStyle,
                alignment = alignment,
            };
            style.normal.textColor = color;
            return style;
        }

        static void EnsureTextures()
        {
            if (_roundTexture != null) return;

            const int size = 64;
            const float radius = 12f;
            const float feather = 2.5f;

            _roundTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Caos Dungeon UI Rounded",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                    var q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) -
                            new Vector2(size * 0.5f - radius, size * 0.5f - radius);
                    float distance = Mathf.Min(Mathf.Max(q.x, q.y), 0f) +
                                     new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude -
                                     radius;
                    byte alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(0.5f - distance / feather) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            _roundTexture.SetPixels32(pixels);
            _roundTexture.Apply();

            _panelStyle = new GUIStyle
            {
                normal = new GUIStyleState { background = _roundTexture },
                border = new RectOffset(12, 12, 12, 12),
            };
        }
    }
}
