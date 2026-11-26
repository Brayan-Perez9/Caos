using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Objeto que se puede coger con clic y lanzar contra todo.</summary>
    public class ChaosThrowable : MonoBehaviour
    {
        [SerializeField] float throwForce = 13f;

        Rigidbody _body;
        Transform _hand;
        bool _held;
        float _lastThrow;

        public bool IsHeld => _held;

        void Awake() => _body = GetComponent<Rigidbody>();

        Rigidbody Body()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            return _body;
        }

        public void PickUp(Transform hand)
        {
            if (_held) return;
            _held = true;
            _hand = hand;

            var body = Body();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;

            transform.SetParent(hand, true);
            transform.localPosition = new Vector3(0f, 0f, 0.35f);
            transform.localRotation = Quaternion.identity;
        }

        public void Throw()
        {
            if (!_held) return;
            _held = false;

            var body = Body();
            transform.SetParent(null, true);
            body.isKinematic = false;
            _lastThrow = Time.time;

            Vector3 forward = _hand != null ? _hand.forward : Vector3.forward;
            body.linearVelocity = forward * throwForce + Vector3.up * 4.5f;
            body.angularVelocity = new Vector3(
                Random.Range(-8f, 8f), Random.Range(-8f, 8f), Random.Range(-8f, 8f));
            _hand = null;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_held || Time.time - _lastThrow < 0.12f) return;
            if (_body == null || _body.linearVelocity.magnitude < 5f) return;

            var target = collision.collider.GetComponentInParent<ChaosTarget>();
            if (target != null)
            {
                target.TakeHit();
                return;
            }

            var monster = collision.collider.GetComponentInParent<ChaosMonster>();
            if (monster != null)
            {
                monster.Kill();
                return;
            }

            var boss = collision.collider.GetComponentInParent<ChaosBoss>();
            if (boss != null)
                FindFirstObjectByType<ChaosExpressMission>()?.ApplyBossDamage(25f);
        }
    }
}
