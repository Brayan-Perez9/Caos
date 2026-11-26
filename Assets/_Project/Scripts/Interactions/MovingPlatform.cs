using System.Collections.Generic;
using UnityEngine;

namespace Caos.Interactions
{
    /// <summary>
    /// Plataforma que va y viene entre dos puntos locales. Lleva a los jugadores
    /// que estén encima moviéndolos con CharacterController.Move.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class MovingPlatform : MonoBehaviour
    {
        [SerializeField] Vector3 offsetA = Vector3.zero;
        [SerializeField] Vector3 offsetB = new Vector3(6f, 0f, 0f);
        [SerializeField] float speed = 1.4f;

        readonly List<CharacterController> _riders = new List<CharacterController>();

        Vector3 _origin;
        Vector3 _delta;

        public void Configure(Vector3 a, Vector3 b, float moveSpeed)
        {
            offsetA = a;
            offsetB = b;
            speed = moveSpeed;
        }

        void Awake()
        {
            _origin = transform.position;

            var body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();

            body.isKinematic = true;
            body.useGravity = false;

            EnsureRiderZone();
        }

        /// <summary>
        /// La plataforma sólida no puede ser trigger (el jugador se caería),
        /// así que un hijo trigger detecta quién se sube y se lo pasa.
        /// </summary>
        void EnsureRiderZone()
        {
            var zone = transform.Find("RiderZone");
            if (zone == null)
            {
                var zoneObject = new GameObject("RiderZone");
                zoneObject.transform.SetParent(transform, false);
                var collider = zoneObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.center = new Vector3(0f, 2.5f, 0f);
                collider.size = new Vector3(1.3f, 6f, 1.3f);
                zone = zoneObject.transform;
            }

            var forwarder = zone.GetComponent<RiderZoneForwarder>();
            if (forwarder == null)
            {
                forwarder = zone.gameObject.AddComponent<RiderZoneForwarder>();
                forwarder.Configure(this);
            }
        }

        public void AddRider(CharacterController rider)
        {
            if (rider != null && !_riders.Contains(rider)) _riders.Add(rider);
        }

        public void RemoveRider(CharacterController rider)
        {
            if (rider != null) _riders.Remove(rider);
        }

        sealed class RiderZoneForwarder : MonoBehaviour
        {
            MovingPlatform _platform;

            public void Configure(MovingPlatform platform) => _platform = platform;

            void OnTriggerEnter(Collider other)
            {
                var rider = other.GetComponent<CharacterController>();
                if (rider != null) _platform.AddRider(rider);
            }

            void OnTriggerExit(Collider other)
            {
                var rider = other.GetComponent<CharacterController>();
                if (rider != null) _platform.RemoveRider(rider);
            }
        }

        void Update()
        {
            float t = Mathf.PingPong(Time.time * speed, 1f);
            t = Mathf.SmoothStep(0f, 1f, t);

            Vector3 next = Vector3.Lerp(_origin + offsetA, _origin + offsetB, t);
            _delta = next - transform.position;
            transform.position = next;
        }

        void LateUpdate()
        {
            if (_delta == Vector3.zero) return;

            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                var rider = _riders[i];
                if (rider == null)
                {
                    _riders.RemoveAt(i);
                    continue;
                }

                rider.Move(_delta);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            var rider = other.GetComponent<CharacterController>();
            if (rider != null && !_riders.Contains(rider)) _riders.Add(rider);
        }

        void OnTriggerExit(Collider other)
        {
            var rider = other.GetComponent<CharacterController>();
            if (rider != null) _riders.Remove(rider);
        }
    }
}
