using UnityEngine;
using TMPro;
using UnityEngine.UI; 
public class PlayerProfileManager : MonoBehaviour
{
    public static PlayerProfileManager Instance { get; private set; }

    [Header("Profile Text Info")]
    public TMP_InputField playerNameInput;
    private const string PlayerNameKey = "PlayerName";

    [Header("Profile Image System")]
    public Image mainProfileImageDisplay;   
    public Sprite[] profileSprites = new Sprite[6]; 
    private const string ProfilePictureKey = "ProfilePictureIndex";

    public string PlayerName
    {
        get
        {
            string name = playerNameInput.text.Trim();
            if (string.IsNullOrEmpty(name)) return "Player";
            return name;
        }
    }
    public int SelectedProfileIndex
    {
        get
        {
            return PlayerPrefs.GetInt(ProfilePictureKey, 0);
        }
    }
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (PlayerPrefs.HasKey(PlayerNameKey))
            playerNameInput.text = PlayerPrefs.GetString(PlayerNameKey);
        else
            playerNameInput.text = "Player";

        playerNameInput.onEndEdit.AddListener(SavePlayerName);

        int savedIndex = PlayerPrefs.GetInt(ProfilePictureKey, 0); 
        SetProfileImageVisual(savedIndex);
    }

    public void SavePlayerName(string newName)
    {
        newName = newName.Trim();
        if (string.IsNullOrEmpty(newName)) newName = "Player";

        PlayerPrefs.SetString(PlayerNameKey, newName);
        PlayerPrefs.Save();
        playerNameInput.text = newName;
    }

    public void SelectProfilePicture(int index)
    {
        if (index < 0 || index >= profileSprites.Length) return;

        PlayerPrefs.SetInt(ProfilePictureKey, index);
        PlayerPrefs.Save();

        SetProfileImageVisual(index);
    }

    private void SetProfileImageVisual(int index)
    {
        if (mainProfileImageDisplay != null && profileSprites[index] != null)
        {
            mainProfileImageDisplay.sprite = profileSprites[index];
        }
    }
}