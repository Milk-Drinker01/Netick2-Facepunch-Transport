using Netick.Unity;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

using Network = Netick.Unity.Network;

[DefaultExecutionOrder(-50)]
public class SteamworksUtils : MonoBehaviour
{
    public static SteamworksUtils instance;
    public static event Action<Lobby> OnLobbyEnteredEvent;
    public static event Action OnLobbyLeftEvent;
    public static event Action OnLobbySearchStart;
    public static event Action<List<Lobby>> OnLobbySearchFinished;
    public static event Action OnGameServerShutdown;
    public static Lobby CurrentLobby;

    [SerializeField] uint AppID = 480;
    [SerializeField] bool AutoStartServerWithLobby = true;

    [Header("Lobby Host Settings")]
    [SerializeField] int NumberOfSlots = 16;
    
    [Header("Lobby Search Settings")]
    [Tooltip("this is just so that you dont find other peoples lobbies while testing with app id 480! make it unique!")]
    [SerializeField] string GameName = "Your Games Name";

    [SerializeField] DistanceFilter LobbySearchDistance = DistanceFilter.WorldWide;
    [SerializeField] int MinimumSlotsAvailable = 1;

    [Header("Steam Debug")]
    [SerializeField] bool EnableSteam = true;

    public bool DisableNagleTimer;
    
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
    
    IEnumerator EnsureValidity(){
        yield return new WaitUntil(() => SteamClient.IsValid);
        Debug.Log("Steam Client Validated!");
        InitCallbacks();
    }

    void OnDestroy()
    {
        if (instance == this)
            SteamClient.Shutdown();
    }

    void InitCallbacks()
    {
        SteamNetworkingUtils.InitRelayNetworkAccess();
        
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
            OnLobbyEnteredEvent?.Invoke(lobby);

            if (AutoStartServerWithLobby && !lobby.IsOwnedBy(SteamID))
                ConnectToGameServer();
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

            if (AutoStartServerWithLobby && !lobby.IsOwnedBy(SteamID))
                ConnectToGameServer();
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

        LobbyQuery query = SteamMatchmaking.LobbyList.WithSlotsAvailable(MinimumSlotsAvailable).WithKeyValue("GameName", GameName);

        query = LobbySearchDistance switch {
            DistanceFilter.Close => query.FilterDistanceClose(),
            DistanceFilter.Far => query.FilterDistanceFar(),
            DistanceFilter.WorldWide => query.FilterDistanceWorldwide(),
            _ => query
        };

        lobbies = await query.RequestAsync();
        
        Matches.Clear();
        OnLobbySearchStart?.Invoke();

        if (lobbies == null)
        {
            Debug.Log("No lobbies found");
            //CreateLobby(0);
            return;
        }

        foreach (var lobby in lobbies)
        {
            if (!Matches.Contains(lobby) && lobby.MemberCount != 0)
                Matches.Add(lobby);
        }
        
        OnLobbySearchFinished?.Invoke(Matches);
    }

    public void CreateLobby(LobbyType lobbyType = LobbyType.Public)
    {
        _lobbyType = lobbyType;
        SteamMatchmaking.CreateLobbyAsync();
    }
    
    public void CreateLobby(int lobbyType = 0)
    {
        _lobbyType = (LobbyType)lobbyType;
        SteamMatchmaking.CreateLobbyAsync();
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
        }

        if (AutoStartServerWithLobby)
            StartGameServer();
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
        OnLobbyLeftEvent?.Invoke();
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

    void StartGameServer()
    {
        if (!CurrentLobby.IsOwnedBy(SteamID))
        {
            Debug.LogWarning("you cant start a server, you dont own the lobby");
            return;
        }
        if (Network.IsRunning)
        {
            Debug.LogWarning("a game server is already running");
            return;
        }
            
        Network.StartAsHost(Transport, Port, SandboxPrefab);
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
    void ConnectToGameServer()
    {
        uint ip = 0;
        ushort port = 4050;
        SteamId serverID = 0;
        if (!CurrentLobby.GetGameServer(ref ip, ref port, ref serverID) || serverID == 0)
        {
            Debug.LogWarning("Trying to connect to the lobbys server, but one has not been assigned");
            return;
        }

        var sandbox = Network.StartAsClient(Transport, Port, SandboxPrefab);
        sandbox.Connect(Port, CurrentLobby.Owner.Id.ToString());
    }

    public void DisconnectedFromHostServer()
    {
        Debug.Log("Disconnected");
    }
    #endregion

    public void DisconnectFromServer()
    {
        Debug.Log("Game Server Shutdown");
        OnGameServerShutdown?.Invoke();
        Network.Shutdown();
    }

    public void OnNetickShutdown()
    {
        if (CurrentLobby.IsOwnedBy(SteamID))
            CurrentLobby.SetGameServer("127.0.0.1", 0);
    }
}
