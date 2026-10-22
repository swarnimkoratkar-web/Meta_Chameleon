using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Unity.Netcode;

public class AnswerCheckOrbitCamera : MonoBehaviour
{
    public static AnswerCheckOrbitCamera Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject answerCheckPanel;
    [SerializeField] private Image touchDragImage; // 👈 Drag your transparent Image here
    [SerializeField] private TextMeshProUGUI spectateNameText;
    [SerializeField] private Button nextHiderButton;
    [SerializeField] private Slider distanceSlider;

    [Header("Camera Control Settings")]
    [SerializeField] private float rotationSensitivity = 0.2f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private LayerMask obstacleLayers;

    private Camera orbitCamera;
    private List<Transform> hiderTargets = new List<Transform>();
    private List<string> hiderNames = new List<string>();
    private int currentTargetIndex = 0;

    private float currentDistance = 3.5f;
    private float currentYaw = 0f;
    private float currentPitch = 20f;

    private void Awake()
    {
        Instance = this;
        orbitCamera = GetComponent<Camera>();
        if (orbitCamera != null) orbitCamera.enabled = false;
        if (answerCheckPanel != null) answerCheckPanel.SetActive(false);
    }

    private void Start()
    {
        if (nextHiderButton != null)
            nextHiderButton.onClick.AddListener(NextHider);

        if (distanceSlider != null)
        {
            currentDistance = distanceSlider.value;
            distanceSlider.onValueChanged.AddListener(OnSliderDistanceChanged);
        }

        // 🟢 AUTO-ASSIGN DRAG EVENT IN CODE (No Event Trigger component needed!)
        SetupTouchDragListener();
    }

    private void SetupTouchDragListener()
    {
        if (touchDragImage == null) return;

        EventTrigger trigger = touchDragImage.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = touchDragImage.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.Drag
        };

        entry.callback.AddListener((data) => { HandleDrag((PointerEventData)data); });
        trigger.triggers.Add(entry);
    }

    private void HandleDrag(PointerEventData eventData)
    {
        if (!orbitCamera.enabled) return;

        currentYaw += eventData.delta.x * rotationSensitivity;
        currentPitch -= eventData.delta.y * rotationSensitivity;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (!orbitCamera.enabled || hiderTargets.Count == 0) return;

        Transform target = hiderTargets[currentTargetIndex];
        if (target == null) return;

        Vector3 focusPoint = target.position + Vector3.up * 1.2f;

        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 targetDir = rotation * -Vector3.forward;

        float desiredDistance = currentDistance;
        if (Physics.SphereCast(focusPoint, 0.2f, targetDir, out RaycastHit hit, currentDistance, obstacleLayers))
        {
            desiredDistance = Mathf.Clamp(hit.distance - 0.1f, 0.5f, currentDistance);
        }

        transform.position = focusPoint + targetDir * desiredDistance;
        transform.LookAt(focusPoint);
    }

    public void EnableAnswerCheckSpectating(List<NetworkObject> hiderObjects)
    {
        hiderTargets.Clear();
        hiderNames.Clear();

        foreach (var netObj in hiderObjects)
        {
            if (netObj != null)
            {
                hiderTargets.Add(netObj.transform);
                string realName = MainMenuData.GetPlayerName(netObj.OwnerClientId);
                hiderNames.Add(realName);
            }
        }

        if (hiderTargets.Count == 0) return;

        currentTargetIndex = 0;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                Camera localCam = localPlayer.GetComponentInChildren<Camera>();
                if (localCam != null) localCam.enabled = false;
            }
        }
        if (orbitCamera != null) orbitCamera.enabled = true;
        if (answerCheckPanel != null) answerCheckPanel.SetActive(true);

        UpdateSpectateUI();
    }

    public void DisableAnswerCheckSpectating()
    {
        if (orbitCamera != null) orbitCamera.enabled = false;
        if (answerCheckPanel != null) answerCheckPanel.SetActive(false);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                Camera localCam = localPlayer.GetComponentInChildren<Camera>();
                if (localCam != null) localCam.enabled = true;
            }
        }
    }

    public void NextHider()
    {
        if (hiderTargets.Count <= 1) return;
        currentTargetIndex = (currentTargetIndex + 1) % hiderTargets.Count;
        UpdateSpectateUI();
    }

    private void OnSliderDistanceChanged(float val)
    {
        currentDistance = val;
    }

    private void UpdateSpectateUI()
    {
        if (spectateNameText != null && hiderNames.Count > currentTargetIndex)
        {
            spectateNameText.text = $"Spectating: {hiderNames[currentTargetIndex]}";
        }
    }
}