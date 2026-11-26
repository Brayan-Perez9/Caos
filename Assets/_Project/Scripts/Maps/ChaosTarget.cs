using UnityEngine;

namespace Caos.Maps
{
    /// <summary>Diana flotante: se tumba al lanzarle algo, golpearla o moverla lejos.</summary>
    public class ChaosTarget : MonoBehaviour
    {
        Vector3 _origin;
        ChaosExpressMission _mission;
        bool _done;

        void Start()
        {
            _origin = transform.position;
            _mission = FindFirstObjectByType<ChaosExpressMission>();
        }

        void Update()
        {
            if (_done || Vector3.Distance(transform.position, _origin) < 1.5f) return;
            TakeHit();
        }

        public void TakeHit()
        {
            if (_done) return;
            _done = true;

            if (_mission == null) _mission = FindFirstObjectByType<ChaosExpressMission>();
            _mission?.TargetDown();
            ChaosFx.Burst(transform.position, new Color(1f, 0.86f, 0.35f), 6);
            Destroy(gameObject);
        }
    }
}
