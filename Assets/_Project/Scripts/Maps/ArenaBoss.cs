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

        [SerializeField] float moveSpeed = 3.1f;
        [SerializeField] float hitDamage = 26f;
        [SerializeField] float slamRadius = 4.3f;
        [SerializeField] float headHeight = 1.65f;

        ArenaBossKind _kind = ArenaBossKind.Martillo;
        int _bossIndex = 1;
        Color _bodyColor = new Color(0.34f, 0.04f, 0.10f);
        Color _accentColor = new Color(0.50f, 0.08f, 0.13f);
        float _powerCooldown = 3.1f;

        ArenaDirector _director;
        ChaosHealth _health;
        PlayerController _player;
        CharacterController _controller;
        Transform _head;
        Transform _leftArm;
        Transform _hammerPivot;
        Transform[] _legs;
        Renderer[] _renderers;
        Color[] _baseColors;

        Vector3 _baseScale;
        Vector3 _knockback;
        float _verticalSpeed;
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

        public void Configure(float speed, float damage, ArenaDirector director,
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
            Color legColor = Color.Lerp(_bodyColor, Color.black, 0.45f);
            Color armColor = Color.Lerp(_bodyColor, _accentColor, 0.35f);

            Renderer torso = GetComponent<Renderer>();
            torso.sharedMaterial = ArenaMaterial.Create(_bodyColor);
            parts.Add(torso);
            colors.Add(_bodyColor);

            _head = Part(PrimitiveType.Sphere, "Head",
                new Vector3(0f, headHeight, 0.12f), Vector3.one * 0.62f, _accentColor, parts, colors);

            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Sphere, "Eye",
                    new Vector3(side * 0.22f, headHeight + 0.07f, 0.55f),
                    Vector3.one * 0.13f, EyeColor, parts, colors);
            }

            _legs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                _legs[i] = Part(PrimitiveType.Cube, "Leg",
                    new Vector3(side * 0.36f, -0.92f, 0f),
                    new Vector3(0.28f, 0.85f, 0.28f), legColor, parts, colors);
            }

            _leftArm = Part(PrimitiveType.Cube, "LeftArm",
                new Vector3(-0.95f, 0.7f, 0.1f),
                new Vector3(0.24f, 0.75f, 0.24f), armColor, parts, colors);

            _hammerPivot = new GameObject("HammerPivot").transform;
            _hammerPivot.SetParent(transform, false);
            _hammerPivot.localPosition = new Vector3(0.95f, 0.7f, 0.2f);

            Color weaponColor = _kind == ArenaBossKind.Canion
                ? new Color(0.16f, 0.16f, 0.18f)
                : _kind == ArenaBossKind.Tormenta
                    ? new Color(0.52f, 0.20f, 0.82f)
                    : HammerColor;

            AddChild(PrimitiveType.Cube, "HammerHandle", _hammerPivot,
                new Vector3(0f, 0f, 0.6f), new Vector3(0.14f, 0.14f, 1.2f),
                HandleColor, parts, colors);
            AddChild(PrimitiveType.Cube, "HammerHead", _hammerPivot,
                new Vector3(0f, 0f, 1.25f), new Vector3(0.7f, 0.7f, 0.7f),
                weaponColor, parts, colors);

            _renderers = parts.ToArray();
            _baseColors = colors.ToArray();
        }

        Transform Part(PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale,
            Color color, List<Renderer> renderers, List<Color> colors)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Destroy(part.GetComponent<Collider>());
            return Register(part, color, renderers, colors);
        }

        Transform AddChild(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Color color,
            List<Renderer> renderers, List<Color> colors)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Destroy(part.GetComponent<Collider>());
            return Register(part, color, renderers, colors);
        }

        Transform Register(GameObject part, Color color, List<Renderer> renderers, List<Color> colors)
        {
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = ArenaMaterial.Create(color);
            renderers.Add(renderer);
            colors.Add(color);
            return part.transform;
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

            bool visible = ArenaNavigation.HasLineOfSight(transform, _player.transform);
            if (_kind == ArenaBossKind.Martillo && distance <= slamRadius && visible) Slam();
            else if (_kind == ArenaBossKind.Canion) FireCannon();
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
            float gait = Time.time * (6f + _bossIndex);
            float swing = Mathf.Sin(gait) * 16f;

            if (_legs != null)
            {
                _legs[0].localRotation = Quaternion.Euler(swing, 0f, 0f);
                _legs[1].localRotation = Quaternion.Euler(-swing, 0f, 0f);
            }
            if (_leftArm != null) _leftArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            if (_head != null)
            {
                Vector3 position = _head.localPosition;
                position.y = headHeight + Mathf.Sin(gait * 1.6f) * 0.08f;
                _head.localPosition = position;
                _head.localRotation = Quaternion.Euler(0f, Mathf.Sin(gait * 0.6f) * 12f, 0f);
            }

            float breathe = 1f + Mathf.Sin(gait * 0.8f) * 0.028f;
            transform.localScale = new Vector3(
                _baseScale.x * breathe,
                _baseScale.y * (2f - breathe),
                _baseScale.z * breathe);

            if (_hammerPivot == null) return;
            if (_powerAnimation > 0f)
            {
                _powerAnimation = Mathf.Max(0f, _powerAnimation - Time.deltaTime);
                float t = 1f - (_powerAnimation / 0.42f);
                _hammerPivot.localRotation = Quaternion.Euler(Mathf.Lerp(-115f, 24f, t), 0f, 0f);
            }
            else
            {
                _hammerPivot.localRotation = Quaternion.Euler(
                    24f + Mathf.Sin(gait * 0.9f) * 8f, 0f, 0f);
            }
        }

        void Slam()
        {
            _nextPower = Time.time + _powerCooldown;
            _powerAnimation = 0.42f;
            _lunge = 0.22f;
            ChaosFx.Burst(transform.position + Vector3.up * 0.35f,
                new Color(0.45f, 0.20f, 0.14f), 24);
            ArenaAudio.Play(ArenaAudio.Sound.Boss, transform.position, 0.85f);
            _director?.ShowBanner("¡PISOTÓN DEL JEFE 1!", 1.6f);

            if (_player != null &&
                Vector3.Distance(_player.transform.position, transform.position) <= slamRadius)
            {
                _director?.DamagePlayer(_director.MonsterDamage(hitDamage), transform.position);
            }
        }

        void FireCannon()
        {
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
            _director?.ShowBanner("¡CAÑÓN DEL JEFE 2!", 1.5f);
        }

        void StormPulse()
        {
            _nextPower = Time.time + _powerCooldown;
            _powerAnimation = 0.42f;

            Vector3 destination = _player.transform.position + new Vector3(
                Random.Range(-4.5f, 4.5f), 0f, Random.Range(-4.5f, 4.5f));
            destination.x = Mathf.Clamp(destination.x, -21f, 21f);
            destination.z = Mathf.Clamp(destination.z, -21f, 21f);
            destination.y = _player.transform.position.y;

            if (_controller != null) _controller.enabled = false;
            transform.position = destination;
            if (_controller != null) _controller.enabled = true;

            ChaosFx.Burst(transform.position, new Color(0.66f, 0.25f, 0.95f), 24);
            ArenaAudio.Play(ArenaAudio.Sound.Boss, transform.position, 0.9f);
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

            if (_renderers != null && !_flashing) StartCoroutine(Flash());
            if (IsDead) Die();
        }

        IEnumerator Flash()
        {
            _flashing = true;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].material.color = new Color(1f, 0.22f, 0.20f);

            yield return new WaitForSeconds(0.11f);

            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].material.color = _baseColors[i];
            _flashing = false;
        }

        void Die()
        {
            if (_dead) return;
            _dead = true;
            ChaosFx.Burst(transform.position + Vector3.up, BloodColor, 36);
            ArenaAudio.Play(ArenaAudio.Sound.Death, transform.position, 0.9f);
            _director?.BossDefeated(this);
            Destroy(gameObject);
        }
    }
}
