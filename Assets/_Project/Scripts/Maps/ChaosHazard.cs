using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Barra giratoria o superficie peligrosa: empuja al jugador.</summary>
    public class ChaosHazard : MonoBehaviour
    {
        [SerializeField] Vector3 direction = Vector3.forward;
        [SerializeField] float strength = 10f;
        [SerializeField] float damage = 14f;

        ChaosExpressMission _mission;

        void Start() => _mission = FindFirstObjectByType<ChaosExpressMission>();

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player == null) return;

            player.AddImpulse(direction.normalized * strength + Vector3.up * 3f);
            player.GetComponent<ChaosHealth>()?.TakeDamage(damage, "barra giratoria");
            _mission?.ShowBanner("¡PUM!");
        }
    }
}
