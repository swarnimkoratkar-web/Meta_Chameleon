using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;
public class HiderMovement : NetworkBehaviour, IDragHandler, IPointerDownHandler
{
    [Header("Movement")]
    public MobileJoystick moveJoystick;
    public float moveSpeed = 4f;
    public float jumpForce = 5f;
    public AudioSource jumpAudioSource;
    public AudioClip jumpClip;

    [Header("PUBG Looking")]
    public Transform hiderCameraHolder;
    public float lookSensitivity = 0.2f;
    [Header("360 Orbit Settings")]
    public Transform cameraOrbitAnchor;         
    public Transform cameraOrbitAnchorPaint;   
    public Transform cameraOrbitAnchorPaintStick;
    public float orbitSensitivity = 0.2f;
    public float minPitch = -20f;
    public float normalMinPitch = -25f;
    public float maxPitch = 80f;
    public float basePaintDistance = 5f;
    public float currentDistance = 5f;
    public float panSensitivity = 0.05f;         
    private Vector3 cameraPanOffset = Vector3.zero; 
    public bool isCameraLocked = false;
    [Header("Camera Collision Settings")]
    public LayerMask wallCollisionLayers;     
    public float cameraRadius = 0.8f;      
    public float minimumCollisionDistance = 0.8f; 
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private SurfaceWalker surfaceWalker;
    private Vector2 lastTouchPos;
    private float currentYaw = 0f;
    private float currentPitch = 20f;
    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        surfaceWalker = GetComponent<SurfaceWalker>();

        if (IsOwner)
        {
            lookSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 0.2f);
            float savedSens = PlayerPrefs.GetFloat("MouseSensitivity", 0.2f);
            lookSensitivity = savedSens;
            orbitSensitivity = savedSens;
            if (hiderCameraHolder != null) hiderCameraHolder.gameObject.SetActive(true);
            currentYaw = transform.localEulerAngles.y;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }
        else
        {
            if (hiderCameraHolder != null) hiderCameraHolder.gameObject.SetActive(false);
            return;
        }
        StartCoroutine(WaitForUIManagerAndSetup());
    }
    private System.Collections.IEnumerator WaitForUIManagerAndSetup()
    {
        while (UIAssignmentManager.Instance == null) yield return new WaitForSeconds(0.1f);
        UIAssignmentManager.Instance.SetupHiderUI(this, GetComponent<HiderUIToggle>(), GetComponentInChildren<CharacterPainter>());
        if (surfaceWalker != null)
        {
            surfaceWalker.OnStickModeStateChanged += (isStuck) => {
                var ui = UIAssignmentManager.Instance;
                if (ui != null && ui.hiderStickRotationSlider != null)
                {
                    ui.hiderStickRotationSlider.gameObject.SetActive(isStuck);
                    if (!isStuck) ui.hiderStickRotationSlider.value = 0f;
                }
            };
        }
    }
    void Update()
    {
        if (!IsOwner || moveJoystick == null) return;
        if (surfaceWalker != null)
        {
            Vector2 input = moveJoystick.InputDirection;
            if (surfaceWalker.CurrentState != SurfaceWalker.MovementState.Ground)
            {
                input.x *= -1f;
            }

            surfaceWalker.Move(input);
        }
    }
    private void FixedUpdate()
    {
        if (!IsOwner || moveJoystick == null || surfaceWalker == null) return;

        if (surfaceWalker.CurrentState == SurfaceWalker.MovementState.Ground)
        {

            Vector2 input = moveJoystick.InputDirection;

            Vector3 forwardDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            Vector3 rightDir = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            if (forwardDir.sqrMagnitude < 0.001f) forwardDir = Vector3.forward;

            if (rightDir.sqrMagnitude < 0.001f) rightDir = Vector3.right;

            Vector3 groundMoveDirection = (rightDir * input.x) + (forwardDir * input.y);

            if (groundMoveDirection.magnitude > 0.01f)
            {
                groundMoveDirection.Normalize();

                Vector3 targetVelocity = groundMoveDirection * moveSpeed;

                targetVelocity.y = rb.linearVelocity.y;

                Vector3 vel = groundMoveDirection * moveSpeed;
                vel.y = rb.linearVelocity.y;
                rb.linearVelocity = vel;
            }
            else
            {
                if (!rb.isKinematic)
                {
                    Vector3 currentVel = rb.linearVelocity;
                    currentVel.x = 0f; currentVel.z = 0f;
                    rb.linearVelocity = currentVel;
                }
            }
        }
    }
    private void LateUpdate()
    {
        if (!IsOwner) return;
        UpdateCameraOrbitPosition(0f, 0f);
    }
    public void ToggleWallStickButtonAction()
    {
        if (!IsOwner) return;
        var uiToggle = GetComponent<HiderUIToggle>();
        if (uiToggle != null && uiToggle.IsPaintingMode) return;
        if (surfaceWalker != null)
        {
            surfaceWalker.ToggleClimb();
        }
    }
    public void CustomPointerDown(BaseEventData eventData)
    {
        if (!IsOwner) return;
        PointerEventData pointerData = eventData as PointerEventData;
        if (pointerData != null) lastTouchPos = pointerData.position;
    }
    public void CustomDrag(BaseEventData eventData)
    {
        if (!IsOwner || hiderCameraHolder == null) return;
        PointerEventData pointerData = eventData as PointerEventData;
        if (pointerData == null) return;
        Vector2 delta = pointerData.position - lastTouchPos;
        lastTouchPos = pointerData.position;
        if (surfaceWalker != null && surfaceWalker.CurrentState != SurfaceWalker.MovementState.Ground)
        {
            currentYaw += delta.x * lookSensitivity;
            currentPitch -= delta.y * lookSensitivity;
            currentPitch = Mathf.Clamp(currentPitch, -35f, 75f);
            hiderCameraHolder.localRotation = Quaternion.Euler(currentPitch, currentYaw - transform.localEulerAngles.y, 0f);
            UpdateCameraOrbitPosition(0f, 0f);
        }
        else
        {
            float deltaX = delta.x * (lookSensitivity / Mathf.Max(0.01f, orbitSensitivity));
            float deltaY = delta.y * (lookSensitivity / Mathf.Max(0.01f, orbitSensitivity));
            UpdateCameraOrbitPosition(deltaX, deltaY);
            transform.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
    }
    public void CustomOrbitPointerDown(BaseEventData eventData)
    {
        if (!IsOwner || hiderCameraHolder == null) return;
        PointerEventData pointerData = eventData as PointerEventData;
        if (pointerData == null) return;
        lastTouchPos = pointerData.position;
        Camera activeCam = Camera.main;
        if (activeCam != null)
        {
            Ray ray = activeCam.ScreenPointToRay(pointerData.position);
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
            foreach (var hit in hits)
            {
                if (hit.collider is MeshCollider && hit.collider.GetComponent<CharacterPainter>() != null)
                {
                    isCameraLocked = true;
                    return;
                }
            }
        }
        isCameraLocked = false;
    }
    public void CustomOrbitDrag(BaseEventData eventData)
    {
        if (!IsOwner || isCameraLocked) return;
 
        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);
            Vector2 averageDelta = (touchZero.deltaPosition + touchOne.deltaPosition) / 2f;
            Vector3 panDirection = hiderCameraHolder.right * (-averageDelta.x * panSensitivity) +
                                   hiderCameraHolder.up * (-averageDelta.y * panSensitivity);
            cameraPanOffset += panDirection;
            UpdateCameraOrbitPosition(0, 0);
            return;
        }

        if (Input.touchCount <= 1)
        {
            PointerEventData pointerData = eventData as PointerEventData;

            if (pointerData == null) return;
            Vector2 delta = pointerData.position - lastTouchPos;

            lastTouchPos = pointerData.position;

            UpdateCameraOrbitPosition(delta.x, delta.y);
        }
    }
    private void UpdateCameraOrbitPosition(float deltaX, float deltaY)
    {
        if (hiderCameraHolder == null) return;
        currentYaw += deltaX * orbitSensitivity;
        currentPitch -= deltaY * orbitSensitivity;
        var uiToggle = GetComponent<HiderUIToggle>();
       bool isPaintingModeActive = uiToggle != null && uiToggle.IsPaintingMode;
        bool isCurrentlyStuck = surfaceWalker != null && surfaceWalker.CurrentState != SurfaceWalker.MovementState.Ground;

        if (!isPaintingModeActive && !isCurrentlyStuck)
        {
            currentPitch = Mathf.Clamp(currentPitch, normalMinPitch, 60f);
        }
        else
        {
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }

        Transform activeAnchor = cameraOrbitAnchor; 
        if (isPaintingModeActive)
        {
            if (isCurrentlyStuck)
            {
                if (cameraOrbitAnchorPaintStick != null) activeAnchor = cameraOrbitAnchorPaintStick;
            }
            else
            {
                if (cameraOrbitAnchorPaint != null) activeAnchor = cameraOrbitAnchorPaint;
            }
        }

        Vector3 pivotAnchorPoint = (activeAnchor != null ? activeAnchor.position : transform.position + (Vector3.up * 1.4f)) + cameraPanOffset;
        if (!isPaintingModeActive && !isCurrentlyStuck)
        {
         pivotAnchorPoint += transform.up * -0.2f;
        }

        if (isCurrentlyStuck)
        {
            pivotAnchorPoint += transform.up * 1.5f;
        }

        if (UIAssignmentManager.Instance != null && UIAssignmentManager.Instance.hiderZoomSlider != null)
        {
            currentDistance = UIAssignmentManager.Instance.hiderZoomSlider.value;
        }
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);

        Vector3 targetLocalOffset = new Vector3(0.0f, 0.0f, -currentDistance);

        Vector3 desiredCameraPosition = pivotAnchorPoint + (rotation * targetLocalOffset);

        Vector3 rayDirection = desiredCameraPosition - pivotAnchorPoint;
        float rayLength = rayDirection.magnitude;
        rayDirection.Normalize();

        float finalAdjustedDistance = currentDistance;

        if (Physics.SphereCast(pivotAnchorPoint, cameraRadius, rayDirection, out RaycastHit hit, rayLength, wallCollisionLayers))
        {
            finalAdjustedDistance = Mathf.Clamp(hit.distance - 0.1f, minimumCollisionDistance, currentDistance);
        }
        Vector3 safeLocalOffset = new Vector3(0.0f, 0.0f, -finalAdjustedDistance);
        Vector3 finalTargetPosition = pivotAnchorPoint + (rotation * safeLocalOffset);
        hiderCameraHolder.position = finalTargetPosition;
        hiderCameraHolder.LookAt(pivotAnchorPoint);
    }
    public void ForceCameraZoomRefresh()
    {
        if (!IsOwner) return;
        UpdateCameraOrbitPosition(0f, 0f);
    }

    public void PressWhistleButtonLocal()
    {
        if (!IsOwner) return;
        TriggerWhistleServerRpc();
    }
    [ServerRpc] private void TriggerWhistleServerRpc() => TriggerLocalWhistleClientRpc();
    [ClientRpc]
    private void TriggerLocalWhistleClientRpc()
    {
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null && audio.clip != null) audio.PlayOneShot(audio.clip);
    }
    public void OnPointerDown(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void PressJumpButtonLocal()
    {
        if (!IsOwner) return;
        var uiToggle = GetComponent<HiderUIToggle>();
        bool isPaintingModeActive = uiToggle != null && uiToggle.IsPaintingMode;
        bool isCurrentlyStuck = surfaceWalker != null && surfaceWalker.CurrentState != SurfaceWalker.MovementState.Ground;

        if (isPaintingModeActive || isCurrentlyStuck || rb == null || rb.isKinematic)
            return;
        Vector3 rayOrigin = transform.position + (Vector3.up * 0.1f);
        float rayDistance = 0.3f;
        if (Physics.Raycast(rayOrigin, Vector3.down, rayDistance))
        {
            Vector3 currentVel = rb.linearVelocity;
            currentVel.y = 0f;
            rb.linearVelocity = currentVel;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            if (jumpAudioSource != null && jumpClip != null)
            {
                jumpAudioSource.PlayOneShot(jumpClip);
            }
        }
        else
        {
        }
    }
}