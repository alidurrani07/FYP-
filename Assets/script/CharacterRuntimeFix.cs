using System.Collections;
using Invector.vCharacterController;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class CharacterRuntimeFix : MonoBehaviour
{
    [Header("Animator")]
    public bool keepAnimatorAlwaysAnimating = true;
    public bool resetMovementParametersOnStart = true;
    public bool crossFadeIdleOnStart = true;
    public bool syncAnimatorRootMotionWithInvector = true;
    public string[] idleStateNames = { "Idle", "Free Locomotion", "Locomotion" };

    [Header("Invector Motor")]
    public bool disableStepOffsetOnThisObject = true;

    [Header("NavMesh")]
    public bool disableNavMeshAgentOnThisObject = false;
    public bool snapEnabledAgentToNavMesh = true;
    public float navMeshSampleRadius = 5f;

    private Animator animator;
    private NavMeshAgent navMeshAgent;
    private vThirdPersonMotor motor;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        motor = GetComponent<vThirdPersonMotor>();
        ConfigureAnimator();
        ConfigureInvectorMotor();
        ConfigureNavMeshAgent();
    }

    private void OnEnable()
    {
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        ConfigureNavMeshAgent();
    }

    private IEnumerator Start()
    {
        yield return null;

        ConfigureAnimator();
        ConfigureInvectorMotor();

        if (resetMovementParametersOnStart)
        {
            ResetMovementParameters();
        }

        if (crossFadeIdleOnStart)
        {
            PlayFirstAvailableIdleState();
        }
    }

    private void LateUpdate()
    {
        if (disableNavMeshAgentOnThisObject && navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.enabled = false;
        }

        ConfigureAnimator();
        ConfigureInvectorMotor();
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.enabled = true;

        if (keepAnimatorAlwaysAnimating)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (syncAnimatorRootMotionWithInvector)
        {
            if (motor == null)
            {
                motor = GetComponent<vThirdPersonMotor>();
            }

            animator.applyRootMotion = motor != null && motor.useRootMotion;
        }
    }

    private void ConfigureInvectorMotor()
    {
        if (motor == null)
        {
            motor = GetComponent<vThirdPersonMotor>();
        }

        if (motor == null)
        {
            return;
        }

        if (disableStepOffsetOnThisObject)
        {
            motor.useStepOffset = false;
        }
    }

    private void ConfigureNavMeshAgent()
    {
        NavMeshAgent agent = navMeshAgent != null ? navMeshAgent : GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            return;
        }

        if (disableNavMeshAgentOnThisObject)
        {
            agent.enabled = false;
            return;
        }

        if (snapEnabledAgentToNavMesh && agent.enabled && !agent.isOnNavMesh &&
            NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleRadius, agent.areaMask))
        {
            agent.Warp(hit.position);
        }
    }

    private void ResetMovementParameters()
    {
        if (animator == null)
        {
            return;
        }

        SetFloatIfExists("InputMagnitude", 0f);
        SetFloatIfExists("InputVertical", 0f);
        SetFloatIfExists("InputHorizontal", 0f);
        SetFloatIfExists("RotationMagnitude", 0f);
        SetFloatIfExists("Speed", 0f);

        SetBoolIfExists("isWalking", false);
        SetBoolIfExists("isRunning", false);
        SetBoolIfExists("isShooting", false);
        SetBoolIfExists("isWalkingBackward", false);
        SetBoolIfExists("isLeftStrafe", false);
        SetBoolIfExists("isRightStrafe", false);
        SetBoolIfExists("isStopWalking", true);
    }

    private void PlayFirstAvailableIdleState()
    {
        if (animator == null || animator.runtimeAnimatorController == null || idleStateNames == null)
        {
            return;
        }

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            for (int i = 0; i < idleStateNames.Length; i++)
            {
                string stateName = idleStateNames[i];
                if (string.IsNullOrEmpty(stateName))
                {
                    continue;
                }

                int stateHash = Animator.StringToHash(stateName);
                int layerStateHash = Animator.StringToHash(animator.GetLayerName(layer) + "." + stateName);
                if (animator.HasState(layer, stateHash) || animator.HasState(layer, layerStateHash))
                {
                    animator.CrossFadeInFixedTime(animator.HasState(layer, layerStateHash) ? layerStateHash : stateHash, 0.1f, layer, 0f);
                    animator.Update(0f);
                    return;
                }
            }
        }

        animator.Update(0f);
    }

    private void SetFloatIfExists(string parameter, float value)
    {
        for (int i = 0; i < animator.parameters.Length; i++)
        {
            AnimatorControllerParameter animatorParameter = animator.parameters[i];
            if (animatorParameter.type == AnimatorControllerParameterType.Float && animatorParameter.name == parameter)
            {
                animator.SetFloat(parameter, value);
                return;
            }
        }
    }

    private void SetBoolIfExists(string parameter, bool value)
    {
        for (int i = 0; i < animator.parameters.Length; i++)
        {
            AnimatorControllerParameter animatorParameter = animator.parameters[i];
            if (animatorParameter.type == AnimatorControllerParameterType.Bool && animatorParameter.name == parameter)
            {
                animator.SetBool(parameter, value);
                return;
            }
        }
    }
}
