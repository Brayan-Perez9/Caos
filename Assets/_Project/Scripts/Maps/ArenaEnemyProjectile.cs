using System;
using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Proyectil disparado por los jefes. Solo hace daño al jugador.</summary>
    public class ArenaEnemyProjectile : MonoBehaviour
    {
        const float HitRadius = 0.4f;

        ArenaDirector _director;
        Vector3 _direction;
        float _speed;
        float _damage;
        float _life;
        bool _spent;

        public void Configure(ArenaDirector director, Vector3 direction, float speed, float damage)
        {
            _director = director;
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _life = 4f;

            Collider collider = GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;

            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        void Update()
        {
            if (_director == null || _spent)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 previous = transform.position;
            Vector3 next = previous + _direction * (_speed * Time.deltaTime);

            if (HitAlong(previous, next))
            {
                Destroy(gameObject);
                return;
            }

            transform.position = next;
            _life -= Time.deltaTime;
            if (_life <= 0f) Destroy(gameObject);
        }

        bool HitAlong(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;

            if (distance > 0.0001f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(from, HitRadius, _direction,
                    distance, ~0, QueryTriggerInteraction.Collide);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < hits.Length; i++)
                {
                    if (Process(hits[i].collider)) return true;
                }
            }

            Collider[] overlaps = Physics.OverlapSphere(to, HitRadius, ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (Process(overlaps[i])) return true;
            }

            return false;
        }

        bool Process(Collider hit)
        {
            if (hit == null) return false;

            if (hit.GetComponentInParent<PlayerController>() != null)
            {
                _spent = true;
                _director.DamagePlayer(_damage, transform.position);
                ChaosFx.Burst(transform.position, new Color(1f, 0.35f, 0.2f), 5);
                return true;
            }

            if (!hit.isTrigger) return true;
            return false;
        }
    }
}
