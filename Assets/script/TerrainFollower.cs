using UnityEngine;

public class TerrainFollower : MonoBehaviour
{
    public float rayDistance = 10f;
    public float heightOffset = 0.0f;
    public float smoothSpeed = 10f;
    public LayerMask groundMask = ~0;

    void FixedUpdate()
    {
        Vector3 origin = transform.position + Vector3.up * 2f;

        if (TryGetGroundHit(origin, out RaycastHit hit))
        {
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, hit.point.y + heightOffset, Time.deltaTime * smoothSpeed);
            transform.position = pos;
        }
    }

    private bool TryGetGroundHit(Vector3 origin, out RaycastHit bestHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
        bool found = false;
        float bestScore = float.MaxValue;
        bestHit = new RaycastHit();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || IsOwnCollider(hitCollider))
                continue;

            bool preferredGround = hitCollider is TerrainCollider || hitCollider is MeshCollider;
            float score = hits[i].distance + (preferredGround ? 0f : 1000f);
            if (score < bestScore)
            {
                bestScore = score;
                bestHit = hits[i];
                found = true;
            }
        }

        return found;
    }

    private bool IsOwnCollider(Collider hitCollider)
    {
        Transform hitTransform = hitCollider.transform;
        if (hitTransform == null)
            return false;

        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return true;

        Rigidbody attachedBody = hitCollider.attachedRigidbody;
        return attachedBody != null &&
               (attachedBody.transform == transform || attachedBody.transform.IsChildOf(transform));
    }
}
