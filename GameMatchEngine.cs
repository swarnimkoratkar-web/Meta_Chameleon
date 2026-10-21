using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameMatchEngine : NetworkBehaviour
{   public static GameMatchEngine Instance { get; private set; }
    public enum MatchState { Lobby, Hiding, Hunting, AnswerCheck, PostGame }
    public NetworkVariable<MatchState> CurrentState = new NetworkVariable<MatchState>(MatchState.Lobby);

    [Header("Match Timers (Seconds)")]
    public NetworkVariable<float> netHidingDuration = new NetworkVariable<float>(100f);
    public NetworkVariable<float> netHuntingDuration = new NetworkVariable<float>(240f);
    public float answerCheckDuration = 15f;
    public float postGameDuration = 3f;
    private float localTimerDisplay = 0f;
    [Header("Global Netcode Prefabs")]
    public GameObject hiderPrefab;
    public GameObject hunterPrefab;

    [Header("Loading System")]
    public GameObject fallbackSceneCamera;
    public GameObject loadingPanel;
    public float minLoadingScreenDuration = 4f; 
    private bool isLoadingFinished = false;
    public Slider loadingSlider;
    public TextMeshProUGUI loadingTipText; 
    [TextArea(2, 4)]
    public string[] loadingTips;
    [Header("Spawn Locations")]
    public Transform hiderSpawnPoint;
    public Transform hunterWaitingArea;
    public Transform hunterArenaSpawnPoint;

    [Header("Match End Audio")]
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip loseClip;
    private AudioSource audioSource;
   
    [Header("Environment Optimization Sections")]
    public GameObject mainMapSection;     
    public GameObject hunterWaitingSection;


    [Header("Global UI References")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI statusText;
    public GameObject victoryPanel;
    public GameObject lossPanel;
    [Header("Player Count UI References")]
    public TextMeshProUGUI hiderCountText;
    public TextMeshProUGUI hunterCountText;

    [Header("Panel Score Text Fields")]
    public TextMeshProUGUI victoryPanelScoreText;
    public TextMeshProUGUI lossPanelScoreText;
    public TextMeshProUGUI victoryHunterScoreText;
    public TextMeshProUGUI lossHunterScoreText;

    public TextMeshProUGUI localPlayerScoreText; 
    public TextMeshProUGUI victoryLocalScoreText;
    public TextMeshProUGUI lossLocalScoreText;   

    [Header("Leaderboard UI")]
    public TextMeshProUGUI leaderboardText;
    private Dictionary<ulong, int> playerScores = new Dictionary<ulong, int>();
    private Dictionary<ulong, int> hunterScores = new Dictionary<ulong, int>();
    private Dictionary<ulong, bool> originalIsHiderMap = new Dictionary<ulong, bool>();
    private float scoreTickTimer = 0f;

    [Header("Whistle Settings")]
    public TextMeshProUGUI whistleTimerText;
    private NetworkVariable<float> whistleCountdown = new NetworkVariable<float>(20f);
    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(0f);

    public List<ulong> currentHiders = new List<ulong>();
    private List<ulong> currentHunters = new List<ulong>();
    private bool loopEnded = false;
    private bool matchStarted = false;
    private HashSet<ulong> disconnectingClients = new HashSet<ulong>();
    private bool isShuttingDown = false;
    [Header("Bot Prefabs")]
    public GameObject botHiderPrefab;
    public GameObject botHunterPrefab;
    [Header("Solo / Debug Role Override")]
    [Tooltip("If true and playing solo with bots, forces the local player to be Hider.")]
    public bool forceSoloHider = true;
    // Tracks active bot network objects by their assigned virtual ClientId
    private Dictionary<ulong, NetworkObject> activeBotObjects = new Dictionary<ulong, NetworkObject>();

    private void Awake()
    {
        Instance = this;
        audioSource = GetComponent<AudioSource>();
    }
    public override void OnNetworkSpawn()
    {
        if (LevelPlayAdManager.Instance != null)
        {
            LevelPlayAdManager.Instance.LoadInterstitial();
        }
        timeRemaining.OnValueChanged += UpdateTimerUI;
        CurrentState.OnValueChanged += HandleStateChangeUI;
        whistleCountdown.OnValueChanged += UpdateWhistleTimerUI;
        CurrentState.OnValueChanged += OnMatchStateChangedVisibility;
        timeRemaining.OnValueChanged += (oldVal, newVal) => EvaluateLocalVisibility(CurrentState.Value);
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandlePlayerDisconnect;
        }

        if (IsServer)
        {
            netHidingDuration.Value = MainMenuData.SelectedHidingTime;
            netHuntingDuration.Value = MainMenuData.SelectedHuntingTime;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleAllClientsLoaded;
        }
        else
        {
            EvaluateLocalVisibility(CurrentState.Value);
        }
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStopped += OnLocalClientSessionStopped;
        }
    }
    private void Update()
    {
        if (!IsServer)
        {
            if (localTimerDisplay > 0)
            {
                localTimerDisplay -= Time.deltaTime;

                // Just update UI text directly here — do NOT call UpdateTimerUI!
                if (timerText != null)
                {
                    timerText.text = Mathf.CeilToInt(localTimerDisplay).ToString() + "s";
                }
            }
        }
    }
    private void HandleAllClientsLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleAllClientsLoaded;

        if (matchStarted) return;
        matchStarted = true;
        AssignPlayerRoles();
        StartCoroutine(MatchLifecycleRoutine());
    }

private IEnumerator MatchLifecycleRoutine()
    {
        CurrentState.Value = MatchState.Hiding;
        timeRemaining.Value = netHidingDuration.Value;

        while (timeRemaining.Value > 0)
        {
            if (!IsServer || !NetworkManager.Singleton.IsListening)
                yield break;

            if (currentHiders.Count == 0 || currentHunters.Count == 0)
                break;

            timeRemaining.Value = Mathf.Max(0, timeRemaining.Value - Time.deltaTime);

            yield return null;
        }

        if (currentHiders.Count == 0 || currentHunters.Count == 0)
        {
            CurrentState.Value = MatchState.PostGame;

            ulong[] earlyScoreKeys = new ulong[playerScores.Count];
            int[] earlyScoreValues = new int[playerScores.Count];

            int sIdx = 0;
            foreach (var kvp in playerScores)
            {
                earlyScoreKeys[sIdx] = kvp.Key;
                earlyScoreValues[sIdx] = kvp.Value;
                sIdx++;
            }

            ulong[] earlyHunterKeys = new ulong[hunterScores.Count];
            int[] earlyHunterValues = new int[hunterScores.Count];

            sIdx = 0;
            foreach (var kvp in hunterScores)
            {
                earlyHunterKeys[sIdx] = kvp.Key;
                earlyHunterValues[sIdx] = kvp.Value;
                sIdx++;
            }

            string earlyWinner = currentHiders.Count == 0 ? "HUNTERS WIN!" : "HIDERS WIN!";
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.LogMatchCompleted(MainMenuData.IsFriendsMode);
            }

            NotifyEndGameClientRpc(
                earlyWinner,
                earlyScoreKeys,
                earlyScoreValues,
                earlyHunterKeys,
                earlyHunterValues);

            yield return new WaitForSeconds(5f);
            CleanShutdownAndExit();
            yield break;
        }
  // Teleport Hunters: Direct server reposition for bots, ClientRpc for human players
        if (IsServer)
        {
            foreach (ulong hunterId in currentHunters)
            {
                if (activeBotObjects.TryGetValue(hunterId, out var botObj) && botObj != null)
                {
                    if (hunterArenaSpawnPoint != null)
                    {
                        var agent = botObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null)
                        {
                            agent.enabled = true;
                            if (agent.isOnNavMesh) agent.Warp(hunterArenaSpawnPoint.position);
                            else botObj.transform.position = hunterArenaSpawnPoint.position;
                        }
                        else
                        {
                            botObj.transform.position = hunterArenaSpawnPoint.position;
                        }

                        botObj.transform.rotation = hunterArenaSpawnPoint.rotation;
                    }
                }
                else
                {
                    ClientRpcParams targetParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new List<ulong> { hunterId } }
                    };
                    ExecuteHunterTeleportTargetClientRpc(targetParams);
                }
            }
        }

        CurrentState.Value = MatchState.Hunting;
        timeRemaining.Value = netHuntingDuration.Value;
        whistleCountdown.Value = 20f;

        while (timeRemaining.Value > 0)
        {
            if (!IsServer || !NetworkManager.Singleton.IsListening) yield break;
            if (currentHiders.Count == 0) break;

            timeRemaining.Value = Mathf.Max(0, timeRemaining.Value - Time.deltaTime);

            if (whistleCountdown.Value > 0)
            {
                whistleCountdown.Value = Mathf.Max(0, whistleCountdown.Value - Time.deltaTime);
            }
            else
            {
                whistleCountdown.Value = 20f;
                TriggerAllHidersWhistleClientRpc();
            }

            scoreTickTimer += Time.deltaTime;
            if (scoreTickTimer >= 1f)
            {
                scoreTickTimer = 0f;
                ProcessScoreTick();
            }

            yield return null;
        }

        if (currentHiders.Count > 0)
        {
            CurrentState.Value = MatchState.AnswerCheck;

            TriggerHunterAnswerCheckSpectateClientRpc();
            FreezeHidersServerRpc(true);

            timeRemaining.Value = answerCheckDuration;
            while (timeRemaining.Value > 0)
            {
                if (currentHiders.Count == 0) break;
                timeRemaining.Value = Mathf.Max(0, timeRemaining.Value - Time.deltaTime);
                yield return null;
            }

            FreezeHidersServerRpc(false);
            CurrentState.Value = MatchState.PostGame;
        }
        else
        {
            CurrentState.Value = MatchState.PostGame;
        }

        // Build score texts
        string hiderText = "";
        foreach (var kvp in playerScores)
        {
            string name = GetPlayerNameFromId(kvp.Key);
            hiderText += $"{name} : {kvp.Value} pts\n";
        }

        string hunterText = "";
        foreach (var kvp in hunterScores)
        {
            string name = GetPlayerNameFromId(kvp.Key);
            hunterText += $"{name} : {kvp.Value} pts\n";
        }

        if (string.IsNullOrEmpty(hiderText)) hiderText = "No Hiders";
        if (string.IsNullOrEmpty(hunterText)) hunterText = "No Hunters";

        List<ulong> originalHiderIds = new List<ulong>();
        foreach (var kvp in originalIsHiderMap)
        {
            if (kvp.Value) originalHiderIds.Add(kvp.Key);
        }

        string finalMessage = currentHiders.Count > 0 ? "HIDERS WIN!" : "HUNTERS WIN!";
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.LogMatchCompleted(MainMenuData.IsFriendsMode);
        }

        SyncEndGameDataAndShutdownClientRpc(finalMessage, hiderText, hunterText, originalHiderIds.ToArray());
    }

    private void ProcessScoreTick()
    {
        foreach (ulong hiderId in currentHiders)
        {
            if (TryGetParticipantPosition(hiderId, out Vector3 hiderPos))
            {
                foreach (ulong hunterId in currentHunters)
                {
                    if (TryGetParticipantPosition(hunterId, out Vector3 hunterPos))
                    {
                        float distance = Vector3.Distance(hiderPos, hunterPos);
                        if (distance <= 15f)
                        {
                            if (!playerScores.ContainsKey(hiderId)) playerScores[hiderId] = 0;
                            playerScores[hiderId] += 10;
                        }
                    }
                }
            }
        }

        UpdateLeaderboardClientRpc(GetTop3LeaderboardData());
        UpdatePlayerCountsClientRpc(currentHiders.Count, currentHunters.Count);
        UpdateLiveLocalScoreClientRpc();
    }
    private bool TryGetParticipantPosition(ulong id, out Vector3 position)
    {
        position = Vector3.zero;

        // Check AI bots first
        if (activeBotObjects.TryGetValue(id, out var botNetObj) && botNetObj != null)
        {
            position = botNetObj.transform.position;
            return true;
        }

        // Check human clients
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out var client) && client.PlayerObject != null)
        {
            position = client.PlayerObject.transform.position;
            return true;
        }

        return false;
    }
    [ClientRpc]
    private void UpdatePlayerCountsClientRpc(int hidersAlive, int huntersAlive)
    {
        if (hiderCountText != null)
            hiderCountText.text = hidersAlive.ToString();

        if (hunterCountText != null)
            hunterCountText.text = huntersAlive.ToString();
    }
    [ClientRpc]
    private void UpdateLiveLocalScoreClientRpc()
    {
        if (localPlayerScoreText == null) return;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        int myScore = GetScoreForClient(localClientId);

        localPlayerScoreText.text = $"YOUR SCORE: {myScore}";
    }

    private int GetScoreForClient(ulong clientId)
    {
        int total = 0;
        if (playerScores.TryGetValue(clientId, out int hiderPts)) total += hiderPts;
        if (hunterScores.TryGetValue(clientId, out int hunterPts)) total += hunterPts;
        return total;
    }
    private List<NetworkObject> GetActiveHiders()
    {
        List<NetworkObject> hiders = new List<NetworkObject>();

        // Collect human hiders
        HiderMovement[] activeHiders = FindObjectsByType<HiderMovement>(FindObjectsInactive.Exclude);
        foreach (var hider in activeHiders)
        {
            NetworkObject netObj = hider.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                hiders.Add(netObj);
            }
        }

        // Collect bot hiders
        BotHiderAI[] activeBotHiders = FindObjectsByType<BotHiderAI>(FindObjectsInactive.Exclude);
        foreach (var bot in activeBotHiders)
        {
            NetworkObject netObj = bot.GetComponent<NetworkObject>();
            if (netObj != null && !hiders.Contains(netObj))
            {
                hiders.Add(netObj);
            }
        }

        return hiders;
    }
    private void AssignPlayerRoles()
    {
        currentHiders.Clear();
        currentHunters.Clear();
        playerScores.Clear();
        hunterScores.Clear();
        disconnectingClients.Clear();
        originalIsHiderMap.Clear();
        activeBotObjects.Clear();

        List<ulong> humanIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        List<ulong> botIds = new List<ulong>();

        botIds.Clear();
        for (int i = 0; i < MainMenuData.CachedLobbyBots.Count; i++)
        {
            botIds.Add(MainMenuData.CachedLobbyBots[i].ClientId);
        }

        // Shuffle humans
        for (int i = 0; i < humanIds.Count; i++)
        {
            ulong temp = humanIds[i];
            int rnd = Random.Range(i, humanIds.Count);
            humanIds[i] = humanIds[rnd];
            humanIds[rnd] = temp;
        }

        // Shuffle bots
        for (int i = 0; i < botIds.Count; i++)
        {
            ulong temp = botIds[i];
            int rnd = Random.Range(i, botIds.Count);
            botIds[i] = botIds[rnd];
            botIds[rnd] = temp;
        }

        int totalParticipants = humanIds.Count + botIds.Count;
        int targetHunters = (totalParticipants >= 6) ? 2 : 1;
        int huntersAssigned = 0;

        // --- SOLO TESTING OVERRIDE ---
        // If enabled and playing solo with AI bots, force yourself into Hider
        if (forceSoloHider && humanIds.Count == 1 && botIds.Count > 0)
        {
            ulong myId = humanIds[0];
            currentHiders.Add(myId);
            playerScores[myId] = 0;
            originalIsHiderMap[myId] = true;
            SpawnPlayerPrefab(myId, hiderPrefab, hiderSpawnPoint);

            for (int i = 0; i < botIds.Count; i++)
            {
                ulong bId = botIds[i];
                if (huntersAssigned < targetHunters)
                {
                    currentHunters.Add(bId);
                    hunterScores[bId] = 0;
                    originalIsHiderMap[bId] = false;
                    SpawnBotPrefab(bId, botHunterPrefab, hunterWaitingArea);
                    huntersAssigned++;
                }
                else
                {
                    currentHiders.Add(bId);
                    playerScores[bId] = 0;
                    originalIsHiderMap[bId] = true;
                    SpawnBotPrefab(bId, botHiderPrefab, hiderSpawnPoint);
                }
            }

            UpdatePlayerCountsClientRpc(currentHiders.Count, currentHunters.Count);
            return;
        }

        // --- NORMAL MULTIPLAYER ROLE ASSIGNMENT ---
        // Assign humans
        for (int i = 0; i < humanIds.Count; i++)
        {
            ulong id = humanIds[i];
            if (huntersAssigned < targetHunters)
            {
                currentHunters.Add(id);
                hunterScores[id] = 0;
                originalIsHiderMap[id] = false;
                SpawnPlayerPrefab(id, hunterPrefab, hunterWaitingArea);
                huntersAssigned++;
            }
            else
            {
                currentHiders.Add(id);
                playerScores[id] = 0;
                originalIsHiderMap[id] = true;
                SpawnPlayerPrefab(id, hiderPrefab, hiderSpawnPoint);
            }
        }

        // Assign bots
        for (int i = 0; i < botIds.Count; i++)
        {
            ulong id = botIds[i];
            if (huntersAssigned < targetHunters)
            {
                currentHunters.Add(id);
                hunterScores[id] = 0;
                originalIsHiderMap[id] = false;
                SpawnBotPrefab(id, botHunterPrefab, hunterWaitingArea);
                huntersAssigned++;
            }
            else
            {
                currentHiders.Add(id);
                playerScores[id] = 0;
                originalIsHiderMap[id] = true;
                SpawnBotPrefab(id, botHiderPrefab, hiderSpawnPoint);
            }
        }

        UpdatePlayerCountsClientRpc(currentHiders.Count, currentHunters.Count);
    }

    private void SpawnBotPrefab(ulong botId, GameObject prefab, Transform spawnPoint)
    {
        if (!IsServer || prefab == null) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject botObj = Instantiate(prefab, pos, rot);
        NetworkObject netObj = botObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            // Cache virtual ID on the bot script
            var botHider = botObj.GetComponent<BotHiderAI>();
            if (botHider != null) botHider.BotVirtualClientId = botId;

            var botHunter = botObj.GetComponent<BotHunterAI>();
            if (botHunter != null)
            {
                botHunter.BotVirtualClientId = botId;

                // Pick the spawn point's nearest or direct WaypointNode
                WaypointNode startNode = spawnPoint != null ? spawnPoint.GetComponent<WaypointNode>() : null;
                if (startNode == null)
                {
                    startNode = FindObjectsByType<WaypointNode>(FindObjectsSortMode.None)
                        .OrderBy(n => Vector3.Distance(pos, n.transform.position))
                        .FirstOrDefault();
                }
                botHunter.currentWaypoint = startNode;
            }

            netObj.Spawn(true);
            activeBotObjects[botId] = netObj;
        }
    }
    private void SpawnPlayerPrefab(ulong clientId, GameObject prefab, Transform spawnPoint)
    {
        if (NetworkManager.Singleton == null || !IsServer) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        if (client.PlayerObject != null) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject playerObj = Instantiate(prefab, pos, rot);
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId, true);
        }
    }

    [ClientRpc]
    private void ExecuteHunterTeleportTargetClientRpc(ClientRpcParams clientRpcParams = default)
    {
        var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localPlayer != null && hunterArenaSpawnPoint != null)
        {
            var cc = localPlayer.GetComponent<CharacterController>();
            var rb = localPlayer.GetComponent<Rigidbody>();

            if (cc != null) cc.enabled = false;
            if (rb != null) rb.isKinematic = true;

            localPlayer.transform.position = hunterArenaSpawnPoint.position;
            localPlayer.transform.rotation = hunterArenaSpawnPoint.rotation;

            if (cc != null) cc.enabled = true;
            if (rb != null) rb.isKinematic = false;
        }
    }

    public void OnHiderEliminated(ulong hunterClientId, ulong hiderClientId)
    {
        if (!IsServer || CurrentState.Value != MatchState.Hunting) return;

        // Verify the target is actually registered as a current hider
        if (!currentHiders.Contains(hiderClientId)) return;

        if (!hunterScores.ContainsKey(hunterClientId))
            hunterScores[hunterClientId] = 0;

        hunterScores[hunterClientId] += 200;

        if (disconnectingClients.Contains(hiderClientId)) return;

        bool isBot = activeBotObjects.ContainsKey(hiderClientId);

        // Always remove from active hiders list first
        currentHiders.Remove(hiderClientId);

        if (currentHiders.Count > 0)
        {
            currentHunters.Add(hiderClientId);

            if (isBot)
            {
                if (activeBotObjects.TryGetValue(hiderClientId, out var botNetObj) && botNetObj != null)
                {
                    botNetObj.gameObject.SetActive(false);
                    botNetObj.Despawn(true);
                    Destroy(botNetObj.gameObject);
                    activeBotObjects.Remove(hiderClientId);
                }

                Transform targetSpawn = (CurrentState.Value == MatchState.Hiding) ? hunterWaitingArea : hunterArenaSpawnPoint;
                SpawnBotPrefab(hiderClientId, botHunterPrefab, targetSpawn);

                if (activeBotObjects.TryGetValue(hiderClientId, out var newHunterObj) && newHunterObj != null)
                {
                    var botHunterAI = newHunterObj.GetComponent<BotHunterAI>();
                    if (botHunterAI != null && botHunterAI.currentWaypoint == null)
                    {
                        botHunterAI.currentWaypoint = FindObjectsByType<WaypointNode>(FindObjectsSortMode.None)
                            .OrderBy(n => Vector3.Distance(newHunterObj.transform.position, n.transform.position))
                            .FirstOrDefault();
                    }
                }
            }
            else
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(hiderClientId, out var client))
                {
                    if (client.PlayerObject != null)
                    {
                        client.PlayerObject.Despawn(true);
                    }
                }
                StartCoroutine(SpawnHunterNextFrame(hiderClientId));
            }
        }
        else
        {
            // Last hider eliminated! Despawn and trigger Hunter victory immediately
            if (isBot)
            {
                if (activeBotObjects.TryGetValue(hiderClientId, out var botNetObj) && botNetObj != null)
                {
                    botNetObj.gameObject.SetActive(false);
                    botNetObj.Despawn(true);
                    Destroy(botNetObj.gameObject);
                    activeBotObjects.Remove(hiderClientId);
                }
            }

            StopAllCoroutines();
            StartCoroutine(EndMatchImmediately("HUNTERS WIN!"));
        }

        UpdatePlayerCountsClientRpc(currentHiders.Count, currentHunters.Count);
    }

    private IEnumerator SpawnHunterNextFrame(ulong clientId)
    {
        yield return new WaitForSeconds(0.1f);

        if (disconnectingClients.Contains(clientId) || !NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            currentHunters.Remove(clientId);
            yield break;
        }

        NotifyClientHiderClearClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new List<ulong> { clientId } } });

        yield return null;

        if (disconnectingClients.Contains(clientId) || !NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            currentHunters.Remove(clientId);
            yield break;
        }

        SpawnPlayerPrefab(clientId, hunterPrefab, hunterArenaSpawnPoint);
    }

    [ClientRpc]
    private void NotifyClientHiderClearClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (UIAssignmentManager.Instance != null && UIAssignmentManager.Instance.hiderMasterUIGroup != null)
        {
            UIAssignmentManager.Instance.hiderMasterUIGroup.SetActive(false);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void FreezeHidersServerRpc(bool shouldFreeze)
    {
        // 1. Freeze bot hiders directly on the server
        BotHiderAI[] allBotHiders = FindObjectsByType<BotHiderAI>(FindObjectsInactive.Exclude);
        foreach (BotHiderAI bot in allBotHiders)
        {
            bot.enabled = !shouldFreeze;

            var agent = bot.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = shouldFreeze;
            }
        }

        // 2. Freeze human hiders on their local machines
        FreezeHidersClientRpc(shouldFreeze);
    }

    [ClientRpc]
    private void FreezeHidersClientRpc(bool shouldFreeze)
    {
        HiderMovement localHider = GetLocalHiderMovement();
        if (localHider != null) localHider.enabled = !shouldFreeze;
    }
    private string GetTop3HunterData()
    {
        List<KeyValuePair<ulong, int>> sorted =
            new List<KeyValuePair<ulong, int>>(hunterScores);

        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        string text = "TOP HUNTERS:\n";

        int count = Mathf.Min(3, sorted.Count);

        if (count == 0)
        {
            text += "No hunter points.";
            return text;
        }

        for (int i = 0; i < count; i++)
        {
            text += $"{GetPlayerNameFromId(sorted[i].Key)} : {sorted[i].Value} pts\n";
        }

        return text;
    }
    [ClientRpc]
    private void NotifyEndGameClientRpc(string victoryMessage,ulong[] hiderKeys,int[] hiderValues,ulong[] hunterKeys,int[] hunterValues)
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (lossPanel != null) lossPanel.SetActive(false);

        playerScores.Clear();
        hunterScores.Clear();

        if (hiderKeys != null && hiderValues != null)
        {
            int count = Mathf.Min(hiderKeys.Length, hiderValues.Length);

            for (int i = 0; i < count; i++)
                playerScores[hiderKeys[i]] = hiderValues[i];
        }

        if (hunterKeys != null && hunterValues != null)
        {
            int count = Mathf.Min(hunterKeys.Length, hunterValues.Length);

            for (int i = 0; i < count; i++)
                hunterScores[hunterKeys[i]] = hunterValues[i];
        }

        List<KeyValuePair<ulong, int>> sortedScores = new List<KeyValuePair<ulong, int>>(playerScores);
        sortedScores.Sort((x, y) => y.Value.CompareTo(x.Value));

        string topScoresDisplay = "TOP PLAYERS:\n";
        string topHunterDisplay = GetTop3HunterData();
        int displayCount = Mathf.Min(3, sortedScores.Count);

        if (displayCount == 0)
        {
            topScoresDisplay += "No scores recorded.";
        }
        else
        {
            for (int i = 0; i < displayCount; i++)
            {
                string realName = GetPlayerNameFromId(sortedScores[i].Key);

                bool isHunter = currentHunters.Contains(sortedScores[i].Key);
                string role = isHunter ? "Hunter" : "Hider";

                topScoresDisplay += $"{realName} ({role}) : {sortedScores[i].Value} pts\n";
            }
        }

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        bool isLocalHider = currentHiders.Contains(localClientId);
        bool hidersWon = victoryMessage.Contains("HIDERS");

        int myFinalScore = GetScoreForClient(localClientId);
        string scoreFormattedText = $"YOUR SCORE: {myFinalScore}";

        if (localPlayerScoreText != null)
            localPlayerScoreText.text = scoreFormattedText;

        if ((hidersWon && isLocalHider) || (!hidersWon && !isLocalHider))
        {
            if (victoryPanel != null) victoryPanel.SetActive(true);
            if (audioSource != null && winClip != null)
            {
                audioSource.PlayOneShot(winClip);
            }
            if (victoryPanelScoreText != null)
                victoryPanelScoreText.text = topScoresDisplay;

            if (victoryHunterScoreText != null)
                victoryHunterScoreText.text = topHunterDisplay;

            if (victoryLocalScoreText != null)
                victoryLocalScoreText.text = scoreFormattedText;
        }
        else
        {
            if (lossPanel != null) lossPanel.SetActive(true);
            if (audioSource != null && loseClip != null)
            {
                audioSource.PlayOneShot(loseClip);
            }
            if (lossPanelScoreText != null)
                lossPanelScoreText.text = topScoresDisplay;

            if (lossHunterScoreText != null)
                lossHunterScoreText.text = topHunterDisplay;

            if (lossLocalScoreText != null)
                lossLocalScoreText.text = scoreFormattedText;
        }
    }

    private void CleanShutdownAndExit()
    {
        ExecuteGlobalShutdown();
    }

    private void UpdateTimerUI(float oldTime, float newTime)
    {
        // Sync local display value ONLY when host sends a network value update
        localTimerDisplay = newTime;

        if (timerText == null) return;
        timerText.text = Mathf.CeilToInt(newTime).ToString() + "s";
    }

    private void HandleStateChangeUI(MatchState oldState, MatchState newState)
    {
        if (statusText == null) return;
        switch (newState)
        {
            case MatchState.Hiding: statusText.text = "HIDING TIME - RUN!"; break;
            case MatchState.Hunting: statusText.text = "HUNTING TIME - FIND THEM!"; break;
            case MatchState.AnswerCheck: statusText.text = "ANSWER CHECK - ROOM TO INSPECT!"; break;
            case MatchState.PostGame: statusText.text = "MATCH OVER"; break;
        }
    }

    private void HandlePlayerDisconnect(ulong clientId)
    {
        if (loopEnded) return;

        if (!IsServer && clientId == NetworkManager.ServerClientId)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            if (string.IsNullOrEmpty(reason))
            {
                reason = "Host closed the lobby or lost connection.";
            }

            MainMenuData.LastDisconnectReason = $"Disconnected: {reason}";
            ExecuteGlobalShutdown();
            return;
        }

        if (IsServer)
        {
            disconnectingClients.Add(clientId);

            currentHiders.Remove(clientId);
            currentHunters.Remove(clientId);
            playerScores.Remove(clientId);

            // Count remaining active human clients (excluding virtual bot IDs)
            int humanCount = 0;
            foreach (var connectedId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (!disconnectingClients.Contains(connectedId))
                {
                    humanCount++;
                }
            }

            // Abort match if no real human players remain
            if (humanCount == 0)
            {
                StopAllCoroutines();
                ExecuteGlobalShutdown();
                return;
            }

            if (CurrentState.Value == MatchState.Hiding || CurrentState.Value == MatchState.Hunting)
            {
                if (currentHiders.Count == 0)
                {
                    StopAllCoroutines();
                    StartCoroutine(EndMatchImmediately("HUNTERS WIN!"));
                }
                else if (currentHunters.Count == 0)
                {
                    StopAllCoroutines();
                    StartCoroutine(EndMatchImmediately("HIDERS WIN!"));
                }
            }
        }
    }
    private void OnLocalClientSessionStopped(bool isHost)
    {
        if (!IsServer && !loopEnded)
        {
            if (string.IsNullOrEmpty(MainMenuData.LastDisconnectReason))
            {
                MainMenuData.LastDisconnectReason = "Connection to host lost.";
            }
            Debug.LogWarning("Connection to host lost! Exiting to main menu...");
            ExecuteGlobalShutdown();
        }
    }
    private IEnumerator EndMatchImmediately(string winnerMessage)
    {
        CurrentState.Value = MatchState.PostGame;

        ulong[] scoreKeys = new ulong[playerScores.Count];
        int[] scoreValues = new int[playerScores.Count];

        int idx = 0;
        foreach (var kvp in playerScores)
        {
            scoreKeys[idx] = kvp.Key;
            scoreValues[idx] = kvp.Value;
            idx++;
        }

        ulong[] hunterKeys = new ulong[hunterScores.Count];
        int[] hunterValues = new int[hunterScores.Count];

        idx = 0;
        foreach (var kvp in hunterScores)
        {
            hunterKeys[idx] = kvp.Key;
            hunterValues[idx] = kvp.Value;
            idx++;
        }

        NotifyEndGameClientRpc(
            winnerMessage,
            scoreKeys,
            scoreValues,
            hunterKeys,
            hunterValues);

        yield return new WaitForSeconds(5f);

        CleanShutdownAndExit();
    }
    public void ExecuteGlobalShutdown()
    {
        if (loopEnded || isShuttingDown)
        {
            Debug.LogWarning("[Match Engine] ExecuteGlobalShutdown BLOCKED because loopEnded or isShuttingDown was true!");
            return;
        }

        isShuttingDown = true;
        StartCoroutine(GracefulShutdownRoutine());
    }

    private IEnumerator GracefulShutdownRoutine()
    {
        loopEnded = true;

        /* voice chat
        if (VoiceChatManager.Instance != null)
        {
            var voiceTask = VoiceChatManager.Instance.LeaveCurrentVoiceChannel();
            yield return new WaitUntil(() => voiceTask.IsCompleted);
            VoiceChatManager.Instance.ActiveVoiceChannelToken = null;
        }
        */
        if (NetworkManager.Singleton != null)
        {
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

        MainMenuData.ClearAllData();

        if (LevelPlayAdManager.Instance != null)
        {
            bool adFinished = false;
            LevelPlayAdManager.Instance.ShowMatchEndInterstitial(() => {
                adFinished = true;
            });
            yield return new WaitUntil(() => adFinished);
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    public override void OnDestroy()
    {
        timeRemaining.OnValueChanged -= UpdateTimerUI;
        CurrentState.OnValueChanged -= HandleStateChangeUI;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandlePlayerDisconnect;

            NetworkManager.Singleton.OnClientStopped -= OnLocalClientSessionStopped;
        }
        base.OnDestroy();
    }

    [ClientRpc]
    private void TriggerAllHidersWhistleClientRpc()
    {
        // Human hiders
        HiderMovement[] allHumanHiders = FindObjectsByType<HiderMovement>(FindObjectsInactive.Exclude);
        foreach (HiderMovement hider in allHumanHiders)
        {
            AudioSource audio = hider.GetComponent<AudioSource>();
            if (audio != null && audio.clip != null) audio.PlayOneShot(audio.clip);
        }

        // Bot hiders
        BotHiderAI[] allBotHiders = FindObjectsByType<BotHiderAI>(FindObjectsInactive.Exclude);
        foreach (BotHiderAI bot in allBotHiders)
        {
            AudioSource audio = bot.GetComponent<AudioSource>();
            if (audio != null && audio.clip != null) audio.PlayOneShot(audio.clip);
        }
    }

    private HiderMovement GetLocalHiderMovement()
    {
        HiderMovement[] allHiders = FindObjectsByType<HiderMovement>();
        foreach (HiderMovement hider in allHiders)
        {
            if (hider.IsOwner) return hider;
        }
        return null;
    }

    private void UpdateWhistleTimerUI(float oldTime, float newTime)
    {
        if (whistleTimerText != null && CurrentState.Value == MatchState.Hunting)
        {
            whistleTimerText.text = "Next Whistle: " + Mathf.CeilToInt(newTime).ToString() + "s";
        }
        else if (whistleTimerText != null)
        {
            whistleTimerText.text = "";
        }
    }
    [ClientRpc]
    private void TriggerHunterAnswerCheckSpectateClientRpc()
    {
        HunterController localHunter = null;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj != null)
            {
                localHunter = localPlayerObj.GetComponent<HunterController>();
            }
        }

        if (localHunter == null)
        {
            HunterController[] allHunters = FindObjectsByType<HunterController>(FindObjectsInactive.Include);
            foreach (var h in allHunters)
            {
                if (h != null && h.IsOwner)
                {
                    localHunter = h;
                    break;
                }
            }
        }               

        if (localHunter != null)
        {
            ApplySpectateToHunter(localHunter);
            return;
        }
        StartCoroutine(WaitForLocalHunterAndApply());
    }

    private void ApplySpectateToHunter(HunterController localHunter)
    {
        if (localHunter == null)
        {
            Debug.LogWarning("[GME] ApplySpectateToHunter called with null hunter");
            return;
        }

        // 1. Turn off local hunter camera so orbit camera takes over
        if (localHunter.hunterCameraHolder != null)
        {
            Camera cam = localHunter.hunterCameraHolder.GetComponentInChildren<Camera>(true);

            if (cam != null)
            {
                cam.enabled = false;
            }
        }
        else
        {
            Debug.LogWarning("[GME] hunterCameraHolder is null on local hunter");
        }

        // 2. Enable orbit view targeting active hiders
        if (AnswerCheckOrbitCamera.Instance != null)
        {
            AnswerCheckOrbitCamera.Instance.EnableAnswerCheckSpectating(GetActiveHiders());
        }
        else
        {
            Debug.LogWarning("[GME] AnswerCheckOrbitCamera.Instance is null");
        }
    }

    private IEnumerator WaitForLocalHunterAndApply()
    {
        float timer = 0f;
        const float timeout = 3f;

        while (timer < timeout)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (localPlayerObj != null)
                {
                    var h = localPlayerObj.GetComponent<HunterController>();
                    if (h != null && h.IsOwner)
                    {
                        ApplySpectateToHunter(h);
                        yield break;
                    }
                }

                HunterController[] allHunters = FindObjectsByType<HunterController>(FindObjectsInactive.Include);
                foreach (var h in allHunters)
                {
                    if (h != null && h.IsOwner)
                    {
                        ApplySpectateToHunter(h);
                        yield break;
                    }
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[GME] WaitForLocalHunterAndApply timed out after " + timeout + "s without finding local hunter");
    }
    private string GetTop3LeaderboardData()
    {
        List<KeyValuePair<ulong, int>> sortedList = new List<KeyValuePair<ulong, int>>(playerScores);
        sortedList.Sort((x, y) => y.Value.CompareTo(x.Value));

        string dataString = "TOP 3 LEADERS:\n";
        int displayLimit = Mathf.Min(3, sortedList.Count);

        for (int i = 0; i < displayLimit; i++)
        {
            ulong id = sortedList[i].Key;
            string realName = GetPlayerNameFromId(id);
            dataString += $"{realName} - {sortedList[i].Value}\n";
        }
        return dataString;
    }

    [ClientRpc]
    private void UpdateLeaderboardClientRpc(string leaderboardDisplay)
    {
        if (leaderboardText != null && CurrentState.Value == MatchState.Hunting)
        {
            leaderboardText.text = leaderboardDisplay;
        }
        else if (leaderboardText != null)
        {
            leaderboardText.text = "";
        }
    }
    private void OnMatchStateChangedVisibility(MatchState oldState, MatchState newState)
    {
        EvaluateLocalVisibility(newState);

        if (newState == MatchState.AnswerCheck)
        {
            // Trigger spectate setup on local client if we are a hunter
            HunterController localHunter = GetLocalHunterController();
            if (localHunter != null && localHunter.IsOwner)
            {
                ApplySpectateToHunter(localHunter);
            }
        }
        else if (oldState == MatchState.AnswerCheck)
        {
            // Restore normal hunter camera when leaving AnswerCheck
            HunterController localHunter = GetLocalHunterController();
            if (localHunter != null && localHunter.IsOwner && localHunter.hunterCameraHolder != null)
            {
                Camera cam = localHunter.hunterCameraHolder.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    cam.enabled = true;
                }
                localHunter.hunterCameraHolder.gameObject.SetActive(true);
            }

            if (AnswerCheckOrbitCamera.Instance != null)
            {
                AnswerCheckOrbitCamera.Instance.DisableAnswerCheckSpectating();
            }
        }
    }
    private HunterController GetLocalHunterController()
    {
        // 1. Try fetching via LocalClient's assigned PlayerObject
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj != null)
            {
                var hunter = localPlayerObj.GetComponent<HunterController>();
                if (hunter != null) return hunter;
            }
        }

        // 2. Fallback: Scan scene for the Hunter owned by this client
        HunterController[] allHunters = FindObjectsByType<HunterController>(FindObjectsInactive.Include);
        foreach (var h in allHunters)
        {
            if (h != null && h.IsOwner) return h;
        }

        return null;
    }
    private void EvaluateLocalVisibility(MatchState state)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            bool isLocalPlayerSpawned = NetworkManager.Singleton.LocalClient.PlayerObject != null;

            if (fallbackSceneCamera != null)
            {
                fallbackSceneCamera.SetActive(!isLocalPlayerSpawned);
            }
            if (isLocalPlayerSpawned && !isLoadingFinished)
            {
                StartCoroutine(KeepLoadingPanelActiveRoutine());
            }
        }
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        bool isLocalHider = currentHiders.Contains(localClientId);
        bool isLocalHunter = currentHunters.Contains(localClientId);

        HiderMovement[] allHumanHiders = FindObjectsByType<HiderMovement>(FindObjectsInactive.Include);
        HunterController[] allHumanHunters = FindObjectsByType<HunterController>(FindObjectsInactive.Include);
        BotHiderAI[] allBotHiders = FindObjectsByType<BotHiderAI>(FindObjectsInactive.Include);
        BotHunterAI[] allBotHunters = FindObjectsByType<BotHunterAI>(FindObjectsInactive.Include);

        if (UIAssignmentManager.Instance != null && UIAssignmentManager.Instance.enterSpectateButton != null)
        {
            if (state == MatchState.Hunting || state == MatchState.AnswerCheck)
            {
                UIAssignmentManager.Instance.enterSpectateButton.gameObject.SetActive(true);
            }
            else
            {
                UIAssignmentManager.Instance.enterSpectateButton.gameObject.SetActive(false);
            }
        }

        if (isLocalHider)
        {
            ToggleRenderers(hunterWaitingSection, false);
            ToggleRenderers(mainMapSection, true);
        }

        if (isLocalHunter)
        {
            if (state == MatchState.Hiding)
            {
                if (timeRemaining.Value <= 10f)
                {
                    ToggleRenderers(mainMapSection, true);
                }
                else
                {
                    ToggleRenderers(mainMapSection, false);
                }

                ToggleRenderers(hunterWaitingSection, true);
            }
            else if (state == MatchState.Hunting || state == MatchState.AnswerCheck)
            {
                ToggleRenderers(mainMapSection, true);

                if (timeRemaining.Value <= (netHuntingDuration.Value - 10f))
                {
                    ToggleRenderers(hunterWaitingSection, false);
                }
                else
                {
                    ToggleRenderers(hunterWaitingSection, true);
                }
            }
        }

        if (state == MatchState.Hiding)
        {
            if (isLocalHunter)
            {
                foreach (var hider in allHumanHiders) ToggleRenderers(hider.gameObject, false);
                foreach (var botHider in allBotHiders) ToggleRenderers(botHider.gameObject, false);
            }
            if (isLocalHider)
            {
                foreach (var hunter in allHumanHunters) ToggleRenderers(hunter.gameObject, false);
                foreach (var botHunter in allBotHunters) ToggleRenderers(botHunter.gameObject, false);
            }
        }
        else if (state == MatchState.Hunting || state == MatchState.AnswerCheck)
        {
            foreach (var hider in allHumanHiders) ToggleRenderers(hider.gameObject, true);
            foreach (var botHider in allBotHiders) ToggleRenderers(botHider.gameObject, true);

            foreach (var hunter in allHumanHunters)
            {
                if (!hunter.IsOwner) ToggleRenderers(hunter.gameObject, true);
            }
            foreach (var botHunter in allBotHunters)
            {
                ToggleRenderers(botHunter.gameObject, true);
            }
        }
    }
    private IEnumerator KeepLoadingPanelActiveRoutine()
    {
        isLoadingFinished = true;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        if (loadingTipText != null && loadingTips != null && loadingTips.Length > 0)
        {
            int randomIndex = Random.Range(0, loadingTips.Length);
            loadingTipText.text = loadingTips[randomIndex];
        }

        if (loadingSlider != null)
        {
            loadingSlider.value = 0f;
        }

        float elapsedTime = 0f;
        while (elapsedTime < minLoadingScreenDuration)
        {
            elapsedTime += Time.deltaTime;

            if (loadingSlider != null)
            {
                loadingSlider.value = Mathf.Clamp01(elapsedTime / minLoadingScreenDuration);
            }

            yield return null;
        }

        if (loadingSlider != null)
        {
            loadingSlider.value = 1f;
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }
    private string GetPlayerNameFromId(ulong clientId)
    {
        for (int i = 0; i < MainMenuData.CachedLobbyBots.Count; i++)
        {
            if (MainMenuData.CachedLobbyBots[i].ClientId == clientId)
            {
                return MainMenuData.CachedLobbyBots[i].PlayerName.ToString();
            }
        }

        return MainMenuData.GetPlayerName(clientId);
    }
    [ClientRpc]
    private void SyncEndGameDataAndShutdownClientRpc(string victoryMessage, string hidersScoreText, string huntersScoreText, ulong[] winningHiderIds)
    {
        StartCoroutine(ShowOfflineResultRoutine(victoryMessage, hidersScoreText, huntersScoreText, winningHiderIds));
    }

    private IEnumerator ShowOfflineResultRoutine(string victoryMessage, string hidersScoreText, string huntersScoreText, ulong[] originalHiderIds)
    {
        // 1. Unhook disconnect listeners
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandlePlayerDisconnect;
            NetworkManager.Singleton.OnClientStopped -= OnLocalClientSessionStopped;
        }

        // 2. Accurately check starting role
        ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;

        List<ulong> hidersList = new List<ulong>(originalHiderIds);
        bool wasOriginallyHider = hidersList.Contains(localClientId);
        bool hidersWon = victoryMessage.Contains("HIDERS");

        // Win check: (Hiders Won AND I was Originally a Hider) OR (Hunters Won AND I was Originally a Hunter)
        bool localWon = (hidersWon && wasOriginallyHider) || (!hidersWon && !wasOriginallyHider);

        int myScore = GetScoreForClient(localClientId);

        // 3. Save to local MainMenuData
        MainMenuData.MatchWinnerMessage = victoryMessage;
        MainMenuData.FinalHidersScoreText = hidersScoreText;
        MainMenuData.FinalHuntersScoreText = huntersScoreText;
        MainMenuData.LocalPlayerScore = myScore;
        MainMenuData.DidLocalPlayerWin = localWon;

        // 4. Disable gameplay UI & enable fallback camera
        if (UIAssignmentManager.Instance != null)
        {
            if (UIAssignmentManager.Instance.hiderMasterUIGroup) UIAssignmentManager.Instance.hiderMasterUIGroup.SetActive(false);
            if (UIAssignmentManager.Instance.hunterUIGroup) UIAssignmentManager.Instance.hunterUIGroup.SetActive(false);
        }

        if (fallbackSceneCamera != null)
        {
            fallbackSceneCamera.SetActive(true);

            // 🟢 Fix: Ensure AudioListener on fallback camera is enabled for end-game sounds
            AudioListener fallbackListener = fallbackSceneCamera.GetComponent<AudioListener>();
            if (fallbackListener != null)
            {
                fallbackListener.enabled = true;
            }
        }

        // 5. Shutdown Netcode safely & process match counter/ads
        if (!isShuttingDown && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            isShuttingDown = true;

            if (LevelPlayAdManager.Instance != null)
            {
                bool adFinished = false;
                LevelPlayAdManager.Instance.ShowMatchEndInterstitial(() => {
                    adFinished = true;
                });
                yield return new WaitUntil(() => adFinished);
            }

            NetworkManager.Singleton.Shutdown();
            while (NetworkManager.Singleton.IsListening) yield return null;
        }

        // 6. Display correct Victory or Loss Panel offline
        if (localWon && victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            if (victoryPanelScoreText) victoryPanelScoreText.text = MainMenuData.FinalHidersScoreText;
            if (victoryHunterScoreText) victoryHunterScoreText.text = MainMenuData.FinalHuntersScoreText;
            if (victoryLocalScoreText) victoryLocalScoreText.text = $"YOUR SCORE: {MainMenuData.LocalPlayerScore}";
            if (audioSource != null && winClip != null) audioSource.PlayOneShot(winClip);
        }
        else if (lossPanel != null)
        {
            lossPanel.SetActive(true);
            if (lossPanelScoreText) lossPanelScoreText.text = MainMenuData.FinalHidersScoreText;
            if (lossHunterScoreText) lossHunterScoreText.text = MainMenuData.FinalHuntersScoreText;
            if (lossLocalScoreText) lossLocalScoreText.text = $"YOUR SCORE: {MainMenuData.LocalPlayerScore}";
            if (audioSource != null && loseClip != null) audioSource.PlayOneShot(loseClip);
        }
    }
    private void ToggleRenderers(GameObject target, bool setEnabled)
    {
        if (target == null) return;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (var ren in renderers)
        {
            if (ren.gameObject.GetComponent<Camera>() == null && !ren.gameObject.CompareTag("Gun"))
            {
                ren.enabled = setEnabled;
            }
        }
    }
}