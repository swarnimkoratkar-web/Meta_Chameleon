using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class SpectatorManager : MonoBehaviour
{
    public static SpectatorManager Instance { get; private set; }

    // Unified target representation for both Human and Bot Hunters
    private class SpectatorTarget
    {
        public GameObject rootObject;
        public Transform cameraHolder;
        public string displayName;

        public bool IsValid => rootObject != null;
    }

    private List<SpectatorTarget> activeHunters = new List<SpectatorTarget>();
    private int currentTargetIndex = 0;

    private bool isSpectatingModeActive = false;

    // Cached references for the local client session
    private Transform cachedLocalCameraHolder;
    private HiderMovement cachedLocalHiderMovement;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (isSpectatingModeActive)
        {
            SpectatorTarget targetHunter = GetCurrentTarget();
            if (targetHunter == null || !targetHunter.IsValid)
            {
                SpectateNextPlayer();
            }
        }
    }

    // 🎯 Step 1: Initialize the Spectator mode loop
    public void StartSpectating(Transform cameraHolder, HiderMovement hiderMovement)
    {
        if (isSpectatingModeActive) return;

        cachedLocalCameraHolder = cameraHolder;
        cachedLocalHiderMovement = hiderMovement;

        FindAllActiveHunters();

        if (activeHunters.Count == 0)
        {
            Debug.LogWarning("[Spectator System] No Hunters found in the current match to watch!");
            return;
        }

        // Reset the mobile controls joystick
        var ui = UIAssignmentManager.Instance;
        if (ui != null && ui.hiderMoveJoystick != null)
        {
            ui.hiderMoveJoystick.OnPointerUp(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
        }

        // Disable local Hider camera
        if (cachedLocalCameraHolder != null)
        {
            Camera hiderCam = cachedLocalCameraHolder.GetComponentInChildren<Camera>(true);
            if (hiderCam != null) hiderCam.enabled = false;

            AudioListener hiderListener = cachedLocalCameraHolder.GetComponentInChildren<AudioListener>(true);
            if (hiderListener != null) hiderListener.enabled = false;
        }

        isSpectatingModeActive = true;
        currentTargetIndex = 0;

        // UI Panel switch
        if (ui != null)
        {
            if (ui.hiderPaintAndMovementGroup != null) ui.hiderPaintAndMovementGroup.SetActive(false);
            if (ui.spectatorUIGroup != null) ui.spectatorUIGroup.SetActive(true);
        }

        UpdateSpectatorTargetView();
    }

    // 🎯 Step 2: Stop Spectating and return safely back to the Hider body
    public void StopSpectating()
    {
        if (!isSpectatingModeActive) return;

        isSpectatingModeActive = false;

        // Shut down current remote camera
        SpectatorTarget currentTarget = GetCurrentTarget();
        if (currentTarget != null && currentTarget.cameraHolder != null)
        {
            SetTargetCameraActive(currentTarget, false);
        }

        // Re-enable local Hider camera
        if (cachedLocalCameraHolder != null)
        {
            Camera hiderCam = cachedLocalCameraHolder.GetComponentInChildren<Camera>(true);
            if (hiderCam != null) hiderCam.enabled = true;

            AudioListener hiderListener = cachedLocalCameraHolder.GetComponentInChildren<AudioListener>(true);
            if (hiderListener != null) hiderListener.enabled = true;
        }

        var ui = UIAssignmentManager.Instance;
        if (ui != null)
        {
            if (ui.spectatorUIGroup != null) ui.spectatorUIGroup.SetActive(false);
            if (ui.hiderPaintAndMovementGroup != null) ui.hiderPaintAndMovementGroup.SetActive(true);
        }

        cachedLocalCameraHolder = null;
        cachedLocalHiderMovement = null;
    }

    // 🎯 Step 3: Cycle target index forward
    public void SpectateNextPlayer()
    {
        if (!isSpectatingModeActive) return;

        FindAllActiveHunters();

        if (activeHunters.Count == 0)
        {
            StopSpectating();
            return;
        }

        currentTargetIndex = (currentTargetIndex + 1) % activeHunters.Count;
        UpdateSpectatorTargetView();
    }

    private void FindAllActiveHunters()
    {
        activeHunters.Clear();

        // 1. Collect Human Hunters
        HunterController[] humanHunters = FindObjectsByType<HunterController>(FindObjectsSortMode.None);
        foreach (var hunter in humanHunters)
        {
            if (hunter != null)
            {
                string pName = hunter.gameObject.name;
                NetworkObject netObj = hunter.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    pName = MainMenuData.GetPlayerName(netObj.OwnerClientId);
                }

                activeHunters.Add(new SpectatorTarget
                {
                    rootObject = hunter.gameObject,
                    cameraHolder = hunter.hunterCameraHolder,
                    displayName = pName
                });
            }
        }

        // 2. Collect Bot Hunters
        BotHunterAI[] botHunters = FindObjectsByType<BotHunterAI>(FindObjectsSortMode.None);
        foreach (var bot in botHunters)
        {
            if (bot != null)
            {
                // Attempt to grab camera holder from bot hierarchy, fallback to bot's transform
                Transform camHolder = bot.transform.Find("CameraHolder");
                if (camHolder == null) camHolder = bot.transform;

                string botName = bot.gameObject.name;
                NetworkObject netObj = bot.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    botName = MainMenuData.GetPlayerName(netObj.OwnerClientId);
                }

                activeHunters.Add(new SpectatorTarget
                {
                    rootObject = bot.gameObject,
                    cameraHolder = camHolder,
                    displayName = string.IsNullOrEmpty(botName) ? "Hunter Bot" : botName
                });
            }
        }
    }

    private SpectatorTarget GetCurrentTarget()
    {
        if (activeHunters.Count == 0) return null;

        if (currentTargetIndex >= activeHunters.Count)
        {
            currentTargetIndex = 0;
        }

        return activeHunters[currentTargetIndex];
    }

    private void UpdateSpectatorTargetView()
    {
        SpectatorTarget target = GetCurrentTarget();
        var ui = UIAssignmentManager.Instance;

        // Toggle camera visibility per target
        foreach (var hunter in activeHunters)
        {
            if (hunter == null || hunter.cameraHolder == null) continue;
            SetTargetCameraActive(hunter, hunter == target);
        }

        if (target != null && ui != null && ui.spectatedPlayerNameText != null)
        {
            ui.spectatedPlayerNameText.text = "SPECTATING: " + target.displayName;
        }
    }

    private void SetTargetCameraActive(SpectatorTarget target, bool state)
    {
        if (target == null || target.cameraHolder == null) return;

        // Check for "Main Camera (1)" or fallback to any Camera in child hierarchy
        Transform remoteCam = target.cameraHolder.Find("Main Camera (1)");
        if (remoteCam != null)
        {
            remoteCam.gameObject.SetActive(state);
            return;
        }

        Camera cam = target.cameraHolder.GetComponentInChildren<Camera>(true);
        if (cam != null)
        {
            cam.gameObject.SetActive(state);
        }
    }
}