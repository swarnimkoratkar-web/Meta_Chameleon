using UnityEngine;
using UnityEngine.UI;

public class CustomizableControl : MonoBehaviour
{
    public string controlID; // e.g., "HunterFire"

    [Header("Sync Target")]
    public RectTransform realGameplayButton; // 🎯 Drag the REAL gameplay button here!

    [HideInInspector] public RectTransform rectTransform;
    [HideInInspector] public CanvasGroup canvasGroup;

    // 🎯 Changed from Awake to OnValidate so it assigns itself inside the Editor instantly!
    private void OnValidate()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    // Backup execution safety check when waking up
    void Awake()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
    }
}