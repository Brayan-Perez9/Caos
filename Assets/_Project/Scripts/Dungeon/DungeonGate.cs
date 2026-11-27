using UnityEngine;
using UnityEngine.InputSystem;

namespace Caos.Dungeon
{
    /// <summary>
    /// Puerta de salida de una sala. Se enciende al cumplir el objetivo y se
    /// abre con E/F al acercarse. El collider es el mismo trigger de siempre:
    /// solo se activa cuando la puerta está lista.
    /// </summary>
    public class DungeonGate : MonoBehaviour
    {
        static readonly Color ReadyColor = new Color(0.25f, 0.95f, 0.62f);
        static readonly Color LockedColor = new Color(0.42f, 0.45f, 0.55f);

        [SerializeField] Collider trigger;
        [SerializeField] Renderer visual;

        DungeonDirector _director;
        bool _active;

        public bool Active => _active;

        public void SetTrigger(Collider value) => trigger = value;
        public void SetVisual(Renderer value) => visual = value;

        void Awake()
        {
            if (trigger == null) trigger = GetComponent<Collider>();
            if (visual == null) visual = GetComponentInChildren<Renderer>();
            if (trigger != null) trigger.isTrigger = true;
            ApplyVisual(false);
        }

        /// <summary>El director inyecta la referencia al construir la sala.</summary>
        public void Configure(DungeonDirector director) => _director = director;

        public void SetActive(bool value)
        {
            _active = value;
            if (trigger != null) trigger.enabled = value;
            ApplyVisual(value);
        }

        void ApplyVisual(bool value)
        {
            if (visual == null) return;
            visual.material.color = value ? ReadyColor : LockedColor;
        }

        void OnTriggerStay(Collider other)
        {
            if (!_active || _director == null || !other.CompareTag("Player")) return;
            if (Keyboard.current == null) return;
            if (Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.fKey.wasPressedThisFrame)
            {
                _director.TryUseGate();
            }
        }
    }
}
