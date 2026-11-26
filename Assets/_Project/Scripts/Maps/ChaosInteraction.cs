using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Clic izquierdo con prioridades: si llevas algo lo lanzas, si no coges
    /// el objeto que tengas delante y, si no hay nada, golpeas con el martillo.
    /// </summary>
    public class ChaosInteraction : MonoBehaviour
    {
        [SerializeField] float reach = 2.2f;

        ChaosThrowable _held;
        Transform _hand;
        ChaosWeapon _weapon;

        void Awake()
        {
            _hand = new GameObject("GrabHold").transform;
            _hand.SetParent(transform, false);
            _hand.localPosition = new Vector3(0f, 1f, 0.75f);
        }

        void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            if (_held != null)
            {
                _held.Throw();
                _held = null;
                return;
            }

            Vector3 center = transform.position + transform.forward * 1.1f + Vector3.up * 0.7f;
            foreach (var hit in Physics.OverlapSphere(center, reach, ~0, QueryTriggerInteraction.Collide))
            {
                var throwable = hit.GetComponentInParent<ChaosThrowable>();
                if (throwable == null || throwable.IsHeld) continue;

                throwable.PickUp(_hand);
                _held = throwable;
                return;
            }

            if (_weapon == null) _weapon = FindFirstObjectByType<ChaosWeapon>();
            if (_weapon != null && _weapon.IsEquipped) _weapon.Swing();
        }
    }
}
