using Invector;
using Invector.vCamera;
using Invector.vCharacterController;
using Invector.vEventSystems;
using Invector.vShooter;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public class PlayerWeaponPointFix : MonoBehaviour
{
    public vThirdPersonCamera cameraRig;
    public bool autoFindCamera = true;
    public bool createMissingMuzzle = true;
    public bool repairShooterReferences = true;
    public bool cameraDotDamageAssist = true;
    public bool requireAimForCameraDotDamage;
    public bool debugCameraDotDamage;
    public bool stabilizeRifleArmPosition = true;
    public bool forceRifleGripPosition = true;
    public float cameraDotRange = 200f;
    public int fallbackCameraDotDamage = 25;
    public Vector3 fallbackMuzzleLocalPosition = new Vector3(0f, 0f, 1f);
    public Vector3 fallbackAimReferenceLocalPosition = new Vector3(0f, 0f, 1.25f);
    public Vector3 rifleLeftGripLocalPosition = new Vector3(-0.08f, -0.055f, 0.42f);
    public Vector3 rifleLeftGripLocalEulerAngles = new Vector3(0f, 0f, 0f);

    private vShooterManager shooterManager;
    private vShooterMeleeInput shooterInput;
    private Camera aimCamera;
    private float nextWeaponCheck;
    private float nextAssistedShotTime;
    private bool IsDemoScene1
    {
        get { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Demo Scene 1"; }
    }

    private void Awake()
    {
        CacheComponents();
        ApplyFixes();
    }

    private void Start()
    {
        ApplyFixes();
    }

    private void Update()
    {
        HandleCameraDotDamageAssist();
    }

    private void LateUpdate()
    {
        if (Time.time < nextWeaponCheck)
        {
            return;
        }

        nextWeaponCheck = Time.time + 0.5f;
        ApplyFixes();
    }

    private void CacheComponents()
    {
        if (!shooterManager)
        {
            shooterManager = GetComponent<vShooterManager>();
        }

        if (!shooterInput)
        {
            shooterInput = GetComponent<vShooterMeleeInput>();
        }
    }

    private void ApplyFixes()
    {
        CacheComponents();
        ResolveCamera();
        AssignCameraReferences();
        ConfigureShooterManager();
        FixWeaponPoints(shooterManager ? shooterManager.rWeapon : null);
        FixWeaponPoints(shooterManager ? shooterManager.lWeapon : null);
        FixWeaponPoints(shooterManager ? shooterManager.CurrentWeapon : null);
    }

    private void ResolveCamera()
    {
        if (cameraRig || !autoFindCamera)
        {
            aimCamera = Camera.main;
            return;
        }

        cameraRig = FindObjectOfType<vThirdPersonCamera>();
        aimCamera = Camera.main;
    }

    private void AssignCameraReferences()
    {
        if (!cameraRig)
        {
            return;
        }

        cameraRig.mainTarget = transform;
        if (shooterManager)
        {
            shooterManager.tpCamera = cameraRig;
        }

        if (shooterInput)
        {
            shooterInput.tpCamera = cameraRig;
        }
    }

    private void ConfigureShooterManager()
    {
        if (!repairShooterReferences || !shooterManager)
        {
            return;
        }

        if (shooterInput)
        {
            shooterInput.shooterManager = shooterManager;
        }

        LayerMask damageMask = LayerMaskFor("Default", "Enemy", "BodyPart");
        if (damageMask.value != 0)
        {
            shooterManager.damageLayer = damageMask;
        }

        LayerMask blockMask = LayerMaskFor("Default");
        if (blockMask.value != 0)
        {
            shooterManager.blockAimLayer = blockMask;
        }

        if (!shooterManager.ignoreTags.Contains("Player"))
        {
            shooterManager.ignoreTags.Add("Player");
        }
        if (!shooterManager.ignoreTags.Contains("CompanionAI"))
        {
            shooterManager.ignoreTags.Add("CompanionAI");
        }
        shooterManager.ignoreTags.Remove("Enemy");
        shooterManager.raycastAimTarget = true;
        shooterManager.alignArmToHitPoint = true;
        if (IsDemoScene1)
        {
            shooterManager.useLeftIK = true;
            shooterManager.useRightIK = true;
            shooterManager.armIKSmoothIn = Mathf.Max(shooterManager.armIKSmoothIn, 16f);
            shooterManager.armIKSmoothOut = Mathf.Max(shooterManager.armIKSmoothOut, 18f);
            shooterManager.smoothArmIKRotation = Mathf.Max(shooterManager.smoothArmIKRotation, 35f);
            shooterManager.smoothArmWeight = Mathf.Max(shooterManager.smoothArmWeight, 28f);
            shooterManager.maxAimAngle = Mathf.Max(shooterManager.maxAimAngle, 75f);
        }

        ConfigureWeapon(shooterManager.rWeapon);
        ConfigureWeapon(shooterManager.lWeapon);
        ConfigureWeapon(shooterManager.CurrentWeapon);
    }

    private void ConfigureWeapon(vShooterWeapon weapon)
    {
        if (!weapon)
        {
            return;
        }

        weapon.root = transform;
        weapon.hitLayer = shooterManager.damageLayer;
        weapon.ignoreTags = shooterManager.ignoreTags;
        weapon.ignoreTags.Remove("Enemy");
        weapon.raycastAimTarget = true;
        weapon.alignRightHandToAim = true;
        weapon.alignRightUpperArmToAim = true;
        if (IsDemoScene1)
        {
            weapon.useIKOnAiming = true;
            weapon.disableIkOnShot = false;
            weapon.freeIKOptions.use = true;
            weapon.freeIKOptions.useOnIdle = true;
            weapon.freeIKOptions.useOnWalk = true;
            weapon.freeIKOptions.useOnRun = true;
            weapon.freeIKOptions.useOnSprint = false;
            weapon.strafeIKOptions.use = true;
            weapon.strafeIKOptions.useOnIdle = true;
            weapon.strafeIKOptions.useOnWalk = true;
            weapon.strafeIKOptions.useOnRun = true;
            weapon.strafeIKOptions.useOnSprint = false;
        }
        weapon.dispersion = 0f;

        if (IsDemoScene1 && stabilizeRifleArmPosition)
        {
            StabilizeWeaponGrip(weapon);
        }
    }

    private void FixWeaponPoints(vShooterWeapon weapon)
    {
        if (!weapon)
        {
            return;
        }

        weapon.root = transform;

        var muzzleWasMissing = !weapon.muzzle;
        var aimReferenceWasMissing = !weapon.aimReference;

        if (!weapon.muzzle)
        {
            weapon.muzzle = FindWeaponPoint(weapon.transform, "muzzle", "fire", "barrel", "shoot");
            if (!weapon.muzzle && createMissingMuzzle)
            {
                weapon.muzzle = CreateWeaponPoint(weapon.transform, "Muzzle_Auto", fallbackMuzzleLocalPosition);
            }
        }

        if (!weapon.aimReference)
        {
            weapon.aimReference = FindWeaponPoint(weapon.transform, "aim", "reference", "sight", "scope");
            if (!weapon.aimReference && createMissingMuzzle)
            {
                var parent = weapon.muzzle ? weapon.muzzle : weapon.transform;
                weapon.aimReference = CreateWeaponPoint(parent, "AimReference_Auto", fallbackAimReferenceLocalPosition);
            }
        }

        if (weapon.muzzle && weapon.aimReference && (muzzleWasMissing || aimReferenceWasMissing))
        {
            weapon.muzzle.rotation = weapon.aimReference.rotation;
        }

        if (IsDemoScene1 && stabilizeRifleArmPosition)
        {
            StabilizeWeaponGrip(weapon);
        }
    }

    private void StabilizeWeaponGrip(vShooterWeapon weapon)
    {
        if (!weapon)
        {
            return;
        }

        Transform grip = weapon.handIKTarget;
        bool createdGrip = false;
        if (!grip)
        {
            grip = FindWeaponPoint(weapon.transform, "lefthandiktarget", "leftgrip", "left hand", "foregrip", "support");
        }

        if (!grip)
        {
            grip = CreateWeaponPoint(weapon.transform, "LeftHandIKTarget_Auto", rifleLeftGripLocalPosition);
            createdGrip = true;
        }

        weapon.handIKTarget = grip;

        if (forceRifleGripPosition && createdGrip)
        {
            grip.localPosition = rifleLeftGripLocalPosition;
            grip.localRotation = Quaternion.Euler(rifleLeftGripLocalEulerAngles);
            grip.localScale = Vector3.one;
        }

        if (weapon.handIKTargetOffset)
        {
            weapon.handIKTargetOffset.localPosition = Vector3.zero;
            weapon.handIKTargetOffset.localRotation = Quaternion.identity;
        }
    }

    private void HandleCameraDotDamageAssist()
    {
        if (!cameraDotDamageAssist || !shooterInput || !shooterManager || !shooterManager.CurrentWeapon)
        {
            return;
        }

        if (requireAimForCameraDotDamage && !shooterInput.IsAiming)
        {
            return;
        }

        if (!shooterInput.shotInput.GetButton() || Time.time < nextAssistedShotTime)
        {
            return;
        }

        var weapon = shooterManager.CurrentWeapon;
        if (!weapon.gameObject.activeInHierarchy || !weapon.HasAmmo())
        {
            return;
        }

        nextAssistedShotTime = Time.time + Mathf.Max(weapon.shootFrequency, 0.08f);
        if (!TryGetCameraDotRay(out var ray))
        {
            return;
        }

        if (debugCameraDotDamage)
        {
            Debug.DrawRay(ray.origin, ray.direction * cameraDotRange, Color.red, 0.25f);
        }

        var triggerMode = IsDemoScene1 ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        var hits = Physics.RaycastAll(ray, cameraDotRange, shooterManager.damageLayer, triggerMode);
        if (hits == null || hits.Length == 0)
        {
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (!hit.collider || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (shooterManager.ignoreTags.Contains(hit.collider.tag))
            {
                continue;
            }

            var damageTarget = FindDamageTarget(hit.collider);
            if (!damageTarget)
            {
                return;
            }

            if (IsEnemyTarget(damageTarget.gameObject) || IsEnemyTarget(hit.collider.gameObject))
            {
                int damageValue = Mathf.Max(fallbackCameraDotDamage, weapon.maxDamage);
                ApplyCameraDotDamage(hit, ray.direction, damageValue);
            }

            return;
        }
    }

    private bool TryGetCameraDotRay(out Ray ray)
    {
        if (!aimCamera)
        {
            aimCamera = Camera.main;
        }

        if (!aimCamera)
        {
            ray = default;
            return false;
        }

        ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return true;
    }

    private Transform FindDamageTarget(Collider collider)
    {
        if (!collider)
        {
            return null;
        }

        var attackReceiver = collider.GetComponentInParent<vIAttackReceiver>();
        if (attackReceiver != null)
        {
            var receiverComponent = attackReceiver as Component;
            return receiverComponent ? receiverComponent.transform : collider.transform;
        }

        var damageReceiver = collider.GetComponentInParent<vIDamageReceiver>();
        if (damageReceiver != null)
        {
            return damageReceiver.transform;
        }

        var health = collider.GetComponentInParent<vHealthController>();
        if (health)
        {
            return health.transform;
        }

        return collider.transform;
    }

    private bool IsEnemyTarget(GameObject target)
    {
        if (!target)
        {
            return false;
        }

        if (target.CompareTag("Enemy"))
        {
            return true;
        }

        var enemyLayer = LayerMask.NameToLayer("Enemy");
        var bodyPartLayer = LayerMask.NameToLayer("BodyPart");
        return (enemyLayer >= 0 && target.layer == enemyLayer) || (bodyPartLayer >= 0 && target.layer == bodyPartLayer) || target.GetComponentInParent<CompleteEnemyAI>();
    }

    private void ApplyCameraDotDamage(RaycastHit hit, Vector3 direction, int damageValue)
    {
        var damage = new vDamage(damageValue)
        {
            sender = transform,
            receiver = hit.collider.transform,
            hitPosition = hit.point,
            force = direction * damageValue
        };

        var attacker = GetComponent<vIMeleeFighter>();
        var hitObject = hit.collider.gameObject;
        var attackReceiver = hitObject.GetComponent<vIAttackReceiver>();
        if (attackReceiver != null)
        {
            attackReceiver.OnReceiveAttack(damage, attacker);
            return;
        }

        var damageReceiver = hitObject.GetComponent<vIDamageReceiver>();
        if (damageReceiver != null)
        {
            damageReceiver.TakeDamage(damage);
            return;
        }

        attackReceiver = hitObject.GetComponentInParent<vIAttackReceiver>();
        if (attackReceiver != null)
        {
            var receiverComponent = attackReceiver as Component;
            damage.receiver = receiverComponent ? receiverComponent.transform : hit.collider.transform;
            attackReceiver.OnReceiveAttack(damage, attacker);
            return;
        }

        damageReceiver = hitObject.GetComponentInParent<vIDamageReceiver>();
        if (damageReceiver != null)
        {
            damage.receiver = damageReceiver.transform;
            damageReceiver.TakeDamage(damage);
            return;
        }

        var health = hitObject.GetComponentInParent<vHealthController>();
        if (health && !health.isDead)
        {
            damage.receiver = health.transform;
            health.TakeDamage(damage);
        }
    }

    private static Transform FindWeaponPoint(Transform root, params string[] tokens)
    {
        if (!root)
        {
            return null;
        }

        var children = root.GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            var lowerName = child.name.ToLowerInvariant();
            foreach (var token in tokens)
            {
                if (lowerName.Contains(token))
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static LayerMask LayerMaskFor(params string[] layerNames)
    {
        int mask = 0;
        for (int i = 0; i < layerNames.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layerNames[i]);
            if (layer >= 0)
            {
                mask |= 1 << layer;
            }
        }

        return mask;
    }

    private static Transform CreateWeaponPoint(Transform parent, string pointName, Vector3 localPosition)
    {
        var point = new GameObject(pointName).transform;
        point.SetParent(parent, false);
        point.localPosition = localPosition;
        point.localRotation = Quaternion.identity;
        point.localScale = Vector3.one;
        return point;
    }
}
