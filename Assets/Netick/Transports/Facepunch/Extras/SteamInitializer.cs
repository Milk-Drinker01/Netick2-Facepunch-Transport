using System;
using System.Collections;
using Steamworks;
using UnityEngine;

namespace Netick.Transports.Facepunch.Extras
{
    public class SteamInitializer : MonoBehaviour
    {
        public static event Action OnInitialize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            OnInitialize = delegate { };
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
