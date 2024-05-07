using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Netick.Unity;
using Netick.Transport;
using Steamworks;
using Steamworks.Data;

[DefaultExecutionOrder(-50)]
public class SteamInitializer : MonoBehaviour
{
    public static SteamInitializer Instance;
    public static event Action OnInitializeCallbacks;

    [SerializeField] uint AppID = 480;
    [SerializeField] bool EnableSteam = true;

    public static SteamId SteamID => SteamClient.SteamId;

    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            if (EnableSteam)
            {
                try
                {
                    SteamClient.Init(AppID);
                    StartCoroutine(EnsureValidity());

                }
                catch (Exception)
                {
                    Debug.LogWarning("something went wrong loading steam, make sure you have it open!");
                }
            }
        }
        else
        {
            Destroy(this);
        }
    }

    void OnDestroy()
    {
        if (Instance != this)
            return;
        SteamClient.Shutdown();
    }

    IEnumerator EnsureValidity()
    {
        yield return new WaitUntil(() => SteamClient.IsValid);
        Debug.Log("Steam Client Validated!");
        InitCallbacks();
    }

    void InitCallbacks()
    {
        SteamNetworkingUtils.InitRelayNetworkAccess();

        SteamFriends.ListenForFriendsMessages = true;

        OnInitializeCallbacks?.Invoke();
    }
}
