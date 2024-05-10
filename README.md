# Netick Facepunch Transport
This repository contains source code of Facepunch transport as well as an example of its usage with Steam Lobby service.

## Requirements
- Unity 2021.3 or newer
- [Netick 2 for Unity](https://github.com/NetickNetworking/NetickForUnity)

## Installation
1. Download .unitypackage from the release section and import it into your project. Uncheck the Plugins folder if you already have Facepunch DLLs in your project.
2. Pass reference to `FacepunchTransportProvider` scriptable object from `Netick/Transports/Facepunch` to `StartAsHost` or and `StartAsClient` methods of Netick. [Using transport in Netick](https://netick.net/docs/2/articles/getting-started-guide/2-setting-up-the-game.html#transport)
3. FacepunchTransport requires you to initialize SteamClient before starting the server. There is a MonoBehavior named `Facepunch Initializer` that can do that for you, place it on a game object in your starting scene.

## Usage
Facepunch provides several APIs for games, including matchmaking. In order to connect using Facepunch you need to pass LobbyID (or friend's SteamID) as an address to `Connect` method of sandbox. 
```cs
var sandbox = Netick.Unity.Network.StartAsClient(Transport, Port, SandboxPrefab);
sandbox.Connect(Port, CurrentLobby.Owner.Id.ToString());
```
For a complete example of the lobby usage check `Assets/NetickSteamDemo/SteamNetick` scene. 

## Additional Documentation
[Netick](https://netick.net/docs/2) | [Facepunch](https://wiki.facepunch.com/steamworks/) | [Steamworks](https://partner.steamgames.com/doc/home)
