using UnityEngine;

namespace Caos.UI
{
    /// <summary>
    /// Estilos y utilidades compartidas por las pantallas del armazón. Cada
    /// pantalla vive en su propia escena; aquí solo está lo que todas dibujan.
    /// </summary>
    public static class CaosShellUI
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        public static readonly Color Gold = new Color32(255, 211, 96, 255);
        public static readonly Color Cyan = new Color32(62, 220, 237, 255);
        public static readonly Color Coral = new Color32(255, 112, 120, 255);
        public static readonly Color Violet = new Color32(155, 118, 255, 255);

        public sealed class Styles
        {
            public GUIStyle Title;
            public GUIStyle Subtitle;
            public GUIStyle Section;
            public GUIStyle Kicker;
            public GUIStyle CardTitle;
            public GUIStyle CardBody;
            public GUIStyle Hint;
            public GUIStyle Field;
            public GUIStyle Button;
            public GUIStyle Panel;
            public GUIStyle Card;
            public GUIStyle Credit;
        }

        static Styles _styles;

        public static Styles GetStyles()
        {
            if (_styles != null) return _styles;

            var ink = new Color32(8, 20, 34, 238);
            var panelTexture = CaosFrontendArt.RoundedTexture(ink, 22f);
            var cardTexture = CaosFrontendArt.RoundedTexture(new Color32(9, 22, 38, 244), 22f);
            var buttonTexture = CaosFrontendArt.RoundedTexture(new Color32(32, 74, 104, 248), 18f);
            var buttonHoverTexture = CaosFrontendArt.RoundedTexture(new Color32(46, 116, 146, 255), 18f);

            var styles = new Styles
            {
                Title = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 92,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                },
                Subtitle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 30,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                },
                Section = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                },
                Kicker = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                },
                CardTitle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 32,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                },
                CardBody = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 19,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true
                },
                Hint = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 19,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                },
                Field = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 21,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                },
                Button = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 21,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    border = new RectOffset(18, 18, 18, 18),
                    normal = new GUIStyleState
                    {
                        background = buttonTexture,
                        textColor = new Color(0.94f, 0.97f, 1f, 1f)
                    },
                    hover = new GUIStyleState
                    {
                        background = buttonHoverTexture,
                        textColor = Color.white
                    },
                    active = new GUIStyleState
                    {
                        background = buttonHoverTexture,
                        textColor = Gold
                    }
                },
                Panel = new GUIStyle(GUI.skin.box)
                {
                    border = new RectOffset(22, 22, 22, 22),
                    normal = new GUIStyleState { background = panelTexture }
                },
                Card = new GUIStyle(GUI.skin.box)
                {
                    border = new RectOffset(22, 22, 22, 22),
                    normal = new GUIStyleState { background = cardTexture }
                },
                Credit = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleLeft
                }
            };

            styles.Title.normal.textColor = Gold;
            styles.Subtitle.normal.textColor = Cyan;
            styles.Section.normal.textColor = new Color(0.88f, 0.92f, 1f, 1f);
            styles.Kicker.normal.textColor = Gold;
            styles.CardTitle.normal.textColor = Color.white;
            styles.CardBody.normal.textColor = new Color(0.86f, 0.91f, 0.98f, 1f);
            styles.Hint.normal.textColor = new Color(0.78f, 0.86f, 0.96f, 0.88f);
            styles.Button.focused.textColor = Gold;
            styles.Credit.normal.textColor = new Color(1f, 1f, 1f, 0.72f);

            _styles = styles;
            return styles;
        }

        public static void ComputeScale(out float scale, out float offsetX, out float offsetY)
        {
            scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            offsetX = (Screen.width - ReferenceWidth * scale) * 0.5f;
            offsetY = (Screen.height - ReferenceHeight * scale) * 0.5f;
        }

        public static Matrix4x4 Begin(float scale, float offsetX, float offsetY)
        {
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f),
                Quaternion.identity, new Vector3(scale, scale, 1f));
            return previous;
        }

        public static void Restore(Matrix4x4 previous) => GUI.matrix = previous;

        public static Vector2 ToReference(Vector2 screenPoint, float scale,
            float offsetX, float offsetY)
        {
            if (scale <= 0f) return Vector2.zero;
            return new Vector2(
                (screenPoint.x - offsetX) / scale,
                (screenPoint.y - offsetY) / scale);
        }

        public static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void DrawCover(Rect rect, Texture2D texture, float alpha)
        {
            if (texture == null) return;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop, true);
            GUI.color = previous;
        }

        public static void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static void DrawShadowLabel(Rect rect, string text, GUIStyle style, Color shadow)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = shadow;
            GUI.Label(new Rect(rect.x + 4f, rect.y + 5f, rect.width, rect.height), text, style);
            style.normal.textColor = previous;
            GUI.Label(rect, text, style);
        }

        public static void DrawBar(Rect rect, float value)
        {
            float amount = Mathf.Clamp01(value);
            DrawRect(rect, new Color(1f, 1f, 1f, 0.14f));
            DrawRect(new Rect(rect.x, rect.y, rect.width * amount, rect.height),
                new Color(0.24f, 0.92f, 0.92f, 0.96f));
            DrawRect(new Rect(rect.x, rect.y + rect.height - 4f,
                rect.width * amount, 4f), Gold);
        }
    }
}
