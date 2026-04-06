using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Collections;

public class LobbyUI : MonoBehaviourPunCallbacks
{
    [Header("Panels")]
    [SerializeField] private GameObject nicknamePanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private GameObject gameStartPanel;

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
    [SerializeField] private Button createPrivateRoomButton;
    [SerializeField] private Button joinPrivateRoomButton;
    [SerializeField] private GameObject privateRoomJoinPanel;
    [SerializeField] private TMP_InputField privateRoomCodeInput;
    [SerializeField] private Button confirmPrivateJoinButton;

    [Header("GameStart Panel")]
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button gameEndButton;

    [Header("Intro Animation")]
    [SerializeField] private CanvasGroup logoCanvasGroup;
    [SerializeField] private GameObject gameStartButtonObj;
    [SerializeField] private GameObject gameEndButtonObj;

    [Header("Back Buttons")]
    [SerializeField] private Button nicknameBackButton;
    [SerializeField] private Button lobbyBackButton;

    private bool isReady = false;

    [Header("Button Images")]
    [SerializeField] private Sprite startButtonSprite;
    [SerializeField] private Sprite readyButtonSprite;

    
[Header("Draw Time Settings")]
    [SerializeField] private GameObject drawTimePanel;
    [SerializeField] private Toggle drawTime5Toggle;
    [SerializeField] private Toggle drawTime7Toggle;

    private int selectedDrawTime = 5;


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
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayLobbyBGM();

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.AutomaticallySyncScene = PhotonNetwork.IsMasterClient;

            ShowPanel("Room");
            roomNameText.text = PhotonNetwork.CurrentRoom.Name;

            if (PhotonNetwork.IsMasterClient)
            {
                isReady = true;
                networkManager.SetPlayerReady(true);
                drawTimePanel.SetActive(true);
            }
            else
            {
                isReady = false;
                networkManager.SetPlayerReady(false);
                drawTimePanel.SetActive(false);
            }

            UpdatePlayerList();
            UpdateStartButton();
        }
        else
        {
            ShowPanel("GameStart");
            StartCoroutine(PlayIntroAnimation());
        }

        gameEndButton.onClick.AddListener(OnGameEnd);
        gameStartButton.onClick.AddListener(OnGameStart);
        confirmButton.onClick.AddListener(OnConfirmNickname);
        createRoomButton.onClick.AddListener(OnCreateRoom);
        joinRandomButton.onClick.AddListener(OnJoinRandom);
        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(OnStartOrReadyClick);
        leaveButton.onClick.AddListener(OnLeaveRoom);
        createPrivateRoomButton.onClick.AddListener(OnCreatePrivateRoom);
        joinPrivateRoomButton.onClick.AddListener(OnJoinPrivateRoomClick);
        confirmPrivateJoinButton.onClick.AddListener(OnConfirmPrivateJoin);
        nicknameBackButton.onClick.AddListener(OnNicknameBack);
        lobbyBackButton.onClick.AddListener(OnLobbyBack);

        drawTime7Toggle.isOn = false;
        drawTime5Toggle.isOn = true;
        selectedDrawTime = 5;
        drawTime5Toggle.onValueChanged.AddListener(on5 =>
        {
            if (on5) { selectedDrawTime = 5; UpdateRoomDrawTime(5); }
        });
        drawTime7Toggle.onValueChanged.AddListener(on7 =>
        {
            if (on7) { selectedDrawTime = 7; UpdateRoomDrawTime(7); }
        });
    }

    private void OnGameStart()
    {
        Debug.Log("[LobbyUI] Game Start clicked");

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPencilButtonClick();
        }
        
        // Photon 연결 시작
        networkManager.ConnectToPhoton();
        
        // Nickname Panel로 전환
        ShowPanel("Nickname");
    }

    private void OnGameEnd()
    {
        Debug.Log("[LobbyUI] Game End clicked");

        // 바로 종료 (Photon 연결 전)
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif

    }

    // ===== 패널 전환 =====
    private void ShowPanel(string panel)
    {
        if (nicknamePanel == null || lobbyPanel == null || roomPanel == null)
            return;

        gameStartPanel.SetActive(panel == "GameStart");
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

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayCorrectAnswer();
        }

        networkManager.SetPlayerName(nickname);
        welcomeText.text = $"Welcome, {nickname}!";
        ShowPanel("Lobby");
    }

    public void OnConfirmNickname(string nickname)
    {
        OnConfirmNickname();
    }

    // ===== 방 생성/입장 =====
private void OnCreateRoom()
    {
        networkManager.CreateRoom(null, selectedDrawTime);
    }

    private void OnJoinRandom()
    {
        networkManager.JoinRandomRoom();
    }

    private void OnLeaveRoom()
    {
        networkManager.LeaveRoom();
    }

private void UpdateRoomDrawTime(int drawTime)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

        var props = new ExitGames.Client.Photon.Hashtable { { "DrawTime", (float)drawTime } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log($"[LobbyUI] Room DrawTime updated to: {drawTime}s");
    }


    private void OnStartGame()
    {
        networkManager.StartGame();
    }

    // ===== Photon 콜백 =====
public override void OnJoinedRoom()
    {
        ShowPanel("Room");
        roomNameText.text = $"{PhotonNetwork.CurrentRoom.Name}";

        if (PhotonNetwork.IsMasterClient)
        {
            isReady = true;
            networkManager.SetPlayerReady(true);
            drawTimePanel.SetActive(true);
        }
        else
        {
            isReady = false;
            networkManager.SetPlayerReady(false);
            drawTimePanel.SetActive(false);
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
        isReady = false;
        networkManager.SetPlayerReady(false);

        if (newMasterClient == PhotonNetwork.LocalPlayer)
        {
            // 내가 새 호스트
            isReady = true;
            networkManager.SetPlayerReady(true);
            drawTimePanel.SetActive(true);
        }
        else
        {
            drawTimePanel.SetActive(false);
        }

        UpdatePlayerList();
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
                //playerName += " ✓";
                playerName += " 준비";

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
        var buttonImage = startButton.GetComponent<UnityEngine.UI.Image>();
        var canvasGroup = startButton.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = startButton.gameObject.AddComponent<CanvasGroup>();

        if (PhotonNetwork.IsMasterClient)
        {
            if (buttonImage != null && startButtonSprite != null)
                buttonImage.sprite = startButtonSprite;

            bool allReady = networkManager.AreAllPlayersReady();
            startButton.interactable = allReady && PhotonNetwork.CurrentRoom.PlayerCount >= 2;
            canvasGroup.alpha = startButton.interactable ? 1f : 0.4f;
        }
        else
        {
            if (buttonImage != null && readyButtonSprite != null)
                buttonImage.sprite = readyButtonSprite;

            startButton.interactable = true;
            canvasGroup.alpha = isReady ? 0.4f : 1f;
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

private void OnCreatePrivateRoom()
    {
        string code = networkManager.CreatePrivateRoom(selectedDrawTime);
        GUIUtility.systemCopyBuffer = code;
        Debug.Log($"[LobbyUI] Private room code: {code}");
    }

    private void OnJoinPrivateRoomClick()
    {
        privateRoomJoinPanel.SetActive(true);
    }

    private void OnConfirmPrivateJoin()
    {
        string code = privateRoomCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;
        
        networkManager.JoinPrivateRoom(code);
        privateRoomJoinPanel.SetActive(false);
    }

    public void OnConfirmPrivateJoin(string code)
    {
        OnConfirmPrivateJoin();
    }

    IEnumerator PlayIntroAnimation()
    {
        gameStartButtonObj.SetActive(false);
        gameEndButtonObj.SetActive(false);

        logoCanvasGroup.alpha = 0f;
        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            logoCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        logoCanvasGroup.alpha = 1f;
        yield return new WaitForSeconds(0.3f);

        yield return StartCoroutine(PopButton(gameStartButtonObj));
        yield return StartCoroutine(PopButton(gameEndButtonObj));
    }

    IEnumerator PopButton(GameObject button)
    {
        button.SetActive(true);
        button.transform.localScale = Vector3.zero;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.2f;
            if (t > 0.7f) scale = Mathf.Lerp(1.2f, 1f, (t - 0.7f) / 0.3f);
            button.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        button.transform.localScale = Vector3.one;
    }

    private void OnNicknameBack()
    {
        ShowPanel("GameStart");
        StartCoroutine(PlayIntroAnimation());
    }

    private void OnLobbyBack()
    {
        ShowPanel("GameStart");
        StartCoroutine(PlayIntroAnimation());
    }
}