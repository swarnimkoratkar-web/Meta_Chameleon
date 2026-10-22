using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class StartupLoadingScreen : MonoBehaviour
{
    private static bool hasAppBeenOpenedBefore = false;

    [Header("UI References")]
    [SerializeField] private GameObject loadingScreenPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text loadingText;

    [Header("Timing Settings")]
    [SerializeField] private float totalDuration = 5f;

    [Header("Random Loading Messages")]
    [TextArea(2, 4)]
    [SerializeField] private string[] loadingMessages;

    private void Awake()
    {
        if (loadingScreenPanel == null) loadingScreenPanel = gameObject;

        if (!hasAppBeenOpenedBefore)
        {
            hasAppBeenOpenedBefore = true;
            loadingScreenPanel.SetActive(true);

            if (progressBar != null)
            {
                progressBar.minValue = 0f;
                progressBar.maxValue = 1f;
                progressBar.value = 0f;
            }

            StartCoroutine(UnevenLoadingRoutine());
        }
        else
        {
            // If already opened before, hide loading screen & play music immediately
            loadingScreenPanel.SetActive(false);
            if (AudiomanagerMenu.Instance != null)
            {
                AudiomanagerMenu.Instance.PlayRandomMusicTrack();
            }
        }
    }

    private IEnumerator UnevenLoadingRoutine()
    {
        // 🎯 Pick two distinct random messages
        int firstMsgIndex = -1;
        int secondMsgIndex = -1;

        if (loadingMessages != null && loadingMessages.Length > 0)
        {
            firstMsgIndex = Random.Range(0, loadingMessages.Length);

            if (loadingMessages.Length > 1)
            {
                do
                {
                    secondMsgIndex = Random.Range(0, loadingMessages.Length);
                } while (secondMsgIndex == firstMsgIndex);
            }
            else
            {
                secondMsgIndex = firstMsgIndex;
            }

            if (loadingText != null) loadingText.text = loadingMessages[firstMsgIndex];
        }

        float elapsed = 0f;
        float currentProgress = 0f;
        bool switchedToSecondMessage = false;

        while (elapsed < totalDuration)
        {
            float stepTime = Random.Range(0.15f, 0.45f);
            yield return new WaitForSeconds(stepTime);

            elapsed += stepTime;

            // Switch to 2nd message halfway through
            if (!switchedToSecondMessage && elapsed >= (totalDuration * 0.5f) && secondMsgIndex != -1)
            {
                switchedToSecondMessage = true;
                if (loadingText != null) loadingText.text = loadingMessages[secondMsgIndex];
            }

            float targetProgress = Mathf.Clamp01(elapsed / totalDuration);

            if (targetProgress < 0.9f)
            {
                targetProgress += Random.Range(-0.05f, 0.12f);
            }

            float startVal = currentProgress;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 4f;
                currentProgress = Mathf.Lerp(startVal, targetProgress, t);

                if (progressBar != null)
                {
                    progressBar.value = currentProgress;
                }

                yield return null;
            }
        }

        if (progressBar != null) progressBar.value = 1f;
        yield return new WaitForSeconds(0.2f);

        // 🎯 Hide loading screen AND start background music right here!
        loadingScreenPanel.SetActive(false);

        if (AudiomanagerMenu.Instance != null)
        {
            AudiomanagerMenu.Instance.PlayRandomMusicTrack();
        }
    }
}