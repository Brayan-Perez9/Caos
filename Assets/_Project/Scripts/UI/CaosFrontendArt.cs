using System;
using UnityEngine;

namespace Caos.UI
{
    /// <summary>
    /// Carga las ilustraciones de las pantallas iniciales desde Resources y
    /// ofrece un fondo procedural mientras no estén presentes. Las imágenes
    /// deben colocarse en Assets/_Project/Resources/Art/Screens/ con los
    /// nombres descritos en ese directorio.
    /// </summary>
    public static class CaosFrontendArt
    {
        public enum Slot
        {
            Startup,
            Customization,
            Dungeon,
            Arena
        }

        const string Folder = "Art/Screens/";

        static readonly string[] Paths =
        {
            "StartupBackground",
            "CustomizationBackground",
            "DungeonCard",
            "ArenaCard"
        };

        static readonly int[] Seeds = { 1207, 2909, 4703, 8311 };
        static ResourceRequest[] _requests;
        static readonly Texture2D[] _fallbacks = new Texture2D[4];

        public static void BeginPreload()
        {
            if (_requests != null) return;

            _requests = new ResourceRequest[Paths.Length];
            for (int i = 0; i < Paths.Length; i++)
                _requests[i] = Resources.LoadAsync<Texture2D>(Folder + Paths[i]);
        }

        public static bool PreloadFinished
        {
            get
            {
                if (_requests == null) BeginPreload();
                for (int i = 0; i < _requests.Length; i++)
                {
                    if (_requests[i] == null || !_requests[i].isDone) return false;
                }
                return true;
            }
        }

        /// <summary>Progreso real de las peticiones de Resources.LoadAsync.</summary>
        public static float Progress
        {
            get
            {
                if (_requests == null) BeginPreload();

                float total = 0f;
                for (int i = 0; i < _requests.Length; i++)
                {
                    ResourceRequest request = _requests[i];
                    total += request == null ? 0f : request.isDone ? 1f : request.progress;
                }
                return Mathf.Clamp01(total / _requests.Length);
            }
        }

        public static Texture2D Get(Slot slot)
        {
            int index = (int)slot;
            if (_requests != null && index >= 0 && index < _requests.Length)
            {
                ResourceRequest request = _requests[index];
                if (request != null && request.isDone)
                {
                    var loaded = request.asset as Texture2D;
                    if (loaded != null) return loaded;
                }
            }

            return Fallback(index);
        }

        static Texture2D Fallback(int index)
        {
            if (index < 0 || index >= _fallbacks.Length) index = 0;
            if (_fallbacks[index] != null) return _fallbacks[index];

            Palette palette;
            switch (index)
            {
                case 1:
                    palette = new Palette(
                        new Color(0.08f, 0.16f, 0.30f),
                        new Color(0.25f, 0.10f, 0.34f),
                        new Color(0.20f, 0.92f, 0.86f),
                        new Color(0.98f, 0.48f, 0.58f),
                        new Color(1.00f, 0.82f, 0.36f));
                    break;
                case 2:
                    palette = new Palette(
                        new Color(0.06f, 0.07f, 0.16f),
                        new Color(0.20f, 0.09f, 0.28f),
                        new Color(0.62f, 0.42f, 1.00f),
                        new Color(1.00f, 0.68f, 0.25f),
                        new Color(0.28f, 0.88f, 0.92f));
                    break;
                case 3:
                    palette = new Palette(
                        new Color(0.10f, 0.30f, 0.47f),
                        new Color(0.38f, 0.12f, 0.30f),
                        new Color(1.00f, 0.78f, 0.25f),
                        new Color(0.32f, 0.92f, 1.00f),
                        new Color(0.98f, 0.38f, 0.44f));
                    break;
                default:
                    palette = new Palette(
                        new Color(0.05f, 0.20f, 0.32f),
                        new Color(0.36f, 0.10f, 0.33f),
                        new Color(0.25f, 0.94f, 0.88f),
                        new Color(1.00f, 0.42f, 0.42f),
                        new Color(1.00f, 0.82f, 0.30f));
                    break;
            }

            _fallbacks[index] = BuildFallback(palette, Seeds[index]);
            return _fallbacks[index];
        }

        static Texture2D BuildFallback(Palette palette, int seed)
        {
            const int width = 640;
            const int height = 360;
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float vertical = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float wave = Mathf.Sin(x * 0.013f + y * 0.021f + seed) * 0.025f;
                    var color = Color.Lerp(palette.Top, palette.Bottom, vertical);
                    color += new Color(wave, wave * 0.6f, wave, 0f);
                    pixels[y * width + x] = color;
                }
            }

            var random = new System.Random(seed);
            for (int i = 0; i < 7; i++)
            {
                int x = random.Next(-80, width + 80);
                int y = random.Next(-60, height + 60);
                float radius = 54f + random.Next(0, 96);
                Color tint = i % 3 == 0 ? palette.A : i % 3 == 1 ? palette.B : palette.C;
                FillCircle(pixels, width, height, x, y, radius, tint, 0.17f);
            }

            for (int i = 0; i < 4; i++)
            {
                var platform = new Rect(
                    width * (0.04f + i * 0.24f),
                    height * (0.66f + (i % 2) * 0.09f),
                    width * 0.23f,
                    height * 0.13f);
                FillRoundedRect(pixels, width, height, platform, 18f,
                    i % 2 == 0 ? palette.C : palette.A, 0.45f);
            }

            for (int i = 0; i < 80; i++)
            {
                var speck = new Rect(
                    random.Next(0, width),
                    random.Next(0, height - 8),
                    2 + random.Next(1, 9),
                    2 + random.Next(0, 5));
                Color tint = i % 2 == 0 ? palette.A : palette.C;
                FillRect(pixels, width, height, speck, tint, 0.24f);
            }

            for (int y = 0; y < height; y++)
            {
                float ny = (y - height * 0.5f) / (height * 0.5f);
                for (int x = 0; x < width; x++)
                {
                    float nx = (x - width * 0.5f) / (width * 0.5f);
                    float shade = Mathf.Clamp01(1f - 0.24f * (nx * nx + ny * ny));
                    Color color = pixels[y * width + x];
                    pixels[y * width + x] = new Color(color.r * shade, color.g * shade,
                        color.b * shade, 1f);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "CaosFrontendFallback",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>Textura 9-slice para paneles y botones sin assets externos.</summary>
        public static Texture2D RoundedTexture(Color color, float corner = 18f)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            float radius = Mathf.Clamp(corner, 0f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(2f - x, 0f, x - (size - 3f));
                    float dy = Mathf.Max(2f - y, 0f, y - (size - 3f));
                    bool inside = dx * dx + dy * dy <= radius * radius;
                    pixels[y * size + x] = inside ? color : new Color(color.r, color.g, color.b, 0f);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CaosFrontendPanel",
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static void FillCircle(Color32[] pixels, int width, int height,
            int centerX, int centerY, float radius, Color color, float alpha)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radius));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centerX + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radius));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(centerY + radius));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01(radius - distance + 1.5f);
                    BlendPixel(pixels, width, height, x, y, color, alpha * edge);
                }
            }
        }

        static void FillRoundedRect(Color32[] pixels, int width, int height,
            Rect rect, float radius, Color color, float alpha)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(rect.x));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(rect.x + rect.width));
            int minY = Mathf.Max(0, Mathf.FloorToInt(rect.y));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(rect.y + rect.height));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = Mathf.Max(rect.x - x, 0f, x - (rect.x + rect.width - 1f));
                    float dy = Mathf.Max(rect.y - y, 0f, y - (rect.y + rect.height - 1f));
                    if (dx * dx + dy * dy <= radius * radius)
                        BlendPixel(pixels, width, height, x, y, color, alpha);
                }
            }
        }

        static void FillRect(Color32[] pixels, int width, int height,
            Rect rect, Color color, float alpha)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(rect.x));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(rect.x + rect.width));
            int minY = Mathf.Max(0, Mathf.FloorToInt(rect.y));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(rect.y + rect.height));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                    BlendPixel(pixels, width, height, x, y, color, alpha);
            }
        }

        static void BlendPixel(Color32[] pixels, int width, int height,
            int x, int y, Color color, float alpha)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            float amount = Mathf.Clamp01(alpha);
            if (amount <= 0f) return;

            int index = y * width + x;
            Color result = Color.Lerp(pixels[index], color, amount);
            result.a = 1f;
            pixels[index] = result;
        }

        struct Palette
        {
            public readonly Color Top;
            public readonly Color Bottom;
            public readonly Color A;
            public readonly Color B;
            public readonly Color C;

            public Palette(Color top, Color bottom, Color a, Color b, Color c)
            {
                Top = top;
                Bottom = bottom;
                A = a;
                B = b;
                C = c;
            }
        }
    }
}
