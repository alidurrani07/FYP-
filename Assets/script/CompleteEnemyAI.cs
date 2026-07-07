using System.Collections;
using System.Collections.Generic;
using Invector;
using Invector.vEventSystems;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class CompleteEnemyAI : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        AttackBurst,
        Rest,
        Dead
    }

    [Header("Target")]
    public string playerTag = "Player";
    public string[] targetTags = { "Player", "CompanionAI" };
    public Transform player;
    public float detectionRange = 25f;
    public float attackRange = 18f;
    public float stopDistance = 10f;
    public float aimHeight = 1.35f;
    public bool requireLineOfSight = false;
    public LayerMask lineOfSightMask = ~0;
    public bool faceTargetWhileFiring = true;
    public float patrolTurnSpeed = 8f;
    public float combatTurnSpeed = 28f;
    public bool staticEnemy;

    [Header("Patrol")]
    public Transform[] patrolPoints;
    public float patrolSpeed = 1.15f;
    public float chaseSpeed = 3.5f;
    public float patrolPointTolerance = 0.45f;
    public float fallbackPatrolRadius = 8f;
    public float fallbackPatrolWait = 1.5f;
    public bool applyEnemyLayerOnStart = true;
    public int enemyLayer = 15;
    public int bodyPartLayer = 15;

    [Header("Attack")]
    public int bulletDamage = 8;
    public bool usePercentBurstDamage = true;
    [Range(0f, 1f)] public float burstDamagePercent = 0.3f;
    public int burstShots = 3;
    public float fireInterval = 0.35f;
    public float restAfterBurst = 6f;
    public string damageType = "EnemyRifle";

    [Header("Gun Socket")]
    public GameObject gunPrefab;
    public Transform existingGun;
    public HumanBodyBones gunHandBone = HumanBodyBones.RightHand;
    public Vector3 gunLocalPosition = new Vector3(0.03f, 0.02f, 0.04f);
    public Vector3 gunLocalEulerAngles = new Vector3(-90f, 0f, -116.205f);
    public bool overrideGunLocalScale;
    public Vector3 gunLocalScale = Vector3.one;
    public Vector3 leftGripLocalPosition = new Vector3(-0.05f, -0.03f, 0.42f);
    public Vector3 muzzleLocalPosition = new Vector3(0f, 0f, 0.9f);
    public bool createMissingGripAndMuzzle = true;
    public bool useLeftHandIK = true;
    public bool useLookAtIK = true;
    [Range(0f, 1f)] public float patrolGripIKWeight = 0.75f;
    [Range(0f, 1f)] public float combatGripIKWeight = 1f;
    public float ikBlendSpeed = 8f;

    [Header("Shot Visuals")]
    public GameObject bulletVisualPrefab;
    public float bulletVisualSpeed = 60f;
    public float bulletVisualLifetime = 2f;
    public GameObject muzzleParticlePrefab;
    public GameObject hitParticlePrefab;
    public bool drawDebugRay = true;

    [Header("Animator")]
    public string speedParameter = "Speed";
    public string shootingParameter = "isShooting";
    public string shootTrigger = "Shoot";
    public string reloadTrigger = "Reload";
    public string hitTrigger = "Hit";
    public string deadTrigger = "Dead";

    [Header("Hit Reaction")]
    public bool staggerOnHit = true;
    public float hitStaggerDuration = 0.45f;
    public float hitLeanAngle = 28f;
    public float hitKnockbackDistance = 0.18f;

    [Header("Death Grounding")]
    public bool snapToGroundOnDeath = true;
    public LayerMask deathGroundMask = ~0;
    public float deathGroundRayHeight = 2.5f;
    public float deathGroundRayDistance = 8f;
    public float deathGroundOffset = 0.02f;
    public float deathGroundSettleDuration = 0.75f;

    [Header("Hitboxes")]
    public bool createHumanoidHitboxes = true;
    public float armHitboxRadius = 0.12f;
    public float armHitboxHeight = 0.5f;
    public float legHitboxRadius = 0.16f;
    public float legHitboxHeight = 0.58f;
    public float torsoHitboxRadius = 0.3f;
    public float torsoHitboxHeight = 0.55f;
    public float headHitboxRadius = 0.18f;

    [Header("Health Bar")]
    public bool createTemporaryHealthBar = true;
    public bool drawGuiHealthBar = true;
    public Vector3 healthBarOffset = new Vector3(0f, 2.15f, 0f);
    public Vector2 healthBarSize = new Vector2(1.25f, 0.16f);
    public Color healthBarFillColor = new Color(0.9f, 0.05f, 0.05f, 1f);
    public Vector2 guiHealthBarSize = new Vector2(90f, 10f);

    [Header("Runtime")]
    public EnemyState currentState = EnemyState.Patrol;

    private readonly HashSet<string> floatParameters = new HashSet<string>();
    private readonly HashSet<string> boolParameters = new HashSet<string>();
    private readonly HashSet<string> triggerParameters = new HashSet<string>();

    private NavMeshAgent agent;
    private Animator animator;
    private vHealthController selfHealth;
    private vIHealthController playerHealth;
    private Transform gunHand;
    private Transform runtimeGun;
    private Transform leftGrip;
    private Transform muzzle;
    private Vector3 homePosition;
    private int currentPatrolIndex;
    private int shotsInCurrentBurst;
    private int damageAppliedInCurrentBurst;
    private float nextFireTime;
    private float restEndTime;
    private float fallbackWaitEndTime;
    private bool fallbackDestinationSet;
    private Canvas healthCanvas;
    private Slider healthSlider;
    private Camera healthBarCamera;
    private Transform visualRoot;
    private Quaternion visualBaseLocalRotation;
    private float staggerEndTime;
    private Coroutine deathGroundRoutine;
    private float currentGripIKWeight;
    private float currentLookIKWeight;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        selfHealth = GetComponent<vHealthController>();
        visualRoot = animator != null ? animator.transform : transform;
        visualBaseLocalRotation = visualRoot.localRotation;
        homePosition = transform.position;

        ApplyEnemyLayer();
        CacheAnimatorParameters();
        CacheBones();
        AttachGunToHand();
        EnsureRootBulletHitbox();
        EnsureHumanoidHitboxes();
        EnsureDamageReceiversOnColliders();
        CreateTemporaryHealthBar();
    }

    private void OnEnable()
    {
        if (selfHealth != null)
        {
            selfHealth.onDead.AddListener(HandleDead);
            selfHealth.onReceiveDamage.AddListener(HandleReceiveDamage);
        }
    }

    private void OnDisable()
    {
        if (selfHealth != null)
        {
            selfHealth.onDead.RemoveListener(HandleDead);
            selfHealth.onReceiveDamage.RemoveListener(HandleReceiveDamage);
        }
    }

    private void Start()
    {
        ResolvePlayer();
        ConfigureAgent();
        EnterPatrol();
    }

    private void Update()
    {
        if (IsDead())
        {
            EnterDead();
            return;
        }

        ResolvePlayer();
        UpdateAnimatorSpeed();
        UpdateGunIKWeights();
        UpdateTemporaryHealthBar();
        UpdateHitStagger();

        if (Time.time < staggerEndTime && currentState != EnemyState.Dead)
        {
            SetShooting(false);
            return;
        }

        switch (currentState)
        {
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Chase:
                UpdateChase();
                break;
            case EnemyState.AttackBurst:
                UpdateAttackBurst();
                break;
            case EnemyState.Rest:
                UpdateRest();
                break;
        }
    }

    private void OnGUI()
    {
        DrawGuiHealthBar();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.isHuman || player == null || currentState == EnemyState.Dead)
        {
            return;
        }

        Vector3 targetPoint = GetTargetPoint();

        if (useLookAtIK)
        {
            animator.SetLookAtWeight(currentLookIKWeight, 0.2f, 0.8f, 0.9f, 0.5f);
            animator.SetLookAtPosition(targetPoint);
        }

        if (useLeftHandIK && leftGrip != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, currentGripIKWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, currentGripIKWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftGrip.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftGrip.rotation);
        }
    }

    private void ConfigureAgent()
    {
        if (agent == null)
        {
            return;
        }

        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;
        agent.updateRotation = false;
        if (staticEnemy)
        {
            StopAgent();
            return;
        }

        ResumeAgent();
    }

    private void ApplyEnemyLayer()
    {
        if (!applyEnemyLayerOnStart || enemyLayer < 0 || enemyLayer > 31)
        {
            return;
        }

        gameObject.layer = enemyLayer;
    }

    private void CacheAnimatorParameters()
    {
        floatParameters.Clear();
        boolParameters.Clear();
        triggerParameters.Clear();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Float)
            {
                floatParameters.Add(parameter.name);
            }
            else if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                boolParameters.Add(parameter.name);
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                triggerParameters.Add(parameter.name);
            }
        }
    }

    private void CacheBones()
    {
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        gunHand = animator.GetBoneTransform(gunHandBone);
    }

    private void AttachGunToHand()
    {
        if (runtimeGun != null || gunHand == null)
        {
            return;
        }

        if (existingGun != null)
        {
            runtimeGun = existingGun;
        }
        else if (gunPrefab != null)
        {
            GameObject gun = Instantiate(gunPrefab);
            gun.name = gunPrefab.name;
            runtimeGun = gun.transform;
        }

        if (runtimeGun == null)
        {
            return;
        }

        runtimeGun.SetParent(gunHand, false);
        runtimeGun.localPosition = gunLocalPosition;
        runtimeGun.localRotation = Quaternion.Euler(gunLocalEulerAngles);
        if (overrideGunLocalScale)
        {
            runtimeGun.localScale = gunLocalScale;
        }

        if (createMissingGripAndMuzzle)
        {
            leftGrip = FindOrCreateChild(runtimeGun, "LeftGrip", leftGripLocalPosition);
            muzzle = FindOrCreateChild(runtimeGun, "Muzzle", muzzleLocalPosition);
        }
        else
        {
            leftGrip = FindChild(runtimeGun, "LeftGrip");
            muzzle = FindChild(runtimeGun, "Muzzle");
        }
    }

    private void ResolvePlayer()
    {
        Transform bestTarget = null;
        vIHealthController bestHealth = null;
        float bestDistance = float.MaxValue;

        if (targetTags != null && targetTags.Length > 0)
        {
            for (int i = 0; i < targetTags.Length; i++)
            {
                FindBestTargetWithTag(targetTags[i], ref bestTarget, ref bestHealth, ref bestDistance);
            }
        }
        else
        {
            FindBestTargetWithTag(playerTag, ref bestTarget, ref bestHealth, ref bestDistance);
        }

        if (bestTarget != null)
        {
            player = bestTarget;
            playerHealth = bestHealth;
            return;
        }

        ResolveFallbackPlayerByHealth();
    }

    private void UpdatePatrol()
    {
        SetShooting(false);

        if (CanDetectPlayer())
        {
            if (staticEnemy && CanAttackPlayer())
            {
                EnterAttackBurst();
            }
            else if (!staticEnemy)
            {
                EnterChase();
            }
            return;
        }

        if (staticEnemy)
        {
            StopAgent();
            return;
        }

        if (!ResumeAgent())
        {
            return;
        }

        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            PatrolAssignedPoints();
        }
        else
        {
            PatrolFallbackArea();
        }

        RotateWithAgentVelocity();
    }

    private void PatrolAssignedPoints()
    {
        if (!agent.hasPath)
        {
            SetDestinationToCurrentPatrolPoint();
        }

        if (!agent.pathPending && agent.remainingDistance <= patrolPointTolerance)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            SetDestinationToCurrentPatrolPoint();
        }
    }

    private void PatrolFallbackArea()
    {
        if (!fallbackDestinationSet || (!agent.pathPending && agent.remainingDistance <= patrolPointTolerance))
        {
            if (Time.time < fallbackWaitEndTime)
            {
                return;
            }

            Vector3 randomPoint = homePosition + Random.insideUnitSphere * fallbackPatrolRadius;
            randomPoint.y = homePosition.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, fallbackPatrolRadius, NavMesh.AllAreas))
            {
                SetAgentDestination(hit.position);
                fallbackDestinationSet = true;
                fallbackWaitEndTime = Time.time + fallbackPatrolWait;
            }
        }
    }

    private void UpdateChase()
    {
        SetShooting(false);

        if (!CanDetectPlayer())
        {
            EnterPatrol();
            return;
        }

        Vector3 targetPoint = GetTargetPoint();
        RotateToward(targetPoint, combatTurnSpeed);

        if (CanAttackPlayer())
        {
            EnterAttackBurst();
            return;
        }

        if (staticEnemy)
        {
            StopAgent();
            return;
        }

        if (!ResumeAgent())
        {
            return;
        }

        agent.speed = chaseSpeed;
        agent.stoppingDistance = stopDistance;
        SetAgentDestination(player.position);
    }

    private void UpdateAttackBurst()
    {
        if (!CanDetectPlayer())
        {
            EnterPatrol();
            return;
        }

        Vector3 targetPoint = GetTargetPoint();
        StopAgent();
        if (faceTargetWhileFiring)
        {
            FaceTargetImmediately(targetPoint);
        }
        else
        {
            RotateToward(targetPoint, combatTurnSpeed);
        }
        SetShooting(true);

        if (!CanAttackPlayer())
        {
            if (staticEnemy)
            {
                EnterRest();
            }
            else
            {
                EnterChase();
            }
            return;
        }

        if (shotsInCurrentBurst >= Mathf.Max(1, burstShots))
        {
            EnterRest();
            return;
        }

        if (Time.time >= nextFireTime)
        {
            FaceTargetImmediately(targetPoint);
            FireShot(targetPoint);
            shotsInCurrentBurst++;
            nextFireTime = Time.time + fireInterval;
        }
    }

    private void UpdateRest()
    {
        SetShooting(false);

        if (!CanDetectPlayer())
        {
            EnterPatrol();
            return;
        }

        if (staticEnemy)
        {
            StopAgent();
            if (player != null)
            {
                Vector3 targetPoint = GetTargetPoint();
                if (faceTargetWhileFiring)
                {
                    FaceTargetImmediately(targetPoint);
                }
                else
                {
                    RotateToward(targetPoint, combatTurnSpeed);
                }
            }

            if (Time.time >= restEndTime)
            {
                if (CanAttackPlayer())
                {
                    EnterAttackBurst();
                }
                else
                {
                    EnterPatrol();
                }
            }
            return;
        }

        if (!ResumeAgent())
        {
            return;
        }

        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            PatrolAssignedPoints();
        }
        else
        {
            PatrolFallbackArea();
        }

        RotateWithAgentVelocity();

        if (Time.time >= restEndTime)
        {
            if (CanAttackPlayer())
            {
                EnterAttackBurst();
            }
            else
            {
                EnterChase();
            }
        }
    }

    private void EnterPatrol()
    {
        currentState = EnemyState.Patrol;
        SetShooting(false);
        if (agent != null)
        {
            if (staticEnemy)
            {
                StopAgent();
                return;
            }

            agent.speed = patrolSpeed;
            agent.stoppingDistance = 0f;
            ResumeAgent();
        }
    }

    private void EnterChase()
    {
        currentState = EnemyState.Chase;
        SetShooting(false);
        if (agent != null)
        {
            if (staticEnemy)
            {
                StopAgent();
                return;
            }

            agent.speed = chaseSpeed;
            agent.stoppingDistance = stopDistance;
            ResumeAgent();
        }
    }

    private void EnterAttackBurst()
    {
        currentState = EnemyState.AttackBurst;
        shotsInCurrentBurst = 0;
        damageAppliedInCurrentBurst = 0;
        nextFireTime = Time.time;
        StopAgent();
        SetShooting(true);
    }

    private void EnterRest()
    {
        currentState = EnemyState.Rest;
        restEndTime = Time.time + restAfterBurst;
        SetShooting(false);
        SetTrigger(reloadTrigger);
        if (agent != null)
        {
            if (staticEnemy)
            {
                StopAgent();
                return;
            }

            agent.speed = patrolSpeed;
            agent.stoppingDistance = 0f;
            ResumeAgent();
        }
    }

    private void EnterDead()
    {
        if (currentState == EnemyState.Dead)
        {
            return;
        }

        currentState = EnemyState.Dead;
        staggerEndTime = 0f;
        if (visualRoot != null)
        {
            visualRoot.localRotation = visualBaseLocalRotation;
        }

        SetShooting(false);
        SnapDeadBodyToGround();
        PlayDeadAnimation();
        StopAgent();
        if (agent != null)
        {
            agent.enabled = false;
        }

        if (deathGroundRoutine != null)
        {
            StopCoroutine(deathGroundRoutine);
        }
        deathGroundRoutine = StartCoroutine(SettleDeadBodyOnGround());
    }

    private void HandleDead(GameObject deadObject)
    {
        EnterDead();
    }

    private void HandleReceiveDamage(vDamage damage)
    {
        if (IsDead())
        {
            EnterDead();
            return;
        }

        SetTrigger(hitTrigger);
        UpdateTemporaryHealthBar();
        StartHitStagger(damage);
    }

    private bool CanDetectPlayer()
    {
        if (player == null)
        {
            return false;
        }

        if (playerHealth != null && (playerHealth.isDead || playerHealth.currentHealth <= 0f))
        {
            return false;
        }

        return Vector3.Distance(transform.position, player.position) <= detectionRange;
    }

    private bool CanAttackPlayer()
    {
        if (!CanDetectPlayer())
        {
            return false;
        }

        if (Vector3.Distance(transform.position, player.position) > attackRange)
        {
            return false;
        }

        if (!requireLineOfSight)
        {
            return true;
        }

        Vector3 origin = GetMuzzlePosition();
        Vector3 targetPoint = GetTargetPoint();
        Vector3 direction = targetPoint - origin;
        if (direction.sqrMagnitude < 0.01f)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude, lineOfSightMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
        {
            return true;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return hitTransform == player || hitTransform.IsChildOf(player);
        }

        return true;
    }

    private bool IsDead()
    {
        return selfHealth != null && selfHealth.isDead;
    }

    private bool IsCombatState()
    {
        return currentState == EnemyState.Chase || currentState == EnemyState.AttackBurst || currentState == EnemyState.Rest;
    }

    private void FireShot(Vector3 targetPoint)
    {
        if (!CanAttackPlayer())
        {
            SetShooting(false);
            return;
        }

        SetTrigger(shootTrigger);

        if (playerHealth != null && !playerHealth.isDead)
        {
            Vector3 muzzlePosition = GetMuzzlePosition();
            Vector3 shotDirection = (targetPoint - muzzlePosition).sqrMagnitude > 0.01f ? (targetPoint - muzzlePosition).normalized : transform.forward;
            int damageValue = GetDamageForCurrentShot();
            vDamage damage = new vDamage
            {
                damageValue = damageValue,
                damageType = damageType,
                sender = transform,
                receiver = playerHealth.transform,
                hitPosition = targetPoint,
                force = shotDirection * damageValue
            };
            playerHealth.TakeDamage(damage);
            damageAppliedInCurrentBurst += damageValue;
        }
        else
        {
            ResolvePlayerHealth();
        }

        PlayShotVisuals(targetPoint);
    }

    private int GetDamageForCurrentShot()
    {
        if (!usePercentBurstDamage || playerHealth == null)
        {
            return Mathf.Max(1, bulletDamage);
        }

        int shotsPerBurst = Mathf.Max(1, burstShots);
        int totalBurstDamage = Mathf.Max(1, Mathf.RoundToInt(playerHealth.MaxHealth * burstDamagePercent));
        int remainingShots = Mathf.Max(1, shotsPerBurst - shotsInCurrentBurst);
        int remainingDamage = Mathf.Max(1, totalBurstDamage - damageAppliedInCurrentBurst);

        return Mathf.Max(1, Mathf.CeilToInt(remainingDamage / (float)remainingShots));
    }

    private void PlayShotVisuals(Vector3 targetPoint)
    {
        Vector3 muzzlePosition = GetMuzzlePosition();
        Vector3 direction = targetPoint - muzzlePosition;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = transform.forward;
        }

        Quaternion shotRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        if (muzzleParticlePrefab != null)
        {
            GameObject muzzleParticles = Instantiate(muzzleParticlePrefab, muzzlePosition, shotRotation);
            Destroy(muzzleParticles, 2f);
        }

        if (bulletVisualPrefab != null)
        {
            GameObject bullet = Instantiate(bulletVisualPrefab, muzzlePosition, shotRotation);
            bullet.SetActive(true);
            StartCoroutine(MoveBulletVisual(bullet, targetPoint, direction.normalized));
        }
        else if (hitParticlePrefab != null)
        {
            SpawnHitParticles(targetPoint, shotRotation);
        }

        if (drawDebugRay)
        {
            Debug.DrawLine(muzzlePosition, targetPoint, Color.red, 0.2f);
        }
    }

    private IEnumerator MoveBulletVisual(GameObject bullet, Vector3 targetPoint, Vector3 fallbackDirection)
    {
        if (bullet == null)
        {
            yield break;
        }

        Rigidbody bulletRigidbody = bullet.GetComponent<Rigidbody>();
        if (bulletRigidbody != null)
        {
            bulletRigidbody.linearVelocity = fallbackDirection * bulletVisualSpeed;
            Destroy(bullet, bulletVisualLifetime);
            yield break;
        }

        float startTime = Time.time;
        while (bullet != null && Time.time - startTime < bulletVisualLifetime)
        {
            Vector3 toTarget = targetPoint - bullet.transform.position;
            float step = bulletVisualSpeed * Time.deltaTime;
            if (toTarget.magnitude <= step)
            {
                bullet.transform.position = targetPoint;
                SpawnHitParticles(targetPoint, bullet.transform.rotation);
                Destroy(bullet);
                yield break;
            }

            bullet.transform.position += toTarget.normalized * step;
            bullet.transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            yield return null;
        }

        if (bullet != null)
        {
            Destroy(bullet);
        }
    }

    private void SpawnHitParticles(Vector3 position, Quaternion rotation)
    {
        if (hitParticlePrefab == null)
        {
            return;
        }

        GameObject hitParticles = Instantiate(hitParticlePrefab, position, rotation);
        Destroy(hitParticles, 2f);
    }

    private Vector3 GetTargetPoint()
    {
        return player != null ? player.position + Vector3.up * aimHeight : transform.position + transform.forward * 10f;
    }

    private Vector3 GetMuzzlePosition()
    {
        if (muzzle != null)
        {
            return muzzle.position;
        }

        if (runtimeGun != null)
        {
            return runtimeGun.position + runtimeGun.forward * 0.85f;
        }

        return transform.position + Vector3.up * aimHeight + transform.forward * 0.85f;
    }

    private void RotateToward(Vector3 targetPoint, float turnSpeed)
    {
        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    }

    private void FaceTargetImmediately(Vector3 targetPoint)
    {
        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void RotateWithAgentVelocity()
    {
        if (agent == null)
        {
            return;
        }

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 0.05f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * patrolTurnSpeed);
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
    }

    private bool EnsureAgentOnNavMesh()
    {
        if (agent == null || !agent.enabled)
        {
            return false;
        }

        if (agent.isOnNavMesh)
        {
            return true;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 8f, agent.areaMask))
        {
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        return false;
    }

    private bool ResumeAgent()
    {
        if (!EnsureAgentOnNavMesh())
        {
            return false;
        }

        agent.isStopped = false;
        return true;
    }

    private bool SetAgentDestination(Vector3 destination)
    {
        if (!ResumeAgent())
        {
            return false;
        }

        return agent.SetDestination(destination);
    }

    private void SetDestinationToCurrentPatrolPoint()
    {
        if (agent == null || patrolPoints == null || patrolPoints.Length == 0)
        {
            return;
        }

        currentPatrolIndex = Mathf.Clamp(currentPatrolIndex, 0, patrolPoints.Length - 1);
        Transform point = patrolPoints[currentPatrolIndex];
        if (point != null)
        {
            SetAgentDestination(point.position);
        }
    }

    private void UpdateAnimatorSpeed()
    {
        if (animator == null || !floatParameters.Contains(speedParameter))
        {
            return;
        }

        float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        animator.SetFloat(speedParameter, speed, 0.12f, Time.deltaTime);
    }

    private void UpdateGunIKWeights()
    {
        if (animator == null || !animator.isHuman || currentState == EnemyState.Dead)
        {
            currentGripIKWeight = Mathf.MoveTowards(currentGripIKWeight, 0f, Time.deltaTime * Mathf.Max(0.1f, ikBlendSpeed));
            currentLookIKWeight = Mathf.MoveTowards(currentLookIKWeight, 0f, Time.deltaTime * Mathf.Max(0.1f, ikBlendSpeed));
            return;
        }

        float targetGripWeight = runtimeGun != null ? patrolGripIKWeight : 0f;
        float targetLookWeight = 0f;
        if (IsCombatState())
        {
            targetGripWeight = runtimeGun != null ? combatGripIKWeight : 0f;
            targetLookWeight = 1f;
        }

        float blendStep = Time.deltaTime * Mathf.Max(0.1f, ikBlendSpeed);
        currentGripIKWeight = Mathf.MoveTowards(currentGripIKWeight, targetGripWeight, blendStep);
        currentLookIKWeight = Mathf.MoveTowards(currentLookIKWeight, targetLookWeight, blendStep);
    }

    private void SetShooting(bool value)
    {
        if (animator != null && boolParameters.Contains(shootingParameter))
        {
            animator.SetBool(shootingParameter, value);
        }
    }

    private void SetTrigger(string parameter)
    {
        if (animator != null && triggerParameters.Contains(parameter))
        {
            animator.SetTrigger(parameter);
        }
    }

    private void ResetTrigger(string parameter)
    {
        if (animator != null && triggerParameters.Contains(parameter))
        {
            animator.ResetTrigger(parameter);
        }
    }

    private void PlayDeadAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = false;
        ResetTrigger(shootTrigger);
        ResetTrigger(reloadTrigger);
        ResetTrigger(hitTrigger);
        SetTrigger(deadTrigger);

        int deadStateHash = Animator.StringToHash(deadTrigger);
        int fullPathHash = Animator.StringToHash(animator.GetLayerName(0) + "." + deadTrigger);
        if (animator.HasState(0, fullPathHash))
        {
            animator.CrossFadeInFixedTime(fullPathHash, 0.05f, 0);
            animator.Update(0f);
        }
        else if (animator.HasState(0, deadStateHash))
        {
            animator.CrossFadeInFixedTime(deadStateHash, 0.05f, 0);
            animator.Update(0f);
        }
    }

    private IEnumerator SettleDeadBodyOnGround()
    {
        if (!snapToGroundOnDeath)
        {
            yield break;
        }

        float endTime = Time.time + Mathf.Max(0.05f, deathGroundSettleDuration);
        while (Time.time < endTime)
        {
            SnapDeadBodyToGround();
            yield return null;
        }

        SnapDeadBodyToGround();
    }

    private void SnapDeadBodyToGround()
    {
        if (!snapToGroundOnDeath)
        {
            return;
        }

        Vector3 position = transform.position;
        Vector3 groundPosition;
        if (TryGetDeathGroundPosition(position, out groundPosition))
        {
            transform.position = new Vector3(position.x, groundPosition.y + deathGroundOffset, position.z);
        }
    }

    private bool TryGetDeathGroundPosition(Vector3 position, out Vector3 groundPosition)
    {
        Vector3 rayOrigin = position + Vector3.up * Mathf.Max(0.1f, deathGroundRayHeight);
        float rayDistance = Mathf.Max(0.1f, deathGroundRayHeight + deathGroundRayDistance);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, rayDistance, deathGroundMask, QueryTriggerInteraction.Ignore);
        RaycastHit bestHit = new RaycastHit();
        bool foundHit = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                bestHit = hits[i];
                bestDistance = hits[i].distance;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            groundPosition = bestHit.point;
            return true;
        }

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(position, out navHit, Mathf.Max(0.1f, deathGroundRayDistance), NavMesh.AllAreas))
        {
            groundPosition = navHit.position;
            return true;
        }

        groundPosition = position;
        return false;
    }

    private void ResolvePlayerHealth()
    {
        playerHealth = null;
        if (player == null)
        {
            return;
        }

        playerHealth = player.GetComponent<vIHealthController>();
        if (playerHealth == null)
        {
            playerHealth = player.GetComponentInChildren<vIHealthController>();
        }
        if (playerHealth == null)
        {
            playerHealth = player.GetComponentInParent<vIHealthController>();
        }
    }

    private void FindBestTargetWithTag(string tagName, ref Transform bestTarget, ref vIHealthController bestHealth, ref float bestDistance)
    {
        if (string.IsNullOrEmpty(tagName))
        {
            return;
        }

        GameObject[] candidates;
        try
        {
            candidates = GameObject.FindGameObjectsWithTag(tagName);
        }
        catch (UnityException)
        {
            return;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject candidate = candidates[i];
            if (candidate == null || candidate.transform == transform || candidate.transform.IsChildOf(transform) || transform.IsChildOf(candidate.transform))
            {
                continue;
            }

            vIHealthController health = candidate.GetComponent<vIHealthController>();
            if (health == null)
            {
                health = candidate.GetComponentInChildren<vIHealthController>();
            }
            if (health == null)
            {
                health = candidate.GetComponentInParent<vIHealthController>();
            }
            if (health == null || health.isDead || health.currentHealth <= 0f)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(candidate.transform.position - transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = candidate.transform;
                bestHealth = health;
            }
        }
    }

    private void ResolveFallbackPlayerByHealth()
    {
        vHealthController[] healthControllers = FindObjectsOfType<vHealthController>();
        vIHealthController bestHealth = null;
        Transform bestTransform = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < healthControllers.Length; i++)
        {
            vHealthController candidate = healthControllers[i];
            if (candidate == null || candidate == selfHealth || candidate.isDead || candidate.transform.IsChildOf(transform))
            {
                continue;
            }

            if (!HasTargetTag(candidate.gameObject))
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(candidate.transform.position - transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestHealth = candidate;
                bestTransform = candidate.transform;
            }
        }

        playerHealth = bestHealth;
        player = bestTransform;
    }

    private bool HasTargetTag(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (targetTags == null || targetTags.Length == 0)
        {
            return candidate.CompareTag(playerTag);
        }

        for (int i = 0; i < targetTags.Length; i++)
        {
            string tagName = targetTags[i];
            if (!string.IsNullOrEmpty(tagName) && candidate.CompareTag(tagName))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureRootBulletHitbox()
    {
        Transform existing = transform.Find("EnemyBulletHitbox");
        if (existing != null)
        {
            return;
        }

        GameObject hitbox = new GameObject("EnemyBulletHitbox");
        hitbox.layer = GetBodyPartLayer();
        hitbox.transform.SetParent(transform, false);
        hitbox.transform.localPosition = Vector3.zero;
        hitbox.transform.localRotation = Quaternion.identity;
        hitbox.transform.localScale = Vector3.one;

        CapsuleCollider capsule = hitbox.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1f, 0f);
        capsule.height = 2f;
        capsule.radius = 0.45f;
        capsule.isTrigger = IsDemoScene1();

        CompleteEnemyDamageReceiver receiver = hitbox.AddComponent<CompleteEnemyDamageReceiver>();
        receiver.ownerHealth = selfHealth;
    }

    private void EnsureHumanoidHitboxes()
    {
        if (!createHumanoidHitboxes || animator == null || !animator.isHuman || selfHealth == null)
        {
            return;
        }

        EnsureBoneHitbox(HumanBodyBones.Head, "Head", headHitboxRadius, headHitboxRadius * 2.2f, Vector3.zero);
        if (!EnsureBoneHitbox(HumanBodyBones.Chest, "Chest", torsoHitboxRadius, torsoHitboxHeight, Vector3.zero))
        {
            EnsureBoneHitbox(HumanBodyBones.UpperChest, "UpperChest", torsoHitboxRadius, torsoHitboxHeight, Vector3.zero);
        }

        EnsureLimbHitbox(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, "LeftUpperArm", armHitboxRadius, armHitboxHeight);
        EnsureLimbHitbox(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, "LeftLowerArm", armHitboxRadius, armHitboxHeight);
        EnsureLimbHitbox(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, "RightUpperArm", armHitboxRadius, armHitboxHeight);
        EnsureLimbHitbox(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, "RightLowerArm", armHitboxRadius, armHitboxHeight);

        EnsureLimbHitbox(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, "LeftUpperLeg", legHitboxRadius, legHitboxHeight);
        EnsureLimbHitbox(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, "LeftLowerLeg", legHitboxRadius, legHitboxHeight);
        EnsureLimbHitbox(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, "RightUpperLeg", legHitboxRadius, legHitboxHeight);
        EnsureLimbHitbox(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, "RightLowerLeg", legHitboxRadius, legHitboxHeight);
    }

    private bool EnsureLimbHitbox(HumanBodyBones startBone, HumanBodyBones endBone, string suffix, float radius, float fallbackHeight)
    {
        Transform startTransform = animator.GetBoneTransform(startBone);
        if (startTransform == null)
        {
            return false;
        }

        string hitboxName = "EnemyHitbox_" + suffix;
        Transform existing = startTransform.Find(hitboxName);
        GameObject hitbox = existing != null ? existing.gameObject : new GameObject(hitboxName);
        hitbox.layer = GetBodyPartLayer();
        if (existing == null)
        {
            hitbox.transform.SetParent(startTransform, false);
        }

        CapsuleCollider capsule = hitbox.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = hitbox.AddComponent<CapsuleCollider>();
        }

        capsule.direction = 1;
        capsule.center = Vector3.zero;
        capsule.radius = Mathf.Max(0.01f, radius);
        capsule.isTrigger = IsDemoScene1();

        Transform endTransform = animator.GetBoneTransform(endBone);
        Vector3 direction = endTransform != null ? endTransform.position - startTransform.position : Vector3.zero;
        if (direction.sqrMagnitude > 0.0001f)
        {
            float length = direction.magnitude;
            hitbox.transform.position = startTransform.position + direction * 0.5f;
            hitbox.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            capsule.height = Mathf.Max(length * 1.15f, capsule.radius * 2f);
        }
        else
        {
            hitbox.transform.localPosition = Vector3.zero;
            hitbox.transform.localRotation = Quaternion.identity;
            capsule.height = Mathf.Max(fallbackHeight, capsule.radius * 2f);
        }

        hitbox.transform.localScale = Vector3.one;
        CompleteEnemyDamageReceiver receiver = hitbox.GetComponent<CompleteEnemyDamageReceiver>();
        if (receiver == null)
        {
            receiver = hitbox.AddComponent<CompleteEnemyDamageReceiver>();
        }

        receiver.ownerHealth = selfHealth;
        return true;
    }

    private bool EnsureBoneHitbox(HumanBodyBones bone, string suffix, float radius, float height, Vector3 center)
    {
        Transform boneTransform = animator.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            return false;
        }

        string hitboxName = "EnemyHitbox_" + suffix;
        Transform existing = boneTransform.Find(hitboxName);
        if (existing != null)
        {
            CompleteEnemyDamageReceiver existingReceiver = existing.GetComponent<CompleteEnemyDamageReceiver>();
            if (existingReceiver == null)
            {
                existingReceiver = existing.gameObject.AddComponent<CompleteEnemyDamageReceiver>();
            }

            existingReceiver.ownerHealth = selfHealth;
            existing.gameObject.layer = GetBodyPartLayer();
            return true;
        }

        GameObject hitbox = new GameObject(hitboxName);
        hitbox.layer = GetBodyPartLayer();
        hitbox.transform.SetParent(boneTransform, false);
        hitbox.transform.localPosition = Vector3.zero;
        hitbox.transform.localRotation = Quaternion.identity;
        hitbox.transform.localScale = Vector3.one;

        CapsuleCollider capsule = hitbox.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.center = center;
        capsule.radius = Mathf.Max(0.01f, radius);
        capsule.height = Mathf.Max(height, capsule.radius * 2f);
        capsule.isTrigger = IsDemoScene1();

        CompleteEnemyDamageReceiver receiver = hitbox.AddComponent<CompleteEnemyDamageReceiver>();
        receiver.ownerHealth = selfHealth;
        return true;
    }

    private void EnsureDamageReceiversOnColliders()
    {
        vHealthController health = selfHealth != null ? selfHealth : GetComponent<vHealthController>();
        if (health == null)
        {
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];
            if (targetCollider == null)
            {
                continue;
            }

            if (IsDemoScene1() && targetCollider.transform != transform)
            {
                targetCollider.isTrigger = true;
            }

            if (targetCollider.GetComponent<vIDamageReceiver>() != null && targetCollider.GetComponent<vIAttackReceiver>() != null)
            {
                continue;
            }

            CompleteEnemyDamageReceiver receiver = targetCollider.gameObject.AddComponent<CompleteEnemyDamageReceiver>();
            receiver.ownerHealth = health;
        }
    }

    private int GetBodyPartLayer()
    {
        return bodyPartLayer >= 0 && bodyPartLayer <= 31 ? bodyPartLayer : gameObject.layer;
    }

    private bool IsDemoScene1()
    {
        return SceneManager.GetActiveScene().name == "Demo Scene 1";
    }

    private void StartHitStagger(vDamage damage)
    {
        if (!staggerOnHit || currentState == EnemyState.Dead || selfHealth == null || selfHealth.isDead)
        {
            return;
        }

        staggerEndTime = Time.time + hitStaggerDuration;
        SetShooting(false);
        StopAgent();
        if (staticEnemy)
        {
            return;
        }

        Vector3 knockDirection = Vector3.zero;
        if (damage != null && damage.sender != null)
        {
            knockDirection = transform.position - damage.sender.position;
            knockDirection.y = 0f;
        }

        if (knockDirection.sqrMagnitude < 0.01f)
        {
            knockDirection = -transform.forward;
        }

        knockDirection.Normalize();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Move(knockDirection * hitKnockbackDistance);
        }
        else
        {
            transform.position += knockDirection * hitKnockbackDistance;
        }
    }

    private void UpdateHitStagger()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (Time.time >= staggerEndTime)
        {
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, visualBaseLocalRotation, Time.deltaTime * 10f);
            return;
        }

        float remaining = Mathf.Clamp01((staggerEndTime - Time.time) / Mathf.Max(0.001f, hitStaggerDuration));
        float weight = Mathf.Sin(remaining * Mathf.PI);
        Quaternion fallRotation = visualBaseLocalRotation * Quaternion.Euler(-hitLeanAngle * weight, 0f, 0f);
        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, fallRotation, Time.deltaTime * 18f);
    }

    private void CreateTemporaryHealthBar()
    {
        if (!createTemporaryHealthBar || selfHealth == null || healthSlider != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("TemporaryEnemyHealthBar");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = healthBarOffset;
        healthCanvas = canvasObject.AddComponent<Canvas>();
        healthCanvas.renderMode = RenderMode.WorldSpace;
        healthCanvas.sortingOrder = 20;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = healthBarSize;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(backgroundObject.transform, false);
        Image fill = fillObject.AddComponent<Image>();
        fill.color = healthBarFillColor;
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(0.03f, 0.03f);
        fillRect.offsetMax = new Vector2(-0.03f, -0.03f);

        healthSlider = canvasObject.AddComponent<Slider>();
        healthSlider.transition = Selectable.Transition.None;
        healthSlider.minValue = 0f;
        healthSlider.maxValue = selfHealth.MaxHealth;
        healthSlider.value = selfHealth.currentHealth;
        healthSlider.targetGraphic = fill;
        healthSlider.fillRect = fillRect;

        canvasObject.transform.localScale = Vector3.one * 0.01f;
        healthBarCamera = Camera.main;
    }

    private void UpdateTemporaryHealthBar()
    {
        if (healthSlider == null || selfHealth == null)
        {
            return;
        }

        healthSlider.maxValue = Mathf.Max(1, selfHealth.MaxHealth);
        healthSlider.value = selfHealth.currentHealth;

        if (healthCanvas != null)
        {
            if (healthBarCamera == null)
            {
                healthBarCamera = Camera.main;
            }

            if (healthBarCamera != null)
            {
                healthCanvas.transform.rotation = Quaternion.LookRotation(healthCanvas.transform.position - healthBarCamera.transform.position, Vector3.up);
            }
        }
    }

    private void DrawGuiHealthBar()
    {
        if (!drawGuiHealthBar || selfHealth == null || selfHealth.isDead)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        Vector3 worldPosition = transform.position + healthBarOffset;
        Vector3 screenPosition = targetCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
        {
            return;
        }

        float healthPercent = Mathf.Clamp01(selfHealth.currentHealth / Mathf.Max(1f, selfHealth.MaxHealth));
        Rect background = new Rect(
            screenPosition.x - guiHealthBarSize.x * 0.5f,
            Screen.height - screenPosition.y,
            guiHealthBarSize.x,
            guiHealthBarSize.y);
        Rect fill = new Rect(background.x + 1f, background.y + 1f, (background.width - 2f) * healthPercent, background.height - 2f);

        Color previousColor = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = healthBarFillColor;
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(background.x, background.y - 18f, background.width, 18f), Mathf.CeilToInt(selfHealth.currentHealth).ToString());
        GUI.color = previousColor;
    }

    private static Transform FindOrCreateChild(Transform parent, string childName, Vector3 localPosition)
    {
        Transform child = FindChild(parent, childName);
        if (child != null)
        {
            return child;
        }

        GameObject marker = new GameObject(childName);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = Vector3.one;
        return marker.transform;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return children[i];
            }
        }

        return null;
    }
}

public class CompleteEnemyDamageReceiver : MonoBehaviour, vIDamageReceiver, vIAttackReceiver
{
    public vHealthController ownerHealth;

    private OnReceiveDamage startReceiveDamage = new OnReceiveDamage();
    private OnReceiveDamage receiveDamage = new OnReceiveDamage();

    public OnReceiveDamage onStartReceiveDamage { get { return startReceiveDamage; } }
    public OnReceiveDamage onReceiveDamage { get { return receiveDamage; } }

    public void OnReceiveAttack(vDamage damage, vIMeleeFighter attacker)
    {
        TakeDamage(damage);
    }

    public void TakeDamage(vDamage damage)
    {
        if (ownerHealth == null)
        {
            ownerHealth = GetComponentInParent<vHealthController>();
        }

        if (ownerHealth == null)
        {
            return;
        }

        startReceiveDamage.Invoke(damage);
        damage.receiver = ownerHealth.transform;
        ownerHealth.TakeDamage(damage);
        receiveDamage.Invoke(damage);
    }
}
