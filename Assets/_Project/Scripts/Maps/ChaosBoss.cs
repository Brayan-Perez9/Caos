using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Jefe final: embiste al jugador y pisa fuerte. Solo cae a martillazos.</summary>
    public class ChaosBoss : MonoBehaviour
    {
        [SerializeField] float speed = 2.6f;
        [SerializeField] float damage = 22f;
        [SerializeField] float slamCooldown = 3.4f;
        [SerializeField] float slamRadius = 3.5f;

        ChaosHealth _health;
        ChaosExpressMission _mission;
        PlayerController _player;
        Rigidbody _body;
        float _nextSlam;
        bool _dead;

        public void Configure(float hitPoints, ChaosExpressMission mission)
        {
            _health = GetComponent<ChaosHealth>();
            _health.Configure(hitPoints, false);
            _mission = mission;
        }

        void Awake()
        {
            _health = GetComponent<ChaosHealth>();
            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();

            _body.isKinematic = true;
            _body.useGravity = false;
        }

        void Start()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_mission == null) _mission = FindFirstObjectByType<ChaosExpressMission>();
        }

        void Update()
        {
            if (_dead || _health == null || _health.IsDead) return;

            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player == null) return;

            Vector3 to = _player.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;

            if (distance > 1.8f)
            {
                Vector3 next = transform.position + to.normalized * speed * Time.deltaTime;
                next.y = 1.3f + Mathf.Sin(Time.time * 4f) * 0.12f;
                _body.MovePosition(next);

                Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, look, 1f - Mathf.Exp(-5f * Time.deltaTime));
            }

            if (Time.time >= _nextSlam)
            {
                _nextSlam = Time.time + slamCooldown;
                Slam(distance);
            }
        }

        void Slam(float distance)
        {
            _mission?.ShowBanner("¡PISOTÓN! SALTA O ALÉJATE");
            ChaosFx.Burst(transform.position + Vector3.up * 0.2f,
                new Color(1f, 0.35f, 0.2f), 10);

            if (distance <= slamRadius)
            {
                _player.GetComponent<ChaosHealth>()?.TakeDamage(damage, "pisotón del jefe");
                _player.AddImpulse(Vector3.up * 5f);
            }
        }

        public void Die()
        {
            if (_dead) return;
            _dead = true;

            ChaosFx.Burst(transform.position + Vector3.up,
                new Color(1f, 0.85f, 0.3f), 16);
            _mission?.BossDefeated();
            Destroy(gameObject);
        }
    }
}
