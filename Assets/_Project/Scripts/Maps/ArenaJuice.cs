using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Feedback visual de todos los mapas: sacudida de cámara por trauma,
    /// destellos breves, squash &amp; stretch y chispas procedurales agrupadas.
    /// Solo toma eventos discretos; no cambia la simulación ni mueve colisiones.
    /// Todo lo visible durante pausa/hit-stop avanza con tiempo real.
    /// </summary>
    public class ArenaJuice : MonoBehaviour
    {
        static ArenaJuice _instance;

        /// <summary>Solo el bus ya creado; no instancia nada desde el Update de los enemigos.</summary>
        public static ArenaJuice Active => _instance;

        /// <summary>Accesibilidad: sustituye el destello por un tinte estático legible.</summary>
        public static bool ReduceFlashing
        {
            get => PlayerPrefs.GetInt("Caos.ReduceFlashing", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("Caos.ReduceFlashing", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>El botón de pausa escribe esta misma preferencia desde PlayerCamera.</summary>
        static bool MotionReduced => PlayerPrefs.GetInt("Caos.ReduceMotion", 0) == 1;

        /// <summary>Se crea solo cuando algo pide feedback, sin tocar las escenas.</summary>
        public static ArenaJuice Current
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindFirstObjectByType<ArenaJuice>();
                if (_instance == null)
                {
                    var holder = new GameObject("ArenaJuice");
                    _instance = holder.AddComponent<ArenaJuice>();
                }
                return _instance;
            }
        }

        [Header("Chispas")]
        [SerializeField] int poolSize = 48;
        [SerializeField] float particleLifetime = 0.42f;
        [SerializeField] float particleSpeed = 4.6f;

        [Header("Destello y rebote")]
        [SerializeField] float flashTime = 0.09f;
        [SerializeField] float squashTime = 0.22f;

        const float SparkBaseScale = 0.11f;
        const int MaxBrightFlashesPerSecond = 3;

        struct Spark
        {
            public Transform transform;
            public Renderer renderer;
            public Vector3 velocity;
            public Vector3 spin;
            public float life;
        }

        readonly List<Spark> _sparks = new List<Spark>();
        readonly Dictionary<Transform, Vector3> _restScales = new Dictionary<Transform, Vector3>();
        readonly Dictionary<Transform, Vector3> _squashFactors = new Dictionary<Transform, Vector3>();
        readonly Dictionary<Transform, Coroutine> _squashes = new Dictionary<Transform, Coroutine>();
        readonly Dictionary<Renderer, MaterialPropertyBlock> _flashOriginals =
            new Dictionary<Renderer, MaterialPropertyBlock>();
        readonly Dictionary<Transform, Coroutine> _flashRoutines =
            new Dictionary<Transform, Coroutine>();
        readonly float[] _flashTimes = new float[MaxBrightFlashesPerSecond];
        int _flashCount;
        PlayerCamera _follow;
        Material _sparkMaterial;
        MaterialPropertyBlock _block;
        static Shader _litShader;
        static Mesh _sparkMesh;

        // Paleta semántica: amarillo = impacto, coral = daño, cian = recompensa,
        // violeta = remate/elemento, celeste = aterrizaje (sin grises neutros).
        static readonly Color HitColor = new Color32(255, 209, 102, 255);
        static readonly Color HurtColor = new Color32(255, 107, 107, 255);
        static readonly Color PickupColor = new Color32(53, 208, 255, 255);
        static readonly Color DeathColor = new Color32(123, 97, 255, 255);
        static readonly Color LandColor = new Color32(143, 233, 255, 255);

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (_litShader == null)
                _litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (_litShader != null)
            {
                _sparkMaterial = new Material(_litShader);
                if (_sparkMaterial.HasProperty(BaseColorId))
                    _sparkMaterial.SetColor(BaseColorId, Color.white);
                else if (_sparkMaterial.HasProperty(ColorId))
                    _sparkMaterial.SetColor(ColorId, Color.white);
                if (_sparkMaterial.HasProperty(SmoothnessId))
                    _sparkMaterial.SetFloat(SmoothnessId, 0.2f);
            }

            _block = new MaterialPropertyBlock();
            if (_sparkMesh == null) _sparkMesh = CreateSparkMesh();

            for (int i = 0; i < poolSize; i++)
            {
                // Malla diamantada compartida: menos triángulos que una esfera y sin Collider.
                var spark = new GameObject("Chispa");
                spark.transform.SetParent(transform, false);
                spark.transform.localScale = Vector3.one * SparkBaseScale;

                var filter = spark.AddComponent<MeshFilter>();
                filter.sharedMesh = _sparkMesh;
                var renderer = spark.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _sparkMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                spark.SetActive(false);

                _sparks.Add(new Spark
                {
                    transform = spark.transform,
                    renderer = renderer,
                });
            }
        }

        void OnDestroy()
        {
            bool owned = _instance == this;
            if (owned) _instance = null;
            if (_sparkMaterial != null) Destroy(_sparkMaterial);
            if (owned && _sparkMesh != null)
            {
                Destroy(_sparkMesh);
                _sparkMesh = null;
            }
        }

        void Update()
        {
            // Tiempo real: las chispas siguen vivas durante hit-stop y pausa.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            for (int i = 0; i < _sparks.Count; i++)
            {
                Spark spark = _sparks[i];
                if (spark.life <= 0f) continue;

                spark.life -= dt;
                if (spark.life <= 0f)
                {
                    spark.transform.gameObject.SetActive(false);
                    spark.spin = Vector3.zero;
                    _sparks[i] = spark;
                    continue;
                }

                spark.velocity.y -= 10f * dt;
                spark.transform.position += spark.velocity * dt;
                if (spark.spin.sqrMagnitude > 0f)
                    spark.transform.Rotate(spark.spin * dt, Space.Self);

                float k = Mathf.Clamp01(spark.life / particleLifetime);
                float scale = SparkBaseScale * Mathf.SmoothStep(0f, 1f, k);
                spark.transform.localScale = Vector3.one * scale;

                _sparks[i] = spark;
            }
        }

        // ------------------------------------------------------------ eventos

        /// <summary>Golpe normal: chispas, rebote del objetivo y sacudida corta.</summary>
        public void Hit(Vector3 position, float strength = 0.4f, Transform visual = null)
        {
            AddTrauma(0.12f + strength * 0.45f);
            Burst(position, 6 + Mathf.RoundToInt(strength * 8f), HitColor, particleSpeed);
            Punch(visual, 1.24f);
            FlashTinted(visual, flashTime, HitColor);
        }

        /// <summary>
        /// Impacto en enemigos que ya tienen flash propio: solo chispas,
        /// sacudida y rebote, para que dos destellos no peleen entre sí.
        /// </summary>
        public void Impact(Vector3 position, float strength, Transform visual)
        {
            AddTrauma(0.08f + strength * 0.35f);
            Burst(position, 5 + Mathf.RoundToInt(strength * 6f), HitColor, particleSpeed);
            Punch(visual, 1.18f + strength * 0.1f);
        }

        /// <summary>Factor que deben aplicar las animaciones que escriben localScale.</summary>
        public Vector3 SquashFactor(Transform visual)
        {
            return visual != null && _squashFactors.TryGetValue(visual, out Vector3 factor)
                ? factor
                : Vector3.one;
        }

        /// <summary>Remate o explosión: más chispas y más cámara.</summary>
        public void BigHit(Vector3 position, Transform visual = null)
        {
            AddTrauma(0.85f);
            Burst(position, 24, DeathColor, particleSpeed * 1.5f);
            RadialBurst(position, 10, PickupColor, particleSpeed * 0.9f);
            Punch(visual, 1.4f);
            FlashTinted(visual, flashTime * 1.6f, DeathColor);
        }

        public void Pickup(Vector3 position)
        {
            AddTrauma(0.08f);
            Vector3 origin = position + Vector3.up * 0.6f;
            Burst(origin, 8, PickupColor, particleSpeed * 0.6f);
            RadialBurst(origin, 6, PickupColor, particleSpeed * 0.42f);
        }

        public void Land(Vector3 position, float impactSpeed)
        {
            AddTrauma(Mathf.Clamp(impactSpeed * 0.016f, 0.05f, 0.22f));
            Burst(position, 5, LandColor, particleSpeed * 0.45f);
            RadialBurst(position, 4, LandColor, particleSpeed * 0.3f);
        }

        public void PlayerHurt(Vector3 position)
        {
            AddTrauma(0.55f);
            Vector3 origin = position + Vector3.up;
            Burst(origin, 12, HurtColor, particleSpeed);
            RadialBurst(origin, 8, HurtColor, particleSpeed * 0.8f);
        }

        /// <summary>Suma trauma; la cámara lo decay con tiempo real.</summary>
        public void AddTrauma(float amount)
        {
            if (_follow == null)
                _follow = Camera.main != null ? Camera.main.GetComponent<PlayerCamera>() : null;
            if (_follow == null)
                _follow = FindFirstObjectByType<PlayerCamera>();

            if (_follow != null) _follow.AddTrauma(amount);
        }

        // ---------------------------------------------------------- feedback

        /// <summary>Chispas en abanico; la preferencia de movimiento reduce cantidad y velocidad.</summary>
        void Burst(Vector3 position, int count, Color color, float speed)
        {
            bool reduced = MotionReduced;
            if (reduced)
            {
                count = Mathf.Max(2, Mathf.CeilToInt(count * 0.55f));
                speed *= 0.7f;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.15f;
                Vector3 velocity = direction.normalized * speed * Random.Range(0.55f, 1.15f);
                Vector3 spin = reduced
                    ? Vector3.zero
                    : new Vector3(Random.Range(-320f, 320f), Random.Range(-320f, 320f),
                        Random.Range(-320f, 320f));
                EmitSpark(position, velocity, color, spin);
            }
        }

        /// <summary>Anillo de chispas radiales: lee la dirección del golpe sin coste extra.</summary>
        void RadialBurst(Vector3 position, int count, Color color, float speed)
        {
            bool reduced = MotionReduced;
            if (reduced)
            {
                count = Mathf.Max(2, Mathf.CeilToInt(count * 0.5f));
                speed *= 0.6f;
            }

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 velocity = radial * speed * Random.Range(0.8f, 1.2f)
                    + Vector3.up * speed * (reduced ? 0.22f : 0.35f);
                Vector3 spin = reduced ? Vector3.zero : new Vector3(0f, 0f, Random.Range(-260f, 260f));
                EmitSpark(position, velocity, color, spin);
            }
        }

        bool EmitSpark(Vector3 position, Vector3 velocity, Color color, Vector3 spin)
        {
            int index = NextSpark();
            if (index < 0) return false;

            Spark spark = _sparks[index];
            spark.velocity = velocity;
            spark.life = particleLifetime * Random.Range(0.8f, 1f);
            spark.spin = spin;

            Transform t = spark.transform;
            t.position = position + Vector3.up * 0.2f;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one * SparkBaseScale;
            t.gameObject.SetActive(true);

            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            spark.renderer.SetPropertyBlock(_block);
            _sparks[index] = spark;
            return true;
        }

        int NextSpark()
        {
            for (int i = 0; i < _sparks.Count; i++)
                if (_sparks[i].life <= 0f) return i;
            return -1;
        }

        static int BaseColorId => Shader.PropertyToID("_BaseColor");
        static int ColorId => Shader.PropertyToID("_Color");
        static int SmoothnessId => Shader.PropertyToID("_Smoothness");

        /// <summary>Diamante de 8 caras, creado una vez y compartido por todo el pool.</summary>
        static Mesh CreateSparkMesh()
        {
            var mesh = new Mesh { name = "ChispaDiamante" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 1f, 0f), new Vector3(0f, -1f, 0f),
                new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f),
                new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f),
            };
            mesh.triangles = new[]
            {
                0, 4, 2, 0, 3, 4, 0, 5, 3, 0, 2, 5,
                1, 2, 4, 1, 4, 3, 1, 3, 5, 1, 5, 2,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Escala el visual con overshoot y vuelve exactamente a su reposo.</summary>
        public void Punch(Transform visual, float amount = 1.25f)
        {
            if (visual == null) return;

            if (!_restScales.TryGetValue(visual, out Vector3 rest))
            {
                rest = visual.localScale;
                _restScales[visual] = rest;
            }

            if (_squashes.TryGetValue(visual, out Coroutine running) && running != null)
                StopCoroutine(running);

            // Con menos movimiento el rebote sigue siendo legible, pero mucho más corto.
            float presented = MotionReduced ? 1f + Mathf.Max(0f, amount - 1f) * 0.4f : amount;
            _squashes[visual] = StartCoroutine(Squash(visual, rest, presented));
        }

        IEnumerator Squash(Transform visual, Vector3 rest, float amount)
        {
            var squashed = new Vector3(amount, 1f / amount, amount);
            for (float elapsed = 0f; elapsed < squashTime; elapsed += Time.unscaledDeltaTime)
            {
                if (visual == null) break;
                float k = EaseOutBack(Mathf.Clamp01(elapsed / squashTime));
                _squashFactors[visual] = Vector3.LerpUnclamped(squashed, Vector3.one, k);
                yield return null;
            }

            if (visual != null) _squashFactors[visual] = Vector3.one;
            _squashFactors.Remove(visual);
            _squashes.Remove(visual);
        }

        /// <summary>0 → 1 con ~10% de overshoot; sin paquete de tween.</summary>
        static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float m = k - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }

        /// <summary>Blanquea los materiales del objetivo durante unos frames.</summary>
        public void Flash(Transform visual, float duration = 0f)
        {
            FlashTinted(visual, duration, HitColor);
        }

        /// <summary>
        /// Mezcla el color del impacto con el color original del material: con
        /// "menos destellos" queda un tinte estático, no una luz blanca ni un apagón.
        /// </summary>
        void FlashTinted(Transform visual, float duration, Color tint)
        {
            if (visual == null) return;
            if (duration <= 0f) duration = flashTime;

            bool bright = !ReduceFlashing && AllowBrightFlash();
            float strength = bright ? 0.7f : 0.34f;
            if (!bright) duration = Mathf.Max(duration, 0.16f);

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 16) System.Array.Resize(ref renderers, 16);
            if (renderers.Length == 0) return;

            if (_flashRoutines.TryGetValue(visual, out Coroutine running) && running != null)
                StopCoroutine(running);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                if (!_flashOriginals.TryGetValue(r, out MaterialPropertyBlock original))
                {
                    original = new MaterialPropertyBlock();
                    r.GetPropertyBlock(original);
                    _flashOriginals[r] = original;
                }

                Color baseColor = ReadOriginalColor(r, original, BaseColorId);
                Color tinted = Color.Lerp(baseColor, tint, strength);

                // Se copia el bloque original para conservar texturas y propiedades;
                // solo se reescribe el color, así que no se pierde información.
                _block.Clear();
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, tinted);
                _block.SetColor(ColorId, tinted);
                r.SetPropertyBlock(_block);
            }

            _flashRoutines[visual] = StartCoroutine(RestoreFlash(visual, renderers, duration));
        }

        static Color ReadOriginalColor(Renderer renderer, MaterialPropertyBlock block, int id)
        {
            if (block.HasProperty(id)) return block.GetColor(id);
            var material = renderer.sharedMaterial;
            return material != null && material.HasProperty(id) ? material.GetColor(id) : Color.white;
        }

        IEnumerator RestoreFlash(Transform visual, Renderer[] renderers, float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !_flashOriginals.TryGetValue(r, out MaterialPropertyBlock original))
                    continue;
                r.SetPropertyBlock(original);
                _flashOriginals.Remove(r);
            }
            _flashRoutines.Remove(visual);
        }

        /// <summary>Máximo 3 destellos brillantes por segundo (WCAG 2.3.1).</summary>
        bool AllowBrightFlash()
        {
            float now = Time.unscaledTime;
            int fresh = 0;
            for (int i = 0; i < _flashCount; i++)
                if (now - _flashTimes[i] < 1f) _flashTimes[fresh++] = _flashTimes[i];

            _flashCount = fresh;
            if (_flashCount >= _flashTimes.Length) return false;
            _flashTimes[_flashCount++] = now;
            return true;
        }
    }
}
