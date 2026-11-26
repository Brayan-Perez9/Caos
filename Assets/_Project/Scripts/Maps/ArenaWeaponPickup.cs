using UnityEngine;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>
    /// Arma apoyada en un pedestal. Su color indica el elemento: fuego, aire,
    /// hielo, rayo o veneno. Se equipa al acercarse.
    /// </summary>
    public class ArenaWeaponPickup : MonoBehaviour
    {
        [SerializeField] ArenaWeaponKind kind = ArenaWeaponKind.Pistola;
        [SerializeField] ArenaElement element = ArenaElement.Normal;

        Vector3 _origin;
        float _phase;

        public ArenaWeaponKind Kind => kind;
        public ArenaElement Element => element;

        public void Configure(ArenaWeaponKind weaponKind, ArenaElement weaponElement)
        {
            kind = weaponKind;
            element = weaponElement;
            ApplyColor();
        }

        void Start()
        {
            _origin = transform.position;
            _phase = Random.Range(0f, 6.28f);
            ApplyColor();

            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        void ApplyColor()
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = ArenaMaterial.Create(
                    ArenaElementInfo.Color(element), 0f, 0.55f);
        }

        void Update()
        {
            _phase += Time.deltaTime * 2.6f;
            transform.position = _origin + Vector3.up * (0.12f * Mathf.Sin(_phase));
            transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            FindFirstObjectByType<ArenaDirector>()?.EquipWeapon(this, kind, element);
        }
    }
}
