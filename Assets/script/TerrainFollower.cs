using UnityEngine;

public class TerrainFollower : MonoBehaviour
{
    public float rayDistance = 10f;
    public float heightOffset = 0.0f;
    public float smoothSpeed = 10f;

    void FixedUpdate()
    {
        Vector3 origin = transform.position + Vector3.up * 2f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance))
        {
            // Only follow layers with terrain/colliders
            if (hit.collider is TerrainCollider || hit.collider is MeshCollider)
            {
                Vector3 pos = transform.position;
                pos.y = Mathf.Lerp(pos.y, hit.point.y + heightOffset, Time.deltaTime * smoothSpeed);
                transform.position = pos;
            }
        }
    }
}
