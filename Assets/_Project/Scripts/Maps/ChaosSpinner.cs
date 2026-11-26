using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Hace girar la barra de una trampa.</summary>
    public class ChaosSpinner : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 110f;

        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
