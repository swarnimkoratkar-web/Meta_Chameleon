using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class HunterCombat : NetworkBehaviour
{
    [Header("Weapon FX Settings")]
    public Transform gunMuzzleTip;
    public ParticleSystem muzzleFlashFX;
    public AudioClip gunshotSound;
    private AudioSource audioSource;

    [Header("Hit Feedback Settings")]
    public GameObject hitParticlePrefab;
    public AudioClip hitSoundAttacker;
    public AudioClip hitSoundTarget;

    [Header("Bullet Tracer Visuals")]
    [SerializeField] private GameObject tracerPrefab;
    [SerializeField] private float tracerSpeed = 250f;

    public RectTransform hunterCrosshair;
    public Camera hunterCamera;
    public float fireCooldown = 1.5f;

    private bool canShoot = true;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (hunterCamera != null) hunterCamera.gameObject.SetActive(false);
            return;
        }

        UIAssignmentManager ui = FindAnyObjectByType<UIAssignmentManager>();
        if (ui != null)
        {
            ui.SetupHunterUI(this, GetComponent<HunterController>());
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void TryFireWeapon()
    {
        if (!IsOwner || !canShoot || hunterCamera == null || hunterCrosshair == null) return;

        StartCoroutine(FireCooldownRoutine());

        Ray ray = hunterCamera.ScreenPointToRay(hunterCrosshair.position);
        RaycastHit hit;

        Vector3 targetPoint = ray.origin + (ray.direction * 100f);
        int mask = ~LayerMask.GetMask("Hunter", "Ignore Raycast");

        if (Physics.Raycast(ray, out hit, 100f, mask, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;

            if (hit.collider.CompareTag("Hider") || (hit.collider.transform.root != null && hit.collider.transform.root.CompareTag("Hider")))
            {
                if (hit.collider.transform.root != transform.root)
                {
                    BotHiderAI botHider = hit.collider.GetComponentInParent<BotHiderAI>();
                    if (botHider != null)
                    {
                        TriggerServerRpc(botHider.BotVirtualClientId);
                        TriggerHitFeedbackServerRpc(hit.point, hit.normal, botHider.BotVirtualClientId);
                    }
                    else
                    {
                        NetworkObject netObj = hit.collider.GetComponentInParent<NetworkObject>();
                        if (netObj != null)
                        {
                            TriggerServerRpc(netObj.OwnerClientId);
                            TriggerHitFeedbackServerRpc(hit.point, hit.normal, netObj.OwnerClientId);
                        }
                    }
                }
            }
        }

        PlayWeaponEffectsServerRpc(targetPoint);
    }

    [ServerRpc]
    private void TriggerServerRpc(ulong hiderID, ServerRpcParams rpcParams = default)
    {
        if (GameMatchEngine.Instance != null)
        {
            ulong hunterID = rpcParams.Receive.SenderClientId;
            GameMatchEngine.Instance.OnHiderEliminated(hunterID, hiderID);
        }
    }

    [ServerRpc]
    private void TriggerHitFeedbackServerRpc(Vector3 hitPoint, Vector3 hitNormal, ulong hiderClientId)
    {
        TriggerHitFeedbackClientRpc(hitPoint, hitNormal, hiderClientId);
    }

    [ClientRpc]
    private void TriggerHitFeedbackClientRpc(Vector3 hitPoint, Vector3 hitNormal, ulong hiderClientId)
    {
        if (hitParticlePrefab != null)
        {
            Quaternion hitRot = Quaternion.LookRotation(hitNormal);
            GameObject fx = Instantiate(hitParticlePrefab, hitPoint, hitRot);
            Destroy(fx, 2f);
        }

        if (IsOwner)
        {
            if (audioSource != null && hitSoundAttacker != null)
            {
                audioSource.PlayOneShot(hitSoundAttacker);
            }
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.12f, 0.15f);
            }
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

    private IEnumerator FireCooldownRoutine()
    {
        canShoot = false;
        yield return new WaitForSeconds(fireCooldown);
        canShoot = true;
    }

    [ServerRpc]
    private void PlayWeaponEffectsServerRpc(Vector3 endPoint)
    {
        PlayWeaponEffectsClientRpc(endPoint);
    }

    [ClientRpc]
    private void PlayWeaponEffectsClientRpc(Vector3 endPoint)
    {
        Vector3 startPoint = gunMuzzleTip != null ? gunMuzzleTip.position : transform.position;

        if (muzzleFlashFX != null)
        {
            if (gunMuzzleTip != null)
            {
                muzzleFlashFX.transform.position = gunMuzzleTip.position;
                muzzleFlashFX.transform.rotation = gunMuzzleTip.rotation;
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
}