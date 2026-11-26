using System;
using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Bala: usa SphereCast a lo largo del movimiento para no atravesar a los
    /// monstruos aunque el disparo sea rápido o el frame largo. Lleva el
    /// elemento del arma para aplicar quemadura, hielo, rayo, etc.
    /// </summary>
    public class ArenaProjectile : MonoBehaviour
    {
        const float HitRadius = 0.38f;

        ArenaDirector _director;
        Vector3 _direction;
        ArenaElement _element;
        float _speed;
        float _damage;
        float _life;
        bool _spent;

        public void Configure(ArenaDirector director, Vector3 direction, float speed,
            float damage, ArenaElement element)
        {
            _director = director;
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _element = element;
            _life = 2.8f;

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
                    if (Process(hits[i].collider, hits[i].point)) return true;
                }
            }

            Collider[] overlaps = Physics.OverlapSphere(to, HitRadius, ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (Process(overlaps[i], to)) return true;
            }

            return false;
        }

        bool Process(Collider hit, Vector3 point)
        {
            if (hit == null) return false;
            if (hit.GetComponentInParent<PlayerController>() != null) return false;

            ArenaMonster monster = hit.GetComponentInParent<ArenaMonster>();
            if (monster != null)
            {
                _spent = true;
                monster.TakeDamage(_damage, point, _direction, _element);
                return true;
            }

            ArenaBoss boss = hit.GetComponentInParent<ArenaBoss>();
            if (boss != null)
            {
                _spent = true;
                boss.TakeDamage(_damage, point, _direction, _element);
                return true;
            }

            if (!hit.isTrigger)
            {
                ChaosFx.Burst(point, ArenaElementInfo.Color(_element), 3);
                return true;
            }

            return false;
        }
    }
}
