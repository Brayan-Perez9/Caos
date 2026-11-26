using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Jefe final de la arena: piernas, martillo y pisotones.</summary>
    public class ArenaBoss : MonoBehaviour
    {
        static readonly Color BloodColor = new Color(0.55f, 0.01f, 0.03f);
        static readonly Color BodyColor = new Color(0.34f, 0.04f, 0.10f);
        static readonly Color HeadColor = new Color(0.50f, 0.08f, 0.13f);
        static readonly Color LegColor = new Color(0.20f, 0.03f, 0.06f);
        static readonly Color ArmColor = new Color(0.42f, 0.07f, 0.11f);
        static readonly Color EyeColor = new Color(1f, 0.72f, 0.12f);
        static readonly Color HandleColor = new Color(0.55f, 0.34f, 0.16f);
        static readonly Color HammerColor = new Color(0.42f, 0.44f, 0.48f);

        [SerializeField] float moveSpeed = 3.1f;
        [SerializeField] float hitDamage = 26f;
        [SerializeField] float slamCooldown = 3.1f;
        [SerializeField] float slamRadius = 4.3f;
        [SerializeField] float headHeight = 1.65f;

        ArenaDirector _director;
        ChaosHealth _health;
        PlayerController _player;
        Rigidbody _body;
        Transform _head;
        Transform _leftArm;
        Transform _hammerPivot;
        Transform[] _legs;
        Renderer[] _renderers;
        Color[] _baseColors;

        Vector3 _baseScale;
        float _nextSlam;
        float _slamAnimation;
        float _lunge;
        bool _built;
        bool _flashing;

        public bool IsDead => _health != null && _health.IsDead;

        public void Configure(float speed, float damage, ArenaDirector director)
        {
            moveSpeed = speed;
            hitDamage = damage;
            _director = director;
            _nextSlam = Time.time + 1.4f;
        }

        void Awake()
        {
            _health = GetComponent<ChaosHealth>();
            if (_health == null) _health = gameObject.AddComponent<ChaosHealth>();

            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

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

            Renderer torso = GetComponent<Renderer>();
            torso.sharedMaterial = ArenaMaterial.Create(BodyColor);
            parts.Add(torso);
            colors.Add(BodyColor);

            _head = Part(PrimitiveType.Sphere, "Head",
                new Vector3(0f, headHeight, 0.12f), Vector3.one * 0.62f, HeadColor, parts, colors);

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
                    new Vector3(0.28f, 0.85f, 0.28f), LegColor, parts, colors);
            }

            _leftArm = Part(PrimitiveType.Cube, "LeftArm",
                new Vector3(-0.95f, 0.7f, 0.1f),
                new Vector3(0.24f, 0.75f, 0.24f), ArmColor, parts, colors);

            _hammerPivot = new GameObject("HammerPivot").transform;
            _hammerPivot.SetParent(transform, false);
            _hammerPivot.localPosition = new Vector3(0.95f, 0.7f, 0.2f);

            AddChild(PrimitiveType.Cube, "HammerHandle", _hammerPivot,
                new Vector3(0f, 0f, 0.6f), new Vector3(0.14f, 0.14f, 1.2f),
                HandleColor, parts, colors);
            AddChild(PrimitiveType.Cube, "HammerHead", _hammerPivot,
                new Vector3(0f, 0f, 1.25f), new Vector3(0.7f, 0.7f, 0.7f),
                HammerColor, parts, colors);

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
            if (_player == null) return;

            Vector3 flat = _player.transform.position - transform.position;
            flat.y = 0f;
            float distance = flat.magnitude;

            if (distance > 0.05f)
            {
                Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look,
                    1f - Mathf.Exp(-7f * Time.deltaTime));

                float speed = _director != null ? _director.MonsterSpeed(moveSpeed) : moveSpeed;
                if (_lunge > 0f)
                {
                    _lunge -= Time.deltaTime;
                    speed += 8f;
                }

                Vector3 next = transform.position + flat.normalized * (speed * Time.deltaTime);
                next.y = Mathf.MoveTowards(transform.position.y,
                    _player.transform.position.y, speed * 0.7f * Time.deltaTime);

                if (_body != null) _body.MovePosition(next);
                else transform.position = next;
            }

            Animate();

            if (distance <= slamRadius && Time.time >= _nextSlam &&
                _director != null && !_director.Finished)
            {
                _nextSlam = Time.time + slamCooldown;
                Slam();
            }
        }

        void Animate()
        {
            float gait = Time.time * 6f;
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
            if (_slamAnimation > 0f)
            {
                _slamAnimation = Mathf.Max(0f, _slamAnimation - Time.deltaTime);
                float t = 1f - (_slamAnimation / 0.42f);
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
            _slamAnimation = 0.42f;
            _lunge = 0.22f;
            ChaosFx.Burst(transform.position + Vector3.up * 0.35f,
                new Color(0.45f, 0.20f, 0.14f), 24);
            ArenaAudio.Play(ArenaAudio.Sound.Boss, transform.position, 0.85f);
            _director?.ShowBanner("¡PISOTÓN DEL JEFE!", 1.6f);

            if (_player != null &&
                Vector3.Distance(_player.transform.position, transform.position) <= slamRadius)
            {
                float damage = _director != null ? _director.MonsterDamage(hitDamage) : hitDamage;
                _director?.DamagePlayer(damage, transform.position);
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDead || amount <= 0f) return;

            _health.TakeDamage(amount, "disparo");
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
            ChaosFx.Burst(transform.position + Vector3.up, BloodColor, 36);
            ArenaAudio.Play(ArenaAudio.Sound.Death, transform.position, 0.9f);
            _director?.BossDefeated();
            Destroy(gameObject);
        }
    }
}
