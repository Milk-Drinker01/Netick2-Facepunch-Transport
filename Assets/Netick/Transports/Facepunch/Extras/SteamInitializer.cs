using System;
using System.Collections;
using Steamworks;
using UnityEngine;

namespace Netick.Transports.Facepunch.Extras
{
    public class SteamInitializer : MonoBehaviour
    {
        public static event Action OnInitialize;
        public static event Action OnConnected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            OnInitialize = delegate { };
            OnConnected = delegate { };
        }

        [SerializeField]
        uint AppID = 480;

        void Awake()
        {
            if (!SteamClient.IsValid)
            {
                SteamClient.Init(AppID);
            }

            StartCoroutine(EnsureValidity());
        }

        IEnumerator EnsureValidity()
        {
            yield return new WaitUntil(() => SteamClient.IsValid);
            OnInitialize?.Invoke();
            yield return new WaitUntil(() => SteamClient.IsLoggedOn);
            OnConnected?.Invoke();
        }

        private void OnDestroy()
        {
            if (SteamClient.IsValid)
            {
                Debug.Log("Steam Initializer destroyed - shutting down steam!");
                SteamClient.Shutdown();
            }
        }
    }
}
