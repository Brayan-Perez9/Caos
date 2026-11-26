using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Monstruo de la arena: cuerpo, cabeza y piernas procedurales. Sangra al
    /// recibir disparos y persigue al jugador incluso si sube de piso.
    /// </summary>
    public class ArenaMonster : MonoBehaviour
    {
        static readonly Color BloodColor = new Color(0.62f, 0.02f, 0.03f);

        [SerializeField] float moveSpeed = 4.8f;
        [SerializeField] float hitDamage = 14f;
        [SerializeField] float attackCooldown = 0.85f;
        [SerializeField] float attackRange = 1.6f;

        ArenaDirector _director;
        ChaosHealth _health;
        PlayerController _player;
        Rigidbody _body;

        Transform[] _legs;
        Renderer[] _renderers;
        Color[] _baseColors;

        float _nextAttack;
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

        void Start() => BuildVisual();

        void BuildVisual()
        {
            if (_built) return;
            _built = true;

            var parts = new List<Renderer>();
            var colors = new List<Color>();

            Renderer torso = GetComponent<Renderer>();
            torso.material.color = new Color(0.48f, 0.08f, 0.12f);
            parts.Add(torso);
            colors.Add(torso.material.color);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 0.95f, 0.08f);
            head.transform.localScale = Vector3.one * 0.42f;
            Destroy(head.GetComponent<Collider>());
            Renderer headRenderer = head.GetComponent<Renderer>();
            headRenderer.material.color = new Color(0.62f, 0.12f, 0.16f);
            parts.Add(headRenderer);
            colors.Add(headRenderer.material.color);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = "Eye";
                eye.transform.SetParent(transform, false);
                eye.transform.localPosition = new Vector3(side * 0.15f, 1f, 0.34f);
                eye.transform.localScale = Vector3.one * 0.10f;
                Destroy(eye.GetComponent<Collider>());
                Renderer eyeRenderer = eye.GetComponent<Renderer>();
                eyeRenderer.material.color = new Color(1f, 0.86f, 0.25f);
                parts.Add(eyeRenderer);
                colors.Add(eyeRenderer.material.color);
            }

            _legs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Leg";
                leg.transform.SetParent(transform, false);
                leg.transform.localPosition = new Vector3(i == 0 ? -0.22f : 0.22f, -0.62f, 0f);
                leg.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
                Destroy(leg.GetComponent<Collider>());
                Renderer legRenderer = leg.GetComponent<Renderer>();
                legRenderer.material.color = new Color(0.28f, 0.05f, 0.08f);
                parts.Add(legRenderer);
                colors.Add(legRenderer.material.color);
                _legs[i] = leg.transform;
            }

            _renderers = parts.ToArray();
            _baseColors = colors.ToArray();
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
                Vector3 next = transform.position + flat.normalized * (speed * Time.deltaTime);
                next.y = Mathf.MoveTowards(transform.position.y,
                    _player.transform.position.y, speed * 0.8f * Time.deltaTime);

                if (_body != null) _body.MovePosition(next);
                else transform.position = next;
            }

            AnimateLegs(distance);

            if (distance <= attackRange && Time.time >= _nextAttack &&
                _director != null && !_director.Finished)
            {
                _nextAttack = Time.time + attackCooldown;
                float damage = _director.MonsterDamage(hitDamage);
                _director.DamagePlayer(damage, transform.position);
            }
        }

        void AnimateLegs(float distance)
        {
            if (_legs == null) return;
            float speed = distance > 0.1f ? 12f : 3f;
            float swing = Mathf.Sin(Time.time * speed) * 22f;
            _legs[0].localRotation = Quaternion.Euler(swing, 0f, 0f);
            _legs[1].localRotation = Quaternion.Euler(-swing, 0f, 0f);
        }

        public void TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDead || amount <= 0f) return;

            _health.TakeDamage(amount, "disparo");
            ChaosFx.Burst(hitPoint, BloodColor, Random.Range(4, 8));
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
            ChaosFx.Burst(transform.position + Vector3.up * 0.7f, BloodColor, 16);
            _director?.MonsterKilled(this);
            Destroy(gameObject);
        }
    }
}
