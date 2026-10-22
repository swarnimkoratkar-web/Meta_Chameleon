using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class StrokeRecorder : MonoBehaviour
{
    [Header("Target Data Asset")]
    [Tooltip("The ScriptableObject asset where recorded strokes will be saved.")]
    public PaintStrokeData targetPaintData;

    [Header("Controls")]
    public bool isRecording = false;

    private CharacterPainter painter;
    private bool isPaintButtonHeld = false;
    private bool hasHookedButton = false;

    private void Awake()
    {
        painter = GetComponent<CharacterPainter>();
        if (painter == null)
            painter = GetComponentInChildren<CharacterPainter>();
        if (painter == null)
            painter = GetComponentInParent<CharacterPainter>();

        if (painter == null)
            Debug.LogError("<color=red>[StrokeRecorder] CharacterPainter component NOT FOUND!</color>");
    }

    private void Start()
    {
        StartCoroutine(HookToPaintButton());
    }

    private System.Collections.IEnumerator HookToPaintButton()
    {
        while (UIAssignmentManager.Instance == null || UIAssignmentManager.Instance.hiderPaintButton == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        var trigger = UIAssignmentManager.Instance.hiderPaintButton;

        EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((data) => { isPaintButtonHeld = true; });
        trigger.triggers.Add(down);

        EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener((data) => { isPaintButtonHeld = false; });
        trigger.triggers.Add(up);

        hasHookedButton = true;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            isRecording = !isRecording;

            if (isRecording)
            {
                Debug.Log("<color=green>[StrokeRecorder] Recording STARTED.</color>");
            }
            else
            {
                SaveAsset();
                int count = targetPaintData != null ? targetPaintData.strokes.Count : 0;
                Debug.Log($"<color=yellow>[StrokeRecorder] Recording STOPPED and SAVED! Total strokes: {count}</color>");
            }
        }

        if (!isRecording || targetPaintData == null || painter == null) return;

       
        if (isPaintButtonHeld)
        {
            CaptureCrosshairStroke();
        }
    }

    private void CaptureCrosshairStroke()
    {
        if (painter.crosshair == null) return;

        Camera cam = painter.mainCamera != null ? painter.mainCamera : Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(painter.crosshair.position);
        int mask = LayerMask.GetMask("PlayerBody");
        if (mask == 0) mask = ~0;

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, mask))
        {
            CharacterPainter hitPainter = hit.collider.GetComponent<CharacterPainter>();
            if (hitPainter == null)
                hitPainter = hit.collider.GetComponentInParent<CharacterPainter>();

            if (hitPainter == painter && hit.textureCoord != Vector2.zero)
            {
                float radius = GetCurrentRadius();
                Color32 color = painter.brushColor;

                if (targetPaintData.strokes.Count > 0)
                {
                    PaintStroke lastStroke = targetPaintData.strokes[targetPaintData.strokes.Count - 1];
                    if (Vector2.Distance(lastStroke.uv, hit.textureCoord) < 0.003f)
                        return;
                }

                targetPaintData.AddStroke(hit.textureCoord, radius, color);
            }
        }
    }

    private float GetCurrentRadius()
    {
        if (UIAssignmentManager.Instance != null && UIAssignmentManager.Instance.hiderBrushSizeSlider != null)
        {
            return UIAssignmentManager.Instance.hiderBrushSizeSlider.value;
        }

        return painter.baseRadius > 0 ? painter.baseRadius : 12f;
    }

    [ContextMenu("Save Asset Dirty State")]
    public void SaveAsset()
    {
#if UNITY_EDITOR
        if (targetPaintData != null)
        {
            UnityEditor.EditorUtility.SetDirty(targetPaintData);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
    }
}