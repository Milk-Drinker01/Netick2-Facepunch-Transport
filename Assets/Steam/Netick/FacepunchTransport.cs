using System;
using System.Net;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

namespace Netick.Transport
{

    [CreateAssetMenu(fileName = "FacepunchTransport", menuName = "Netick/Transport/FacepunchTransport", order = 2)]
    public class FacepunchTransport : NetworkTransport, ISocketManager, IConnectionManager
    {
        SocketManager _steamworksServer;

        ConnectionManager _steamConnection;

        static Dictionary<Connection, FacepunchConnection> InternalConnections = new Dictionary<Connection, FacepunchConnection>();

        byte[] ReceiveData = new byte[1200];

        public class FacepunchConnection : NetickConnection
        {

            public Connection Connection { get; set; }

            public override int Mtu => 1200;

            public override IPEndPoint EndPoint => new IPEndPoint(IPAddress.Any, 4050);

            public override void Send(byte[] data, int length)
            {
                //Debug.Log($"SENDING A {length} BYTE PACKET");

                Connection.SendMessage(data, 0, length, SendType.Unreliable);
            }
        }

        public bool Server = false;

        public override void Run(RunMode mode, int port)
        {
            switch (mode)
            {
                case RunMode.Server:
                    _steamworksServer = SteamNetworkingSockets.CreateRelaySocket<SocketManager>(0);
                    _steamworksServer.Interface = this;

                    Server = true;
                    break;
                case RunMode.Client:

                    Server = false;
                    break;
                default:
                    Debug.LogError("YOU FUCKED UP");
                    break;
            }
        }

        public override void Shutdown()
        {
            _steamConnection?.Close();
            _steamworksServer?.Close();
            _steamworksServer = null;
            _steamConnection = null;
        }

        public override void Connect(string address, int port, byte[] connectionData, int connectionDataLen)
        {
            _steamConnection = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(SteamworksUtils.instance._lobby.Owner.Id, 0);
            _steamConnection.Interface = this;
        }

        public override void Disconnect(NetickConnection connection)
        {
            SteamworksUtils.instance._lobby.Leave();
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

        void ISocketManager.OnConnecting(Connection connection, ConnectionInfo info)
        {
            connection.Accept();
        }

        void ISocketManager.OnConnected(Connection connection, ConnectionInfo info)
        {
            var facepunchConnection = new FacepunchConnection();

            facepunchConnection.Connection = connection;

            InternalConnections.Add(connection, facepunchConnection);

            Debug.Log("Someone connected");

            NetworkPeer.OnConnected(InternalConnections[connection]);
        }

        void ISocketManager.OnDisconnected(Connection connection, ConnectionInfo info)
        {
            InternalConnections.Remove(connection);
        }

        unsafe void ISocketManager.OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            //Debug.Log($"{size} PACKET SIZE_");

            byte* b = (byte*)data;

            for (int ix = 0; ix < size; ++ix)
            {
                ReceiveData[ix] = *b;
                b++;
            }

            NetworkPeer.Receive(InternalConnections[connection], ReceiveData, size);
        }

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
            InternalConnections.Clear();
            clientToServerConnection = null;
        }

        unsafe void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            //Debug.Log($"{size} PACKET SIZE_");

            byte* b = (byte*)data;

            for (int ix = 0; ix < size; ++ix)
            {
                ReceiveData[ix] = *b;
                b++;
            }

            //NetworkPeer.Receive(InternalConnections.Values.First(), ReceiveData, size);
            NetworkPeer.Receive(clientToServerConnection, ReceiveData, size);
        }

    }
}