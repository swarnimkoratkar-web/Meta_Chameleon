using System.Collections.Generic;
using UnityEngine;

public class WaypointNode : MonoBehaviour
{
    [Header("Connected Paths")]
    public List<WaypointNode> neighbors = new List<WaypointNode>();

    public WaypointNode GetRandomNextNode(WaypointNode previousNode = null)
    {
        if (neighbors == null || neighbors.Count == 0) return null;
        if (neighbors.Count == 1) return neighbors[0];

        List<WaypointNode> options = new List<WaypointNode>(neighbors);

        // Avoid instantly retracing steps unless there's no other path
        if (previousNode != null && options.Contains(previousNode) && options.Count > 1)
        {
            options.Remove(previousNode);
        }

        return options[Random.Range(0, options.Count)];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        if (neighbors == null) return;

        Gizmos.color = Color.yellow;
        foreach (WaypointNode neighbor in neighbors)
        {
            if (neighbor != null)
            {
                Gizmos.DrawLine(transform.position, neighbor.transform.position);
            }
        }
    }
}