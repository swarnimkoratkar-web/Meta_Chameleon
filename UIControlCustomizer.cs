using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIControlCustomizer : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    [Header("Sliders")]
    public Slider scaleSlider;
    public Slider transparencySlider;
    public GameObject HunterUI;
    public GameObject HiderUI;
    public GameObject movmentbutton;
    public GameObject Paintbutton;
    public GameObject HiderMovementUI;
    public GameObject HiderPaintUI;

    [Header("Active Selection Context")]
    private CustomizableControl selectedControl;

    void Start()
    {
        // Setup slider change listeners dynamically
        if (scaleSlider != null) scaleSlider.onValueChanged.AddListener(OnScaleChanged);
        if (transparencySlider != null) transparencySlider.onValueChanged.AddListener(OnTransparencyChanged);

        // Start with sliders disabled until a control is clicked
        SetSlidersInteractable(false);
    }

    public void OpenHunterUIPannel()
    {
        HunterUI.SetActive(true);
        HiderUI.SetActive(false);
        movmentbutton.SetActive(false);
        Paintbutton.SetActive(false);
    }
    public void OpenHiderUIPannel()
    {
        HunterUI.SetActive(false);
        HiderUI.SetActive(true);
        movmentbutton.SetActive(true);
        Paintbutton.SetActive(true);
    }
    public void OpenHiderPaintUIPannel()
    {
        HiderMovementUI.SetActive(false);
        HiderPaintUI.SetActive(true);
    }
    public void OpenHiderMovementUIPannel()
    {
        HiderPaintUI.SetActive(false);
        HiderMovementUI.SetActive(true);
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        // 1. Raycast specifically checking if they tapped an editable control
        GameObject clickedObject = eventData.pointerCurrentRaycast.gameObject;
        if (clickedObject == null) return;

        CustomizableControl control = clickedObject.GetComponentInParent<CustomizableControl>();

        if (control != null)
        {
            // 2. Select the control and update slider values to match its current states
            selectedControl = control;
            SetSlidersInteractable(true);

            scaleSlider.value = selectedControl.rectTransform.localScale.x;
            transparencySlider.value = selectedControl.canvasGroup.alpha;

        }
    }
    // 🎯 Call this function from your UI "SAVE" Button's OnClick() event!
    public void SaveLayoutSettings()
    {
        // Find every customizable dummy button inside this customization screen
        CustomizableControl[] allDuplicates = GetComponentsInChildren<CustomizableControl>(true);

        foreach (var dummy in allDuplicates)
        {
            if (dummy.realGameplayButton != null)
            {
                // 1. Sync the Position
                dummy.realGameplayButton.anchoredPosition = dummy.rectTransform.anchoredPosition;

                // 2. Sync the Scale (Size)
                dummy.realGameplayButton.localScale = dummy.rectTransform.localScale;

                // 3. Sync the Transparency (Alpha)
                CanvasGroup realGroup = dummy.realGameplayButton.GetComponent<CanvasGroup>();
                if (realGroup == null) realGroup = dummy.realGameplayButton.gameObject.AddComponent<CanvasGroup>();

                realGroup.alpha = dummy.canvasGroup.alpha;
            }
        }

    }
    // Handles moving the selected button smoothly around the screen
    public void OnDrag(PointerEventData eventData)
    {
        if (selectedControl == null) return;

        // 3. Translate screen drag movements directly to the UI elements anchored position
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            selectedControl.rectTransform.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            selectedControl.rectTransform.anchoredPosition = localPoint;
        }
    }

    private void OnScaleChanged(float value)
    {
        if (selectedControl != null)
        {
            selectedControl.rectTransform.localScale = new Vector3(value, value, 1f);
        }
    }

    private void OnTransparencyChanged(float value)
    {
        if (selectedControl != null)
        {
            selectedControl.canvasGroup.alpha = value;
        }
    }

    private void SetSlidersInteractable(bool state)
    {
        if (scaleSlider != null) scaleSlider.interactable = state;
        if (transparencySlider != null) transparencySlider.interactable = state;
    }
}