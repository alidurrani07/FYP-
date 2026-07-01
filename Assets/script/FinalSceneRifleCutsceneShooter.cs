using System.Collections;
using System.Collections.Generic;
using Invector;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class FinalSceneRifleCutsceneShooter : MonoBehaviour
{
    [SerializeField] private Animator firingRifleAnimator;
    [SerializeField] private string shotResourceName = "shot";
    [SerializeField] private string enemyNamePrefix = "CompleteEnemy";
    [SerializeField] private string explosionObjectName = "ex";
    [SerializeField] private string shotParticleObjectName = "ShotParticle";
    [SerializeField] private float engagementRange = 18f;
    [SerializeField] private float movementThreshold = 0.01f;
    [SerializeField] private float rotationThreshold = 0.1f;
    [SerializeField] private float hitDelay = 0.05f;
    [SerializeField] private float shotCooldown = 0.25f;
    [SerializeField] private float deathViewHoldTime = 3f;
    [SerializeField] private float explosionSearchRadius = 5f;
    [SerializeField] private float explosionLifetime = 2f;
    [SerializeField] private float shotVolume = 1f;
    [SerializeField] private bool snapExplosionToEnemy = true;
    [SerializeField] private Vector3 explosionLocalOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private bool pauseCameraForDeathView = true;
    [SerializeField] private bool rotateRifleTowardEnemy = true;
    [SerializeField] private float rifleTurnSpeed = 18f;
    [SerializeField] private Vector3 rifleAimEulerOffset;
    [SerializeField] private float deadEnemyYPosition = 2.12f;
    [SerializeField] private float deadEnemyYApplyDelay = 0.05f;
    [SerializeField] private float deadEnemyYLockDuration = 2.5f;
    [SerializeField] private bool disableEnemyRootCapsuleOnDeath = true;

    private readonly HashSet<CompleteEnemyAI> shotEnemies = new HashSet<CompleteEnemyAI>();
    private readonly Dictionary<CompleteEnemyAI, Transform> enemyEffects = new Dictionary<CompleteEnemyAI, Transform>();
    private AudioSource shotSource;
    private Animator cameraAnimator;
    private float cameraAnimatorDefaultSpeed = 1f;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Coroutine shotRoutine;

    private void Awake()
    {
        if (firingRifleAnimator == null)
        {
            firingRifleAnimator = FindFiringRifleAnimator();
        }

        cameraAnimator = GetComponent<Animator>();
        if (cameraAnimator != null)
        {
            cameraAnimatorDefaultSpeed = cameraAnimator.speed;
        }

        shotSource = gameObject.AddComponent<AudioSource>();
        shotSource.clip = Resources.Load<AudioClip>(shotResourceName);
        shotSource.loop = false;
        shotSource.playOnAwake = false;
        shotSource.spatialBlend = 0f;
        shotSource.volume = Mathf.Clamp01(shotVolume);

        if (shotSource.clip == null)
        {
            Debug.LogWarning($"Gunfire sound clip not found in Resources: {shotResourceName}");
        }

        EnsureEnemyEffects();
        SetRifleAnimationPlaying(false);
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    private void Update()
    {
        bool isMoving = IsCutsceneMoving();

        if (!isMoving)
        {
            if (shotRoutine == null)
            {
                SetRifleAnimationPlaying(false);
            }

            return;
        }

        if (shotRoutine == null)
        {
            CompleteEnemyAI target = FindNextTarget();
            if (target != null)
            {
                shotRoutine = StartCoroutine(ShootTarget(target));
            }
            else
            {
                SetRifleAnimationPlaying(false);
            }
        }
    }

    private bool IsCutsceneMoving()
    {
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float movementSpeed = Vector3.Distance(transform.position, lastPosition) / deltaTime;
        float rotationSpeed = Quaternion.Angle(transform.rotation, lastRotation) / deltaTime;

        lastPosition = transform.position;
        lastRotation = transform.rotation;

        return movementSpeed > movementThreshold || rotationSpeed > rotationThreshold;
    }

    private IEnumerator ShootTarget(CompleteEnemyAI target)
    {
        shotEnemies.Add(target);
        SetCameraPaused(true);
        AimRifleAt(target.transform, true);
        SetRifleAnimationPlaying(true);

        if (shotSource.clip != null)
        {
            shotSource.PlayOneShot(shotSource.clip, shotVolume);
        }

        yield return new WaitForSeconds(hitDelay);

        if (target != null)
        {
            AimRifleAt(target.transform, false);
            PlayExplosionAtTarget(target.transform);
            KillEnemy(target);
        }

        yield return new WaitForSeconds(deathViewHoldTime);

        SetRifleAnimationPlaying(false);
        SetCameraPaused(false);
        yield return new WaitForSeconds(shotCooldown);

        shotRoutine = null;
    }

    private CompleteEnemyAI FindNextTarget()
    {
        CompleteEnemyAI[] enemies = FindObjectsOfType<CompleteEnemyAI>();
        CompleteEnemyAI best = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < enemies.Length; i++)
        {
            CompleteEnemyAI enemy = enemies[i];
            if (enemy == null || shotEnemies.Contains(enemy) || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(enemyNamePrefix) && !enemy.name.StartsWith(enemyNamePrefix))
            {
                continue;
            }

            vHealthController health = enemy.GetComponent<vHealthController>();
            if (health != null && health.isDead)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance <= engagementRange && distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void KillEnemy(CompleteEnemyAI enemy)
    {
        PrepareEnemyPhysicsForCutsceneDeath(enemy);

        vHealthController health = enemy.GetComponent<vHealthController>();
        if (health != null && !health.isDead)
        {
            health.ChangeHealth(0);
        }

        StartCoroutine(ApplyDeadEnemyYPosition(enemy.transform));
    }

    private IEnumerator ApplyDeadEnemyYPosition(Transform enemy)
    {
        yield return new WaitForSeconds(deadEnemyYApplyDelay);

        if (enemy == null)
        {
            yield break;
        }

        float endTime = Time.time + Mathf.Max(0.1f, deadEnemyYLockDuration);
        while (enemy != null && Time.time < endTime)
        {
            Vector3 position = enemy.position;
            enemy.position = new Vector3(position.x, deadEnemyYPosition, position.z);
            yield return null;
        }
    }

    private void PrepareEnemyPhysicsForCutsceneDeath(CompleteEnemyAI enemy)
    {
        if (enemy == null)
        {
            return;
        }

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.enabled = false;
        }

        CapsuleCollider rootCapsule = enemy.GetComponent<CapsuleCollider>();
        if (rootCapsule != null)
        {
            rootCapsule.direction = 2;
            rootCapsule.center = new Vector3(rootCapsule.center.x, 0.35f, rootCapsule.center.z);

            if (disableEnemyRootCapsuleOnDeath)
            {
                rootCapsule.enabled = false;
            }
        }
    }

    private void PlayExplosionAtTarget(Transform target)
    {
        CompleteEnemyAI enemy = target.GetComponent<CompleteEnemyAI>();
        Transform effect = enemy != null ? GetOrCreateEnemyEffect(enemy) : FindClosestExplosion(target.position, null);
        if (effect == null)
        {
            return;
        }

        if (snapExplosionToEnemy)
        {
            effect.SetParent(target, false);
            effect.localPosition = explosionLocalOffset;
        }

        effect.gameObject.SetActive(true);

        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }

        Animator[] animators = effect.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].speed = 1f;
            animators[i].Play(0, 0, 0f);
        }

        AudioSource[] audioSources = effect.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            audioSources[i].Play();
        }

        StartCoroutine(DisableExplosionLater(effect.gameObject));
    }

    private void EnsureEnemyEffects()
    {
        CompleteEnemyAI[] enemies = FindObjectsOfType<CompleteEnemyAI>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            CompleteEnemyAI enemy = enemies[i];
            if (enemy == null || (!string.IsNullOrEmpty(enemyNamePrefix) && !enemy.name.StartsWith(enemyNamePrefix)))
            {
                continue;
            }

            GetOrCreateEnemyEffect(enemy);
        }
    }

    private Transform GetOrCreateEnemyEffect(CompleteEnemyAI enemy)
    {
        if (enemyEffects.TryGetValue(enemy, out Transform cachedEffect) && cachedEffect != null)
        {
            return cachedEffect;
        }

        Transform effect = enemy.transform.Find(explosionObjectName);
        if (effect == null)
        {
            effect = FindClosestExplosion(enemy.transform.position, enemy);
        }

        if (effect == null)
        {
            GameObject effectObject = new GameObject(explosionObjectName);
            effect = effectObject.transform;
        }

        effect.SetParent(enemy.transform, false);
        effect.localPosition = explosionLocalOffset;
        effect.localRotation = Quaternion.identity;
        EnsureShotParticle(effect);
        effect.gameObject.SetActive(false);

        enemyEffects[enemy] = effect;
        return effect;
    }

    private void EnsureShotParticle(Transform effect)
    {
        ParticleSystem[] existingParticles = effect.GetComponentsInChildren<ParticleSystem>(true);
        if (existingParticles.Length > 0)
        {
            return;
        }

        GameObject particleObject = new GameObject(shotParticleObjectName);
        particleObject.transform.SetParent(effect, false);

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = 0.35f;
        main.startSpeed = 5f;
        main.startSize = 0.35f;
        main.startColor = new Color(1f, 0.72f, 0.25f, 1f);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;
    }

    private Transform FindClosestExplosion(Vector3 position, CompleteEnemyAI owner)
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        Transform best = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || !candidate.name.Equals(explosionObjectName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CompleteEnemyAI assignedEnemy = candidate.GetComponentInParent<CompleteEnemyAI>();
            if (assignedEnemy != null && assignedEnemy != owner)
            {
                continue;
            }

            float distance = Vector3.Distance(position, candidate.position);
            if (distance <= explosionSearchRadius && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private IEnumerator DisableExplosionLater(GameObject effect)
    {
        yield return new WaitForSeconds(explosionLifetime);

        if (effect != null)
        {
            effect.SetActive(false);
        }
    }

    private void SetRifleAnimationPlaying(bool value)
    {
        if (firingRifleAnimator != null)
        {
            firingRifleAnimator.speed = value ? 1f : 0f;
        }
    }

    private void AimRifleAt(Transform target, bool snap)
    {
        if (!rotateRifleTowardEnemy || firingRifleAnimator == null || target == null)
        {
            return;
        }

        Transform rifle = firingRifleAnimator.transform;
        Vector3 targetPoint = target.position + Vector3.up * 1.2f;
        Vector3 direction = targetPoint - rifle.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(rifleAimEulerOffset);
        rifle.rotation = snap ? desiredRotation : Quaternion.Slerp(rifle.rotation, desiredRotation, Time.deltaTime * rifleTurnSpeed);
    }

    private void SetCameraPaused(bool value)
    {
        if (!pauseCameraForDeathView || cameraAnimator == null)
        {
            return;
        }

        cameraAnimator.speed = value ? 0f : cameraAnimatorDefaultSpeed;
    }

    private void OnDisable()
    {
        SetCameraPaused(false);
    }

    private Animator FindFiringRifleAnimator()
    {
        Animator[] animators = FindObjectsOfType<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
            {
                continue;
            }

            string objectName = animator.gameObject.name;
            string controllerName = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : string.Empty;
            if (objectName.Contains("Fire Rifle") || objectName.Contains("Firing Rifle") || controllerName.Contains("Fire Rifle") || controllerName.Contains("Firing Rifle"))
            {
                return animator;
            }
        }

        return null;
    }
}
