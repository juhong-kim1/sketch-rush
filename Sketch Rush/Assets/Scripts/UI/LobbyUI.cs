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

    private bool isReady = false;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerListItemPrefab;

    private NetworkManager networkManager;
    private List<GameObject> playerListItems = new List<GameObject>();

    void Awake()
    {
        networkManager = FindAnyObjectByType<NetworkManager>();

        GameEventSystem.Subscribe("OnPlayerReadyChanged", OnPlayerReadyChanged);
    }

    private void OnDestroy()
    {
        GameEventSystem.Unsubscribe("OnPlayerReadyChanged", OnPlayerReadyChanged);
    }

    void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            // AutomaticallySyncScene 설정
            if (!PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.AutomaticallySyncScene = false;
            }
            else
            {
                PhotonNetwork.AutomaticallySyncScene = true;
            }

            // Room Panel 표시
            ShowPanel("Room");
            roomNameText.text = $"Room: {PhotonNetwork.CurrentRoom.Name}";

            if (PhotonNetwork.IsMasterClient)
            {
                isReady = true;
                networkManager.SetPlayerReady(true);
            }
            else
            {
                isReady = false;
                networkManager.SetPlayerReady(false);
            }

            UpdatePlayerList();
            UpdateStartButton();
        }
        else
        {
            // Nickname Panel 표시
            ShowPanel("Nickname");
        }

        // 버튼 리스너
        confirmButton.onClick.AddListener(OnConfirmNickname);
        createRoomButton.onClick.AddListener(OnCreateRoom);
        joinRandomButton.onClick.AddListener(OnJoinRandom);
        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(OnStartOrReadyClick);
        leaveButton.onClick.AddListener(OnLeaveRoom);

        // 초기 패널
        //ShowPanel("Nickname");
    }

    // ===== 패널 전환 =====
    private void ShowPanel(string panel)
    {
        if (nicknamePanel == null || lobbyPanel == null || roomPanel == null)
            return;

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

        // Ready 상태 초기화
        if (PhotonNetwork.IsMasterClient)
        {
            isReady = true;
            networkManager.SetPlayerReady(true);
        }
        else
        {
            isReady = false;
            networkManager.SetPlayerReady(false);
        }

        UpdatePlayerList();
        UpdateStartButton();
    }

    private void OnPlayerReadyChanged(object data)
    {
        UpdatePlayerList();
        UpdateStartButton();
    }
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // Ready 상태 초기화
        isReady = false;
        networkManager.SetPlayerReady(false);

        UpdateStartButton();
    }

    public override void OnLeftRoom()
    {
        if (this == null || !gameObject.activeInHierarchy)
            return;

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

            bool isPlayerReady = networkManager.GetPlayerReady(player);
            if (isPlayerReady)
                playerName += " ✓";

            text.text = playerName;
            
            Button kickButton = item.GetComponentInChildren<Button>();
            if (kickButton != null)
            {
                bool canKick = PhotonNetwork.IsMasterClient && player != PhotonNetwork.LocalPlayer;
                kickButton.gameObject.SetActive(canKick);

                if (canKick)
                {
                    Player targetPlayer = player;
                    kickButton.onClick.RemoveAllListeners();
                    kickButton.onClick.AddListener(() => OnKickPlayer(targetPlayer));
                }
            }

            playerListItems.Add(item);
        }
    }
    private void OnKickPlayer(Player player)
    {
        Debug.Log($"[LobbyUI] Kicking player: {player.NickName}");
        networkManager.KickPlayer(player);
    }

    private void ClearPlayerList()
    {
        foreach (GameObject item in playerListItems)
            Destroy(item);
        playerListItems.Clear();
    }

    private void UpdateStartButton()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // 호스트: Start 버튼
            var buttonText = startButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "게임 시작";
            }

            // 모든 플레이어 Ready면 활성화
            bool allReady = networkManager.AreAllPlayersReady();
            startButton.interactable = allReady && PhotonNetwork.CurrentRoom.PlayerCount >= 1;
            startButton.GetComponent<Image>().color = allReady ? Color.green : Color.white;
        }
        else
        {
            // 클라이언트: Ready 버튼
            var buttonText = startButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = isReady ? "준비 취소" : "준비";
            }

            startButton.interactable = true;
            startButton.GetComponent<Image>().color = isReady ? Color.green : Color.white;
        }
    }

    private void OnStartOrReadyClick()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            networkManager.StartGame();
        }
        else
        {
            isReady = !isReady;
            networkManager.SetPlayerReady(isReady);
            UpdateStartButton();
        }
    }
}