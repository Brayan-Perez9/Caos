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
    /// </summary>
    public class ArenaJuice : MonoBehaviour
    {
        static ArenaJuice _instance;

        /// <summary>Solo el bus ya creado; no instancia nada desde el Update de los enemigos.</summary>
        public static ArenaJuice Active => _instance;

        /// <summary>Accesibilidad: sustituye el blanco por un tinte suave.</summary>
        public static bool ReduceFlashing
        {
            get => PlayerPrefs.GetInt("Caos.ReduceFlashing", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("Caos.ReduceFlashing", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

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

        struct Spark
        {
            public Transform transform;
            public Renderer renderer;
            public Vector3 velocity;
            public float life;
        }

        readonly List<Spark> _sparks = new List<Spark>();
        readonly Dictionary<Transform, Vector3> _restScales = new Dictionary<Transform, Vector3>();
        readonly Dictionary<Transform, Vector3> _squashFactors = new Dictionary<Transform, Vector3>();
        readonly Dictionary<Transform, Coroutine> _squashes = new Dictionary<Transform, Coroutine>();
        Material _sparkMaterial;
        MaterialPropertyBlock _block;
        static Shader _litShader;

        static readonly Color HitColor = new Color32(255, 222, 120, 255);
        static readonly Color HurtColor = new Color32(255, 86, 90, 255);
        static readonly Color PickupColor = new Color32(120, 240, 255, 255);
        static readonly Color DeathColor = new Color32(255, 124, 200, 255);
        static readonly Color LandColor = new Color32(200, 210, 220, 255);

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
                if (_sparkMaterial.HasProperty("_BaseColor"))
                    _sparkMaterial.SetColor("_BaseColor", Color.white);
                else if (_sparkMaterial.HasProperty("_Color"))
                    _sparkMaterial.SetColor("_Color", Color.white);
            }

            _block = new MaterialPropertyBlock();

            for (int i = 0; i < poolSize; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "Chispa";
                spark.transform.SetParent(transform, false);
                spark.transform.localScale = Vector3.one * 0.09f;
                spark.gameObject.SetActive(false);
                Destroy(spark.GetComponent<Collider>());
                spark.GetComponent<Renderer>().sharedMaterial = _sparkMaterial;

                _sparks.Add(new Spark
                {
                    transform = spark.transform,
                    renderer = spark.GetComponent<Renderer>(),
                });
            }
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_sparkMaterial != null) Destroy(_sparkMaterial);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _sparks.Count; i++)
            {
                Spark spark = _sparks[i];
                if (spark.life <= 0f) continue;

                spark.life -= dt;
                if (spark.life <= 0f)
                {
                    spark.transform.gameObject.SetActive(false);
                    continue;
                }

                spark.velocity.y -= 12f * dt;
                spark.transform.position += spark.velocity * dt;
                float scale = 0.09f * Mathf.Clamp01(spark.life / particleLifetime);
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
            Flash(visual);
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
            Punch(visual, 1.4f);
            Flash(visual, flashTime * 1.6f);
        }

        public void Pickup(Vector3 position)
        {
            AddTrauma(0.08f);
            Burst(position + Vector3.up * 0.6f, 8, PickupColor, particleSpeed * 0.6f);
        }

        public void Land(Vector3 position, float impactSpeed)
        {
            AddTrauma(Mathf.Clamp(impactSpeed * 0.016f, 0.05f, 0.22f));
            Burst(position, 5, LandColor, particleSpeed * 0.45f);
        }

        public void PlayerHurt(Vector3 position)
        {
            AddTrauma(0.55f);
            Burst(position + Vector3.up, 12, HurtColor, particleSpeed);
        }

        /// <summary>Suma trauma; la cámara lo decay con tiempo real.</summary>
        public void AddTrauma(float amount)
        {
            var follow = Camera.main != null
                ? Camera.main.GetComponent<PlayerCamera>()
                : null;
            if (follow == null)
                follow = FindFirstObjectByType<PlayerCamera>();

            if (follow != null) follow.AddTrauma(amount);
        }

        // ---------------------------------------------------------- feedback

        void Burst(Vector3 position, int count, Color color, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Spark spark;
                int index = NextSpark();
                if (index < 0) return;

                spark = _sparks[index];
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.85f + 0.18f;
                spark.velocity = direction.normalized * speed * Random.Range(0.55f, 1.15f);
                spark.life = particleLifetime * Random.Range(0.75f, 1f);
                spark.transform.position = position + Vector3.up * 0.25f;
                spark.transform.gameObject.SetActive(true);

                _block.SetColor(BaseColorId, color);
                _block.SetColor(ColorId, color);
                spark.renderer.SetPropertyBlock(_block);
                _sparks[index] = spark;
            }
        }

        int NextSpark()
        {
            for (int i = 0; i < _sparks.Count; i++)
                if (_sparks[i].life <= 0f) return i;
            return -1;
        }

        static int BaseColorId => Shader.PropertyToID("_BaseColor");
        static int ColorId => Shader.PropertyToID("_Color");

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

            _squashes[visual] = StartCoroutine(Squash(visual, rest, amount));
        }

        IEnumerator Squash(Transform visual, Vector3 rest, float amount)
        {
            var squashed = new Vector3(amount, 1f / amount, amount);
            for (float elapsed = 0f; elapsed < squashTime; elapsed += Time.unscaledDeltaTime)
            {
                float k = EaseOutBack(Mathf.Clamp01(elapsed / squashTime));
                _squashFactors[visual] = Vector3.LerpUnclamped(squashed, Vector3.one, k);
                yield return null;
            }

            _squashFactors[visual] = Vector3.one;
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
            if (visual == null) return;
            if (duration <= 0f) duration = flashTime;

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 16) System.Array.Resize(ref renderers, 16);

            var originals = new MaterialPropertyBlock[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originals[i] = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(originals[i]);

                // Sin flashes grandes: el tinte suave no supera el umbral
                // de fotosensibilidad y sigue haciendo legible el impacto.
                Color flash = ReduceFlashing
                    ? new Color(0.45f, 0.68f, 1f)
                    : Color.white;
                _block.Clear();
                _block.SetColor(BaseColorId, flash);
                _block.SetColor(ColorId, flash);
                renderers[i].SetPropertyBlock(_block);
            }

            StartCoroutine(RestoreFlash(renderers, originals, duration));
        }

        IEnumerator RestoreFlash(Renderer[] renderers, MaterialPropertyBlock[] originals, float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].SetPropertyBlock(originals[i]);
        }
    }
}
