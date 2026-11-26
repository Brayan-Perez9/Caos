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

        [SerializeField] float moveSpeed = 3.1f;
        [SerializeField] float hitDamage = 26f;
        [SerializeField] float slamCooldown = 3.1f;
        [SerializeField] float slamRadius = 4.3f;

        ArenaDirector _director;
        ChaosHealth _health;
        PlayerController _player;
        Rigidbody _body;
        Transform _hammerPivot;
        Transform[] _legs;
        Renderer[] _renderers;
        Color[] _baseColors;

        float _nextSlam;
        float _slamAnimation;
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

        void Start() => BuildVisual();

        void BuildVisual()
        {
            if (_built) return;
            _built = true;

            var parts = new List<Renderer>();
            var colors = new List<Color>();

            Renderer torso = GetComponent<Renderer>();
            torso.material.color = new Color(0.34f, 0.04f, 0.10f);
            parts.Add(torso);
            colors.Add(torso.material.color);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 1.65f, 0.12f);
            head.transform.localScale = Vector3.one * 0.62f;
            Destroy(head.GetComponent<Collider>());
            Renderer headRenderer = head.GetComponent<Renderer>();
            headRenderer.material.color = new Color(0.48f, 0.07f, 0.12f);
            parts.Add(headRenderer);
            colors.Add(headRenderer.material.color);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = "Eye";
                eye.transform.SetParent(transform, false);
                eye.transform.localPosition = new Vector3(side * 0.22f, 1.72f, 0.55f);
                eye.transform.localScale = Vector3.one * 0.13f;
                Destroy(eye.GetComponent<Collider>());
                Renderer eyeRenderer = eye.GetComponent<Renderer>();
                eyeRenderer.material.color = new Color(1f, 0.72f, 0.12f);
                parts.Add(eyeRenderer);
                colors.Add(eyeRenderer.material.color);
            }

            _legs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Leg";
                leg.transform.SetParent(transform, false);
                leg.transform.localPosition = new Vector3(i == 0 ? -0.36f : 0.36f, -0.92f, 0f);
                leg.transform.localScale = new Vector3(0.28f, 0.85f, 0.28f);
                Destroy(leg.GetComponent<Collider>());
                Renderer legRenderer = leg.GetComponent<Renderer>();
                legRenderer.material.color = new Color(0.20f, 0.03f, 0.06f);
                parts.Add(legRenderer);
                colors.Add(legRenderer.material.color);
                _legs[i] = leg.transform;
            }

            _hammerPivot = new GameObject("HammerPivot").transform;
            _hammerPivot.SetParent(transform, false);
            _hammerPivot.localPosition = new Vector3(0.95f, 0.7f, 0.2f);

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "HammerHandle";
            handle.transform.SetParent(_hammerPivot, false);
            handle.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            handle.transform.localScale = new Vector3(0.14f, 0.14f, 1.2f);
            Destroy(handle.GetComponent<Collider>());
            Renderer handleRenderer = handle.GetComponent<Renderer>();
            handleRenderer.material.color = new Color(0.55f, 0.34f, 0.16f);
            parts.Add(handleRenderer);
            colors.Add(handleRenderer.material.color);

            GameObject hammerHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hammerHead.name = "HammerHead";
            hammerHead.transform.SetParent(_hammerPivot, false);
            hammerHead.transform.localPosition = new Vector3(0f, 0f, 1.25f);
            hammerHead.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            Destroy(hammerHead.GetComponent<Collider>());
            Renderer hammerRenderer = hammerHead.GetComponent<Renderer>();
            hammerRenderer.material.color = new Color(0.42f, 0.44f, 0.48f);
            parts.Add(hammerRenderer);
            colors.Add(hammerRenderer.material.color);

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
                    1f - Mathf.Exp(-7f * Time.deltaTime));

                float speed = _director != null ? _director.MonsterSpeed(moveSpeed) : moveSpeed;
                Vector3 next = transform.position + flat.normalized * (speed * Time.deltaTime);
                next.y = Mathf.MoveTowards(transform.position.y,
                    _player.transform.position.y, speed * 0.7f * Time.deltaTime);

                if (_body != null) _body.MovePosition(next);
                else transform.position = next;
            }

            AnimateHammer();

            if (distance <= slamRadius && Time.time >= _nextSlam &&
                _director != null && !_director.Finished)
            {
                _nextSlam = Time.time + slamCooldown;
                Slam();
            }
        }

        void AnimateHammer()
        {
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
                    24f + Mathf.Sin(Time.time * 2.2f) * 5f, 0f, 0f);
            }

            if (_legs == null) return;
            float swing = Mathf.Sin(Time.time * 6f) * 14f;
            _legs[0].localRotation = Quaternion.Euler(swing, 0f, 0f);
            _legs[1].localRotation = Quaternion.Euler(-swing, 0f, 0f);
        }

        void Slam()
        {
            _slamAnimation = 0.42f;
            ChaosFx.Burst(transform.position + Vector3.up * 0.35f,
                new Color(0.45f, 0.20f, 0.14f), 22);
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
            ChaosFx.Burst(hitPoint, BloodColor, Random.Range(2, 5));
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
            ChaosFx.Burst(transform.position + Vector3.up, BloodColor, 34);
            ArenaAudio.Play(ArenaAudio.Sound.Death, transform.position, 0.9f);
            _director?.BossDefeated();
            Destroy(gameObject);
        }
    }
}
