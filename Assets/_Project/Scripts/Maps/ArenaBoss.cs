using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Jefes de la arena. Tres criaturas con poderes distintos: martillo,
    /// cañón de ráfaga y tormenta con teletransporte y clones. Todos caminan
    /// con CharacterController, respetan el mapa y usan la escalera.
    /// </summary>
    public class ArenaBoss : MonoBehaviour
    {
        static readonly Color BloodColor = new Color(0.55f, 0.01f, 0.03f);
        static readonly Color EyeColor = new Color(1f, 0.86f, 0.25f);
        static readonly Color HammerColor = new Color(0.42f, 0.44f, 0.48f);
        static readonly Color HandleColor = new Color(0.55f, 0.34f, 0.16f);

        const float SpineY = 0.25f;

        [SerializeField] float moveSpeed = 3.1f;
        [SerializeField] float hitDamage = 26f;
        [SerializeField] float slamRadius = 4.3f;
        [SerializeField] float headHeight = 1.65f;

        ArenaBossKind _kind = ArenaBossKind.Martillo;
        int _bossIndex = 1;
        Color _bodyColor = new Color(0.34f, 0.04f, 0.10f);
        Color _accentColor = new Color(0.50f, 0.08f, 0.13f);
        float _powerCooldown = 3.1f;

        IArenaCombatDirector _director;
        ChaosHealth _health;
        PlayerController _player;
        CharacterController _controller;
        Transform _head;
        Transform _torso;
        Transform _spine;
        float _headLocalY;
        int _silhouette;
        Color _accentOverride;
        bool _hasAccentOverride;
        Transform _leftArm;
        Transform _rightArm;
        Transform _hammerPivot;
        Transform _weaponMuzzle;
        Transform[] _legs;
        Transform[] _feet;
        Transform[] _eyes;
        Transform[] _brows;
        Transform[] _accessories;
        Transform _mouth;
        Renderer _telegraph;
        Renderer[] _renderers;
        Color[] _baseColors;

        Vector3 _baseScale;
        Vector3 _knockback;
        float _verticalSpeed;
        float _visualTime;
        float _hurtTimer;
        float _blinkOffset;
        float _nextPower;
        float _powerAnimation;
        float _lunge;
        float _burnTimer;
        float _burnDps;
        float _poisonTimer;
        float _poisonDps;
        float _slowTimer;
        float _slowFactor = 1f;
        bool _built;
        bool _flashing;
        bool _dead;

        public ArenaBossKind Kind => _kind;
        public int BossIndex => _bossIndex;
        public bool IsDead => _dead || (_health != null && _health.IsDead);

        /// <summary>Firma original de la arena; se mantiene por compatibilidad.</summary>
        public void Configure(float speed, float damage, ArenaDirector director,
            ArenaBossKind kind, int index)
        {
            Configure(speed, damage, (IArenaCombatDirector)director, kind, index);
        }

        /// <summary>Permite usar el mismo jefe desde la mazmorra.</summary>
        public void Configure(float speed, float damage, IArenaCombatDirector director,
            ArenaBossKind kind, int index)
        {
            moveSpeed = speed;
            hitDamage = damage;
            _director = director;
            _kind = kind;
            _bossIndex = index;

            if (kind == ArenaBossKind.Canion)
            {
                _bodyColor = new Color(0.62f, 0.27f, 0.05f);
                _accentColor = new Color(0.95f, 0.48f, 0.10f);
                _powerCooldown = 1.9f;
            }
            else if (kind == ArenaBossKind.Tormenta)
            {
                _bodyColor = new Color(0.30f, 0.08f, 0.50f);
                _accentColor = new Color(0.66f, 0.25f, 0.95f);
                _powerCooldown = 3.4f;
            }
            else
            {
                _bodyColor = new Color(0.34f, 0.04f, 0.10f);
                _accentColor = new Color(0.50f, 0.08f, 0.13f);
                _powerCooldown = 3.0f;
            }

            _nextPower = Time.time + 1.2f;
        }

        /// <summary>
        /// Sobrecarga visual: mantiene la firma antigua y añade silueta y color.
        /// La silueta 1 es el superjefe (más crestas); el color tiña el cuerpo
        /// sin crear materiales nuevos por instancia.
        /// </summary>
        public void Configure(float speed, float damage, IArenaCombatDirector director,
            ArenaBossKind kind, int index, int silhouette, Color accent)
        {
            Configure(speed, damage, director, kind, index);
            SetVisualShape(silhouette, accent);
        }

        /// <summary>Perfil visual opcional, aplicado antes de construir la malla.</summary>
        public void SetVisualShape(int silhouette, Color accent)
        {
            _silhouette = Mathf.Clamp(silhouette, 0, 2);
            _accentOverride = accent;
            _hasAccentOverride = true;
            if (_built) return;
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
            _controller.radius = 0.5f;
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

            int kindSeed = (int)_kind * 17 + _bossIndex;
            if (_hasAccentOverride)
            {
                _bodyColor = Color.Lerp(_bodyColor, _accentOverride, 0.32f);
                _accentColor = Color.Lerp(_accentColor, _accentOverride, 0.55f);
            }
            _headLocalY = headHeight - SpineY;
            Color legColor = Color.Lerp(_bodyColor, Color.black, 0.42f);
            Color armColor = Color.Lerp(_bodyColor, _accentColor, 0.38f);
            Color shadow = Color.Lerp(_bodyColor, new Color(0.06f, 0.09f, 0.18f), 0.55f);
            Color rim = _kind == ArenaBossKind.Tormenta
                ? new Color(0.78f, 0.48f, 1f, 1f)
                : _kind == ArenaBossKind.Canion
                    ? new Color(1f, 0.72f, 0.35f, 1f)
                    : new Color(0.6f, 0.9f, 1f, 1f);

            Material bodyMat = ArenaCreatureKit.Toon(_bodyColor, shadow, rim, 0.2f, 0.24f);
            Material accentMat = ArenaCreatureKit.Toon(_accentColor,
                Color.Lerp(_accentColor, shadow, 0.4f), rim, 0.26f, 0.28f);
            Material limbMat = ArenaCreatureKit.Toon(legColor,
                Color.Lerp(legColor, shadow, 0.35f), rim, 0.16f, 0.2f);
            Material armMat = ArenaCreatureKit.Toon(armColor,
                Color.Lerp(armColor, shadow, 0.35f), rim, 0.18f, 0.22f);
            Material eyeMat = ArenaCreatureKit.Toon(EyeColor,
                Color.Lerp(EyeColor, Color.black, 0.35f), new Color(1f, 0.96f, 0.75f),
                0.32f, 0.5f, 0.08f);
            Material darkMat = ArenaCreatureKit.Toon(new Color(0.09f, 0.06f, 0.12f),
                new Color(0.03f, 0.03f, 0.06f), rim, 0.14f, 0.14f);
            Material hornMat = ArenaCreatureKit.Toon(new Color(0.96f, 0.8f, 0.38f),
                new Color(0.42f, 0.28f, 0.12f), new Color(1f, 0.9f, 0.6f), 0.28f, 0.3f);

            Color weaponColor = _kind == ArenaBossKind.Canion
                ? new Color(0.16f, 0.16f, 0.18f)
                : _kind == ArenaBossKind.Tormenta
                    ? new Color(0.52f, 0.20f, 0.82f)
                    : HammerColor;
            Material weaponMat = ArenaCreatureKit.Toon(weaponColor,
                Color.Lerp(weaponColor, Color.black, 0.45f), rim, 0.22f, 0.32f);
            Material handleMat = ArenaCreatureKit.Toon(HandleColor,
                new Color(0.2f, 0.11f, 0.05f), rim, 0.18f, 0.22f);

            // Columna flexible: torso, pecho, cabeza y brazos comparten pivote.
            // Las piernas y el telegrafo siguen sujetos a la raíz.
            _spine = new GameObject("SpinePivot").transform;
            _spine.SetParent(transform, false);
            _spine.localPosition = new Vector3(0f, SpineY, 0f);

            _torso = ArenaCreatureKit.Make(_spine, "Torso",
                CaosVisualKit.OrganicCrown(0.9f, 100 + kindSeed, 0.9f), bodyMat,
                new Vector3(0f, 0.15f - SpineY, 0f), Vector3.one * 1.14f, Quaternion.identity,
                parts, colors, _bodyColor);
            ArenaCreatureKit.Make(_spine, "ChestPlate",
                CaosVisualKit.OrganicCrown(0.44f, 108 + kindSeed, 0.95f), accentMat,
                new Vector3(0f, 0.28f - SpineY, 0.86f), new Vector3(1.12f, 1.05f, 0.22f),
                Quaternion.identity, parts, colors, _accentColor);

            _head = ArenaCreatureKit.Make(_spine, "Head",
                CaosVisualKit.OrganicCrown(0.62f, 120 + kindSeed, 0.84f), accentMat,
                new Vector3(0f, _headLocalY, 0.12f), Vector3.one, Quaternion.identity,
                parts, colors, _accentColor);

            _eyes = new Transform[2];
            _brows = new Transform[2];
            int hornCount = _silhouette == 1 ? 6
                : _kind == ArenaBossKind.Tormenta ? 4 : 2;
            _accessories = new Transform[hornCount];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                _eyes[i] = new GameObject("EyePivot_" + i).transform;
                _eyes[i].SetParent(_head, false);
                _eyes[i].localPosition = new Vector3(side * 0.23f, 0.07f, 0.52f);
                ArenaCreatureKit.Make(_eyes[i], "EyeWhite",
                    CaosVisualKit.OrganicCrown(0.14f, 130 + i, 1f), eyeMat,
                    Vector3.zero, new Vector3(1f, 1.08f, 0.55f), Quaternion.identity,
                    parts, colors, EyeColor);
                ArenaCreatureKit.Make(_eyes[i], "Pupil",
                    CaosVisualKit.OrganicCrown(0.07f, 134 + i, 1f), darkMat,
                    new Vector3(0f, 0f, 0.075f), new Vector3(0.85f, 1f, 0.5f),
                    Quaternion.identity, parts, colors, new Color(0.09f, 0.06f, 0.12f));

                _brows[i] = ArenaCreatureKit.Make(_head, "Brow_" + i,
                    CaosVisualKit.RoundedBox(new Vector3(0.24f, 0.05f, 0.06f), 0.022f, 2),
                    darkMat, new Vector3(side * 0.24f, 0.23f, 0.51f), Vector3.one,
                    Quaternion.Euler(0f, 0f, side * 16f), parts, colors,
                    new Color(0.09f, 0.06f, 0.12f));
            }

            for (int i = 0; i < hornCount; i++)
            {
                float angle = i / (float)hornCount * Mathf.PI * 2f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 0.42f, 0.5f,
                    Mathf.Sin(angle) * 0.42f);
                _accessories[i] = ArenaCreatureKit.Make(_head, "Crest_" + i,
                    CaosVisualKit.TaperedTrunk(0.38f, 0.12f, 0.018f, 0.07f, 140 + kindSeed + i),
                    hornMat, position, Vector3.one,
                    Quaternion.Euler(Mathf.Sin(angle) * 22f, 0f, -Mathf.Cos(angle) * 22f),
                    parts, colors, new Color(0.96f, 0.8f, 0.38f));
            }

            _mouth = ArenaCreatureKit.Make(_head, "Mouth",
                CaosVisualKit.RoundedBox(new Vector3(0.34f, 0.07f, 0.06f), 0.03f, 2),
                darkMat, new Vector3(0f, -0.19f, 0.52f), Vector3.one, Quaternion.identity,
                parts, colors, new Color(0.09f, 0.06f, 0.12f));

            _legs = new Transform[2];
            _feet = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                _legs[i] = ArenaCreatureKit.MakeLimb(transform, "LegPivot_" + i,
                    0.15f, 0.85f, limbMat, new Vector3(side * 0.38f, -0.55f, 0f),
                    Quaternion.identity, legColor, parts, colors);
                _feet[i] = ArenaCreatureKit.Make(_legs[i], "Foot",
                    CaosVisualKit.OrganicCrown(0.24f, 150 + kindSeed + i, 0.5f), limbMat,
                    new Vector3(0f, -0.83f, 0.09f), new Vector3(1.2f, 0.7f, 1.4f),
                    Quaternion.identity, parts, colors, legColor);
            }

            _leftArm = ArenaCreatureKit.MakeLimb(_spine, "LeftArmPivot",
                0.13f, 0.75f, armMat, new Vector3(-0.95f, 0.7f - SpineY, 0.1f),
                Quaternion.Euler(0f, 0f, 12f), armColor, parts, colors);
            ArenaCreatureKit.Make(_leftArm, "LeftHand",
                CaosVisualKit.OrganicCrown(0.19f, 160 + kindSeed, 0.9f), armMat,
                new Vector3(0f, -0.75f, 0f), Vector3.one, Quaternion.identity,
                parts, colors, armColor);

            _rightArm = ArenaCreatureKit.MakeLimb(_spine, "RightArmPivot",
                0.13f, 0.75f, armMat, new Vector3(0.95f, 0.7f - SpineY, 0.1f),
                Quaternion.Euler(0f, 0f, -12f), armColor, parts, colors);
            ArenaCreatureKit.Make(_rightArm, "RightHand",
                CaosVisualKit.OrganicCrown(0.19f, 161 + kindSeed, 0.9f), armMat,
                new Vector3(0f, -0.75f, 0f), Vector3.one, Quaternion.identity,
                parts, colors, armColor);

            _hammerPivot = new GameObject("HammerPivot").transform;
            _hammerPivot.SetParent(_rightArm, false);
            _hammerPivot.localPosition = new Vector3(0f, -0.72f, 0.05f);

            if (_kind == ArenaBossKind.Canion)
            {
                ArenaCreatureKit.Make(_hammerPivot, "CannonBarrel",
                    CaosVisualKit.RoundedCylinder(0.5f, 2f, 14, 2), weaponMat,
                    new Vector3(0f, 0f, 0.65f), new Vector3(0.52f, 0.65f, 0.52f),
                    Quaternion.Euler(90f, 0f, 0f), parts, colors, weaponColor);
                _weaponMuzzle = ArenaCreatureKit.Make(_hammerPivot, "CannonMuzzle",
                    CaosVisualKit.RoundedCylinder(0.5f, 2f, 14, 2), accentMat,
                    new Vector3(0f, 0f, 1.3f), new Vector3(0.64f, 0.08f, 0.64f),
                    Quaternion.Euler(90f, 0f, 0f), parts, colors, _accentColor);
            }
            else if (_kind == ArenaBossKind.Tormenta)
            {
                ArenaCreatureKit.Make(_hammerPivot, "StormCore",
                    CaosVisualKit.OrganicCrown(0.28f, 170 + kindSeed, 1f), weaponMat,
                    new Vector3(0f, 0f, 0.65f), Vector3.one, Quaternion.identity,
                    parts, colors, weaponColor);
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI * 2f / 3f;
                    ArenaCreatureKit.Make(_hammerPivot, "StormShard_" + i,
                        CaosVisualKit.OrganicCrown(0.16f, 175 + i, 0.7f), accentMat,
                        new Vector3(Mathf.Cos(angle) * 0.42f, Mathf.Sin(angle) * 0.42f, 0.65f),
                        Vector3.one, Quaternion.identity, parts, colors, _accentColor);
                }
            }
            else
            {
                ArenaCreatureKit.Make(_hammerPivot, "HammerHandle",
                    CaosVisualKit.RoundedCylinder(0.5f, 2f, 10, 2), handleMat,
                    new Vector3(0f, 0f, 0.6f), new Vector3(0.14f, 0.6f, 0.14f),
                    Quaternion.Euler(90f, 0f, 0f), parts, colors, HandleColor);
                ArenaCreatureKit.Make(_hammerPivot, "HammerHead",
                    CaosVisualKit.RoundedBox(new Vector3(0.72f, 0.72f, 0.72f), 0.13f, 3),
                    weaponMat, new Vector3(0f, 0f, 1.25f), Vector3.one,
                    Quaternion.identity, parts, colors, weaponColor);
                ArenaCreatureKit.Make(_hammerPivot, "HammerBand",
                    CaosVisualKit.RoundedBox(new Vector3(0.78f, 0.16f, 0.78f), 0.06f, 2),
                    accentMat, new Vector3(0f, 0f, 1.25f), Vector3.one,
                    Quaternion.identity, parts, colors, _accentColor);
            }

            var warningMat = ArenaCreatureKit.Toon(new Color(1f, 0.44f, 0.28f),
                new Color(0.5f, 0.14f, 0.1f), new Color(1f, 0.8f, 0.5f), 0.22f, 0.2f, 0.08f);
            var telegraph = ArenaCreatureKit.Make(transform, "PowerTelegraph",
                CaosVisualKit.RoundedCylinder(0.5f, 2f, 16, 2), warningMat,
                new Vector3(0f, -0.92f, 0f), new Vector3(2.6f, 0.02f, 2.6f),
                Quaternion.identity, parts, colors, new Color(1f, 0.44f, 0.28f));
            _telegraph = telegraph.GetComponent<Renderer>();
            _telegraph.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _telegraph.enabled = false;

            _renderers = parts.ToArray();
            _baseColors = colors.ToArray();
            _blinkOffset = kindSeed * 0.29f;
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
                    1f - Mathf.Exp(-7f * Time.deltaTime));

                float speed = _director != null ? _director.MonsterSpeed(moveSpeed) : moveSpeed;
                if (_slowTimer > 0f)
                {
                    _slowTimer -= Time.deltaTime;
                    speed *= _slowFactor;
                }
                if (_lunge > 0f)
                {
                    _lunge -= Time.deltaTime;
                    speed += 3.2f;
                }

                ArenaNavigation.Move(_controller, transform, target, speed, 0.5f, ref _verticalSpeed);

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

            Animate();

            if (Time.time < _nextPower || _director == null || _director.Finished) return;

            bool sameFloor = ArenaNavigation.SameFloor(_player.transform.position, transform.position);
            bool visible = ArenaNavigation.HasLineOfSight(transform, _player.transform);
            if (_kind == ArenaBossKind.Martillo && sameFloor && distance <= slamRadius && visible) Slam();
            else if (_kind == ArenaBossKind.Canion && sameFloor && visible) FireCannon();
            else if (_kind == ArenaBossKind.Tormenta) StormPulse();
        }

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

        void Animate()
        {
            _visualTime += Time.unscaledDeltaTime;
            _hurtTimer = Mathf.Max(0f, _hurtTimer - Time.unscaledDeltaTime);

            float motionScale = ArenaJuice.ReduceMotion ? 0.35f : 1f;
            float gait = _visualTime * (6f + _bossIndex);
            float swing = Mathf.Sin(gait) * 18f * motionScale;
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
            if (_leftArm != null)
                _leftArm.localRotation = Quaternion.Euler(-swing - recoil * 14f, 0f, 12f);
            if (_rightArm != null)
                _rightArm.localRotation = Quaternion.Euler(swing * 0.35f, 0f, -12f);

            if (_head != null)
            {
                Vector3 position = _head.localPosition;
                position.y = _headLocalY + Mathf.Sin(gait * 1.6f) * 0.06f * motionScale
                    - recoil * 0.07f;
                _head.localPosition = position;
                _head.localRotation = Quaternion.Euler(
                    recoil * 10f, Mathf.Sin(gait * 0.6f) * 12f * motionScale,
                    Mathf.Sin(gait) * 4f * motionScale);
            }

            // Flexión de columna: el jefe se balancea y se encoge al golpearlo.
            if (_spine != null)
            {
                float sway = Mathf.Sin(gait * 0.5f);
                _spine.localRotation = Quaternion.Euler(
                    -recoil * 7f + Mathf.Sin(gait) * 2f * motionScale,
                    sway * 3f * motionScale,
                    sway * 4f * motionScale);
                _spine.localPosition = new Vector3(0f,
                    SpineY - Mathf.Abs(Mathf.Sin(gait)) * 0.04f * motionScale, 0f);
            }

            if (_torso != null)
                _torso.localRotation = Quaternion.Euler(-recoil * 6f, 0f,
                    Mathf.Sin(gait * 0.5f) * 3f * motionScale);

            if (_eyes != null)
            {
                float blinkWave = Mathf.Sin(_visualTime * 0.68f + _blinkOffset);
                float blink = blinkWave > 0.986f ? 0.08f : 1f;
                for (int i = 0; i < _eyes.Length; i++)
                    if (_eyes[i] != null) _eyes[i].localScale = new Vector3(1f, blink, 1f);
            }
            if (_brows != null)
            {
                for (int i = 0; i < _brows.Length; i++)
                    if (_brows[i] != null)
                        _brows[i].localRotation = Quaternion.Euler(0f, 0f,
                            (i == 0 ? 16f : -16f) + Mathf.Sin(gait * 0.5f) * 6f - recoil * 12f);
            }
            if (_mouth != null)
                _mouth.localScale = new Vector3(1f, 1f + Mathf.Sin(gait * 1.1f) * 0.25f + recoil * 0.6f, 1f);

            if (_accessories != null)
            {
                for (int i = 0; i < _accessories.Length; i++)
                {
                    if (_accessories[i] == null) continue;
                    float angle = i / (float)_accessories.Length * Mathf.PI * 2f;
                    Vector3 basePosition = new Vector3(Mathf.Cos(angle) * 0.42f, 0.5f,
                        Mathf.Sin(angle) * 0.42f);
                    _accessories[i].localPosition = basePosition + Vector3.up *
                        (Mathf.Sin(_visualTime * 3f + angle) * 0.06f);
                    _accessories[i].localRotation = Quaternion.Euler(
                        Mathf.Sin(angle) * 22f, Mathf.Sin(_visualTime * 2f + angle) * 12f,
                        -Mathf.Cos(angle) * 22f);
                }
            }

            float breathe = 1f + Mathf.Sin(gait * 0.8f) * 0.03f;
            Vector3 juice = ArenaJuice.Active != null
                ? ArenaJuice.Active.SquashFactor(transform) : Vector3.one;
            transform.localScale = new Vector3(
                _baseScale.x * breathe * juice.x,
                _baseScale.y * (2f - breathe) * juice.y,
                _baseScale.z * breathe * juice.z);

            if (_telegraph != null)
            {
                bool warning = _powerAnimation > 0f;
                if (_telegraph.enabled != warning) _telegraph.enabled = warning;
                if (warning)
                {
                    float pulse = 1f + Mathf.Sin(_visualTime * 22f) * 0.08f;
                    _telegraph.transform.localScale = new Vector3(2.6f * pulse, 0.02f, 2.6f * pulse);
                }
            }

            if (_hammerPivot == null) return;
            if (_powerAnimation > 0f)
            {
                _powerAnimation = Mathf.Max(0f, _powerAnimation - Time.unscaledDeltaTime);
                float t = 1f - (_powerAnimation / 0.42f);
                _hammerPivot.localRotation = Quaternion.Euler(Mathf.Lerp(-115f, 24f, t), 0f, 0f);
            }
            else
            {
                float idle = _kind == ArenaBossKind.Canion ? 12f : 24f;
                _hammerPivot.localRotation = Quaternion.Euler(
                    idle + Mathf.Sin(gait * 0.9f) * 8f,
                    Mathf.Sin(_visualTime * 2.2f) * 6f, 0f);
            }
            if (_weaponMuzzle != null && _kind == ArenaBossKind.Canion)
                _weaponMuzzle.localScale = new Vector3(0.64f, 0.08f + Mathf.Sin(gait * 2f) * 0.012f, 0.64f);
        }

        void Slam()
        {
            _nextPower = Time.time + _powerCooldown;
            _powerAnimation = 0.42f;
            _lunge = 0.22f;
            ChaosFx.Burst(transform.position + Vector3.up * 0.35f,
                new Color(0.45f, 0.20f, 0.14f), 24);
            ArenaAudio.Play(ArenaAudio.Sound.Boss, transform.position, 0.85f);
            ArenaJuice.Current.AddTrauma(0.65f);
            _director?.ShowBanner("¡PISOTÓN DEL JEFE 1!", 1.6f);

            if (_player != null &&
                ArenaNavigation.SameFloor(_player.transform.position, transform.position) &&
                Vector3.Distance(_player.transform.position, transform.position) <= slamRadius)
            {
                _director?.DamagePlayer(_director.MonsterDamage(hitDamage), transform.position);
            }
        }

        void FireCannon()
        {
            if (_player == null ||
                !ArenaNavigation.SameFloor(_player.transform.position, transform.position)) return;

            _nextPower = Time.time + _powerCooldown;
            _lunge = 0.1f;

            Vector3 origin = transform.position + Vector3.up * 1.1f + transform.forward * 1f;
            Vector3 target = _player.transform.position + Vector3.up * 0.9f;
            Vector3 direction = (target - origin).normalized;

            for (int i = -1; i <= 1; i++)
            {
                Vector3 shotDirection = Quaternion.Euler(0f, i * 10f, 0f) * direction;
                _director?.SpawnEnemyProjectile(origin, shotDirection, 19f,
                    _director.MonsterDamage(hitDamage * 0.55f),
                    new Color(1f, 0.45f, 0.10f));
            }

            ChaosFx.Burst(origin, new Color(1f, 0.52f, 0.16f), 7);
            ArenaAudio.Play(ArenaAudio.Sound.Shoot, origin, 0.8f);
            ArenaJuice.Current.AddTrauma(0.3f);
            _director?.ShowBanner("¡CAÑÓN DEL JEFE 2!", 1.5f);
        }

        void StormPulse()
        {
            _nextPower = Time.time + _powerCooldown;
            _powerAnimation = 0.42f;

            Vector3 destination = _player.transform.position + new Vector3(
                Random.Range(-4.5f, 4.5f), 0f, Random.Range(-4.5f, 4.5f));
            destination.y = _player.transform.position.y;
            // La arena devuelve el punto sin cambios; la mazmorra lo mete en su sala.
            destination = _director.ClampWorldPosition(destination);

            if (_controller != null) _controller.enabled = false;
            transform.position = destination;
            if (_controller != null) _controller.enabled = true;

            ChaosFx.Burst(transform.position, new Color(0.66f, 0.25f, 0.95f), 24);
            ArenaAudio.Play(ArenaAudio.Sound.Boss, transform.position, 0.9f);
            ArenaJuice.Current.AddTrauma(0.55f);
            _director?.ShowBanner("¡JEFE 3: TORMENTA Y CLONES!", 2f);

            Vector3 origin = transform.position + Vector3.up * 1f;
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f + Random.Range(-6f, 6f);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                _director?.SpawnEnemyProjectile(origin, direction, 17f,
                    _director.MonsterDamage(hitDamage * 0.5f),
                    new Color(0.72f, 0.28f, 1f));
            }

            _director?.SummonMinions(2, transform.position);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 direction,
            ArenaElement element, bool allowChain = true)
        {
            if (IsDead || amount <= 0f) return;

            _health.TakeDamage(amount, "disparo");
            _hurtTimer = 0.18f;

            switch (element)
            {
                case ArenaElement.Fuego:
                    _burnTimer = 3f;
                    _burnDps = Mathf.Max(4f, amount * 0.2f);
                    break;
                case ArenaElement.Aire:
                    _knockback = new Vector3(direction.x, 0f, direction.z).normalized * 3f;
                    break;
                case ArenaElement.Hielo:
                    _slowTimer = 2f;
                    _slowFactor = 0.65f;
                    break;
                case ArenaElement.Rayo:
                    if (allowChain)
                        _director?.ChainLightning(transform.position, amount * 0.5f, this);
                    break;
                case ArenaElement.Veneno:
                    _poisonTimer = 4f;
                    _poisonDps = Mathf.Max(3f, amount * 0.15f);
                    break;
            }

            ChaosFx.Burst(hitPoint, BloodColor, Random.Range(3, 6));
            ArenaAudio.Play(ArenaAudio.Sound.Hit, hitPoint, 0.32f);
            ArenaJuice.Current.Impact(hitPoint, 0.55f, transform);

            if (_renderers != null && !_flashing) StartCoroutine(Flash());
            if (IsDead) Die();
        }

        IEnumerator Flash()
        {
            _flashing = true;
            bool reduced = ArenaJuice.ReduceFlashing;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color flash = reduced
                    ? Color.Lerp(_baseColors[i], new Color(1f, 0.84f, 0.72f), 0.18f)
                    : new Color(1f, 0.25f, 0.21f);
                ArenaCreatureKit.SetColor(_renderers[i], flash);
            }

            yield return new WaitForSecondsRealtime(reduced ? 0.08f : 0.11f);

            for (int i = 0; i < _renderers.Length; i++)
                ArenaCreatureKit.SetColor(_renderers[i], _baseColors[i]);
            _flashing = false;
        }

        void Die()
        {
            if (_dead) return;
            _dead = true;
            ChaosFx.Burst(transform.position + Vector3.up, BloodColor, 36);
            ArenaAudio.Play(ArenaAudio.Sound.Death, transform.position, 0.9f);
            ArenaJuice.Current.BigHit(transform.position + Vector3.up);
            _director?.BossDefeated(this);
            Destroy(gameObject);
        }
    }
}
