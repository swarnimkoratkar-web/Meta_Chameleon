using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class SurfaceWalker : NetworkBehaviour
{
    public enum MovementState { Ground, Wall, Ceiling, Transition }

    [Header("State")]
    [SerializeField] private MovementState currentState = MovementState.Ground;
    public MovementState CurrentState => currentState;

    [Header("Movement Profiles")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float rotationSpeed = 10.0f;
    [SerializeField] private float normalSmoothSpeed = 12.0f;

    [Header("Surface Alignment Constants")]
    [SerializeField] private float attachDistance = 2.0f;
    [SerializeField] private float detachDistance = 2.5f;
    [SerializeField] private float surfaceOffset = 0.05f;
    [SerializeField] private float surfaceDetectionDistance = 1.5f;

    [Header("Surface Stick Audio")]
    [SerializeField] private AudioClip wallStickClip;
    [SerializeField] private AudioClip wallUnstickClip;
    [SerializeField] private AudioSource localAudioSource;

    [Header("Layer Matrices")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask climbableLayer;

    private float manualPoseAngle = 0f;
    public System.Action<bool> OnStickModeStateChanged;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Vector2 rawInput = Vector2.zero;
    private Vector3 currentSurfaceNormal = Vector3.up;
    private Vector3 smoothedSurfaceNormal = Vector3.up;
    private Vector3 pendingSurfaceNormal = Vector3.up;

    // 🎯 Multi-Direction Matrix Offsets
    private readonly Vector3[] raycastOffsetsMatrix = new Vector3[]
    {
        Vector3.zero,
        Vector3.forward,
        Vector3.back,
        Vector3.right,
        Vector3.left
    };

    private Vector3 transitionTargetPosition = Vector3.zero;
    private Quaternion transitionTargetRotation = Quaternion.identity;
    private float transitionProgress = 0.0f;
    private float transitionSpeedMultiplier = 5.0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        localAudioSource = GetComponent<AudioSource>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner) SetState(MovementState.Ground);
        else rb.isKinematic = true;
    }

    public void Move(Vector2 input)
    {
        if (!IsOwner) return;
        rawInput = input;
    }

    public void ToggleClimb()
    {
        if (!IsOwner) return;

        if (currentState == MovementState.Ground)
        {
            InterrogateForwardSurfaces();
        }
        else
        {
            // 🔊 Updated to local 2D AudioSource
            if (localAudioSource != null && wallUnstickClip != null)
            {
                localAudioSource.PlayOneShot(wallUnstickClip);
            }
            InitiateDetachmentSequence();
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        switch (currentState)
        {
            case MovementState.Ground:
                smoothedSurfaceNormal = Vector3.Slerp(smoothedSurfaceNormal, Vector3.up, Time.fixedDeltaTime * normalSmoothSpeed);
                break;
            case MovementState.Wall:
            case MovementState.Ceiling:
                ProcessSurfaceAttachedPhysics();
                break;
            case MovementState.Transition:
                ExecuteTransitionInterpolation();
                break;
        }
    }

    private void ProcessSurfaceAttachedPhysics()
    {
        rb.useGravity = false;

        // 🎯 Multi-Direction Ray Tracking Engine
        if (!EvaluateSurfaceAnchorsMatrix(out Vector3 aggregateNormal, out Vector3 aggregatePoint))
        {
            InitiateDetachmentSequence();
            return;
        }

        currentSurfaceNormal = aggregateNormal;
        smoothedSurfaceNormal = Vector3.Slerp(smoothedSurfaceNormal, currentSurfaceNormal, Time.fixedDeltaTime * normalSmoothSpeed);

        // Run the manual slider dial updates using the tracking matrix layout
        // 🎯 Only align body facing direction automatically if this is a Hider
        if (CompareTag("Hider"))
        {
            UpdatePlayerRotationProfile(Vector3.zero);
        }

        // Calculate absolute axes relative to the wall face plane structure
        Vector3 wallRight = Vector3.Cross(Vector3.up, smoothedSurfaceNormal).normalized;
        if (wallRight.sqrMagnitude < 0.001f) wallRight = Vector3.Cross(smoothedSurfaceNormal, Vector3.forward).normalized;
        Vector3 wallUp = Vector3.Cross(smoothedSurfaceNormal, wallRight).normalized;

        Vector3 planarMoveDirection = (wallRight * rawInput.x) + (wallUp * rawInput.y);

        float centerOffset = (capsule.height * 0.5f) + surfaceOffset;
        Vector3 idealSurfaceLockPoint = aggregatePoint + (currentSurfaceNormal * centerOffset);

        if (planarMoveDirection.magnitude > 0.01f)
        {
            planarMoveDirection.Normalize();
            Vector3 movementStepVector = planarMoveDirection * moveSpeed * Time.fixedDeltaTime;
            Vector3 exploratoryOrigin = rb.position + movementStepVector + (currentSurfaceNormal * 0.1f);

            // Forward Corner / Wall Transition Detection
            if (Physics.Raycast(exploratoryOrigin, -currentSurfaceNormal, out RaycastHit forwardCheckHit, surfaceDetectionDistance + 0.5f, climbableLayer | groundLayer))
            {
                if (Vector3.Angle(forwardCheckHit.normal, currentSurfaceNormal) > 15.0f)
                {
                    InitiateTransitionSequence(forwardCheckHit.point, forwardCheckHit.normal);
                    return;
                }

                Vector3 movement = ExecuteSweptCapsuleMovement(planarMoveDirection * moveSpeed);
                Vector3 snapCorrection = (idealSurfaceLockPoint - rb.position);

                rb.MovePosition(rb.position + movement + (snapCorrection * 0.25f));
            }
            else
            {
                // Edge Wrap / Outer Corner Transition Detection
                Vector3 lookDownRayOrigin = rb.position + movementStepVector - (currentSurfaceNormal * (capsule.height * 0.4f));
                Vector3 lookDownRayDirection = -movementStepVector.normalized;

                if (Physics.Raycast(lookDownRayOrigin, lookDownRayDirection, out RaycastHit edgeWrapHit, capsule.radius * 2.5f, climbableLayer | groundLayer))
                {
                    InitiateTransitionSequence(edgeWrapHit.point, edgeWrapHit.normal);
                }
                else
                {
                    rb.linearVelocity = Vector3.zero;
                }
            }
        }
        else
        {
            Vector3 dampCorrection = (idealSurfaceLockPoint - rb.position) * 0.25f;
            rb.MovePosition(rb.position + dampCorrection);
        }
    }

    public Vector3 ExecuteSweptCapsuleMovement(Vector3 targetVelocity)
    {
        Vector3 move = targetVelocity * Time.fixedDeltaTime;
        if (move.sqrMagnitude < 0.000001f) return Vector3.zero;

        float radius = capsule.radius * 0.95f;
        float halfHeight = (capsule.height * 0.5f) - radius;

        Vector3 center = rb.position + smoothedSurfaceNormal * (capsule.height * 0.5f);
        Vector3 top = center + smoothedSurfaceNormal * halfHeight;
        Vector3 bottom = center - smoothedSurfaceNormal * halfHeight;

        LayerMask mask = currentState == MovementState.Ground ? groundLayer : (groundLayer | climbableLayer);
        Vector3 finalMove = move;

        if (Physics.CapsuleCast(bottom, top, radius, move.normalized, out RaycastHit hit, move.magnitude, mask, QueryTriggerInteraction.Ignore))
        {
            float safeDistance = Mathf.Max(hit.distance - 0.01f, 0f);
            Vector3 safeMove = move.normalized * safeDistance;
            Vector3 slide = Vector3.ProjectOnPlane(move - safeMove, hit.normal);
            finalMove = safeMove + slide;
        }

        return finalMove;
    }

    private bool EvaluateSurfaceAnchorsMatrix(out Vector3 aggregateNormal, out Vector3 aggregatePoint)
    {
        aggregateNormal = Vector3.zero; aggregatePoint = Vector3.zero;
        int authenticatedHitsCount = 0;
        float variance = capsule.radius * 0.6f;

        // 🎯 Use the wall's own tangent axes, not the character's local axes
        Vector3 wallRight = Vector3.Cross(Vector3.up, smoothedSurfaceNormal).normalized;
        if (wallRight.sqrMagnitude < 0.001f) wallRight = Vector3.Cross(smoothedSurfaceNormal, Vector3.forward).normalized;
        Vector3 wallUp = Vector3.Cross(smoothedSurfaceNormal, wallRight).normalized;

        Vector3[] tangentOffsets = new Vector3[]
        {
        Vector3.zero,
        wallUp    * variance,
        -wallUp   * variance,
        wallRight * variance,
        -wallRight* variance
        };

        for (int i = 0; i < 5; i++)
        {
            // 🎯 Push origin AWAY from the wall along the normal, not transform.up
            Vector3 origin = rb.position + (smoothedSurfaceNormal * (capsule.height * 0.5f)) + tangentOffsets[i];

            LayerMask activeMask = (currentState == MovementState.Ground) ? groundLayer : climbableLayer;

            if (Physics.Raycast(origin, -smoothedSurfaceNormal, out RaycastHit hit, detachDistance + 0.5f, activeMask, QueryTriggerInteraction.Ignore))
            {
                aggregateNormal += hit.normal; aggregatePoint += hit.point;
                authenticatedHitsCount++;
            }
        }

        if (authenticatedHitsCount > 0)
        {
            aggregateNormal = (aggregateNormal / authenticatedHitsCount).normalized;
            aggregatePoint = aggregatePoint / authenticatedHitsCount;
            return true;
        }
        return false;
    }

    private void InterrogateForwardSurfaces()
    {
        // Use whichever direction is being pressed, camera-relative — fallback to facing direction if no input
        Vector3 searchDirection;
        if (rawInput.sqrMagnitude > 0.01f)
        {
            Camera activeCam = Camera.main;
            Vector3 camForward = activeCam != null ? activeCam.transform.forward : transform.forward;
            Vector3 camRight = activeCam != null ? activeCam.transform.right : transform.right;
            camForward.y = 0f; camRight.y = 0f;
            camForward.Normalize(); camRight.Normalize();
            searchDirection = ((camRight * rawInput.x) + (camForward * rawInput.y)).normalized;
        }
        else
        {
            searchDirection = transform.forward;
        }

        Vector3 origin = capsule.bounds.center;

        // 🎯 Direct raycast along the search direction — no OverlapSphere ambiguity
        if (Physics.Raycast(origin, searchDirection, out RaycastHit hit, attachDistance, climbableLayer))
        {
            // Reject hits whose normal doesn't actually face back toward us (rules out tops/edges)
            if (Vector3.Dot(hit.normal, -searchDirection) > 0.5f)
            {
                InitiateTransitionSequence(hit.point, hit.normal);
            }
        }
    }

    private void UpdatePlayerRotationProfile(Vector3 moveDirection)
    {
        if (currentState == MovementState.Ground) return;

        Vector3 faceIntoWall = -smoothedSurfaceNormal;

        Vector3 wallUpReference = Vector3.ProjectOnPlane(Vector3.up, smoothedSurfaceNormal).normalized;
        if (wallUpReference.sqrMagnitude < 0.001f) wallUpReference = transform.right;

        Quaternion targetSurfaceAlignment = Quaternion.LookRotation(faceIntoWall, wallUpReference);

        // 🎯 Spin around the wall's vertical axis (turn left/right), not the forward axis (no more cartwheel)
        Quaternion sliderSpinAngle = Quaternion.AngleAxis(manualPoseAngle, wallUpReference);

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetSurfaceAlignment * sliderSpinAngle, rotationSpeed * Time.fixedDeltaTime));
    }

    private void InitiateTransitionSequence(Vector3 destinationPoint, Vector3 destinationNormal)
    {
        SetState(MovementState.Transition);
        transitionProgress = 0.0f;
        transitionTargetPosition = destinationPoint + (destinationNormal * ((capsule.height * 0.5f) + surfaceOffset));

        // Face INTO the wall (chest/face touches surface)
        Vector3 forward = -destinationNormal;
        Vector3 upHint = Vector3.ProjectOnPlane(Vector3.up, destinationNormal).normalized;
        if (upHint.sqrMagnitude < 0.001f) upHint = transform.right; // fallback for ceiling/floor normals

        transitionTargetRotation = Quaternion.LookRotation(forward, upHint);
        pendingSurfaceNormal = destinationNormal;

        rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
    }

    private void ExecuteTransitionInterpolation()
    {
        transitionProgress = Mathf.Clamp01(transitionProgress + Time.fixedDeltaTime * transitionSpeedMultiplier);
        rb.MovePosition(Vector3.Lerp(rb.position, transitionTargetPosition, transitionProgress));
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, transitionTargetRotation, transitionProgress));

        if (transitionProgress >= 1f)
        {
            // 🎯 Classify using the real surface normal, not the character's rotated up-axis
            float dot = Vector3.Dot(pendingSurfaceNormal, Vector3.up);
            if (dot > 0.8f) SetState(MovementState.Ground);
            else if (dot < -0.8f) SetState(MovementState.Ceiling);
            else SetState(MovementState.Wall);

            currentSurfaceNormal = pendingSurfaceNormal;
            smoothedSurfaceNormal = pendingSurfaceNormal;
        }
    }

    private void InitiateDetachmentSequence()
    {
        SetState(MovementState.Ground);
        rb.MovePosition(rb.position + smoothedSurfaceNormal * 0.2f);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.magnitude < 0.01f) forward = Vector3.forward;

        rb.MoveRotation(Quaternion.LookRotation(forward, Vector3.up));
        currentSurfaceNormal = Vector3.up; smoothedSurfaceNormal = Vector3.up;
    }

    private void SetState(MovementState targetState)
    {
        bool wasStuckBefore = (currentState == MovementState.Wall || currentState == MovementState.Ceiling);
        currentState = targetState;
        rb.isKinematic = false;

        bool isStuckNow = (currentState == MovementState.Wall || currentState == MovementState.Ceiling);
        OnStickModeStateChanged?.Invoke(isStuckNow);

        if (IsOwner && !wasStuckBefore && isStuckNow)
        {
            // 🔊 Updated to local 2D AudioSource
            if (localAudioSource != null && wallStickClip != null)
            {
                localAudioSource.PlayOneShot(wallStickClip);
            }
        }
        if (currentState == MovementState.Ground)
        {
            rb.useGravity = true;
        }
        else
        {
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void SetManualPoseRotation(float angle)
    {
        if (!IsOwner) return;
        manualPoseAngle = angle;
    }
}