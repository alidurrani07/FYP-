using UnityEngine;
using UnityEngine.AI;

public class SimpleAI : MonoBehaviour
{
    public enum FormationType
    {
        LeaderCircle,
        None,
        VerticalLine
    }

    public static FormationType currentFormation = FormationType.LeaderCircle;

    [Header("Target")]
    public Transform player;

    [Header("Squad")]
    public int squadIndex;
    public int squadSize = 6;
    public bool isLeader = false;

    [Header("Movement")]
    public float runSpeed = 5f;

    [Header("Formation Tuning")]
    public float tightRadius = 1.5f;
    public float lineSpacing = 0.8f;
    public float sideOffset = 1.2f;

    private NavMeshAgent agent;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.acceleration = 12f;
        agent.angularSpeed = 180f;

        SetIdle();
    }

    void Update()
    {
        HandleInput();

        if (player == null) return;

        // 🔥 ALWAYS MOVE (no blocking logic)
        agent.isStopped = false;

        Vector3 target = GetFormationPosition();
        agent.SetDestination(target);

        HandleAnimation();
        RotateToMove();
    }

    // ---------------- INPUT ----------------
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            currentFormation = FormationType.LeaderCircle;

        if (Input.GetKeyDown(KeyCode.Alpha2))
            currentFormation = FormationType.None;

        if (Input.GetKeyDown(KeyCode.Alpha3))
            currentFormation = FormationType.VerticalLine;
    }

    // ---------------- FORMATIONS ----------------
    Vector3 GetFormationPosition()
    {
        switch (currentFormation)
        {
            // 🔵 Leader + Circle
            case FormationType.LeaderCircle:
                {
                    if (isLeader)
                        return player.position + player.right * sideOffset;

                    float angle = (360f / Mathf.Max(1, (squadSize - 1))) * squadIndex;
                    float rad = angle * Mathf.Deg2Rad;

                    Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * tightRadius;

                    return player.position + offset;
                }

            // 🔴 Overlap mode
            case FormationType.None:
                return player.position;

            // 🟢 Vertical line (behind player)
            case FormationType.VerticalLine:
                {
                    Vector3 forward = -player.forward;

                    float offset = (squadIndex - (squadSize - 1) / 2f) * lineSpacing;

                    return player.position + forward * offset;
                }
        }

        return player.position;
    }

    // ---------------- ANIMATION ----------------
    void HandleAnimation()
    {
        float speed = agent.velocity.magnitude;

        if (speed < 0.15f)
            SetIdle();
        else
            SetRun();
    }

    void SetIdle()
    {
        animator.SetBool("isRunning", false);
    }

    void SetRun()
    {
        animator.SetBool("isRunning", true);
    }

    // ---------------- ROTATION ----------------
    void RotateToMove()
    {
        Vector3 vel = agent.velocity;
        vel.y = 0;

        if (vel.magnitude > 0.1f)
        {
            Quaternion rot = Quaternion.LookRotation(vel);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
        }
    }
}