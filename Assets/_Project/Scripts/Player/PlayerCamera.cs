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

        [Header("Game feel (solo visual)")]
        [SerializeField] float traumaDecay = 1.2f;
        [SerializeField] float maxOffset = 0.22f;
        [SerializeField] float maxRoll = 0.7f;
        [SerializeField] float shakeStrength = 0.8f;
        [SerializeField] bool reduceMotion;

        Transform target;
        Vector3 _smoothVelocity;
        float _targetDistance;
        float _zoomVelocity;
        float _trauma;
        float _shakeTime;

        public float Distance => distance;

        /// <summary>Sacudida opcional para quien quiera menos movimiento de cámara.</summary>
        public bool ReduceMotion
        {
            get => reduceMotion;
            set => reduceMotion = value;
        }

        public void ToggleReduceMotion()
        {
            reduceMotion = !reduceMotion;
            PlayerPrefs.SetInt("Caos.ReduceMotion", reduceMotion ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetTarget(Transform playerTarget) => target = playerTarget;

        /// <summary>El trauma se suma y decae con tiempo real, también en pausa.</summary>
        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));
        }

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

            reduceMotion = PlayerPrefs.GetInt("Caos.ReduceMotion", 0) == 1;

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

            if (snap)
            {
                transform.rotation = rotation;
                transform.position = desired;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired,
                ref _smoothVelocity, followSmoothTime);
            transform.rotation = rotation;

            // Sacudida con trauma^2 y senos a ritmos distintos: nunca azúcar
            // aleatorio por frame. Usa tiempo real para no quedarse torcida.
            float realDelta = Time.unscaledDeltaTime;
            _trauma = Mathf.Max(0f, _trauma - traumaDecay * realDelta);
            if (_trauma <= 0f)
            {
                _shakeTime = 0f;
                return;
            }

            float shake = _trauma * _trauma * shakeStrength
                * (reduceMotion ? 0.3f : 1f);
            _shakeTime += realDelta * 30f;

            Vector3 offset = transform.right * (maxOffset * shake * Mathf.Sin(_shakeTime * 1.7f))
                + transform.up * (maxOffset * shake * Mathf.Sin(_shakeTime * 2.3f));
            float roll = reduceMotion ? 0f : maxRoll * shake * Mathf.Sin(_shakeTime * 1.1f);

            transform.position += offset;
            transform.rotation = rotation * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
