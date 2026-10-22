using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class PingDisplay : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI pingText;

    [Header("Settings")]
    public float updateInterval = 1f;

    private float timer = 0f;

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
        if (pingText == null) return;

        timer += Time.deltaTime;
        if (timer < updateInterval) return;
        timer = 0f;

        ulong ping = GetCurrentPingMs();
        pingText.text = $"{ping}ms";
        if (ping < 100) pingText.color = Color.green;
        else if (ping < 250) pingText.color = Color.yellow;
        else pingText.color = Color.red;
    }

    private ulong GetCurrentPingMs()
    {
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null) return 0; 
        if (NetworkManager.Singleton.IsHost) return 0;
        if (NetworkManager.Singleton.IsServer)
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.Singleton.LocalClientId) continue;
                return (ulong)transport.GetCurrentRtt(clientId);
            }
            return 0;
        }
        return (ulong)transport.GetCurrentRtt(NetworkManager.ServerClientId);
    }
}