using UnityEngine;

namespace Caos.Dungeon
{
    /// <summary>Control de la carrera: solo cuenta el que toca en orden.</summary>
    public class DungeonCheckpoint : MonoBehaviour
    {
        [SerializeField] int index;

        DungeonRaceRoom _room;
        bool _active;

        public int Index => index;
        public bool Active => _active;

        public void SetIndex(int value) => index = value;

        public void Configure(DungeonRaceRoom room, int value)
        {
            _room = room;
            index = value;
        }

        public void SetActive(bool value)
        {
            _active = value;
            var trigger = GetComponent<Collider>();
            if (trigger != null) trigger.enabled = value;

            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.material.color = value
                    ? new Color(1f, 0.83f, 0.35f)
                    : new Color(0.32f, 0.35f, 0.46f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_active || _room == null || !other.CompareTag("Player")) return;
            _room.Reach(index);
        }
    }
}
