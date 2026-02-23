using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    [SerializeField] private string gameVersion = "1.0";
    [SerializeField] private byte maxPlayersPerRoom = 6;

    private void Awake()
    {
        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = "kr";
    
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    private void Start()
    {
        ConnectToPhoton();
    }

    public void ConnectToPhoton()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("[NetworkManager] Connecting to Photon...");
            PhotonNetwork.GameVersion = gameVersion;
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[NetworkManager] Connected to Photon Master Server");
        // 로비 입장 (방 목록 받기 위해)
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[NetworkManager] Joined Lobby");
    }

    // ===== 방 생성/입장 =====
    public void CreateRoom(string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            roomName = "Room_" + Random.Range(1000, 9999);
        }

        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true
        };

        Debug.Log($"[NetworkManager] Creating room: {roomName}");
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinRoom(string roomName)
    {
        Debug.Log($"[NetworkManager] Joining room: {roomName}");
        PhotonNetwork.JoinRoom(roomName);
    }

    public void JoinRandomRoom()
    {
        Debug.Log("[NetworkManager] Joining random room...");
        PhotonNetwork.JoinRandomRoom();
    }

    public void LeaveRoom()
    {
        Debug.Log("[NetworkManager] Leaving room...");
        PhotonNetwork.LeaveRoom();
    }

    public override void OnJoinedRoom()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPencilButtonClick();
        }

        Debug.Log($"[NetworkManager] Joined room: {PhotonNetwork.CurrentRoom.Name}");
        Debug.Log($"[NetworkManager] Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[NetworkManager] Join random failed: {message}");
        CreateRoom(null);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[NetworkManager] Create room failed: {message}");
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[NetworkManager] Left room");
    }

    // ===== 플레이어 =====
    public void SetPlayerName(string playerName)
    {
        PhotonNetwork.NickName = playerName;
        Debug.Log($"[NetworkManager] Nickname set: {playerName}");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[NetworkManager] Player joined: {newPlayer.NickName}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[NetworkManager] Player left: {otherPlayer.NickName}");
    }

    public void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[NetworkManager] Only master can start game!");
            return;
        }

        if (PhotonNetwork.CurrentRoom.PlayerCount < 1)
        {
            Debug.LogWarning("[NetworkManager] Need at least 3 players!");
            return;
        }

        Debug.Log("[NetworkManager] Starting game...");

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.LoadLevel("MainScene");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[NetworkManager] Disconnected: {cause}");
    }

    public void SetPlayerReady(bool isReady)
    {
        // Custom Properties에 저장
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
    {
        { "IsReady", isReady }
    };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log($"[NetworkManager] Set ready: {isReady}");
    }

    public bool GetPlayerReady(Player player)
    {
        if (player.CustomProperties.ContainsKey("IsReady"))
        {
            return (bool)player.CustomProperties["IsReady"];
        }
        return false;
    }

    public bool AreAllPlayersReady()
    {
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.IsMasterClient)
                continue;

            if (!GetPlayerReady(player))
                return false;
        }
        return true;
    }

    // Custom Properties 변경 시 콜백
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey("IsReady"))
        {
            Debug.Log($"[NetworkManager] {targetPlayer.NickName} ready status changed");
            // LobbyUI에게 알림
            GameEventSystem.Publish("OnPlayerReadyChanged", targetPlayer);
        }
    }

    public void KickPlayer(Player player)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[NetworkManager] Only master can kick players!");
            return;
        }

        if (player == PhotonNetwork.LocalPlayer)
        {
            Debug.LogWarning("[NetworkManager] Cannot kick yourself!");
            return;
        }

        Debug.Log($"[NetworkManager] Kicking player: {player.NickName}");

        photonView.RPC("RPC_KickPlayer", RpcTarget.All, player.ActorNumber);
    }

    [PunRPC]
    void RPC_KickPlayer(int actorNumber)
    {
        Debug.Log($"[NetworkManager] RPC_KickPlayer called for actor {actorNumber}, my actor: {PhotonNetwork.LocalPlayer.ActorNumber}");

        if (PhotonNetwork.LocalPlayer.ActorNumber == actorNumber)
        {
            Debug.Log("[NetworkManager] I'm being kicked! Leaving room...");
            PhotonNetwork.LeaveRoom();
        }
    }

    public string CreatePrivateRoom()
{
    string code = Random.Range(10000000, 99999999).ToString();
    
    RoomOptions roomOptions = new RoomOptions
    {
        MaxPlayers = maxPlayersPerRoom,
        IsVisible = false,
        IsOpen = true
    };

    PhotonNetwork.CreateRoom(code, roomOptions);
    return code;
}

public void JoinPrivateRoom(string code)
{
    PhotonNetwork.JoinRoom(code);
}
}