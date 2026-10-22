using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Audio Mixer (For SFX)")]
    [SerializeField] private AudioMixer masterMixer;

    [Header("Gameplay Music Playlist")]
    [SerializeField] private AudioClip[] backgroundMusicClips;

    [Header("UI Audio Clips")]
    [SerializeField] private AudioClip buttonClickClip;

    private int lastPlayedIndex = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 🎯 No DontDestroyOnLoad — music stays isolated ONLY to this scene!
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
        // Load saved SFX mixer volume
        if (PlayerPrefs.HasKey("SFXVolume"))
            SetSFXVolume(PlayerPrefs.GetFloat("SFXVolume"));

        // Load saved Music volume directly into AudioSource (default 75%)
        float savedMusicVol = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
        SetMusicVolume(savedMusicVol);

        // Start gameplay playlist
        PlayRandomMusicTrack();
    }

    private void Update()
    {
        // Auto-play next random song when current finishes
        if (musicSource != null && !musicSource.isPlaying && backgroundMusicClips != null && backgroundMusicClips.Length > 0)
        {
            PlayRandomMusicTrack();
        }
    }

    #region Gameplay Music Playlist & Volume Control
    public void SetMusicVolume(float sliderValue)
    {
        sliderValue = Mathf.Clamp01(sliderValue);

        if (musicSource != null)
        {
            musicSource.volume = sliderValue; // Direct 0.0 to 1.0 volume control
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
    public void SetSFXVolume(float sliderValue)
    {
        float dB = Mathf.Log10(Mathf.Max(0.0001f, sliderValue)) * 20f;
        if (masterMixer != null) masterMixer.SetFloat("SFXVolume", dB);
        PlayerPrefs.SetFloat("SFXVolume", sliderValue);
    }

    public void PlayButtonClick()
    {
        if (sfxSource != null && buttonClickClip != null)
        {
            sfxSource.PlayOneShot(buttonClickClip);
        }
    }
    #endregion
}