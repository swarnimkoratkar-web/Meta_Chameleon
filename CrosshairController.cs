using UnityEngine;

public class CrosshairController : MonoBehaviour
{
    public MobileJoystick joystick;
    public float crosshairSpeed = 300f;

    private RectTransform rectTransform;
    private Canvas parentCanvas;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
       
        parentCanvas = GetComponentInParent<Canvas>();
    }

    void Update()
    {
        if (joystick != null && joystick.InputDirection != Vector2.zero)
        {
            Vector3 move = new Vector3(joystick.InputDirection.x, joystick.InputDirection.y, 0);
            rectTransform.anchoredPosition += (Vector2)move * crosshairSpeed * Time.deltaTime;

            ClampCrosshairToScreen();
        }
    }

    private void ClampCrosshairToScreen()
    {
        if (parentCanvas == null) return;

        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();

        float minX = -canvasRect.rect.width / 2f;
        float maxX = canvasRect.rect.width / 2f;
        float minY = -canvasRect.rect.height / 2f;
        float maxY = canvasRect.rect.height / 2f;

        Vector2 clampedPos = rectTransform.anchoredPosition;
        clampedPos.x = Mathf.Clamp(clampedPos.x, minX, maxX);
        clampedPos.y = Mathf.Clamp(clampedPos.y, minY, maxY);

        rectTransform.anchoredPosition = clampedPos;
    }
}