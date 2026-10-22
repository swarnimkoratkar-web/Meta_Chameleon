using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class HiderPhysicsCuller : NetworkBehaviour
{
    [Header("Culling Settings")]
    [Tooltip("The radius around the hider where prop mesh colliders turn ON.")]
    public float cullingRadius = 12f;

    [Tooltip("How often the script calculates distances (in seconds).")]
    public float checkInterval = 0.2f;

    [Tooltip("Max colliders allowed to turn ON in a single frame to prevent micro-stutters.")]
    public int maxActivationsPerFrame = 3;

    private List<MeshCollider> allPropsInScene = new List<MeshCollider>();
    private HashSet<MeshCollider> activeColliders = new HashSet<MeshCollider>();
    private Coroutine cullingRoutine;

    public override void OnNetworkSpawn()
    {
        // 🎯 RUN ONLY ON LOCAL HIDER
        // Only the player controlling this specific hider needs to activate props around themselves
        if (IsOwner)
        {
            FindAllPropsInScene();
            cullingRoutine = StartCoroutine(CullPhysicsLoop());
        }
    }

    // Finds all the paintable props in the scene once at startup
    private void FindAllPropsInScene()
    {
        allPropsInScene.Clear();
        GameObject[] propObjects = GameObject.FindGameObjectsWithTag("PaintableProp");

        foreach (GameObject obj in propObjects)
        {
            if (obj.TryGetComponent<MeshCollider>(out var meshCol))
            {
                allPropsInScene.Add(meshCol);
                // Ensure they start turned off
                meshCol.enabled = false;
            }
        }
    }

    private IEnumerator CullPhysicsLoop()
    {
        WaitForSeconds waitTime = new WaitForSeconds(checkInterval);
        List<MeshCollider> toRemove = new List<MeshCollider>();

        while (true)
        {
            Vector3 myPos = transform.position;
            float sqrRadius = cullingRadius * cullingRadius;
            int activationCounter = 0;

            // 1. Check distances using fast square-magnitude math (bypasses disabled collider limits)
            foreach (MeshCollider meshCol in allPropsInScene)
            {
                if (meshCol == null) continue;

                // Fast distance check (faster than Vector3.Distance)
                float sqrDistance = (meshCol.transform.position - myPos).sqrMagnitude;

                if (sqrDistance <= sqrRadius)
                {
                    // If it's close and not active yet, wake it up!
                    if (!meshCol.enabled)
                    {
                        meshCol.enabled = true;
                        activationCounter++;

                        // Frame-budget protection
                        if (activationCounter >= maxActivationsPerFrame)
                        {
                            yield return null;
                            activationCounter = 0;
                        }
                    }
                    activeColliders.Add(meshCol);
                }
            }

            // 2. Turn OFF props we walked away from
            toRemove.Clear();
            foreach (MeshCollider activeCol in activeColliders)
            {
                if (activeCol == null)
                {
                    toRemove.Add(activeCol);
                    continue;
                }

                float sqrDistance = (activeCol.transform.position - myPos).sqrMagnitude;
                if (sqrDistance > sqrRadius)
                {
                    activeCol.enabled = false;
                    toRemove.Add(activeCol);
                }
            }

            // Clean up our tracking set
            foreach (MeshCollider col in toRemove)
            {
                activeColliders.Remove(col);
            }
            yield return waitTime;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (cullingRoutine != null) StopCoroutine(cullingRoutine);
        base.OnNetworkDespawn();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, cullingRadius);
    }
}