using System;
using System.Collections;
using Steamworks;
using UnityEngine;

namespace Netick.Transports.FacepunchTransport {

    [DefaultExecutionOrder(-50)]
    public class FacepunchInitializer : MonoBehaviour {

        [SerializeField]
        uint AppID = 480;

        public LogLevel logLevel = LogLevel.Error;

        public static SteamId SteamID { get; private set; }

        public void Awake() {
            try {
                SteamClient.Init(AppID);
                StartCoroutine(EnsureValidity());

            }
            catch (Exception e) {
                if (logLevel <= LogLevel.Error)
                    Debug.Log($"[{nameof(FacepunchInitializer)}] - Error loading Steam: {e}");
            }
        }

        void OnDestroy() {
            SteamClient.Shutdown();
        }
        public static event Action OnInitializeCallbacks;

        IEnumerator EnsureValidity() {
            yield return new WaitUntil(() => SteamClient.IsValid);

            if (logLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchInitializer)}] - Steam Client Validated");

            InitCallbacks();
        }

        void InitCallbacks() {
            SteamNetworkingUtils.InitRelayNetworkAccess();
            SteamID = SteamClient.SteamId;

            if (logLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchInitializer)}] - SteamID received");

            SteamFriends.ListenForFriendsMessages = true;

            OnInitializeCallbacks?.Invoke();
        }
    }
}
