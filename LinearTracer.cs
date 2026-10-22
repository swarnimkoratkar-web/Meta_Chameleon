using UnityEngine;

public class LinearTracer : MonoBehaviour
{
    private Vector3 targetPosition;
    private float moveSpeed = 250f;
    private bool isInitialized = false;

    public void Initialize(Vector3 start, Vector3 end, float speed)
    {
        transform.position = start;
        targetPosition = end;
        moveSpeed = speed;
        isInitialized = true;

        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail != null) trail.Clear();
    }

    void Update()
    {
        if (!isInitialized) return;

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            Destroy(gameObject);
        }
    }
}