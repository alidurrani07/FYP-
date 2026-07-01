using System.Collections.Generic;
using Invector;
using Invector.vCharacterController;
using Invector.vCamera;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class RebelGTAThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Camera targetCamera;
    public vThirdPersonCamera invectorCamera;
    public bool useInvectorCameraMovement = true;
    public bool freezeInvectorCameraMovement = false;
    public bool stabilizeInvectorRigidbody = true;
    public bool lockCursorOnStart = true;

    [Header("GTA View")]
    public Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
    public float distance = 4.6f;
    public float aimingDistance = 3f;
    public float shoulderOffset = 0.45f;
    public float aimingShoulderOffset = 0.65f;
    public float followSmooth = 12f;
    public float rotationSmooth = 18f;
    public float mouseSensitivityX = 3.5f;
    public float mouseSensitivityY = 2.2f;
    public float gamepadSensitivityX = 120f;
    public float gamepadSensitivityY = 90f;
    public float minPitch = -35f;
    public float maxPitch = 65f;
    public float autoBehindDelay = 1.25f;
    public float autoBehindSpeed = 4f;
    public float zoomMin = 2.4f;
    public float zoomMax = 6.5f;
    public float fieldOfView = 60f;
    public float aimingFieldOfView = 55f;

    [Header("Collision")]
    public LayerMask collisionMask = ~(1 << 8);
    public float collisionRadius = 0.28f;
    public float collisionPadding = 0.15f;

    private float yaw;
    private float pitch = 12f;
    private float currentDistance;
    private float lastManualInputTime;
    private Vector3 followVelocity;
    private Rigidbody targetBody;
    private bool shoulderOnRight = true;

    private const float InputDeadZone = 0.001f;
    private static readonly HashSet<string> MissingAxes = new HashSet<string>();

    private void Awake()
    {
        ResolveReferences();
        currentDistance = Mathf.Clamp(distance, zoomMin, zoomMax);
    }

    private void Start()
    {
        ResolveReferences();
        LinkInvectorCamera();
        SnapBehindTarget();

        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (!target || !targetCamera)
        {
            return;
        }

        LinkInvectorCamera();
        if (useInvectorCameraMovement)
        {
            KeepChildCameraAligned();
            UpdateCameraLens();
            return;
        }

        ReadCameraInput();
        AutoFollowBehindPlayer();
        UpdateCameraTransform();
        UpdateCameraLens();
    }

    public void SnapBehindTarget()
    {
        if (!target)
        {
            return;
        }

        yaw = target.eulerAngles.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        lastManualInputTime = Time.time;
    }

    private void ResolveReferences()
    {
        if (!target)
        {
            if (vGameController.instance != null && vGameController.instance.currentPlayer != null)
            {
                target = vGameController.instance.currentPlayer.transform;
            }

            if (!target)
            {
                var controller = FindObjectOfType<vThirdPersonController>();
                if (controller)
                {
                    target = controller.transform;
                }
            }

            if (!target)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player)
                {
                    target = player.transform;
                }
            }
        }

        if (!targetCamera)
        {
            targetCamera = GetComponentInChildren<Camera>();
            if (!targetCamera)
            {
                targetCamera = Camera.main;
            }
        }

        if (!invectorCamera)
        {
            invectorCamera = GetComponent<vThirdPersonCamera>();
        }

        if (target && !targetBody)
        {
            targetBody = target.GetComponent<Rigidbody>();
        }
    }

    private void LinkInvectorCamera()
    {
        if (!invectorCamera || !target)
        {
            return;
        }

        invectorCamera.mainTarget = target;
        invectorCamera.currentTarget = target;
        invectorCamera.targetCamera = targetCamera;
        invectorCamera.isFreezed = freezeInvectorCameraMovement || !useInvectorCameraMovement;
        invectorCamera.switchRight = shoulderOnRight ? 1 : -1;

        if (!invectorCamera.isInit)
        {
            invectorCamera.Init();
            invectorCamera.isFreezed = freezeInvectorCameraMovement || !useInvectorCameraMovement;
        }

        if (stabilizeInvectorRigidbody)
        {
            invectorCamera.selfRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void ReadCameraInput()
    {
        var mouseX = SafeAxis("Mouse X") * mouseSensitivityX;
        var mouseY = SafeAxis("Mouse Y") * mouseSensitivityY;
        var rightX = SafeAxis("RightAnalogHorizontal") * gamepadSensitivityX * Time.deltaTime;
        var rightY = SafeAxis("RightAnalogVertical") * gamepadSensitivityY * Time.deltaTime;
        var x = mouseX + rightX;
        var y = mouseY + rightY;

        if (Mathf.Abs(x) > InputDeadZone || Mathf.Abs(y) > InputDeadZone)
        {
            yaw += x;
            pitch = Mathf.Clamp(pitch - y, minPitch, maxPitch);
            lastManualInputTime = Time.time;
        }

        var scroll = SafeAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > InputDeadZone)
        {
            distance = Mathf.Clamp(distance - (scroll * 2f), zoomMin, zoomMax);
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            shoulderOnRight = !shoulderOnRight;
            if (invectorCamera)
            {
                invectorCamera.switchRight = shoulderOnRight ? 1 : -1;
            }
        }
    }

    private void AutoFollowBehindPlayer()
    {
        if (Time.time - lastManualInputTime < autoBehindDelay || !target)
        {
            return;
        }

        var moving = false;
        if (targetBody)
        {
            moving = targetBody.linearVelocity.sqrMagnitude > 0.25f;
        }

        moving |= Mathf.Abs(SafeAxis("Horizontal")) > 0.1f || Mathf.Abs(SafeAxis("Vertical")) > 0.1f;
        if (!moving)
        {
            return;
        }

        yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, autoBehindSpeed * Time.deltaTime);
    }

    private void UpdateCameraTransform()
    {
        var aiming = IsAiming();
        var wantedDistance = aiming ? aimingDistance : distance;
        currentDistance = Mathf.Lerp(currentDistance, wantedDistance, 10f * Time.deltaTime);

        var orbit = Quaternion.Euler(pitch, yaw, 0f);
        var flatOrbit = Quaternion.Euler(0f, yaw, 0f);
        var side = (aiming ? aimingShoulderOffset : shoulderOffset) * (shoulderOnRight ? 1f : -1f);
        var pivot = target.position + (Vector3.up * pivotOffset.y) + (target.right * pivotOffset.x) + (target.forward * pivotOffset.z);
        pivot += flatOrbit * Vector3.right * side;

        var desiredPosition = pivot + (orbit * Vector3.back * currentDistance);
        desiredPosition = ResolveCollision(pivot, desiredPosition);

        var desiredRotation = Quaternion.LookRotation(pivot - desiredPosition, Vector3.up);
        if (invectorCamera)
        {
            desiredRotation *= Quaternion.Euler(invectorCamera.offsetMouse.y, invectorCamera.offsetMouse.x, 0f);
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, 1f / Mathf.Max(1f, followSmooth));
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmooth * Time.deltaTime);

        KeepChildCameraAligned();
    }

    private void KeepChildCameraAligned()
    {
        if (!targetCamera)
        {
            return;
        }

        targetCamera.transform.localPosition = Vector3.zero;
        targetCamera.transform.localRotation = Quaternion.identity;
    }

    private Vector3 ResolveCollision(Vector3 pivot, Vector3 desiredPosition)
    {
        var toCamera = desiredPosition - pivot;
        var rayDistance = toCamera.magnitude;
        if (rayDistance <= 0.01f)
        {
            return desiredPosition;
        }

        if (Physics.SphereCast(pivot, collisionRadius, toCamera.normalized, out var hit, rayDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            return pivot + toCamera.normalized * Mathf.Max(0.2f, hit.distance - collisionPadding);
        }

        return desiredPosition;
    }

    private void UpdateCameraLens()
    {
        var wantedFov = IsAiming() ? aimingFieldOfView : fieldOfView;
        targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, wantedFov, 10f * Time.deltaTime);
    }

    private bool IsAiming()
    {
        return Input.GetMouseButton(1) || SafeAxis("LT") > 0.1f;
    }

    private static float SafeAxis(string axisName)
    {
        if (MissingAxes.Contains(axisName))
        {
            return 0f;
        }

        try
        {
            return Input.GetAxisRaw(axisName);
        }
        catch
        {
            MissingAxes.Add(axisName);
            return 0f;
        }
    }
}
