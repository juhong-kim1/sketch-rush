using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class LobbyUI : MonoBehaviourPunCallbacks
{
    [Header("Panels")]
    [SerializeField] private GameObject nicknamePanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;

    [Header("Nickname Panel")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button confirmButton;

    [Header("Lobby Panel")]
    [SerializeField] private TextMeshProUGUI welcomeText;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRandomButton;

    [Header("Room Panel")]
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaveButton;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerListItemPrefab;

    private NetworkManager networkManager;
    private List<GameObject> playerListItems = new List<GameObject>();

    void Awake()
    {
        networkManager = FindAnyObjectByType<NetworkManager>();
    }

    void Start()
    {
        // 버튼 리스너
        confirmButton.onClick.AddListener(OnConfirmNickname);
        createRoomButton.onClick.AddListener(OnCreateRoom);
        joinRandomButton.onClick.AddListener(OnJoinRandom);
        startButton.onClick.AddListener(OnStartGame);
        leaveButton.onClick.AddListener(OnLeaveRoom);

        // 초기 패널
        ShowPanel("Nickname");
    }

    // ===== 패널 전환 =====
    private void ShowPanel(string panel)
    {
        nicknamePanel.SetActive(panel == "Nickname");
        lobbyPanel.SetActive(panel == "Lobby");
        roomPanel.SetActive(panel == "Room");
    }

    // ===== 닉네임 =====
    private void OnConfirmNickname()
    {
        string nickname = nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(nickname))
        {
            Debug.LogWarning("[LobbyUI] Nickname is empty!");
            return;
        }

        networkManager.SetPlayerName(nickname);
        welcomeText.text = $"Welcome, {nickname}!";
        ShowPanel("Lobby");
    }

    // ===== 방 생성/입장 =====
    private void OnCreateRoom()
    {
        networkManager.CreateRoom(null); // 랜덤 방 이름
    }

    private void OnJoinRandom()
    {
        networkManager.JoinRandomRoom();
    }

    private void OnLeaveRoom()
    {
        networkManager.LeaveRoom();
    }

    private void OnStartGame()
    {
        networkManager.StartGame();
    }

    // ===== Photon 콜백 =====
    public override void OnJoinedRoom()
    {
        ShowPanel("Room");
        roomNameText.text = $"Room: {PhotonNetwork.CurrentRoom.Name}";
        UpdatePlayerList();
        UpdateStartButton();
    }

    public override void OnLeftRoom()
    {
        ShowPanel("Lobby");
        ClearPlayerList();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerList();
        UpdateStartButton();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerList();
        UpdateStartButton();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        UpdateStartButton();
    }

    // ===== 플레이어 리스트 =====
    private void UpdatePlayerList()
    {
        ClearPlayerList();

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject item = Instantiate(playerListItemPrefab, playerListContent);
            TextMeshProUGUI text = item.GetComponentInChildren<TextMeshProUGUI>();
            
            string playerName = player.NickName;
            if (player.IsMasterClient)
                playerName += " (Host)";
            
            text.text = playerName;
            playerListItems.Add(item);
        }
    }

    private void ClearPlayerList()
    {
        foreach (GameObject item in playerListItems)
            Destroy(item);
        playerListItems.Clear();
    }

    private void UpdateStartButton()
    {
        // 마스터만 Start 버튼 활성화 + 3명 이상
        bool canStart = PhotonNetwork.IsMasterClient && 
                        PhotonNetwork.CurrentRoom.PlayerCount >= 3;
        startButton.interactable = canStart;
    }
}