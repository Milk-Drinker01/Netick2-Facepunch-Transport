using System;
using System.Net;
using Steamworks.Data;

namespace Netick.Transports.Facepunch {
    public class FacepunchConnection : TransportConnection {

        public Steamworks.SteamId PlayerSteamID;
        public Steamworks.Data.Connection Connection { get; set; }
        
        public override int Mtu => 1200;

        public override IEndPoint EndPoint => new IPEndPoint(IPAddress.Any, 4050).ToNetickEndPoint();

        public override void Send(IntPtr data, int length) {
            Connection.SendMessage(data, length, FacepunchTransport.SteamSendType);
            if (FacepunchTransport.ForceFlush)
                Connection.Flush();
        }

        public override void SendUserData(IntPtr ptr, int length, TransportDeliveryMethod transportDeliveryMethod)
        {
            switch(transportDeliveryMethod)
            {
                case TransportDeliveryMethod.Unreliable: Connection.SendMessage(ptr, length, FacepunchTransport.SteamSendType); break;
                case TransportDeliveryMethod.Reliable: Connection.SendMessage(ptr, length, SendType.Reliable); break;
            }
        }
    }
}
