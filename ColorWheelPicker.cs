using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColorWheelPicker : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private Image wheelImage;
    private RectTransform rectTransform;

    [Header("UX Custom Cursor")]
    public RectTransform cursorReticle; // 🎯 Drag your 'ColorPickerCursor' here in the Inspector

    void Awake()
    {
        wheelImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData) => PickColor(eventData);
    public void OnDrag(PointerEventData eventData) => PickColor(eventData);

    private void PickColor(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            // 1. Calculate normalized position for reading texture colors
            float normalizedX = (localPoint.x - rectTransform.rect.x) / rectTransform.rect.width;
            float normalizedY = (localPoint.y - rectTransform.rect.y) / rectTransform.rect.height;

            Texture2D tex = wheelImage.sprite.texture;

            int pixelX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * tex.width), 0, tex.width - 1);
            int pixelY = Mathf.Clamp(Mathf.RoundToInt(normalizedY * tex.height), 0, tex.height - 1);

            Color pickedColor = tex.GetPixel(pixelX, pixelY);

            // 2. Only accept touch and move the cursor if it hits valid colored artwork
            if (pickedColor.a > 0.1f)
            {
                // 🎯 Move the UI selector indicator ring directly to your finger position!
                if (cursorReticle != null)
                {
                    cursorReticle.anchoredPosition = localPoint;
                }

                // Sync local character painter settings context
                var localPainter = FindAnyObjectByType<CharacterPainter>();
                if (localPainter != null && localPainter.IsOwner)
                {
                    localPainter.SetBrushColorLocal(pickedColor);
                }
            }
        }
    }
}