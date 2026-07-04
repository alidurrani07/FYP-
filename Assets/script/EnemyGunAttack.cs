using System.Collections;
using Invector;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EnemyGunAttack : MonoBehaviour
{
    [Header("Target")]
    public string playerTag = "Player";
    public string[] targetTags;
    public float detectionRange = 30f;
    public float closeRange = 4f;

    [Header("Gun Damage")]
    public int bulletDamage = 8;
    public float fireInterval = 0.8f;
    public float aimHeight = 1.35f;
    public bool requireLineOfSight = false;
    public LayerMask lineOfSightMask = ~0;
    public bool useBurstCombatCycle;
    public float shootBurstDuration = 5f;
    public float restDuration = 3f;
    public float restRepositionRadius = 8f;
    public float restRepositionInterval = 1.2f;
    public float minimumPlayerDistance = 10f;
    public float preferredPlayerDistance = 15f;

    [Header("Visuals")]
    public Transform muzzle;
    public Transform existingGunVisual;
    public GameObject gunPrefab;
    public bool createFallbackGun = true;
    public float gunVisualScale = 1.25f;
    public Vector3 gunShoulderOffset = new Vector3(0.22f, -0.08f, 0.32f);
    public bool useAimIK = true;
    public bool forceHandPoseIfNoIKPass = true;
    public float recoilDistance = 0.12f;
    public float recoilDuration = 0.12f;
    public float muzzleFlashDuration = 0.08f;
    public bool hideGunWhileMoving = true;
    public float hideGunMoveSpeed = 0.25f;
    public GameObject bulletVisualPrefab;
    public float bulletVisualSpeed = 60f;
    public float bulletVisualLifetime = 2f;
    public bool applyDamageOnBulletImpact;
    public bool dodgeableProjectiles = true;
    public float projectileCollisionRadius = 0.22f;
    public LayerMask projectileHitMask = ~0;
    public ParticleSystem[] muzzleShotParticles;
    public GameObject muzzleParticlePrefab;
    public GameObject hitParticlePrefab;
    public bool drawDebugRay = true;

    private Transform player;
    private vHealthController playerHealth;
    private vHealthController selfHealth;
    private Animator animator;
    private NavMeshAgent agent;
    private Transform visualRoot;
    private Transform chest;
    private Transform rightHand;
    private Transform leftHand;
    private Transform runtimeGun;
    private bool usingExistingGunVisual;
    private Vector3 existingGunLocalScale = Vector3.one;
    private Transform rightGrip;
    private Transform leftGrip;
    private Transform muzzleFlash;
    private Material runtimeGunMaterial;
    private Material muzzleFlashMaterial;
    private float nextFireTime;
    private float lastShotTime = -999f;
    private float combatPhaseEndTime;
    private float nextRestRepositionTime;
    private float aimWeight;
    private bool playerInRange;
    private bool restingFromShooting;
    private int lastIKFrame = -1;
    private int nextShotId;
    private int lastAppliedShotId;

    private void Awake()
    {
        animator = ResolveAnimator();
        agent = GetComponent<NavMeshAgent>();
        selfHealth = GetComponent<vHealthController>();
        CacheBones();
    }

    private void Start()
    {
        FindPlayer();
        combatPhaseEndTime = Time.time + Mathf.Max(0.1f, shootBurstDuration);

        if (runtimeGun == null)
        {
            AttachGunVisual();
        }
    }

    private void Update()
    {
        if (selfHealth != null && selfHealth.isDead)
        {
            return;
        }

        if (player == null || playerHealth == null || playerHealth.isDead || playerHealth.currentHealth <= 0f)
        {
            FindPlayer();
            if (player == null || playerHealth == null)
            {
                return;
            }
        }

        Vector3 targetPoint = GetTargetPoint();
        float distance = Vector3.Distance(transform.position, player.position);
        playerInRange = distance <= detectionRange || distance <= closeRange;
        aimWeight = Mathf.MoveTowards(aimWeight, playerInRange ? 1f : 0f, Time.deltaTime * 5f);

        if (!playerInRange)
        {
            SetAnimatorBool("isShooting", false);
            return;
        }

        RotateToward(targetPoint);
        MaintainMinimumPlayerDistance(distance);

        UpdateCombatCycle();
        if (restingFromShooting)
        {
            SetAnimatorBool("isShooting", false);
            MoveDuringRest();
            return;
        }

        if (Time.time >= nextFireTime && CanShootTarget(targetPoint))
        {
            Shoot(targetPoint);
        }
    }

    private void MaintainMinimumPlayerDistance(float currentDistance)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || player == null || currentDistance >= minimumPlayerDistance)
        {
            return;
        }

        SetAnimatorBool("isShooting", false);
        Vector3 awayFromPlayer = transform.position - player.position;
        awayFromPlayer.y = 0f;
        if (awayFromPlayer.sqrMagnitude < 0.01f)
        {
            awayFromPlayer = -transform.forward;
        }

        Vector3 desired = player.position + awayFromPlayer.normalized * Mathf.Max(minimumPlayerDistance, preferredPlayerDistance);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(desired, out hit, Mathf.Max(4f, preferredPlayerDistance), NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    private void LateUpdate()
    {
        UpdateGunVisibility();
        UpdateGunVisual();
        PoseHandsDirectlyIfIKPassIsDisabled();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!useAimIK || animator == null || !animator.isHuman || aimWeight <= 0.01f || player == null)
        {
            return;
        }

        Vector3 targetPoint = GetTargetPoint();
        lastIKFrame = Time.frameCount;
        animator.SetLookAtWeight(aimWeight, 0.25f, 0.85f, 0.9f, 0.5f);
        animator.SetLookAtPosition(targetPoint);

        if (rightGrip != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, aimWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, aimWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightGrip.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightGrip.rotation);
        }

        if (leftGrip != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, aimWeight * 0.9f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, aimWeight * 0.9f);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftGrip.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftGrip.rotation);
        }
    }

    private void CacheBones()
    {
        chest = null;
        rightHand = null;
        leftHand = null;

        if (animator == null || !animator.isHuman)
        {
            return;
        }

        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null)
        {
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        }

        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
    }

    private Animator ResolveAnimator()
    {
        if (visualRoot != null)
        {
            Animator visualAnimator = visualRoot.GetComponentInChildren<Animator>(true);
            if (visualAnimator != null)
            {
                return visualAnimator;
            }
        }

        return GetComponent<Animator>();
    }

    private void FindPlayer()
    {
        Transform bestTarget = null;
        vHealthController bestHealth = null;
        float bestDistance = float.MaxValue;

        if (targetTags != null && targetTags.Length > 0)
        {
            for (int i = 0; i < targetTags.Length; i++)
            {
                FindBestTarget(targetTags[i], ref bestTarget, ref bestHealth, ref bestDistance);
            }
        }
        else
        {
            FindBestTarget(playerTag, ref bestTarget, ref bestHealth, ref bestDistance);
        }

        if (bestTarget == null || bestHealth == null)
        {
            player = null;
            playerHealth = null;
            return;
        }

        player = bestTarget;
        playerHealth = bestHealth;
    }

    private void FindBestTarget(string tagName, ref Transform bestTarget, ref vHealthController bestHealth, ref float bestDistance)
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

            vHealthController health = candidate.GetComponent<vHealthController>();
            if (health == null)
            {
                health = candidate.GetComponentInChildren<vHealthController>();
            }
            if (health == null)
            {
                health = candidate.GetComponentInParent<vHealthController>();
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

    private Vector3 GetTargetPoint()
    {
        return player.position + Vector3.up * aimHeight;
    }

    private void RotateToward(Vector3 targetPoint)
    {
        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
    }

    private bool CanShootTarget(Vector3 targetPoint)
    {
        if (!requireLineOfSight)
        {
            return true;
        }

        Vector3 origin = GetMuzzlePosition();
        Vector3 direction = targetPoint - origin;
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

    private void UpdateCombatCycle()
    {
        if (!useBurstCombatCycle)
        {
            restingFromShooting = false;
            return;
        }

        if (Time.time < combatPhaseEndTime)
        {
            return;
        }

        restingFromShooting = !restingFromShooting;
        float duration = restingFromShooting ? restDuration : shootBurstDuration;
        combatPhaseEndTime = Time.time + Mathf.Max(0.1f, duration);
        nextRestRepositionTime = 0f;
        SetAnimatorBool("isShooting", false);
    }

    private void MoveDuringRest()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || player == null || Time.time < nextRestRepositionTime)
        {
            return;
        }

        nextRestRepositionTime = Time.time + Mathf.Max(0.25f, restRepositionInterval);

        Vector3 awayFromPlayer = transform.position - player.position;
        awayFromPlayer.y = 0f;
        if (awayFromPlayer.sqrMagnitude < 0.01f)
        {
            awayFromPlayer = -transform.forward;
        }

        Vector3 side = Vector3.Cross(Vector3.up, awayFromPlayer.normalized);
        if (Random.value < 0.5f)
        {
            side = -side;
        }

        Vector3 desired = transform.position + side * Random.Range(restRepositionRadius * 0.45f, restRepositionRadius);
        desired += awayFromPlayer.normalized * Random.Range(2f, restRepositionRadius * 0.55f);
        if (Vector3.Distance(desired, player.position) < minimumPlayerDistance)
        {
            desired = player.position + awayFromPlayer.normalized * Mathf.Max(minimumPlayerDistance, preferredPlayerDistance);
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(desired, out hit, restRepositionRadius, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    private void Shoot(Vector3 targetPoint)
    {
        nextFireTime = Time.time + fireInterval;
        lastShotTime = Time.time;
        SetAnimatorBool("isShooting", true);
        SetAnimatorTrigger("Shoot");

        vDamage damage = new vDamage
        {
            damageValue = bulletDamage,
            sender = transform,
            receiver = playerHealth.transform,
            hitPosition = targetPoint,
            hitReaction = false,
            activeRagdoll = false,
            senselessTime = 0f,
            recoil_id = -1,
            reaction_id = -1,
            ignoreDefense = true,
            force = (targetPoint - GetMuzzlePosition()).normalized * bulletDamage
        };

        bool waitForBulletImpact = applyDamageOnBulletImpact && bulletVisualPrefab != null;
        int shotId = ++nextShotId;
        if (!waitForBulletImpact)
        {
            ApplyDamageToPlayer(damage, targetPoint, shotId);
        }

        Vector3 muzzlePosition = GetMuzzlePosition();
        Vector3 shotDirection = targetPoint - muzzlePosition;
        if (shotDirection.sqrMagnitude < 0.01f)
        {
            shotDirection = transform.forward;
        }

        PlayShotVisuals(targetPoint, waitForBulletImpact ? damage : null, shotId);

        if (waitForBulletImpact && IsFinalSceneBossShot())
        {
            StartCoroutine(EnsureShotDamagesPlayerAfterTravel(new vDamage(damage), muzzlePosition, shotDirection.normalized, targetPoint, shotId));
        }

        if (drawDebugRay)
        {
            Debug.DrawLine(GetMuzzlePosition(), targetPoint, Color.red, 0.15f);
        }
    }

    private void ApplyDamageToPlayer(vDamage damage)
    {
        ApplyDamageToPlayer(damage, GetTargetPoint(), 0);
    }

    private void ApplyDamageToPlayer(vDamage damage, Vector3 hitPosition, int shotId = 0)
    {
        if (damage == null)
        {
            return;
        }

        if (shotId > 0 && lastAppliedShotId == shotId)
        {
            return;
        }

        if (playerHealth == null || playerHealth.isDead || playerHealth.currentHealth <= 0f)
        {
            FindPlayer();
        }

        if (playerHealth == null || playerHealth.isDead || playerHealth.currentHealth <= 0f)
        {
            return;
        }

        damage.receiver = playerHealth.transform;
        damage.hitPosition = hitPosition;
        FinalScenePlayerDamageGate finalSceneGate = ResolveFinalSceneDamageGate();
        if (finalSceneGate != null && finalSceneGate.ApplyApprovedBossDamage(damage, hitPosition))
        {
            if (shotId > 0)
            {
                lastAppliedShotId = shotId;
            }
            return;
        }

        if (IsFinalSceneBossShot() && ApplyDirectFinalSceneDamage(damage, shotId))
        {
            return;
        }

        playerHealth.TakeDamage(damage);
        if (shotId > 0)
        {
            lastAppliedShotId = shotId;
        }
    }

    private FinalScenePlayerDamageGate ResolveFinalSceneDamageGate()
    {
        if (playerHealth == null)
        {
            return null;
        }

        FinalScenePlayerDamageGate gate = playerHealth.GetComponent<FinalScenePlayerDamageGate>();
        if (gate != null)
        {
            return gate;
        }

        gate = playerHealth.GetComponentInParent<FinalScenePlayerDamageGate>();
        if (gate != null)
        {
            return gate;
        }

        return playerHealth.GetComponentInChildren<FinalScenePlayerDamageGate>(true);
    }

    private bool ApplyDirectFinalSceneDamage(vDamage damage, int shotId)
    {
        if (playerHealth == null || damage == null || damage.damageValue <= 0f)
        {
            return false;
        }

        float nextHealth = Mathf.Max(0f, playerHealth.currentHealth - damage.damageValue);
        playerHealth.ChangeHealth(Mathf.RoundToInt(nextHealth));
        if (shotId > 0)
        {
            lastAppliedShotId = shotId;
        }

        return true;
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

        return GetGunBasePosition() + transform.forward * 0.85f;
    }

    public void ApplyVisualSetup(GameObject riflePrefab, GameObject bulletPrefab, ParticleSystem[] shotParticles, GameObject muzzleParticles, GameObject hitParticles)
    {
        if (riflePrefab != null)
        {
            gunPrefab = riflePrefab;
            createFallbackGun = false;
        }

        if (bulletPrefab != null)
        {
            bulletVisualPrefab = bulletPrefab;
        }

        if (shotParticles != null && shotParticles.Length > 0)
        {
            muzzleShotParticles = shotParticles;
        }

        if (muzzleParticles != null)
        {
            muzzleParticlePrefab = muzzleParticles;
        }

        if (hitParticles != null)
        {
            hitParticlePrefab = hitParticles;
        }
    }

    public void UseExistingGunVisual(Transform gunVisual, GameObject bulletTemplate = null)
    {
        if (gunVisual != null)
        {
            existingGunVisual = gunVisual;
            createFallbackGun = false;
            if (runtimeGun == null)
            {
                AttachGunVisual();
            }
        }

        if (bulletTemplate != null)
        {
            bulletVisualPrefab = bulletTemplate;
        }
    }

    public void UseVisualRoot(Transform newVisualRoot)
    {
        if (newVisualRoot == null)
        {
            return;
        }

        visualRoot = newVisualRoot;
        animator = ResolveAnimator();
        CacheBones();
    }

    private void AttachGunVisual()
    {
        GameObject gun = null;
        usingExistingGunVisual = false;

        if (existingGunVisual != null)
        {
            gun = existingGunVisual.gameObject;
            usingExistingGunVisual = true;
            existingGunLocalScale = existingGunVisual.localScale;
        }
        else if (gunPrefab != null)
        {
            gun = Instantiate(gunPrefab, transform);
        }

        if (gun == null && createFallbackGun)
        {
            gun = BuildFallbackGun(transform);
        }

        if (gun == null)
        {
            return;
        }

        if (!usingExistingGunVisual)
        {
            gun.name = "EnemyRuntimeGun";
        }

        runtimeGun = gun.transform;
        if (!usingExistingGunVisual)
        {
            runtimeGun.SetParent(transform, true);
        }

        if (rightGrip == null)
        {
            rightGrip = CreateMarker(runtimeGun, "RightGrip", new Vector3(0.045f, -0.085f, 0.02f));
        }

        if (leftGrip == null)
        {
            leftGrip = CreateMarker(runtimeGun, "LeftGrip", new Vector3(-0.025f, -0.075f, 0.43f));
        }

        if (muzzle == null)
        {
            muzzle = CreateMarker(runtimeGun, "Muzzle", new Vector3(0f, 0f, 0.93f));
        }

        if (muzzleFlash == null)
        {
            muzzleFlash = CreateMuzzleFlash(muzzle);
        }

        UpdateGunVisual();
    }

    private void UpdateGunVisual()
    {
        if (runtimeGun == null || !runtimeGun.gameObject.activeSelf)
        {
            return;
        }

        Vector3 basePosition = GetGunBasePosition();
        Vector3 targetPoint = player != null ? GetTargetPoint() : basePosition + transform.forward * 10f;
        Vector3 aimDirection = targetPoint - basePosition;

        if (aimDirection.sqrMagnitude < 0.01f)
        {
            aimDirection = transform.forward;
        }

        Vector3 aimForward = aimDirection.normalized;
        float recoilWeight = Mathf.Clamp01(1f - ((Time.time - lastShotTime) / Mathf.Max(0.001f, recoilDuration)));
        if (recoilWeight > 0f)
        {
            basePosition -= aimForward * recoilDistance * recoilWeight;
        }

        Quaternion targetRotation = Quaternion.LookRotation(aimForward, Vector3.up);
        runtimeGun.SetPositionAndRotation(basePosition, targetRotation);
        runtimeGun.localScale = usingExistingGunVisual ? existingGunLocalScale : Vector3.one * gunVisualScale;

        if (muzzleFlash != null)
        {
            muzzleFlash.gameObject.SetActive(Time.time - lastShotTime <= muzzleFlashDuration);
        }
    }

    private void UpdateGunVisibility()
    {
        if (runtimeGun == null)
        {
            return;
        }

        bool shouldHide = hideGunWhileMoving && agent != null && agent.enabled && agent.velocity.magnitude > hideGunMoveSpeed;
        if (runtimeGun.gameObject.activeSelf == !shouldHide)
        {
            return;
        }

        runtimeGun.gameObject.SetActive(!shouldHide);
        if (muzzleFlash != null)
        {
            muzzleFlash.gameObject.SetActive(!shouldHide && Time.time - lastShotTime <= muzzleFlashDuration);
        }
    }

    private void PoseHandsDirectlyIfIKPassIsDisabled()
    {
        if (!forceHandPoseIfNoIKPass || !useAimIK || animator == null || !animator.isHuman || lastIKFrame == Time.frameCount || aimWeight <= 0.01f)
        {
            return;
        }

        if (rightHand != null && rightGrip != null)
        {
            rightHand.SetPositionAndRotation(
                Vector3.Lerp(rightHand.position, rightGrip.position, aimWeight),
                Quaternion.Slerp(rightHand.rotation, rightGrip.rotation, aimWeight));
        }

        if (leftHand != null && leftGrip != null)
        {
            float leftWeight = aimWeight * 0.9f;
            leftHand.SetPositionAndRotation(
                Vector3.Lerp(leftHand.position, leftGrip.position, leftWeight),
                Quaternion.Slerp(leftHand.rotation, leftGrip.rotation, leftWeight));
        }
    }

    private Vector3 GetGunBasePosition()
    {
        Transform anchor = chest != null ? chest : transform;
        Vector3 basePosition = anchor.position;
        basePosition += transform.right * gunShoulderOffset.x;
        basePosition += Vector3.up * gunShoulderOffset.y;
        basePosition += transform.forward * gunShoulderOffset.z;

        if (rightHand != null && aimWeight < 0.2f)
        {
            basePosition = Vector3.Lerp(basePosition, rightHand.position + transform.forward * 0.25f, 1f - aimWeight * 5f);
        }

        return basePosition;
    }

    private void PlayShotVisuals(Vector3 targetPoint, vDamage pendingDamage = null, int shotId = 0)
    {
        Vector3 muzzlePosition = GetMuzzlePosition();
        Vector3 shotDirection = targetPoint - muzzlePosition;
        if (shotDirection.sqrMagnitude < 0.01f)
        {
            shotDirection = transform.forward;
        }

        Quaternion shotRotation = Quaternion.LookRotation(shotDirection.normalized, Vector3.up);

        if (muzzleShotParticles != null)
        {
            for (int i = 0; i < muzzleShotParticles.Length; i++)
            {
                if (muzzleShotParticles[i] != null)
                {
                    muzzleShotParticles[i].Play(true);
                }
            }
        }

        if (muzzleParticlePrefab != null)
        {
            GameObject muzzleParticles = Instantiate(muzzleParticlePrefab, muzzlePosition, shotRotation);
            PlayParticleObject(muzzleParticles);
            Destroy(muzzleParticles, 2f);
        }

        if (bulletVisualPrefab != null)
        {
            GameObject bullet = Instantiate(bulletVisualPrefab, muzzlePosition, shotRotation);
            bullet.SetActive(true);
            StartCoroutine(MoveBulletVisual(bullet, targetPoint, shotDirection.normalized, pendingDamage, shotId));
        }
        else if (hitParticlePrefab != null)
        {
            SpawnHitParticles(targetPoint, shotRotation);
        }
    }

    private IEnumerator MoveBulletVisual(GameObject bullet, Vector3 targetPoint, Vector3 fallbackDirection, vDamage pendingDamage, int shotId)
    {
        if (bullet == null)
        {
            yield break;
        }

        Vector3 direction = fallbackDirection.sqrMagnitude > 0.01f ? fallbackDirection.normalized : bullet.transform.forward;
        Rigidbody bulletRigidbody = bullet.GetComponent<Rigidbody>();
        if (!dodgeableProjectiles && bulletRigidbody != null && pendingDamage == null)
        {
            bulletRigidbody.linearVelocity = direction * bulletVisualSpeed;
            Destroy(bullet, bulletVisualLifetime);
            yield break;
        }

        float startTime = Time.time;
        while (bullet != null && Time.time - startTime < bulletVisualLifetime)
        {
            float step = bulletVisualSpeed * Time.deltaTime;

            if (TryMoveProjectile(bullet, direction, step, pendingDamage, shotId))
            {
                yield break;
            }

            bullet.transform.position += direction * step;
            bullet.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            yield return null;
        }

        if (bullet != null)
        {
            Destroy(bullet);
        }
    }

    private bool TryMoveProjectile(GameObject bullet, Vector3 direction, float distance, vDamage pendingDamage, int shotId)
    {
        Vector3 origin = bullet.transform.position;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, projectileCollisionRadius),
            direction,
            Mathf.Max(0.01f, distance),
            projectileHitMask,
            QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
        {
            return TryDamagePlayerByProjectileProximity(origin, direction, distance, pendingDamage, bullet, shotId);
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider hitCollider = hit.collider;
            if (!IsValidProjectileHit(hitCollider, bullet.transform))
            {
                continue;
            }

            Vector3 hitPoint = hit.point == Vector3.zero ? origin + direction * hit.distance : hit.point;
            bullet.transform.position = hitPoint;
            SpawnHitParticles(hitPoint, bullet.transform.rotation);

            if (IsPlayerCollider(hitCollider))
            {
                ApplyDamageToPlayer(pendingDamage, hitPoint, shotId);
            }

            Destroy(bullet);
            return true;
        }

        return TryDamagePlayerByProjectileProximity(origin, direction, distance, pendingDamage, bullet, shotId);
    }

    private bool TryDamagePlayerByProjectileProximity(Vector3 origin, Vector3 direction, float distance, vDamage pendingDamage, GameObject bullet, int shotId)
    {
        if (pendingDamage == null || player == null || bullet == null)
        {
            return false;
        }

        Vector3 targetPoint = GetTargetPoint();
        float alongSegment = Mathf.Clamp(Vector3.Dot(targetPoint - origin, direction), 0f, Mathf.Max(0.01f, distance));
        Vector3 closestPoint = origin + direction * alongSegment;
        float hitRadius = Mathf.Max(0.9f, projectileCollisionRadius + 0.55f);

        if ((targetPoint - closestPoint).sqrMagnitude > hitRadius * hitRadius)
        {
            return false;
        }

        bullet.transform.position = closestPoint;
        SpawnHitParticles(closestPoint, bullet.transform.rotation);
        ApplyDamageToPlayer(pendingDamage, closestPoint, shotId);
        Destroy(bullet);
        return true;
    }

    private IEnumerator EnsureShotDamagesPlayerAfterTravel(vDamage damage, Vector3 origin, Vector3 direction, Vector3 initialTargetPoint, int shotId)
    {
        if (damage == null || shotId <= 0)
        {
            yield break;
        }

        float travelDistance = Vector3.Distance(origin, initialTargetPoint);
        float travelDelay = Mathf.Clamp(travelDistance / Mathf.Max(1f, bulletVisualSpeed), 0.08f, 0.65f);
        yield return new WaitForSeconds(travelDelay);

        if (lastAppliedShotId == shotId)
        {
            yield break;
        }

        if (player == null || playerHealth == null || playerHealth.isDead || playerHealth.currentHealth <= 0f)
        {
            FindPlayer();
        }

        if (player == null || playerHealth == null || playerHealth.isDead || playerHealth.currentHealth <= 0f)
        {
            yield break;
        }

        Vector3 hitPoint = GetTargetPoint();
        float maxTravel = Mathf.Max(travelDistance, bulletVisualSpeed * bulletVisualLifetime);
        float distanceFromShot = DistanceToShotPath(hitPoint, origin, direction, maxTravel);
        bool playerStillThreatened = distanceFromShot <= Mathf.Max(2.5f, projectileCollisionRadius + 2.1f);
        bool playerInCombatRange = Vector3.Distance(transform.position, player.position) <= detectionRange;

        if (!playerStillThreatened && !playerInCombatRange)
        {
            yield break;
        }

        SpawnHitParticles(hitPoint, Quaternion.LookRotation(direction.sqrMagnitude > 0.01f ? direction : transform.forward, Vector3.up));
        ApplyDamageToPlayer(damage, hitPoint, shotId);
    }

    private static float DistanceToShotPath(Vector3 point, Vector3 origin, Vector3 direction, float maxDistance)
    {
        Vector3 safeDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
        float alongPath = Mathf.Clamp(Vector3.Dot(point - origin, safeDirection), 0f, Mathf.Max(0.01f, maxDistance));
        Vector3 closestPoint = origin + safeDirection * alongPath;
        return Vector3.Distance(point, closestPoint);
    }

    private bool IsValidProjectileHit(Collider hitCollider, Transform bulletTransform)
    {
        if (hitCollider == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        if (hitTransform == null || hitTransform == bulletTransform || hitTransform.IsChildOf(bulletTransform))
        {
            return false;
        }

        if (hitTransform == transform || hitTransform.IsChildOf(transform))
        {
            return false;
        }

        return true;
    }

    private bool IsPlayerCollider(Collider hitCollider)
    {
        if (hitCollider == null || player == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        return hitTransform == player || hitTransform.IsChildOf(player) || player.IsChildOf(hitTransform);
    }

    private bool IsFinalSceneBossShot()
    {
        Scene scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
        if (scene.name != "FinalScene")
        {
            return false;
        }

        Transform current = transform;
        while (current != null)
        {
            if (IsFinalEnemyName(current.name))
            {
                return true;
            }

            current = current.parent;
        }

        return GetComponent<FinalSceneBossFightDirector>() != null || GetComponentInParent<FinalSceneBossFightDirector>() != null;
    }

    private static bool IsFinalEnemyName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName) &&
               (objectName.Contains("Rhea Malik") || objectName.Contains("FinalEnemy") || objectName.Contains("FInalEnemy"));
    }

    private void SpawnHitParticles(Vector3 position, Quaternion rotation)
    {
        if (hitParticlePrefab == null)
        {
            return;
        }

        GameObject hitParticles = Instantiate(hitParticlePrefab, position, rotation);
        PlayParticleObject(hitParticles);
        Destroy(hitParticles, 2f);
    }

    private void PlayParticleObject(GameObject particleObject)
    {
        if (particleObject == null)
        {
            return;
        }

        GameObject hitParticles = particleObject;
        hitParticles.SetActive(true);

        ParticleSystem[] particles = hitParticles.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }

        Animator[] animators = hitParticles.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].speed = 1f;
            animators[i].Play(0, 0, 0f);
        }

        AudioSource[] audioSources = hitParticles.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            audioSources[i].Play();
        }
    }

    private GameObject BuildFallbackGun(Transform parent)
    {
        GameObject root = new GameObject("FallbackRifle");
        root.transform.SetParent(parent, false);

        CreateGunPart(root.transform, "Body", new Vector3(0.13f, 0.1f, 0.5f), new Vector3(0f, 0f, 0.17f));
        CreateGunPart(root.transform, "Barrel", new Vector3(0.045f, 0.045f, 0.58f), new Vector3(0f, 0.015f, 0.62f));
        CreateGunPart(root.transform, "Stock", new Vector3(0.11f, 0.11f, 0.28f), new Vector3(0f, -0.015f, -0.2f));
        CreateGunPart(root.transform, "Grip", new Vector3(0.055f, 0.18f, 0.075f), new Vector3(0.035f, -0.12f, 0.03f));
        CreateGunPart(root.transform, "Magazine", new Vector3(0.08f, 0.18f, 0.095f), new Vector3(-0.025f, -0.12f, 0.25f));
        CreateGunPart(root.transform, "Sight", new Vector3(0.07f, 0.04f, 0.16f), new Vector3(0f, 0.085f, 0.28f));

        return root;
    }

    private void CreateGunPart(Transform parent, string partName, Vector3 scale, Vector3 localPosition)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localScale = scale;
        part.transform.localPosition = localPosition;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Destroy(partCollider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (runtimeGunMaterial == null)
            {
                runtimeGunMaterial = new Material(renderer.sharedMaterial);
                runtimeGunMaterial.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            }

            renderer.sharedMaterial = runtimeGunMaterial;
        }
    }

    private Transform CreateMarker(Transform parent, string markerName, Vector3 localPosition)
    {
        GameObject marker = new GameObject(markerName);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localRotation = Quaternion.identity;
        return marker.transform;
    }

    private Transform CreateMuzzleFlash(Transform parent)
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "MuzzleFlash";
        flash.transform.SetParent(parent, false);
        flash.transform.localPosition = Vector3.zero;
        flash.transform.localScale = new Vector3(0.18f, 0.18f, 0.28f);

        Collider flashCollider = flash.GetComponent<Collider>();
        if (flashCollider != null)
        {
            Destroy(flashCollider);
        }

        Renderer renderer = flash.GetComponent<Renderer>();
        if (renderer != null)
        {
            muzzleFlashMaterial = new Material(renderer.sharedMaterial);
            muzzleFlashMaterial.color = new Color(1f, 0.78f, 0.12f, 1f);
            renderer.sharedMaterial = muzzleFlashMaterial;
        }

        flash.SetActive(false);
        return flash.transform;
    }

    private void SetAnimatorBool(string parameter, bool value)
    {
        if (animator != null && HasAnimatorParameter(parameter, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameter, value);
        }
    }

    private void SetAnimatorTrigger(string parameter)
    {
        if (animator != null && HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameter);
        }
    }

    private bool HasAnimatorParameter(string parameter, AnimatorControllerParameterType type)
    {
        for (int i = 0; i < animator.parameters.Length; i++)
        {
            AnimatorControllerParameter animatorParameter = animator.parameters[i];
            if (animatorParameter.type == type && animatorParameter.name == parameter)
            {
                return true;
            }
        }

        return false;
    }
}
