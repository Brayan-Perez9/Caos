using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Caos.Maps;
using Caos.Net;

namespace Caos.Dungeon
{
    /// <summary>
    /// Sincronización mínima de la mazmorra sobre la sala TCP existente.
    /// El host manda la sala y el resultado; los invitados envían su pose y los
    /// avisos de objetivo/derrota. Nunca duplica la lógica de CaosNetPeer.
    /// </summary>
    public class DungeonNetSync : MonoBehaviour
    {
        const float PoseInterval = 0.08f;

        readonly Dictionary<string, ChaosRemotePlayer> _remotes =
            new Dictionary<string, ChaosRemotePlayer>();

        DungeonDirector _director;
        CaosNetPeer _peer;
        float _nextPose;

        public bool IsHost => _peer != null && _peer.IsHost;
        public bool IsClient => _peer != null && _peer.IsClient;
        public bool Connected => _peer != null && _peer.IsConnected;
        public int RemoteCount => _remotes.Count;

        public void Configure(DungeonDirector director)
        {
            _director = director;

            _peer = CaosNetPeer.Instance != null ? CaosNetPeer.Instance : FindFirstObjectByType<CaosNetPeer>();
            if (_peer == null)
            {
                var go = new GameObject("CaosNetPeer");
                _peer = go.AddComponent<CaosNetPeer>();
            }

            _peer.MessageReceived -= OnMessage;
            _peer.MessageReceived += OnMessage;
        }

        void OnDestroy()
        {
            if (_peer != null) _peer.MessageReceived -= OnMessage;
        }

        public void Shutdown()
        {
            if (_peer == null) return;
            _peer.MessageReceived -= OnMessage;
            _peer.Shutdown();
        }

        void Update()
        {
            if (!Connected || _director == null || _director.Finished) return;
            if (_director.Player == null) return;
            if (Time.time < _nextPose) return;
            _nextPose = Time.time + PoseInterval;

            string name = string.IsNullOrWhiteSpace(CaosSession.LocalName)
                ? _peer.DisplayName : CaosSession.LocalName;
            Vector3 position = _director.Player.transform.position;
            string payload = CaosNetMessages.Join(name,
                F(position.x), F(position.y), F(position.z),
                F(_director.Player.transform.eulerAngles.y));
            _peer.Send(CaosMessageKind.DungeonPose, payload);
        }

        // ------------------------------------------------------------ eventos

        void OnMessage(CaosMessageKind kind, string payload, string from)
        {
            if (_director == null) return;

            switch (kind)
            {
                case CaosMessageKind.DungeonPose:
                    OnPose(payload);
                    break;
                case CaosMessageKind.DungeonRoom:
                    if (IsClient && int.TryParse(payload, out int room))
                        _director.NetworkRoom(room);
                    break;
                case CaosMessageKind.DungeonFinish:
                    if (IsClient) _director.NetworkFinish(payload == "1");
                    break;
                case CaosMessageKind.DungeonGoal:
                    if (IsHost && int.TryParse(payload, out int goal))
                        _director.OnRemoteGoal(goal);
                    break;
                case CaosMessageKind.DungeonDefeat:
                    if (IsHost) _director.OnRemoteDefeat();
                    break;
                case CaosMessageKind.PeerLeft:
                    RemoveRemote(from);
                    break;
            }
        }

        void OnPose(string payload)
        {
            string[] parts = CaosNetMessages.Split(payload);
            if (parts.Length < 5) return;

            string name = string.IsNullOrWhiteSpace(parts[0]) ? "Invitado" : parts[0];
            if (string.Equals(name, CaosSession.LocalName, System.StringComparison.OrdinalIgnoreCase)) return;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) ||
                !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float yaw))
            {
                return;
            }

            if (!_remotes.TryGetValue(name, out ChaosRemotePlayer remote))
            {
                remote = CreateRemote(name);
                _remotes[name] = remote;
            }
            remote.SetPose(new Vector3(x, y, z), yaw);

            // El host hace de repetidor para que todos vean a todos.
            if (IsHost) _peer.Broadcast(CaosMessageKind.DungeonPose, payload);
        }

        ChaosRemotePlayer CreateRemote(string name)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "DungeonRemote_" + name;
            go.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);

            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Color color = Color.HSVToRGB(
                (Mathf.Abs(name.GetHashCode()) % 360) / 360f, 0.55f, 0.95f);
            var remote = go.AddComponent<ChaosRemotePlayer>();
            remote.Configure(name, color);
            return remote;
        }

        void RemoveRemote(string name)
        {
            if (string.IsNullOrEmpty(name) || !_remotes.TryGetValue(name, out ChaosRemotePlayer remote)) return;
            _remotes.Remove(name);
            if (remote != null) Destroy(remote.gameObject);
        }

        // ------------------------------------------------------------ envíos

        public void SendGoal(int roomIndex)
        {
            if (IsClient) _peer?.Send(CaosMessageKind.DungeonGoal, roomIndex.ToString(CultureInfo.InvariantCulture));
        }

        public void SendDefeat(int roomIndex)
        {
            if (IsClient) _peer?.Send(CaosMessageKind.DungeonDefeat, roomIndex.ToString(CultureInfo.InvariantCulture));
        }

        public void NotifyRoom(int roomIndex)
        {
            if (IsHost) _peer?.Broadcast(CaosMessageKind.DungeonRoom, roomIndex.ToString(CultureInfo.InvariantCulture));
        }

        public void NotifyFinish(bool victory)
        {
            if (IsHost) _peer?.Broadcast(CaosMessageKind.DungeonFinish, victory ? "1" : "0");
        }

        static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
