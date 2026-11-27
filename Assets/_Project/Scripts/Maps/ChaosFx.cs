using UnityEngine;

namespace Caos.Maps
{
    /// <summary>
    /// Confeti procedural para golpes y muertes. Un único pool por escena,
    /// movido con tiempo real: no usa Rigidbody, no crea ni destruye por golpe
    /// y sigue viéndose durante pausa o hit-stop.
    /// </summary>
    public static class ChaosFx
    {
        const int PoolSize = 96;
        const int MaxPerBurst = 48;

        struct Bit
        {
            public Transform transform;
            public Renderer renderer;
            public Vector3 velocity;
            public Vector3 spin;
            public float life;
            public float maxLife;
            public float baseScale;
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                if (_pool == null) return;
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                if (dt <= 0f) return;

                for (int i = 0; i < _pool.Length; i++)
                {
                    Bit bit = _pool[i];
                    if (bit.life <= 0f) continue;

                    bit.life -= dt;
                    if (bit.life <= 0f)
                    {
                        bit.transform.gameObject.SetActive(false);
                        bit.spin = Vector3.zero;
                        _pool[i] = bit;
                        continue;
                    }

                    bit.velocity.y -= 8.5f * dt;
                    bit.transform.position += bit.velocity * dt;
                    if (bit.spin.sqrMagnitude > 0f)
                        bit.transform.Rotate(bit.spin * dt, Space.Self);

                    float k = Mathf.Clamp01(bit.life / bit.maxLife);
                    float fade = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 3f));
                    bit.transform.localScale = Vector3.one * (bit.baseScale * fade);
                    _pool[i] = bit;
                }
            }
        }

        static Bit[] _pool;
        static Runner _runner;
        static Material _material;
        static Mesh _cubeMesh;
        static MaterialPropertyBlock _block;
        static int _next;

        // RNG propio: el feedback no consume (ni desordena) el azar de gameplay.
        static uint _rng = 0x5A17C0DEu;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>API existente: misma firma, presentación agrupada y accesible.</summary>
        public static void Burst(Vector3 position, Color color, int count = 8)
        {
            if (count <= 0) return;
            EnsurePool();

            bool reduced = PlayerPrefs.GetInt("Caos.ReduceMotion", 0) == 1;
            int emitted = reduced ? Mathf.Max(2, Mathf.CeilToInt(count * 0.5f)) : count;
            emitted = Mathf.Min(emitted, MaxPerBurst);
            float speed = reduced ? 2.3f : 4.2f;

            for (int i = 0; i < emitted; i++)
            {
                int index = Acquire();
                if (index < 0) break;

                float angle = Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Bit bit = _pool[index];
                bit.velocity = radial * Range(speed * 0.45f, speed)
                    + Vector3.up * Range(reduced ? 1f : 2f, reduced ? 2.2f : 5f);
                bit.life = bit.maxLife = Range(0.9f, 1.35f);
                bit.spin = reduced
                    ? Vector3.zero
                    : new Vector3(Range(-420f, 420f), Range(-420f, 420f), Range(-420f, 420f));
                bit.baseScale = Range(0.07f, 0.16f);

                Transform t = bit.transform;
                t.position = position + Vector3.up * 0.18f;
                t.rotation = Quaternion.Euler(Range(0f, 180f), Range(0f, 180f), Range(0f, 180f));
                t.localScale = Vector3.one * bit.baseScale;
                t.gameObject.SetActive(true);

                _block.Clear();
                _block.SetColor(BaseColorId, color);
                _block.SetColor(ColorId, color);
                bit.renderer.SetPropertyBlock(_block);
                _pool[index] = bit;
            }
        }

        static void EnsurePool()
        {
            if (_runner != null) return;

            if (_material == null)
                _material = ArenaMaterial.Create(Color.white, 0f, 0.2f);

            if (_cubeMesh == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(temp);
            }

            if (_block == null) _block = new MaterialPropertyBlock();

            var host = new GameObject("ChaosFx-Confeti");
            _runner = host.AddComponent<Runner>();
            _pool = new Bit[PoolSize];
            _next = 0;

            for (int i = 0; i < _pool.Length; i++)
            {
                var go = new GameObject("Confeti");
                go.transform.SetParent(host.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _cubeMesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);

                _pool[i] = new Bit { transform = go.transform, renderer = renderer };
            }
        }

        static int Acquire()
        {
            for (int n = 0; n < _pool.Length; n++)
            {
                int i = (_next + n) % _pool.Length;
                if (_pool[i].life > 0f) continue;
                _next = (i + 1) % _pool.Length;
                return i;
            }

            // Pool lleno: se reutiliza el más antiguo en vez de instanciar.
            int oldest = _next;
            _next = (_next + 1) % _pool.Length;
            return oldest;
        }

        static uint NextUInt()
        {
            uint x = _rng;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rng = x;
            return x;
        }

        static float Range(float min, float max)
        {
            return min + (max - min) * ((NextUInt() & 0xFFFFFF) / 16777215f);
        }
    }
}
