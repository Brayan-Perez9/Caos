using UnityEngine;
using Caos.Player;
using Caos.Maps;

namespace Caos.Interactions
{
    /// <summary>
    /// Zona de caída: todo lo que entre (jugador) reaparece en el punto indicado.
    /// Añade un Rigidbody cinemático si no lo hay, porque los triggers necesitan
    /// uno para disparar eventos.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FallZone : MonoBehaviour
    {
        [SerializeField] Transform respawnPoint;

        public void SetRespawnPoint(Transform point) => respawnPoint = point;

        void Awake()
        {
            var body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();

            body.isKinematic = true;
            body.useGravity = false;
        }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            player.GetComponent<ChaosHealth>()?.TakeDamage(10f, "caída");
            player.Respawn(respawnPoint != null ? respawnPoint.position : player.GetSpawnPosition());
        }
    }
}
