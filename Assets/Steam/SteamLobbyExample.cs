using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Netick.Unity;
using Netick.Transport;
using Netick.Transports.FacepunchTransport;
using Steamworks;
using Steamworks.Data;

public class SteamLobbyExample : MonoBehaviour
{
    public static event Action<Lobby> OnLobbyEnteredEvent;
    public static event Action OnLobbyLeftEvent;
    public static event Action OnLobbySearchStart;
    public static event Action<List<Lobby>> OnLobbySearchFinished;
    public static event Action OnGameServerShutdown;
    public static Lobby CurrentLobby;

    [SerializeField] bool AutoStartServerWithLobby = true;

    [Header("Lobby Host Settings")]
    [SerializeField] int NumberOfSlots = 16;

    [Header("Lobby Search Settings")]
    [Tooltip("this is just so that you dont find other peoples lobbies while testing with app id 480! make it unique!")]
    [SerializeField] string GameName = "Your Games Name";
    [SerializeField] DistanceFilter LobbySearchDistance = DistanceFilter.WorldWide;
    [SerializeField] int MinimumSlotsAvailable = 1;

    [Header("Netick Settings")]
    [SerializeField] NetworkTransportProvider Transport;
    [SerializeField] GameObject SandboxPrefab;
    [SerializeField] int Port = 4050;

    private void Start()
    {
        if (SteamClient.IsValid)
            InitLobbyCallbacks();
        else
            FacepunchInitializer.OnInitializeCallbacks += InitLobbyCallbacks;
    }

    void OnDestroy()
    {
        FacepunchInitializer.OnInitializeCallbacks -= InitLobbyCallbacks;
        FacepunchTransport.OnNetickServerStarted -= OnNetickServerStarted;
        FacepunchTransport.OnNetickShutdownEvent -= OnNetickShutdown;
    }

    private void InitLobbyCallbacks()
    {
        FacepunchTransport.OnNetickServerStarted += OnNetickServerStarted;
        FacepunchTransport.OnNetickShutdownEvent += OnNetickShutdown;

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

            if (AutoStartServerWithLobby && !lobby.IsOwnedBy(FacepunchInitializer.SteamID))
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

        SteamMatchmaking.OnLobbyGameCreated += (lobby, ip, port, serverGameId) => {
            if (serverGameId != 0)
                Debug.Log("A server has been associated with this Lobby");

            if (AutoStartServerWithLobby && !lobby.IsOwnedBy(FacepunchInitializer.SteamID))
                ConnectToGameServer();
        };
    }


    #region Lobby Stuff

    LobbyType _lobbyType;
    List<Lobby> Matches = new List<Lobby>();
    public async void SearchPublicLobbies()
    {
        _lobbyType = LobbyType.Public;

        Lobby[] lobbies;

        LobbyQuery query = SteamMatchmaking.LobbyList.WithSlotsAvailable(MinimumSlotsAvailable).WithKeyValue("GameName", GameName);

        query = LobbySearchDistance switch
        {
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
        lobby.SetData("LobbyName", $"{SteamClient.Name}'s lobby.");
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

    public void StartGameServer()
    {
        if (!CurrentLobby.IsOwnedBy(FacepunchInitializer.SteamID))
        {
            Debug.LogWarning("you cant start a server, you dont own the lobby");
            return;
        }
        if (Netick.Unity.Network.IsRunning)
        {
            Debug.LogWarning("a game server is already running");
            return;
        }

        Netick.Unity.Network.StartAsHost(Transport, Port, SandboxPrefab);
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
            Debug.LogWarning("Trying to connect to the lobbys server, but one has not been assigned");
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
        Debug.Log("Shutting Down Netick....");
        Netick.Unity.Network.Shutdown();
    }

    public void OnNetickServerStarted()
    {
        if (CurrentLobby.Owner.Id == FacepunchInitializer.SteamID)
        {
            CurrentLobby.SetGameServer(FacepunchInitializer.SteamID);
        }
    }

    public void OnNetickShutdown()
    {
        OnGameServerShutdown?.Invoke();
        if (CurrentLobby.IsOwnedBy(FacepunchInitializer.SteamID))
            CurrentLobby.SetGameServer("127.0.0.1", 0);
    }
}
