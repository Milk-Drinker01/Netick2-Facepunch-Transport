using System;
using Steamworks;
using UnityEngine;
using System.Collections;

namespace Netick.Transports.Facepunch {
    public class FacepunchInitialier: MonoBehaviour {
        
        [SerializeField] uint AppID = 480;
        public static event Action OnInitializeCallbacks;

        void Awake() {
            if (SteamClient.IsValid) return;
            
            SteamClient.Init(AppID);
            StartCoroutine(EnsureValidity());
        }
        
        IEnumerator EnsureValidity()
        {
            yield return new WaitUntil(() => SteamClient.IsValid);
            OnInitializeCallbacks?.Invoke();
        }

    }
}
