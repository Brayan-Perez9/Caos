using System;
using System.Collections.Generic;
using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Paleta de ambiente del mapa.</summary>
    public enum AmbientBiome
    {
        Pradera,
        Neon,
        Candy
    }

    /// <summary>
    /// Ambiente vivo generado en runtime: árboles con copas que se mecen,
    /// arbustos redondos, hierba, nubes que derivan, luciérnagas y lluvia.
    /// Todo son primitivas y mallas creadas por código, sin Animator ni assets
    /// de arte. La colocación usa RNG con semilla own por sistema, así que
    /// el mismo mapa siempre sale igual y la lluvia no altera la decoración.
    /// </summary>
    public class CaosAmbience : MonoBehaviour
    {
        const string AmbientShaderName = "Caos/Ambient";
        const string LitShaderName = "Universal Render Pipeline/Lit";
        const int VerticesPerDrop = 8;
        const int TrianglesPerDrop = 12;

        [SerializeField] int seed = 20261126;
        [SerializeField] AmbientBiome biome = AmbientBiome.Pradera;
        [SerializeField] Vector2 halfExtents = new Vector2(14f, 14f);
        [SerializeField] float groundY;
        [SerializeField] int treeCount = 14;
        [SerializeField] int grassCount = 80;
        [SerializeField] int cloudCount = 7;
        [SerializeField] int fireflyCount = 18;
        [SerializeField] int rainCount = 140;
        [SerializeField] bool rain;
        [SerializeField] Material ambientMaterial;
        [SerializeField] Material foliageMaterial;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BladeColorId = Shader.PropertyToID("_BladeColor");
        static readonly int SoilColorId = Shader.PropertyToID("_SoilColor");

        readonly List<Sway> _sways = new List<Sway>();
        readonly List<Drift> _clouds = new List<Drift>();
        readonly List<Firefly> _fireflies = new List<Firefly>();
        readonly List<Material> _ownedMaterials = new List<Material>();

        Transform _generated;
        Mesh _grassMesh;
        Mesh _rainMesh;
        System.Random _weatherRng;
        Vector3[] _rainPositions;
        float[] _rainSpeeds;
        Vector3[] _rainVertices;
        bool _generatedOnce;

        struct Sway
        {
            public Transform target;
            public Quaternion rest;
            public float phase;
            public float speed;
            public float amplitude;
            public bool aroundX;
        }

        struct Drift
        {
            public Transform target;
            public float speed;
            public float wrap;
        }

        struct Firefly
        {
            public Transform target;
            public Vector3 center;
            public Vector3 radius;
            public float phase;
            public float speed;
            public float size;
        }

        /// <summary>Añade el ambiente a una escena generada por los builders.</summary>
        public static CaosAmbience Ensure(Transform parent, int mapSeed, AmbientBiome mapBiome,
            Vector2 extents, float floorY, bool withRain, Material ambientTemplate,
            Material foliageTemplate)
        {
            var go = new GameObject("CaosAmbience");
            go.transform.SetParent(parent, false);
            var ambience = go.AddComponent<CaosAmbience>();
            ambience.seed = mapSeed;
            ambience.biome = mapBiome;
            ambience.halfExtents = extents;
            ambience.groundY = floorY;
            ambience.rain = withRain;
            ambience.ambientMaterial = ambientTemplate;
            ambience.foliageMaterial = foliageTemplate;
            return ambience;
        }

        /// <summary>Flujo independiente: cambiar la lluvia no mueve los árboles.</summary>
        System.Random Stream(int salt)
        {
            int mixed = unchecked((int)(seed ^ (salt * 0x9E3779B9L)));
            return new System.Random(mixed);
        }

        void Start()
        {
            Generate();
        }

        void Generate()
        {
            if (_generatedOnce) return;
            _generatedOnce = true;

            _generated = new GameObject("Generated").transform;
            _generated.SetParent(transform, false);
            _weatherRng = Stream(7);

            BuildTreesAndBushes(Stream(11));
            BuildGrass(Stream(23));
            BuildClouds(Stream(37));
            BuildFireflies(Stream(53));
            if (rain) BuildRain();
        }

        // ------------------------------------------------------------ ubicación

        /// <summary>
        /// Muestreo con rechazo: solo borde, nunca el pasillo central ni el
        /// spawn. 32 intentos como tope para no bucles infinitos en mapas chicos.
        /// </summary>
        Vector3 SamplePerimeter(System.Random rng, float margin)
        {
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float x = Mathf.Lerp(-halfExtents.x + margin, halfExtents.x - margin,
                    (float)rng.NextDouble());
                float z = Mathf.Lerp(-halfExtents.y + margin, halfExtents.y - margin,
                    (float)rng.NextDouble());

                bool nearEdge = Mathf.Abs(x) > halfExtents.x * 0.70f
                    || Mathf.Abs(z) > halfExtents.y * 0.70f;
                bool centerLane = Mathf.Abs(x) < 3.2f && Mathf.Abs(z) < halfExtents.y * 0.9f;
                bool spawnLane = Mathf.Abs(x) < 2.5f && z < -halfExtents.y + 4f;

                if (nearEdge && !centerLane && !spawnLane)
                    return new Vector3(x, groundY, z);
            }

            return new Vector3(halfExtents.x - margin, groundY, 0f);
        }

        Vector3 SampleOpen(System.Random rng, float margin)
        {
            return new Vector3(
                Mathf.Lerp(-halfExtents.x + margin, halfExtents.x - margin, (float)rng.NextDouble()),
                groundY,
                Mathf.Lerp(-halfExtents.y + margin, halfExtents.y - margin, (float)rng.NextDouble()));
        }

        // ------------------------------------------------------------- árboles

        void BuildTreesAndBushes(System.Random rng)
        {
            Material trunkMaterial = MakeLit(new Color(0.30f, 0.18f, 0.10f));
            Color crownA;
            Color crownB;
            Color bushColor;
            switch (biome)
            {
                case AmbientBiome.Neon:
                    crownA = new Color(0.06f, 0.24f, 0.78f);
                    crownB = new Color(0.16f, 0.52f, 1f);
                    bushColor = new Color(0.10f, 0.38f, 0.90f);
                    break;
                case AmbientBiome.Candy:
                    crownA = new Color(0.18f, 0.42f, 1f);
                    crownB = new Color(0.36f, 0.72f, 1f);
                    bushColor = new Color(0.22f, 0.56f, 1f);
                    break;
                default:
                    crownA = new Color(0.05f, 0.20f, 0.72f);
                    crownB = new Color(0.22f, 0.58f, 1f);
                    bushColor = new Color(0.12f, 0.40f, 0.86f);
                    break;
            }

            // Copas y arbustos azules con la misma textura de pasto del suelo.
            Material crownAMaterial = MakeFoliage(crownA);
            Material crownBMaterial = MakeFoliage(crownB);
            Material bushMaterial = MakeFoliage(bushColor);

            for (int i = 0; i < treeCount; i++)
            {
                Vector3 position = SamplePerimeter(rng, 1.8f);
                float height = 2.4f + (float)rng.NextDouble() * 1.5f;

                var trunk = CreatePrimitive(PrimitiveType.Cylinder, "Tree_Trunk_" + i,
                    position + Vector3.up * height * 0.5f, trunkMaterial);
                trunk.transform.localScale = new Vector3(0.19f, height * 0.5f, 0.19f);

                var canopy = new GameObject("Tree_Canopy_" + i);
                canopy.transform.SetParent(_generated, false);
                canopy.transform.position = position + Vector3.up * (height + 0.1f);

                var lower = CreatePrimitive(PrimitiveType.Sphere, "Crown_Lower",
                    canopy.transform.position, i % 2 == 0 ? crownAMaterial : crownBMaterial);
                lower.transform.SetParent(canopy.transform, true);
                lower.transform.localScale = new Vector3(1.5f, 1.05f, 1.5f);

                var upper = CreatePrimitive(PrimitiveType.Sphere, "Crown_Upper",
                    canopy.transform.position + Vector3.up * 0.65f, crownAMaterial);
                upper.transform.SetParent(canopy.transform, true);
                upper.transform.localScale = new Vector3(1.0f, 0.85f, 1.0f);

                _sways.Add(new Sway
                {
                    target = canopy.transform,
                    rest = canopy.transform.localRotation,
                    phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                    speed = 0.6f + (float)rng.NextDouble() * 0.7f,
                    amplitude = 2.5f + (float)rng.NextDouble() * 3.5f,
                    aroundX = false
                });

                for (int b = 0; b < 2; b++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))
                        * (0.9f + (float)rng.NextDouble() * 0.5f);
                    var bush = CreatePrimitive(PrimitiveType.Sphere, "Bush_" + i + "_" + b,
                        position + offset + Vector3.up * 0.28f, bushMaterial);
                    bush.transform.localScale = new Vector3(0.75f, 0.44f, 0.75f);
                }
            }
        }

        // -------------------------------------------------------------- hierba

        void BuildGrass(System.Random rng)
        {
            var material = MakeAmbient(Color.white);
            _grassMesh = CreateGrassMesh();

            for (int i = 0; i < grassCount; i++)
            {
                Vector3 position = SampleOpen(rng, 0.8f);
                var go = new GameObject("Grass_" + i);
                go.transform.SetParent(_generated, false);
                go.transform.position = position;
                go.transform.localScale = Vector3.one * (0.65f + (float)rng.NextDouble() * 0.7f);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                go.AddComponent<MeshFilter>().sharedMesh = _grassMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;

                _sways.Add(new Sway
                {
                    target = go.transform,
                    rest = go.transform.localRotation,
                    phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                    speed = 1.1f + (float)rng.NextDouble() * 1.2f,
                    amplitude = 4f + (float)rng.NextDouble() * 4f,
                    aroundX = i % 3 == 0
                });
            }
        }

        static Mesh CreateGrassMesh()
        {
            var mesh = new Mesh { name = "CaosGrass" };
            var vertices = new List<Vector3>(12);
            var colors = new List<Color>(12);
            var triangles = new List<int>(18);
            Color rootColor = new Color(0.30f, 0.62f, 0.26f, 1f);
            Color tipColor = new Color(0.62f, 0.92f, 0.40f, 1f);

            for (int blade = 0; blade < 3; blade++)
            {
                float angle = blade * Mathf.PI / 3f;
                Vector3 right = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.17f;
                Vector3 tip = new Vector3(Mathf.Sin(angle) * 0.09f, 0.62f,
                    Mathf.Cos(angle) * 0.09f);
                int start = vertices.Count;

                vertices.Add(-right);
                vertices.Add(right);
                vertices.Add(tip - right * 0.25f);
                vertices.Add(tip + right * 0.25f);
                colors.Add(rootColor);
                colors.Add(rootColor);
                colors.Add(tipColor);
                colors.Add(tipColor);

                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        // --------------------------------------------------------------- nubes

        void BuildClouds(System.Random rng)
        {
            var material = MakeLit(new Color(0.92f, 0.94f, 1f));
            float wrap = halfExtents.x + 18f;

            for (int i = 0; i < cloudCount; i++)
            {
                var cluster = new GameObject("Cloud_" + i);
                cluster.transform.SetParent(_generated, false);
                cluster.transform.position = new Vector3(
                    Mathf.Lerp(-wrap, wrap, (float)rng.NextDouble()),
                    16f + (float)rng.NextDouble() * 9f,
                    Mathf.Lerp(-halfExtents.y, halfExtents.y, (float)rng.NextDouble()));

                int puffs = 4 + rng.Next(3);
                for (int p = 0; p < puffs; p++)
                {
                    var puff = CreatePrimitive(PrimitiveType.Sphere, "Puff",
                        cluster.transform.position, material);
                    puff.transform.SetParent(cluster.transform, true);
                    puff.transform.localPosition = new Vector3(
                        (float)(rng.NextDouble() - 0.5) * 5f,
                        (float)(rng.NextDouble() - 0.5) * 0.8f,
                        (float)(rng.NextDouble() - 0.5) * 2.4f);
                    puff.transform.localScale = new Vector3(
                        1.3f + (float)rng.NextDouble() * 1.2f,
                        0.45f + (float)rng.NextDouble() * 0.35f,
                        0.9f + (float)rng.NextDouble() * 0.8f);
                }

                _clouds.Add(new Drift
                {
                    target = cluster.transform,
                    speed = 0.25f + (float)rng.NextDouble() * 0.55f,
                    wrap = wrap
                });
            }
        }

        // ---------------------------------------------------------- luciérnagas

        void BuildFireflies(System.Random rng)
        {
            Color glow = biome == AmbientBiome.Neon
                ? new Color(0.35f, 0.95f, 1f, 0.9f)
                : new Color(1f, 0.88f, 0.35f, 0.9f);
            var material = MakeAmbient(glow);

            for (int i = 0; i < fireflyCount; i++)
            {
                var go = CreatePrimitive(PrimitiveType.Sphere, "Firefly_" + i,
                    SampleOpen(rng, 2f) + Vector3.up * (0.9f + (float)rng.NextDouble() * 2.5f),
                    material);
                float size = 0.07f + (float)rng.NextDouble() * 0.06f;
                go.transform.localScale = Vector3.one * size;

                _fireflies.Add(new Firefly
                {
                    target = go.transform,
                    center = go.transform.position,
                    radius = new Vector3(
                        0.8f + (float)rng.NextDouble() * 1.6f,
                        0.3f + (float)rng.NextDouble() * 0.7f,
                        0.8f + (float)rng.NextDouble() * 1.6f),
                    phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                    speed = 0.5f + (float)rng.NextDouble() * 0.9f,
                    size = size
                });
            }
        }

        // --------------------------------------------------------------- lluvia

        void BuildRain()
        {
            var material = MakeAmbient(new Color(0.62f, 0.84f, 1f, 1f));
            _rainPositions = new Vector3[rainCount];
            _rainSpeeds = new float[rainCount];
            _rainVertices = new Vector3[rainCount * VerticesPerDrop];

            var colors = new Color[rainCount * VerticesPerDrop];
            var triangles = new int[rainCount * TrianglesPerDrop];

            for (int i = 0; i < rainCount; i++)
            {
                _rainPositions[i] = new Vector3(
                    Mathf.Lerp(-halfExtents.x - 4f, halfExtents.x + 4f,
                        (float)_weatherRng.NextDouble()),
                    groundY + (float)_weatherRng.NextDouble() * 20f,
                    Mathf.Lerp(-halfExtents.y - 4f, halfExtents.y + 4f,
                        (float)_weatherRng.NextDouble()));
                _rainSpeeds[i] = 13f + (float)_weatherRng.NextDouble() * 9f;
                WriteDrop(i, _rainPositions[i]);

                int vertexStart = i * VerticesPerDrop;
                for (int quad = 0; quad < 2; quad++)
                {
                    int b = vertexStart + quad * 4;
                    colors[b] = new Color(1f, 1f, 1f, 0.05f);
                    colors[b + 1] = new Color(1f, 1f, 1f, 0.05f);
                    colors[b + 2] = new Color(1f, 1f, 1f, 0.55f);
                    colors[b + 3] = new Color(1f, 1f, 1f, 0.55f);

                    int t = i * TrianglesPerDrop + quad * 6;
                    triangles[t] = b;
                    triangles[t + 1] = b + 2;
                    triangles[t + 2] = b + 1;
                    triangles[t + 3] = b + 1;
                    triangles[t + 4] = b + 2;
                    triangles[t + 5] = b + 3;
                }
            }

            _rainMesh = new Mesh { name = "CaosRain" };
            _rainMesh.MarkDynamic();
            _rainMesh.vertices = _rainVertices;
            _rainMesh.colors = colors;
            _rainMesh.triangles = triangles;
            _rainMesh.bounds = new Bounds(new Vector3(0f, 10f, 0f),
                new Vector3(halfExtents.x * 4f + 40f, 60f, halfExtents.y * 4f + 40f));

            var go = new GameObject("Rain");
            go.transform.SetParent(_generated, false);
            go.AddComponent<MeshFilter>().sharedMesh = _rainMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void WriteDrop(int index, Vector3 position)
        {
            int vertexStart = index * VerticesPerDrop;
            const float halfWidth = 0.014f;
            const float halfLength = 0.38f;

            // Dos quads cruzados: se ven desde cualquier ángulo de cámara.
            int b = vertexStart;
            _rainVertices[b] = position + new Vector3(-halfWidth, halfLength, 0f);
            _rainVertices[b + 1] = position + new Vector3(halfWidth, halfLength, 0f);
            _rainVertices[b + 2] = position + new Vector3(-halfWidth, -halfLength, 0f);
            _rainVertices[b + 3] = position + new Vector3(halfWidth, -halfLength, 0f);

            b += 4;
            _rainVertices[b] = position + new Vector3(0f, halfLength, -halfWidth);
            _rainVertices[b + 1] = position + new Vector3(0f, halfLength, halfWidth);
            _rainVertices[b + 2] = position + new Vector3(0f, -halfLength, -halfWidth);
            _rainVertices[b + 3] = position + new Vector3(0f, -halfLength, halfWidth);
        }

        // -------------------------------------------------------------- update

        void Update()
        {
            // timeScale = 0 (mejoras y pausa) congela el ambiente también.
            float delta = Time.deltaTime;
            if (delta <= 0f) return;
            float time = Time.time;

            for (int i = 0; i < _sways.Count; i++)
            {
                Sway sway = _sways[i];
                float angle = Mathf.Sin(time * sway.speed + sway.phase) * sway.amplitude;
                sway.target.localRotation = sway.aroundX
                    ? sway.rest * Quaternion.Euler(angle, 0f, 0f)
                    : sway.rest * Quaternion.Euler(0f, 0f, angle);
            }

            for (int i = 0; i < _clouds.Count; i++)
            {
                Drift cloud = _clouds[i];
                Vector3 position = cloud.target.position;
                position.x += cloud.speed * delta;
                if (position.x > cloud.wrap) position.x = -cloud.wrap;
                cloud.target.position = position;
            }

            for (int i = 0; i < _fireflies.Count; i++)
            {
                Firefly fly = _fireflies[i];
                float t = time * fly.speed + fly.phase;
                fly.target.position = fly.center + new Vector3(
                    Mathf.Sin(t) * fly.radius.x,
                    Mathf.Sin(t * 1.7f + 1.3f) * fly.radius.y,
                    Mathf.Cos(t * 0.8f) * fly.radius.z);
                float blink = 0.7f + 0.3f * Mathf.Sin(t * 3.1f);
                fly.target.localScale = Vector3.one * (fly.size * blink);
            }

            if (_rainMesh == null) return;
            for (int i = 0; i < _rainPositions.Length; i++)
            {
                Vector3 drop = _rainPositions[i];
                drop.y -= _rainSpeeds[i] * delta;
                drop.x += 1.3f * delta;

                if (drop.y < groundY - 0.5f || drop.x > halfExtents.x + 6f)
                {
                    drop.y = groundY + 16f + (float)_weatherRng.NextDouble() * 8f;
                    drop.x = -halfExtents.x - 5f + (float)_weatherRng.NextDouble() * 3f;
                    drop.z = Mathf.Lerp(-halfExtents.y - 4f, halfExtents.y + 4f,
                        (float)_weatherRng.NextDouble());
                }

                _rainPositions[i] = drop;
                WriteDrop(i, drop);
            }

            _rainMesh.vertices = _rainVertices;
        }

        // ------------------------------------------------------------- materiales

        GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position,
            Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_generated, false);
            go.transform.position = position;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return go;
        }

        Material MakeLit(Color color)
        {
            var shader = Shader.Find(LitShaderName) ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            _ownedMaterials.Add(material);
            return material;
        }

        Material MakeAmbient(Color color)
        {
            // Clonar el material referenciado por la escena evita que el
            // stripping quite el shader en el build (Shader.Find no lo garantiza).
            Material material;
            if (ambientMaterial != null) material = new Material(ambientMaterial);
            else
            {
                var shader = Shader.Find(AmbientShaderName) ?? Shader.Find(LitShaderName);
                material = new Material(shader);
            }

            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            _ownedMaterials.Add(material);
            return material;
        }

        /// <summary>Copas azules con la textura de pasto del suelo.</summary>
        Material MakeFoliage(Color color)
        {
            Material material;
            if (foliageMaterial != null) material = new Material(foliageMaterial);
            else
            {
                var shader = Shader.Find("Caos/Grass") ?? Shader.Find(LitShaderName);
                material = new Material(shader);
            }

            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(BladeColorId))
                material.SetColor(BladeColorId, Color.Lerp(color, Color.white, 0.32f));
            if (material.HasProperty(SoilColorId))
                material.SetColor(SoilColorId, color * 0.35f);
            _ownedMaterials.Add(material);
            return material;
        }

        void OnDestroy()
        {
            if (_grassMesh != null) Destroy(_grassMesh);
            if (_rainMesh != null) Destroy(_rainMesh);
            for (int i = 0; i < _ownedMaterials.Count; i++)
                if (_ownedMaterials[i] != null) Destroy(_ownedMaterials[i]);
        }
    }
}
