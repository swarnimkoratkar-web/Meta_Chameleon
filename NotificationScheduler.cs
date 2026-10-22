using System;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
using UnityEngine.Android;
#endif
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

public class NotificationScheduler : MonoBehaviour
{
    private const string CHANNEL_ID = "daily_game_alerts";
    private const string CHANNEL_NAME = "Daily Match Alerts";
    private const string CHANNEL_DESC = "Daily game match reminders";

    // 4:00 PM IST Notification Pool
    private readonly (string title, string body)[] afternoonAlerts = new[]
    {
        ("Meta Chameleon", "Camouflage battles are live! Jump in and blend in! 🦎"),
        ("Spot Any Camouflage?", "Hunters are patrolling the arena. Think you can hide in plain sight?"),
        ("Afternoon Match Hour!", "Can you outsmart the hunters? Grab your spray paint and find a spot! 🎨"),
        ("Hiders are Winning!", "Lobbies are filling up! Join now and show off your best camouflage."),
        ("Quick Match Break?", "Perfect time for a quick hide & seek round. Don't let them find you!")
    };

    // 8:00 PM IST Notification Pool
    private readonly (string title, string body)[] eveningAlerts = new[]
    {
        ("Prime Time Camouflage! 🌙", "Night lobbies are heating up! Jump in and claim your victory."),
        ("Hunters on the Prowl!", "Can you survive the round, or will you get tagged? Join the room now! 🎯"),
        ("Ready for Tonight's Match?", "Grab your squad or test your solo hiding skills right now!"),
        ("The Hunt Begins! 🔍", "The best camouflage master wins tonight. Jump in and disguise yourself!"),
        ("Evening Showdown!", "Lobbies are live and hunters are searching every corner. Can you blend in?")
    };

    private void Start()
    {
        InitializeNotifications();
        ScheduleDailyFixedGlobalNotifications();
    }

    private void InitializeNotifications()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
        {
            Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
        }

        var channel = new AndroidNotificationChannel()
        {
            Id = CHANNEL_ID,
            Name = CHANNEL_NAME,
            Importance = Importance.High,
            Description = CHANNEL_DESC
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
#elif UNITY_IOS
        StartCoroutine(RequestIOSAuthorization());
#endif
    }

#if UNITY_IOS
    private System.Collections.IEnumerator RequestIOSAuthorization()
    {
        var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true);
        while (!req.IsFinished)
        {
            yield return null;
        }
    }
#endif

    public void ScheduleDailyFixedGlobalNotifications()
    {
        // Pick random messages from the pools
        var afternoon = afternoonAlerts[UnityEngine.Random.Range(0, afternoonAlerts.Length)];
        var evening = eveningAlerts[UnityEngine.Random.Range(0, eveningAlerts.Length)];

#if UNITY_ANDROID
        AndroidNotificationCenter.CancelAllScheduledNotifications();

        // Target UTC times (4:00 PM IST = 10:30 UTC, 8:00 PM IST = 14:30 UTC)
        ScheduleNotificationAndroid(afternoon.title, afternoon.body, 10, 30);
        ScheduleNotificationAndroid(evening.title, evening.body, 14, 30);
#elif UNITY_IOS
        iOSNotificationCenter.RemoveAllScheduledNotifications();

        ScheduleNotificationIOS(afternoon.title, afternoon.body, 10, 30);
        ScheduleNotificationIOS(evening.title, evening.body, 14, 30);
#endif
    }

#if UNITY_ANDROID
    private void ScheduleNotificationAndroid(string title, string text, int utcHour, int utcMinute)
    {
        DateTime nowUtc = DateTime.UtcNow;
        DateTime targetUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, utcHour, utcMinute, 0, DateTimeKind.Utc);

        if (nowUtc >= targetUtc)
        {
            targetUtc = targetUtc.AddDays(1);
        }

        DateTime localFireTime = targetUtc.ToLocalTime();

        var notification = new AndroidNotification
        {
            Title = title,
            Text = text,
            FireTime = localFireTime,
            RepeatInterval = new TimeSpan(1, 0, 0, 0) // Repeats daily
        };

        AndroidNotificationCenter.SendNotification(notification, CHANNEL_ID);
    }
#endif

#if UNITY_IOS
    private void ScheduleNotificationIOS(string title, string body, int utcHour, int utcMinute)
    {
        DateTime nowUtc = DateTime.UtcNow;
        DateTime targetUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, utcHour, utcMinute, 0, DateTimeKind.Utc);

        if (nowUtc >= targetUtc)
        {
            targetUtc = targetUtc.AddDays(1);
        }

        DateTime localFireTime = targetUtc.ToLocalTime();

        var timeTrigger = new iOSNotificationCalendarTrigger()
        {
            Hour = localFireTime.Hour,
            Minute = localFireTime.Minute,
            Second = 0,
            Repeats = true
        };

        var notification = new iOSNotification()
        {
            Identifier = $"daily_{utcHour}_{utcMinute}",
            Title = title,
            Body = body,
            ShowInForeground = true,
            ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound,
            Trigger = timeTrigger
        };

        iOSNotificationCenter.ScheduleNotification(notification);
    }
#endif
}