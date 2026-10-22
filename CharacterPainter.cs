using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterPainter : NetworkBehaviour
{
    public RectTransform crosshair;
    public Camera mainCamera;
    public Color brushColor = Color.blue;
    public int baseRadius = 12;
    public ParticleSystem paintSprayParticles;
    public float particleDistanceFromCamera = 0.3f;
    private Slider sizeSlider;
    private Texture2D editableTexture;
    private bool isSpraying = false;          // Tracks crosshair UI spray button state
    private bool isDirectFingerPainting = false; // Tracks if a direct screen finger layout is drawing
    private int activePaintFingerId = -1;
    private HiderMovement hiderMovement;
    private Renderer targetRenderer;
    private int currentBrushRadius;
    private bool textureNeedsApply = false;
    private float nextPaintNetworkTime = 0f;
    private const float PAINT_NETWORK_INTERVAL = 0.08f;
    public override void OnNetworkSpawn()
    {
        StartCoroutine(SetupTextureAndUI());
    }
    private System.Collections.IEnumerator SetupTextureAndUI()
    {
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null && targetRenderer.material.HasProperty("_BaseMap"))
        {
            Texture2D originalTex = targetRenderer.material.GetTexture("_BaseMap") as Texture2D;
            if (originalTex != null)
            {
                try
                {
                    editableTexture = new Texture2D(originalTex.width, originalTex.height, TextureFormat.RGBA32, false);
                    editableTexture.SetPixels(originalTex.GetPixels());
                    editableTexture.Apply();
                    targetRenderer.material.SetTexture("_BaseMap", editableTexture);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[CharacterPainter] Read/Write failed! Ensure file settings allow reading. Error: {e.Message}");
                }
            }
        }
        if (!IsOwner) yield break;
        while (UIAssignmentManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        var ui = UIAssignmentManager.Instance;
        sizeSlider = ui.hiderBrushSizeSlider;
        currentBrushRadius = Mathf.RoundToInt(sizeSlider.value);
        crosshair = ui.hiderPaintCrosshair;
        if (sizeSlider == null) Debug.LogError("[CharacterPainter] Brush Size Slider reference missing from UIAssignmentManager!");

        if (crosshair == null) Debug.LogError("[CharacterPainter] Crosshair UI RectTransform missing from UIAssignmentManager!");
        // 🎯 DYNAMIC CROSSHAIR SCALING & AUTO-RESET MATRIX
        if (sizeSlider != null)
        {
            sizeSlider.onValueChanged.RemoveAllListeners();
            sizeSlider.onValueChanged.AddListener((value) =>
            {
                currentBrushRadius = Mathf.RoundToInt(value);

                if (crosshair != null)
                {
                    float visualScale = 1f + (value * 0.2f);
                    crosshair.localScale = new Vector3(visualScale, visualScale, 1f);
                }
            });

            // Handle PointerUp to natively reset crosshair back to normal size
            EventTrigger trigger = sizeSlider.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = sizeSlider.gameObject.AddComponent<EventTrigger>();

            trigger.triggers.Clear();
            EventTrigger.Entry pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pointerUp.callback.AddListener((data) => {
                if (crosshair != null) crosshair.localScale = Vector3.one;
            });
            trigger.triggers.Add(pointerUp);
        }


        if (mainCamera == null)

            mainCamera = GetComponentInParent<HiderMovement>().hiderCameraHolder.GetComponentInChildren<Camera>();



        if (mainCamera == null) Debug.LogError("[CharacterPainter] Main Camera Holder could not be found under parent!");



        if (ui.hiderDropperButton != null)

        {

            ui.hiderDropperButton.onClick.RemoveAllListeners();

            ui.hiderDropperButton.onClick.AddListener(SampleColorFromWorld);

        }
        UpdateParticleColor();
    }



    void Update()
    {
        if (!IsOwner) return;
        if (hiderMovement == null)
        {
            hiderMovement = GetComponentInParent<HiderMovement>();
        }

        HandleSmartTouchInput();

        if (isSpraying && !isDirectFingerPainting)
        {
            ExecuteSprayThroughCrosshair();
        }
        if (textureNeedsApply && editableTexture != null)
        {
            editableTexture.Apply();
            textureNeedsApply = false;
        }
    }

    private void HandleSmartTouchInput()

    {

        // Only allow smart finger painting inputs if the Paint UI layout panel is active on screen

        if (UIAssignmentManager.Instance != null &&

            UIAssignmentManager.Instance.hiderPaintUIGroup != null &&

            !UIAssignmentManager.Instance.hiderPaintUIGroup.activeInHierarchy)

        {

            // Reset paint locks safely if the mode is exited mid-drag
            if (isDirectFingerPainting) ResetFingerPaintingState();
            return;
        }
        // 💻 UNITY EDITOR MOUSE TESTING FALLBACK
        if (!Application.isMobilePlatform)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            Vector2 mousePos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {

                // Handled gracefully by the pointer down wrapper event matrix now

            }
            if ((hiderMovement != null && hiderMovement.isCameraLocked) || isDirectFingerPainting)
            {
                isDirectFingerPainting = true;
                if (paintSprayParticles != null && !paintSprayParticles.isPlaying)
                    paintSprayParticles.Play();
                ExecutePaintAtScreenPosition(mousePos);
            }
            return;
        }

        // 📱 MOBILE MULTI-TOUCH PROCESSING (New Input System API)

        var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen == null) return;
        var touches = touchscreen.touches;
        for (int i = 0; i < touches.Count; i++)
        {
            var touch = touches[i];
            int fingerId = touch.touchId.ReadValue();
            Vector2 touchPos = touch.position.ReadValue();
            var phase = touch.phase.ReadValue();

            if (phase == UnityEngine.InputSystem.TouchPhase.Began && activePaintFingerId == -1)
            {
                if (hiderMovement != null && hiderMovement.isCameraLocked)
                {
                    activePaintFingerId = fingerId;
                    isDirectFingerPainting = true;
                    if (paintSprayParticles != null && !paintSprayParticles.isPlaying)
                    {
                        paintSprayParticles.Play();
                    }
                }
            }
            // Phase 2: Drawing Drag
            if (fingerId == activePaintFingerId)
            {
                if (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    ExecutePaintAtScreenPosition(touchPos);
                }
                // Phase 3: Lifted Finger
                if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    ResetFingerPaintingState();
                }
            }
        }
    }
    private void ResetFingerPaintingState()
    {
        activePaintFingerId = -1;
        isDirectFingerPainting = false;

        if (hiderMovement != null) hiderMovement.isCameraLocked = false;
        if (paintSprayParticles != null)
            paintSprayParticles.Stop();
        if (paintSprayParticles != null)
        {
            paintSprayParticles.Stop();
        }
    }
    private void UpdateParticleColor()
    {
        if (paintSprayParticles == null) return;

        var main = paintSprayParticles.main;
        main.startColor = brushColor;
    }
    private void UpdateSprayParticleAtHit(RaycastHit hit)
    {
        if (paintSprayParticles == null)
            return;

        paintSprayParticles.transform.position = hit.point + hit.normal * 0.01f;
        paintSprayParticles.transform.rotation = Quaternion.LookRotation(-hit.normal);
    }
    private void ExecutePaintAtScreenPosition(Vector2 screenPosition)
    {
        if (sizeSlider == null || mainCamera == null)
            return;

        Vector2[] offsets =
        {
            Vector2.zero,
            Vector2.left * 5f,
            Vector2.right * 5f
        };

        foreach (Vector2 offset in offsets)
        {
            Ray ray = mainCamera.ScreenPointToRay(screenPosition + offset);
            RaycastHit hit;
            int mask = LayerMask.GetMask("PlayerBody");

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, mask))
            {
                CharacterPainter hitPainter = hit.collider.GetComponent<CharacterPainter>();

                if (hitPainter == this)
                {
                    Vector2 uv = hit.textureCoord;

                    UpdateSprayParticleAtHit(hit);

                    int radius = currentBrushRadius;

                    // 🟢 1. Paint local texture immediately on every frame (Smooth visual feedback)
                    ApplyPaintCircle(uv, radius, brushColor);

                    // 🟢 2. Send over the network ONLY when the interval timer allows it
                    if (Time.time >= nextPaintNetworkTime)
                    {
                        nextPaintNetworkTime = Time.time + PAINT_NETWORK_INTERVAL;
                        PaintPixelServerRpc(uv, radius, brushColor);
                    }

                    return;
                }
            }
        }
    }

    public void SetSpraying(bool state)
    {
        if (!IsOwner) return;

        isSpraying = state;

        if (paintSprayParticles != null)
        {
            if (state)
            {
                if (!paintSprayParticles.isPlaying)
                    paintSprayParticles.Play();
            }
            else
            {
                paintSprayParticles.Stop();
            }
        }
    }
    private void ExecuteSprayThroughCrosshair()
    {
        if (crosshair == null || sizeSlider == null || mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(crosshair.position);
        RaycastHit hit;
        int mask = LayerMask.GetMask("PlayerBody");

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, mask))
        {
            CharacterPainter hitPainter = hit.collider.GetComponent<CharacterPainter>();
            if (hitPainter == this)
            {
                Vector2 uv = hit.textureCoord;
                UpdateSprayParticleAtHit(hit);
                int currentRadius = currentBrushRadius;

                // 🟢 1. Paint local texture immediately on every frame (Smooth visual feedback)
                ApplyPaintCircle(uv, currentRadius, brushColor);

                // 🟢 2. Send over the network ONLY when the interval timer allows it
                if (Time.time >= nextPaintNetworkTime)
                {
                    nextPaintNetworkTime = Time.time + PAINT_NETWORK_INTERVAL;
                    PaintPixelServerRpc(uv, currentRadius, brushColor);
                }
            }
        }
    }
    [ServerRpc]
    private void PaintPixelServerRpc(Vector2 uv, int radius, Color color)
    {
        PaintPixelClientRpc(uv, radius, color, OwnerClientId);
    }
    [ClientRpc]
    private void PaintPixelClientRpc(Vector2 uv, int radius, Color color, ulong senderClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == senderClientId) return;
        ApplyPaintCircle(uv, radius, color);
    }
    private void ApplyPaintCircle(Vector2 uv, int radius, Color color)
    {
        if (editableTexture == null) return;
        int cx = (int)(uv.x * editableTexture.width);
        int cy = (int)(uv.y * editableTexture.height);
        int startX = Mathf.Max(0, cx - radius);
        int endX = Mathf.Min(editableTexture.width, cx + radius);
        int startY = Mathf.Max(0, cy - radius);

        int endY = Mathf.Min(editableTexture.height, cy + radius);

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) < radius * radius)
                {

                    editableTexture.SetPixel(x, y, color);

                }
            }
        }
        editableTexture.Apply();
    }
    public void SetBrushColorLocal(Color newColor)
    {
        if (!IsOwner) return;

        brushColor = newColor;

        UpdateParticleColor();

        if (UIAssignmentManager.Instance != null &&
            UIAssignmentManager.Instance.hiderColorPreviewImage != null)
        {
            UIAssignmentManager.Instance.hiderColorPreviewImage.color = brushColor;
        }
    }

    public void SampleColorFromWorld()
    {
        if (!IsOwner || crosshair == null || mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(crosshair.position);

        RaycastHit hit;

        int mask = ~LayerMask.GetMask("PlayerBody");

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, mask))
        {
            Renderer hitRenderer = hit.transform.GetComponent<Renderer>();
            if (hitRenderer != null)
            {
                Color sampledColor = Color.white;
                bool success = false;
                if (hitRenderer.material.HasProperty("_BaseColor"))
                {
                    Color tintColor = hitRenderer.material.GetColor("_BaseColor");

                    if (tintColor != Color.white)

                    {
                        sampledColor = tintColor;
                        success = true;
                    }
                }
                if (!success)
                {
                    Texture2D targetTex = hitRenderer.material.GetTexture("_BaseMap") as Texture2D;
                    if (targetTex != null)
                    {
                        Vector2 uv = hit.textureCoord;

                        sampledColor = targetTex.GetPixel((int)(uv.x * targetTex.width), (int)(uv.y * targetTex.height));
                        success = true;
                    }
                }
                if (success)
                {
                    brushColor = sampledColor;
                    UpdateParticleColor();
                    if (UIAssignmentManager.Instance != null && UIAssignmentManager.Instance.hiderColorPreviewImage != null)
                    {
                        UIAssignmentManager.Instance.hiderColorPreviewImage.color = brushColor;
                    }
                }
            }
        }
    }
}