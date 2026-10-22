using UnityEngine;

public class AudiomanagerMenu : MonoBehaviour
{
    public static AudiomanagerMenu Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Menu Music Playlist")]
    [SerializeField] private AudioClip[] backgroundMusicClips; // 🎯 Add menu music tracks here

    [Header("UI Audio Clips")]
    [SerializeField] private AudioClip buttonClickClip;

    private int lastPlayedIndex = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.loop = false;
        musicSource.playOnAwake = false;
    }

    private void Start()
    {
        float savedMusicVol = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
        SetMusicVolume(savedMusicVol); 
    }

    private void Update()
    {
        // Auto-play next track when current finishes
        if (musicSource != null && !musicSource.isPlaying && backgroundMusicClips != null && backgroundMusicClips.Length > 0)
        {
            PlayRandomMusicTrack();
        }
    }

    #region Music Control & Slider
    // 🎯 DIRECT SLIDER CONTROL (Min = 0.0, Max = 1.0 on UI Slider)
    public void SetMusicVolume(float sliderValue)
    {
        sliderValue = Mathf.Clamp01(sliderValue);

        if (musicSource != null)
        {
            musicSource.volume = sliderValue;
        }

        PlayerPrefs.SetFloat("MusicVolume", sliderValue);
    }

    public void PlayRandomMusicTrack()
    {
        if (backgroundMusicClips == null || backgroundMusicClips.Length == 0) return;

        int randomIndex = Random.Range(0, backgroundMusicClips.Length);
        if (backgroundMusicClips.Length > 1 && randomIndex == lastPlayedIndex)
        {
            randomIndex = (randomIndex + 1) % backgroundMusicClips.Length;
        }

        lastPlayedIndex = randomIndex;
        AudioClip nextTrack = backgroundMusicClips[randomIndex];

        if (nextTrack != null)
        {
            musicSource.clip = nextTrack;
            musicSource.Play();
        }
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }
    #endregion

    #region SFX & UI
    public void PlayButtonClick()
    {
        if (sfxSource != null && buttonClickClip != null)
        {
            sfxSource.PlayOneShot(buttonClickClip);
        }
    }
    #endregion
}