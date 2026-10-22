using UnityEngine;
using UnityEngine.UI;

public class UpdatePanelManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject updatePanel;
    [SerializeField] private Button okButton;

    private const string SAVED_VERSION_KEY = "LastSeenAppVersion";

    private void Start()
    {
        // Setup OK button click listener
        if (okButton != null)
        {
            okButton.onClick.AddListener(CloseUpdatePanel);
        }

        CheckForNewVersion();
    }

    private void CheckForNewVersion()
    {
        string currentVersion = Application.version;
        string savedVersion = PlayerPrefs.GetString(SAVED_VERSION_KEY, "");

        // Show panel ONLY if this version hasn't been acknowledged yet
        if (savedVersion != currentVersion)
        {
            if (updatePanel != null)
            {
                updatePanel.SetActive(true);
            }
        }
        else
        {
            if (updatePanel != null)
            {
                updatePanel.SetActive(false);
            }
        }
    }

    public void CloseUpdatePanel()
    {
        // 1. Save current version so the panel never opens again for this update
        PlayerPrefs.SetString(SAVED_VERSION_KEY, Application.version);
        PlayerPrefs.Save();

        // 2. Hide the panel
        if (updatePanel != null)
        {
            updatePanel.SetActive(false);
        }
    }
}