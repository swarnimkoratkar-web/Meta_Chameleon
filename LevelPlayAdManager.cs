using System;
using UnityEngine;
using Unity.Services.LevelPlay;

public class LevelPlayAdManager : MonoBehaviour
{
    public static LevelPlayAdManager Instance { get; private set; }
    public event Action OnRewardedViewsUpdated;
    [Header("LevelPlay Settings")]
    [Tooltip("Replace this with your actual App Key from LevelPlay Dashboard")]
    [SerializeField] private string appKey = "27523a225";
    [SerializeField] private string interstitialId = "9rmhqd5w34c57c8u";
     [SerializeField] private string rewardedId = "bqlpagwag3ssuqyj";
    //[SerializeField] private bool enableTestSuite = false;
    private int retryCount = 0;
    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayRewardedAd rewardedAd;

    private int currentRewardedViews = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        LevelPlay.OnInitSuccess += OnSdkInitialized;
        LevelPlay.OnInitFailed += (error) => Debug.LogError($"[Ad System] Init Failed: {error.ErrorMessage}");
        LevelPlay.Init(appKey);
        currentRewardedViews = PlayerPrefs.GetInt("CurrentRewardedViews", 0);
    }

    private void OnSdkInitialized(LevelPlayConfiguration config)
    {
   //     if (enableTestSuite) LevelPlay.LaunchTestSuite();
        // 1. Setup Interstitial
        interstitialAd = new LevelPlayInterstitialAd(interstitialId);

        // Reset retry counter on successful load
        interstitialAd.OnAdLoaded += (info) => {
            retryCount = 0;
        };

        // Smart single retry if the load fails during gameplay
        interstitialAd.OnAdLoadFailed += (error) => {
            Debug.LogError($"[Ad System] Interstitial Load Failed: {error.ErrorMessage}");

            if (retryCount < 2)
            {
                retryCount++;
                Invoke(nameof(LoadInterstitial), 10f);
            }
        };

        interstitialAd.OnAdClosed += (info) => {
            // DO NOT auto-reload ad here
        };

           // 2. Setup Rewarded
           rewardedAd = new LevelPlayRewardedAd(rewardedId);
           rewardedAd.OnAdLoaded += (info) =>
           {
           };
           rewardedAd.OnAdLoadFailed += (error) => {
               Debug.LogError($"[Ad System] Rewarded Ad Failed To Load: {error.ErrorMessage}. Retrying in 5s...");
               Invoke(nameof(ReloadRewardedAd), 5f);
           };

           rewardedAd.OnAdRewarded += OnRewardedAdCompleted;
           rewardedAd.OnAdClosed += (info) => {
               rewardedAd.LoadAd();
           };

           rewardedAd.LoadAd();
        
    }

    // 🎬 Helper method required for retry logic
      private void ReloadRewardedAd()
       {
           if (rewardedAd != null && !rewardedAd.IsAdReady())
           {
               rewardedAd.LoadAd();
           }
       }
    public void LoadInterstitial()
    {
        if (IsNoAdsPassActive()) return;

        if (interstitialAd != null && !interstitialAd.IsAdReady())
        {
            interstitialAd.LoadAd();
        }
    }
    public void ShowMatchEndInterstitial(Action onAdClosed)
    {
        if (IsNoAdsPassActive())
        {
            onAdClosed?.Invoke();
            return;
        }

        if (interstitialAd != null && interstitialAd.IsAdReady())
        {
            PlayerPrefs.SetInt("MatchesPlayedCount", 0);
            PlayerPrefs.Save();
            Action<LevelPlayAdInfo> closedHandler = null;
            closedHandler = (info) =>
            {
                interstitialAd.OnAdClosed -= closedHandler; // Unsubscribe cleanup
                onAdClosed?.Invoke();                       // Execute scene load
            };

            interstitialAd.OnAdClosed += closedHandler;
            interstitialAd.ShowAd();
        }
        else
        {
            Debug.LogWarning("[Ad System] Interstitial Ad not ready yet.");
            onAdClosed?.Invoke();
        }
    }

     public void ShowRewardedAd()
      {
          if (rewardedAd != null && rewardedAd.IsAdReady())
          {
              rewardedAd.ShowAd();
          }
          else
          {
              Debug.LogWarning("[Ad System] Rewarded Ad not ready yet.");
          }
      }

    private void OnRewardedAdCompleted(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        currentRewardedViews++;

        if (currentRewardedViews >= 3)
        {
            DateTime expirationTime = DateTime.Now.AddHours(12);
            PlayerPrefs.SetString("NoAdsExpirationTime", expirationTime.ToString());
            currentRewardedViews = 0; // Reset counter
        }
        PlayerPrefs.SetInt("CurrentRewardedViews", currentRewardedViews);
        PlayerPrefs.Save();

        OnRewardedViewsUpdated?.Invoke();
    }

    public bool IsNoAdsPassActive()
    {
        string savedTime = PlayerPrefs.GetString("NoAdsExpirationTime", "");
        if (string.IsNullOrEmpty(savedTime)) return false;
        if (DateTime.TryParse(savedTime, out DateTime expirationTime))
        {
            return DateTime.Now < expirationTime;
        }
        return false;
    }

    public int GetRewardedViewsCount() => currentRewardedViews;
}