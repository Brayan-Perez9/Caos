using UnityEngine;
using Caos.Maps;
using Caos.Player;

namespace Caos.Dungeon
{
    /// <summary>
    /// Cañón de la mazmorra: apunta al jugador, avisa con una carga luminosa y
    /// dispara balas reutilizando ArenaEnemyProjectile. La malla es procedural
    /// y no añade coliders: el proyectil ya detecta al jugador por SphereCast.
    /// La cadencia se deriva de la semilla de la sala, sin campos de red nuevos.
    /// </summary>
    public class DungeonCannon : MonoBehaviour
    {
        const float ChargeTime = 0.55f;
        const float MaxRange = 26f;

        DungeonDirector _director;
        Transform _barrel;
        Transform _charge;
        Color _color;
        float _interval;
        float _damage;
        float _speed;
        float _nextShot;
        float _chargedUntil;
        bool _charging;

        /// <summary>Configura el cañón. <paramref name="phase"/> escalona los disparos.</summary>
        public void Configure(DungeonDirector director, float interval,
            float damage, float speed, Color color, float phase = 0f)
        {
            _director = director;
            _color = color;
            _interval = Mathf.Max(1f, interval);
            float scale = director != null ? director.DifficultyScale : 1f;
            float relics = director != null && director.State != null
                ? director.State.EnemyDamageMultiplier : 1f;
            _damage = Mathf.Max(1f, damage * scale * relics);
            _speed = Mathf.Max(10f, speed);
            _nextShot = Time.time + 1.4f + Mathf.Repeat(phase, _interval);

            BuildVisual();
            Debug.Log($"[Caos] Cañón de mazmorra listo: {gameObject.name} daño={_damage:0}");
        }

        /// <summary>Coloca cañones alrededor de la sala con posiciones derivadas de la ruta.</summary>
        public static void Place(DungeonDirector director, DungeonRoom room, int count,
            float damage, float interval, float phase = 0f)
        {
            if (director == null || room == null) return;
            count = Mathf.Clamp(count, 0, 6);
            if (count == 0) return;

            float ring = room.Radius * 0.7f;
            for (int i = 0; i < count; i++)
            {
                float angle = phase + i * Mathf.PI * 2f / count;
                Vector3 position = room.Clamp(room.Center + new Vector3(
                    Mathf.Cos(angle) * ring, 0f, Mathf.Sin(angle) * ring));

                GameObject go = new GameObject($"DungeonCannon_{room.Index}_{i}");
                go.transform.SetParent(room.transform, false);
                go.transform.position = position;

                DungeonCannon cannon = go.AddComponent<DungeonCannon>();
                cannon.Configure(director, interval, damage, 23f, DungeonPalette.UiCoral,
                    i * 0.75f + room.Index * 0.2f);
                room.TrackObject(go);
            }
        }

        void BuildVisual()
        {
            Material body = DungeonPalette.Wall;
            Material hot = DungeonPalette.Hazard;

            DungeonVisuals.Cylinder("Base", transform.position,
                new Vector3(1.15f, 0.22f, 1.15f), body, transform, false);
            DungeonVisuals.Cylinder("Pedestal", transform.position + Vector3.up * 0.55f,
                new Vector3(0.46f, 0.55f, 0.46f), body, transform, false);

            GameObject pivot = new GameObject("Pivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.position = transform.position + Vector3.up * 1.05f;
            _barrel = pivot.transform;

            GameObject tube = DungeonVisuals.Cylinder("Canon",
                pivot.transform.position, new Vector3(0.36f, 0.72f, 0.36f),
                hot, pivot.transform, false);
            tube.transform.localPosition = Vector3.zero;
            tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject muzzle = DungeonVisuals.Sphere("Boca",
                pivot.transform.position, 0.52f, hot, pivot.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.78f);

            GameObject glow = DungeonVisuals.Sphere("Carga",
                pivot.transform.position, 0.3f, hot, pivot.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0f, 0.95f);
            _charge = glow.transform;
            _charge.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_director == null || _director.Finished) return;

            PlayerController player = _director.Player;
            if (player == null) return;

            Vector3 muzzle = _barrel != null
                ? _barrel.position + _barrel.forward * 0.95f
                : transform.position + Vector3.up * 1.05f;
            Vector3 aim = player.transform.position + Vector3.up * 0.95f;
            Vector3 direction = aim - muzzle;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;

            // El cañón gira sobre su eje vertical; nunca se inclina hacia el suelo.
            _barrel.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            if (direction.magnitude > MaxRange)
            {
                StopCharge();
                return;
            }

            if (_charging)
            {
                float left = Mathf.Clamp01((_chargedUntil - Time.time) / ChargeTime);
                _charge.localScale = Vector3.one * (0.3f + (1f - left) * 0.9f);
                if (Time.time < _chargedUntil) return;

                StopCharge();
                if (_director.Bridge == null) return;
                ((IArenaCombatDirector)_director.Bridge).SpawnEnemyProjectile(
                    muzzle, direction.normalized, _speed, _damage, _color);
                Debug.Log($"[Caos] Cañón dispara: {gameObject.name} bala daño={_damage:0}");
                _nextShot = Time.time + _interval;
                return;
            }

            if (Time.time < _nextShot) return;
            _charging = true;
            _chargedUntil = Time.time + ChargeTime;
            _charge.gameObject.SetActive(true);
        }

        void StopCharge()
        {
            _charging = false;
            if (_charge != null) _charge.gameObject.SetActive(false);
        }
    }
}
