using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("Settings")]
    [SerializeField] private string gameVersion = "1.0";
    [SerializeField] private byte maxPlayersPerRoom = 15;

    private void Awake()
    {
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
        PhotonNetwork.CurrentRoom.IsOpen = false; // 방 닫기
        PhotonNetwork.LoadLevel("MainScene"); // 모두 게임씬으로
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[NetworkManager] Disconnected: {cause}");
    }
}