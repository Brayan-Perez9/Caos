using UnityEngine;
using Caos.Maps;
using Caos.Player;

namespace Caos.Dungeon
{
    public enum DungeonPickupKind
    {
        Coin,
        Chest,
        Heart,
        Weapon
    }

    /// <summary>Recogida por proximidad: monedas, cofres, curación y armas.</summary>
    public class DungeonPickup : MonoBehaviour
    {
        DungeonDirector _director;
        Vector3 _origin;
        float _phase;
        bool _collected;

        public DungeonPickupKind Kind { get; private set; }
        public int Value { get; private set; }
        public ArenaWeaponKind Weapon { get; private set; }
        public ArenaElement Element { get; private set; }

        public void Configure(DungeonDirector director, DungeonPickupKind kind, int value,
            ArenaWeaponKind weapon = ArenaWeaponKind.Pistola,
            ArenaElement element = ArenaElement.Normal)
        {
            _director = director;
            _origin = transform.position;
            _phase = Random.value * Mathf.PI * 2f;
            Kind = kind;
            Value = value;
            Weapon = weapon;
            Element = element;
        }

        void Update()
        {
            if (_collected || _director == null || _director.Finished ||
                _director.TransitionActive) return;

            transform.Rotate(0f, 72f * Time.unscaledDeltaTime, 0f, Space.World);
            Vector3 position = _origin;
            position.y += Mathf.Sin(Time.unscaledTime * 2.1f + _phase) * 0.16f;
            transform.position = position;

            PlayerController player = _director.Player;
            if (player != null && Vector3.Distance(player.transform.position,
                    transform.position) < 1.5f)
            {
                _collected = true;
                _director.CollectPickup(this);
            }
        }
    }
}
