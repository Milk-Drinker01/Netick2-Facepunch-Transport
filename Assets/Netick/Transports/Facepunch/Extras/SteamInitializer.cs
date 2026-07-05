using System;
using System.Collections;
using Steamworks;
using UnityEngine;

namespace Netick.Transports.Facepunch.Extras
{
    public class SteamInitializer : MonoBehaviour
    {
        public static event Action OnInitialize;
        public static SteamInitializer instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            OnInitialize = delegate { };
            instance = null;
        }

        [SerializeField]
        protected uint AppID = 480;

        void Awake()
        {
            if (instance == null)
            {
                DontDestroyOnLoad(gameObject);
                instance = this;
                InitSteam();
            }
            else
                Destroy(gameObject);
        }

        public virtual uint GetAppId()
        {
            return AppID;
        }

        public virtual bool IsSteamEnabled()
        {
            return true;
        }

        private void InitSteam()
        {
            if (SteamClient.IsValid)
                return;

            if (!IsSteamEnabled())
                return;

            StartCoroutine(TryStartSteam());

            StartCoroutine(EnsureValidity());
        }

        IEnumerator TryStartSteam()
        {
            while(true)
            {
                try
                {
                    SteamClient.Init(GetAppId());
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

        protected virtual void ShutDownSteam()
        {
            if (SteamClient.IsValid)
            {
                Debug.Log("Steam Initializer destroyed - shutting down steam!");
                SteamClient.Shutdown();
            }
        }
    }
}
