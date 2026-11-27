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
        const string StylizedShaderName = "Caos/ToonStylized";
        const string LitShaderName = "Universal Render Pipeline/Lit";
        const int VerticesPerDrop = 8;
        const int TrianglesPerDrop = 12;

        [SerializeField] int seed = 20261126;
        [SerializeField] AmbientBiome biome = AmbientBiome.Pradera;
        [SerializeField] Vector2 halfExtents = new Vector2(14f, 14f);
        [SerializeField] float groundY;
        [SerializeField] int treeCount = 14;
        [SerializeField] int grassCount = 140;
        [SerializeField] int flowerCount = 42;
        [SerializeField] int rockCount = 22;
        [SerializeField] int mountainCount = 14;
        [SerializeField] int edgeHillCount = 18;
        [SerializeField] int cloudCount = 7;
        [SerializeField] int dustCount = 18;
        [SerializeField] int fireflyCount = 0;
        [SerializeField] int rainCount = 0;
        [SerializeField] bool rain;
        [SerializeField] Material ambientMaterial;
        [SerializeField] Material foliageMaterial;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BladeColorId = Shader.PropertyToID("_BladeColor");
        static readonly int SoilColorId = Shader.PropertyToID("_SoilColor");

        readonly List<Sway> _sways = new List<Sway>();
        readonly List<Drift> _clouds = new List<Drift>();
        readonly List<Firefly> _fireflies = new List<Firefly>();
        readonly List<Firefly> _dust = new List<Firefly>();
        readonly List<Material> _ownedMaterials = new List<Material>();

        Transform _generated;
        Mesh _grassMesh;
        Mesh _rainMesh;
        System.Random _weatherRng;
        Vector3[] _rainPositions;
        float[] _rainSpeeds;
        Vector3[] _rainVertices;
        bool _generatedOnce;
        float _visualTime;

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
            BuildFlowersAndRocks(Stream(67), Stream(71));
            BuildEdgeHills(Stream(91));
            BuildMountains(Stream(83));
            BuildClouds(Stream(37));
            BuildDust(Stream(97));
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

        /// <summary>
        /// Árboles pegados al borde, lejos del carril central y del spawn.
        /// Así las copas no quedan delante de la cámara al jugar cerca del borde.
        /// </summary>
        Vector3 SampleTreePerimeter(System.Random rng)
        {
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float x = Mathf.Lerp(-halfExtents.x, halfExtents.x,
                    (float)rng.NextDouble());
                float z = Mathf.Lerp(-halfExtents.y, halfExtents.y,
                    (float)rng.NextDouble());

                bool nearEdge = Mathf.Abs(x) > halfExtents.x * 0.86f
                    || Mathf.Abs(z) > halfExtents.y * 0.86f;
                bool centerLane = Mathf.Abs(x) < 4.5f && Mathf.Abs(z) < halfExtents.y * 0.9f;
                bool spawnLane = Mathf.Abs(x) < 3.5f && z < -halfExtents.y + 5f;

                if (nearEdge && !centerLane && !spawnLane)
                    return new Vector3(x, groundY, z);
            }

            return new Vector3(halfExtents.x, groundY, 0f);
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
                    crownA = new Color(0.10f, 0.42f, 0.30f);
                    crownB = new Color(0.28f, 0.74f, 0.46f);
                    bushColor = new Color(0.18f, 0.60f, 0.36f);
                    break;
                case AmbientBiome.Candy:
                    crownA = new Color(0.16f, 0.52f, 0.38f);
                    crownB = new Color(0.40f, 0.82f, 0.55f);
                    bushColor = new Color(0.28f, 0.70f, 0.44f);
                    break;
                default:
                    crownA = new Color(0.08f, 0.36f, 0.26f);
                    crownB = new Color(0.24f, 0.68f, 0.42f);
                    bushColor = new Color(0.14f, 0.54f, 0.32f);
                    break;
            }

            // Copas y arbustos verdes con la misma textura de pasto del suelo.
            Material crownAMaterial = MakeFoliage(crownA);
            Material crownBMaterial = MakeFoliage(crownB);
            Material bushMaterial = MakeFoliage(bushColor);

            for (int i = 0; i < treeCount; i++)
            {
                Vector3 position = SampleTreePerimeter(rng);
                float height = 2.8f + (float)rng.NextDouble() * 1.4f;

                // Tronco ancho en la base y curvado: silueta orgánica, no un cilindro.
                float bend = Mathf.Sin(i * 1.73f) * 0.16f;
                Mesh trunkMesh = CaosVisualKit.TaperedTrunk(height, 0.19f, 0.09f, bend, i);
                var trunk = CreateMeshObject("Tree_Trunk_" + i, trunkMesh, trunkMaterial, position);

                // Raíces aplanadas: Ensanchan el pie del árbol sin añadir collider.
                for (int r = 0; r < 3; r++)
                {
                    float angle = r * Mathf.PI * 2f / 3f + i * 0.41f;
                    Vector3 rootOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.28f;
                    var root = CreateMeshObject("Tree_Root_" + i + "_" + r,
                        CaosVisualKit.OrganicCrown(0.24f, 700 + i * 3 + r, 0.32f),
                        trunkMaterial, position + rootOffset + Vector3.up * 0.06f);
                    root.transform.localScale = new Vector3(1.15f, 0.65f, 1.15f);
                    root.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                }

                var canopy = new GameObject("Tree_Canopy_" + i);
                canopy.transform.SetParent(_generated, false);
                canopy.transform.position = position + Vector3.up * (height + 0.05f);

                var lower = CreateMeshObject("Crown_Lower",
                    CaosVisualKit.OrganicCrown(0.78f, i, 0.72f),
                    i % 2 == 0 ? crownAMaterial : crownBMaterial, canopy.transform.position);
                lower.transform.SetParent(canopy.transform, true);
                lower.transform.localPosition = Vector3.zero;
                lower.transform.localScale = new Vector3(1f, 1f, 1f);

                var upper = CreateMeshObject("Crown_Upper",
                    CaosVisualKit.OrganicCrown(0.56f, i + 29, 0.78f),
                    crownBMaterial, canopy.transform.position);
                upper.transform.SetParent(canopy.transform, true);
                upper.transform.localPosition = new Vector3(0.10f, 0.50f, -0.06f);
                upper.transform.localScale = new Vector3(0.9f, 0.85f, 0.9f);

                var tip = CreateMeshObject("Crown_Tip",
                    CaosVisualKit.OrganicCrown(0.38f, i + 57, 0.82f),
                    i % 2 == 0 ? crownBMaterial : crownAMaterial, canopy.transform.position);
                tip.transform.SetParent(canopy.transform, true);
                tip.transform.localPosition = new Vector3(-0.07f, 0.92f, 0.04f);
                tip.transform.localScale = Vector3.one * 0.82f;

                _sways.Add(new Sway
                {
                    target = canopy.transform,
                    rest = canopy.transform.localRotation,
                    phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                    speed = 0.6f + (float)rng.NextDouble() * 0.7f,
                    amplitude = 2.5f + (float)rng.NextDouble() * 3.5f,
                    aroundX = false
                });

                for (int b = 0; b < 3; b++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))
                        * (0.9f + (float)rng.NextDouble() * 0.55f);
                    var bush = CreateMeshObject("Bush_" + i + "_" + b,
                        CaosVisualKit.OrganicCrown(0.40f + b * 0.07f, i * 3 + b, 0.55f),
                        b == 2 ? crownAMaterial : bushMaterial,
                        position + offset + Vector3.up * 0.20f);
                    bush.transform.localScale = new Vector3(0.95f, 0.8f, 0.95f);
                    _sways.Add(new Sway
                    {
                        target = bush.transform,
                        rest = bush.transform.localRotation,
                        phase = i * 0.9f + b * 2.1f,
                        speed = 1.1f + b * 0.2f,
                        amplitude = 1.4f + b * 0.5f,
                        aroundX = true
                    });
                }
            }
        }

        // -------------------------------------------------------------- hierba

        void BuildGrass(System.Random rng)
        {
            // Material opaco con color por vértice: las hojas se ven con luz y
            // no quedan como un velo transparente sobre el suelo. Tres matices
            // (neutro, cálido, frío) dan profundidad sin texturas ni ruido extra.
            Material[] materials =
            {
                MakeTuft(),
                MakeTuft(new Color(1f, 0.92f, 0.78f, 1f)),
                MakeTuft(new Color(0.82f, 0.95f, 1f, 1f))
            };
            _grassMesh = CreateGrassMesh();

            for (int i = 0; i < grassCount; i++)
            {
                bool flower = i % 7 == 0;
                Vector3 position = SampleOpen(rng, 0.8f);
                var go = new GameObject(flower ? "Flower_Grass_" + i : "Grass_" + i);
                go.transform.SetParent(_generated, false);
                go.transform.position = position;
                float size = flower
                    ? 0.85f + (float)rng.NextDouble() * 0.5f
                    : 0.65f + (float)rng.NextDouble() * 0.7f;
                go.transform.localScale = Vector3.one * size;
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                go.AddComponent<MeshFilter>().sharedMesh = flower
                    ? CaosVisualKit.Flower(i % 4)
                    : _grassMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = materials[i % materials.Length];

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

        // -------------------------------------------------- flores y rocas

        /// <summary>Detalle de borde: flores ligeras y rocas sin colisión.</summary>
        void BuildFlowersAndRocks(System.Random flowerRng, System.Random rockRng)
        {
            var flowerMaterial = MakeTuft();
            for (int i = 0; i < flowerCount; i++)
            {
                Vector3 position = SampleOpen(flowerRng, 1.1f);
                var flower = CreateMeshObject("Flower_Detail_" + i,
                    CaosVisualKit.Flower(i % 4), flowerMaterial, position);
                flower.transform.rotation = Quaternion.Euler(0f, (float)flowerRng.NextDouble() * 360f, 0f);
                flower.transform.localScale = Vector3.one *
                    (0.75f + (float)flowerRng.NextDouble() * 0.55f);
                _sways.Add(new Sway
                {
                    target = flower.transform,
                    rest = flower.transform.localRotation,
                    phase = i * 1.31f,
                    speed = 1.2f + (i % 3) * 0.18f,
                    amplitude = 2.2f + (i % 4) * 0.4f,
                    aroundX = i % 2 == 0
                });
            }

            var rockMaterial = MakeLit(new Color(0.52f, 0.50f, 0.46f));
            for (int i = 0; i < rockCount; i++)
            {
                float radius = 0.22f + (float)rockRng.NextDouble() * 0.22f;
                Vector3 position = SamplePerimeter(rockRng, 1.4f) + Vector3.up * radius * 0.30f;
                var rock = CreateMeshObject("Rock_" + i,
                    CaosVisualKit.Rock(radius, i), rockMaterial, position);
                rock.transform.rotation = Quaternion.Euler(
                    0f, (float)rockRng.NextDouble() * 360f,
                    (float)(rockRng.NextDouble() - 0.5) * 8f);
            }
        }

        // ------------------------------------------------------ lomas de borde

        /// <summary>
        /// Lomas redondeadas justo fuera del borde jugable. Suavizan la salida
        /// de la arena y no tienen collider, así que no estorban a la IA.
        /// </summary>
        void BuildEdgeHills(System.Random rng)
        {
            // OrganicCrown trae un tono gris por vértice: con base blanca salían
            // manchas claras. Base verde para que se lean como lomas de césped.
            var material = MakeHill();
            float radius = Mathf.Max(halfExtents.x, halfExtents.y) + 3.5f;

            for (int i = 0; i < edgeHillCount; i++)
            {
                float angle = (i + (float)(rng.NextDouble() - 0.5) * 0.25f)
                              / edgeHillCount * Mathf.PI * 2f;
                float distance = radius + (float)rng.NextDouble() * 3f;
                var position = new Vector3(
                    Mathf.Cos(angle) * distance,
                    groundY - 0.35f,
                    Mathf.Sin(angle) * distance);

                var hill = CreateMeshObject("EdgeHill_" + i,
                    CaosVisualKit.OrganicCrown(1.4f, i + 701, 0.28f), material, position);
                hill.transform.localScale = new Vector3(
                    1f + (float)rng.NextDouble() * 0.6f,
                    0.8f + (float)rng.NextDouble() * 0.5f,
                    1f + (float)rng.NextDouble() * 0.6f);
            }
        }

        // ------------------------------------------------------- montañas fondo

        /// <summary>
        /// Anillo de montañas fuera del área de juego. No son colisionables y
        /// sirven de horizonte para que la arena no parezca flotar en el vacío.
        /// </summary>
        void BuildMountains(System.Random rng)
        {
            // Suelo lejano sin collider: da apoyo visual a las montañas y evita
            // que la arena parezca una isla flotando sobre el cielo.
            var farGround = CreatePrimitive(PrimitiveType.Plane, "FarGround",
                new Vector3(0f, groundY - 0.75f, 0f),
                MakeLit(new Color(0.34f, 0.64f, 0.42f, 1f)));
            farGround.transform.localScale = new Vector3(26f, 1f, 26f);

            var material = MakeStylized(new Color(0.90f, 0.96f, 0.92f, 1f));
            material.SetColor("_RimColor", new Color(0.42f, 0.72f, 0.56f, 1f));
            material.SetFloat("_RimStrength", 0.05f);
            float inner = Mathf.Max(halfExtents.x, halfExtents.y) + 20f;
            float outer = inner + 26f;

            for (int i = 0; i < mountainCount; i++)
            {
                float jitter = (float)(rng.NextDouble() - 0.5) * 0.35f;
                float angle = (i + jitter) / mountainCount * Mathf.PI * 2f;
                float distance = Mathf.Lerp(inner, outer, (float)rng.NextDouble());
                float radius = 4f + (float)rng.NextDouble() * 5f;
                float height = 7f + (float)rng.NextDouble() * 12f;

                var position = new Vector3(
                    Mathf.Cos(angle) * distance,
                    groundY - 0.6f,
                    Mathf.Sin(angle) * distance);

                var mountain = CreateMeshObject("Mountain_" + i,
                    CaosVisualKit.Mountain(radius, height, i + 311, 9, 5),
                    material, position);
                mountain.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                mountain.transform.localScale = Vector3.one *
                    (0.85f + (float)rng.NextDouble() * 0.45f);
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
            var material = MakeLit(new Color(0.66f, 0.78f, 0.92f));
            float wrap = Mathf.Max(halfExtents.x, halfExtents.y) + 40f;

            for (int i = 0; i < cloudCount; i++)
            {
                var cluster = new GameObject("Cloud_" + i);
                cluster.transform.SetParent(_generated, false);
                cluster.transform.position = new Vector3(
                    Mathf.Lerp(-wrap, wrap, (float)rng.NextDouble()),
                    26f + (float)rng.NextDouble() * 10f,
                    Mathf.Lerp(-halfExtents.y, halfExtents.y, (float)rng.NextDouble()));

                int puffs = 4 + rng.Next(3);
                for (int p = 0; p < puffs; p++)
                {
                    var puff = CreateMeshObject("Puff",
                        CaosVisualKit.OrganicCrown(1f, i * 7 + p, 0.55f),
                        material, cluster.transform.position);
                    puff.transform.SetParent(cluster.transform, true);
                    puff.transform.localPosition = new Vector3(
                        (float)(rng.NextDouble() - 0.5) * 5f,
                        (float)(rng.NextDouble() - 0.5) * 0.8f,
                        (float)(rng.NextDouble() - 0.5) * 2.4f);
                    puff.transform.localScale = new Vector3(
                        1.0f + (float)rng.NextDouble() * 0.9f,
                        0.35f + (float)rng.NextDouble() * 0.25f,
                        0.8f + (float)rng.NextDouble() * 0.6f);
                }

                _clouds.Add(new Drift
                {
                    target = cluster.transform,
                    speed = 0.25f + (float)rng.NextDouble() * 0.55f,
                    wrap = wrap
                });
            }
        }

        // ---------------------------------------------------------------- polvo

        /// <summary>
        /// Motas opacas y diminutas: dan vida al aire sin brillar, parpadear ni
        /// formar velos. Se colocan cerca del borde, nunca en el carril central.
        /// </summary>
        void BuildDust(System.Random rng)
        {
            if (dustCount <= 0) return;
            Material dustMaterial = MakeLit(new Color(0.78f, 0.86f, 0.72f));
            for (int i = 0; i < dustCount; i++)
            {
                Vector3 center = SamplePerimeter(rng, 2.2f)
                    + Vector3.up * (1.2f + (float)rng.NextDouble() * 3.2f);
                float size = 0.045f + (float)rng.NextDouble() * 0.045f;
                var mote = CreatePrimitive(PrimitiveType.Sphere, "Dust_" + i, center, dustMaterial);
                mote.transform.localScale = Vector3.one * size;
                _dust.Add(new Firefly
                {
                    target = mote.transform,
                    center = center,
                    radius = new Vector3(
                        0.45f + (float)rng.NextDouble() * 0.5f,
                        0.25f + (float)rng.NextDouble() * 0.35f,
                        0.45f + (float)rng.NextDouble() * 0.5f),
                    phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                    speed = 0.18f + (float)rng.NextDouble() * 0.28f,
                    size = size
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
                    // Gotas oscuras y semitransparentes: nunca blanco puro,
                    // que con ZWrite Off y muchas gotas superpuestas crea una niebla blanca.
                    colors[b] = new Color(0.30f, 0.52f, 0.62f, 0.08f);
                    colors[b + 1] = new Color(0.30f, 0.52f, 0.62f, 0.08f);
                    colors[b + 2] = new Color(0.30f, 0.52f, 0.62f, 0.20f);
                    colors[b + 3] = new Color(0.30f, 0.52f, 0.62f, 0.20f);

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
            // El ambiente sigue vivo en pausa y hit-stop. El paso se acota para
            // que una suspensión del proceso no produzca un salto brusco.
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _visualTime += delta;
            if (_visualTime <= 0f) return;
            float time = _visualTime;

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

            for (int i = 0; i < _dust.Count; i++)
            {
                Firefly mote = _dust[i];
                float t = time * mote.speed + mote.phase;
                mote.target.position = mote.center + new Vector3(
                    Mathf.Sin(t) * mote.radius.x,
                    Mathf.Sin(t * 1.3f + 0.7f) * mote.radius.y,
                    Mathf.Cos(t * 0.9f) * mote.radius.z);
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

        GameObject CreateMeshObject(string name, Mesh mesh, Material material, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_generated, false);
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

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

        /// <summary>Material opaco que respeta el color por vértice de montañas.</summary>
        Material MakeStylized(Color color)
        {
            var shader = Shader.Find(StylizedShaderName) ?? Shader.Find(LitShaderName)
                         ?? Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty("_ShadowColor"))
                material.SetColor("_ShadowColor", new Color(0.16f, 0.28f, 0.34f, 1f));
            if (material.HasProperty("_RimColor"))
                material.SetColor("_RimColor", new Color(0.45f, 0.82f, 1f, 1f));
            if (material.HasProperty("_RimStrength")) material.SetFloat("_RimStrength", 0.16f);
            if (material.HasProperty("_Emission")) material.SetFloat("_Emission", 0f);
            _ownedMaterials.Add(material);
            return material;
        }

        /// <summary>Hierba y flores: opacas, con vértices teñidos y brillo suave.</summary>
        Material MakeTuft(Color? tint = null)
        {
            var material = MakeStylized(tint ?? Color.white);
            material.SetColor("_ShadowColor", new Color(0.10f, 0.28f, 0.14f, 1f));
            material.SetColor("_RimColor", new Color(0.55f, 0.95f, 0.55f, 1f));
            material.SetFloat("_RimStrength", 0.10f);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_NoiseScale", 2.2f);
            return material;
        }

        /// <summary>Lomas de borde: verde con el tono gris de OrganicCrown.</summary>
        Material MakeHill()
        {
            var material = MakeStylized(new Color(0.24f, 0.62f, 0.34f, 1f));
            material.SetColor("_ShadowColor", new Color(0.10f, 0.26f, 0.14f, 1f));
            material.SetColor("_RimColor", new Color(0.45f, 0.85f, 0.55f, 1f));
            material.SetFloat("_RimStrength", 0.08f);
            material.SetFloat("_Smoothness", 0.10f);
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

        /// <summary>
        /// Copas opacas y con sombras. Antes se clonaba BlueGrass: su patrón de
        /// rocío y su color de hoja claro hacían que las copas parecieran un velo.
        /// </summary>
        Material MakeFoliage(Color color)
        {
            var material = MakeStylized(color);
            material.SetColor("_ShadowColor", color * 0.42f);
            material.SetColor("_RimColor", new Color(0.32f, 0.62f, 0.40f, 1f));
            material.SetFloat("_RimStrength", 0.03f);
            material.SetFloat("_Smoothness", 0.16f);
            material.SetFloat("_Emission", 0f);
            material.SetFloat("_NoiseScale", 1.2f);
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
