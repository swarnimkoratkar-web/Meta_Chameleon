using UnityEngine;
using Unity.Netcode;

public class HiderAnimationController : NetworkBehaviour
{
    private Animator anim;
    private HiderMovement movementScript;

    [Header("Optimization Settings")]
    [Tooltip("Drop your pre-saved static pose meshes here in the exact order of your PoseIDs!")]
    public Mesh[] preBakedPoseMeshes;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        movementScript = GetComponent<HiderMovement>();

        // 🎯 FIX: Automatically set the default mesh at the start of the match
        MeshCollider meshCol = GetComponentInChildren<MeshCollider>();
        if (meshCol != null && preBakedPoseMeshes != null && preBakedPoseMeshes.Length > 0)
        {
            // Clear out whatever empty mesh is currently loaded
            meshCol.sharedMesh = null;

            // Element 0 in your array should be your standard "Human Mode" / Default Pose mesh asset!
            meshCol.sharedMesh = preBakedPoseMeshes[0];

            Physics.SyncTransforms();
        }
    }

    void Update()
    {
        if (!IsOwner || anim == null || movementScript == null || movementScript.moveJoystick == null) return;

        float currentSpeed = movementScript.moveJoystick.InputDirection.magnitude;
        int activePose = anim.GetInteger("PoseID");

        if (activePose == 0)
        {
            anim.SetFloat("Speed", currentSpeed);
        }
        else
        {
            anim.SetFloat("Speed", 0f);
        }
    }

    public void SelectPoseLocal(int poseIndex)
    {
        if (!IsOwner) return;

        anim.SetInteger("PoseID", poseIndex);
        SetPoseServerRpc(poseIndex);
    }

    [ServerRpc]
    private void SetPoseServerRpc(int poseIndex)
    {
        SetPoseClientRpc(poseIndex);
    }

    [ClientRpc]
    private void SetPoseClientRpc(int poseIndex)
    {
        if (anim != null)
        {
            anim.SetInteger("PoseID", poseIndex);

            // 🎯 GRAB COMPONENTS
            MeshCollider meshCol = GetComponentInChildren<MeshCollider>();
            Rigidbody rb = GetComponentInParent<Rigidbody>();

            if (meshCol != null && preBakedPoseMeshes != null && poseIndex < preBakedPoseMeshes.Length)
            {
                meshCol.sharedMesh = null;
                meshCol.sharedMesh = preBakedPoseMeshes[poseIndex];

                // 🟢 FIX: Force the mesh object and its collider to keep the "Hider" tag!
                meshCol.gameObject.tag = "Hider";

                Physics.SyncTransforms();
            }
        }
    }
}