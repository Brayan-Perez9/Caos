using UnityEngine;
using UnityEngine.InputSystem;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Apunta el personaje hacia el cursor y dispara con el botón izquierdo.
    /// Usa el Input System y el plano de la altura del jugador, así que funciona
    /// tanto en el suelo bajo como en la plataforma alta.
    /// </summary>
    public class ArenaAimController : MonoBehaviour
    {
        [SerializeField] float aimHeight = 0.95f;

        IArenaCombatDirector _director;
        PlayerController _controller;
        Camera _camera;
        Vector3 _aimPoint;

        public Vector3 AimPoint => _aimPoint;

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
            if (_controller != null) _controller.FaceMovement = false;
            _director = FindFirstObjectByType<ArenaDirector>();
            _camera = Camera.main;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>La mazmorra inyecta su puente de combate sin tocar la arena.</summary>
        public void Configure(IArenaCombatDirector director)
        {
            _director = director;
            _controller = GetComponent<PlayerController>();
            if (_controller != null) _controller.FaceMovement = false;
        }

        void Update()
        {
            if (_director == null) _director = FindFirstObjectByType<ArenaDirector>();
            if (_camera == null) _camera = Camera.main;
            if (_controller == null || _camera == null || _director == null) return;
            if (!_controller.InputEnabled || _director.UpgradePending || _director.Finished) return;

            Vector2 pointer = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Ray ray = _camera.ScreenPointToRay(pointer);
            var plane = new Plane(Vector3.up, transform.position + Vector3.up * aimHeight);
            if (plane.Raycast(ray, out float distance)) _aimPoint = ray.GetPoint(distance);

            // Durante el giro con Q manda la dirección del roll, que ya apunta
            // al cursor; el apuntado normal se reanuda al terminar.
            if (!_controller.IsRolling)
            {
                Vector3 toAim = _aimPoint - transform.position;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.04f)
                {
                    Quaternion target = Quaternion.LookRotation(toAim.normalized, Vector3.up);
                    float blend = 1f - Mathf.Exp(-18f * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, target, blend);
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                _director.TryFire(transform, _aimPoint);
        }
    }
}
