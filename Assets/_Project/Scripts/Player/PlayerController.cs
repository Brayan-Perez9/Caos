using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Maps;

namespace Caos.Player
{
    /// <summary>
    /// Movimiento en tercera persona con CharacterController: desplazamiento
    /// relativo a la cámara, salto con coyote time y buffer, esprint y reaparición.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] float walkSpeed = 4.4f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField] float acceleration = 22f;
        [SerializeField] float rotationSpeed = 14f;

        [Header("Salto")]
        [SerializeField] float jumpHeight = 1.15f;
        [SerializeField] float gravity = -26f;
        [SerializeField] float coyoteTime = 0.15f;
        [SerializeField] float jumpBufferTime = 0.15f;

        [Header("Giro hacia delante")]
        [SerializeField] float rollDuration = 0.58f;
        [SerializeField] float rollSpeed = 9.5f;
        [SerializeField] float rollCooldown = 0.85f;

        [Header("Cámara y reaparición")]
        [SerializeField] Transform cameraReference;
        [SerializeField] Transform spawnPoint;
        [SerializeField] float killY = -15f;

        CharacterController _controller;
        PlayerInputReader _input;
        PlayerPusher _pusher;

        Vector3 _velocity;
        float _lastGroundedTime = float.NegativeInfinity;
        float _lastJumpPressedTime = float.NegativeInfinity;
        bool _wasGrounded;

        float _rollTime;
        float _rollCooldown;
        Vector3 _rollDirection;

        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public Vector3 Velocity => _velocity;
        public float HorizontalSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;

        /// <summary>Estado del giro procedural que lee el atuendo para animar.</summary>
        public bool IsRolling => _rollTime > 0f;
        public float RollPhase => rollDuration <= 0f
            ? 0f : Mathf.Clamp01(1f - _rollTime / rollDuration);
        public Vector3 RollDirection => _rollDirection;

        /// <summary>Modificadores temporales que usan los eventos de las misiones.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public float JumpMultiplier { get; set; } = 1f;

        /// <summary>Impulso externo (resortes, trampas, empujones de escenario).</summary>
        public void AddImpulse(Vector3 impulse) => _velocity += impulse;

        /// <summary>
        /// Cuando es false el jugador no se mueve ni ataca (la gravedad sigue
        /// activa). Lo usa la pantalla de personalización.
        /// </summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>El mapa de misión desactiva el empujón de clic para usarlo con las manos.</summary>
        public bool ShoveEnabled { get; set; } = true;

        /// <summary>La arena desactiva esta rotación para que el cursor mande la mirada.</summary>
        public bool FaceMovement { get; set; } = true;

        /// <summary>Límite de caída que impone el paracaídas (negativo).</summary>
        public float FallSpeedLimit { get; set; } = float.NegativeInfinity;

        /// <summary>Control horizontal mientras se desciende con paracaídas.</summary>
        public float AirControlMultiplier { get; set; } = 1f;

        public void SetCamera(Transform camera) => cameraReference = camera;
        public void SetSpawnPoint(Transform spawn) => spawnPoint = spawn;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            _pusher = GetComponent<PlayerPusher>();

            // Se añade solo una vez: funciona en todas las escenas sin tener
            // que regenerarlas ni depender de un objeto colocado a mano.
            if (GetComponent<PlayerParachute>() == null)
                gameObject.AddComponent<PlayerParachute>();
        }

        void Update()
        {
            if (_input == null || _controller == null) return;

            if (InputEnabled && _input.JumpPressed) _lastJumpPressedTime = Time.time;
            if (_controller.isGrounded) _lastGroundedTime = Time.time;

            if (_rollCooldown > 0f) _rollCooldown -= Time.deltaTime;
            if (InputEnabled && _input.RollPressed) TryStartRoll();

            // Flanco de aterrizaje: feedback solo al tocar suelo, no cada frame.
            if (_controller.isGrounded && !_wasGrounded && _velocity.y < -1f)
                ArenaJuice.Active?.Land(transform.position, -_velocity.y);
            _wasGrounded = _controller.isGrounded;

            MoveHorizontally();
            if (_rollTime > 0f) _rollTime = Mathf.Max(0f, _rollTime - Time.deltaTime);
            ApplyGravityAndJump();

            if (InputEnabled && ShoveEnabled && _input.AttackPressed && _pusher != null)
                _pusher.Shove();

            if (transform.position.y < killY) Respawn(GetSpawnPosition());
        }

        /// <summary>Dirección deseada en el plano horizontal, relativa a la cámara.</summary>
        Vector3 GetWishDirection()
        {
            Transform cam = cameraReference != null
                ? cameraReference
                : (Camera.main != null ? Camera.main.transform : transform);

            Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            if (camForward.sqrMagnitude < 0.001f) camForward = transform.forward;
            if (camRight.sqrMagnitude < 0.001f) camRight = transform.right;

            Vector2 move = InputEnabled ? _input.Move : Vector2.zero;
            Vector3 wish = camForward * move.y + camRight * move.x;
            wish.y = 0f;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            return wish;
        }

        /// <summary>
        /// Dirección hacia el cursor, en el plano del suelo. La usa el giro con Q
        /// para que la volterada vaya a donde mira el jugador, no hacia donde
        /// esté caminando. Input System: nunca UnityEngine.Input.
        /// </summary>
        Vector3 GetAimDirection(Vector3 fallback)
        {
            Camera cam = cameraReference != null
                ? cameraReference.GetComponent<Camera>()
                : Camera.main;

            if (cam != null && Mouse.current != null)
            {
                Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                var plane = new Plane(Vector3.up, transform.position + Vector3.up * 0.95f);
                if (plane.Raycast(ray, out float distance))
                {
                    Vector3 toCursor = ray.GetPoint(distance) - transform.position;
                    toCursor.y = 0f;
                    if (toCursor.sqrMagnitude > 0.04f) return toCursor.normalized;
                }
            }

            // En la arena/dungeon el puntero ya calcula un punto de mira compartido.
            ArenaAimController aim = FindFirstObjectByType<ArenaAimController>();
            if (aim != null)
            {
                Vector3 toAim = aim.AimPoint - transform.position;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.04f) return toAim.normalized;
            }

            return fallback;
        }

        void TryStartRoll()
        {
            if (_rollTime > 0f || _rollCooldown > 0f || !_controller.isGrounded) return;

            Vector3 wish = GetWishDirection();
            if (wish.sqrMagnitude < 0.001f) wish = transform.forward;
            _rollDirection = GetAimDirection(wish.normalized);
            _rollTime = rollDuration;
            _rollCooldown = rollCooldown;

            transform.rotation = Quaternion.LookRotation(_rollDirection, Vector3.up);
            Debug.Log($"[Caos] Giro hacia el cursor con Q: dir=({_rollDirection.x:F2}, {_rollDirection.z:F2})");
        }

        void MoveHorizontally()
        {
            if (_rollTime > 0f)
            {
                // Arco de velocidad: arranca y termina suave, pero recorre más
                // suelo que una caminada normal. La rotación la manda el giro.
                float speed = rollSpeed * Mathf.Sin(Mathf.PI * RollPhase);
                _velocity.x = _rollDirection.x * speed;
                _velocity.z = _rollDirection.z * speed;

                Quaternion rollRotation = Quaternion.LookRotation(_rollDirection, Vector3.up);
                float rollBlend = 1f - Mathf.Exp(-24f * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, rollRotation, rollBlend);
                return;
            }

            Vector3 wish = GetWishDirection();
            float airControl = _controller.isGrounded ? 1f : AirControlMultiplier;
            float targetSpeed = (_input.SprintHeld ? sprintSpeed : walkSpeed)
                * SpeedMultiplier * airControl;
            Vector3 targetVelocity = wish * targetSpeed;

            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, targetVelocity, acceleration * Time.deltaTime);
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;

            if (FaceMovement && wish.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(wish, Vector3.up);
                float blend = 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
            }
        }

        void ApplyGravityAndJump()
        {
            if (_controller.isGrounded && _velocity.y < 0f) _velocity.y = -2f;

            bool canCoyote = Time.time - _lastGroundedTime <= coyoteTime;
            bool jumpQueued = Time.time - _lastJumpPressedTime <= jumpBufferTime;

            if (!IsRolling && canCoyote && jumpQueued)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity) * JumpMultiplier;
                _lastJumpPressedTime = float.NegativeInfinity;
                _lastGroundedTime = float.NegativeInfinity;
            }

            _velocity.y += gravity * Time.deltaTime;
            if (!_controller.isGrounded && _velocity.y < FallSpeedLimit)
                _velocity.y = FallSpeedLimit;
            _controller.Move(_velocity * Time.deltaTime);
        }

        public Vector3 GetSpawnPosition()
        {
            return spawnPoint != null ? spawnPoint.position : new Vector3(0f, 1.5f, 0f);
        }

        public void Respawn() => Respawn(GetSpawnPosition());

        public void Respawn(Vector3 position)
        {
            GetComponent<PlayerParachute>()?.Cancel();

            _controller.enabled = false;
            transform.position = position;
            _velocity = Vector3.zero;
            _rollTime = 0f;
            _rollCooldown = 0f;
            _controller.enabled = true;
        }
    }
}
