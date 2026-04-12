using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.UI;

public class SteamLobbyButton : MonoBehaviour
{
    public Text LobbyNameText;
    public Text LobbyLatencyText;

    public Lobby AssociatedLobby;

    public async void OnPress()
    {
        await SteamLobbyExample.JoinLobby(AssociatedLobby.Id);
    }

    public void SetupButton(Lobby lobby)
    {
        AssociatedLobby = lobby;

        LobbyNameText.text = AssociatedLobby.GetData("LobbyName");
        EstimateLatency();
    }
    
    public void EstimateLatency()
    {
        string pingStr = AssociatedLobby.GetData("PingLocation");
        LobbyLatencyText.text = "-ms";

        if (string.IsNullOrEmpty(pingStr))
            return;

        var location = NetPingLocation.TryParseFromString(pingStr);
        if (!location.HasValue)
            return;

        int ping = SteamNetworkingUtils.EstimatePingTo(location.Value);
        if (ping == -1)
            return;

        //Debug.Log($"Lobby {AssociatedLobby.GetData("LobbyName")} - Est. ping: {ping}ms");
        LobbyLatencyText.text = $"{ping} ms";
    }
}
