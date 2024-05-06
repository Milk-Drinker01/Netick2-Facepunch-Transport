using Netick;
using Netick.Unity;
using Steamworks;
using Steamworks.Data;
using Steamworks.ServerList;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class SteamLobbySearchEvent : UnityEvent<List<Lobby>>
{
}
[System.Serializable]
public class SteamLobbyJoinedEvent : UnityEvent<Lobby>
{
}

[DefaultExecutionOrder(-50)]
public class SteamworksUtils : MonoBehaviour
{
    public static SteamworksUtils instance;
    public static SteamLobbyJoinedEvent OnLobbyEnteredEvent;
    public static UnityEvent OnLobbyLeftEvent;
    public static UnityEvent OnLobbySearchStart;
    public static SteamLobbySearchEvent OnLobbySearchFinished;
    public static UnityEvent OnGameServerShutdown;
    public static Lobby CurrentLobby;

    [SerializeField] uint AppID = 480;

    [Header("Lobby Host Settings")]

    [SerializeField] int NumberOfSlots = 16;



    [Header("Lobby Search Settings")]

    [Tooltip("this is just so that you dont find other peoples lobbies while testing with app id 480! make it unique!")]
    [SerializeField] string GameName = "Your Games Name";

    [SerializeField] DistanceFilter LobbySearchDistance = DistanceFilter.WorldWide;

    [SerializeField] int MinimumSlotsAvailable = 1;



    [Header("Steam Debug")]

    [SerializeField] bool _steamEnabled = false;

    public bool DisableNagleTimer = false;



    [Header("Netick Settings")]
    [SerializeField] NetworkTransportProvider Transport;
    [SerializeField] GameObject SandboxPrefab;
    [SerializeField] int Port = 4050;

    public static SteamId SteamID => SteamClient.SteamId;

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
            OnLobbyEnteredEvent = new SteamLobbyJoinedEvent();
            OnLobbyLeftEvent = new UnityEvent();
            OnLobbySearchStart = new UnityEvent();
            OnLobbySearchFinished = new SteamLobbySearchEvent();
            OnGameServerShutdown = new UnityEvent();

            if (_steamEnabled)
            {
                try
                {
                    SteamClient.Init(AppID);
                    SteamNetworkingUtils.InitRelayNetworkAccess();
                }
                catch (Exception)
                {
                    Debug.Log("something went wrong loading steam, make sure you have it open!");
                    _steamEnabled = false;
                }
            }
        }
        else
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            SteamClient.Shutdown();
    }

    private void Start()
    {
        if (!SteamClient.IsValid || instance != this)
        {
            return;
        }

        SteamFriends.ListenForFriendsMessages = true;

        SteamFriends.OnGameLobbyJoinRequested += async (lobby, steamId) => {
            await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
        };

        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;

        SteamMatchmaking.OnLobbyMemberJoined += (lobby, friend) => {
            Debug.Log(friend.Name + " Joined the lobby");
        };

        SteamMatchmaking.OnLobbyEntered += (lobby) => {
            Debug.Log($"You joined {lobby.GetData("LobbyName")}");
            CurrentLobby = lobby;
            OnLobbyEnteredEvent.Invoke(lobby);
        };

        SteamMatchmaking.OnLobbyMemberLeave += (lobby, friend) => {
            Debug.Log(friend.Name + " Left the lobby");
        };

        SteamMatchmaking.OnChatMessage += (lobby, friend, message) => {
            Debug.Log($"From {friend.Name}: {message}");
        };

        SteamMatchmaking.OnLobbyDataChanged += (lobby) => {

        };

        SteamMatchmaking.OnLobbyMemberDataChanged += (lobby, friend) => {

        };

        //.Invoke(new Lobby(x.SteamIDLobby), x.IP, x.Port, x.SteamIDGameServer);
        SteamMatchmaking.OnLobbyGameCreated += (lobby, ip, port, serverGameId) => {
            if (serverGameId != 0)
                Debug.Log("A server has been associated with this Lobby");
        };
    }
    #region Lobby Stuff
    
    LobbyType _lobbyType;
    List<Lobby> Matches = new List<Lobby>();
    public async void Search()
    {
        _lobbyType = LobbyType.Public;

        //var lobbies = await SteamMatchmaking.LobbyList.WithSlotsAvailable(1).FilterDistanceWorldwide().WithKeyValue("GameName", GameName).RequestAsync();
        Lobby[] lobbies;
        switch (LobbySearchDistance)
        {
            case DistanceFilter.Close:      lobbies = await SteamMatchmaking.LobbyList.WithSlotsAvailable(MinimumSlotsAvailable).FilterDistanceClose().WithKeyValue("GameName", GameName).RequestAsync(); break;
            case DistanceFilter.Default:    lobbies = await SteamMatchmaking.LobbyList.WithSlotsAvailable(MinimumSlotsAvailable).WithKeyValue("GameName", GameName).RequestAsync(); break;
            case DistanceFilter.Far:        lobbies = await SteamMatchmaking.LobbyList.WithSlotsAvailable(MinimumSlotsAvailable).FilterDistanceFar().WithKeyValue("GameName", GameName).RequestAsync(); break;
            case DistanceFilter.WorldWide:  lobbies = await SteamMatchmaking.LobbyList.WithSlotsAvailable(MinimumSlotsAvailable).FilterDistanceWorldwide().WithKeyValue("GameName", GameName).RequestAsync(); break;

            default:                        lobbies = await SteamMatchmaking.LobbyList.WithSlotsAvailable(MinimumSlotsAvailable).WithKeyValue("GameName", GameName).RequestAsync(); break;
        }

        Matches.Clear();
        OnLobbySearchStart.Invoke();

        if (lobbies == null)
        {
            Debug.Log("No lobbies found");
            //CreateLobby(0);
            return;
        }

        foreach (var lobby in lobbies)
        {
            if (lobby.GetData("GameName") == GameName && !(Matches.Contains(lobby)) && lobby.MemberCount != 0)
                Matches.Add(lobby);
        }

        if (Matches.Count == 0)
        {
            //you might want to create a public lobby if none are found in the search
            //CreateLobby(0);
        }
        else
        {
            //or you might want to join the first match found automatically
            //await SteamMatchmaking.JoinLobbyAsync(Matches.First().Id);
        }

        OnLobbySearchFinished.Invoke(Matches);
    }

    public void CreateLobby(int lobbyType)
    {
        _lobbyType = (LobbyType)lobbyType;

        Task.Run(async () => await SteamMatchmaking.CreateLobbyAsync());
    }

    void OnLobbyCreated(Result status, Lobby lobby)
    {
        lobby.SetData("GameName", GameName);
        lobby.SetData("LobbyName", $"{SteamClient.Name}'s lobby." );
        lobby.SetJoinable(true);
        //NetickConfig config = Resources.Load<NetickConfig>("netickConfig");
        //lobby.MaxMembers = config.GetMaxPlayers;
        lobby.MaxMembers = NumberOfSlots;

        CurrentLobby = lobby;

        Debug.Log($"lobby {lobby.Id} was created");

        switch (_lobbyType)
        {
            case LobbyType.Public:
                lobby.SetPublic();
                break;
            case LobbyType.FriendsOnly:
                lobby.SetFriendsOnly();
                break;
            case LobbyType.Private:
                lobby.SetPrivate();
                break;
            default:
                break;
        }
    }

    public static async Task JoinLobby(ulong id)
    {
        await SteamMatchmaking.JoinLobbyAsync(id);
    }

    public void LeaveLobby()
    {
        Debug.Log("leaving lobby");
        CurrentLobby.Leave();
        DisconnectFromServer();
        OnLobbyLeftEvent.Invoke();
    }

    public enum DistanceFilter
    {
        Close,
        Default,
        Far,
        WorldWide
    }

    public enum LobbyType
    {
        Public,
        FriendsOnly,
        Private
    }
    #endregion

    #region Server Stuff
    public void StartGameServer()
    {
        Netick.Unity.Network.StartAsHost(Transport, Port, SandboxPrefab);
    }

    public void GameServerInitialized()
    {
        if (CurrentLobby.Owner.Id == SteamID)
        {
            CurrentLobby.SetGameServer(SteamID);
        }
    }

    public void StopGameServer()
    {
        DisconnectFromServer();
    }
    #endregion

    #region Client Stuff
    public void ConnectToGameServer()
    {
        uint ip = 0;
        ushort port = 4050;
        SteamId serverID = 0;
        if (!CurrentLobby.GetGameServer(ref ip, ref port, ref serverID) || serverID == 0)
        {
            Debug.Log("Trying to connect to the lobbys server, but one has not been assigned");
            return;
        }

        var sandbox = Netick.Unity.Network.StartAsClient(Transport, Port, SandboxPrefab);
        sandbox.Connect(Port, CurrentLobby.Owner.Id.ToString());
    }

    public void DisconnectedFromHostServer()
    {
        Debug.Log("Disconnected");
    }
    #endregion

    public void DisconnectFromServer()
    {
        OnGameServerShutdown.Invoke();
        Netick.Unity.Network.Shutdown();
    }

    public void OnNetickShutdown()
    {
        SteamId invalidID;
        invalidID.Value = 0;
        if (CurrentLobby.IsOwnedBy(SteamID))
            CurrentLobby.SetGameServer("127.0.0.1", 0);
    }
}