using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Botón final: solo cuenta cuando los tres pedidos están listos.</summary>
    public class ChaosFinish : MonoBehaviour
    {
        ChaosExpressMission _mission;

        void Start() => _mission = FindFirstObjectByType<ChaosExpressMission>();

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null) _mission?.ReachFinish(player);
        }
    }
}
