using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class HiderUIToggle : NetworkBehaviour
{
    [Header("Camera System")]
    public Transform hiderCameraHolder;
    [Header("Master Group Panels")]
    public GameObject hiderPaintAndMovementGroup;

    private bool wasPhysicsFrozenByFreeRoam = false;
    private bool isFreeRoamMode = false;

    // 🆕 Expose the Free Roam Camera slot so it can still be set in the prefab
    [Header("Free Roam System")]
    public Camera freeRoamCamera;

    private Vector3 savedNormalPos;
    private Quaternion savedNormalRot;
    private Vector3 savedPaintPos;
    private Quaternion savedPaintRot;

    private bool isPaintingMode = false;
    private bool hasSavedPaintState = false;

    private Rigidbody rb;
    private SurfaceWalker surfaceWalker;

    public bool IsPaintingMode => isPaintingMode;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        rb = GetComponent<Rigidbody>();
        surfaceWalker = GetComponent<SurfaceWalker>();

        savedNormalPos = hiderCameraHolder.localPosition;
        savedNormalRot = hiderCameraHolder.localRotation;

        StartCoroutine(WaitForUIAndLinkToggle());
    }

    private System.Collections.IEnumerator WaitForUIAndLinkToggle()
    {
        while (UIAssignmentManager.Instance == null)
            yield return new WaitForSeconds(0.1f);

        var ui = UIAssignmentManager.Instance;

        if (ui.hiderToggleModeButton != null)
        {
            ui.hiderToggleModeButton.onClick.RemoveAllListeners();
            ui.hiderToggleModeButton.onClick.AddListener(ToggleModes);
        }

        ShowMovementMode();
    }

    public void ToggleModes()
    {
        if (!IsOwner) return;

        var ui = UIAssignmentManager.Instance;
        if (ui == null) return;

        if (!isPaintingMode)
        {
            bool isCurrentlyClimbing = surfaceWalker != null && surfaceWalker.CurrentState != SurfaceWalker.MovementState.Ground;
            if (!isCurrentlyClimbing && rb != null)
            {
                // Shoot a short ray down from the player base to check for floor surfaces, tables, or beds
                if (!Physics.Raycast(transform.position + (Vector3.up * 0.1f), Vector3.down, 0.3f))
                {
                    Debug.LogWarning("[Exploit Prevented] Cannot enter Paint Mode while airborne mid-jump!");
                    return; // ⛔ Abort out of the mode toggle entirely
                }
            }
        }
        isPaintingMode = !isPaintingMode;

        if (isPaintingMode)
        {
            savedNormalPos = hiderCameraHolder.localPosition;
            savedNormalRot = hiderCameraHolder.localRotation;

            if (ui.hiderMovementUIGroup != null)
                ui.hiderMovementUIGroup.SetActive(false);

            if (ui.hiderPaintUIGroup != null)
                ui.hiderPaintUIGroup.SetActive(true);

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            // 🎯 FORCE JOYSTICK TO SNAP BACK TO CENTER AND CLEAR INPUTS
            if (ui != null && ui.hiderMoveJoystick != null)
            {
                ui.hiderMoveJoystick.OnPointerUp(new PointerEventData(EventSystem.current));
            }
            var meshCol = GetComponentInChildren<MeshCollider>();
            if (meshCol != null) meshCol.enabled = true;
            var mainCapsule = GetComponent<CapsuleCollider>();
            if (mainCapsule != null) mainCapsule.enabled = true;
            // 🎯 STEP 1 FIX: If we are in default stance (0), automatically switch to Pose 1
            var animController = GetComponent<HiderAnimationController>();
            if (animController != null)
            {
                var animator = GetComponentInChildren<Animator>();
                if (animator != null && animator.GetInteger("PoseID") == 0)
                {
                    animController.SelectPoseLocal(5);
                }
            }
            if (hasSavedPaintState)
            {
                hiderCameraHolder.localPosition = savedPaintPos;
                hiderCameraHolder.localRotation = savedPaintRot;
            }
        }
        else
        {
            savedPaintPos = hiderCameraHolder.localPosition;
            savedPaintRot = hiderCameraHolder.localRotation;
            hasSavedPaintState = true;

            ShowMovementMode();
        }
    }

    private void ShowMovementMode()
    {
        if (!IsOwner) return;

        var ui = UIAssignmentManager.Instance;
        if (ui == null) return;

        if (ui.hiderMovementUIGroup != null)
            ui.hiderMovementUIGroup.SetActive(true);

        if (ui.hiderPaintUIGroup != null)
            ui.hiderPaintUIGroup.SetActive(false);

        isPaintingMode = false;

        if (rb != null)
        {
            rb.isKinematic = false;

            if (surfaceWalker != null &&
                surfaceWalker.CurrentState != SurfaceWalker.MovementState.Ground)
            {
                rb.useGravity = false;
            }
            else
            {
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }
        var meshCol = GetComponentInChildren<MeshCollider>();
        if (meshCol != null) meshCol.enabled = false;
        var mainCapsule = GetComponent<CapsuleCollider>();
        if (mainCapsule != null) mainCapsule.enabled = true;
        hiderCameraHolder.localPosition = savedNormalPos;
        hiderCameraHolder.localRotation = savedNormalRot;
        // 🎯 CLEAR JOYSTICK VALUES AGAIN WHEN WE SWITCH BACK TO WALKING
        if (ui != null && ui.hiderMoveJoystick != null)
        {
            ui.hiderMoveJoystick.OnPointerUp(new PointerEventData(EventSystem.current));
        }
    }
    public void ToggleFreeRoamMode()
    {
        if (!IsOwner) return;

        var ui = UIAssignmentManager.Instance;
        if (ui == null) return;

        isFreeRoamMode = !isFreeRoamMode;

        if (isFreeRoamMode)
        {
            // 🎥 ENTER FREE ROAM MODE
            // 🎯 FIXED: Pulling group assets directly via the Scene's dynamic UIAssignmentManager instance
            if (ui.hiderPaintAndMovementGroup != null) ui.hiderPaintAndMovementGroup.SetActive(false);
            if (ui.freeRoamUIGroup != null) ui.freeRoamUIGroup.SetActive(true);

            hiderCameraHolder.gameObject.SetActive(false);

            if (freeRoamCamera != null)
            {
                Camera gameplayCam = hiderCameraHolder.GetComponentInChildren<Camera>(true);
                if (gameplayCam != null)
                {
                    freeRoamCamera.transform.position = gameplayCam.transform.position;
                    freeRoamCamera.transform.rotation = gameplayCam.transform.rotation;
                }
                freeRoamCamera.gameObject.SetActive(true);
            }

            // 🔍 FOOLPROOF CHECK: Assess if player is already locked in Stick Mode or Paint Mode
            bool isCurrentlyClimbing = surfaceWalker != null &&
                                       (surfaceWalker.CurrentState == SurfaceWalker.MovementState.Wall ||
                                        surfaceWalker.CurrentState == SurfaceWalker.MovementState.Ceiling ||
                                        surfaceWalker.CurrentState == SurfaceWalker.MovementState.Transition);

            bool isAlreadyLockedDown = isPaintingMode || isCurrentlyClimbing;

            if (!isAlreadyLockedDown && rb != null)
            {
                // Player was completely normal on the ground—freeze physics parameters safely
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
                wasPhysicsFrozenByFreeRoam = true;
            }
            else
            {
                // Player is sticking or painting—leave their active physics state exactly as it is
                wasPhysicsFrozenByFreeRoam = false;
            }
        }
        else
        {
            // 🏃 EXIT FREE ROAM MODE
            // 🎯 FIXED: Pulling group assets directly via the Scene's dynamic UIAssignmentManager instance
            if (ui.freeRoamUIGroup != null) ui.freeRoamUIGroup.SetActive(false);
            if (ui.hiderPaintAndMovementGroup != null) ui.hiderPaintAndMovementGroup.SetActive(true);

            if (freeRoamCamera != null) freeRoamCamera.gameObject.SetActive(false);
            hiderCameraHolder.gameObject.SetActive(true);

            if (rb != null)
            {
                // 🔍 RESTORE CHECK: Only wake up physics configurations if we explicitly froze them in Step 1
                if (wasPhysicsFrozenByFreeRoam)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.constraints = RigidbodyConstraints.FreezeRotation;
                }
                // If they were painting or sticking, we don't touch the Rigidbody, preserving their position!
            }
        }
    }
}