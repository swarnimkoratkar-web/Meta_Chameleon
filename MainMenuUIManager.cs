using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MainMenuUIManager : MonoBehaviour
{
    public static MainMenuUIManager Instance { get; private set; }

    [Header("Menu Panels")]
    public GameObject ProfilePannel;

    [Header("First Time Tutorial UI")]
    public GameObject firstTimeTutorialPanel;
    public Button tutorialYesButton;
    public Button tutorialNoButton;
    public Button tutorialButton;

    [Header("Public Matchmaking Panels")]
    public GameObject findMatchParentPanel;
    public GameObject publicJoinPanel;
    public GameObject publicCreatePanel;
    public GameObject playWithFriendsPanel;

    [Header("Disconnect Alert UI")]
    public GameObject disconnectDialogPanel;
    public TextMeshProUGUI disconnectReasonText;

    [Header("Settings Master UI")]
    public GameObject settingsParentPanel;

    [Header("Settings Sub-Panels")]
    public GameObject audioSubPanel;
    public GameObject controlsSubPanel;
    public GameObject privacySubPanel;
    public GameObject creditsSubPanel;

    [Header("Settings Navigation Buttons")]
    public Button openSettingsButton;
    public Button settingsBackButton;
    public Button audioTabButton;
    public Button controlsTabButton;
    public Button privacyTabButton;
    public Button creditsTabButton;

    [Header("Settings Sliders")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public Slider speakerSlider;
    public Slider sensitivitySlider;

    [Header("Sub-Panel Buttons")]
    public Button joinTabButton;
    public Button createTabButton;
    public Button refreshLobbiesButton;
    [Tooltip("Add all your panels here (including sub-panels) so they auto-close when switching menus!")]
    public List<GameObject> allPanels = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        CloseAllPanels();
        SetupSubTabListeners();
        SetupSettingsListeners(); 
        LoadSavedSettings();
        if (tutorialButton != null)
        {
            tutorialButton.onClick.RemoveAllListeners();
            tutorialButton.onClick.AddListener(OpenTutorialScene);
        }
        CheckFirstTimeTutorialPrompt();
        if (!string.IsNullOrEmpty(MainMenuData.LastDisconnectReason))
        {
            if (disconnectDialogPanel != null)
            {
                disconnectDialogPanel.SetActive(true);
            }

            if (disconnectReasonText != null)
            {
                disconnectReasonText.text = MainMenuData.LastDisconnectReason;
            }
            MainMenuData.LastDisconnectReason = "";
        }
    }
    public void OpenTutorialScene()
    {
        SceneManager.LoadScene("tut scene");
    }

    private void CheckFirstTimeTutorialPrompt()
    {
        // 0 = Has never seen prompt before, 1 = Has already been prompted/completed
        if (PlayerPrefs.GetInt("HasCompletedTutorial", 0) == 0)
        {
            if (firstTimeTutorialPanel != null)
            {
                firstTimeTutorialPanel.SetActive(true);
            }

            if (tutorialYesButton != null)
            {
                tutorialYesButton.onClick.RemoveAllListeners();
                tutorialYesButton.onClick.AddListener(OnTutorialYesClicked);
            }

            if (tutorialNoButton != null)
            {
                tutorialNoButton.onClick.RemoveAllListeners();
                tutorialNoButton.onClick.AddListener(OnTutorialNoClicked);
            }
        }
        else
        {
            if (firstTimeTutorialPanel != null)
            {
                firstTimeTutorialPanel.SetActive(false);
            }
        }
    }

    // Player clicked "YES" -> They have played before -> Skip tutorial & stay in Main Menu
    private void OnTutorialYesClicked()
    {
        PlayerPrefs.SetInt("HasCompletedTutorial", 1);
        PlayerPrefs.Save();

        if (firstTimeTutorialPanel != null)
        {
            firstTimeTutorialPanel.SetActive(false);
        }
    }

    private void OnTutorialNoClicked()
    {
        PlayerPrefs.SetInt("HasCompletedTutorial", 1);
        PlayerPrefs.Save();

        OpenTutorialScene();
    }
    public void CloseDisconnectDialog() => disconnectDialogPanel?.SetActive(false);
    private void SetupSubTabListeners()
    {
        if (joinTabButton != null) joinTabButton.onClick.AddListener(OpenJoinTab);
        if (createTabButton != null) createTabButton.onClick.AddListener(OpenCreateTab);
        if (refreshLobbiesButton != null) refreshLobbiesButton.onClick.AddListener(RefreshPublicLobbyList);
    }

    public void OpenPlayWithFriends()
    {
        ResetLobbyManagerUIContext();
        SwitchToPanel(playWithFriendsPanel);
    }

    public void OpenFindMatchMenu()
    {
        ResetLobbyManagerUIContext();
        SwitchToPanel(findMatchParentPanel);
        OpenJoinTab();
    }

    public void OpenJoinTab()
    {
        if (publicJoinPanel != null) publicJoinPanel.SetActive(true);
        if (publicCreatePanel != null) publicCreatePanel.SetActive(false);
        RefreshPublicLobbyList();
    }

    public void OpenCreateTab()
    {
        if (publicJoinPanel != null) publicJoinPanel.SetActive(false);
        if (publicCreatePanel != null) publicCreatePanel.SetActive(true);
    }

    public void RefreshPublicLobbyList()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.RefreshPublicLobbyList();
        }
    }

    private void ResetLobbyManagerUIContext()
    {
        if (LobbyManager.Instance != null)
        {
            if (LobbyManager.Instance.connectionStatusText != null)
                LobbyManager.Instance.connectionStatusText.text = "";

            if (LobbyManager.Instance.hostLobbyButton != null)
                LobbyManager.Instance.hostLobbyButton.interactable = true;
        }
    }

    public void CloseAllPanels()
    {
        foreach (GameObject panel in allPanels)
        {
            if (panel != null) panel.SetActive(false);
        }
    }

    public void SwitchToPanel(GameObject targetPanel)
    {
        CloseAllPanels();
        if (targetPanel != null) targetPanel.SetActive(true);
    }
    public void OpenProfilePannel()
    {
        ProfilePannel.SetActive(true);
    }
    public void CloseProfilePannel()
    {
        ProfilePannel.SetActive(false);
    }
    private void SetupSettingsListeners()
    {
        if (openSettingsButton != null) openSettingsButton.onClick.AddListener(OpenSettingsMenu);
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettingsMenu);

        if (audioTabButton != null) audioTabButton.onClick.AddListener(OpenAudioTab);
        if (controlsTabButton != null) controlsTabButton.onClick.AddListener(OpenControlsTab);
        if (privacyTabButton != null) privacyTabButton.onClick.AddListener(OpenPrivacyTab);
        if (creditsTabButton != null) creditsTabButton.onClick.AddListener(OpenCreditsTab);

        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        if (speakerSlider != null) speakerSlider.onValueChanged.AddListener(OnSpeakerVolumeChanged);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
    }

    private void LoadSavedSettings()
    {
        if (musicSlider != null) musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1.0f);
        if (sfxSlider != null) sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        if (speakerSlider != null) speakerSlider.value = PlayerPrefs.GetFloat("SpeakerVolume", 1.0f);
        if (sensitivitySlider != null) sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 0.2f);
    }
    public void OpenSettingsMenu()
    {
        SwitchToPanel(settingsParentPanel);
        OpenAudioTab();
    }

    public void CloseSettingsMenu() => settingsParentPanel?.SetActive(false);

    public void OpenAudioTab() => SwitchSettingsSubPanel(audioSubPanel);
    public void OpenControlsTab() => SwitchSettingsSubPanel(controlsSubPanel);
    public void OpenPrivacyTab() => SwitchSettingsSubPanel(privacySubPanel);
    public void OpenCreditsTab() => SwitchSettingsSubPanel(creditsSubPanel);

    private void SwitchSettingsSubPanel(GameObject targetSubPanel)
    {
        if (audioSubPanel != null) audioSubPanel.SetActive(false);
        if (controlsSubPanel != null) controlsSubPanel.SetActive(false);
        if (privacySubPanel != null) privacySubPanel.SetActive(false);
        if (creditsSubPanel != null) creditsSubPanel.SetActive(false);

        if (targetSubPanel != null) targetSubPanel.SetActive(true);
    }
    private void OnMusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
        AudioListener.volume = value;
    }

    private void OnSFXVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();
    }

    private void OnSpeakerVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("SpeakerVolume", value);
        PlayerPrefs.Save();
    }

    private void OnSensitivityChanged(float value)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();
    }
    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    public void ClosePlayWithFriends() => playWithFriendsPanel?.SetActive(false);
    public void CloseFindMatchMenu() => findMatchParentPanel?.SetActive(false);
}