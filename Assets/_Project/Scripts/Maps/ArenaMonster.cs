using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Monstruo de la arena: cuerpo, cabeza, brazos y piernas procedurales.
    /// Se mueve con animación de marcha, sangra al recibir disparos y persigue
    /// al jugador incluso si sube de piso.
    /// </summary>
    public class ArenaMonster : MonoBehaviour
    {
        static readonly Color BloodColor = new Color(0.62f, 0.02f, 0.03f);
        static readonly Color BodyColor = new Color(0.48f, 0.08f, 0.12f);
        static readonly Color HeadColor = new Color(0.68f, 0.14f, 0.18f);
        static readonly Color ArmColor = new Color(0.40f, 0.06f, 0.10f);
        static readonly Color LegColor = new Color(0.28f, 0.05f, 0.08f);
        static readonly Color EyeColor = new Color(1f, 0.86f, 0.25f);

        [SerializeField] float moveSpeed = 4.8f;
        [SerializeField] float hitDamage = 14f;
        [SerializeField] float attackCooldown = 0.85f;
        [SerializeField] float attackRange = 1.6f;
        [SerializeField] float headHeight = 0.95f;

        ArenaDirector _director;
        ChaosHealth _health;
        PlayerController _player;
        Rigidbody _body;

        Transform _head;
        Transform[] _legs;
        Transform[] _arms;
        Renderer[] _renderers;
        Color[] _baseColors;

        Vector3 _baseScale;
        float _nextAttack;
        float _lunge;
        bool _built;
        bool _flashing;

        public bool IsDead => _health != null && _health.IsDead;

        public void Configure(float speed, float damage, ArenaDirector director)
        {
            moveSpeed = speed;
            hitDamage = damage;
            _director = director;
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
                new Vector3(0f, headHeight, 0.08f), Vector3.one * 0.42f, HeadColor, parts, colors);

            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Sphere, "Eye",
                    new Vector3(side * 0.15f, headHeight + 0.05f, 0.34f),
                    Vector3.one * 0.10f, EyeColor, parts, colors);
            }

            _legs = new Transform[2];
            _arms = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                _legs[i] = Part(PrimitiveType.Cube, "Leg",
                    new Vector3(side * 0.22f, -0.62f, 0f),
                    new Vector3(0.18f, 0.55f, 0.18f), LegColor, parts, colors);
                _arms[i] = Part(PrimitiveType.Cube, "Arm",
                    new Vector3(side * 0.55f, 0.12f, 0.06f),
                    new Vector3(0.16f, 0.55f, 0.16f), ArmColor, parts, colors);
            }

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
                    1f - Mathf.Exp(-10f * Time.deltaTime));

                float speed = _director != null ? _director.MonsterSpeed(moveSpeed) : moveSpeed;
                if (_lunge > 0f)
                {
                    _lunge -= Time.deltaTime;
                    speed += 7f;
                }

                Vector3 next = transform.position + flat.normalized * (speed * Time.deltaTime);
                next.y = Mathf.MoveTowards(transform.position.y,
                    _player.transform.position.y, speed * 0.8f * Time.deltaTime);

                if (_body != null) _body.MovePosition(next);
                else transform.position = next;
            }

            Animate(distance);

            if (distance <= attackRange && Time.time >= _nextAttack &&
                _director != null && !_director.Finished)
            {
                _nextAttack = Time.time + attackCooldown;
                _lunge = 0.18f;
                float damage = _director.MonsterDamage(hitDamage);
                _director.DamagePlayer(damage, transform.position);
            }
        }

        void Animate(float distance)
        {
            float gait = Time.time * (distance > 0.1f ? 11f : 3f);
            float swing = Mathf.Sin(gait) * 26f;

            if (_legs != null)
            {
                _legs[0].localRotation = Quaternion.Euler(swing, 0f, 0f);
                _legs[1].localRotation = Quaternion.Euler(-swing, 0f, 0f);
            }
            if (_arms != null)
            {
                _arms[0].localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);
                _arms[1].localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f);
            }
            if (_head != null)
            {
                Vector3 headPosition = _head.localPosition;
                headPosition.y = headHeight + Mathf.Sin(gait * 2f) * 0.055f;
                _head.localPosition = headPosition;
                _head.localRotation = Quaternion.Euler(0f, Mathf.Sin(gait * 0.5f) * 10f, 0f);
            }

            float breathe = 1f + Mathf.Sin(gait * 0.7f) * 0.035f;
            transform.localScale = new Vector3(
                _baseScale.x * breathe,
                _baseScale.y * (2f - breathe),
                _baseScale.z * breathe);
        }

        public void TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDead || amount <= 0f) return;

            _health.TakeDamage(amount, "disparo");
            ChaosFx.Burst(hitPoint, BloodColor, Random.Range(5, 9));
            ArenaAudio.Play(ArenaAudio.Sound.Hit, hitPoint, 0.45f);

            if (_renderers != null && !_flashing) StartCoroutine(Flash());
            if (IsDead) Die();
        }

        IEnumerator Flash()
        {
            _flashing = true;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].material.color = new Color(1f, 0.25f, 0.22f);

            yield return new WaitForSeconds(0.12f);

            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].material.color = _baseColors[i];
            _flashing = false;
        }

        void Die()
        {
            ChaosFx.Burst(transform.position + Vector3.up * 0.7f, BloodColor, 18);
            _director?.MonsterKilled(this);
            Destroy(gameObject);
        }
    }
}
