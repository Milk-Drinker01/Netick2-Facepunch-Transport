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
            if (SteamClient.IsValid)
                return;
            //SteamClient.Init(AppID);
            StartCoroutine(TryStartSteam());
            StartCoroutine(EnsureValidity());
        }

        IEnumerator TryStartSteam()
        {
            while(true)
            {
                try
                {
                    SteamClient.Init(AppID);
                    yield break;
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
                //Debug.Log("something went wrong loading steam, make sure you have it open!");
                yield return new WaitForSeconds(.5f);
            }
        }

        IEnumerator EnsureValidity()
        {
            yield return new WaitUntil(() => SteamClient.IsValid);
            Debug.Log("Successfully Initialized Steam");
            OnInitialize?.Invoke();
        }

        private void OnDestroy()
        {
            ShutDownSteam();
        }

        void OnApplicationQuit()
        {
            ShutDownSteam();
        }

        void ShutDownSteam()
        {
            if (SteamClient.IsValid)
            {
                Debug.Log("Steam Initializer destroyed - shutting down steam!");
                SteamClient.Shutdown();
            }
        }
    }
}
