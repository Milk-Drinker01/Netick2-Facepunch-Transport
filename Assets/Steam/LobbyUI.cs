using Steamworks;
using Steamworks.Data;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Netick.Examples.Steam
{
    public class LobbyUI : MonoBehaviour
    {
        public static LobbyUI instance;

        public GameObject SearchMenu;
        public GameObject LobbyMenu;
        public GameObject LobbyContent;
        public GameObject LobbyInfoPrefab;
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                SteamworksUtils.OnLobbyLeftEvent.AddListener(LeftLobby);
                SteamworksUtils.OnLobbySearchStart.AddListener(ClearLobbyList);
                SteamworksUtils.OnLobbySearchFinished.AddListener(UpdateLobbyList);
                SteamworksUtils.OnGameServerShutdown.AddListener(ResetLobbyCamera);
            }
            else
                Destroy(gameObject);
        }

        bool locked;
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                toggleCursor();
        }

        void toggleCursor()
        {
            locked = !locked;
            if (locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
            }
        }

        public void ClearLobbyList()
        {
            for (int i = 0; i < LobbyContent.transform.childCount; i++)
                Destroy(LobbyContent.transform.GetChild(i).gameObject);
        }

        public void UpdateLobbyList(List<Lobby> LobbyList)
        {
            foreach (var lobby in LobbyList)
            {
                var lobbyGO = Instantiate(LobbyInfoPrefab, LobbyContent.transform);
                lobbyGO.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = lobby.GetData("LobbyName");
                lobbyGO.GetComponent<Button>().onClick.AddListener(async () => {
                    await SteamworksUtils.JoinLobby(lobby.Id);
                });
            }
        }

        public void JoinedLobby(Lobby lobby)
        {
            bool IsOwner = lobby.IsOwnedBy(SteamworksUtils.SteamID);
            if (IsOwner)
            {
                LobbyMenu.transform.GetChild(1).GetComponent<Button>().interactable = true;
                LobbyMenu.transform.GetChild(2).GetComponent<Button>().interactable = false;
            }
            else
            {
                LobbyMenu.transform.GetChild(1).GetComponent<Button>().interactable = false;
                LobbyMenu.transform.GetChild(2).GetComponent<Button>().interactable = true;
            }
            SearchMenu.SetActive(false);
            LobbyMenu.SetActive(true);
        }
        public void LeftLobby()
        {
            SearchMenu.SetActive(true);
            LobbyMenu.SetActive(false);
        }

        public void ResetLobbyCamera()
        {
            FindObjectOfType<Camera>().transform.SetParent(null);
        }
    }
}