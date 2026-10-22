using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotHiderAI : NetworkBehaviour
{
    [Header("Visual Reference")]
    public Renderer characterRenderer;

    [Header("Burst Painting Settings")]
    [Tooltip("Number of strokes painted per burst step.")]
    public int strokesPerBurst = 12;
    [Header("Pacing Settings")]
    [Tooltip("Total target duration in seconds for the bot to finish painting.")]
    public float totalPaintDuration = 15f;

    [Tooltip("Delay in seconds between stroke bursts.")]
    public float burstInterval = 0.3f;

    private NavMeshAgent agent;
    private Animator anim;
    private HidingSpot assignedSpot;
    private bool hasReachedSpot = false;
    private Texture2D runtimePaintTexture;
    private Coroutine activePaintRoutine;
    public ulong BotVirtualClientId;

    public override void OnNetworkSpawn()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        if (characterRenderer == null)
            characterRenderer = GetComponentInChildren<Renderer>();

        // Create an isolated Texture2D copy for this bot instance
        SetupRuntimeTexture();

        if (IsServer)
        {
            StartCoroutine(HiderBotRoutine());
        }
    }

    private void SetupRuntimeTexture()
    {
        if (characterRenderer == null) return;

        Material mat = characterRenderer.material; // Instances the material for this bot
        Texture baseTex = mat.mainTexture;

        if (baseTex != null)
        {
            // Clone the existing texture into a readable/writable RGB format
            RenderTexture rt = RenderTexture.GetTemporary(baseTex.width, baseTex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(baseTex, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            runtimePaintTexture = new Texture2D(baseTex.width, baseTex.height, TextureFormat.RGBA32, false);
            runtimePaintTexture.ReadPixels(new Rect(0, 0, baseTex.width, baseTex.height), 0, 0);
            runtimePaintTexture.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            mat.mainTexture = runtimePaintTexture;
        }
    }

    private IEnumerator HiderBotRoutine()
    {
        while (HidingSpotManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        assignedSpot = HidingSpotManager.Instance.GetAvailableHidingSpot();

        if (assignedSpot == null)
        {
            Debug.LogWarning("[BotHiderAI] No available hiding spots found on the map!");
            yield break;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(assignedSpot.transform.position);
        }

        while (!hasReachedSpot)
        {
            if (agent != null && !agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
                    {
                        hasReachedSpot = true;
                    }
                }
            }

            if (anim != null && agent != null && agent.enabled)
            {
                anim.SetFloat("Speed", agent.velocity.magnitude);
            }

            yield return null;
        }

        // Disable NavMeshAgent so it doesn't fight position/rotation
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        transform.position = assignedSpot.transform.position;
        if (assignedSpot.matchSpotRotation)
        {
            transform.rotation = assignedSpot.transform.rotation;
        }

        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
        }

        // Sync pose swap, spot assignment, and transform across all clients
        ApplyBotPoseAndCamouflageClientRpc(
            assignedSpot.targetPoseID,
            assignedSpot.spotID,
            assignedSpot.transform.position,
            assignedSpot.matchSpotRotation ? assignedSpot.transform.rotation : transform.rotation
        );
    }

    [ClientRpc]
    private void ApplyBotPoseAndCamouflageClientRpc(int poseID, int spotID, Vector3 targetPos, Quaternion targetRot)
    {
        transform.position = targetPos;
        transform.rotation = targetRot;

        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        if (characterRenderer == null)
            characterRenderer = GetComponentInChildren<Renderer>();

        // 1. Pose transition
        if (anim != null)
        {
            anim.SetFloat("Speed", 0f);
            anim.SetInteger("PoseID", poseID);
        }

        // 2. Locate the hiding spot on this client
        HidingSpot localSpot = null;
        if (HidingSpotManager.Instance != null)
        {
            localSpot = HidingSpotManager.Instance.GetSpotByID(spotID);
        }

        // 3. Begin burst painting playback
        if (localSpot != null && localSpot.paintData != null && localSpot.paintData.strokes.Count > 0)
        {
            if (activePaintRoutine != null)
            {
                StopCoroutine(activePaintRoutine);
            }
            activePaintRoutine = StartCoroutine(PlaybackBurstStrokes(localSpot.paintData));
        }
    }

    private IEnumerator PlaybackBurstStrokes(PaintStrokeData paintData)
    {
        if (runtimePaintTexture == null)
            SetupRuntimeTexture();

        if (runtimePaintTexture == null) yield break;

        int totalStrokes = paintData.strokes.Count;
        if (totalStrokes == 0) yield break;

        // Dynamically calculate batch size to finish in totalPaintDuration (15s)
        int totalBursts = Mathf.Max(1, Mathf.RoundToInt(totalPaintDuration / burstInterval));
        int dynamicStrokesPerBurst = Mathf.Max(1, Mathf.CeilToInt((float)totalStrokes / totalBursts));

        int currentStroke = 0;
        int texWidth = runtimePaintTexture.width;
        int texHeight = runtimePaintTexture.height;

        while (currentStroke < totalStrokes)
        {
            int burstEnd = Mathf.Min(currentStroke + dynamicStrokesPerBurst, totalStrokes);

            // Draw this batch of strokes to pixel memory
            for (int i = currentStroke; i < burstEnd; i++)
            {
                PaintStroke stroke = paintData.strokes[i];
                DrawBrushCircle(stroke.uv, stroke.radius, stroke.color, texWidth, texHeight);
            }

            currentStroke = burstEnd;

            // Push pixels to GPU once per burst
            runtimePaintTexture.Apply(false);

            // Pacing delay
            yield return new WaitForSeconds(burstInterval);
        }
    }

    private void DrawBrushCircle(Vector2 uv, float radius, Color32 color, int texWidth, int texHeight)
    {
        int centerX = Mathf.RoundToInt(uv.x * texWidth);
        int centerY = Mathf.RoundToInt(uv.y * texHeight);
        int r = Mathf.RoundToInt(radius);
        int sqrRadius = r * r;

        int startX = Mathf.Clamp(centerX - r, 0, texWidth - 1);
        int endX = Mathf.Clamp(centerX + r, 0, texWidth - 1);
        int startY = Mathf.Clamp(centerY - r, 0, texHeight - 1);
        int endY = Mathf.Clamp(centerY + r, 0, texHeight - 1);

        for (int y = startY; y <= endY; y++)
        {
            int dy = y - centerY;
            for (int x = startX; x <= endX; x++)
            {
                int dx = x - centerX;
                if (dx * dx + dy * dy <= sqrRadius)
                {
                    runtimePaintTexture.SetPixel(x, y, color);
                }
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && assignedSpot != null && HidingSpotManager.Instance != null)
        {
            HidingSpotManager.Instance.ReleaseSpot(assignedSpot.spotID);
        }

        if (activePaintRoutine != null)
        {
            StopCoroutine(activePaintRoutine);
        }

        if (runtimePaintTexture != null)
        {
            Destroy(runtimePaintTexture);
        }

        base.OnNetworkDespawn();
    }
}