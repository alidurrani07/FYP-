using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyPatrol : MonoBehaviour
{
    public Transform[] patrolPoints;
    public int targetPoint;
    public float speed = 1.15f;
    public string speedParameter = "Speed";
    public float animationDampTime = 0.12f;

    [Header("Runtime Patrol Fallback")]
    public bool generateRuntimePatrolPoints = true;
    public int generatedPointCount = 4;
    public float generatedPointRadius = 6f;
    public float arriveDistance = 0.25f;
    public bool snapGeneratedPointsToGround = true;
    public bool snapToGroundWhileMoving = true;
    public float groundOffset = 0.03f;
    public float groundRayHeight = 4f;
    public float groundRayDistance = 12f;
    public LayerMask groundMask = ~0;

    private Animator animator;

    void Start()
    {
        targetPoint = 0;
        animator = GetComponentInChildren<Animator>();
        EnsurePatrolPoints();
        SnapTransformToGround();
    }

    void Update()
    {
        PatrolMovement();
        UpdateAnimation();
    }

    void PatrolMovement()
    {
        if (!HasPatrolPoints())
            return;

        if (targetPoint < 0 || targetPoint >= patrolPoints.Length)
            targetPoint = 0;

        Vector3 targetPosition = GetGroundedMovePosition(patrolPoints[targetPoint].position);

        // Distance check uses horizontal distance so bad point height cannot make actors climb.
        Vector3 flatDelta = targetPosition - transform.position;
        flatDelta.y = 0f;
        float distance = flatDelta.magnitude;
        if (distance < arriveDistance)
        {
            IncreaseTargetIndex();
            targetPosition = GetGroundedMovePosition(patrolPoints[targetPoint].position);
        }

        // Move enemy
        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime
        );
        transform.position = GetGroundedMovePosition(nextPosition);

        // Optional: Face direction of movement
        Vector3 dir = targetPosition - transform.position;
        dir.y = 0f;
        if (dir.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                Time.deltaTime * 5f
            );
        }
    }

    void UpdateAnimation()
    {
        if (animator == null)
            return;

        if (!HasPatrolPoints())
        {
            animator.SetFloat(speedParameter, 0f, animationDampTime, Time.deltaTime);
            return;
        }

        // Calculate speed for animation (0 = idle, >0 = walking)
        float currentSpeed = speed;

        // When very close to target, speed becomes 0 (idle)
        Vector3 flatDelta = GetGroundedMovePosition(patrolPoints[targetPoint].position) - transform.position;
        flatDelta.y = 0f;
        if (flatDelta.magnitude < arriveDistance)
            currentSpeed = 0f;

        // Send speed to Animator
        animator.SetFloat(speedParameter, currentSpeed, animationDampTime, Time.deltaTime);
    }

    void IncreaseTargetIndex()
    {
        targetPoint++;
        if (targetPoint >= patrolPoints.Length)
        {
            targetPoint = 0;
        }
    }

    void EnsurePatrolPoints()
    {
        patrolPoints = RemoveMissingPatrolPoints(patrolPoints);

        if (patrolPoints.Length > 0 || !generateRuntimePatrolPoints)
            return;

        int pointCount = Mathf.Max(2, generatedPointCount);
        float radius = Mathf.Max(1f, generatedPointRadius);
        Vector3 center = transform.position;

        GameObject root = new GameObject($"{gameObject.name}_RuntimePatrolPoints");
        root.transform.position = Vector3.zero;

        patrolPoints = new Transform[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            float angle = ((Mathf.PI * 2f) / pointCount) * i + Random.Range(-0.45f, 0.45f);
            float distance = Random.Range(radius * 0.45f, radius);
            Vector3 pointPosition = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            pointPosition = snapGeneratedPointsToGround
                ? SnapToGround(pointPosition, center.y, 0f)
                : new Vector3(pointPosition.x, center.y, pointPosition.z);

            GameObject point = new GameObject($"{gameObject.name}_PatrolPoint_{i + 1}");
            point.transform.SetParent(root.transform);
            point.transform.position = pointPosition;
            patrolPoints[i] = point.transform;
        }

        targetPoint = 0;
    }

    Transform[] RemoveMissingPatrolPoints(Transform[] points)
    {
        if (points == null || points.Length == 0)
            return new Transform[0];

        List<Transform> validPoints = new List<Transform>();
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null)
                validPoints.Add(points[i]);
        }

        return validPoints.ToArray();
    }

    Vector3 GetGroundedMovePosition(Vector3 position)
    {
        if (!snapToGroundWhileMoving)
            return position;

        return SnapToGround(position, position.y, groundOffset);
    }

    void SnapTransformToGround()
    {
        if (snapToGroundWhileMoving)
            transform.position = GetGroundedMovePosition(transform.position);
    }

    Vector3 SnapToGround(Vector3 position, float fallbackY, float offset)
    {
        if (TryGetGroundPosition(position, out Vector3 groundPosition))
            return new Vector3(position.x, groundPosition.y + offset, position.z);

        return new Vector3(position.x, fallbackY + offset, position.z);
    }

    bool TryGetGroundPosition(Vector3 position, out Vector3 groundPosition)
    {
        Vector3 rayStart = position + Vector3.up * Mathf.Max(0.1f, groundRayHeight);
        float rayDistanceTotal = Mathf.Max(0.1f, groundRayHeight + groundRayDistance);
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, rayDistanceTotal, groundMask, QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestScore = float.MaxValue;
        RaycastHit bestHit = new RaycastHit();

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

        if (found)
        {
            groundPosition = bestHit.point;
            return true;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
            {
                groundPosition = position;
                groundPosition.y = terrain.SampleHeight(position) + terrain.transform.position.y;
                return true;
            }
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit navHit, Mathf.Max(1f, groundRayDistance), NavMesh.AllAreas))
        {
            groundPosition = navHit.position;
            return true;
        }

        groundPosition = position;
        return false;
    }

    bool IsOwnCollider(Collider hitCollider)
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

    bool HasPatrolPoints()
    {
        return patrolPoints != null &&
               patrolPoints.Length > 0 &&
               targetPoint >= 0 &&
               targetPoint < patrolPoints.Length &&
               patrolPoints[targetPoint] != null;
    }
}
