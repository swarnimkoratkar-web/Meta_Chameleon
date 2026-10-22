using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WatchAdButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button watchAdButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private void Awake()
    {
        if (watchAdButton != null)
        {
            watchAdButton.onClick.AddListener(OnWatchAdButtonClicked);
        }
    }

    private void Start()
    {
        // Subscribe here to ensure LevelPlayAdManager.Instance is ready
        if (LevelPlayAdManager.Instance != null)
        {
            LevelPlayAdManager.Instance.OnRewardedViewsUpdated -= UpdateCounterUI;
            LevelPlayAdManager.Instance.OnRewardedViewsUpdated += UpdateCounterUI;
        }

        UpdateCounterUI();
    }

    private void OnEnable()
    {
        if (LevelPlayAdManager.Instance != null)
        {
            LevelPlayAdManager.Instance.OnRewardedViewsUpdated -= UpdateCounterUI;
            LevelPlayAdManager.Instance.OnRewardedViewsUpdated += UpdateCounterUI;
        }

        UpdateCounterUI();
    }

    private void OnDisable()
    {
        if (LevelPlayAdManager.Instance != null)
        {
            LevelPlayAdManager.Instance.OnRewardedViewsUpdated -= UpdateCounterUI;
        }
    }

    private void OnWatchAdButtonClicked()
    {
        if (LevelPlayAdManager.Instance != null)
        {
            LevelPlayAdManager.Instance.ShowRewardedAd();
        }
    }

    public void UpdateCounterUI()
    {
        if (LevelPlayAdManager.Instance == null) return;

        if (LevelPlayAdManager.Instance.IsNoAdsPassActive())
        {
            if (statusText != null) statusText.text = "Reward Granted!";
            if (watchAdButton != null) watchAdButton.interactable = false;
            return;
        }

        int currentViews = LevelPlayAdManager.Instance.GetRewardedViewsCount();

        if (statusText != null)
        {
            statusText.text = $"Watch ({currentViews}/4) to skip ads for 12 hrs";
        }

        if (watchAdButton != null)
        {
            watchAdButton.interactable = true;
        }
    }
}