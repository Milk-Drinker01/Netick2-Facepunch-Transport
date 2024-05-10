using System;
using System.Collections;
using Steamworks;
using UnityEngine;

namespace Netick.Transports.Facepunch.Extras {
    public class SteamInitializer : MonoBehaviour {

        [SerializeField]
        uint AppID = 480;

        void Awake() {
            if (!SteamClient.IsValid) {
                SteamClient.Init(AppID);
            }

            StartCoroutine(EnsureValidity());
        }
        public static event Action OnInitializeCallbacks;

        IEnumerator EnsureValidity() {
            yield return new WaitUntil(() => SteamClient.IsValid);
            OnInitializeCallbacks?.Invoke();
        }
    }
}
