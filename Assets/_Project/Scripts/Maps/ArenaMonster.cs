using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Monstruo de la arena: cuerpo, cabeza, brazos y piernas procedurales.
    /// Camina con CharacterController, así que respeta paredes, cajas, columnas
    /// y plataformas; usa la escalera para cambiar de piso. Sangra al recibir
    /// disparos y sufre los efectos de las armas elementales.
    /// </summary>
    public class ArenaMonster : MonoBehaviour
    {
        static readonly Color BloodColor = new Color(0.62f, 0.02f, 0.03f);
        static readonly Color BodyColor = new Color(0.48f, 0.08f, 0.12f);
        static readonly Color HeadColor = new Color(0.68f, 0.14f, 0.18f);
        static readonly Color ArmColor = new Color(0.40f, 0.06f, 0.10f);
        static readonly Color LegColor = new Color(0.28f, 0.05f, 0.08f);
        static readonly Color EyeColor = new Color(1f, 0.86f, 0.25f);

        const float SpineY = 0.22f;

        [SerializeField] float moveSpeed = 4.8f;
        [SerializeField] float hitDamage = 14f;
        [SerializeField] float attackCooldown = 0.85f;
        [SerializeField] float attackRange = 1.6f;
        [SerializeField] float headHeight = 0.95f;

        IArenaCombatDirector _director;
        ChaosHealth _health;
        PlayerController _player;
        CharacterController _controller;

        Transform _head;
        Transform _torso;
        Transform _spine;
        float _headLocalY;
        int _shape;
        Color _accent = new Color(0.84f, 0.16f, 0.20f);
        Transform[] _legs;
        Transform[] _arms;
        Transform[] _feet;
        Transform[] _hands;
        Transform[] _eyes;
        Transform[] _brows;
        Transform _mouth;
        Transform _tail;
        Renderer[] _renderers;
        Color[] _baseColors;

        Vector3 _baseScale;
        Vector3 _knockback;
        float _nextAttack;
        float _lunge;
        float _verticalSpeed;
        float _visualTime;
        float _hurtTimer;
        float _blinkOffset;
        float _burnTimer;
        float _burnDps;
        float _poisonTimer;
        float _poisonDps;
        float _slowTimer;
        float _slowFactor = 1f;
        bool _built;
        bool _flashing;
        bool _dead;

        public bool IsDead => _dead || (_health != null && _health.IsDead);

        /// <summary>Firma original de la arena; se mantiene por compatibilidad.</summary>
        public void Configure(float speed, float damage, ArenaDirector director)
        {
            Configure(speed, damage, (IArenaCombatDirector)director);
        }

        /// <summary>Permite usar el mismo monstruo desde la mazmorra.</summary>
        public void Configure(float speed, float damage, IArenaCombatDirector director)
        {
            moveSpeed = speed;
            hitDamage = damage;
            _director = director;
        }

        /// <summary>Sobrecarga visual con la firma antigua intacta.</summary>
        public void Configure(float speed, float damage, IArenaCombatDirector director,
            int shape, Color accent)
        {
            Configure(speed, damage, director);
            SetVisualShape(shape, accent);
        }

        /// <summary>Sobrecarga visual para la arena.</summary>
        public void Configure(float speed, float damage, ArenaDirector director,
            int shape, Color accent)
        {
            Configure(speed, damage, director);
            SetVisualShape(shape, accent);
        }

        /// <summary>
        /// Perfil visual opcional: 0 clásico, 1 tanque, 2 lanzador alto y
        /// 3 bestia. Solo cambia mallas y colores; el CharacterController,
        /// el combate y la red quedan intactos. Se llama antes de Start.
        /// </summary>
        public void SetVisualShape(int shape, Color accent)
        {
            _shape = Mathf.Clamp(shape, 0, 3);
            _accent = accent;
            if (_built) return; // la forma ya se aplicó al construirse
        }

        void Awake()
        {
            _health = GetComponent<ChaosHealth>();
            if (_health == null) _health = gameObject.AddComponent<ChaosHealth>();

            Collider primitiveCollider = GetComponent<Collider>();
            if (primitiveCollider != null) Destroy(primitiveCollider);

            _controller = GetComponent<CharacterController>();
            if (_controller == null) _controller = gameObject.AddComponent<CharacterController>();
            _controller.height = 1.8f;
            _controller.radius = 0.34f;
            _controller.center = Vector3.zero;
            _controller.slopeLimit = 50f;
            _controller.stepOffset = 0.42f;
            _controller.skinWidth = 0.06f;

            if (_director == null) _director = FindFirstObjectByType<ArenaDirector>();
        }

        void Start()
        {
            _baseScale = transform.localScale;
            BuildVisual();
        }

        void BuildVisual()
        {
            if (_built) return;
            _built = true;

            var parts = new List<Renderer>();
            var colors = new List<Color>();
            var rootRenderer = GetComponent<Renderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;

            int seed = Mathf.Abs(transform.GetInstanceID()) % 5;
            float accentMix = _shape == 0 ? 0f : 0.3f;
            Color bodyColor = Color.Lerp(BodyColor, _accent, accentMix);
            Color headColor = Color.Lerp(HeadColor, _accent, accentMix * 0.8f);
            Color limbColor = Color.Lerp(LegColor, _accent, accentMix * 0.55f);
            Color armColor = Color.Lerp(ArmColor, _accent, accentMix * 0.7f);
            Color bellyColor = Color.Lerp(HeadColor, Color.white, 0.28f);
            Color shadow = Color.Lerp(bodyColor, new Color(0.07f, 0.11f, 0.19f), 0.55f);
            Color rim = Color.Lerp(_accent, new Color(0.58f, 0.92f, 1f), 0.45f);

            Material bodyMat = ArenaCreatureKit.Toon(bodyColor, shadow, rim, 0.18f, 0.22f);
            Material headMat = ArenaCreatureKit.Toon(headColor,
                Color.Lerp(headColor, shadow, 0.45f), rim, 0.24f, 0.26f);
            Material limbMat = ArenaCreatureKit.Toon(limbColor,
                Color.Lerp(limbColor, shadow, 0.4f), rim, 0.16f, 0.2f);
            Material armMat = ArenaCreatureKit.Toon(armColor,
                Color.Lerp(armColor, shadow, 0.4f), rim, 0.16f, 0.2f);
            Material bellyMat = ArenaCreatureKit.Toon(bellyColor,
                Color.Lerp(headColor, shadow, 0.35f), rim, 0.2f, 0.24f);
            Material eyeMat = ArenaCreatureKit.Toon(EyeColor,
                Color.Lerp(EyeColor, Color.black, 0.35f), new Color(1f, 0.95f, 0.72f),
                0.3f, 0.48f, 0.07f);
            Material darkMat = ArenaCreatureKit.Toon(new Color(0.10f, 0.07f, 0.14f),
                new Color(0.04f, 0.03f, 0.07f), new Color(0.7f, 0.8f, 1f), 0.12f, 0.14f);
            Material hornMat = ArenaCreatureKit.Toon(
                Color.Lerp(new Color(0.96f, 0.78f, 0.35f), _accent, 0.35f),
                new Color(0.42f, 0.28f, 0.12f), new Color(1f, 0.9f, 0.6f), 0.26f, 0.3f);

            // Pivote de columna: torso, panza, cabeza y brazos se balancean
            // juntos con una flexión suave; las piernas siguen ancladas al suelo.
            _spine = new GameObject("SpinePivot").transform;
            _spine.SetParent(transform, false);
            _spine.localPosition = new Vector3(0f, SpineY, 0f);

            Vector3 torsoScale = Vector3.one * 1.12f;
            Vector3 headScale = Vector3.one;
            float headY = headHeight;
            float hornLength = 0.3f;
            float tailLength = 0.52f;
            float armRadius = 0.085f;
            float armLength = 0.5f;
            if (_shape == 1)
            {
                torsoScale = new Vector3(1.55f, 0.86f, 1.3f);
                headScale = new Vector3(1.16f, 0.9f, 1.08f);
                headY = 0.92f;
                hornLength = 0.42f;
                tailLength = 0.32f;
                armRadius = 0.12f;
                armLength = 0.46f;
            }
            else if (_shape == 2)
            {
                torsoScale = new Vector3(0.82f, 1.5f, 0.82f);
                headScale = new Vector3(0.84f, 1.22f, 0.84f);
                headY = 1.2f;
                hornLength = 0.5f;
                tailLength = 0.7f;
                armRadius = 0.072f;
                armLength = 0.62f;
            }
            else if (_shape == 3)
            {
                torsoScale = new Vector3(1.4f, 1.04f, 1.4f);
                headScale = new Vector3(1.22f, 0.9f, 1.25f);
                headY = 0.9f;
                hornLength = 0.24f;
                tailLength = 0.4f;
                armRadius = 0.1f;
                armLength = 0.44f;
            }
            _headLocalY = headY - SpineY;

            _torso = ArenaCreatureKit.Make(_spine, "Torso",
                CaosVisualKit.OrganicCrown(0.66f, 10 + seed, 0.86f), bodyMat,
                new Vector3(0f, 0.22f - SpineY, 0f), torsoScale, Quaternion.identity,
                parts, colors, bodyColor);

            ArenaCreatureKit.Make(_spine, "Belly",
                CaosVisualKit.OrganicCrown(0.34f, 20 + seed, 0.9f), bellyMat,
                new Vector3(0f, 0.16f - SpineY, 0.66f * torsoScale.z),
                new Vector3(torsoScale.x * 0.94f, 1f, 0.24f),
                Quaternion.identity, parts, colors, bellyColor);

            _head = ArenaCreatureKit.Make(_spine, "Head",
                CaosVisualKit.OrganicCrown(0.42f, 30 + seed, 0.86f), headMat,
                new Vector3(0f, _headLocalY, 0.08f), headScale, Quaternion.identity,
                parts, colors, headColor);

            _eyes = new Transform[2];
            _brows = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                _eyes[i] = new GameObject("EyePivot_" + i).transform;
                _eyes[i].SetParent(_head, false);
                _eyes[i].localPosition = new Vector3(side * 0.16f, 0.05f, 0.36f);
                ArenaCreatureKit.Make(_eyes[i], "EyeWhite",
                    CaosVisualKit.OrganicCrown(0.105f, 41 + i, 1f), eyeMat,
                    Vector3.zero, new Vector3(1f, 1.05f, 0.55f), Quaternion.identity,
                    parts, colors, EyeColor);
                ArenaCreatureKit.Make(_eyes[i], "Pupil",
                    CaosVisualKit.OrganicCrown(0.052f, 45 + i, 1f), darkMat,
                    new Vector3(0f, 0f, 0.056f), new Vector3(0.85f, 1f, 0.5f),
                    Quaternion.identity, parts, colors, new Color(0.10f, 0.07f, 0.14f));

                _brows[i] = ArenaCreatureKit.Make(_head, "Brow_" + i,
                    CaosVisualKit.RoundedBox(new Vector3(0.17f, 0.038f, 0.045f), 0.018f, 2),
                    darkMat, new Vector3(side * 0.17f, 0.19f, 0.36f), Vector3.one,
                    Quaternion.Euler(0f, 0f, side * 14f), parts, colors,
                    new Color(0.10f, 0.07f, 0.14f));

                Vector3 hornPosition = _shape == 2
                    ? new Vector3(side * 0.14f, 0.4f, -0.04f)
                    : new Vector3(side * 0.28f, 0.32f, -0.02f);
                float hornLean = _shape == 3 ? -34f : -18f;
                ArenaCreatureKit.Make(_head, "Horn_" + i,
                    CaosVisualKit.TaperedTrunk(hornLength, 0.1f, 0.018f, 0.055f, 60 + seed + i),
                    hornMat, hornPosition, Vector3.one,
                    Quaternion.Euler(_shape == 2 ? -8f : 12f, 0f, side * hornLean),
                    parts, colors, Color.Lerp(new Color(0.96f, 0.78f, 0.35f), _accent, 0.35f));

                if (_shape == 1)
                {
                    // Hombreras anchas: el tanque se reconoce de lejos.
                    ArenaCreatureKit.Make(_spine, "ShoulderPlate_" + i,
                        CaosVisualKit.RoundedBox(new Vector3(0.3f, 0.12f, 0.34f), 0.055f, 3),
                        armMat, new Vector3(side * 0.66f, 0.42f - SpineY, 0.02f), Vector3.one,
                        Quaternion.Euler(0f, 0f, side * -12f), parts, colors, armColor);
                }
                else if (_shape == 2)
                {
                    // Cresta cristalina por encima de la cabeza del lanzador.
                    ArenaCreatureKit.Make(_head, "Crest_" + i,
                        CaosVisualKit.TaperedTrunk(0.34f, 0.07f, 0.012f, 0.04f, 66 + seed + i),
                        hornMat, new Vector3(side * 0.09f, 0.3f, -0.12f), Vector3.one,
                        Quaternion.Euler(-18f, 0f, side * 8f), parts, colors,
                        Color.Lerp(new Color(0.96f, 0.78f, 0.35f), _accent, 0.35f));
                }
            }

            _mouth = ArenaCreatureKit.Make(_head, "Mouth",
                CaosVisualKit.RoundedBox(new Vector3(_shape == 3 ? 0.34f : 0.24f, 0.05f, 0.045f),
                    0.022f, 2),
                darkMat, new Vector3(0f, -0.13f, 0.39f), Vector3.one, Quaternion.identity,
                parts, colors, new Color(0.10f, 0.07f, 0.14f));

            _legs = new Transform[2];
            _arms = new Transform[2];
            _feet = new Transform[2];
            _hands = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float legX = side * (_shape == 1 ? 0.34f : _shape == 3 ? 0.4f : 0.22f);
                _legs[i] = ArenaCreatureKit.MakeLimb(transform, "LegPivot_" + i,
                    _shape == 1 ? 0.14f : 0.105f, 0.55f, limbMat,
                    new Vector3(legX, -0.35f, 0f), Quaternion.identity, limbColor, parts, colors);
                _feet[i] = ArenaCreatureKit.Make(_legs[i], "Foot",
                    CaosVisualKit.OrganicCrown(0.16f, 70 + seed + i, 0.5f), limbMat,
                    new Vector3(0f, -0.54f, 0.06f), new Vector3(1.15f, 0.7f, 1.35f),
                    Quaternion.identity, parts, colors, limbColor);

                float armX = side * (_shape == 1 ? 0.76f : _shape == 3 ? 0.64f : 0.57f);
                _arms[i] = ArenaCreatureKit.MakeLimb(_spine, "ArmPivot_" + i,
                    armRadius, armLength, armMat,
                    new Vector3(armX, 0.42f - SpineY, 0.05f),
                    Quaternion.Euler(0f, 0f, side * 12f), armColor, parts, colors);
                _hands[i] = ArenaCreatureKit.Make(_arms[i], "Hand",
                    CaosVisualKit.OrganicCrown(0.13f, 80 + seed + i, 0.92f), armMat,
                    new Vector3(0f, -armLength, 0f), Vector3.one, Quaternion.identity,
                    parts, colors, armColor);
            }

            _tail = new GameObject("TailPivot").transform;
            _tail.SetParent(transform, false);
            _tail.localPosition = new Vector3(0f, 0.06f, -0.52f * torsoScale.z);
            _tail.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ArenaCreatureKit.Make(_tail, "Tail",
                CaosVisualKit.TaperedTrunk(tailLength, _shape == 3 ? 0.16f : 0.12f,
                    0.02f, _shape == 3 ? 0.11f : 0.09f, 90 + seed),
                bodyMat, Vector3.zero, Vector3.one, Quaternion.identity,
                parts, colors, bodyColor);
            ArenaCreatureKit.Make(_tail, "TailTip",
                CaosVisualKit.OrganicCrown(0.11f, 95 + seed, 0.9f), hornMat,
                new Vector3(0f, tailLength, 0f), Vector3.one, Quaternion.identity,
                parts, colors, Color.Lerp(new Color(0.96f, 0.78f, 0.35f), _accent, 0.35f));

            _renderers = parts.ToArray();
            _baseColors = colors.ToArray();
            _blinkOffset = seed * 0.31f;
        }

        void Update()
        {
            if (IsDead) return;
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player == null || _controller == null) return;

            if (_director == null) _director = FindFirstObjectByType<ArenaDirector>();
            if (TickStatus()) return;

            Vector3 target = ArenaNavigation.Target(transform.position, _player.transform.position);
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            float distance = flat.magnitude;

            if (distance > 0.05f)
            {
                Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look,
                    1f - Mathf.Exp(-10f * Time.deltaTime));

                float speed = _director != null ? _director.MonsterSpeed(moveSpeed) : moveSpeed;
                if (_slowTimer > 0f)
                {
                    _slowTimer -= Time.deltaTime;
                    speed *= _slowFactor;
                }
                if (_lunge > 0f)
                {
                    _lunge -= Time.deltaTime;
                    speed += 2.6f;
                }

                ArenaNavigation.Move(_controller, transform, target, speed, 0.34f, ref _verticalSpeed);

                if (_knockback.sqrMagnitude > 0.01f)
                {
                    _controller.Move(_knockback * Time.deltaTime);
                    _knockback = Vector3.Lerp(_knockback, Vector3.zero,
                        1f - Mathf.Exp(-6f * Time.deltaTime));
                }
            }
            else
            {
                _verticalSpeed += Physics.gravity.y * Time.deltaTime;
                if (_controller.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -2f;
                _controller.Move(Vector3.up * (_verticalSpeed * Time.deltaTime));
            }

            Animate(distance);

            float playerDistance = Vector3.Distance(_player.transform.position, transform.position);
            bool sameFloor = ArenaNavigation.SameFloor(_player.transform.position, transform.position);
            bool visible = ArenaNavigation.HasLineOfSight(transform, _player.transform);
            if (playerDistance <= attackRange && sameFloor && visible && Time.time >= _nextAttack &&
                _director != null && !_director.Finished)
            {
                _nextAttack = Time.time + attackCooldown;
                _lunge = 0.18f;
                float damage = _director.MonsterDamage(hitDamage);
                _director.DamagePlayer(damage, transform.position);
            }
        }

        /// <summary>Aplica quemadura/veneno. Devuelve true si el monstruo ha muerto.</summary>
        bool TickStatus()
        {
            float damage = 0f;
            if (_burnTimer > 0f)
            {
                _burnTimer -= Time.deltaTime;
                damage += _burnDps * Time.deltaTime;
            }
            if (_poisonTimer > 0f)
            {
                _poisonTimer -= Time.deltaTime;
                damage += _poisonDps * Time.deltaTime;
            }

            if (damage <= 0f) return false;

            _health.TakeDamage(damage, "estado");
            if (IsDead)
            {
                Die();
                return true;
            }
            return false;
        }

        void Animate(float distance)
        {
            _visualTime += Time.unscaledDeltaTime;
            _hurtTimer = Mathf.Max(0f, _hurtTimer - Time.unscaledDeltaTime);

            bool moving = distance > 0.1f;
            float motionScale = ArenaJuice.ReduceMotion ? 0.35f : 1f;
            float gait = _visualTime * (moving ? 10.5f : 2.6f);
            float swing = Mathf.Sin(gait) * (moving ? 28f : 6f) * motionScale;
            float recoil = (_hurtTimer > 0f
                ? Mathf.Sin((1f - _hurtTimer / 0.18f) * Mathf.PI) : 0f) * motionScale;

            if (_legs != null)
            {
                for (int i = 0; i < _legs.Length; i++)
                {
                    float sign = i == 0 ? 1f : -1f;
                    _legs[i].localRotation = Quaternion.Euler(swing * sign, 0f, 0f);
                    if (_feet != null && _feet[i] != null)
                        _feet[i].localRotation = Quaternion.Euler(-swing * sign * 0.35f, 0f, 0f);
                }
            }
            if (_arms != null)
            {
                for (int i = 0; i < _arms.Length; i++)
                {
                    float sign = i == 0 ? -1f : 1f;
                    _arms[i].localRotation = Quaternion.Euler(
                        swing * sign * 0.85f - recoil * 16f, 0f,
                        sign * (12f + Mathf.Sin(gait * 0.5f) * 4f));
                    if (_hands != null && _hands[i] != null)
                        _hands[i].localRotation = Quaternion.Euler(-swing * sign * 0.4f, 0f, 0f);
                }
            }
            if (_head != null)
            {
                Vector3 headPosition = _head.localPosition;
                headPosition.y = _headLocalY + Mathf.Sin(gait * 2f) * 0.045f * motionScale
                    - recoil * 0.08f;
                _head.localPosition = headPosition;
                _head.localRotation = Quaternion.Euler(
                    recoil * 12f, Mathf.Sin(gait * 0.5f) * 10f * motionScale,
                    Mathf.Sin(gait) * 4f * motionScale);
            }

            // La columna se dobla con el paso y se encoge al recibir impacto.
            if (_spine != null)
            {
                float sway = Mathf.Sin(gait * 0.5f);
                _spine.localRotation = Quaternion.Euler(
                    -recoil * 8f + Mathf.Sin(gait) * 2.5f * motionScale,
                    sway * 3f * motionScale,
                    sway * 5f * motionScale);
                _spine.localPosition = new Vector3(0f,
                    SpineY - Mathf.Abs(Mathf.Sin(gait)) * 0.035f * motionScale, 0f);
            }

            if (_torso != null)
                _torso.localRotation = Quaternion.Euler(-recoil * 6f, 0f,
                    Mathf.Sin(gait * 0.5f) * 2.5f * motionScale);

            if (_eyes != null)
            {
                float blinkWave = Mathf.Sin(_visualTime * 0.82f + _blinkOffset);
                float blink = blinkWave > 0.988f ? 0.08f : 1f;
                for (int i = 0; i < _eyes.Length; i++)
                    if (_eyes[i] != null) _eyes[i].localScale = new Vector3(1f, blink, 1f);
            }
            if (_brows != null)
            {
                for (int i = 0; i < _brows.Length; i++)
                    if (_brows[i] != null)
                        _brows[i].localRotation = Quaternion.Euler(0f, 0f,
                            (i == 0 ? 14f : -14f) + Mathf.Sin(gait * 0.5f) * 5f - recoil * 10f);
            }
            if (_mouth != null)
                _mouth.localScale = new Vector3(1f, 1f + Mathf.Sin(gait * 1.3f) * 0.22f + recoil * 0.5f, 1f);
            if (_tail != null)
                _tail.localRotation = Quaternion.Euler(-90f,
                    Mathf.Sin(_visualTime * 3.4f) * 22f * motionScale, 0f);

            float breathe = 1f + Mathf.Sin(gait * 0.7f) * (moving ? 0.035f : 0.055f);
            Vector3 juice = ArenaJuice.Active != null
                ? ArenaJuice.Active.SquashFactor(transform) : Vector3.one;
            transform.localScale = new Vector3(
                _baseScale.x * breathe * juice.x,
                _baseScale.y * (2f - breathe) * juice.y,
                _baseScale.z * breathe * juice.z);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 direction,
            ArenaElement element, bool allowChain = true)
        {
            if (IsDead || amount <= 0f) return;

            _health.TakeDamage(amount, "disparo");
            _hurtTimer = 0.18f;
            ApplyElement(element, direction, amount, allowChain);

            ChaosFx.Burst(hitPoint, BloodColor, Random.Range(5, 9));
            ArenaAudio.Play(ArenaAudio.Sound.Hit, hitPoint, 0.45f);
            ArenaJuice.Current.Impact(hitPoint, 0.35f, transform);

            if (_renderers != null && !_flashing) StartCoroutine(Flash());
            if (IsDead) Die();
        }

        void ApplyElement(ArenaElement element, Vector3 direction, float damage, bool allowChain)
        {
            switch (element)
            {
                case ArenaElement.Fuego:
                    _burnTimer = 3f;
                    _burnDps = Mathf.Max(4f, damage * 0.25f);
                    ChaosFx.Burst(transform.position + Vector3.up, ArenaElementInfo.Color(element), 5);
                    break;

                case ArenaElement.Aire:
                    _knockback = new Vector3(direction.x, 0f, direction.z).normalized * 9f;
                    break;

                case ArenaElement.Hielo:
                    _slowTimer = 2.5f;
                    _slowFactor = 0.5f;
                    ChaosFx.Burst(transform.position + Vector3.up, ArenaElementInfo.Color(element), 4);
                    break;

                case ArenaElement.Rayo:
                    if (allowChain)
                    {
                        _director?.ChainLightning(transform.position, damage * 0.6f, this);
                        ChaosFx.Burst(transform.position + Vector3.up,
                            ArenaElementInfo.Color(element), 6);
                    }
                    break;

                case ArenaElement.Veneno:
                    _poisonTimer = 4f;
                    _poisonDps = Mathf.Max(3f, damage * 0.2f);
                    ChaosFx.Burst(transform.position + Vector3.up, ArenaElementInfo.Color(element), 4);
                    break;
            }
        }

        IEnumerator Flash()
        {
            _flashing = true;
            bool reduced = ArenaJuice.ReduceFlashing;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color flash = reduced
                    ? Color.Lerp(_baseColors[i], new Color(1f, 0.82f, 0.7f), 0.18f)
                    : new Color(1f, 0.28f, 0.22f);
                ArenaCreatureKit.SetColor(_renderers[i], flash);
            }

            yield return new WaitForSecondsRealtime(reduced ? 0.08f : 0.12f);

            for (int i = 0; i < _renderers.Length; i++)
                ArenaCreatureKit.SetColor(_renderers[i], _baseColors[i]);
            _flashing = false;
        }

        void Die()
        {
            if (_dead) return;
            _dead = true;
            ChaosFx.Burst(transform.position + Vector3.up * 0.7f, BloodColor, 18);
            ArenaJuice.Current.BigHit(transform.position);
            _director?.MonsterKilled(this);
            Destroy(gameObject);
        }
    }
}
