using UnityEngine;
using UnityEngine.UI;

public class InGameMenuController : MonoBehaviour
{
    public static InGameMenuController Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject controlPannel;

    [Header("Menu Control Buttons")]
    [SerializeField] private Button menuButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Audio Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider speakerSlider;
    [SerializeField] private Slider sensitivitySlider;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);

        // Hook up button click listeners
        if (menuButton != null) menuButton.onClick.AddListener(OpenMenuPanel);
        if (resumeButton != null) resumeButton.onClick.AddListener(CloseMenuPanel);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(QuitToMainMenu);

        // 🟢 LOAD SAVED AUDIO VOLUMES & ADD LISTENERS
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1.0f);
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            OnMusicVolumeChanged(musicSlider.value);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            OnSFXVolumeChanged(sfxSlider.value);
        }

        if (speakerSlider != null)
        {
            speakerSlider.value = PlayerPrefs.GetFloat("SpeakerVolume", 1.0f);
            speakerSlider.onValueChanged.AddListener(OnSpeakerVolumeChanged);
            OnSpeakerVolumeChanged(speakerSlider.value);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 0.2f);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            OnSensitivityChanged(sensitivitySlider.value);
        }
    }
    private void OnSensitivityChanged(float val)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", val);
        PlayerPrefs.Save();

        // 🟢 Update active local player sensitivity if spawned in scene
        var hunter = FindAnyObjectByType<HunterController>();
        if (hunter != null && hunter.IsOwner) hunter.lookSensitivity = val;

        var hider = FindAnyObjectByType<HiderMovement>();
        if (hider != null && hider.IsOwner)
        {
            hider.lookSensitivity = val;
            hider.orbitSensitivity = val;
        }
    }
    public void OpenMenuPanel()
    {
        menuPanel?.SetActive(true);
    }
    public void OpenControlPanel()
    {
        controlPannel?.SetActive(true);
    }
    public void CloseMenuPanel() => menuPanel?.SetActive(false);
    public void CloseControlPanel() => controlPannel?.SetActive(false);

    public void QuitToMainMenu()
    {
        CloseMenuPanel();
        if (GameMatchEngine.Instance != null)
        {
            GameMatchEngine.Instance.ExecuteGlobalShutdown();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
        AudioListener.volume = value; // Master volume fallback
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

        if (VoiceChatManager.Instance != null)
        {
            VoiceChatManager.Instance.SetSpeakerVolume(value);
        }
    }

    private void OnDestroy()
    {
        if (menuButton != null) menuButton.onClick.RemoveListener(OpenMenuPanel);
        if (resumeButton != null) resumeButton.onClick.RemoveListener(CloseMenuPanel);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(QuitToMainMenu);
    }
}