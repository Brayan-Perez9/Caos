using System.Collections.Generic;
using UnityEngine;

namespace Caos.Player
{
    /// <summary>
    /// Empuje: un trigger delante del jugador que hace que los rígidos se muevan
    /// solos al caminar contra ellos, más un empujón fuerte al atacar.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerPusher : MonoBehaviour
    {
        [SerializeField] float pushAcceleration = 7f;
        [SerializeField] float shoveImpulse = 9f;
        [SerializeField] float referenceSpeed = 1.2f;

        readonly List<Collider> _targets = new List<Collider>();

        void OnTriggerEnter(Collider other)
        {
            if (!_targets.Contains(other)) _targets.Add(other);
        }

        void OnTriggerExit(Collider other)
        {
            _targets.Remove(other);
        }

        void OnTriggerStay(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null || body.isKinematic) return;

            float speedFactor = Mathf.Clamp01(body.linearVelocity.magnitude / referenceSpeed);
            float strength = pushAcceleration * (0.35f + 0.65f * speedFactor);
            body.AddForce(transform.forward * strength, ForceMode.Acceleration);
        }

        /// <summary>Empujón fuerte de todo lo que tenga delante.</summary>
        public void Shove()
        {
            Vector3 direction = transform.forward;

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                var collider = _targets[i];
                if (collider == null)
                {
                    _targets.RemoveAt(i);
                    continue;
                }

                var body = collider.attachedRigidbody;
                if (body == null || body.isKinematic) continue;

                body.AddForce(direction * shoveImpulse, ForceMode.Impulse);
            }
        }
    }
}
