using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Caos.Net
{
    /// <summary>
    /// Sala cooperativa muy ligera sobre TCP. El host escucha y el invitado se
    /// conecta con el código de sala. Los mensajes se encolan en un hilo y se entregan en
    /// el Update del hilo principal de Unity (nunca se toca la escena desde el hilo).
    /// </summary>
    public class CaosNetPeer : MonoBehaviour
    {
        public enum Role { None, Host, Client }

        class Connection
        {
            public TcpClient Client;
            public StreamWriter Writer;
            public Thread Thread;
            public string Name = "Jugador";
            public volatile bool Closed;
        }

        public static CaosNetPeer Instance { get; private set; }

        [SerializeField] int port = 7777;

        readonly ConcurrentQueue<Incoming> _incoming = new ConcurrentQueue<Incoming>();
        readonly List<Connection> _connections = new List<Connection>();
        readonly object _connectionsLock = new object();
        readonly object _sendLock = new object();

        TcpListener _listener;
        Thread _acceptThread;
        Connection _server;
        volatile bool _running;
        Role _role;
        string _displayName = "Jugador";

        struct Incoming
        {
            public Connection From;
            public CaosMessageKind Kind;
            public string Payload;
        }

        public event Action<CaosMessageKind, string, string> MessageReceived;
        public event Action<string> PeerListChanged;

        public Role CurrentRole => _role;
        public bool IsHost => _role == Role.Host;
        public bool IsClient => _role == Role.Client;
        public bool IsConnected => _running && (IsHost || (_server != null && _server.Client.Connected));
        public int Port => port;
        public string DisplayName
        {
            get => _displayName;
            set => _displayName = string.IsNullOrWhiteSpace(value) ? "Jugador" : value.Trim();
        }

        public IReadOnlyList<string> PeerNames
        {
            get
            {
                var names = new List<string>();
                lock (_connectionsLock)
                {
                    foreach (var connection in _connections)
                        if (!connection.Closed) names.Add(connection.Name);
                }
                return names;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            Shutdown();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            while (_incoming.TryDequeue(out var message))
            {
                string name = message.From != null ? message.From.Name : string.Empty;
                MessageReceived?.Invoke(message.Kind, message.Payload, name);
            }
        }

        void OnApplicationQuit() => Shutdown();

        // ------------------------------------------------------------- sala

        public bool StartHost(out string error)
        {
            error = string.Empty;
            Shutdown();

            try
            {
                // Si el puerto está ocupado, probamos los siguientes.
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    int candidate = port + attempt;
                    try
                    {
                        _listener = new TcpListener(IPAddress.Any, candidate);
                        _listener.Start();
                        port = candidate;
                        break;
                    }
                    catch (SocketException)
                    {
                        _listener = null;
                    }
                }

                if (_listener == null)
                {
                    error = $"No se pudo abrir el puerto {port}.";
                    return false;
                }

                _role = Role.Host;
                _running = true;
                _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "CaosHostAccept" };
                _acceptThread.Start();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Shutdown();
                return false;
            }
        }

        public bool Connect(string address, int remotePort, out string error)
        {
            error = string.Empty;
            Shutdown();

            if (string.IsNullOrWhiteSpace(address))
            {
                error = "Escribe el código de la sala.";
                return false;
            }

            try
            {
                var client = new TcpClient();
                client.Connect(address.Trim(), remotePort);
                if (!client.Connected)
                {
                    client.Close();
                    error = "No se pudo conectar con el host.";
                    return false;
                }

                _server = new Connection
                {
                    Client = client,
                    Writer = new StreamWriter(client.GetStream()) { AutoFlush = true, NewLine = "\n" },
                };

                _role = Role.Client;
                _running = true;
                _server.Thread = new Thread(() => ReadLoop(_server)) { IsBackground = true, Name = "CaosClientRead" };
                _server.Thread.Start();

                Send(CaosMessageKind.Hello, _displayName);
                return true;
            }
            catch (Exception exception)
            {
                error = $"No se pudo conectar: {exception.Message}";
                Shutdown();
                return false;
            }
        }

        public void Shutdown()
        {
            _running = false;

            try { _listener?.Stop(); } catch { /* ya detenido */ }
            _listener = null;

            List<Connection> snapshot;
            lock (_connectionsLock)
            {
                snapshot = new List<Connection>(_connections);
                _connections.Clear();
            }

            foreach (var connection in snapshot) Close(connection);
            if (_server != null) Close(_server);
            _server = null;

            _acceptThread = null;
            _role = Role.None;
            PeerListChanged?.Invoke(string.Empty);
        }

        public void Send(CaosMessageKind kind, string payload = "")
        {
            if (!IsConnected) return;
            string text = CaosNetMessages.Encode(kind, payload);
            if (text.Length > CaosNetMessages.MaxLength) return;

            if (IsClient)
            {
                SendTo(_server, text);
                return;
            }

            Broadcast(text);
        }

        /// <summary>Solo host: envía el mensaje a todos los invitados.</summary>
        public void Broadcast(string text)
        {
            if (!IsHost) return;
            lock (_connectionsLock)
            {
                foreach (var connection in _connections)
                    if (!connection.Closed) SendTo(connection, text);
            }
        }

        public void Broadcast(CaosMessageKind kind, string payload = "") =>
            Broadcast(CaosNetMessages.Encode(kind, payload));

        void SendTo(Connection connection, string text)
        {
            if (connection == null || connection.Closed || connection.Writer == null) return;
            try
            {
                lock (_sendLock) connection.Writer.WriteLine(text);
            }
            catch
            {
                Close(connection);
            }
        }

        // ------------------------------------------------------------- hilos

        void AcceptLoop()
        {
            while (_running && _listener != null)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { break; }

                var connection = new Connection
                {
                    Client = client,
                    Writer = new StreamWriter(client.GetStream()) { AutoFlush = true, NewLine = "\n" },
                };

                lock (_connectionsLock)
                {
                    // Anfitrión + 3 invitados = 4 jugadores como máximo.
                    if (_connections.Count >= 3)
                    {
                        try { client.Close(); } catch { /* ya cerrado */ }
                        continue;
                    }
                    _connections.Add(connection);
                }
                connection.Thread = new Thread(() => ReadLoop(connection))
                {
                    IsBackground = true,
                    Name = "CaosHostRead",
                };
                connection.Thread.Start();
            }
        }

        void ReadLoop(Connection connection)
        {
            try
            {
                using (var reader = new StreamReader(connection.Client.GetStream()))
                {
                    while (_running && !connection.Closed)
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;
                        if (!CaosNetMessages.TryDecode(line, out var kind, out string payload)) continue;

                        if (kind == CaosMessageKind.Hello && IsHost)
                            connection.Name = string.IsNullOrWhiteSpace(payload) ? "Invitado" : payload;

                        _incoming.Enqueue(new Incoming
                        {
                            From = connection,
                            Kind = kind,
                            Payload = payload,
                        });
                    }
                }
            }
            catch
            {
                // Conexión cerrada por el otro lado.
            }
            finally
            {
                Close(connection);
                _incoming.Enqueue(new Incoming
                {
                    From = connection,
                    Kind = CaosMessageKind.PeerLeft,
                    Payload = connection.Name,
                });
            }
        }

        void Close(Connection connection)
        {
            if (connection == null) return;
            connection.Closed = true;
            try { connection.Client.Close(); } catch { /* ya cerrado */ }
        }
    }
}
