using UnityEngine;
using Caos.Player;

namespace Caos.Dungeon
{
    public enum DungeonHazardMode
    {
        Orbit,
        Sweep,
        Pulse
    }

    /// <summary>
    /// Obstáculo reparable a simple vista: barra giratoria, choque que late o
    /// bola que orbita. Solo hace daño por proximidad con un cooldown corto.
    /// </summary>
    public class DungeonHazard : MonoBehaviour
    {
        DungeonDirector _director;
        Vector3 _anchor;
        Vector3 _baseScale;
        float _radius;
        float _speed;
        float _damage;
        float _angle;
        float _nextHit;
        DungeonHazardMode _mode;

        public void Configure(DungeonDirector director, Vector3 anchor, float radius,
            float speed, float damage, DungeonHazardMode mode, float phase = 0f)
        {
            _director = director;
            _anchor = anchor;
            _radius = radius;
            _speed = speed;
            _damage = damage;
            _mode = mode;
            _angle = phase;
            _baseScale = transform.localScale;
            _nextHit = Time.time + 0.8f;
        }

        void Update()
        {
            if (_director == null || _director.Finished || _director.TransitionActive) return;
            DungeonRoom room = _director.CurrentRoom;
            if (room == null || !room.Active || room.Completed || room.Failed) return;

            _angle += _speed * Time.deltaTime * Mathf.Rad2Deg;
            float radians = _angle * Mathf.Deg2Rad;

            switch (_mode)
            {
                case DungeonHazardMode.Sweep:
                    transform.position = _anchor + Vector3.up * 0.85f;
                    transform.rotation = Quaternion.Euler(0f, _angle, 0f);
                    break;

                case DungeonHazardMode.Pulse:
                    float pulse = 1f + Mathf.Sin(radians * 2f) * 0.22f;
                    transform.position = _anchor + Vector3.up * 0.8f;
                    transform.localScale = _baseScale * pulse;
                    break;

                default:
                    transform.position = _anchor + new Vector3(
                        Mathf.Cos(radians) * _radius, 0.85f, Mathf.Sin(radians) * _radius);
                    transform.rotation = Quaternion.Euler(0f, -_angle, 0f);
                    break;
            }

            PlayerController player = _director.Player;
            if (player == null || Time.time < _nextHit) return;
            if (Vector3.Distance(player.transform.position, transform.position) > 1.5f) return;

            _nextHit = Time.time + 0.65f;
            _director.DamagePlayer(_damage, transform.position);
        }
    }
}
