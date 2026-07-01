using System.Collections;
using Invector;
using UnityEngine;
using UnityEngine.AI;

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
    private float aimWeight;
    private bool playerInRange;
    private int lastIKFrame = -1;

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

        if (Time.time >= nextFireTime && CanShootTarget(targetPoint))
        {
            Shoot(targetPoint);
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
            force = (targetPoint - GetMuzzlePosition()).normalized * bulletDamage
        };

        playerHealth.TakeDamage(damage);
        PlayShotVisuals(targetPoint);

        if (drawDebugRay)
        {
            Debug.DrawLine(GetMuzzlePosition(), targetPoint, Color.red, 0.15f);
        }
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

    private void PlayShotVisuals(Vector3 targetPoint)
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
            Destroy(muzzleParticles, 2f);
        }

        if (bulletVisualPrefab != null)
        {
            GameObject bullet = Instantiate(bulletVisualPrefab, muzzlePosition, shotRotation);
            bullet.SetActive(true);
            StartCoroutine(MoveBulletVisual(bullet, targetPoint, shotDirection.normalized));
        }
        else if (hitParticlePrefab != null)
        {
            SpawnHitParticles(targetPoint, shotRotation);
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
