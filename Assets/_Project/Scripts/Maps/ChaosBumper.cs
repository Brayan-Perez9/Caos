using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Saltarín: lanza al jugador hacia arriba.</summary>
    public class ChaosBumper : MonoBehaviour
    {
        [SerializeField] float strength = 13f;

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null)
                player.AddImpulse(Vector3.up * strength + transform.forward * 2f);
        }
    }
}
