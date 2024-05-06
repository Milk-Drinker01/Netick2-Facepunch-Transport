using System;
using System.Net;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using Netick.Unity;

namespace Netick.Transport
{
    [CreateAssetMenu(fileName = "FacepunchTransport", menuName = "Netick/Transport/FacepunchTransport", order = 2)]
    public class FacepunchTransportProvider : NetworkTransportProvider
    {
        public override NetworkTransport MakeTransportInstance() => new FacepunchTransport();
    }

    public class FacepunchTransport : NetworkTransport, ISocketManager, IConnectionManager
    {
        SocketManager _steamworksServer;

        ConnectionManager _steamConnection;

        static Dictionary<Steamworks.Data.Connection, FacepunchConnection> InternalConnections = new Dictionary<Steamworks.Data.Connection, FacepunchConnection>();

        private BitBuffer _buffer;
        //byte[] ReceiveData = new byte[1200];

        public class FacepunchConnection : TransportConnection
        {

            public Steamworks.Data.Connection Connection { get; set; }

            public override int Mtu => 1200;

            public override IEndPoint EndPoint => new IPEndPoint(IPAddress.Any, 4050).ToNetickEndPoint();

            public unsafe override void Send(IntPtr data, int length)
            {
                //Debug.Log($"SENDING A {length} BYTE PACKET");

                Connection.SendMessage(data, length, SteamworksUtils.instance.DisableNagleTimer ? SendType.Unreliable : SendType.NoNagle);
            }
        }

        public bool Server = false;

        public override void Init()
        {
            Debug.Log("Initializing Transport");
            _buffer = new BitBuffer(createChunks: false);
        }

        public override void Run(RunMode mode, int port)
        {
            switch (mode)
            {
                case RunMode.Server:
                    {
                        _steamworksServer = SteamNetworkingSockets.CreateRelaySocket<SocketManager>(port);
                        _steamworksServer.Interface = this;

                        Server = true;
                        SteamworksUtils.instance.GameServerInitialized();
                        break;
                    }
                case RunMode.Client:
                    {
                        Server = false;
                        break;
                    }
            }
        }

        public override void Shutdown()
        {
            if (_steamConnection != null)
                _steamConnection.Close();
            if (_steamworksServer != null)
                _steamworksServer.Close();
            _steamworksServer = null;
            _steamConnection = null;
        }

        public override void Connect(string address, int port, byte[] connectionData, int connectionDataLen)
        {
            if (!ulong.TryParse(address, out ulong ID))
                return;
            _steamConnection = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(ID, port);
            _steamConnection.Interface = this;
        }

        public override void Disconnect(TransportConnection connection)
        {
            SteamworksUtils.instance.CurrentLobby.Leave();
        }

        public override void PollEvents()
        {
            if (Server)
            {
                _steamworksServer?.Receive();
            }
            else
            {
                _steamConnection?.Receive();
            }
        }

        #region SERVER
        void ISocketManager.OnConnecting(Steamworks.Data.Connection connection, ConnectionInfo info)
        {
            connection.Accept();
        }

        void ISocketManager.OnConnected(Steamworks.Data.Connection connection, ConnectionInfo info)
        {
            var facepunchConnection = new FacepunchConnection();

            facepunchConnection.Connection = connection;

            InternalConnections.Add(connection, facepunchConnection);

            Debug.Log("Someone connected to the server");

            NetworkPeer.OnConnected(InternalConnections[connection]);
        }

        void ISocketManager.OnDisconnected(Steamworks.Data.Connection connection, ConnectionInfo info)
        {
            //TransportDisconnectReason reason = info.EndReason == NetConnectionEnd.Remote_Timeout ? TransportDisconnectReason.Timeout : TransportDisconnectReason.Shutdown;
            //Debug.Log(reason);
            NetworkPeer.OnDisconnected(InternalConnections[connection], TransportDisconnectReason.Timeout);
            InternalConnections.Remove(connection);
        }

        unsafe void ISocketManager.OnMessage(Steamworks.Data.Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            //Debug.Log($"RECEIVED PACKET SIZE: {size}");

            byte* ptr = (byte*)data;
            _buffer.SetFrom(ptr, size, size);
            NetworkPeer.Receive(InternalConnections[connection], _buffer);

            //byte* b = (byte*)data;

            //for (int ix = 0; ix < size; ++ix)
            //{
            //    ReceiveData[ix] = *b;
            //    b++;
            //}

            //NetworkPeer.Receive(InternalConnections[connection], ReceiveData, size);
        }
        #endregion

        #region CLIENT
        void IConnectionManager.OnConnecting(ConnectionInfo info)
        {

        }

        static FacepunchConnection clientToServerConnection;
        void IConnectionManager.OnConnected(ConnectionInfo info)
        {

            var facepunchConnection = new FacepunchConnection();
            facepunchConnection.Connection = _steamConnection.Connection;

            InternalConnections.Add(_steamConnection.Connection, facepunchConnection);
            clientToServerConnection = facepunchConnection;

            NetworkPeer.OnConnected(InternalConnections[_steamConnection.Connection]);

        }

        void IConnectionManager.OnDisconnected(ConnectionInfo info)
        {
            //TransportDisconnectReason reason = info.EndReason == NetConnectionEnd.Remote_Timeout ? TransportDisconnectReason.Timeout : TransportDisconnectReason.Shutdown;
            NetworkPeer.OnDisconnected(InternalConnections[_steamConnection.Connection], TransportDisconnectReason.Timeout);
            InternalConnections.Clear();
            clientToServerConnection = null;
            SteamworksUtils.instance.DisconnectFromServer();
        }

        unsafe void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            //Debug.Log($"RECEIVED PACKET SIZE: {size}");
            if (clientToServerConnection == null)
                return;

            byte* ptr = (byte*)data;
            _buffer.SetFrom(ptr, size, size);
            NetworkPeer.Receive(clientToServerConnection, _buffer);
        }
        #endregion
    }
}