using UnityEngine;

public class HidingSpot : MonoBehaviour
{
    [Header("Spot Settings")]
    public int spotID;
    public Color wallColor = Color.white;
    public Texture2D wallTexture;

    [Header("Camouflage Stroke Data")]
    [Tooltip("Recorded stroke data asset used by bots to paint this camouflage.")]
    public PaintStrokeData paintData;

    [Header("Pose Settings")]
    [Tooltip("The PoseID integer for the Animator (e.g., 1 for sitpose, 2 for cactuspose, etc.)")]
    public int targetPoseID = 1;
    public bool matchSpotRotation = true;

    [Header("Occupancy State")]
    public bool isOccupied = false;

    private void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? Color.red : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.25f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.8f);
    }
}