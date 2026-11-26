using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Recoge paquetes o patitos secretos al entrar en su trigger.</summary>
    public class ChaosPickup : MonoBehaviour
    {
        [SerializeField] bool secret;

        ChaosExpressMission _mission;

        void Start() => _mission = FindFirstObjectByType<ChaosExpressMission>();

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") || _mission == null) return;

            if (secret) _mission.DuckFound();
            else _mission.ParcelCollected();

            gameObject.SetActive(false);
        }
    }
}
