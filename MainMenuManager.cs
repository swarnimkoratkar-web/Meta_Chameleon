using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Button hostButton;
    public Button joinButton;
    public TMPro.TMP_InputField roomCodeInput; // Optional for now, required later for Relay

    [Header("Settings")]
    public string gameplaySceneName = "YourGameplaySceneName"; // ⚠️ Change this to your exact gameplay scene name!

    void Start()
    {
        // Hook up button clicks to code functions
        if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnHostClicked()
    {
        NetworkManager.Singleton.StartHost();

        // 2. Use Netcode's SceneManager to sync-load all players into the match map
        NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    private void OnJoinClicked()
    {
        NetworkManager.Singleton.StartClient();
    }
}