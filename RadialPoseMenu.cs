using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class RadialPoseMenu : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public GameObject menuPanel; // ⭕ Drag your pop-up Wheel UI panel graphic here!
    public RectTransform selectionPointer; // 🎯 The pointer/dot in the center
    public Image[] sliceHighlights; // Array of your 6 section highlight images

    private Vector2 pointerInput;
    private int currentSelectedSlice = 0;
    private HiderAnimationController animController;

    void Start()
    {
        if (menuPanel != null) menuPanel.SetActive(false); // Hidden by default
    }

    private void TryFindLocalPlayerController()
    {
        if (animController == null && NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj != null)
            {
                animController = localPlayerObj.GetComponent<HiderAnimationController>();
            }
        }
    }

    // 👆 WHEN YOU PRESS DOWN ON THE SCREEN ICON:
    public void OnPointerDown(PointerEventData eventData)
    {
        TryFindLocalPlayerController();
        if (menuPanel != null) menuPanel.SetActive(true); // Pops up the radial menu!
        pointerInput = Vector2.zero;
        if (selectionPointer != null) selectionPointer.anchoredPosition = Vector2.zero;
    }

    // 👆 WHILE YOU DRAG YOUR FINGER ACROSS THE SCREEN:
    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPos;
        // Tracks coordinates relative to the screen icon's center position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform, eventData.position, eventData.pressEventCamera, out localPos);

        pointerInput = localPos;
        if (selectionPointer != null)
            selectionPointer.anchoredPosition = Vector2.ClampMagnitude(pointerInput, 100f);

        if (pointerInput.magnitude > 20f) // Deadzone check
        {
            // Calculate raw angle (0 to 360, 0 at bottom)
            float angle = Mathf.Atan2(pointerInput.x, -pointerInput.y) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            // Calculate the raw mathematical slice index
            int rawSlice = Mathf.FloorToInt((angle + 30f) % 360f / 60f);

            // 🎯 REMAP THE SWAPPED PAIRS MECHANICALLY:
            switch (rawSlice)
            {
                case 0: currentSelectedSlice = 3; break;
                case 3: currentSelectedSlice = 0; break;
                case 1: currentSelectedSlice = 2; break;
                case 2: currentSelectedSlice = 1; break;
                case 4: currentSelectedSlice = 5; break;
                case 5: currentSelectedSlice = 4; break;
                default: currentSelectedSlice = 0; break;
            }

            UpdateVisualHighlights();
        }
    }

    // 👆 WHEN YOU LIFT YOUR FINGER OFF THE SCREEN:
    public void OnPointerUp(PointerEventData eventData)
    {
        if (animController != null)
        {
            animController.SelectPoseLocal(currentSelectedSlice); // Executes your chosen pose!

            // 🎯 STEP 2 FIX: If the player manually selects Pose 0, kick them out of paint mode!
            if (currentSelectedSlice == 0)
            {
                HiderUIToggle uiToggle = animController.GetComponent<HiderUIToggle>();
                if (uiToggle != null && uiToggle.IsPaintingMode)
                {
                    uiToggle.ToggleModes(); // Safely flips mode back to normal movement
                }
            }
        }

        if (menuPanel != null) menuPanel.SetActive(false); // Instantly hides the menu!
        ClearAllHighlights();
    }

    private void UpdateVisualHighlights()
    {
        ClearAllHighlights(); // Reset all slices to normal size first

        if (sliceHighlights != null && currentSelectedSlice < sliceHighlights.Length)
        {
            Transform targetSlice = sliceHighlights[currentSelectedSlice]?.transform;
            if (targetSlice != null)
            {
                // 🎯 Scale the selected slice slightly larger (1.2x size)
                targetSlice.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            }
        }
    }

    private void ClearAllHighlights()
    {
        foreach (var img in sliceHighlights)
        {
            if (img != null)
            {
                // Ensure the Image component stays completely active
                img.enabled = true;

                // Reset its scale back to normal (1.0x size)
                img.transform.localScale = Vector3.one;
            }
        }
    }
}