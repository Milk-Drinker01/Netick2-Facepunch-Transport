using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Netick.Unity;
using Netick.Transports.Facepunch.Extras;
using Netick.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using System.Linq;

public class SteamLobbyExample : MonoBehaviour
{
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

    public static event Action<Lobby> OnLobbyEnteredEvent;
    public static event Action OnLobbyLeftEvent;
    public static event Action OnLobbySearchStart;
    public static event Action<List<Lobby>> OnLobbySearchFinished;
    public static event Action OnGameServerShutdown;
    public static Lobby CurrentLobby;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void OnLoad()
    {
        CurrentLobby = default;
        OnLobbyEnteredEvent = delegate { };
        OnLobbyLeftEvent = delegate { };
        OnLobbySearchStart = delegate { };
        OnLobbySearchFinished = delegate { };
        OnGameServerShutdown = delegate { };
    }

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

    public static SteamId SteamID => SteamClient.SteamId;

    private void Start() {
        if (SteamClient.IsValid)
            InitLobbyCallbacks();
        else
            SteamInitializer.OnInitialize += InitLobbyCallbacks;
    }

    void OnDestroy()
    {
        SteamInitializer.OnInitialize -= InitLobbyCallbacks;
        FacepunchTransport.OnNetickServerStarted -= OnNetickServerStarted;
        FacepunchTransport.OnNetickClientDisconnect -= DisconnectedFromServer;
        FacepunchTransport.OnNetickShutdownEvent -= OnNetickShutdown;
    }

    private void InitLobbyCallbacks()
    {
        FacepunchTransport.OnNetickServerStarted += OnNetickServerStarted;
        FacepunchTransport.OnNetickClientDisconnect += DisconnectedFromServer;
        FacepunchTransport.OnNetickShutdownEvent += OnNetickShutdown;

        SteamFriends.OnGameLobbyJoinRequested += async (lobby, steamId) => {
            await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
        };

        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;

        SteamMatchmaking.OnLobbyMemberJoined += (lobby, friend) => {
            Debug.Log(friend.Name + " Joined the lobby");
        };

        SteamMatchmaking.OnLobbyEntered += (lobby) => {
            //if (lobby.Id != CurrentLobby.Id)
            //    LeaveLobby();

            Debug.Log($"You joined {lobby.GetData("LobbyName")}");
            CurrentLobby = lobby;
            OnLobbyEnteredEvent?.Invoke(lobby);

            if (AutoStartServerWithLobby && !lobby.IsOwnedBy(SteamClient.SteamId))
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

            if (AutoStartServerWithLobby && !lobby.IsOwnedBy(SteamClient.SteamId))
                ConnectToGameServer();
        };


        //THIS CODE WILL AUTO JOIN A LOBBY IF THE GAME WAS LAUNCHED BY CLICKING "join friend" ON STEAM
        string[] args = System.Environment.GetCommandLineArgs();
        if (args.Length >= 2)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].ToLower() == "+connect_lobby")
                {
                    if (ulong.TryParse(args[i + 1], out ulong lobbyID))
                    {
                        if (lobbyID > 0)
                        {
                            SteamMatchmaking.JoinLobbyAsync(lobbyID);
                        }
                    }
                    break;
                }
            }
        }
    }

    #region Lobby Stuff

    LobbyType _lobbyType;
    public LobbyType CurrentLobbyType => _lobbyType;
    public async void SearchPublicLobbies()
    {
        OnLobbySearchStart?.Invoke();

        if (!SteamClient.IsValid)
            return;

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

        if (lobbies == null)
        {
            Debug.Log("No lobbies found");
            //CreateLobby(0);
            return;
        }

        List<Lobby> ValidMatches = new List<Lobby>();

        foreach (var lobby in lobbies)
        {
            if (!ValidMatches.Contains(lobby) && lobby.MemberCount != 0)
                ValidMatches.Add(lobby);
        }

        OnLobbySearchFinished?.Invoke(ValidMatches);
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

        SetLobbyType();

        if (AutoStartServerWithLobby)
            StartGameServer();
    }

    public void ChangeLobbyType(LobbyType _newType)
    {
        _lobbyType = _newType;
        SetLobbyType();
    }

    void SetLobbyType()
    {
        switch (_lobbyType)
        {
            case LobbyType.Public:
                CurrentLobby.SetPublic();
                break;
            case LobbyType.FriendsOnly:
                CurrentLobby.SetFriendsOnly();
                break;
            case LobbyType.Private:
                CurrentLobby.SetPrivate();
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
        Friend me = new Friend(SteamID);
        if (!CurrentLobby.Members.Contains(me))
        {
            //Debug.Log("trying to leave a lobby ur not a part of!");
            return;
        }
        Debug.Log("leaving lobby");
        if (CurrentLobby.IsOwnedBy(SteamID))
        {
            CurrentLobby.SetJoinable(false);
            CurrentLobby.SetPrivate();
        }
        CurrentLobby.Leave();
        DisconnectedFromServer();
        OnLobbyLeftEvent?.Invoke();
        CurrentLobby = default;
    }
    #endregion

    #region Server Stuff

    public void StartGameServer()
    {
        if (!CurrentLobby.IsOwnedBy(SteamClient.SteamId))
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
    #endregion

    public void DisconnectFromServer()
    {
        DisconnectedFromServer();
    }

    public void DisconnectedFromServer()
    {
        if (Netick.Unity.Network.IsRunning)
        {
            Debug.Log("Shutting Down Netick....");
            Netick.Unity.Network.Shutdown();
        }
    }

    public void OnNetickServerStarted()
    {
        if (CurrentLobby.Owner.Id == SteamClient.SteamId)
        {
            CurrentLobby.SetGameServer(SteamClient.SteamId);
        }
    }

    public void OnNetickShutdown()
    {
        OnGameServerShutdown?.Invoke();
        if (CurrentLobby.IsOwnedBy(SteamClient.SteamId))
            CurrentLobby.SetGameServer("127.0.0.1", 0);
    }

    public static void OpenInviteFriendsMenu()
    {
        if (!SteamClient.IsValid)
            return;

        SteamFriends.OpenGameInviteOverlay(CurrentLobby.Id);
    }

    public static string GetUserName()
    {
        if (!SteamClient.IsValid)
            return "";

        return SteamClient.Name;
    }
}