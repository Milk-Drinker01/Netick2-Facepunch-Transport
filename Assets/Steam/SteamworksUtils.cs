using Netick;
using Steamworks;
using Steamworks.Data;
using Steamworks.ServerList;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SteamworksUtils : MonoBehaviour
{
    public static SteamworksUtils instance;

    [SerializeField] uint AppID = 480;

    [Header("Lobby Search Settings")]

    [Tooltip("this is just so that you dont find other peoples lobbies while testing with app id 480! make it unique!")]
    [SerializeField] string GameName = "Your Games Name";

    [SerializeField] DistanceFilter LobbySearchDistance = DistanceFilter.WorldWide;

    [SerializeField] int MinimumSlotsAvailable = 1;

    [Header("Steam Debug")]
    [SerializeField] bool _steamEnabled = false;

    [Header("Netick Settings")]
    [SerializeField] GameObject SandboxPrefab;

    public static SteamId SteamID => SteamClient.SteamId;
    public void Awake()
    {
        if (instance == null)
        {
            instance = this;

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
        SteamClient.Shutdown();
    }

    private void Start()
    {
        if (!SteamClient.IsValid)
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
            Debug.Log($"You joined {lobby.GetData("LobbyName")}'s fucking lobby.");
            _lobby = lobby;
            Netick.Examples.Steam.LobbyUI.instance.JoinedLobby(_lobby.IsOwnedBy(SteamClient.SteamId));
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
            Debug.LogError("Game Lobby Created");
        };
    }
    #region Lobby Stuff

    public Lobby _lobby;
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
        

        if (lobbies == null)
        {
            Debug.Log("No lobbies found");
            //CreateLobby(0);
            return;
        }

        Matches.Clear();

        Netick.Examples.Steam.LobbyUI.instance.ClearLobbyList();

        foreach (var item in lobbies)
        {
            if (item.GetData("GameName") == GameName && !(Matches.Contains(item) || item.MemberCount == 0))
                Matches.Add(item);
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

        Netick.Examples.Steam.LobbyUI.instance.UpdateLobbyList(Matches);
    }

    public void CreateLobby(int lobbyType)
    {
        _lobbyType = (LobbyType)lobbyType;

        Task.Run(async () => await SteamMatchmaking.CreateLobbyAsync());
    }

    void OnLobbyCreated(Result status, Lobby lobby)
    {
        lobby.SetData("GameName", GameName);
        lobby.SetData("LobbyName", SteamClient.Name);
        lobby.SetJoinable(true);
        lobby.MaxMembers = 16;

        _lobby = lobby;

        Debug.Log("lobby " + lobby.Id + " was created");

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
        _lobby.Leave();
        DisconnectFromServer();
        Netick.Examples.Steam.LobbyUI.instance.LeftLobby();
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
    public void StartGame()
    {
        Netick.Network.StartAsServer(4050, SandboxPrefab);
    }
    public void Connect()
    {
        var sandbox = Netick.Network.StartAsClient(4050, SandboxPrefab);
        sandbox.Connect(4050, "127.0.0.1");
    }
    public void StartGameClientAndServer()
    {
        Netick.Network.StartAsServerAndClient(4050, SandboxPrefab);
    }

    public void StopGame()
    {
        DisconnectFromServer();
    }

    public void DisconnectFromServer()
    {
        FindObjectOfType<Camera>().transform.SetParent(null);
        Netick.Network.Shutdown();
        foreach (NetworkObject go in FindObjectsOfType<NetworkObject>())
            Destroy(go.gameObject);
    }
    #endregion

}