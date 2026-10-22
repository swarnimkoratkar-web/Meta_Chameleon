using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkHealthUI : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("Drag your dedicated network warning TextMeshProUGUI element here.")]
    public TextMeshProUGUI networkWarningText;

    [Header("Network Health Settings")]
    public float clientWarningThresholdMs = 350f; // 350ms for Clients
    public float hostWarningThresholdMs = 300f;   // 300ms for Host

    private void Update()
    {
        // Only run when connected to an active session
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            SetWarningText("");
            return;
        }

        ulong currentPing = GetLocalPingMs();
        bool isHost = NetworkManager.Singleton.IsHost;

        // 🟡 1. HOST WARNING (>= 300ms)
        if (isHost && currentPing >= hostWarningThresholdMs)
        {
            SetWarningText($"⚠️ Your network is unstable ({currentPing}ms). Players may experience lag!");
        }
        // 🟡 2. CLIENT WARNING (>= 350ms)
        else if (!isHost && currentPing >= clientWarningThresholdMs)
        {
            SetWarningText($"⚠️ Host Connection Unstable ({currentPing}ms).");
        }
        // 🟢 3. CLEAN CONNECTION (< Threshold) - Clear Text
        else
        {
            SetWarningText("");
        }
    }

    private void SetWarningText(string message)
    {
        if (networkWarningText != null)
        {
            networkWarningText.text = message;
        }
    }

    private ulong GetLocalPingMs()
    {
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null) return 0;

        if (NetworkManager.Singleton.IsHost)
        {
            // Host checks highest RTT among connected clients
            ulong highestRtt = 0;
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.Singleton.LocalClientId) continue;
                ulong clientRtt = (ulong)transport.GetCurrentRtt(clientId);
                if (clientRtt > highestRtt) highestRtt = clientRtt;
            }
            return highestRtt;
        }

        // Client gets RTT to Host
        return (ulong)transport.GetCurrentRtt(NetworkManager.ServerClientId);
    }
}