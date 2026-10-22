using UnityEngine;
using UnityEngine.EventSystems;

public class FreeRoamCameraController : MonoBehaviour
{
    [Header("UI & Input Hooks")]
    public MobileJoystick freeRoamJoystick;
    public float flySpeed = 5f;
    public float lookSensitivity = 0.2f;

    [Header("Trigger Barrier Settings")]
    public float cameraThickness = 0.2f;
    [SerializeField] private string boundaryTag = "CameraBounds";

    private Vector2 lastTouchPos;
    private float currentYaw = 0f;
    private float currentPitch = 0f;

    void OnEnable()
    {
        currentYaw = transform.eulerAngles.y;
        currentPitch = transform.eulerAngles.x;
        if (currentPitch > 180f) currentPitch -= 360f;
    }

    void Update()
    {
        if (freeRoamJoystick == null) return;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 moveDir = (forward * freeRoamJoystick.InputDirection.y) + (right * freeRoamJoystick.InputDirection.x);

        if (moveDir.sqrMagnitude > 0.001f)
        {
            moveDir.Normalize();
            float moveDistance = flySpeed * Time.deltaTime;
            if (Physics.SphereCast(transform.position, cameraThickness, moveDir, out RaycastHit hit, moveDistance, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.CompareTag(boundaryTag))
                {
                    float safeDistance = Mathf.Max(0, hit.distance - 0.01f);
                    transform.position += moveDir * safeDistance;
                    return;
                }
            }
            transform.position += moveDir * moveDistance;
        }
    }

    public void CustomPointerDown(BaseEventData eventData)
    {
        PointerEventData pointerData = eventData as PointerEventData;
        if (pointerData != null) lastTouchPos = pointerData.position;
    }

    public void CustomDrag(BaseEventData eventData)
    {
        PointerEventData pointerData = eventData as PointerEventData;
        if (pointerData == null) return;

        Vector2 delta = pointerData.position - lastTouchPos;
        lastTouchPos = pointerData.position;

        currentYaw += delta.x * lookSensitivity;
        currentPitch -= delta.y * lookSensitivity;
        currentPitch = Mathf.Clamp(currentPitch, -85f, 85f);

        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }
}