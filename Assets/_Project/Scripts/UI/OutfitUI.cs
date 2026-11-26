using System;
using UnityEngine;
using Caos.Player;

namespace Caos.UI
{
    /// <summary>
    /// Panel compartido de personalización: pestañas de color, gorros, camisas,
    /// pantalones y rostro. Lo usan la pantalla de inicio y el menú in-game (P).
    /// </summary>
    public static class OutfitUI
    {
        public const int TabColor = 0;
        public const int TabHats = 1;
        public const int TabShirts = 2;
        public const int TabPants = 3;
        public const int TabFace = 4;
        public const int TabBody = 5;
        public const int TabCostume = 6;
        public const int TabCount = 7;

        static readonly string[] TabNames =
        {
            "COLOR", "GORROS", "CAMISAS", "PANTALON", "ROSTRO", "CUERPO", "ATUENDO",
        };

        static GUIStyle _tabStyle;
        static GUIStyle _optionStyle;
        static GUIStyle _hintStyle;
        static readonly Color Selected = new Color32(255, 214, 96, 255);

        /// <summary>Dibuja el panel dentro de <paramref name="area"/> y devuelve la pestaña activa.</summary>
        public static int Draw(Rect area, CharacterCustomization character, int tab)
        {
            if (_tabStyle == null)
            {
                _tabStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _optionStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                };
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.UpperCenter,
                    wordWrap = true,
                };
                _hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            }

            // ---------------------------------------------------- pestañas
            const float tabHeight = 30f;
            float tabWidth = area.width / TabCount;

            for (int i = 0; i < TabCount; i++)
            {
                var rect = new Rect(area.x + i * tabWidth, area.y, tabWidth - 4f, tabHeight);
                Color before = GUI.color;
                GUI.color = i == tab ? Selected : Color.white;
                if (GUI.Button(rect, TabNames[i], _tabStyle)) tab = i;
                GUI.color = before;
            }

            Rect content = new Rect(area.x, area.y + tabHeight + 12f, area.width,
                area.height - tabHeight - 12f);

            if (character == null) return tab;

            switch (tab)
            {
                case TabColor:
                    DrawPalette(content, character);
                    break;
                case TabHats:
                    DrawOptions(content, OutfitCatalog.Hats, character.HatIndex, character.SetHat);
                    break;
                case TabShirts:
                    DrawOptions(content, OutfitCatalog.Shirts, character.ShirtIndex, character.SetShirt);
                    break;
                case TabPants:
                    DrawOptions(content, OutfitCatalog.Pants, character.PantsIndex, character.SetPants);
                    break;
                case TabFace:
                    DrawOptions(content, OutfitCatalog.Faces, character.FaceIndex, character.SetFace);
                    break;
                case TabBody:
                    DrawOptions(content, OutfitCatalog.Builds, character.BuildIndex, character.SetBuild);
                    break;
                case TabCostume:
                    DrawOptions(content, OutfitCatalog.Costumes, character.CostumeIndex,
                        character.SetCostume);
                    GUI.Label(new Rect(area.x, area.y + content.height - 34f, area.width, 30f),
                        "El atuendo completo cubre ropa, rostro y sombrero", _hintStyle);
                    break;
            }

            return tab;
        }

        static void DrawPalette(Rect area, CharacterCustomization character)
        {
            const int columns = 5;
            const float gap = 12f;

            Color[] colors = CharacterPalette.Colors;
            float swatch = Mathf.Clamp((area.width - gap * (columns - 1)) / columns, 34f, 64f);
            float rowWidth = columns * swatch + (columns - 1) * gap;
            float startX = area.x + (area.width - rowWidth) * 0.5f;

            for (int i = 0; i < colors.Length; i++)
            {
                int col = i % columns;
                int row = i / columns;
                var rect = new Rect(startX + col * (swatch + gap), area.y + row * (swatch + gap),
                    swatch, swatch);

                if (IsSelected(character.PlayerColor, colors[i]))
                {
                    Color before = GUI.color;
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f),
                        Texture2D.whiteTexture);
                    GUI.color = before;
                }

                Color tint = GUI.color;
                GUI.color = colors[i];
                if (GUI.Button(rect, GUIContent.none)) character.SetColor(colors[i]);
                GUI.color = tint;
            }

            GUI.Label(new Rect(area.x, area.y + swatch * 2 + gap * 2 + 12f, area.width, 44f),
                "El color se aplica al cuerpo y a la cabeza", _hintStyle);
        }

        static void DrawOptions(Rect area, string[] names, int selected, Action<int> setter)
        {
            const int columns = 2;
            const float gap = 8f;
            const float rowHeight = 38f;

            float buttonWidth = (area.width - gap) / columns;

            for (int i = 0; i < names.Length; i++)
            {
                int col = i % columns;
                int row = i / columns;
                var rect = new Rect(area.x + col * (buttonWidth + gap),
                    area.y + row * (rowHeight + gap), buttonWidth, rowHeight);

                Color before = GUI.color;
                GUI.color = i == selected ? Selected : Color.white;
                if (GUI.Button(rect, names[i], _optionStyle)) setter(i);
                GUI.color = before;
            }
        }

        static bool IsSelected(Color current, Color color)
        {
            return Mathf.Abs(current.r - color.r) < 0.02f
                && Mathf.Abs(current.g - color.g) < 0.02f
                && Mathf.Abs(current.b - color.b) < 0.02f;
        }
    }
}
