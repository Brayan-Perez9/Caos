using UnityEngine;

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

        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public Vector3 Velocity => _velocity;
        public float HorizontalSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;

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

        public void SetCamera(Transform camera) => cameraReference = camera;
        public void SetSpawnPoint(Transform spawn) => spawnPoint = spawn;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            _pusher = GetComponent<PlayerPusher>();
        }

        void Update()
        {
            if (_input == null || _controller == null) return;

            if (InputEnabled && _input.JumpPressed) _lastJumpPressedTime = Time.time;
            if (_controller.isGrounded) _lastGroundedTime = Time.time;

            MoveHorizontally();
            ApplyGravityAndJump();

            if (InputEnabled && ShoveEnabled && _input.AttackPressed && _pusher != null)
                _pusher.Shove();

            if (transform.position.y < killY) Respawn(GetSpawnPosition());
        }

        void MoveHorizontally()
        {
            Transform cam = cameraReference != null
                ? cameraReference
                : (Camera.main != null ? Camera.main.transform : transform);

            Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;

            Vector2 move = InputEnabled ? _input.Move : Vector2.zero;
            Vector3 wish = camForward * move.y + camRight * move.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            float targetSpeed = (_input.SprintHeld ? sprintSpeed : walkSpeed) * SpeedMultiplier;
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

            if (canCoyote && jumpQueued)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity) * JumpMultiplier;
                _lastJumpPressedTime = float.NegativeInfinity;
                _lastGroundedTime = float.NegativeInfinity;
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        public Vector3 GetSpawnPosition()
        {
            return spawnPoint != null ? spawnPoint.position : new Vector3(0f, 1.5f, 0f);
        }

        public void Respawn() => Respawn(GetSpawnPosition());

        public void Respawn(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _velocity = Vector3.zero;
            _controller.enabled = true;
        }
    }
}
