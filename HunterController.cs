using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class HunterController : NetworkBehaviour, IDragHandler, IPointerDownHandler
{
    [Header("Movement")]
    public MobileJoystick joystick;
    public float moveSpeed = 5f;
    private Rigidbody rb;

    [Header("PUBG Aiming")]
    public Transform hunterCameraHolder;
    public float lookSensitivity = 0.2f;

    [Header("Climb Spam Guard")]
    private float lastClimbToggleTime = 0f;
    private const float CLIMB_TOGGLE_COOLDOWN = 0.35f;

    [Header("Jump Settings")]
    public float jumpForce = 5f;
    public AudioClip jumpClip;
    private SurfaceWalker surfaceWalker;
    private Vector2 lastTouchPos;
    private float currentYaw = 0f;
    private float currentPitch = 0f;
    public NetworkVariable<float> NetworkPitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
            if (hunterCameraHolder != null) hunterCameraHolder.gameObject.SetActive(true);
            surfaceWalker = GetComponent<SurfaceWalker>();
            Renderer[] visualRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer ren in visualRenderers)
            {
                if (ren.transform != hunterCameraHolder &&
                    !ren.gameObject.CompareTag("Gun") &&
                    ren.gameObject.name != "PaintMuzzleFX")
                {
                    ren.enabled = false;
                }
            }

            currentYaw = transform.localEulerAngles.y;
            if (hunterCameraHolder != null)
            {
                currentPitch = hunterCameraHolder.localEulerAngles.x;
                if (currentPitch > 180) currentPitch -= 360;
            }
        }
        else
        {
            if (hunterCameraHolder != null)
            {
                hunterCameraHolder.gameObject.SetActive(true);

                Transform childCamera = hunterCameraHolder.Find("Main Camera (1)");
                if (childCamera != null)
                {
                    childCamera.gameObject.SetActive(false);
                }
            }
            return;
        }

        StartCoroutine(WaitForHunterUISetup());
    }

    private System.Collections.IEnumerator WaitForHunterUISetup()
    {
        while (UIAssignmentManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        var combat = GetComponentInChildren<HunterCombat>();
        var controller = GetComponentInChildren<HunterController>();

        if (controller != null && combat != null)
        {
            UIAssignmentManager.Instance.SetupHunterUI(combat, controller);
        }
        else
        {
            Debug.LogError($"Component lookup failure! Controller found: {controller != null}, Combat found: {combat != null}");
        }
    }

    void Update()
    {
        if (!IsOwner)
        {
            if (hunterCameraHolder != null)
            {
                Quaternion targetRot = Quaternion.Euler(NetworkPitch.Value, 0f, 0f);
                hunterCameraHolder.localRotation = Quaternion.Lerp(hunterCameraHolder.localRotation, targetRot, Time.deltaTime * 15f);
            }
            return;
        }

        if (joystick == null) return;
        if (surfaceWalker != null && surfaceWalker.CurrentState != SurfaceWalker.MovementState.Ground)
        {
            Vector2 input = joystick.InputDirection;
            input.x *= -1f;

            surfaceWalker.Move(input);
            return;
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner || joystick == null || surfaceWalker == null) return;

        if (surfaceWalker.CurrentState == SurfaceWalker.MovementState.Ground)
        {
            if (rb != null && rb.isKinematic) rb.isKinematic = false;

            Vector2 input = joystick.InputDirection;

            Vector3 forwardDir = Vector3.ProjectOnPlane(hunterCameraHolder.forward, Vector3.up).normalized;
            Vector3 rightDir = Vector3.ProjectOnPlane(hunterCameraHolder.right, Vector3.up).normalized;

            if (forwardDir.sqrMagnitude < 0.001f) forwardDir = Vector3.forward;
            if (rightDir.sqrMagnitude < 0.001f) rightDir = Vector3.right;

            Vector3 groundMoveDirection = (rightDir * input.x) + (forwardDir * input.y);

            if (groundMoveDirection.magnitude > 0.01f)
            {
                groundMoveDirection.Normalize();

                Vector3 vel = groundMoveDirection * moveSpeed;
                vel.y = rb.linearVelocity.y;
                rb.linearVelocity = vel;
            }
            else if (!rb.isKinematic)
            {
                Vector3 currentVel = rb.linearVelocity;
                currentVel.x = 0f;
                currentVel.z = 0f;
                rb.linearVelocity = currentVel;
            }
        }
        else
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsOwner) return;
        lastTouchPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsOwner || hunterCameraHolder == null) return;

        Vector2 delta = eventData.position - lastTouchPos;
        lastTouchPos = eventData.position;

        currentYaw += delta.x * lookSensitivity;
        currentPitch -= delta.y * lookSensitivity;
        currentPitch = Mathf.Clamp(currentPitch, -60f, 60f);

        transform.localRotation = Quaternion.Euler(0f, currentYaw, 0f);

        hunterCameraHolder.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        NetworkPitch.Value = currentPitch;
    }

    public void CustomPointerDown(BaseEventData eventData)
    {
        PointerEventData pointerData = eventData as PointerEventData;
        if (pointerData != null) OnPointerDown(pointerData);
    }

    public void CustomDrag(BaseEventData eventData)
    {
        PointerEventData pointerData = eventData as PointerEventData;
        if (pointerData != null) OnDrag(pointerData);
    }

    public void ToggleWallStickButtonAction()
    {
        if (!IsOwner || surfaceWalker == null) return;

        if (Time.time - lastClimbToggleTime < CLIMB_TOGGLE_COOLDOWN) return;

        lastClimbToggleTime = Time.time;
        surfaceWalker.ToggleClimb();
    }

    public void PressJumpButtonLocal()
    {
        if (!IsOwner || rb == null || rb.isKinematic) return;

        bool isCurrentlyStuck = surfaceWalker != null && surfaceWalker.CurrentState != SurfaceWalker.MovementState.Ground;
        if (isCurrentlyStuck) return;

        if (Physics.Raycast(transform.position + (Vector3.up * 0.1f), Vector3.down, 0.3f))
        {
            Vector3 currentVel = rb.linearVelocity;
            currentVel.y = 0f;
            rb.linearVelocity = currentVel;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            AudioSource audio = GetComponent<AudioSource>();
            if (audio != null && jumpClip != null)
            {
                audio.PlayOneShot(jumpClip);
            }
        }
    }
}