using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Navegación sencilla para monstruos y jefes: respeta paredes, cajas y
    /// plataformas mediante CharacterController, y usa la escalera para cambiar
    /// de piso en vez de atravesar la estructura.
    /// </summary>
    public static class ArenaNavigation
    {
        public const float UpperSurfaceY = 4.25f;
        public const float StairBottomZ = -11f;
        public const float StairTopZ = 7.2f;

        static readonly Vector3 StairBottom = new Vector3(0f, 0.95f, StairBottomZ);
        static readonly Vector3 StairTop = new Vector3(0f, UpperSurfaceY + 0.9f, StairTopZ);

        public static bool IsUpper(Vector3 point) => point.y > 2.8f;

        /// <summary>Zona de la escalera: ahí las plataformas son escalables.</summary>
        static bool NearStairLane(Vector3 point) =>
            Mathf.Abs(point.x) < 4.5f
            && point.z > StairBottomZ - 2f
            && point.z < StairTopZ + 3f;

        /// <summary>Punto al que debe ir la criatura para alcanzar al jugador.</summary>
        public static Vector3 Target(Vector3 from, Vector3 player)
        {
            bool fromUpper = IsUpper(from);
            bool playerUpper = IsUpper(player);
            bool onStairs = Mathf.Abs(from.x) < 4.5f
                && from.z > StairBottomZ - 2f && from.z < StairTopZ + 3f;

            // Mientras se está en la escalera se sigue por ella aunque la
            // altura ya parezca la del piso alto: evita saltar al vacío.
            if (onStairs) return playerUpper ? StairTop : StairBottom;
            if (fromUpper == playerUpper) return player;

            if (!fromUpper)
            {
                // Desde abajo: primero a la boca de la escalera, luego a subir.
                bool nearBottom = Mathf.Abs(from.x) < 4.5f && from.z < StairBottomZ + 3f;
                return nearBottom ? StairTop : StairBottom;
            }

            // Desde arriba: primero a la entrada de la escalera, luego a bajar.
            bool nearTop = Mathf.Abs(from.x) < 4.5f && from.z > StairTopZ - 3f;
            return nearTop ? StairBottom : StairTop;
        }

        /// <summary>Comprueba que no haya una pared o caja entre dos criaturas.</summary>
        public static bool HasLineOfSight(Transform from, Transform to)
        {
            Vector3 start = from.position + Vector3.up * 0.25f;
            Vector3 end = to.position + Vector3.up * 0.25f;

            if (!Physics.Linecast(start, end, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return true;

            Collider collider = hit.collider;
            if (collider == null) return true;
            if (collider.transform.IsChildOf(from) || collider.transform.IsChildOf(to)) return true;

            if (collider.GetComponentInParent<PlayerController>() != null ||
                collider.GetComponentInParent<ArenaMonster>() != null ||
                collider.GetComponentInParent<ArenaBoss>() != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mueve al enemigo con gravedad y evita obstáculos delante. Devuelve la
        /// dirección elegida para que la animación mire hacia donde camina.
        /// </summary>
        public static Vector3 Move(CharacterController controller, Transform self,
            Vector3 target, float speed, float radius, ref float verticalSpeed)
        {
            Vector3 flat = target - self.position;
            flat.y = 0f;

            Vector3 direction = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.zero;
            if (direction.sqrMagnitude > 0f) direction = Steer(self, direction, radius, 1.5f);

            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;

            Vector3 motion = direction * (speed * Time.deltaTime)
                + Vector3.up * (verticalSpeed * Time.deltaTime);
            controller.Move(motion);
            return direction;
        }

        /// <summary>Prueba varios ángulos hasta encontrar uno despejado.</summary>
        public static Vector3 Steer(Transform self, Vector3 desired, float radius, float probe)
        {
            Vector3 origin = self.position;
            float[] angles = { 0f, 30f, -30f, 62f, -62f, 100f, -100f, 180f };

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 candidate = Quaternion.Euler(0f, angles[i], 0f) * desired;
                if (Clear(origin, candidate, radius, probe, self)) return candidate;
            }

            return desired;
        }

        static bool Clear(Vector3 origin, Vector3 direction, float radius, float probe, Transform self)
        {
            if (!Physics.SphereCast(origin, radius, direction, out RaycastHit hit,
                    probe, ~0, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            Collider collider = hit.collider;
            if (collider == null || collider.transform.IsChildOf(self)) return true;

            if (collider.GetComponentInParent<PlayerController>() != null ||
                collider.GetComponentInParent<ArenaMonster>() != null ||
                collider.GetComponentInParent<ArenaBoss>() != null)
            {
                return true;
            }

            // En la boca de la escalera, peldaños, plataforma y barandillas
            // forman el mismo acceso: no los tratamos como obstáculo.
            if (NearStairLane(origin) &&
                (collider.name.StartsWith("Stair") ||
                 collider.name.StartsWith("UpperFloor") ||
                 collider.name.StartsWith("StairRail")))
            {
                return true;
            }

            if (collider.name.StartsWith("Stair")) return true;

            float feetHeight = origin.y - radius;
            return hit.point.y - feetHeight <= 0.5f;
        }
    }
}
