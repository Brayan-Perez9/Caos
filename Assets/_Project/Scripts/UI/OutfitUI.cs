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
        public const int TabShop = 7;
        public const int TabCount = 8;

        static readonly string[] TabNames =
        {
            "COLOR", "GORROS", "CAMISAS", "PANTALON", "ROSTRO", "CUERPO", "ATUENDO", "TIENDA",
        };

        static GUIStyle _tabStyle;
        static GUIStyle _optionStyle;
        static GUIStyle _hintStyle;
        static GUIStyle _shopNameStyle;
        static GUIStyle _shopBodyStyle;
        static string _shopMessage = "";
        static float _shopMessageUntil;
        static bool _lobbyShopLogged;
        static readonly Color Selected = new Color32(255, 214, 96, 255);

        /// <summary>
        /// Tienda compacta que se muestra directamente en el lobby (fuera de la
        /// escena de personalización). Ofrece tres skins del catálogo sin tocar
        /// sus índices: el clásico y las dos primeras opciones de pago.
        /// </summary>
        public static void DrawLobbyShop(Rect area, CharacterCustomization character)
        {
            EnsureStyles();

            if (!_lobbyShopLogged)
            {
                Debug.Log("[Caos] Tienda exterior del lobby: 3 skins");
                _lobbyShopLogged = true;
            }

            GUI.Label(new Rect(area.x, area.y, area.width, 24f),
                "TIENDA DEL LOBBY", _hintStyle);
            GUI.Label(new Rect(area.x, area.y + 22f, area.width, 24f),
                $"MONEDAS: {CaosProfile.Coins}", _hintStyle);

            CaosSkinDefinition[] skins = CaosSkinCatalog.Skins;
            const int shown = 3;
            float gap = 8f;
            float cardWidth = (area.width - gap * (shown - 1)) / shown;
            float cardHeight = area.height - 52f;

            for (int i = 0; i < shown && i < skins.Length; i++)
            {
                var card = new Rect(area.x + i * (cardWidth + gap),
                    area.y + 48f, cardWidth, cardHeight);
                DrawSkinCard(card, skins[i], character);
            }

            if (Time.realtimeSinceStartup < _shopMessageUntil)
            {
                Color before = GUI.color;
                GUI.color = new Color(1f, 0.86f, 0.5f, 1f);
                GUI.Label(new Rect(area.x, area.y + area.height - 20f, area.width, 20f),
                    _shopMessage, _hintStyle);
                GUI.color = before;
            }
        }

        static void DrawSkinCard(Rect card, CaosSkinDefinition skin,
            CharacterCustomization character)
        {
            Color before = GUI.color;
            GUI.color = new Color(0.05f, 0.07f, 0.13f, 0.88f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = before;
            CaosShellUI.DrawOutline(card, new Color(0.28f, 0.78f, 0.95f, 1f), 2f);

            GUI.Label(new Rect(card.x + 8f, card.y + 6f, card.width - 16f, 18f),
                skin.title, _shopNameStyle);
            GUI.Label(new Rect(card.x + 8f, card.y + 24f, card.width - 16f, 40f),
                $"{skin.description} · {skin.price} monedas", _shopBodyStyle);

            bool owned = CaosProfile.Owns(skin.id);
            bool selected = string.Equals(CaosProfile.EquippedSkinId, skin.id,
                StringComparison.OrdinalIgnoreCase);
            string action = selected ? "EQUIPADO" : owned ? "USAR" : "COMPRAR";

            GUI.enabled = !selected;
            if (GUI.Button(new Rect(card.x + 8f, card.y + card.height - 26f,
                    card.width - 16f, 22f), action, _tabStyle))
            {
                BuyOrEquip(skin, character);
            }
            GUI.enabled = true;
        }

        static void BuyOrEquip(CaosSkinDefinition skin, CharacterCustomization character)
        {
            bool owned = CaosProfile.Owns(skin.id);
            if (!owned && !CaosProfile.TryBuy(skin))
            {
                _shopMessage = $"Te faltan {skin.price - CaosProfile.Coins} monedas";
                _shopMessageUntil = Time.realtimeSinceStartup + 2.2f;
                Debug.Log($"[Caos] Tienda: faltan monedas para {skin.id}");
                return;
            }

            CaosProfile.Equip(skin.id);
            if (character != null) character.ApplySkin(skin);
            _shopMessage = "Skin aplicada";
            _shopMessageUntil = Time.realtimeSinceStartup + 1.6f;
            Debug.Log($"[Caos] Tienda: {skin.id} equipada");
        }

        /// <summary>Acción de una de las tres skins de la tienda del lobby.</summary>
        public static void BuyLobbySkin(int index, CharacterCustomization character)
        {
            CaosSkinDefinition[] skins = CaosSkinCatalog.Skins;
            if (index < 0 || index >= 3 || index >= skins.Length) return;
            BuyOrEquip(skins[index], character);
        }

        /// <summary>Dibuja el panel dentro de <paramref name="area"/> y devuelve la pestaña activa.</summary>
        public static int Draw(Rect area, CharacterCustomization character, int tab)
        {
            EnsureStyles();

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
                case TabShop:
                    DrawShop(content, character);
                    break;
            }

            return tab;
        }

        static void EnsureStyles()
        {
            if (_tabStyle != null) return;

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
            _shopNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            _shopNameStyle.normal.textColor = new Color32(255, 214, 96, 255);
            _shopBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
            _shopBodyStyle.normal.textColor = new Color(1f, 1f, 1f, 0.72f);
        }

        static void DrawShop(Rect area, CharacterCustomization character)
        {
            const float gap = 8f;
            const int columns = 2;

            GUI.Label(new Rect(area.x, area.y, area.width, 24f),
                $"MONEDAS: {CaosProfile.Coins}   ·   Gana monedas limpiando la mazmorra",
                _hintStyle);

            CaosSkinDefinition[] skins = CaosSkinCatalog.Skins;
            int rows = Mathf.CeilToInt(skins.Length / (float)columns);
            float gridTop = area.y + 28f;
            float cellWidth = (area.width - gap) / columns;
            float cellHeight = Mathf.Max(58f, (area.height - 30f - gap * (rows - 1)) / rows);

            for (int i = 0; i < skins.Length; i++)
            {
                CaosSkinDefinition skin = skins[i];
                int col = i % columns;
                int row = i / columns;
                var card = new Rect(area.x + col * (cellWidth + gap),
                    gridTop + row * (cellHeight + gap), cellWidth, cellHeight);

                Color before = GUI.color;
                GUI.color = new Color(0.05f, 0.07f, 0.13f, 0.82f);
                GUI.DrawTexture(card, Texture2D.whiteTexture);
                GUI.color = before;

                GUI.Label(new Rect(card.x + 8f, card.y + 4f, card.width - 16f, 18f),
                    skin.title, _shopNameStyle);
                GUI.Label(new Rect(card.x + 8f, card.y + 21f, card.width - 16f, cellHeight - 46f),
                    $"{skin.description} · {skin.price} monedas", _shopBodyStyle);

                bool owned = CaosProfile.Owns(skin.id);
                bool selected = string.Equals(CaosProfile.EquippedSkinId, skin.id,
                    System.StringComparison.OrdinalIgnoreCase);
                string action = selected ? "EQUIPADO" : owned ? "USAR" : "COMPRAR";

                GUI.enabled = !selected;
                if (GUI.Button(new Rect(card.x + 8f, card.y + cellHeight - 25f,
                        card.width - 16f, 21f), action, _tabStyle))
                {
                    if (!owned && !CaosProfile.TryBuy(skin))
                    {
                        _shopMessage = $"Te faltan {skin.price - CaosProfile.Coins} monedas";
                        _shopMessageUntil = Time.realtimeSinceStartup + 2.2f;
                    }
                    else
                    {
                        CaosProfile.Equip(skin.id);
                        character.ApplySkin(skin);
                        _shopMessage = "Skin aplicada";
                        _shopMessageUntil = Time.realtimeSinceStartup + 1.6f;
                    }
                }
                GUI.enabled = true;
            }

            if (Time.realtimeSinceStartup < _shopMessageUntil)
            {
                Color before = GUI.color;
                GUI.color = new Color(1f, 0.86f, 0.5f, 1f);
                GUI.Label(new Rect(area.x, area.y + area.height - 20f, area.width, 20f),
                    _shopMessage, _hintStyle);
                GUI.color = before;
            }
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
