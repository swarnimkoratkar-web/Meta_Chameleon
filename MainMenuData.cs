using System.Collections.Generic;

public static class MainMenuData
{
    private static Dictionary<ulong, string> cachedNames = new Dictionary<ulong, string>();
    public static string LastDisconnectReason = "";
    public static float SelectedHidingTime = 100f;
    public static float SelectedHuntingTime = 240f;
    public static bool IsFriendsMode = false;
    public static string FinalHidersScoreText = "";
    public static string FinalHuntersScoreText = "";
    public static int LocalPlayerScore = 0;
    public static string MatchWinnerMessage = "";
    public static bool HasEndGameData = false;
    public static bool DidLocalPlayerWin = false;
    public static string SelectedMapScene = "GameplayScene";
    public static List<LobbyManager.PlayerLobbyData> CachedLobbyBots = new List<LobbyManager.PlayerLobbyData>();
    public static void SavePlayerName(ulong clientId, string name)
    {
        cachedNames[clientId] = name;
    }

    public static string GetPlayerName(ulong clientId)
    {
        if (cachedNames.TryGetValue(clientId, out string name))
        {
            return name;
        }
        return $"Player {clientId + 1}";
    }

    public static void ClearAllData()
    {
        cachedNames.Clear();

        SelectedHidingTime = 100f;
        SelectedHuntingTime = 240f;
        IsFriendsMode = false;
        SelectedMapScene = "GameplayScene";
        LastDisconnectReason = "";
        FinalHidersScoreText = "";
        FinalHuntersScoreText = "";
        LocalPlayerScore = 0;
        MatchWinnerMessage = "";
        HasEndGameData = false;
        DidLocalPlayerWin = false;
    }
}