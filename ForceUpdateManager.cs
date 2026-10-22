using System.Collections;
using UnityEngine;
using Google.Play.AppUpdate;
using Google.Play.Common;

public class ForceUpdateManager : MonoBehaviour
{
    private AppUpdateManager appUpdateManager;

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        appUpdateManager = new AppUpdateManager();
        StartCoroutine(CheckForAppUpdate());
#endif
    }

    private IEnumerator CheckForAppUpdate()
    {
        // Request update availability from Google Play Store
        PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> appUpdateInfoOperation =
            appUpdateManager.GetAppUpdateInfo();

        yield return appUpdateInfoOperation;

        if (appUpdateInfoOperation.IsSuccessful)
        {
            AppUpdateInfo appUpdateInfoResult = appUpdateInfoOperation.GetResult();

            // If an update is available on Google Play
            if (appUpdateInfoResult.UpdateAvailability == UpdateAvailability.UpdateAvailable)
            {
                // Request an Immediate (Forced) full-screen update overlay
                var appUpdateOptions = AppUpdateOptions.ImmediateAppUpdateOptions();
                StartCoroutine(StartImmediateUpdate(appUpdateInfoResult, appUpdateOptions));
            }
        }
    }

    private IEnumerator StartImmediateUpdate(AppUpdateInfo updateInfo, AppUpdateOptions updateOptions)
    {
        var startUpdateRequest = appUpdateManager.StartUpdate(updateInfo, updateOptions);

        while (!startUpdateRequest.IsDone)
        {
            yield return null;
        }
    }
}