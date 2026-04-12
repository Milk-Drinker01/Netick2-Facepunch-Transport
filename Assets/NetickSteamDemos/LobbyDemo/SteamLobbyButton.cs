using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Netick.Examples.Steam
{
    public class SteamLobbyButton : MonoBehaviour
    {
        public Text LobbyNameText;
        public Text LobbyLatencyText;

        public Lobby AssociatedLobby;

        private int currentGeneration = 0;

        public async void OnPress()
        {
            await SteamLobbyExample.JoinLobby(AssociatedLobby.Id);
        }

        public void SetupButton(Lobby lobby)
        {
            currentGeneration++;
            AssociatedLobby = lobby;

            LobbyNameText.text = AssociatedLobby.GetData(SteamLobbyExample.LobbyNameKey);
            EstimateLatency(currentGeneration);
        }

        async void EstimateLatency(int generation)
        {
            LobbyLatencyText.text = "-ms";
            await SteamNetworkingUtils.WaitForPingDataAsync();

            if (generation != currentGeneration || !gameObject.activeInHierarchy)    //prevent double set (because of pooling)
                return;

            string pingStr = AssociatedLobby.GetData(SteamLobbyExample.LobbyLocationKey);
            while (string.IsNullOrEmpty(pingStr))
            {
                if (generation != currentGeneration || !gameObject.activeInHierarchy)
                    return;
                AssociatedLobby.Refresh();
                await Task.Delay(1000);
                pingStr = AssociatedLobby.GetData(SteamLobbyExample.LobbyLocationKey);
            }

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
}