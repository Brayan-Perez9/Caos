using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Bala sencilla: avanza recta y detecta impactos por superposición.</summary>
    public class ArenaProjectile : MonoBehaviour
    {
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

            transform.position += _direction * (_speed * Time.deltaTime);
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Collider[] hits = Physics.OverlapSphere(
                transform.position, 0.34f, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null) continue;

                if (hit.GetComponentInParent<PlayerController>() != null) continue;

                ArenaMonster monster = hit.GetComponentInParent<ArenaMonster>();
                if (monster != null)
                {
                    _spent = true;
                    monster.TakeDamage(_damage, transform.position);
                    Destroy(gameObject);
                    return;
                }

                ArenaBoss boss = hit.GetComponentInParent<ArenaBoss>();
                if (boss != null)
                {
                    _spent = true;
                    boss.TakeDamage(_damage, transform.position);
                    Destroy(gameObject);
                    return;
                }

                if (!hit.isTrigger)
                {
                    ChaosFx.Burst(transform.position, new Color(1f, 0.85f, 0.35f), 3);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
