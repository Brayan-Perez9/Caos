using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Caos.Maps;
using Caos.Player;

namespace Caos.Net
{
    /// <summary>
    /// Presencia de los jugadores: envía la pose del local, crea avatares de los
    /// demás y replica disparos y golpes cuerpo a cuerpo. En la sala muestra a
    /// todos en pedestales; en el mapa sigue al PlayerController real.
    /// Los proyectiles remotos son solo visuales: el daño lo resuelve el autor.
    /// </summary>
    public class CaosNetPlayers : MonoBehaviour
    {
        const float PoseInterval = 0.08f;

        sealed class RemoteState
        {
            public CharacterCustomization Avatar;
            public ChaosRemotePlayer Motion;
            public string Look;
        }

        readonly Dictionary<string, RemoteState> _remotes =
            new Dictionary<string, RemoteState>(StringComparer.OrdinalIgnoreCase);

        [SerializeField] bool lobbyMode;

        CaosNetPeer _peer;
        PlayerController _player;
        ChaosHealth _health;
        CharacterCustomization _localCustom;
        Transform _target;
        float _nextPose;
        float _nextPoseLog;
        int _shotId;
        string _identity;

        bool Connected => _peer != null && _peer.IsConnected;

        /// <summary>Cuantos avatares remotos hay activos (sin contar el local).</summary>
        public int RemoteCount => _remotes.Count;

        /// <summary>Nombre + token de proceso: dos jugadores pueden llamarse igual.</summary>
        string Identity => _identity ?? (_identity = LocalName + "@" +
            Guid.NewGuid().ToString("N").Substring(0, 6));

        static string DisplayFrom(string identity)
        {
            if (string.IsNullOrEmpty(identity)) return "Invitado";
            int at = identity.IndexOf('@');
            return at > 0 ? identity.Substring(0, at) : identity;
        }

        string LocalName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CaosSession.LocalName)) return CaosSession.LocalName;
                if (_peer != null && !string.IsNullOrWhiteSpace(_peer.DisplayName)) return _peer.DisplayName;
                if (_localCustom != null && !string.IsNullOrWhiteSpace(_localCustom.PlayerName))
                    return _localCustom.PlayerName;
                var custom = CharacterCustomization.Instance;
                return custom != null ? custom.PlayerName : "Jugador";
            }
        }

        /// <summary>Llama desde la sala: sin PlayerController, avatares en pedestal.</summary>
        public void ConfigureLobby() => lobbyMode = true;

        void Start()
        {
            _peer = CaosNetPeer.Instance != null
                ? CaosNetPeer.Instance
                : FindFirstObjectByType<CaosNetPeer>();

            if (_peer != null)
            {
                _peer.MessageReceived -= OnMessage;
                _peer.MessageReceived += OnMessage;
            }

            if (lobbyMode)
            {
                _localCustom = CharacterCustomization.CreateLocalAvatar();
                _target = _localCustom.transform;
                _target.position = LobbySlot(Identity);
                _target.rotation = Quaternion.identity;
                Debug.Log($"[Caos] Avatar local de sala: {LocalName} en {_target.position}");
            }
            else
            {
                _player = FindFirstObjectByType<PlayerController>();
                _target = _player != null ? _player.transform : null;
                if (_player != null) _health = _player.GetComponent<ChaosHealth>();
                _localCustom = CharacterCustomization.Instance != null
                    ? CharacterCustomization.Instance
                    : _player?.GetComponent<CharacterCustomization>();
            }
        }

        void OnDestroy()
        {
            if (_peer != null) _peer.MessageReceived -= OnMessage;
        }

        void Update()
        {
            if (!Connected || _target == null || Time.time < _nextPose) return;
            _nextPose = Time.time + PoseInterval;
            SendPose();
        }

        void SendPose()
        {
            if (lobbyMode) _target.position = LobbySlot(Identity);

            string color = _localCustom != null
                ? ColorUtility.ToHtmlStringRGB(_localCustom.PlayerColor)
                : ColorUtility.ToHtmlStringRGB(Color.red);

            string payload = CaosNetMessages.Join(
                Identity,
                F(_target.position.x), F(_target.position.y), F(_target.position.z),
                F(_target.eulerAngles.y),
                color,
                _localCustom != null ? _localCustom.HatIndex.ToString(CultureInfo.InvariantCulture) : "0",
                _localCustom != null ? _localCustom.ShirtIndex.ToString(CultureInfo.InvariantCulture) : "0",
                _localCustom != null ? _localCustom.PantsIndex.ToString(CultureInfo.InvariantCulture) : "0",
                _localCustom != null ? _localCustom.FaceIndex.ToString(CultureInfo.InvariantCulture) : "0",
                _localCustom != null ? _localCustom.BuildIndex.ToString(CultureInfo.InvariantCulture) : "0",
                _localCustom != null ? _localCustom.CostumeIndex.ToString(CultureInfo.InvariantCulture) : "0",
                _health != null ? _health.Current.ToString("0", CultureInfo.InvariantCulture) : "100");

            Dispatch(CaosMessageKind.NetworkPose, payload);
        }

        /// <summary>Llamado por Arena/Dungeon justo después de crear la bala local.</summary>
        public void NotifyShot(Vector3 origin, Vector3 direction, float speed,
            float damage, ArenaElement element)
        {
            if (!Connected) return;

            _shotId++;
            string payload = CaosNetMessages.Join(
                Identity, _shotId.ToString(CultureInfo.InvariantCulture),
                F(origin.x), F(origin.y), F(origin.z),
                F(direction.x), F(direction.y), F(direction.z),
                F(speed), F(damage),
                ((int)element).ToString(CultureInfo.InvariantCulture));

            Dispatch(CaosMessageKind.NetworkShot, payload);
        }

        /// <summary>Llamado por Chaos Express cuando el martillo local golpea.</summary>
        public void NotifyMelee(Vector3 origin, float yaw)
        {
            if (!Connected) return;

            string payload = CaosNetMessages.Join(Identity,
                F(origin.x), F(origin.y), F(origin.z), F(yaw));
            Dispatch(CaosMessageKind.NetworkMelee, payload);
        }

        void Dispatch(CaosMessageKind kind, string payload)
        {
            if (_peer == null || !_peer.IsConnected) return;
            if (_peer.IsHost) _peer.Broadcast(kind, payload);
            else _peer.Send(kind, payload);
        }

        // ------------------------------------------------------------ recepción

        void OnMessage(CaosMessageKind kind, string payload, string from)
        {
            switch (kind)
            {
                case CaosMessageKind.NetworkPose:
                    OnPose(payload);
                    break;
                case CaosMessageKind.NetworkShot:
                    OnShot(payload);
                    break;
                case CaosMessageKind.NetworkMelee:
                    OnMelee(payload);
                    break;
                case CaosMessageKind.PeerLeft:
                    RemoveRemote(payload);
                    if (_peer != null && _peer.IsHost && !string.IsNullOrWhiteSpace(payload))
                        _peer.Broadcast(CaosMessageKind.PeerLeft, payload);
                    break;
            }
        }

        void OnPose(string payload)
        {
            string[] parts = CaosNetMessages.Split(payload);
            if (parts.Length < 12) return;

            string identity = string.IsNullOrWhiteSpace(parts[0]) ? "Invitado" : parts[0];
            if (string.Equals(identity, Identity, StringComparison.OrdinalIgnoreCase)) return;
            string name = DisplayFrom(identity);

            if (!TryVector(parts, 1, out Vector3 position) ||
                !TryFloat(parts[4], out float yaw)) return;

            Color color = ParseColor(parts[5], name);
            int hat = ParseInt(parts[6]);
            int shirt = ParseInt(parts[7]);
            int pants = ParseInt(parts[8]);
            int face = ParseInt(parts[9]);
            int build = ParseInt(parts[10]);
            int costume = ParseInt(parts[11]);

            string look = string.Join("/", parts, 5, 7);
            if (!_remotes.TryGetValue(identity, out RemoteState state))
            {
                state = new RemoteState
                {
                    Avatar = CharacterCustomization.CreateRemote(
                        name, color, hat, shirt, pants, face, build, costume),
                    Motion = null,
                };
                state.Motion = state.Avatar.gameObject.AddComponent<ChaosRemotePlayer>();
                state.Motion.Configure(name, color, false);
                _remotes[identity] = state;
                Debug.Log($"[Caos] Avatar remoto creado: {name} en {position}");
            }
            else if (!string.Equals(state.Look, look, StringComparison.Ordinal))
            {
                state.Avatar.ApplyRemoteAppearance(
                    name, color, hat, shirt, pants, face, build, costume);
            }

            state.Look = look;
            state.Motion.SetPose(position, yaw);
            if (Time.time >= _nextPoseLog)
            {
                _nextPoseLog = Time.time + 2f;
                Debug.Log($"[Caos] Pose remota: {name} en {position} yaw={yaw:0.#}");
            }

            // El host repite la pose para que todos vean a todos.
            if (_peer != null && _peer.IsHost)
                _peer.Broadcast(CaosMessageKind.NetworkPose, payload);
        }

        void OnShot(string payload)
        {
            string[] parts = CaosNetMessages.Split(payload);
            if (parts.Length < 11) return;
            if (string.Equals(parts[0], Identity, StringComparison.OrdinalIgnoreCase)) return;

            if (!TryVector(parts, 2, out Vector3 origin) ||
                !TryVector(parts, 5, out Vector3 direction) ||
                !TryFloat(parts[8], out float speed)) return;

            int elementValue = ParseInt(parts[10]);
            if (elementValue < 0 || elementValue > (int)ArenaElement.Veneno) return;
            var element = (ArenaElement)elementValue;

            CreateRemoteBullet(origin, direction, speed, element);
            Debug.Log($"[Caos] Disparo remoto replicado: {DisplayFrom(parts[0])}");

            if (_peer != null && _peer.IsHost)
                _peer.Broadcast(CaosMessageKind.NetworkShot, payload);
        }

        void OnMelee(string payload)
        {
            string[] parts = CaosNetMessages.Split(payload);
            if (parts.Length < 5) return;
            if (string.Equals(parts[0], Identity, StringComparison.OrdinalIgnoreCase)) return;
            if (!TryVector(parts, 1, out Vector3 origin)) return;

            ChaosFx.Burst(origin + Vector3.up * 1.1f,
                new Color(1f, 0.9f, 0.4f), 4);
            Debug.Log($"[Caos] Golpe cuerpo a cuerpo replicado: {DisplayFrom(parts[0])}");

            if (_peer != null && _peer.IsHost)
                _peer.Broadcast(CaosMessageKind.NetworkMelee, payload);
        }

        void CreateRemoteBullet(Vector3 origin, Vector3 direction,
            float speed, ArenaElement element)
        {
            GameObject shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "RemoteBullet";
            shot.transform.position = origin;
            shot.transform.localScale = Vector3.one *
                (element == ArenaElement.Rayo ? 0.24f : 0.19f);
            shot.GetComponent<Renderer>().sharedMaterial =
                ArenaMaterial.Create(ArenaElementInfo.Color(element), 0f, 0.6f);

            var projectile = shot.AddComponent<ArenaProjectile>();
            projectile.ConfigureRemote(direction, speed, element);
            Destroy(shot, 3.2f);
        }

        void RemoveRemote(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            var keys = new List<string>();
            foreach (string key in _remotes.Keys)
            {
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(DisplayFrom(key), name, StringComparison.OrdinalIgnoreCase))
                    keys.Add(key);
            }

            foreach (string key in keys)
            {
                RemoteState state = _remotes[key];
                _remotes.Remove(key);
                if (state.Avatar != null) Destroy(state.Avatar.gameObject);
            }
        }

        // ------------------------------------------------------------ utilidades

        /// <summary>Pedestales deterministas para que todos coincidan en la sala.</summary>
        static Vector3 LobbySlot(string name)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < name.Length; i++)
                hash = (hash ^ (byte)name[i]) * 16777619u;

            switch (hash % 4u)
            {
                case 0u: return new Vector3(-3.6f, 0f, 0.2f);
                case 1u: return new Vector3(3.6f, 0f, 0.2f);
                case 2u: return new Vector3(-2.1f, 0f, 2.2f);
                default: return new Vector3(2.1f, 0f, 2.2f);
            }
        }

        static bool TryVector(string[] parts, int start, out Vector3 value)
        {
            value = Vector3.zero;
            if (parts.Length < start + 3) return false;
            return TryFloat(parts[start], out value.x) &&
                   TryFloat(parts[start + 1], out value.y) &&
                   TryFloat(parts[start + 2], out value.z);
        }

        static bool TryFloat(string text, out float value) => float.TryParse(
            text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        static int ParseInt(string text) => int.TryParse(
            text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

        static Color ParseColor(string html, string name)
        {
            if (!string.IsNullOrEmpty(html) &&
                ColorUtility.TryParseHtmlString("#" + html, out Color color)) return color;

            float hue = 0f;
            for (int i = 0; i < name.Length; i++) hue += name[i] * 0.07f;
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), 0.65f, 0.95f);
        }

        static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
