using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIAssignmentManager : MonoBehaviour
{
    public static UIAssignmentManager Instance { get; private set; }
    public static bool IsMicMuted { get; private set; } = true;
    public static bool IsSpeakerMuted { get; private set; } = false;

    [Header("Global UI Panels")]
    public GameObject hunterUIGroup;
    public GameObject hiderMasterUIGroup;
    public GameObject hiderMovementUIGroup;
    public GameObject hiderPaintUIGroup;
    public GameObject hiderPaintAndMovementGroup; 
    public GameObject freeRoamUIGroup;

    [Header("Dedicated Joysticks")]
    public MobileJoystick hunterJoystick;    
    public MobileJoystick hiderMoveJoystick;
    public MobileJoystick hiderPaintJoystick;

    [Header("Spectator System UI")]
    public GameObject spectatorUIGroup;
    public Button enterSpectateButton;
    public Button exitSpectateButton;
    public Button nextSpectateTargetButton;
    public TextMeshProUGUI spectatedPlayerNameText;

    [Header("Hunter Controls")]
    public Button hunterFireButton;
    public EventTrigger hunterAimPanel;
    public RectTransform hunterCrosshair;
    public Button hunterJumpButton;
    public Button hunterClimbButton;

    [Header("Hider Controls")]
    public EventTrigger hiderAimPanel;
    public EventTrigger hiderOrbitPanel;
    public RectTransform hiderPaintCrosshair;
    public EventTrigger hiderPaintButton;
    public Slider hiderZoomSlider;
    public Button hiderDropperButton;
    public Button hiderToggleModeButton;
    public Slider hiderBrushSizeSlider;
    public Button hiderClimbButton;
    public Image hiderColorPreviewImage;
    public Button hiderWhistleButton;
    public Slider hiderStickRotationSlider;
    public Button hiderJumpButton;
    public Button[] hiderQuickColorButtons; 
    public Color[] quickColors = new Color[6] { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta, Color.cyan }; // Default fallback matrix

    [Header("Free Roam Scene Controls")]
    public MobileJoystick freeRoamJoystick;
    public EventTrigger freeRoamAimPanel;
    public Button enterFreeRoamButton; // 🆕 Put the button sitting inside HiderPaint&Movement here
    public Button exitFreeRoamButton;

    [Header("Voice Call Controls")]
    private bool isInVoiceCall = false;
    public Button micToggleButton;
    public Sprite micOnSprite;
    public Sprite micOffSprite;
    public Sprite speakerOnSprite;
    public Sprite speakerOffSprite;
    [Header("Voice Call Confirmation UI")]
    public GameObject joinCallConfirmationPanel;
    public Button confirmJoinCallYesButton;
    public Button confirmJoinCallNoButton;

    [Header("Voice Call Controls")]
    public Button callActionButton;             // Main Call / Cut Call button
    public Sprite startCallSprite;               // Green Phone (Join)
    public Sprite endCallSprite;

    void Awake()
    {
        Instance = this;
        if (hunterUIGroup) hunterUIGroup.SetActive(false);
        if (hiderMasterUIGroup) hiderMasterUIGroup.SetActive(false);

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = false;
            canvas.enabled = true;
        }
    }

    private void Start()
    {
        // voice chat AssignVoiceChatClickHandlers();
        // SetupCallButtonSystem();
        // UpdateVoiceCallUI();

        // voice chat 🟢 Force hide voice UI completely:
        if (callActionButton != null) callActionButton.gameObject.SetActive(false);
        if (micToggleButton != null) micToggleButton.gameObject.SetActive(false);
        if (joinCallConfirmationPanel != null) joinCallConfirmationPanel.SetActive(false);
    }
    private void SetupCallButtonSystem()
    {
        if (callActionButton != null)
        {
            callActionButton.onClick.RemoveAllListeners();
            callActionButton.onClick.AddListener(OnCallActionButtonClicked);
        }

        if (confirmJoinCallYesButton != null)
        {
            confirmJoinCallYesButton.onClick.RemoveAllListeners();
            confirmJoinCallYesButton.onClick.AddListener(ConfirmAndJoinCall);
        }

        if (confirmJoinCallNoButton != null)
        {
            confirmJoinCallNoButton.onClick.RemoveAllListeners();
            confirmJoinCallNoButton.onClick.AddListener(CloseCallConfirmationPanel);
        }

        if (joinCallConfirmationPanel != null)
        {
            joinCallConfirmationPanel.SetActive(false);
        }
    }

    private void OnCallActionButtonClicked()
    {
        if (!MainMenuData.IsFriendsMode) return;
        if (!isInVoiceCall)
        {
            if (joinCallConfirmationPanel != null)
            {
                joinCallConfirmationPanel.SetActive(true);
            }
        }
        else
        {
            DisconnectFromCall();
        }
    }

    public void ConfirmAndJoinCall()
    {
        CloseCallConfirmationPanel();

        if (VoiceChatManager.Instance != null && !string.IsNullOrEmpty(VoiceChatManager.Instance.ActiveVoiceChannelToken))
        {
            VoiceChatManager.Instance.JoinRoomVoiceChannel(VoiceChatManager.Instance.ActiveVoiceChannelToken);
            isInVoiceCall = true;
            UpdateVoiceCallUI();
        }
    }

    public void CloseCallConfirmationPanel()
    {
        if (joinCallConfirmationPanel != null)
        {
            joinCallConfirmationPanel.SetActive(false);
        }
    }
    public void DisconnectFromCall()
    {
        if (VoiceChatManager.Instance != null)
        {
            _ = VoiceChatManager.Instance.LeaveCurrentVoiceChannel();
        }

        isInVoiceCall = false;
        UpdateVoiceCallUI();
    }

    private void UpdateVoiceCallUI()
    {
        if (!MainMenuData.IsFriendsMode)
        {
            if (callActionButton != null) callActionButton.gameObject.SetActive(false);
            if (micToggleButton != null) micToggleButton.gameObject.SetActive(false);
            if (joinCallConfirmationPanel != null) joinCallConfirmationPanel.SetActive(false);
            return;
        }
        if (micToggleButton != null)
        {
            micToggleButton.gameObject.SetActive(isInVoiceCall);
        }
        if (callActionButton != null)
        {
            callActionButton.gameObject.SetActive(true);

            Image callImg = callActionButton.GetComponent<Image>();
            if (callImg != null)
            {
                if (isInVoiceCall && endCallSprite != null)
                {
                    callImg.sprite = endCallSprite;
                }
                else if (!isInVoiceCall && startCallSprite != null)
                {
                    callImg.sprite = startCallSprite;
                }
            }
        }
    }
    public void ToggleVoiceCallConnection()
    {
        if (VoiceChatManager.Instance == null) return;

        if (!isInVoiceCall)
        {
            // 🟢 Join the voice room
            if (!string.IsNullOrEmpty(VoiceChatManager.Instance.ActiveVoiceChannelToken))
            {
                VoiceChatManager.Instance.JoinRoomVoiceChannel(VoiceChatManager.Instance.ActiveVoiceChannelToken);
                isInVoiceCall = true;
            }
        }
        else
        {
            // 🔴 Leave the voice room
            VoiceChatManager.Instance.LeaveCurrentVoiceChannel();
            isInVoiceCall = false;
        }

        UpdateVoiceCallUI();
    }

    public void SetupHunterUI(HunterCombat combat, HunterController controller)
    {
        if (hiderMasterUIGroup != null) hiderMasterUIGroup.SetActive(false);
        if (hunterUIGroup != null) hunterUIGroup.SetActive(true);

        if (controller != null) controller.joystick = hunterJoystick;
        if (combat != null) combat.hunterCrosshair = hunterCrosshair;

        if (hunterFireButton != null)
        {
            hunterFireButton.onClick.RemoveAllListeners();
            hunterFireButton.onClick.AddListener(() => {
                if (combat != null) combat.TryFireWeapon();
            });
        }
        if (hunterJumpButton != null && controller != null)
        {
            hunterJumpButton.onClick.RemoveAllListeners();
            hunterJumpButton.onClick.AddListener(controller.PressJumpButtonLocal);
        }
        if (hunterClimbButton != null && controller != null)
        {
            hunterClimbButton.onClick.RemoveAllListeners();
            hunterClimbButton.onClick.AddListener(controller.ToggleWallStickButtonAction);
        }
        if (hunterAimPanel != null && controller != null)
        {
            hunterAimPanel.triggers.Clear();
            EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener((data) => { controller.CustomPointerDown(data); });
            hunterAimPanel.triggers.Add(down);

            EventTrigger.Entry drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener((data) => { controller.CustomDrag(data); });
            hunterAimPanel.triggers.Add(drag);
        }
        AssignVoiceChatClickHandlers();
        UpdateVoiceButtonsVisuals();
    }

    public void SetupHiderUI(HiderMovement movement, HiderUIToggle toggle, CharacterPainter painter)
    {
        if (hunterUIGroup != null) hunterUIGroup.SetActive(false);
        if (hiderMasterUIGroup != null) hiderMasterUIGroup.SetActive(true);
        if (hiderMovementUIGroup != null) hiderMovementUIGroup.SetActive(true);
        if (hiderPaintUIGroup != null) hiderPaintUIGroup.SetActive(false);

        if (movement != null) movement.moveJoystick = hiderMoveJoystick;
        if (painter != null) painter.crosshair = hiderPaintCrosshair;

        if (hiderAimPanel != null && movement != null)
        {
            hiderAimPanel.triggers.Clear();
            EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener((data) => { movement.CustomPointerDown(data); });
            hiderAimPanel.triggers.Add(down);

            EventTrigger.Entry drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener((data) => { movement.CustomDrag(data); });
            hiderAimPanel.triggers.Add(drag);
        }

        if (hiderWhistleButton != null && movement != null)
        {
            hiderWhistleButton.onClick.RemoveAllListeners();
            hiderWhistleButton.onClick.AddListener(movement.PressWhistleButtonLocal);
        }
        if (hiderJumpButton != null && movement != null)
        {
            hiderJumpButton.onClick.RemoveAllListeners();
            hiderJumpButton.onClick.AddListener(movement.PressJumpButtonLocal);
        }
        if (hiderStickRotationSlider != null && movement != null)
        {
            hiderStickRotationSlider.gameObject.SetActive(false);
            hiderStickRotationSlider.onValueChanged.RemoveAllListeners();
            hiderStickRotationSlider.minValue = 0f;
            hiderStickRotationSlider.maxValue = 360f;
            hiderStickRotationSlider.value = 0f;

            hiderStickRotationSlider.onValueChanged.AddListener((value) => {
                var walker = movement.GetComponent<SurfaceWalker>();
                if (walker != null) walker.SetManualPoseRotation(value);
            });
        }

        if (hiderOrbitPanel != null && movement != null)
        {
            hiderOrbitPanel.triggers.Clear();
            EventTrigger.Entry downOrbit = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downOrbit.callback.AddListener((data) => { movement.CustomOrbitPointerDown(data); });
            hiderOrbitPanel.triggers.Add(downOrbit);

            EventTrigger.Entry dragOrbit = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragOrbit.callback.AddListener((data) => { movement.CustomOrbitDrag(data); });
            hiderOrbitPanel.triggers.Add(dragOrbit);
        }

        if (hiderDropperButton != null && painter != null)
        {
            hiderDropperButton.onClick.RemoveAllListeners();
            hiderDropperButton.onClick.AddListener(painter.SampleColorFromWorld);
        }

        if (hiderPaintButton != null && painter != null)
        {
            hiderPaintButton.triggers.Clear();
            EventTrigger.Entry pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener((data) => { painter.SetSpraying(true); });
            hiderPaintButton.triggers.Add(pointerDown);

            EventTrigger.Entry pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pointerUp.callback.AddListener((data) => { painter.SetSpraying(false); });
            hiderPaintButton.triggers.Add(pointerUp);
        }

        if (hiderZoomSlider != null && movement != null)
        {
            hiderZoomSlider.onValueChanged.RemoveAllListeners();
            hiderZoomSlider.minValue = 1.5f;
            hiderZoomSlider.maxValue = 7.0f;

            hiderZoomSlider.onValueChanged.AddListener((value) => {
                movement.ForceCameraZoomRefresh();
            });
        }
        if (hiderClimbButton != null && movement != null)
        {
            hiderClimbButton.onClick.RemoveAllListeners();
            hiderClimbButton.onClick.AddListener(movement.ToggleWallStickButtonAction);
        }

        if (hiderQuickColorButtons != null && painter != null)
        {
            for (int i = 0; i < hiderQuickColorButtons.Length; i++)
            {
                if (hiderQuickColorButtons[i] == null) continue;

                int index = i;
                hiderQuickColorButtons[i].onClick.RemoveAllListeners();
                hiderQuickColorButtons[i].onClick.AddListener(() => {
                    Color chosenColor = (index < quickColors.Length) ? quickColors[index] : Color.white;
                    painter.SetBrushColorLocal(chosenColor);
                });
            }
        }
        // 🎯 LINK SPECTATOR BUTTONS
        if (enterSpectateButton != null)
        {
            enterSpectateButton.onClick.RemoveAllListeners();
            enterSpectateButton.onClick.AddListener(() => {
                if (SpectatorManager.Instance != null)
                    SpectatorManager.Instance.StartSpectating(movement.hiderCameraHolder, movement);
            });
        }

        if (exitSpectateButton != null)
        {
            exitSpectateButton.onClick.RemoveAllListeners();
            exitSpectateButton.onClick.AddListener(() => {
                if (SpectatorManager.Instance != null)
                    SpectatorManager.Instance.StopSpectating();
            });
        }

        if (nextSpectateTargetButton != null)
        {
            nextSpectateTargetButton.onClick.RemoveAllListeners();
            nextSpectateTargetButton.onClick.AddListener(() => {
                if (SpectatorManager.Instance != null)
                    SpectatorManager.Instance.SpectateNextPlayer();
            });
        }

        // Ensure spectator panel starts off by default when spawning
        if (spectatorUIGroup != null) spectatorUIGroup.SetActive(false);
        // ==========================================
        // 🎯 🆕 MOVE DYNAMIC HOOK MATRIX TO THE END
        // ==========================================
        if (toggle != null && toggle.freeRoamCamera != null)
        {
            var freeRoamCtrl = toggle.freeRoamCamera.GetComponent<FreeRoamCameraController>();
            if (freeRoamCtrl != null)
            {
                freeRoamCtrl.freeRoamJoystick = freeRoamJoystick;

                if (freeRoamAimPanel != null)
                {
                    freeRoamAimPanel.triggers.Clear();

                    EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                    down.callback.AddListener((data) => { freeRoamCtrl.CustomPointerDown(data); });
                    freeRoamAimPanel.triggers.Add(down);

                    EventTrigger.Entry drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
                    drag.callback.AddListener((data) => { freeRoamCtrl.CustomDrag(data); });
                    freeRoamAimPanel.triggers.Add(drag);
                }
            }
        }

        if (toggle != null)
        {
            if (enterFreeRoamButton != null)
            {
                enterFreeRoamButton.onClick.RemoveAllListeners();
                enterFreeRoamButton.onClick.AddListener(toggle.ToggleFreeRoamMode);
            }

            if (exitFreeRoamButton != null)
            {
                exitFreeRoamButton.onClick.RemoveAllListeners();
                exitFreeRoamButton.onClick.AddListener(toggle.ToggleFreeRoamMode);
            }
        }
        AssignVoiceChatClickHandlers();
        UpdateVoiceButtonsVisuals();
    }

    private void AssignVoiceChatClickHandlers()
    {
        if (micToggleButton != null)
        {
            micToggleButton.onClick.RemoveAllListeners();
            micToggleButton.onClick.AddListener(() => {
                if (VoiceChatManager.Instance != null)
                {
                    VoiceChatManager.Instance.ToggleMicState();
                    IsMicMuted = !IsMicMuted;
                    UpdateVoiceButtonsVisuals();
                }
            });
        }
    }

    public static void ToggleGlobalSpeakerState()
    {
        if (VoiceChatManager.Instance != null)
        {
            VoiceChatManager.Instance.ToggleSpeakerState();
        }

        IsSpeakerMuted = !IsSpeakerMuted;

        // Force UI instance to instantly update artwork elements 
        if (Instance != null)
        {
            Instance.UpdateVoiceButtonsVisuals();
        }
    }

    public void UpdateVoiceButtonsVisuals()
    {
        // Swap Mic Textures
        if (micToggleButton != null)
        {
            Image micImg = micToggleButton.GetComponent<Image>();
            if (micImg != null && micOnSprite != null && micOffSprite != null)
            {
                micImg.sprite = IsMicMuted ? micOffSprite : micOnSprite;
            }
        }
    }
    public void ClickExitToMainMenuDuringMatch()
    {
        // 1. Fetch the active match state
        GameMatchEngine.MatchState currentState = GameMatchEngine.Instance != null
            ? GameMatchEngine.Instance.CurrentState.Value
            : GameMatchEngine.MatchState.PostGame;

        // 2. Quit in HIDING MODE -> Increment MatchesPlayedCount & require 2 quits for ad
        if (currentState == GameMatchEngine.MatchState.Hiding)
        {
            int matchesPlayed = PlayerPrefs.GetInt("MatchesPlayedCount", 0) + 1;

            if (matchesPlayed >= 2 && LevelPlayAdManager.Instance != null)
            {
                LevelPlayAdManager.Instance.ShowMatchEndInterstitial(() => {
                    ClickExitToMainMenuManual();
                });
            }
            else
            {
                PlayerPrefs.SetInt("MatchesPlayedCount", matchesPlayed);
                PlayerPrefs.Save();
                ClickExitToMainMenuManual(); // Exits without ad
            }
        }
        // 3. Quit in HUNTING / ANSWER CHECK / OTHER -> Show ad DIRECTLY
        else
        {
            if (LevelPlayAdManager.Instance != null)
            {
                LevelPlayAdManager.Instance.ShowMatchEndInterstitial(() => {
                    ClickExitToMainMenuManual();
                });
            }
            else
            {
                ClickExitToMainMenuManual();
            }
        }
    }

    public void ClickExitToMainMenuManual()
    {
        if (VoiceChatManager.Instance != null)
        {
            _ = VoiceChatManager.Instance.LeaveCurrentVoiceChannel();
            VoiceChatManager.Instance.ActiveVoiceChannelToken = null;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        MainMenuData.ClearAllData();

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }
}