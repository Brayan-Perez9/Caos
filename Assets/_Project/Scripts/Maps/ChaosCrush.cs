using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Objeto de los eventos: hace daño si cae encima del jugador.</summary>
    public class ChaosCrush : MonoBehaviour
    {
        [SerializeField] float damage = 14f;

        public void Configure(float hitDamage) => damage = hitDamage;

        void OnCollisionEnter(Collision collision)
        {
            var player = collision.collider.GetComponentInParent<PlayerController>();
            if (player == null) return;

            player.GetComponent<ChaosHealth>()?.TakeDamage(damage, "objeto volador");
            player.AddImpulse(Vector3.up * 2f);
            Destroy(gameObject);
        }
    }
}
