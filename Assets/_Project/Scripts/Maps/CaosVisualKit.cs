using System.Collections.Generic;
using UnityEngine;

namespace Caos.Maps
{
    /// <summary>
    /// Mallas procedurales compactas para la renovación visual: biseles suaves,
    /// copas orgánicas, troncos curvos, flores, rocas y hierba. Todo se cachea
    /// por parámetros para compartir geometría entre instancias.
    /// </summary>
    public static class CaosVisualKit
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Caja con bisel real. El collider original queda intacto.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int segments = 3)
        {
            radius = Mathf.Clamp(radius, 0.001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.49f);
            segments = Mathf.Clamp(segments, 1, 6);
            string key = $"box|{size.x:0.###}|{size.y:0.###}|{size.z:0.###}|{radius:0.###}|{segments}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            Vector3[] faceNormal =
            {
                Vector3.right, Vector3.left, Vector3.up,
                Vector3.down, Vector3.forward, Vector3.back
            };
            Vector3[] faceU =
            {
                Vector3.back, Vector3.forward, Vector3.right,
                Vector3.right, Vector3.right, Vector3.left
            };
            Vector3[] faceV =
            {
                Vector3.up, Vector3.up, Vector3.back,
                Vector3.forward, Vector3.up, Vector3.up
            };

            Vector3 inner = new Vector3(
                size.x * 0.5f - radius,
                size.y * 0.5f - radius,
                size.z * 0.5f - radius);

            for (int face = 0; face < 6; face++)
            {
                int baseIndex = vertices.Count;
                Vector3 n = faceNormal[face];
                Vector3 u = faceU[face];
                Vector3 v = faceV[face];

                for (int iy = 0; iy <= segments; iy++)
                {
                    float ty = iy / (float)segments;
                    for (int ix = 0; ix <= segments; ix++)
                    {
                        float tx = ix / (float)segments;
                        Vector3 unit = n * 0.5f
                            + u * Mathf.Lerp(-0.5f, 0.5f, tx)
                            + v * Mathf.Lerp(-0.5f, 0.5f, ty);
                        Vector3 point = Vector3.Scale(unit, size);

                        Vector3 core = new Vector3(
                            Mathf.Clamp(point.x, -inner.x, inner.x),
                            Mathf.Clamp(point.y, -inner.y, inner.y),
                            Mathf.Clamp(point.z, -inner.z, inner.z));
                        Vector3 delta = point - core;
                        float length = delta.magnitude;
                        Vector3 outward = length > 0.00001f ? delta / length : n;

                        vertices.Add(core + outward * radius);
                        normals.Add(outward);
                        uvs.Add(new Vector2(tx, ty));
                        colors.Add(Color.white);
                    }
                }

                int stride = segments + 1;
                for (int iy = 0; iy < segments; iy++)
                {
                    for (int ix = 0; ix < segments; ix++)
                    {
                        int a = baseIndex + iy * stride + ix;
                        int b = a + 1;
                        int c = a + stride;
                        int d = c + 1;
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(b);
                        triangles.Add(d);
                        triangles.Add(c);
                    }
                }
            }

            return Finish(key, vertices, normals, uvs, colors, triangles);
        }

        /// <summary>Copa irregular con lóbulos suaves; no es una esfera perfecta.</summary>
        public static Mesh OrganicCrown(float radius, int seed, float squash = 0.85f)
        {
            string key = $"crown|{radius:0.###}|{seed}|{squash:0.###}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            const int segments = 12;
            const int rings = 7;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (int iy = 0; iy <= rings; iy++)
            {
                float phi = Mathf.PI * (0.035f + 0.93f * iy / (float)rings);
                for (int ix = 0; ix <= segments; ix++)
                {
                    float theta = Mathf.PI * 2f * ix / segments;
                    float wobble = 1f
                        + 0.11f * Mathf.Sin(theta * 3f + seed * 0.37f) * Mathf.Sin(phi * 2.6f + seed * 0.11f)
                        + 0.06f * Mathf.Sin(theta * 5f - phi * 4f + seed * 0.73f);
                    float r = radius * wobble;
                    Vector3 p = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta) * r,
                        Mathf.Cos(phi) * r * squash + radius * 0.06f * Mathf.Sin(phi * 3f + seed),
                        Mathf.Sin(phi) * Mathf.Sin(theta) * r);
                    vertices.Add(p);
                    uvs.Add(new Vector2(ix / (float)segments, iy / (float)rings));
                    float tone = Mathf.Lerp(0.78f, 1.12f, iy / (float)rings);
                    colors.Add(new Color(tone, tone, tone, 1f));
                }
            }

            int stride = segments + 1;
            for (int iy = 0; iy < rings; iy++)
            {
                for (int ix = 0; ix < segments; ix++)
                {
                    int a = iy * stride + ix;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(d);
                    triangles.Add(c);
                }
            }

            var normals = new List<Vector3>(new Vector3[vertices.Count]);
            return Finish(key, vertices, normals, uvs, colors, triangles, true);
        }

        /// <summary>Tronco estrecho en la copa, ancho en la base y ligeramente curvado.</summary>
        public static Mesh TaperedTrunk(float height, float baseRadius, float topRadius,
            float bend, int seed)
        {
            string key = $"trunk|{height:0.###}|{baseRadius:0.###}|{topRadius:0.###}|{bend:0.###}|{seed}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            const int sides = 9;
            const int rings = 6;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (int iy = 0; iy <= rings; iy++)
            {
                float t = iy / (float)rings;
                Vector3 center = new Vector3(
                    Mathf.Sin(t * Mathf.PI) * bend + Mathf.Sin(seed + t * 3.7f) * 0.018f,
                    t * height,
                    Mathf.Cos(t * Mathf.PI * 0.7f + seed) * bend * 0.35f);
                float radius = Mathf.Lerp(baseRadius, topRadius, t)
                    * (1f + 0.07f * Mathf.Sin(t * 9f + seed))
                    + (1f - t) * (1f - t) * baseRadius * 0.22f;

                for (int ix = 0; ix <= sides; ix++)
                {
                    float theta = Mathf.PI * 2f * ix / sides;
                    vertices.Add(center + new Vector3(Mathf.Cos(theta) * radius, 0f, Mathf.Sin(theta) * radius));
                    uvs.Add(new Vector2(ix / (float)sides, t));
                    colors.Add(Color.white);
                }
            }

            int stride = sides + 1;
            for (int iy = 0; iy < rings; iy++)
            {
                for (int ix = 0; ix < sides; ix++)
                {
                    int a = iy * stride + ix;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            var normals = new List<Vector3>(new Vector3[vertices.Count]);
            return Finish(key, vertices, normals, uvs, colors, triangles, true);
        }

        /// <summary>Flor completa en una sola malla con colores por vértice.</summary>
        public static Mesh Flower(int seed)
        {
            string key = $"flower|{seed}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            Color stem = new Color(0.24f, 0.70f, 0.32f, 1f);
            AddQuad(vertices, normals, uvs, colors, triangles,
                new Vector3(-0.018f, 0f, 0f), new Vector3(0.018f, 0f, 0f),
                new Vector3(0.018f, 0.20f, 0f), new Vector3(-0.018f, 0.20f, 0f), stem);
            AddQuad(vertices, normals, uvs, colors, triangles,
                new Vector3(0f, 0f, -0.018f), new Vector3(0f, 0f, 0.018f),
                new Vector3(0f, 0.20f, 0.018f), new Vector3(0f, 0.20f, -0.018f), stem);

            Color[] petals =
            {
                new Color(1f, 0.43f, 0.55f, 1f),
                new Color(1f, 0.82f, 0.32f, 1f),
                new Color(0.96f, 0.55f, 0.92f, 1f),
                new Color(0.43f, 0.76f, 1f, 1f)
            };
            Color petal = petals[Mathf.Abs(seed) % petals.Length];
            int count = 5;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count + seed * 0.13f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                Vector3 y = new Vector3(0f, 0.21f, 0f);
                AddQuad(vertices, normals, uvs, colors, triangles,
                    dir * 0.035f + y - side * 0.055f,
                    dir * 0.035f + y + side * 0.055f,
                    dir * 0.22f + y + side * 0.03f,
                    dir * 0.22f + y - side * 0.03f, petal);
            }

            Vector3 c = new Vector3(0f, 0.225f, 0f);
            Color heart = new Color(1f, 0.92f, 0.35f, 1f);
            AddQuad(vertices, normals, uvs, colors, triangles,
                c + new Vector3(-0.055f, 0f, -0.055f), c + new Vector3(0.055f, 0f, -0.055f),
                c + new Vector3(0.055f, 0f, 0.055f), c + new Vector3(-0.055f, 0f, 0.055f), heart);

            return Finish(key, vertices, normals, uvs, colors, triangles);
        }

        /// <summary>Roca achatada y irregular, con muy pocos triángulos.</summary>
        public static Mesh Rock(float radius, int seed)
        {
            string key = $"rock|{radius:0.###}|{seed}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            const int segments = 9;
            const int rings = 4;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (int iy = 0; iy <= rings; iy++)
            {
                float phi = Mathf.PI * (0.09f + 0.82f * iy / (float)rings);
                for (int ix = 0; ix <= segments; ix++)
                {
                    float theta = Mathf.PI * 2f * ix / segments;
                    float wobble = 1f + 0.14f * Mathf.Sin(theta * 4f + seed) * Mathf.Sin(phi * 3f + seed * 0.3f);
                    float r = radius * wobble;
                    vertices.Add(new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta) * r,
                        Mathf.Cos(phi) * r * 0.55f,
                        Mathf.Sin(phi) * Mathf.Sin(theta) * r));
                    uvs.Add(new Vector2(ix / (float)segments, iy / (float)rings));
                    float tone = Mathf.Lerp(0.85f, 1.05f, iy / (float)rings);
                    colors.Add(new Color(tone, tone, tone, 1f));
                }
            }

            int stride = segments + 1;
            for (int iy = 0; iy < rings; iy++)
            {
                for (int ix = 0; ix < segments; ix++)
                {
                    int a = iy * stride + ix;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(d);
                    triangles.Add(c);
                }
            }

            var normals = new List<Vector3>(new Vector3[vertices.Count]);
            return Finish(key, vertices, normals, uvs, colors, triangles, true);
        }

        /// <summary>
        /// Montaña de fondo: perfil irregular con nieve pastel arriba. Se usa
        /// solo como decoración alejada, sin collider y con vértices teñidos.
        /// </summary>
        public static Mesh Mountain(float radius, float height, int seed,
            int sides = 9, int rings = 5)
        {
            radius = Mathf.Max(0.5f, radius);
            height = Mathf.Max(0.5f, height);
            sides = Mathf.Clamp(sides, 6, 16);
            rings = Mathf.Clamp(rings, 2, 8);
            string key = $"mountain|{radius:0.###}|{height:0.###}|{seed}|{sides}|{rings}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var rng = new System.Random(seed);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            var radialNoise = new float[sides];
            for (int s = 0; s < sides; s++)
                radialNoise[s] = 0.84f + (float)rng.NextDouble() * 0.32f;

            Color baseGreen = new Color(0.20f, 0.55f, 0.38f, 1f);
            Color midGreen = new Color(0.38f, 0.72f, 0.52f, 1f);
            Color snow = new Color(0.82f, 0.94f, 1f, 1f);

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float y = height * t;
                float radial = radius * Mathf.Pow(1f - t, 0.72f);
                radial = Mathf.Max(radial, radius * 0.02f);

                for (int s = 0; s <= sides; s++)
                {
                    int index = s % sides;
                    float angle = index / (float)sides * Mathf.PI * 2f;
                    float wobble = radialNoise[index] *
                                   (1f + 0.06f * Mathf.Sin(t * Mathf.PI * 3f + index * 1.7f));
                    vertices.Add(new Vector3(
                        Mathf.Cos(angle) * radial * wobble,
                        y,
                        Mathf.Sin(angle) * radial * wobble));
                    uvs.Add(new Vector2(index / (float)sides, t));

                    Color tint = t < 0.68f
                        ? Color.Lerp(baseGreen, midGreen, t / 0.68f)
                        : Color.Lerp(midGreen, snow, (t - 0.68f) / 0.32f);
                    colors.Add(tint);
                }
            }

            int stride = sides + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = r * stride + s;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    // Orden invertido para que las normales queden hacia fuera.
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, height, 0f));
            uvs.Add(new Vector2(0.5f, 1f));
            colors.Add(snow);
            for (int s = 0; s < sides; s++)
            {
                triangles.Add(topCenter);
                triangles.Add(rings * stride + s + 1);
                triangles.Add(rings * stride + s);
            }

            var normals = new List<Vector3>(new Vector3[vertices.Count]);
            return Finish(key, vertices, normals, uvs, colors, triangles, true);
        }

        /// <summary>Matiz de hierba cruzada con degradado de color por vértice.</summary>
        public static Mesh GrassTuft(int seed)
        {
            string key = $"tuft|{seed}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            Color baseColor = new Color(0.42f, 0.72f, 0.36f, 1f);
            Color tipColor = new Color(0.70f, 1f, 0.58f, 1f);
            float height = 0.30f + (seed % 5) * 0.025f;

            for (int blade = 0; blade < 3; blade++)
            {
                float angle = blade * Mathf.PI / 3f + seed * 0.11f;
                Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.075f;
                Vector3 lean = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * 0.045f;
                int start = vertices.Count;

                vertices.Add(-side);
                vertices.Add(side);
                vertices.Add(side + Vector3.up * height + lean);
                vertices.Add(-side + Vector3.up * height + lean);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                colors.Add(baseColor);
                colors.Add(baseColor);
                colors.Add(tipColor);
                colors.Add(tipColor);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));

                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            return Finish(key, vertices, normals, uvs, colors, triangles);
        }

        /// <summary>Cilindro compacto con bordes redondeados; conserva el collider original.</summary>
        public static Mesh RoundedCylinder(float radius = 0.5f, float height = 1f,
            int segments = 12, int bevelSegments = 2)
        {
            radius = Mathf.Max(0.01f, radius);
            height = Mathf.Max(0.02f, height);
            segments = Mathf.Clamp(segments, 6, 24);
            bevelSegments = Mathf.Clamp(bevelSegments, 1, 4);
            string key = $"cyl|{radius:0.###}|{height:0.###}|{segments}|{bevelSegments}";
            if (Cache.TryGetValue(key, out Mesh cached)) return cached;

            float bevel = Mathf.Min(radius, height * 0.5f) * 0.18f;
            bevel = Mathf.Max(0.004f, bevel);
            float flatRadius = radius - bevel;
            float half = height * 0.5f;

            var profile = new List<Vector4>();
            for (int i = 0; i <= bevelSegments; i++)
            {
                float angle = Mathf.Lerp(-Mathf.PI * 0.5f, 0f, i / (float)bevelSegments);
                profile.Add(new Vector4(
                    flatRadius + bevel * Mathf.Cos(angle),
                    -half + bevel + bevel * Mathf.Sin(angle),
                    Mathf.Cos(angle), Mathf.Sin(angle)));
            }
            profile.Add(new Vector4(radius, half - bevel, 1f, 0f));
            for (int i = 1; i <= bevelSegments; i++)
            {
                float angle = Mathf.Lerp(0f, Mathf.PI * 0.5f, i / (float)bevelSegments);
                profile.Add(new Vector4(
                    flatRadius + bevel * Mathf.Cos(angle),
                    half - bevel + bevel * Mathf.Sin(angle),
                    Mathf.Cos(angle), Mathf.Sin(angle)));
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            vertices.Add(new Vector3(0f, -half, 0f));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));
            colors.Add(Color.white);

            vertices.Add(new Vector3(0f, half, 0f));
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));
            colors.Add(Color.white);

            int ringStart = vertices.Count;
            for (int ring = 0; ring < profile.Count; ring++)
            {
                Vector4 p = profile[ring];
                for (int ix = 0; ix <= segments; ix++)
                {
                    float theta = Mathf.PI * 2f * ix / segments;
                    float c = Mathf.Cos(theta);
                    float s = Mathf.Sin(theta);
                    vertices.Add(new Vector3(c * p.x, p.y, s * p.x));
                    normals.Add(new Vector3(c * p.z, p.w, s * p.z));
                    uvs.Add(new Vector2(ix / (float)segments, ring / (float)(profile.Count - 1)));
                    colors.Add(Color.white);
                }
            }

            int stride = segments + 1;
            for (int ring = 0; ring < profile.Count - 1; ring++)
            {
                for (int ix = 0; ix < segments; ix++)
                {
                    int a = ringStart + ring * stride + ix;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            int bottomRing = ringStart;
            int topRing = ringStart + (profile.Count - 1) * stride;
            for (int ix = 0; ix < segments; ix++)
            {
                triangles.Add(0);
                triangles.Add(bottomRing + ix + 1);
                triangles.Add(bottomRing + ix);

                triangles.Add(1);
                triangles.Add(topRing + ix);
                triangles.Add(topRing + ix + 1);
            }

            return Finish(key, vertices, normals, uvs, colors, triangles);
        }

        /// <summary>Sustituye solo el render por una versión biselada; colisiones intactas.</summary>
        public static Renderer ApplyRoundedVisual(GameObject target, Vector3 size,
            float radius, Material material, int segments = 3)
        {
            if (target == null || material == null) return null;

            var parentRenderer = target.GetComponent<Renderer>();
            if (parentRenderer != null) parentRenderer.enabled = false;
            if (target.GetComponent<BoxCollider>() == null)
            {
                var collider = target.AddComponent<BoxCollider>();
                collider.size = Vector3.one;
            }

            Transform existing = target.transform.Find("Visual_Rounded");
            GameObject visual = existing != null ? existing.gameObject : new GameObject("Visual_Rounded");
            visual.transform.SetParent(target.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            // El padre conserva su escala original (y por tanto su collider); el hijo la cancela.
            visual.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, size.x),
                1f / Mathf.Max(0.0001f, size.y),
                1f / Mathf.Max(0.0001f, size.z));

            var filter = visual.GetComponent<MeshFilter>();
            if (filter == null) filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = RoundedBox(size, radius, segments);

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return renderer;
        }

        /// <summary>Cambia el render de un cilindro sin añadir ni quitar colisiones.</summary>
        public static Renderer ApplyCylinderVisual(GameObject target, Vector3 scale,
            Material material)
        {
            if (target == null || material == null) return null;

            var parentRenderer = target.GetComponent<Renderer>();
            if (parentRenderer != null) parentRenderer.enabled = false;

            Transform existing = target.transform.Find("Visual_Rounded");
            GameObject visual = existing != null ? existing.gameObject : new GameObject("Visual_Rounded");
            visual.transform.SetParent(target.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            // Malla en coordenadas locales del primitivo: radio 0.5 y alto 2.
            // Así la escala del padre (y por tanto el collider) se conserva tal cual.
            visual.transform.localScale = Vector3.one;

            var filter = visual.GetComponent<MeshFilter>();
            if (filter == null) filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = RoundedCylinder(0.5f, 2f, 12, 2);

            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return renderer;
        }

        /// <summary>Pieza decorativa sin colider, con malla propia y material compartido.</summary>
        public static GameObject CreateDecor(string name, Mesh mesh, Material material,
            Vector3 position, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        static void AddQuad(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<Color> colors, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        static Mesh Finish(string key, List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<Color> colors, List<int> triangles, bool recalculateNormals = false)
        {
            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            if (recalculateNormals || normals.Count != vertices.Count || normals[0] == Vector3.zero)
                mesh.RecalculateNormals();
            else
                mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }
    }
}
