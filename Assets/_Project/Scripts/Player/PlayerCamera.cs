using UnityEngine;
using UnityEngine.InputSystem;

namespace Caos.Player
{
    /// <summary>
    /// Cámara cenital estilo League of Legends: ángulo fijo (mirando hacia
    /// abajo) que sigue al personaje con suavizado. No gira con el ratón;
    /// el cursor queda libre para apuntar y hacer clic.
    /// La rueda del ratón acerca / aleja el personaje.
    /// pitch=55º y yaw=0º se conservan: de ellos depende el movimiento relativo.
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
        [SerializeField] float lookAhead = 0.7f;
        [SerializeField] float lookAheadSmoothTime = 0.22f;
        [SerializeField] float teleportSnapDistance = 8f;

        [Header("Game feel (solo visual)")]
        [SerializeField] float traumaDecay = 1.2f;
        [SerializeField] float maxOffset = 0.22f;
        [SerializeField] float maxRoll = 0.7f;        // legado: no se aplica al transform base
        [SerializeField] float shakeStrength = 0.8f;
        [SerializeField] bool reduceMotion;

        [Header("Encuadre")]
        [SerializeField] float baseFov = 56f;
        [SerializeField] float fovSpeedBonus = 3f;
        [SerializeField] float fovSpeedReference = 9f;
        [SerializeField] float fovSmoothTime = 0.18f;

        Transform target;
        Camera _camera;
        Vector3 _followPosition;
        Vector3 _smoothVelocity;
        Vector3 _lastTargetPosition;
        Vector3 _lookAheadOffset;
        Vector3 _lookAheadVelocity;
        float _planarSpeed;
        float _targetDistance;
        float _zoomVelocity;
        float _trauma;
        float _shakeTime;
        float _fovVelocity;

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

        public void SetTarget(Transform playerTarget)
        {
            target = playerTarget;
            if (target != null) _lastTargetPosition = target.position;
        }

        /// <summary>El trauma se suma y decae con tiempo real, también en pausa.</summary>
        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));
        }

        void Start()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null) _camera.fieldOfView = baseFov;

            if (target == null)
            {
                var controller = FindFirstObjectByType<PlayerController>();
                if (controller != null) SetTarget(controller.transform);
            }

            // Cámara fija: el cursor se queda visible (no hace falta candado).
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            reduceMotion = PlayerPrefs.GetInt("Caos.ReduceMotion", 0) == 1;
            _ = maxRoll; // parámetro conservado por compatibilidad; el roll se omite
                         // para no alterar la base de movimiento relativa a la cámara.

            _targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            _lookAheadOffset = Vector3.zero;
            _planarSpeed = 0f;
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
            // Tiempo real: suavizado, FOV y shake siguen vivos en hit-stop/pausa.
            float realDelta = Mathf.Max(0f, Time.unscaledDeltaTime);
            float step = Mathf.Min(realDelta, 0.1f);

            if (target != null)
            {
                Vector3 targetPosition = target.position;
                Vector3 planarDelta = targetPosition - _lastTargetPosition;

                if (planarDelta.magnitude > teleportSnapDistance)
                {
                    // Respawn/teleport: corte limpio, sin latigazo de seguimiento.
                    _smoothVelocity = Vector3.zero;
                    _lookAheadVelocity = Vector3.zero;
                    _lookAheadOffset = Vector3.zero;
                    _followPosition = targetPosition + Vector3.up * pivotHeight;
                }

                Vector3 planarVelocity = new Vector3(planarDelta.x, 0f, planarDelta.z)
                    / Mathf.Max(step, 0.0001f);
                _lastTargetPosition = targetPosition;
                _planarSpeed = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.001f, fovSpeedReference));

                Vector3 desiredLead = reduceMotion
                    ? Vector3.zero
                    : planarVelocity.normalized * (_planarSpeed * lookAhead);
                _lookAheadOffset = Vector3.SmoothDamp(_lookAheadOffset, desiredLead,
                    ref _lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, step);
            }

            distance = Mathf.SmoothDamp(distance, _targetDistance, ref _zoomVelocity,
                zoomSmoothTime, Mathf.Infinity, step);
            ApplyTransform(false, realDelta);
        }

        void ApplyTransform(bool snap, float realDelta = 0f)
        {
            if (target == null) return;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 desired = pivot - rotation * Vector3.forward * distance + _lookAheadOffset;

            if (snap)
            {
                _followPosition = desired;
                transform.position = desired;
                transform.rotation = rotation;
                ApplyFov(true, 0f);
                return;
            }

            float step = Mathf.Min(realDelta, 0.1f);
            // Se suaviza una posición lógica: el offset de shake NO realimenta el SmoothDamp.
            _followPosition = Vector3.SmoothDamp(_followPosition, desired, ref _smoothVelocity,
                followSmoothTime, Mathf.Infinity, step);

            transform.position = _followPosition + ShakeOffset(rotation, realDelta);
            // Sin roll: pitch/yaw exactos mantienen el movimiento relativo del jugador.
            transform.rotation = rotation;
            ApplyFov(false, step);
        }

        void ApplyFov(bool snap, float step)
        {
            if (_camera == null) return;
            float desired = baseFov + (reduceMotion ? 0f : fovSpeedBonus * _planarSpeed);
            _camera.fieldOfView = snap
                ? desired
                : Mathf.SmoothDamp(_camera.fieldOfView, desired, ref _fovVelocity,
                    fovSmoothTime, Mathf.Infinity, step);
        }

        Vector3 ShakeOffset(Quaternion rotation, float realDelta)
        {
            _trauma = Mathf.Max(0f, _trauma - traumaDecay * realDelta);
            if (_trauma <= 0f)
            {
                _shakeTime = 0f;
                return Vector3.zero;
            }

            float shake = _trauma * _trauma * shakeStrength * (reduceMotion ? 0.25f : 1f);
            _shakeTime += realDelta * 30f;

            // Senos a ritmos distintos: temblor continuo, nunca zumbido aleatorio.
            float x = 0.6f * Mathf.Sin(_shakeTime * 1.7f)
                + 0.4f * Mathf.Sin(_shakeTime * 3.1f);
            float y = 0.6f * Mathf.Sin(_shakeTime * 2.3f + 1.7f)
                + 0.4f * Mathf.Sin(_shakeTime * 4.3f);

            return (rotation * Vector3.right) * (maxOffset * shake * x)
                + (rotation * Vector3.up) * (maxOffset * shake * y);
        }
    }
}
