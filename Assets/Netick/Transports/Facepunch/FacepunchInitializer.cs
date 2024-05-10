using System;
using System.Collections;
using Steamworks;
using UnityEngine;

namespace Netick.Transports.Facepunch {
    public class FacepunchInitializer : MonoBehaviour {

        [SerializeField]
        uint AppID = 480;

        void Awake() {
            if (SteamClient.IsValid) return;

            SteamClient.Init(AppID);
            StartCoroutine(EnsureValidity());
        }
        public static event Action OnInitializeCallbacks;

        IEnumerator EnsureValidity() {
            yield return new WaitUntil(() => SteamClient.IsValid);
            OnInitializeCallbacks?.Invoke();
        }
    }
}
