using System.Threading.Tasks;
using UnityEngine;

public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance { get; private set; }
    public string ActiveVoiceChannelToken { get; set; }

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

    public void JoinRoomVoiceChannel(string roomJoinCode) { }

    public Task LeaveCurrentVoiceChannel() => Task.CompletedTask;

    public void ToggleMicState() { }
    public void MuteMic() { }
    public void UnmuteMic() { }
    public void SetSpeakerVolume(float volume) { }
    public void ToggleSpeakerState() { }
}