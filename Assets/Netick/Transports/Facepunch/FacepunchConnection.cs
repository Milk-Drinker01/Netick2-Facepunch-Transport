using System;
using System.Net;
using Steamworks.Data;

namespace Netick.Transports.Facepunch {
    public class FacepunchConnection : TransportConnection {

        public bool ForceFlush;
        public SendType SteamSendType = SendType.NoNagle;

        public Steamworks.SteamId PlayerSteamID;
        public Steamworks.Data.Connection Connection { get; set; }
        
        public override int Mtu => 1200;

        public override IEndPoint EndPoint => new IPEndPoint(IPAddress.Any, 4050).ToNetickEndPoint();

        public override void Send(IntPtr data, int length) {
            Connection.SendMessage(data, length, SteamSendType);
            if (ForceFlush)
                Connection.Flush();
        }
    }
}
