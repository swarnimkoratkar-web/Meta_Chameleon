using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TutorialUIManager : MonoBehaviour
{
    public static TutorialUIManager Instance { get; private set; }

    [Header("Info Panels")]
    public GameObject panel1_GameInfo;
    public GameObject panel2_HunterInfo;
    public GameObject panel3_HiderInfo;

    [Header("Panel Navigation Buttons")]
    public Button panel1NextButton;
    public Button panel2NextButton;
    public Button panel3NextButton;
    public Button globalSkipButton;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Bind navigation click listeners
        if (panel1NextButton) panel1NextButton.onClick.AddListener(OnPanel1Next);
        if (panel2NextButton) panel2NextButton.onClick.AddListener(OnPanel2Next);
        if (panel3NextButton) panel3NextButton.onClick.AddListener(CompleteAndExitTutorial);
        if (globalSkipButton) globalSkipButton.onClick.AddListener(CompleteAndExitTutorial);

        StartIntroPanels();
    }

    private void StartIntroPanels()
    {
        if (panel1_GameInfo) panel1_GameInfo.SetActive(true);
        if (panel2_HunterInfo) panel2_HunterInfo.SetActive(false);
        if (panel3_HiderInfo) panel3_HiderInfo.SetActive(false);
    }

    private void OnPanel1Next()
    {
        if (panel1_GameInfo) panel1_GameInfo.SetActive(false);
        if (panel2_HunterInfo) panel2_HunterInfo.SetActive(true);
    }

    private void OnPanel2Next()
    {
        if (panel2_HunterInfo) panel2_HunterInfo.SetActive(false);
        if (panel3_HiderInfo) panel3_HiderInfo.SetActive(true);
    }

    private void CompleteAndExitTutorial()
    {
        PlayerPrefs.SetInt("HasCompletedTutorial", 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene("MainMenuScene");
    }
}