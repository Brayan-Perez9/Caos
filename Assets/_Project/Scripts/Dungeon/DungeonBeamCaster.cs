using UnityEngine;
using Caos.Maps;
using Caos.Player;

namespace Caos.Dungeon
{
    /// <summary>
    /// Arma de los lanzadores de rayos de la mazmorra. Primero telegrafía un
    /// haz y después dispara un proyectil rápido usando el contrato de combate
    /// existente, así que el daño sigue siendo autoritativo del director local.
    /// No añade colliders nuevos: el proyectil reutiliza ArenaEnemyProjectile.
    /// </summary>
    public class DungeonBeamCaster : MonoBehaviour
    {
        const float MaxRange = 20f;
        const float ChargeTime = 0.38f;

        IArenaCombatDirector _director;
        Transform _target;
        LineRenderer _beam;
        Material _material;
        Color _color;
        float _interval;
        float _damage;
        float _speed;
        float _nextShot;
        float _chargedUntil;
        bool _charging;

        /// <summary>Configura el rayo. Si no se da objetivo, busca al jugador local.</summary>
        public void Configure(IArenaCombatDirector director, Transform target,
            float interval, float damage, float speed, Color color)
        {
            _director = director;
            _target = target;
            _color = color;
            _interval = Mathf.Max(0.8f, interval);
            _damage = Mathf.Max(1f, damage);
            _speed = Mathf.Max(8f, speed);
            _nextShot = Time.time + Random.Range(0.8f, Mathf.Min(_interval, 3.5f));
            BuildBeam();
            Debug.Log($"[Caos] Rayo telegrafiado listo: {gameObject.name}");
        }

        void BuildBeam()
        {
            if (_beam != null) return;

            GameObject holder = new GameObject("RayoTelegrafo");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = Vector3.zero;

            _beam = holder.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.startWidth = 0.13f;
            _beam.endWidth = 0.035f;
            _beam.numCapVertices = 2;
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.receiveShadows = false;

            _material = ArenaMaterial.Create(_color, 0f, 0.85f);
            if (_material.HasProperty("_EmissionColor"))
            {
                _material.EnableKeyword("_EMISSION");
                _material.SetColor("_EmissionColor", _color * 1.6f);
            }
            _beam.material = _material;
            _beam.enabled = false;
        }

        void Update()
        {
            if (_director == null || _director.Finished)
            {
                HideBeam();
                return;
            }

            if (_target == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                _target = player != null ? player.transform : null;
            }
            if (_target == null)
            {
                HideBeam();
                return;
            }

            Vector3 origin = transform.position + Vector3.up * 1.15f;
            Vector3 aim = _target.position + Vector3.up * 0.95f;
            if (Vector3.Distance(origin, aim) > MaxRange)
            {
                HideBeam();
                return;
            }

            if (_charging)
            {
                DrawBeam(origin, aim);
                if (Time.time < _chargedUntil) return;

                HideBeam();
                Vector3 direction = aim - origin;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
                _director.SpawnEnemyProjectile(origin, direction.normalized,
                    _speed, _damage, _color);
                _nextShot = Time.time + _interval;
                return;
            }

            if (Time.time < _nextShot) return;
            _charging = true;
            _chargedUntil = Time.time + ChargeTime;
            DrawBeam(origin, aim);
        }

        void DrawBeam(Vector3 origin, Vector3 aim)
        {
            if (_beam == null) BuildBeam();
            if (_beam == null) return;
            _beam.enabled = true;
            _beam.SetPosition(0, origin);
            _beam.SetPosition(1, aim);
        }

        void HideBeam()
        {
            _charging = false;
            if (_beam != null) _beam.enabled = false;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
