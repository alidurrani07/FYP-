using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float jumpResetSeconds = 1f;
    public float deadZone = 0.15f;
    public float rotationSpeed = 10f;
    public float mouseSensitivity = 2f; // NEW

    private Animator animator;
    private InputSystem_Actions inputActions;
    private Vector2 moveInput = Vector2.zero;
    private Vector2 lookInput = Vector2.zero; // NEW
    private bool runInput = false; // Animator parameters

    private int isWalkingHash;
    private int isRunningHash;
    private int isJumpingHash;
    private int isWalkingBackwardHash;
    private int isLeftStrafeHash;
    private int isRightStrafeHash;
    private int isStopWalkingHash;
    private int isWalkingLeftTurnHash;
    private int isLeftStrafeWalkingHash;
    private int isRightStrafeWalkingHash;
    private int isRightTurnHash;
    private int isRunningRightTurnHash;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();

        // Movement
        inputActions.Player.Move.performed += OnMovePerformed;
        inputActions.Player.Move.canceled += OnMoveCanceled;

        // Sprint
        inputActions.Player.Sprint.started += OnRunStarted;
        inputActions.Player.Sprint.canceled += OnRunCanceled;

        // Jump
        inputActions.Player.Jump.started += OnJumpStarted;

        // Mouse Look --------------- NEW
        inputActions.Player.Look.performed += OnLookPerformed;
        inputActions.Player.Look.canceled += OnLookCanceled;
    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= OnMovePerformed;
        inputActions.Player.Move.canceled -= OnMoveCanceled;

        inputActions.Player.Sprint.started -= OnRunStarted;
        inputActions.Player.Sprint.canceled -= OnRunCanceled;

        inputActions.Player.Jump.started -= OnJumpStarted;

        // Mouse Look
        inputActions.Player.Look.performed -= OnLookPerformed;
        inputActions.Player.Look.canceled -= OnLookCanceled;

        inputActions.Player.Disable();
    }

    private void Start()
    {
        animator = GetComponent<Animator>();
        isWalkingHash = Animator.StringToHash("isWalking");
        isRunningHash = Animator.StringToHash("isRunning");
        isJumpingHash = Animator.StringToHash("isJump");
        isWalkingBackwardHash = Animator.StringToHash("isWalkingBackward");
        isLeftStrafeHash = Animator.StringToHash("isLeftStrafe");
        isRightStrafeHash = Animator.StringToHash("isRightStrafe");
        isStopWalkingHash = Animator.StringToHash("isStopWalking");
        isWalkingLeftTurnHash = Animator.StringToHash("isWalkingLeftTurn");
        isLeftStrafeWalkingHash = Animator.StringToHash("isLeftStrafeWalking");
        isRightStrafeWalkingHash = Animator.StringToHash("isRightStrafeWalking");
        isRightTurnHash = Animator.StringToHash("isRightTurn");
        isRunningRightTurnHash = Animator.StringToHash("isRunningRightTurn");
    }

    private void Update()
    {
        HandleMovement();
    }

    // -------- INPUT EVENTS --------
    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        moveInput = Vector2.zero;
    }

    private void OnRunStarted(InputAction.CallbackContext ctx)
    {
        runInput = true;
    }

    private void OnRunCanceled(InputAction.CallbackContext ctx)
    {
        runInput = false;
    }

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        StopAllCoroutines();
        animator.SetBool(isJumpingHash, true);
        StartCoroutine(ResetJump(jumpResetSeconds));
    }

    private IEnumerator ResetJump(float delay)
    {
        yield return new WaitForSeconds(delay);
        animator.SetBool(isJumpingHash, false);
    }

    // -------- MOUSE LOOK INPUT --------
    private void OnLookPerformed(InputAction.CallbackContext ctx)
    {
        lookInput = ctx.ReadValue<Vector2>();
    }

    private void OnLookCanceled(InputAction.CallbackContext ctx)
    {
        lookInput = Vector2.zero;
    }

    // -------- MOVEMENT --------
    private void HandleMovement()
    {
        float x = moveInput.x;
        float y = moveInput.y;
        float mag = moveInput.magnitude;

        // ---------------- MOUSE ROTATION ----------------
        if (lookInput.sqrMagnitude > 0.01f)
        {
            float mouseX = lookInput.x * mouseSensitivity;
            transform.Rotate(0f, mouseX, 0f);
        }

        // ---------------- ANIMATOR STATES ----------------
        animator.SetBool(isStopWalkingHash, mag < deadZone);
        animator.SetBool(isWalkingHash, y > deadZone && !runInput);
        animator.SetBool(isRunningHash, y > deadZone && runInput);
        animator.SetBool(isWalkingBackwardHash, y < -deadZone);
        animator.SetBool(isLeftStrafeHash, x < -deadZone && Mathf.Abs(y) <= deadZone);
        animator.SetBool(isRightStrafeHash, x > deadZone && Mathf.Abs(y) <= deadZone);
        animator.SetBool(isLeftStrafeWalkingHash, x < -deadZone && y > deadZone);
        animator.SetBool(isRightStrafeWalkingHash, x > deadZone && y > deadZone);
        animator.SetBool(isWalkingLeftTurnHash, y > deadZone && x < -deadZone);
        animator.SetBool(isRightTurnHash, y > -deadZone && x > deadZone);
        animator.SetBool(isRunningRightTurnHash, runInput && x > deadZone && y > deadZone);

        // ---------------- MOVEMENT ----------------
        float speed = runInput ? runSpeed : walkSpeed;
        Vector3 move = new Vector3(x, 0, y);
        Vector3 direction = transform.TransformDirection(move);
        transform.Translate(direction.normalized * speed * Time.deltaTime, Space.World);
    }
}
