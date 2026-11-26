using UnityEngine;
using UnityEngine.InputSystem;

namespace Caos.Player
{
    /// <summary>
    /// Cámara cenital estilo League of Legends: ángulo fijo (mirando hacia
    /// abajo) que sigue al personaje con suavizado. No gira con el ratón;
    /// el cursor queda libre para apuntar y hacer clic.
    /// La rueda del ratón acerca / aleja el personaje.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerCamera : MonoBehaviour
    {
        [Header("Ángulo (estilo LoL)")]
        [SerializeField] float pitch = 55f;           // positivo = mira hacia abajo
        [SerializeField] float yaw = 0f;              // 0 = W mueve "hacia arriba" en pantalla
        [SerializeField] float distance = 15f;
        [SerializeField] float pivotHeight = 1.4f;

        [Header("Zoom (rueda del ratón)")]
        [SerializeField] float minDistance = 6f;
        [SerializeField] float maxDistance = 24f;
        [SerializeField] float zoomSpeed = 2f;
        [SerializeField] float zoomSmoothTime = 0.12f;

        [Header("Seguimiento")]
        [SerializeField] float followSmoothTime = 0.12f;

        Transform target;
        Vector3 _smoothVelocity;
        float _targetDistance;
        float _zoomVelocity;

        public float Distance => distance;

        public void SetTarget(Transform playerTarget) => target = playerTarget;

        void Start()
        {
            if (target == null)
            {
                var controller = FindFirstObjectByType<PlayerController>();
                if (controller != null) SetTarget(controller.transform);
            }

            // Cámara fija: el cursor se queda visible (no hace falta candado).
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _targetDistance = distance;
            ApplyTransform(true);
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            // Rueda arriba => acercar (menos distancia).
            _targetDistance = Mathf.Clamp(
                _targetDistance - scroll * 0.01f * zoomSpeed,
                minDistance, maxDistance);
        }

        void LateUpdate()
        {
            distance = Mathf.SmoothDamp(distance, _targetDistance, ref _zoomVelocity,
                zoomSmoothTime);
            ApplyTransform(false);
        }

        void ApplyTransform(bool snap)
        {
            if (target == null) return;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 desired = pivot - rotation * Vector3.forward * distance;

            transform.rotation = rotation;
            transform.position = snap
                ? desired
                : Vector3.SmoothDamp(transform.position, desired, ref _smoothVelocity, followSmoothTime);
        }
    }
}
