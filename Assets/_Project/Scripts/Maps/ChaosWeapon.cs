using UnityEngine;
using Caos.Net;
using Caos.Player;

namespace Caos.Maps
{
    /// <summary>Martillo: se recoge en el arco y se usa con clic izquierdo.</summary>
    public class ChaosWeapon : MonoBehaviour
    {
        [SerializeField] float range = 2.8f;
        [SerializeField] float damage = 30f;
        [SerializeField] float cooldown = 0.4f;

        ChaosExpressMission _mission;
        PlayerController _player;
        Transform _hold;
        bool _equipped;
        float _nextHit;
        float _swing;
        CaosNetPlayers _netPlayers;

        public bool IsEquipped => _equipped;

        public void Equip(PlayerController player)
        {
            if (_equipped) return;
            _equipped = true;
            _player = player;
            _mission = FindFirstObjectByType<ChaosExpressMission>();

            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            _hold = new GameObject("HammerHold").transform;
            _hold.SetParent(player.transform, false);
            _hold.localPosition = new Vector3(0.55f, 1.05f, 0.65f);

            _mission?.WeaponPickedUp();
        }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null) Equip(player);
        }

        void Update()
        {
            if (!_equipped || _player == null) return;

            transform.position = _hold.position;
            transform.rotation = _hold.rotation * Quaternion.Euler(0f, 0f, _swing);

            if (_swing > 0f)
                _swing = Mathf.MoveTowards(_swing, 0f, 420f * Time.deltaTime);
        }

        public void Swing()
        {
            if (!_equipped || Time.time < _nextHit) return;
            _nextHit = Time.time + cooldown;
            _swing = 55f;
            ChaosFx.Burst(transform.position, new Color(1f, 0.9f, 0.4f), 4);

            if (_netPlayers == null) _netPlayers = FindFirstObjectByType<CaosNetPlayers>();
            _netPlayers?.NotifyMelee(_player.transform.position, _player.transform.eulerAngles.y);

            var boss = FindFirstObjectByType<ChaosBoss>();
            if (boss != null &&
                Vector3.Distance(boss.transform.position, _player.transform.position) <= range + 1f)
            {
                if (_mission == null) _mission = FindFirstObjectByType<ChaosExpressMission>();
                _mission?.ApplyBossDamage(damage);
                _player.AddImpulse(-_player.transform.forward * 1.5f);
                return;
            }

            foreach (var monster in FindObjectsByType<ChaosMonster>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(monster.transform.position, _player.transform.position) <= range)
                    monster.Kill();
            }

            foreach (var target in FindObjectsByType<ChaosTarget>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(target.transform.position, _player.transform.position) <= range + 1f)
                    target.TakeHit();
            }
        }
    }
}
