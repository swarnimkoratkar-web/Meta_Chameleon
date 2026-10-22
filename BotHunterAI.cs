using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotHunterAI : NetworkBehaviour
{
    [Header("Branching Patrol Settings")]
    public WaypointNode currentWaypoint;
    public float waypointWaitTime = 2f;
    public float detectionRadius = 15f;
    public float fieldOfViewAngle = 90f;
    public LayerMask visionBlockLayers;

    [Header("Spectator & Aim Visuals")]
    public Transform hunterCameraHolder;
    public float aimTurnSpeed = 10f;
    public NetworkVariable<float> NetworkPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Combat References")]
    public Transform gunBarrel;
    public float attackCooldown = 2f;

    [Header("Weapon FX Settings")]
    public ParticleSystem muzzleFlashFX;
    public AudioClip gunshotSound;
    private AudioSource audioSource;

    [Header("Bullet Tracer Visuals")]
    [SerializeField] private GameObject tracerPrefab;
    [SerializeField] private float tracerSpeed = 250f;

    [Header("Hit Feedback Settings")]
    public GameObject hitParticlePrefab;
    public AudioClip hitSoundTarget;

    private NavMeshAgent agent;
    private Animator anim;
    private bool canAttack = true;
    public ulong BotVirtualClientId;

    private WaypointNode previousWaypoint;
    private float stuckCheckTimer = 0f;
    private Transform currentTargetHider;

    public override void OnNetworkSpawn()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        anim = GetComponentInChildren<Animator>();

        if (!IsServer) return;

        agent = GetComponent<NavMeshAgent>();

        // Fallback: Pick the closest waypoint if none was assigned by GameManager
        if (currentWaypoint == null)
        {
            currentWaypoint = FindNearestWaypoint();
        }

        if (currentWaypoint != null && agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(currentWaypoint.transform.position);
        }

        StartCoroutine(HunterAILoop());
    }

    private void Update()
    {
        if (anim != null && agent != null && agent.enabled)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude);
        }

        // Client/Spectator Camera Sync
        if (!IsServer)
        {
            if (hunterCameraHolder != null)
            {
                Quaternion targetRot = Quaternion.Euler(NetworkPitch.Value, 0f, 0f);
                hunterCameraHolder.localRotation = Quaternion.Lerp(hunterCameraHolder.localRotation, targetRot, Time.deltaTime * 15f);
            }
            return;
        }

        // Server-side Camera Orientation & NetworkPitch write
        HandleServerCameraAim();
    }

    private void HandleServerCameraAim()
    {
        if (hunterCameraHolder == null) return;

        if (currentTargetHider != null)
        {
            // Aim camera holder directly at hider's center
            Vector3 aimTarget = currentTargetHider.position + Vector3.up * 0.5f;
            Vector3 localTargetDir = hunterCameraHolder.parent.InverseTransformPoint(aimTarget);

            float targetPitch = -Mathf.Atan2(localTargetDir.y, localTargetDir.z) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(targetPitch, -60f, 60f);

            hunterCameraHolder.localRotation = Quaternion.Slerp(
                hunterCameraHolder.localRotation,
                Quaternion.Euler(targetPitch, 0f, 0f),
                Time.deltaTime * aimTurnSpeed
            );

            NetworkPitch.Value = hunterCameraHolder.localEulerAngles.x;
        }
        else
        {
            // Idle vertical scanning for spectators
            float scanPitch = Mathf.Sin(Time.time * 2f) * 12f;
            hunterCameraHolder.localRotation = Quaternion.Slerp(
                hunterCameraHolder.localRotation,
                Quaternion.Euler(scanPitch, 0f, 0f),
                Time.deltaTime * 4f
            );

            NetworkPitch.Value = scanPitch;
        }
    }

    private IEnumerator HunterAILoop()
    {
        while (true)
        {
            currentTargetHider = DetectNearbyHider();

            if (currentTargetHider != null)
            {
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

                Vector3 targetDirection = (currentTargetHider.position - transform.position).normalized;
                targetDirection.y = 0;
                if (targetDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(targetDirection);
                }

                if (canAttack)
                {
                    FireAtHider(currentTargetHider);
                    yield return new WaitForSeconds(attackCooldown);
                }

                if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
            }
            else
            {
                // Branching waypoint navigation
                if (agent != null && agent.isOnNavMesh)
                {
                    bool arrivedAtNode = !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance;

                    // Anti-stuck watchdog
                    if (!arrivedAtNode && agent.velocity.sqrMagnitude < 0.05f)
                    {
                        stuckCheckTimer += 0.25f;
                        if (stuckCheckTimer >= 2.0f)
                        {
                            arrivedAtNode = true;
                            stuckCheckTimer = 0f;
                        }
                    }
                    else
                    {
                        stuckCheckTimer = 0f;
                    }

                    if (arrivedAtNode)
                    {
                        if (currentWaypoint != null)
                        {
                            WaypointNode next = currentWaypoint.GetRandomNextNode(previousWaypoint);
                            if (next != null)
                            {
                                previousWaypoint = currentWaypoint;
                                currentWaypoint = next;
                                agent.SetDestination(currentWaypoint.transform.position);
                            }
                        }
                        yield return new WaitForSeconds(waypointWaitTime);
                    }
                }
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    private WaypointNode FindNearestWaypoint()
    {
        WaypointNode[] allNodes = FindObjectsByType<WaypointNode>(FindObjectsSortMode.None);
        WaypointNode closest = null;
        float minDist = Mathf.Infinity;

        foreach (WaypointNode node in allNodes)
        {
            float dist = Vector3.Distance(transform.position, node.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = node;
            }
        }
        return closest;
    }

    private Transform DetectNearbyHider()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius);
        foreach (var col in hits)
        {
            if (col.CompareTag("Hider") || (col.transform.root != null && col.transform.root.CompareTag("Hider")))
            {
                if (col.transform.root == transform.root) continue;

                Vector3 directionToTarget = (col.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, directionToTarget);

                if (angle < fieldOfViewAngle * 0.5f)
                {
                    float dist = Vector3.Distance(transform.position, col.transform.position);
                    if (!Physics.Raycast(transform.position + Vector3.up, directionToTarget, dist, visionBlockLayers))
                    {
                        return col.transform.root;
                    }
                }
            }
        }
        return null;
    }

    private void FireAtHider(Transform targetHider)
    {
        canAttack = false;

        Vector3 startOrigin = gunBarrel != null ? gunBarrel.position : (transform.position + Vector3.up * 0.8f);
        Vector3 targetPoint = targetHider.position + Vector3.up * 0.5f;
        Vector3 shootDir = (targetPoint - startOrigin).normalized;

        int mask = ~LayerMask.GetMask("Hunter", "Ignore Raycast");
        RaycastHit hit;

        if (Physics.Raycast(startOrigin, shootDir, out hit, 100f, mask, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;

            if (hit.collider.CompareTag("Hider") || (hit.collider.transform.root != null && hit.collider.transform.root.CompareTag("Hider")))
            {
                if (hit.collider.transform.root != transform.root)
                {
                    ulong hunterId = BotVirtualClientId != 0 ? BotVirtualClientId : NetworkObjectId;

                    BotHiderAI botHider = hit.collider.GetComponentInParent<BotHiderAI>();
                    if (botHider != null)
                    {
                        if (GameMatchEngine.Instance != null)
                        {
                            GameMatchEngine.Instance.OnHiderEliminated(hunterId, botHider.BotVirtualClientId);
                        }
                        TriggerHitFeedbackClientRpc(hit.point, hit.normal, botHider.BotVirtualClientId);
                    }
                    else
                    {
                        NetworkObject targetNetObj = hit.collider.GetComponentInParent<NetworkObject>();
                        if (targetNetObj != null && GameMatchEngine.Instance != null)
                        {
                            GameMatchEngine.Instance.OnHiderEliminated(hunterId, targetNetObj.OwnerClientId);
                            TriggerHitFeedbackClientRpc(hit.point, hit.normal, targetNetObj.OwnerClientId);
                        }
                    }
                }
            }
        }

        PlayWeaponEffectsClientRpc(targetPoint);
        StartCoroutine(ResetAttackCooldown());
    }

    private IEnumerator ResetAttackCooldown()
    {
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    [ClientRpc]
    private void PlayWeaponEffectsClientRpc(Vector3 endPoint)
    {
        Vector3 startPoint = gunBarrel != null ? gunBarrel.position : transform.position;

        if (muzzleFlashFX != null)
        {
            if (gunBarrel != null)
            {
                muzzleFlashFX.transform.position = gunBarrel.position;
                muzzleFlashFX.transform.rotation = gunBarrel.rotation;
            }
            muzzleFlashFX.Play();
        }

        if (audioSource != null && gunshotSound != null)
        {
            audioSource.PlayOneShot(gunshotSound);
        }

        if (tracerPrefab != null)
        {
            GameObject tracer = Instantiate(tracerPrefab, startPoint, Quaternion.identity);
            LinearTracer tracerScript = tracer.GetComponent<LinearTracer>();
            if (tracerScript != null)
            {
                tracerScript.Initialize(startPoint, endPoint, tracerSpeed);
            }
        }
    }

    [ClientRpc]
    private void TriggerHitFeedbackClientRpc(Vector3 hitPoint, Vector3 hitNormal, ulong hiderClientId)
    {
        if (hitParticlePrefab != null)
        {
            GameObject fx = Instantiate(hitParticlePrefab, hitPoint, Quaternion.LookRotation(hitNormal));
            Destroy(fx, 2f);
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == hiderClientId)
        {
            AudioSource localAudio = NetworkManager.Singleton.LocalClient.PlayerObject?.GetComponent<AudioSource>();
            if (localAudio != null && hitSoundTarget != null)
            {
                localAudio.PlayOneShot(hitSoundTarget);
            }
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.25f, 0.35f);
            }
        }
    }
}