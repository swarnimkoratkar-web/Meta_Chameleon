using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Analytics;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

public class LobbyManager : NetworkBehaviour
{
    [Header("Lobby UI Panel Overlay")] public GameObject lobbyPanel;

    [Header("Lobby Displays")]
    public TextMeshProUGUI roomCodeText;
    public Transform playerListContainer;
    public GameObject playerEntryPrefab;
    public TMP_InputField joinCodeInput;
    private string joinedLobbyId;

    [Header("Public Matchmaking UIs")]
    public TMP_InputField publicLobbyNameInput;
    public Transform lobbyListContainer;
    public GameObject lobbyCardPrefab;
    private Lobby hostedLobby;

    private bool isRefreshingLobbyList;
    private float nextRefreshTime;

    [Header("Host Controls")]
    public Button startMatchButton;

    [Header("Connection Status UI")]
    public TextMeshProUGUI connectionStatusText;
    public Button hostLobbyButton;
    public Button joinLobbyButton;
    public Button playWithFriendsBackButton;
    public Button findMatchBackButton;
    public Button findJoinTabButton;
    public Button findCreateTabButton;
    public Button hostPublicLobbyButton;
    public TMP_Text createLobbyStatusText;
    public TMP_Text joinLobbyStatusText;
    private System.Threading.CancellationTokenSource lobbyCancellationSource;

    [Header("Match Size Overrides")]
    public TMP_Dropdown friendsMaxPlayersDropdown;
    public TMP_Dropdown publicMaxPlayersDropdown;
    public TMP_Dropdown publicHidingDropdown;
    public TMP_Dropdown publicHuntingDropdown;
    public TMP_Dropdown friendsHidingDropdown;
    public TMP_Dropdown friendsHuntingDropdown;
    public TMP_Dropdown friendsMapDropdown;
    public TMP_Dropdown publicMapDropdown;

    [Header("Map Previews")]
    public Image friendsMapPreviewImage;
    public Image publicMapPreviewImage;
    public Sprite[] mapPreviewSprites;

    public NetworkList<PlayerLobbyData> connectedPlayers;
    private bool isStartingMatch = false; // Guard flag against double start match clicks
    private bool isPendingHost = false;    // Tracks hosting intentions while waiting for old shutdowns
    private bool isPendingJoin = false;    // Tracks join intentions while waiting for old shutdowns
   // private int nextPlayerNumber = 1;      // Persistent incrementing counter so names never repeat
    private bool isJoiningPublicLobby;
    private bool isCreatingPublicLobby;
    private Coroutine botFillRoutine;
    private const int MAX_ROOM_CAPACITY = 8;
    private const int MAX_BOT_LIMIT = 4;
    private const int TARGET_TOTAL_WITH_BOTS = 5;
    private static readonly string[] botNames = { "Minjun", "Jiwoo", "Dohyun", "Seoyeon", "Kenji", "Aarav", "Mateo", "Lucas", "Elena", "Camille", "K-Ghost", "ShadowFox", "PixelNinja", "NeonPulse", "Vortex99", "Zenith", "Blaze_X", "CamoKing" };
    private int selectedLobbyMaxCapacity = 8;
    public static LobbyManager Instance { get; private set; }

    public struct PlayerLobbyData : INetworkSerializable, System.IEquatable<PlayerLobbyData>
    {
        public FixedString32Bytes PlayerName;
        public ulong ClientId;
        public int ProfileId;
        public bool IsBot;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref ProfileId);
            serializer.SerializeValue(ref IsBot);
        }

        public bool Equals(PlayerLobbyData other)
        {
            return ClientId == other.ClientId;
        }
    }
    private IEnumerator BotLobbyFillRoutine()
    {
        WaitForSeconds delay = new WaitForSeconds(3.5f);
        ulong botIdCounter = 9000;

        while (IsServer)
        {
            yield return delay;

            int botCount = 0;
            for (int i = 0; i < connectedPlayers.Count; i++)
            {
                if (connectedPlayers[i].IsBot) botCount++;
            }

            // Cap strictly at the user's selected dropdown size
            if (connectedPlayers.Count < selectedLobbyMaxCapacity && botCount < MAX_BOT_LIMIT)
            {
                string botName = botNames[botCount % botNames.Length];
                int randomProfile = Random.Range(0, PlayerProfileManager.Instance.profileSprites.Length);

                connectedPlayers.Add(new PlayerLobbyData
                {
                    PlayerName = botName,
                    ClientId = botIdCounter++,
                    ProfileId = randomProfile,
                    IsBot = true
                });
            }
        }
    }
    private void Awake()
    {
        Instance = this;
        connectedPlayers = new NetworkList<PlayerLobbyData>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    private async void Start()
    {
        /* voice chat if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }*/
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (startMatchButton != null) startMatchButton.gameObject.SetActive(false);

        connectedPlayers.OnListChanged += OnPlayerListChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleGlobalDisconnect;
            NetworkManager.Singleton.OnClientStopped += OnNetworkSessionStopped;
            NetworkManager.Singleton.OnServerStarted += HandleServerStartedResetList;
        }

        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
#pragma warning disable CS0618
                AnalyticsService.Instance.StartDataCollection();
#pragma warning restore CS0618
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            ClickLeaveLobby();
        }

        // 🟢 ADD HERE:
        if (friendsMapDropdown != null)
        {
            friendsMapDropdown.onValueChanged.AddListener(index => UpdateMapPreview(friendsMapPreviewImage, index));
            UpdateMapPreview(friendsMapPreviewImage, friendsMapDropdown.value);
        }

        if (publicMapDropdown != null)
        {
            publicMapDropdown.onValueChanged.AddListener(index => UpdateMapPreview(publicMapPreviewImage, index));
            UpdateMapPreview(publicMapPreviewImage, publicMapDropdown.value);
        }
    }
    private void UpdateMapPreview(Image targetImage, int mapIndex)
    {
        if (targetImage != null && mapPreviewSprites != null && mapIndex < mapPreviewSprites.Length)
        {
            targetImage.sprite = mapPreviewSprites[mapIndex];
        }
    }
    private async Task<bool> EnsureOnlineAndAuthenticated()
    {
        // 1. Check physical internet access
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            return false;
        }

        // 2. Re-initialize Unity Services if offline boot failed previously
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            try
            {
                await UnityServices.InitializeAsync();
            }
            catch
            {
                return false;
            }
        }

        // 3. Ensure Anonymous Sign-In is active
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch
            {
                return false;
            }
        }

        return true;
    }
    public void LogMatchCompleted(bool isPrivateLobby)
    {
        CustomEvent matchEvent = new CustomEvent("match_completed")
    {
        { "lobby_type", isPrivateLobby ? "private" : "public" }
    };

        AnalyticsService.Instance.RecordEvent(matchEvent);
    }
    private void HandleServerStartedResetList()
    {
        if (!IsServer) return;

        connectedPlayers.Clear();

        string myName = (PlayerProfileManager.Instance != null && !string.IsNullOrEmpty(PlayerProfileManager.Instance.PlayerName))
            ? PlayerProfileManager.Instance.PlayerName
            : "Host Player";

        connectedPlayers.Add(new PlayerLobbyData
        {
            PlayerName = myName,
            ClientId = NetworkManager.Singleton.LocalClientId,
            ProfileId = PlayerProfileManager.Instance != null ? PlayerProfileManager.Instance.SelectedProfileIndex : 0,
            IsBot = false
        });
    }
    public async void ClickHostLobby()
    {
        if (NetworkManager.Singleton == null) return;

        SetPlayWithFriendsButtons(false);

        if (!await EnsureOnlineAndAuthenticated())
        {
            if (connectionStatusText != null) connectionStatusText.text = "No Internet Connection!";
            SetPlayWithFriendsButtons(true);
            return;
        }

        if (connectionStatusText != null) connectionStatusText.text = "Host Status: In Progress...";

        // If a previous session is closing or active, wait until it fully finishes shutting down
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(WaitShutdownAndExecute(() => ExecuteHostRelayAllocation()));
        }
        else
        {
            ExecuteHostRelayAllocation();
        }
    }

    private void OnNetworkSessionStopped(bool isHost)
    {
        // Session fully stopped; reset UI states cleanly if not transitioning
        CleanUpLobbyUIState();
    }

    private async void ExecuteHostRelayAllocation()
    {

        if (lobbyCancellationSource != null) lobbyCancellationSource.Dispose();
        lobbyCancellationSource = new System.Threading.CancellationTokenSource();
        var cancelToken = lobbyCancellationSource.Token;

        try
        {
            int maxPlayers = GetMaxPlayersFromDropdown(friendsMaxPlayersDropdown);
            selectedLobbyMaxCapacity = maxPlayers; // <-- ADD THIS
            MainMenuData.SelectedHidingTime = GetTimeFromDropdown(friendsHidingDropdown, 100f);
            MainMenuData.SelectedHuntingTime = GetTimeFromDropdown(friendsHuntingDropdown, 240f);
            MainMenuData.SelectedMapScene = GetMapSceneFromDropdown(friendsMapDropdown, "GameplayScene");

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp != null) utp.SetRelayServerData(default);

            Allocation allocation = null;
            int maxRetries = 3;

            // Retry loop handles temporary Relay 500 cloud allocation errors
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var allocTask = RelayService.Instance.CreateAllocationAsync(maxPlayers);
                    if (await WaitWithTimeout(allocTask, 10))
                    {
                        allocation = allocTask.Result;
                        break;
                    }
                }
                catch (RelayServiceException ex) when (attempt < maxRetries)
                {
                    Debug.LogWarning($"[Relay] Allocation attempt {attempt} failed ({ex.Message}). Retrying in 1s...");
                    await Task.Delay(1000);
                }
            }

            if (allocation == null)
            {
                if (connectionStatusText != null) connectionStatusText.text = "Host Status: Timed Out!";
                SetPlayWithFriendsButtons(true);
                return;
            }

            if (cancelToken.IsCancellationRequested)
            {
                if (connectionStatusText != null) connectionStatusText.text = "Hosting Cancelled.";
                SetPlayWithFriendsButtons(true); // 🟢 Re-enable buttons on cancel
                return;
            }

            string relayCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "udp"));

            bool started = NetworkManager.Singleton.StartHost();

            if (!started)
            {
                if (connectionStatusText != null) connectionStatusText.text = "Failed to start Host!";
                SetPlayWithFriendsButtons(true);
                return;
            }
            MainMenuData.IsFriendsMode = true;
            // voice chat if (VoiceChatManager.Instance != null) VoiceChatManager.Instance.ActiveVoiceChannelToken = relayCode;
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(true);
                startMatchButton.interactable = false; // 🎯 Locked until player 2 joins!
            }
            if (roomCodeText != null) roomCodeText.text = "CODE: " + relayCode;
            if (connectionStatusText != null) connectionStatusText.text = "Host Status: Success!";

            //nextPlayerNumber = 1;
            connectedPlayers.OnListChanged -= OnPlayerListChanged;
            connectedPlayers.OnListChanged += OnPlayerListChanged;
            connectedPlayers.Clear();

            connectedPlayers.Add(new PlayerLobbyData
            {
                PlayerName = PlayerProfileManager.Instance.PlayerName,
                ClientId = NetworkManager.Singleton.LocalClientId,
                ProfileId = PlayerProfileManager.Instance.SelectedProfileIndex,
                IsBot = false
            });
          //  if (botFillRoutine != null) StopCoroutine(botFillRoutine);
           // botFillRoutine = StartCoroutine(BotLobbyFillRoutine());
            isCreatingPublicLobby = false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Relay Host Crash: {e.Message}");
            if (connectionStatusText != null) connectionStatusText.text = "Host Status: Failed!";
            SetPlayWithFriendsButtons(true);
        }
    }

    public async void ClickJoinLobby()
    {
        string inputCode = joinCodeInput.text.Trim().Replace(" ", "").ToUpper();

        if (string.IsNullOrEmpty(inputCode) || inputCode.Length < 6 || NetworkManager.Singleton == null)
            return;

        MainMenuData.IsFriendsMode = true;
        SetPlayWithFriendsButtons(false);

        if (!await EnsureOnlineAndAuthenticated())
        {
            if (connectionStatusText != null) connectionStatusText.text = "No Internet Connection!";
            SetPlayWithFriendsButtons(true);
            return;
        }

        if (connectionStatusText != null) connectionStatusText.text = "Join Status: Connecting...";

        // If a previous session is closing or active, wait until it fully finishes shutting down
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(WaitShutdownAndExecute(() => ExecuteJoinRelay(inputCode)));
        }
        else
        {
            ExecuteJoinRelay(inputCode);
        }
    }
    private IEnumerator WaitShutdownAndExecute(System.Action onComplete)
    {
        if (NetworkManager.Singleton != null &&
           (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();

            while (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            {
                yield return null;
            }
        }

        onComplete?.Invoke();
    }



    private IEnumerator JoinAfterShutdown(string code)
    {
        NetworkManager.Singleton.Shutdown();

        while (NetworkManager.Singleton.IsListening ||
               NetworkManager.Singleton.IsClient ||
               NetworkManager.Singleton.IsServer)
        {
            yield return null;
        }

        isPendingJoin = false;
        ExecuteJoinRelay(code);
    }

    private async void ExecuteJoinRelay(string inputCode)
    {
        try
        {
            JoinAllocation joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(inputCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(
                    AllocationUtils.ToRelayServerData(joinAllocation, "udp"));

            NetworkManager.Singleton.OnClientConnectedCallback -= SubmitPlayerNameOnConnect;
            NetworkManager.Singleton.OnClientConnectedCallback += SubmitPlayerNameOnConnect;

            bool started = NetworkManager.Singleton.StartClient();
            if (!started)
            {
                if (joinLobbyStatusText != null)
                    joinLobbyStatusText.text = "Failed to start client.";

                SetFindMatchButtons(true, false);
                EnableAllLobbyCardJoinButtons();

                isJoiningPublicLobby = false;
                return;
            }

            // 🎯 Checks if joining via Friends Mode or Public Lobby
            if (MainMenuData.IsFriendsMode)
            {
                // voice chat if (VoiceChatManager.Instance != null)
                //     VoiceChatManager.Instance.ActiveVoiceChannelToken = inputCode;
            }
            else
            {
                // Wipe token if joining a public lobby
                if (VoiceChatManager.Instance != null)
                    VoiceChatManager.Instance.ActiveVoiceChannelToken = null;
            }

            if (lobbyPanel != null) lobbyPanel.SetActive(true);
            if (startMatchButton != null) startMatchButton.gameObject.SetActive(false);
            if (roomCodeText != null) roomCodeText.text = "CODE: " + inputCode;
            if (joinLobbyStatusText != null)
                joinLobbyStatusText.text = "Connected!";

            isJoiningPublicLobby = false;
            SetFindMatchButtons(true, false);
            EnableAllLobbyCardJoinButtons();
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);

            if (joinLobbyStatusText != null)
            {
                string msg = e.Message.ToLower();

                if (msg.Contains("404") || msg.Contains("not found"))
                    joinLobbyStatusText.text = "Lobby no longer exists!";
                else if (msg.Contains("full"))
                    joinLobbyStatusText.text = "Lobby is full!";
                else
                    joinLobbyStatusText.text = "Join Failed!";
            }
            isJoiningPublicLobby = false;
            SetFindMatchButtons(true, false);
            EnableAllLobbyCardJoinButtons();
        }
    }

    private void SetButtonsInteractable(bool state)
    {
        if (hostLobbyButton != null) hostLobbyButton.interactable = state;
        if (joinLobbyButton != null) joinLobbyButton.interactable = state;
    }

    private void SubmitPlayerNameOnConnect(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            // Request clean sequential numerical assignment from the server
            RequestLobbyJoinServerRpc(
                clientId,
                PlayerProfileManager.Instance.PlayerName,
                PlayerProfileManager.Instance.SelectedProfileIndex
            );
            NetworkManager.Singleton.OnClientConnectedCallback -= SubmitPlayerNameOnConnect;
        }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestLobbyJoinServerRpc(ulong id, string playerName, int profileId)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        playerName = playerName.Trim();

        if (playerName.Length > 9)
            playerName = playerName.Substring(0, 9);

        // Check if already in room
        for (int i = 0; i < connectedPlayers.Count; i++)
        {
            if (connectedPlayers[i].ClientId == id)
                return;
        }

        // Count real humans currently connected
        int humanCount = 0;
        for (int i = 0; i < connectedPlayers.Count; i++)
        {
            if (!connectedPlayers[i].IsBot) humanCount++;
        }

        // Reject connection if room is already completely filled with real humans
        if (humanCount >= selectedLobbyMaxCapacity)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.ContainsKey(id))
            {
                NetworkManager.Singleton.DisconnectClient(id);
            }
            return;
        }

        // If at room capacity, remove the oldest bot to make room
        if (connectedPlayers.Count >= selectedLobbyMaxCapacity || connectedPlayers.Count >= TARGET_TOTAL_WITH_BOTS)
        {
            for (int i = connectedPlayers.Count - 1; i >= 0; i--)
            {
                if (connectedPlayers[i].IsBot)
                {
                    connectedPlayers.RemoveAt(i);
                    break;
                }
            }
        }

        connectedPlayers.Add(new PlayerLobbyData
        {
            PlayerName = playerName,
            ClientId = id,
            ProfileId = profileId,
            IsBot = false
        });
    }

    private void OnPlayerListChanged(NetworkListEvent<PlayerLobbyData> changeEvent)
    {
        for (int i = playerListContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = playerListContainer.GetChild(i).gameObject;
            child.SetActive(false); 
            Destroy(child);
        }

        for (int i = 0; i < connectedPlayers.Count; i++)
        {
            string entryName = connectedPlayers[i].PlayerName.ToString();

            if (!string.IsNullOrEmpty(entryName))
            {
                GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
                PlayerEntryUI ui = entry.GetComponent<PlayerEntryUI>();

                ui.playerNameText.text = entryName;
                ui.playerTitleText.text = connectedPlayers[i].IsBot ? "Rookie" : "Rookie";

                if (PlayerProfileManager.Instance != null &&
                    connectedPlayers[i].ProfileId < PlayerProfileManager.Instance.profileSprites.Length)
                {
                    ui.profileImage.sprite = PlayerProfileManager.Instance.profileSprites[connectedPlayers[i].ProfileId];
                }

                bool isHost = !connectedPlayers[i].IsBot && connectedPlayers[i].ClientId == NetworkManager.ServerClientId;
                ui.hostBadge.text = isHost ? "HOST" : (connectedPlayers[i].IsBot ? "" : "");

                MainMenuData.SavePlayerName(connectedPlayers[i].ClientId, entryName);
            }
        }

        if (startMatchButton != null)
        {
            if (IsServer)
            {
                startMatchButton.gameObject.SetActive(true);
                startMatchButton.interactable = (connectedPlayers.Count >= 2);
            }
            else
            {
                startMatchButton.gameObject.SetActive(false);
            }
        }
    }

    private void HandleGlobalDisconnect(ulong clientId)
    {
        if (IsServer)
        {
            for (int i = 0; i < connectedPlayers.Count; i++)
            {
                if (connectedPlayers[i].ClientId == clientId)
                {
                    connectedPlayers.RemoveAt(i);
                    break;
                }
            }

            if (botFillRoutine != null)
            {
                StopCoroutine(botFillRoutine);
            }
            botFillRoutine = StartCoroutine(BotLobbyFillRoutine());
        }

        if (clientId == NetworkManager.Singleton.LocalClientId ||
            (NetworkManager.Singleton.IsClient &&
             !NetworkManager.Singleton.IsHost &&
             clientId == NetworkManager.ServerClientId))
        {
            if (botFillRoutine != null)
            {
                StopCoroutine(botFillRoutine);
                botFillRoutine = null;
            }
            CleanUpLobbyUIState();
        }
    }
    public void ClickStartMatch()
    {
        if (!IsServer || isStartingMatch) return;

        isStartingMatch = true;

        // Cache all active bots into static memory before the scene switches
        MainMenuData.CachedLobbyBots.Clear();
        for (int i = 0; i < connectedPlayers.Count; i++)
        {
            if (connectedPlayers[i].IsBot)
            {
                MainMenuData.CachedLobbyBots.Add(connectedPlayers[i]);
            }
        }

        NetworkManager.Singleton.SceneManager.LoadScene(MainMenuData.SelectedMapScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
    private IEnumerator PublicLobbyHeartbeatRoutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (hostedLobby != null && IsServer)
        {
            var task = LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return new WaitUntil(() => task.IsCompleted);
            yield return delay;
        }
    }
    public void ClickLeaveLobby()
    {
        if (botFillRoutine != null)
        {
            StopCoroutine(botFillRoutine);
            botFillRoutine = null;
        }

        isPendingHost = false;
        isPendingJoin = false;
        StartCoroutine(GracefulShutdownRoutine());
    }
    private int GetMaxPlayersFromDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return 9; // Fallback default if not assigned
        string selectedText = dropdown.options[dropdown.value].text;
        if (int.TryParse(System.Text.RegularExpressions.Regex.Match(selectedText, @"\d+").Value, out int result))
        {
            return result;
        }
        return 9; // Fallback limit
    }
    private IEnumerator GracefulShutdownRoutine()
    {
        if (botFillRoutine != null)
        {
            StopCoroutine(botFillRoutine);
            botFillRoutine = null;
        }

        if (VoiceChatManager.Instance != null)
        {
            var voiceTask = VoiceChatManager.Instance.LeaveCurrentVoiceChannel();
            yield return new WaitUntil(() => voiceTask.IsCompleted);
            VoiceChatManager.Instance.ActiveVoiceChannelToken = null;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= SubmitPlayerNameOnConnect;

            if (hostedLobby != null)
            {
                var deleteTask = LobbyService.Instance.DeleteLobbyAsync(hostedLobby.Id);
                yield return new WaitUntil(() => deleteTask.IsCompleted);

                if (deleteTask.Exception != null)
                    Debug.LogWarning(deleteTask.Exception);

                hostedLobby = null;
            }

            if (NetworkManager.Singleton.IsListening ||
                NetworkManager.Singleton.IsClient ||
                NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.Shutdown();

                while (NetworkManager.Singleton.IsListening ||
                       NetworkManager.Singleton.IsClient ||
                       NetworkManager.Singleton.IsServer)
                {
                    yield return null;
                }
            }
        }

        CleanUpLobbyUIState();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private void CleanUpLobbyUIState()
    {
        if (NetworkManager.Singleton != null)
        {
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp != null) utp.SetRelayServerData(default);
        }
        if (VoiceChatManager.Instance != null)
        {
            _ = VoiceChatManager.Instance.LeaveCurrentVoiceChannel();
            VoiceChatManager.Instance.ActiveVoiceChannelToken = null;
        }
        MainMenuData.ClearAllData();
        isStartingMatch = false;
        isPendingHost = false;
        isPendingJoin = false;

        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (startMatchButton != null) startMatchButton.gameObject.SetActive(false);
        if (joinCodeInput != null) joinCodeInput.text = "";
        foreach (Transform child in playerListContainer)
        {
            Destroy(child.gameObject);
        }

        if (connectionStatusText != null) connectionStatusText.text = "";

        if (createLobbyStatusText != null)
            createLobbyStatusText.text = "";

        if (joinLobbyStatusText != null)
            joinLobbyStatusText.text = "";
        if (startMatchButton != null)
        {
            startMatchButton.interactable = false;
            startMatchButton.gameObject.SetActive(false);
        }
        SetButtonsInteractable(true);
        SetPlayWithFriendsButtons(true);
        SetFindMatchButtons(true, true);
        EnableAllLobbyCardJoinButtons();
        // 🎯 Bulletproof UI flush: Force-clear out any active gameplay panels left behind by the engine
        if (GameMatchEngine.Instance != null)
        {
            if (GameMatchEngine.Instance.victoryPanel != null) GameMatchEngine.Instance.victoryPanel.SetActive(false);
            if (GameMatchEngine.Instance.lossPanel != null) GameMatchEngine.Instance.lossPanel.SetActive(false);
        }

        if (UIAssignmentManager.Instance != null)
        {
            if (UIAssignmentManager.Instance.hiderMasterUIGroup != null) UIAssignmentManager.Instance.hiderMasterUIGroup.SetActive(false);
            if (UIAssignmentManager.Instance.hiderPaintUIGroup != null) UIAssignmentManager.Instance.hiderPaintUIGroup.SetActive(false);
        }
    }
    private float GetTimeFromDropdown(TMP_Dropdown dropdown, float defaultTime)
    {
        if (dropdown == null) return defaultTime;
        string text = dropdown.options[dropdown.value].text;
        if (int.TryParse(System.Text.RegularExpressions.Regex.Match(text, @"\d+").Value, out int seconds))
        {
            return seconds;
        }
        return defaultTime;
    }
    public override void OnDestroy()
    {
        if (connectedPlayers != null)
            connectedPlayers.OnListChanged -= OnPlayerListChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= SubmitPlayerNameOnConnect;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleGlobalDisconnect;
            NetworkManager.Singleton.OnClientStopped -= OnNetworkSessionStopped;
            NetworkManager.Singleton.OnServerStarted -= HandleServerStartedResetList;
        }

        base.OnDestroy();
    }
    private void OnApplicationQuit()
    {
        if (hostedLobby != null)
        {
            try
            {
                // Instantly delete the room from the cloud so it doesn't linger as a ghost lobby
                LobbyService.Instance.DeleteLobbyAsync(hostedLobby.Id);
                hostedLobby = null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Lobby Service] Error deleting lobby on quit: {e.Message}");
            }
        }
    }
    public void ClickBackButtonInLobby()
    {

        // 🎯 Cancel any connection process currently waiting on Unity Services
        if (lobbyCancellationSource != null)
        {
            lobbyCancellationSource.Cancel();
            lobbyCancellationSource.Dispose();
            lobbyCancellationSource = null;
        }

        isPendingHost = false;
        isPendingJoin = false;

        // 🎯 FIX: Instead of calling the full cleanup routine immediately while the socket is closing, 
        // just shut down the network manager. OnNetworkSessionStopped will cleanly catch it when done!
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }
        else
        {
            CleanUpLobbyUIState();
        }
    }
    public async void ClickCreatePublicLobby()
    {
        if (isCreatingPublicLobby) return;

        isCreatingPublicLobby = true;
        SetFindMatchButtons(false, true);
        SetButtonsInteractable(false);

        if (NetworkManager.Singleton == null)
        {
            SetFindMatchButtons(true, true);
            SetButtonsInteractable(true);
            isCreatingPublicLobby = false;
            return;
        }

        if (!await EnsureOnlineAndAuthenticated())
        {
            if (createLobbyStatusText != null) createLobbyStatusText.text = "No Internet Connection!";
            SetFindMatchButtons(true, true);
            SetButtonsInteractable(true);
            isCreatingPublicLobby = false;
            return;
        }

        if (createLobbyStatusText != null) createLobbyStatusText.text = "Creating Lobby...";

        if (lobbyCancellationSource != null) lobbyCancellationSource.Dispose();
        lobbyCancellationSource = new System.Threading.CancellationTokenSource();
        var cancelToken = lobbyCancellationSource.Token;

        try
        {
            int maxPlayers = GetMaxPlayersFromDropdown(publicMaxPlayersDropdown);
            selectedLobbyMaxCapacity = maxPlayers; // <-- ADD THIS
            MainMenuData.SelectedHidingTime = GetTimeFromDropdown(publicHidingDropdown, 100f);
            MainMenuData.SelectedHuntingTime = GetTimeFromDropdown(publicHuntingDropdown, 240f);
            MainMenuData.SelectedMapScene = GetMapSceneFromDropdown(publicMapDropdown, "GameplayScene");

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp != null) utp.SetRelayServerData(default);

            Allocation allocation = null;
            int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var allocTask = RelayService.Instance.CreateAllocationAsync(maxPlayers);
                    if (await WaitWithTimeout(allocTask, 10))
                    {
                        allocation = allocTask.Result;
                        break;
                    }
                }
                catch (RelayServiceException ex) when (attempt < maxRetries)
                {
                    Debug.LogWarning($"[Relay] Public Allocation attempt {attempt} failed ({ex.Message}). Retrying in 1s...");
                    await Task.Delay(1000);
                }
            }

            if (allocation == null)
            {
                if (createLobbyStatusText != null) createLobbyStatusText.text = "Timed Out!";
                SetFindMatchButtons(true, true);
                SetButtonsInteractable(true);
                isCreatingPublicLobby = false;
                return;
            }
            string relayCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            if (cancelToken.IsCancellationRequested)
            {
                if (createLobbyStatusText != null) createLobbyStatusText.text = "Cancelled.";
                SetFindMatchButtons(true, true);
                SetButtonsInteractable(true);
                isCreatingPublicLobby = false;
                return;
            }

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "udp"));

            string roomName = string.IsNullOrEmpty(publicLobbyNameInput.text)
                ? "Public Match Room"
                : publicLobbyNameInput.text.Trim();

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Public, relayCode) }
                }
            };

            var task = LobbyService.Instance.CreateLobbyAsync(roomName, maxPlayers, options);

            if (!await WaitWithTimeout(task, 15))
            {
                if (createLobbyStatusText != null) createLobbyStatusText.text = "Timed Out!";
                SetFindMatchButtons(true, true);
                SetButtonsInteractable(true);
                isCreatingPublicLobby = false;
                return;
            }

            hostedLobby = task.Result;

            if (cancelToken.IsCancellationRequested)
            {
                try { await LobbyService.Instance.DeleteLobbyAsync(hostedLobby.Id); } catch { }
                hostedLobby = null;
                if (createLobbyStatusText != null) createLobbyStatusText.text = "Cancelled.";
                SetFindMatchButtons(true, true);
                SetButtonsInteractable(true);
                isCreatingPublicLobby = false;
                return;
            }

            bool started = NetworkManager.Singleton.StartHost();

            if (!started)
            {
                if (createLobbyStatusText != null) createLobbyStatusText.text = "Failed to start Host!";
                try { await LobbyService.Instance.DeleteLobbyAsync(hostedLobby.Id); } catch { }
                hostedLobby = null;
                SetFindMatchButtons(true, true);
                SetButtonsInteractable(true);
                isCreatingPublicLobby = false;
                return;
            }

            StartCoroutine(PublicLobbyHeartbeatRoutine(hostedLobby.Id, 6f));
            MainMenuData.IsFriendsMode = false;
            if (VoiceChatManager.Instance != null) VoiceChatManager.Instance.ActiveVoiceChannelToken = null;

            if (lobbyPanel != null) lobbyPanel.SetActive(true);
            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(true);
                startMatchButton.interactable = false;
            }
            if (roomCodeText != null) roomCodeText.text = "PUBLIC: " + roomName;
            if (createLobbyStatusText != null) createLobbyStatusText.text = "Lobby Created!";

            // Start bot filler routine on host for public lobby
            if (botFillRoutine != null) StopCoroutine(botFillRoutine);
            botFillRoutine = StartCoroutine(BotLobbyFillRoutine());

            SetFindMatchButtons(true, true);
            SetButtonsInteractable(true);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            if (createLobbyStatusText != null) createLobbyStatusText.text = "Creation Failed!";
            SetFindMatchButtons(true, true);
            SetButtonsInteractable(true);
        }
        finally
        {
            isCreatingPublicLobby = false;
        }
    }

    public async void RefreshPublicLobbyList()
    {
        if (isRefreshingLobbyList)
            return;

        if (Time.unscaledTime < nextRefreshTime)
            return;

        isRefreshingLobbyList = true;
        nextRefreshTime = Time.unscaledTime + 3f;

        if (lobbyListContainer == null || lobbyCardPrefab == null)
        {
            isRefreshingLobbyList = false;
            return;
        }

        // Clear previous lobby cards
        foreach (Transform child in lobbyListContainer)
        {
            Destroy(child.gameObject);
        }

        try
        {
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
        {
            new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
        }
            };

            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

            foreach (Lobby lobby in queryResponse.Results)
            {
                GameObject cardInstance = Instantiate(lobbyCardPrefab, lobbyListContainer);

                TextMeshProUGUI[] cardTexts = cardInstance.GetComponentsInChildren<TextMeshProUGUI>();

                if (cardTexts.Length >= 2)
                {
                    cardTexts[0].text = lobby.Name;

                    // 🎯 Dynamically calculates total slots left based on the dropdown limit set by the host
                    int currentConnected = lobby.MaxPlayers - lobby.AvailableSlots;
                    cardTexts[1].text = $"{currentConnected} / {lobby.MaxPlayers}";
                }

                Button cardJoinButton = cardInstance.GetComponentInChildren<Button>();

                if (cardJoinButton != null)
                {
                    cardJoinButton.onClick.RemoveAllListeners();
                    cardJoinButton.onClick.AddListener(() => ConnectToPublicLobby(lobby));
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Lobby Fetch Failed: {e.Message}");
        }
        finally
        {
            isRefreshingLobbyList = false;
        }
    }

    // ==============================================================================
    // 🚀 STEP 5: DE-SERIALIZE AND LAUNCH TRANSPORT DRIVER FOR SELECTED PUBLIC LOBBY
    // ==============================================================================
    private void ConnectToPublicLobby(Lobby targetLobby)
    {
        SetFindMatchButtons(false, false);
        DisableAllLobbyCardJoinButtons();

        if (targetLobby == null ||
            targetLobby.Data == null ||
            !targetLobby.Data.ContainsKey("RelayCode"))
        {
            if (joinLobbyStatusText != null)
                joinLobbyStatusText.text = "Invalid Lobby!";

            SetFindMatchButtons(true, false);
            EnableAllLobbyCardJoinButtons();
            return;
        }

        string relayCode = targetLobby.Data["RelayCode"].Value;

        VerifyAndJoinPublicLobby(targetLobby, relayCode);
    }
    private async void VerifyAndJoinPublicLobby(Lobby targetLobby, string relayCode)
    {
        if (isJoiningPublicLobby)
            return;

        isJoiningPublicLobby = true;

        try
        {
            var task = LobbyService.Instance.GetLobbyAsync(targetLobby.Id);

            if (!await WaitWithTimeout(task, 15))
            {
                if (joinLobbyStatusText != null) joinLobbyStatusText.text = "Connection Timed Out!";
                SetFindMatchButtons(true, false);
                EnableAllLobbyCardJoinButtons();
                isJoiningPublicLobby = false;
                return;
            }

            Lobby latestLobby = task.Result;

            if (latestLobby == null)
            {
                if (joinLobbyStatusText != null) joinLobbyStatusText.text = "Lobby no longer exists!";
                SetFindMatchButtons(true, false);
                EnableAllLobbyCardJoinButtons();
                isJoiningPublicLobby = false;
                return;
            }

            if (latestLobby.AvailableSlots <= 0)
            {
                if (joinLobbyStatusText != null) joinLobbyStatusText.text = "Lobby is full!";
                SetFindMatchButtons(true, false);
                EnableAllLobbyCardJoinButtons();
                isJoiningPublicLobby = false;
                return;
            }

            // Reserve a slot in the Unity Lobby
            await LobbyService.Instance.JoinLobbyByIdAsync(latestLobby.Id);

            if (joinLobbyStatusText != null) joinLobbyStatusText.text = "Joining...";
            MainMenuData.IsFriendsMode = false;

            if (NetworkManager.Singleton != null &&
               (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                isPendingJoin = true;
                StartCoroutine(JoinAfterShutdown(relayCode));
            }
            else
            {
                ExecuteJoinRelay(relayCode);
            }
        }
        catch (LobbyServiceException e)
        {
            if (joinLobbyStatusText != null)
            {
                switch (e.Reason)
                {
                    case LobbyExceptionReason.LobbyNotFound:
                        joinLobbyStatusText.text = "Lobby no longer exists!";
                        break;
                    case LobbyExceptionReason.LobbyFull:
                        joinLobbyStatusText.text = "Lobby is full!";
                        break;
                    case LobbyExceptionReason.RateLimited:
                        joinLobbyStatusText.text = "Too many requests.";
                        break;
                    case LobbyExceptionReason.ServiceUnavailable:
                        joinLobbyStatusText.text = "Lobby service unavailable.";
                        break;
                    default:
                        joinLobbyStatusText.text = "Couldn't join lobby.";
                        break;
                }
            }
            Debug.LogError(e);
            MainMenuData.IsFriendsMode = false;
            isJoiningPublicLobby = false;
            SetFindMatchButtons(true, false);
            EnableAllLobbyCardJoinButtons();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Unexpected Join Exception: {e.Message}");
            if (joinLobbyStatusText != null) joinLobbyStatusText.text = "Connection Failed!";
            MainMenuData.IsFriendsMode = false;
            isJoiningPublicLobby = false;
            SetFindMatchButtons(true, false);
            EnableAllLobbyCardJoinButtons();
        }
    }
    private void SetPlayWithFriendsButtons(bool interactable)
    {
        if (hostLobbyButton != null) hostLobbyButton.interactable = interactable;
        if (joinLobbyButton != null) joinLobbyButton.interactable = interactable;
    }
    private void SetFindMatchButtons(bool interactable, bool disableBack)
    {
        if (findJoinTabButton != null) findJoinTabButton.interactable = interactable;
        if (findCreateTabButton != null) findCreateTabButton.interactable = interactable;
        if (hostPublicLobbyButton != null) hostPublicLobbyButton.interactable = interactable;
        if (findMatchBackButton != null) findMatchBackButton.interactable = true;
    }
    private void DisableAllLobbyCardJoinButtons()
    {
        foreach (Transform child in lobbyListContainer)
        {
            Button btn = child.GetComponentInChildren<Button>();
            if (btn != null)
                btn.interactable = false;
        }
    }

    private void EnableAllLobbyCardJoinButtons()
    {
        foreach (Transform child in lobbyListContainer)
        {
            Button btn = child.GetComponentInChildren<Button>();
            if (btn != null)
                btn.interactable = true;
        }
    }
    private async Task<bool> WaitWithTimeout<T>(Task<T> task, int seconds)
    {
        var timeout = Task.Delay(seconds * 1000);
        return await Task.WhenAny(task, timeout) == task;
    }
    private string GetMapSceneFromDropdown(TMP_Dropdown dropdown, string defaultScene = "GameplayScene")
    {
        if (dropdown == null) return defaultScene;
        return dropdown.options[dropdown.value].text.Trim();
    }
}