using Netick.Unity;
using Steamworks.Data;
using UnityEngine;

namespace Netick.Transports.Facepunch {
    [CreateAssetMenu(fileName = "FacepunchTransport", menuName = "Netick/Transport/FacepunchTransport", order = 2)]
    public class FacepunchTransportProvider : NetworkTransportProvider {

        [Tooltip("ive found that No Nagle provides the best latency for me")]
        [SerializeField]
        SendType SteamDataSendType = SendType.NoNagle;

        [Tooltip("Keep this off if you are using the NoNagle send type. turning this on disables the nagle timer.")]
        [SerializeField]
        bool FlushMessages;

        [Tooltip("Use this to decide if clients destroy network objects on disconnect.")]
        [SerializeField]
        bool DestroyNetworkObjectsOnDisconnect;

        [SerializeField]
        LogLevel logLevel = LogLevel.Error;

#if UNITY_EDITOR
        public void OnValidate() {
            FacepunchTransport.SteamSendType = SteamDataSendType;
            FacepunchTransport.ForceFlush = FlushMessages;
        }
#endif
        public override NetworkTransport MakeTransportInstance() => new FacepunchTransport(SteamDataSendType, FlushMessages, DestroyNetworkObjectsOnDisconnect, logLevel);
    }
}
