using UnityEngine;
using UnityEngine.EventSystems;

public class MobileJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    private RectTransform background;
    private RectTransform handle;
    private float movementRange;

    // This is the variable your paint script will read to move the crosshair
    public Vector2 InputDirection { get; private set; }

    void Start()
    {
        background = GetComponent<RectTransform>();
        handle = transform.GetChild(0).GetComponent<RectTransform>();

        // Calculate how far the handle can slide based on background size
        movementRange = background.sizeDelta.x / 2f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position = Vector2.zero;

        // Calculate touch position relative to the background center
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out position))
        {
            // Clamp position within the joystick background circle
            position.x = position.x / (background.sizeDelta.x / 2f);
            position.y = position.y / (background.sizeDelta.y / 2f);

            InputDirection = (position.magnitude > 1.0f) ? position.normalized : position;

            // Move the visual handle mesh
            handle.anchoredPosition = new Vector2(InputDirection.x * movementRange, InputDirection.y * movementRange);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Reset the joystick back to the exact center when you let go
        InputDirection = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }
}