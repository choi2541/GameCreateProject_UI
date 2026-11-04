using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform player;

    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 cameraPosition = transform.position;

        cameraPosition.x = player.position.x;
        cameraPosition.y = player.position.y;

        transform.position = cameraPosition;
    }
}