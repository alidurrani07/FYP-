using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyPatrol : MonoBehaviour
{
    public Transform[] patrolPoints;
    public int targetPoint;
    public float speed;

    private Animator animator;

    void Start()
    {
        targetPoint = 0;
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        PatrolMovement();
        UpdateAnimation();
    }

    void PatrolMovement()
    {
        // Distance check (more reliable than == comparison)
        float distance = Vector3.Distance(transform.position, patrolPoints[targetPoint].position);
        if (distance < 0.1f)
        {
            IncreaseTargetIndex();
        }

        // Move enemy
        transform.position = Vector3.MoveTowards(
            transform.position,
            patrolPoints[targetPoint].position,
            speed * Time.deltaTime
        );

        // Optional: Face direction of movement
        Vector3 dir = patrolPoints[targetPoint].position - transform.position;
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
        // Calculate speed for animation (0 = idle, >0 = walking)
        float currentSpeed = speed;

        // When very close to target, speed becomes 0 (idle)
        if (Vector3.Distance(transform.position, patrolPoints[targetPoint].position) < 0.1f)
            currentSpeed = 0f;

        // Send speed to Animator
        animator.SetFloat("Speed", currentSpeed);
    }

    void IncreaseTargetIndex()
    {
        targetPoint++;
        if (targetPoint >= patrolPoints.Length)
        {
            targetPoint = 0;
        }
    }
}
