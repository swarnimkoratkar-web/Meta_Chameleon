using UnityEngine;
using Unity.Netcode;

public class WeaponFollowCamera : NetworkBehaviour
{
    public Transform cameraHolder;

    void LateUpdate()
    {
        if (cameraHolder == null) return;

        // Automatically matches the exact vertical pivot/rotation of the camera container
        transform.rotation = cameraHolder.rotation;
    }
}