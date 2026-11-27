using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Caos.Player
{
    /// <summary>
    /// Traduce el <see cref="InputActionAsset"/> del proyecto en propiedades simples.
    /// Un lector por jugador: más adelante se puede sustituir por una fuente de red
    /// sin tocar el resto del código.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        const string MapName = "Player";

        [SerializeField] InputActionAsset actions;

        InputAction _move;
        InputAction _look;
        InputAction _jump;
        InputAction _sprint;
        InputAction _attack;

        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
        public bool JumpPressed => _jump != null && _jump.WasPressedThisFrame();
        public bool SprintHeld => _sprint != null && _sprint.IsPressed();
        public bool AttackPressed => _attack != null && _attack.WasPressedThisFrame();

        /// <summary>
        /// Giro hacia delante. Se lee del teclado con Input System para no añadir
        /// una acción nueva al asset compartido por todas las escenas (el botón
        /// oeste del gamepad ya es salto).
        /// </summary>
        public bool RollPressed =>
            Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;

        public void SetActions(InputActionAsset asset)
        {
            if (actions == asset) return;

            if (_move != null) OnDisable();

            actions = asset;
            CacheActions();

            // En modo edición no se habilitan: solo al jugar (al cargar la escena,
            // Unity llama a Awake/OnEnable igualmente).
            if (Application.isPlaying && isActiveAndEnabled) OnEnable();
        }

        void Awake() => CacheActions();

        void OnEnable()
        {
            foreach (var action in All()) action?.Enable();
        }

        void OnDisable()
        {
            foreach (var action in All()) action?.Disable();
        }

        void CacheActions()
        {
            if (actions == null)
            {
                Debug.LogError("[Caos] PlayerInputReader sin InputActionAsset asignado.", this);
                return;
            }

            _move = Find("Move");
            _look = Find("Look");
            _jump = Find("Jump");
            _sprint = Find("Sprint");
            _attack = Find("Attack");
        }

        InputAction Find(string actionName)
        {
            var action = actions.FindAction($"{MapName}/{actionName}", false);
            if (action == null)
            {
                Debug.LogWarning($"[Caos] Acción '{MapName}/{actionName}' no existe en '{actions.name}'.", this);
            }

            return action;
        }

        IEnumerable<InputAction> All()
        {
            yield return _move;
            yield return _look;
            yield return _jump;
            yield return _sprint;
            yield return _attack;
        }
    }
}
