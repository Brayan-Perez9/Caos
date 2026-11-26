using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Monstruo agresivo: persigue, carga contra el jugador y muerde.</summary>
    public class ChaosMonster : MonoBehaviour
    {
        [SerializeField] float speed = 4.6f;
        [SerializeField] float damage = 16f;
        [SerializeField] float attackCooldown = 0.75f;
        [SerializeField] float attackRange = 1.6f;

        ChaosHealth _health;
        PlayerController _player;
        Rigidbody _body;
        Vector3 _chargeDir;
        float _nextAttack;
        float _nextCharge;
        float _chargeUntil;
        float _phase;
        bool _dead;

        public void Configure(float moveSpeed, float hitDamage)
        {
            speed = moveSpeed;
            damage = hitDamage;
        }

        void Awake()
        {
            _health = GetComponent<ChaosHealth>();
            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();

            _body.isKinematic = true;
            _body.useGravity = false;
            _phase = Random.Range(0f, 6f);
            _nextCharge = Time.time + Random.Range(2f, 5f);
        }

        void Update()
        {
            if (_dead || (_health != null && _health.IsDead)) return;

            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player == null) return;

            Vector3 to = _player.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;

            if (Time.time >= _nextCharge && distance > 2.5f && distance < 11f)
            {
                _chargeDir = to.normalized;
                _chargeUntil = Time.time + 0.5f;
                _nextCharge = Time.time + Random.Range(4f, 7f);
                ChaosFx.Burst(transform.position + Vector3.up * 0.4f,
                    new Color(0.75f, 0.3f, 1f), 3);
            }

            float currentSpeed = Time.time < _chargeUntil ? speed * 2.6f : speed;
            Vector3 next = transform.position + to.normalized * currentSpeed * Time.deltaTime;
            next.y = 0.55f + Mathf.Sin(Time.time * 9f + _phase) * 0.1f;
            _body.MovePosition(next);

            Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, look, 1f - Mathf.Exp(-12f * Time.deltaTime));

            if (distance < attackRange && Time.time >= _nextAttack)
            {
                _nextAttack = Time.time + attackCooldown;
                _player.GetComponent<ChaosHealth>()?.TakeDamage(damage, "mordisco");
                _player.AddImpulse(to.normalized * -5f + Vector3.up * 3.5f);
            }
        }

        public void Kill()
        {
            if (_dead) return;
            _dead = true;
            FindFirstObjectByType<ChaosExpressMission>()?.MonsterKilled();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_dead)
                ChaosFx.Burst(transform.position + Vector3.up * 0.5f,
                    new Color(0.65f, 0.35f, 1f), 8);
        }
    }
}
