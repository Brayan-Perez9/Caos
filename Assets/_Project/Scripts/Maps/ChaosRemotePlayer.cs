using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Representación visual de otro jugador conectado a la sala.</summary>
    public class ChaosRemotePlayer : MonoBehaviour
    {
        Vector3 _target;
        float _targetYaw;

        public string PlayerName { get; private set; } = "Jugador";

        public void Configure(string name, Color color)
        {
            PlayerName = name;
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.material.color = color;
        }

        public void SetPose(Vector3 position, float yaw)
        {
            _target = position;
            _targetYaw = yaw;
        }

        void Update()
        {
            transform.position = Vector3.Lerp(
                transform.position, _target, 1f - Mathf.Exp(-16f * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.Euler(0f, _targetYaw, 0f),
                1f - Mathf.Exp(-16f * Time.deltaTime));
        }
    }
}
