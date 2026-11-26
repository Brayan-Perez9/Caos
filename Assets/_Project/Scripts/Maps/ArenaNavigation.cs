using System.Collections.Generic;
using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Navegación sencilla para monstruos y jefes: respeta paredes, cajas y
    /// plataformas mediante CharacterController y usa la escalera completa
    /// (boca, peldaños y meseta) para cambiar de piso sin quedarse parado.
    /// </summary>
    public static class ArenaNavigation
    {
        public const float UpperSurfaceY = 4.25f;
        public const float StairBottomZ = -11f;
        public const float StairTopZ = 7.2f;

        const float LandingZ = 9.4f;

        static readonly Vector3 StairBottom = new Vector3(0f, 1f, StairBottomZ - 1.6f);
        static readonly Vector3 StairTop = new Vector3(0f, UpperSurfaceY + 0.9f, LandingZ);

        static readonly Dictionary<Transform, float> StuckTimers =
            new Dictionary<Transform, float>();

        public static bool IsUpper(Vector3 point) => point.y > 2.8f;

        /// <summary>Comprueba que dos criaturas estén en el mismo nivel del mapa.</summary>
        public static bool SameFloor(Vector3 a, Vector3 b, float tolerance = 2f) =>
            Mathf.Abs(a.y - b.y) <= tolerance;

        /// <summary>Zona continua de la escalera, incluida la meseta superior.</summary>
        static bool InStairLane(Vector3 point) =>
            Mathf.Abs(point.x) < 4.6f
            && point.z > StairBottomZ - 4f
            && point.z < LandingZ + 3f;

        /// <summary>Punto al que debe ir la criatura para alcanzar al jugador.</summary>
        public static Vector3 Target(Vector3 from, Vector3 player)
        {
            bool playerUpper = IsUpper(player);
            bool selfUpper = IsUpper(from);
            bool lane = InStairLane(from);

            // El centro del CharacterController está ~0.9 m por encima del suelo.
            bool onSteps = lane && from.y > 1f && from.y < UpperSurfaceY + 1f;
            bool atUpperLanding = lane && from.y >= UpperSurfaceY + 0.35f
                && from.z >= LandingZ - 1.2f;
            bool atLowerMouth = lane && from.y <= 1.15f
                && from.z <= StairBottomZ + 2.5f;

            if (!selfUpper && !playerUpper)
            {
                // Bajando la escalera con el jugador abajo: no volver a subir.
                if (onSteps) return StairBottom;
                return player;
            }

            if (selfUpper && playerUpper)
            {
                // Ya en la meseta: seguir hacia el jugador en vez de pararse en
                // el waypoint. Es lo que antes los dejaba quietos en el último peldaño.
                if (atUpperLanding) return player;
                if (onSteps && from.z < LandingZ + 1f) return StairTop;
                return player;
            }

            if (!selfUpper && playerUpper)
            {
                if (atUpperLanding) return player;
                if (onSteps || (lane && from.z <= StairBottomZ + 3f)) return StairTop;
                return StairBottom;
            }

            // En el piso alto pero el jugador está abajo: buscar la boca de la
            // escalera y bajar por ella.
            if (atLowerMouth) return player;
            if (lane && from.z <= LandingZ + 1.5f) return StairBottom;
            return StairTop;
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

            Vector3 before = self.position;
            Vector3 motion = direction * (speed * Time.deltaTime)
                + Vector3.up * (verticalSpeed * Time.deltaTime);
            controller.Move(motion);

            if (direction.sqrMagnitude > 0f)
                ApplyUnstick(controller, self, before, direction, speed, radius);

            return direction;
        }

        /// <summary>Prueba varios ángulos hasta encontrar uno despejado.</summary>
        public static Vector3 Steer(Transform self, Vector3 desired, float radius, float probe)
        {
            Vector3 origin = self.position;
            float[] angles = InStairLane(origin)
                ? new[] { 0f, 10f, -10f, 22f, -22f, 35f, -35f }
                : new[] { 0f, 30f, -30f, 62f, -62f, 100f, -100f, 180f };

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 candidate = Quaternion.Euler(0f, angles[i], 0f) * desired;
                if (Clear(origin, candidate, radius, probe, self)) return candidate;
            }

            return desired;
        }

        /// <summary>
        /// Si el CharacterController lleva tiempo sin avanzar, da un pequeño
        /// empujón lateral hacia el centro de la escalera (o alterno en campo
        /// abierto) para despegarlo de un canto.
        /// </summary>
        static void ApplyUnstick(CharacterController controller, Transform self,
            Vector3 before, Vector3 direction, float speed, float radius)
        {
            StuckTimers.TryGetValue(self, out float timer);

            float moved = Vector3.ProjectOnPlane(self.position - before, Vector3.up).magnitude;
            float expected = speed * Time.deltaTime;

            if (controller.isGrounded && moved < expected * 0.15f)
                timer += Time.deltaTime;
            else
                timer = Mathf.Max(0f, timer - Time.deltaTime * 2f);

            if (timer < 0.45f)
            {
                StuckTimers[self] = timer;
                return;
            }

            Vector3 correction;
            if (InStairLane(self.position))
            {
                float towardCenter = self.position.x >= 0f ? -1f : 1f;
                correction = Vector3.right * towardCenter * radius * 2.2f
                    + direction * 0.35f;
            }
            else
            {
                float side = Time.frameCount % 120 < 60 ? 1f : -1f;
                correction = Vector3.Cross(Vector3.up, direction) * side * radius * 2f
                    + direction * 0.4f;
            }

            controller.Move(correction * Time.deltaTime * 3f + Vector3.up * 0.16f);
            StuckTimers[self] = 0.2f;
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

            // En la boca de la escalera, peldaños, plataforma, meseta y
            // barandillas forman el mismo acceso: no los tratamos como obstáculo.
            if (InStairLane(origin) &&
                (collider.name.StartsWith("Stair") ||
                 collider.name.StartsWith("UpperFloor") ||
                 collider.name.StartsWith("UpperBridge") ||
                 collider.name.StartsWith("StairLanding")))
            {
                return true;
            }

            if (collider.name.StartsWith("Stair")) return true;

            float feetHeight = origin.y - radius;
            return hit.point.y - feetHeight <= 0.5f;
        }
    }
}
