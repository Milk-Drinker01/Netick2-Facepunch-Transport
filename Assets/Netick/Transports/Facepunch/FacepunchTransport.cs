﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Network = Netick.Unity.Network;

namespace Netick.Transports.Facepunch {

    public class FacepunchTransport : NetworkTransport, ISocketManager, IConnectionManager {

        public static SendType SteamSendType = SendType.NoNagle;
        public static bool ForceFlush;

        static readonly Dictionary<Steamworks.Data.Connection, FacepunchConnection> InternalConnections = new Dictionary<Steamworks.Data.Connection, FacepunchConnection>();

        static FacepunchConnection clientToServerConnection;

        readonly LogLevel _logLevel;

        BitBuffer _buffer;

        ConnectionManager _steamConnection;

        SocketManager _steamworksServer;

        public bool IsServer;

        public FacepunchTransport(SendType sendType, bool forceFlush, LogLevel logLevel = LogLevel.Error) {
            SteamSendType = sendType;
            ForceFlush = forceFlush;
            _logLevel = logLevel;
        }
        public static SteamId SteamID { get; private set; }
        public static event Action OnNetickServerStarted;
        public static event Action OnNetickClientStarted;
        public static event Action OnNetickClientDisconnect;
        public static event Action OnNetickShutdownEvent;
        private static bool TransportInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            OnNetickServerStarted = delegate { };
            OnNetickClientStarted = delegate { };
            OnNetickClientDisconnect = delegate { };
            OnNetickShutdownEvent = delegate { };
            TransportInitialized = false;
        }

        public static SteamId GetPlayerSteamID(NetworkPlayer player)
        {
            //return server player id
            if (player is Server) return SteamID;

            //return client player id
            var networkConnection = (NetworkConnection)player;
            var facepunchConnection = (FacepunchConnection)networkConnection.TransportConnection;
            return facepunchConnection.PlayerSteamID;
        }

        private Queue<FacepunchConnection> _freeConnections = new Queue<FacepunchConnection>();
        public override void Init() {
            if (_logLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Initializing Transport");

            if (!SteamClient.IsValid) {
                if (_logLevel <= LogLevel.Error)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - SteamClient wasn't initialized. " +
                              "Read more on how to set up transport: https://github.com/Milk-Drinker01/Netick2-Facepunch-Transport");
                return;
            }

            InitSteamworks(_logLevel);

            _buffer = new BitBuffer(createChunks: false);

            for (int i = 0; i < Engine.MaxClients; i++)
                _freeConnections.Enqueue(new FacepunchConnection());
        }

        public static async void InitSteamworks(LogLevel logLevel = LogLevel.Developer)
        {
            if (TransportInitialized)
                return;
            TransportInitialized = true;

            while (!SteamClient.IsValid)
            {
                await Task.Yield();
            }

            SteamNetworkingUtils.InitRelayNetworkAccess();

            if (logLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Initialized access to Steam Relay Network.");

            SteamID = SteamClient.SteamId;

            if (logLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Fetched user Steam ID.");
        }

        public override void Run(RunMode mode, int port) {
            switch (mode) {
                case RunMode.Server: {
                        if (_logLevel <= LogLevel.Developer)
                            Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as server");

                        _steamworksServer = SteamNetworkingSockets.CreateRelaySocket<SocketManager>(port);
                        _steamworksServer.Interface = this;

                        IsServer = true;
                        OnNetickServerStarted?.Invoke();
                        break;
                    }
                case RunMode.Client: {
                        if (_logLevel <= LogLevel.Developer)
                            Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as client");

                        IsServer = false;
                        OnNetickClientStarted?.Invoke();
                        break;
                    }
            }
        }

        public override void Shutdown() {
            try {
                _steamConnection?.Close();
                _steamworksServer?.Close();
                _steamworksServer = null;
                _steamConnection = null;
            }
            catch (Exception e) {
                if (_logLevel <= LogLevel.Error)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Shutting down error: {e}");
            }

            OnNetickShutdownEvent?.Invoke();
        }

        public override void PollEvents() {
            _steamworksServer?.Receive();
            _steamConnection?.Receive();
        }

        public override void Disconnect(TransportConnection connection)
        {
            FacepunchConnection facepunchConnection = (FacepunchConnection)connection;
            facepunchConnection.Connection.Flush();
            facepunchConnection.Connection.Close();
            InternalConnections.Remove(facepunchConnection.Connection);

            if (_logLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Player {(facepunchConnection).PlayerSteamID} Disconnected from server.");
        }

        #region SERVER

        void ISocketManager.OnConnecting(Steamworks.Data.Connection connection, ConnectionInfo info) {
            if (Engine.ConnectedPlayers.Count == Engine.MaxClients)
            {
                if (_logLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Declining connection from Steam user {info.Identity.SteamId}. (server is full)");
                connection.Close();
            }
            else
            {
                if (_logLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Accepting connection from Steam user {info.Identity.SteamId}.");
                connection.Accept();
            }
        }

        void ISocketManager.OnConnected(Steamworks.Data.Connection connection, ConnectionInfo info) {
            var facepunchConnection = _freeConnections.Dequeue();
            facepunchConnection.Connection = connection;
            facepunchConnection.PlayerSteamID = info.Identity.SteamId;

            if (InternalConnections.TryAdd(connection, facepunchConnection)) {
                if (_logLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");
                NetworkPeer.OnConnected(InternalConnections[connection]);
            }
            else if (_logLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to connect client with ID {connection.Id}, client already connected.");

        }

        void ISocketManager.OnDisconnected(Steamworks.Data.Connection connection, ConnectionInfo info) {
            _freeConnections.Enqueue(InternalConnections[connection]);
            NetworkPeer.OnDisconnected(InternalConnections[connection], TransportDisconnectReason.Timeout);
            InternalConnections.Remove(connection);

            if (_logLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}");
        }

        unsafe void ISocketManager.OnMessage(Steamworks.Data.Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel) {
            var ptr = (byte*)data;
            _buffer.SetFrom(ptr, size, size);
            NetworkPeer.Receive(InternalConnections[connection], _buffer);
        }

        #endregion

        #region CLIENT

        public override void Connect(string address, int port, byte[] connectionData, int connectionDataLen) {
            if (!ulong.TryParse(address, out var ID))
                return;
            _steamConnection = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(ID, port);
            _steamConnection.Interface = this;
        }

        void IConnectionManager.OnConnecting(ConnectionInfo info) {
            if (_logLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connecting with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnConnected(ConnectionInfo info) {

            var facepunchConnection = new FacepunchConnection {
                Connection = _steamConnection.Connection,
            };

            if (InternalConnections.TryAdd(_steamConnection.Connection, facepunchConnection)) {
                if (_logLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");

                clientToServerConnection = facepunchConnection;
                NetworkPeer.OnConnected(InternalConnections[_steamConnection.Connection]);

            }
            else if (_logLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to connect with Steam user {info.Identity.SteamId}, client already connected.");
        }

        void IConnectionManager.OnDisconnected(ConnectionInfo info) {
            NetworkPeer.OnDisconnected(InternalConnections[_steamConnection.Connection], TransportDisconnectReason.Timeout);
            InternalConnections.Clear();
            clientToServerConnection = null;

            if (_logLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - You have been removed from the server (either you were kicked, or the server shut down).");

            OnNetickClientDisconnect.Invoke();
        }

        unsafe void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel) {
            if (clientToServerConnection == null) return;

            var ptr = (byte*)data;
            _buffer.SetFrom(ptr, size, size);
            NetworkPeer.Receive(clientToServerConnection, _buffer);
        }

        #endregion

    }
}
