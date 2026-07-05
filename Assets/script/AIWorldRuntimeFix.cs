using System.Collections;
using System.Reflection;
using Invector;
using Invector.IK;
using Invector.vEventSystems;
using Invector.vCamera;
using Invector.vCharacterController;
using Invector.vCharacterController.AI;
using Invector.vItemManager;
using Invector.vShooter;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-300)]
public class AIWorldRuntimeFix : MonoBehaviour
{
    private const string DemoSceneName = "Demo Scene 1";
    private const string LevelOneSceneName = "Level-1";
    private const int DemoRifleItemId = 11;
    private const float EnemyShootRange = 45f;
    private const float EnemyBurstCooldown = 6f;

    private static AIWorldRuntimeFix instance;
    private static vWeaponIKAdjustList demoRifleIKAdjustList;
    private float nextRuntimeRepairTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeFix()
    {
        if (instance != null)
        {
            return;
        }

        GameObject fixerObject = new GameObject("AIWorldRuntimeFix");
        DontDestroyOnLoad(fixerObject);
        instance = fixerObject.AddComponent<AIWorldRuntimeFix>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(ConfigureRepeatedly());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        if (Time.time < nextRuntimeRepairTime)
        {
            return;
        }

        nextRuntimeRepairTime = Time.time + 0.5f;
        ConfigurePlayerCharacters();
        ConfigureSimpleMeleeAI();
        ConfigureCompleteEnemies();
        ConfigureLevelOneGroundFollowers();
        if (IsDemoScene1())
        {
            ConfigureEnemyPassThrough();
        }
        ConfigureShooterManagers();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ConfigureRepeatedly());
    }

    private IEnumerator ConfigureRepeatedly()
    {
        yield return null;
        ConfigureAll();
        yield return new WaitForSeconds(0.5f);
        ConfigureAll();
        yield return new WaitForSeconds(2f);
        ConfigureAll();
        yield return new WaitForSeconds(4f);
        ConfigureAll();
    }

    private void ConfigureAll()
    {
        ConfigureTerrain();
        ConfigurePlayerCharacters();
        ConfigureSimpleMeleeAI();
        ConfigureCompleteEnemies();
        ConfigureLevelOneGroundFollowers();
        if (IsDemoScene1())
        {
            ConfigureEnemyPassThrough();
        }
        ConfigureShooterManagers();
        ConfigureGtaCamera();
    }

    private void ConfigureTerrain()
    {
        Terrain[] terrains = FindObjectsOfType<Terrain>(true);
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null)
            {
                continue;
            }

            terrain.gameObject.layer = GetLayer("Default", terrain.gameObject.layer);
            terrain.drawHeightmap = true;
            terrain.drawTreesAndFoliage = true;

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                terrainCollider.enabled = true;
                terrainCollider.terrainData = terrain.terrainData;
                terrainCollider.gameObject.layer = terrain.gameObject.layer;
            }
        }

        NavMeshSurface[] surfaces = FindObjectsOfType<NavMeshSurface>(true);
        for (int i = 0; i < surfaces.Length; i++)
        {
            NavMeshSurface surface = surfaces[i];
            if (surface == null)
            {
                continue;
            }

            surface.enabled = true;
            if (surface.layerMask == 0)
            {
                surface.layerMask = LayerMaskFor("Default", "Player", "Enemy", "CompanionAI", "BodyPart");
                surface.layerMask |= LayerMaskFromLayers(0, 8, 9, 10, 15);
            }
        }
    }

    private void ConfigureSimpleMeleeAI()
    {
        vSimpleMeleeAI_Controller[] controllers = FindObjectsOfType<vSimpleMeleeAI_Controller>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            vSimpleMeleeAI_Controller ai = controllers[i];
            if (ai == null)
            {
                continue;
            }

            bool companion = ai is vSimpleMeleeAI_Companion || ai.CompareTag("CompanionAI") || ai.gameObject.layer == GetLayer("CompanionAI", 10);
            if (companion)
            {
                ConfigureCompanion(ai);
            }
            else
            {
                ConfigureMeleeEnemy(ai);
            }

            ConfigureHumanoid(ai.transform);
            ConfigureSimpleAgent(ai.GetComponent<NavMeshAgent>(), companion);
            EnsureBodyDamageReceivers(ai.gameObject, companion ? "CompanionAI" : "Enemy", companion);
            EnsureDeathGrounder(ai.gameObject);
            if (companion)
            {
                EnsureCompanionRuntimeSupport(ai as vSimpleMeleeAI_Companion);
                RemoveCompanionGuns(ai.gameObject);
            }
        }
    }

    private void ConfigureMeleeEnemy(vSimpleMeleeAI_Controller ai)
    {
        SetTag(ai.gameObject, "Enemy");
        ai.gameObject.layer = GetLayer("Enemy", 9);
        ai.tagsToDetect.Clear();
        ai.tagsToDetect.Add("Player");
        ai.tagsToDetect.Add("CompanionAI");
        ai.layersToDetect = LayerMaskFor("Player", "CompanionAI", "BodyPart") | LayerMaskFromLayers(8, 10, 15);
        ai.maxDetectDistance = Mathf.Max(ai.maxDetectDistance, 18f);
        ai.minDetectDistance = Mathf.Max(ai.minDetectDistance, 3f);
        ai.distanceToLostTarget = Mathf.Max(ai.distanceToLostTarget, 10f);
        ai.fieldOfView = Mathf.Max(ai.fieldOfView, 180f);
        ai.sortTargetFromDistance = true;
        ai.agressiveAtFirstSight = true;
        ai.passiveToDamage = false;
        ai.chaseStopDistance = Mathf.Clamp(ai.chaseStopDistance, 0.9f, 1.4f);
    }

    private void ConfigureCompanion(vSimpleMeleeAI_Controller ai)
    {
        SetTag(ai.gameObject, "CompanionAI");
        ai.gameObject.layer = GetLayer("CompanionAI", 10);
        ai.tagsToDetect.Clear();
        ai.tagsToDetect.Add("Enemy");
        ai.layersToDetect = LayerMaskFor("Enemy", "BodyPart") | LayerMaskFromLayers(9, 15);
        ai.maxDetectDistance = Mathf.Max(ai.maxDetectDistance, 18f);
        ai.minDetectDistance = Mathf.Max(ai.minDetectDistance, 3f);
        ai.distanceToLostTarget = Mathf.Max(ai.distanceToLostTarget, 10f);
        ai.fieldOfView = Mathf.Max(ai.fieldOfView, 220f);
        ai.sortTargetFromDistance = true;
        ai.agressiveAtFirstSight = true;
        ai.passiveToDamage = false;
        ai.chaseStopDistance = Mathf.Clamp(ai.chaseStopDistance, 0.9f, 1.4f);

        vSimpleMeleeAI_Companion companion = ai as vSimpleMeleeAI_Companion;
        if (companion != null)
        {
            companion.companionTag = "Player";
            companion.companionState = vSimpleMeleeAI_Companion.CompanionState.Follow;
            companion.companion = ResolvePlayerTransform();
            companion.companionMaxDistance = Mathf.Max(companion.companionMaxDistance, 18f);
            companion.followStopDistance = Mathf.Clamp(companion.followStopDistance, 1.8f, 3f);
            companion.followSpeed = Mathf.Max(companion.followSpeed, 1f);
            companion.debug = false;
        }
    }

    private void ConfigureCompleteEnemies()
    {
        CompleteEnemyAI[] enemies = FindObjectsOfType<CompleteEnemyAI>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            CompleteEnemyAI enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            SetTag(enemy.gameObject, "Enemy");
            enemy.targetTags = new[] { "Player", "CompanionAI" };
            enemy.detectionRange = EnemyShootRange;
            enemy.attackRange = EnemyShootRange;
            enemy.restAfterBurst = EnemyBurstCooldown;
            enemy.usePercentBurstDamage = true;
            enemy.burstDamagePercent = 0.3f;
            enemy.burstShots = Mathf.Max(enemy.burstShots, 3);
            enemy.fireInterval = Mathf.Min(Mathf.Max(enemy.fireInterval, 0.1f), 0.35f);
            enemy.requireLineOfSight = false;
            enemy.faceTargetWhileFiring = true;
            enemy.staticEnemy = true;
            enemy.gameObject.layer = GetLayer("Enemy", 9);
            enemy.enemyLayer = GetLayer("Enemy", 9);
            enemy.bodyPartLayer = GetLayer("BodyPart", 15);
            ConfigureHumanoid(enemy.transform);
            ConfigureShooterAgent(enemy.GetComponent<NavMeshAgent>());
            EnsureBodyDamageReceivers(enemy.gameObject, "Enemy", false);
            EnsureDeathGrounder(enemy.gameObject);
        }
    }

    private void ConfigureLevelOneGroundFollowers()
    {
        if (SceneManager.GetActiveScene().name != LevelOneSceneName)
        {
            return;
        }

        SimpleAI[] soldiers = FindObjectsOfType<SimpleAI>(true);
        for (int i = 0; i < soldiers.Length; i++)
        {
            if (soldiers[i] != null)
            {
                EnsureTerrainFollower(soldiers[i].gameObject);
            }
        }

        EnemyPatrol[] patrols = FindObjectsOfType<EnemyPatrol>(true);
        for (int i = 0; i < patrols.Length; i++)
        {
            if (patrols[i] != null)
            {
                EnsureTerrainFollower(patrols[i].gameObject);
            }
        }

        EnemyGunAttack[] gunEnemies = FindObjectsOfType<EnemyGunAttack>(true);
        for (int i = 0; i < gunEnemies.Length; i++)
        {
            if (gunEnemies[i] != null)
            {
                EnsureTerrainFollower(gunEnemies[i].gameObject);
            }
        }

        CompleteEnemyAI[] completeEnemies = FindObjectsOfType<CompleteEnemyAI>(true);
        for (int i = 0; i < completeEnemies.Length; i++)
        {
            if (completeEnemies[i] != null)
            {
                EnsureTerrainFollower(completeEnemies[i].gameObject);
            }
        }
    }

    private void ConfigurePlayerCharacters()
    {
        GameObject[] players = FindTaggedObjects("Player");
        for (int i = 0; i < players.Length; i++)
        {
            GameObject player = players[i];
            if (player == null)
            {
                continue;
            }

            player.layer = GetLayer("Player", 8);
            ConfigureHumanoid(player.transform);
            ConfigurePlayerAgent(player.GetComponent<NavMeshAgent>());
            if (IsDemoScene1())
            {
                EnsurePlayerWeaponFix(player);
            }
        }

        vThirdPersonController[] controllers = FindObjectsOfType<vThirdPersonController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            vThirdPersonController controller = controllers[i];
            if (controller == null || !controller.gameObject.activeInHierarchy || controller.currentHealth <= 0f)
            {
                continue;
            }

            SetTag(controller.gameObject, "Player");
            controller.gameObject.layer = GetLayer("Player", 8);
            ConfigureHumanoid(controller.transform);
            ConfigurePlayerAgent(controller.GetComponent<NavMeshAgent>());
            if (IsDemoScene1())
            {
                EnsurePlayerWeaponFix(controller.gameObject);
            }

            if (vGameController.instance != null && vGameController.instance.currentPlayer == null)
            {
                vGameController.instance.currentPlayer = controller.gameObject;
            }
        }
    }

    private void ConfigureShooterManagers()
    {
        vShooterManager[] managers = FindObjectsOfType<vShooterManager>(true);
        LayerMask damageMask = LayerMaskFor("Default", "Enemy", "BodyPart");
        damageMask |= LayerMaskFromLayers(9, 15);
        for (int i = 0; i < managers.Length; i++)
        {
            vShooterManager manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            manager.damageLayer = damageMask;
            manager.blockAimLayer = LayerMaskFor("Default");
            if (!manager.ignoreTags.Contains("Player"))
            {
                manager.ignoreTags.Add("Player");
            }
            if (!manager.ignoreTags.Contains("CompanionAI"))
            {
                manager.ignoreTags.Add("CompanionAI");
            }
            manager.ignoreTags.Remove("Enemy");
            bool demoScene = IsDemoScene1();
            if (demoScene)
            {
                manager.useLeftIK = true;
                manager.useRightIK = true;
                manager.raycastAimTarget = true;
                manager.alignArmToHitPoint = true;
                manager.SetIKAdjustList(EnsureDemoRifleIKAdjustList());
                EnsureDemoRifleEquipped(manager);
                SnapEquippedRifleToHandler(manager);
            }

            ConfigureShooterWeapon(manager.rWeapon, damageMask, manager.transform, demoScene);
            ConfigureShooterWeapon(manager.lWeapon, damageMask, manager.transform, demoScene);
            ConfigureShooterWeapon(manager.CurrentWeapon, damageMask, manager.transform, demoScene);

            vShooterWeapon[] childWeapons = manager.GetComponentsInChildren<vShooterWeapon>(true);
            for (int weaponIndex = 0; weaponIndex < childWeapons.Length; weaponIndex++)
            {
                ConfigureShooterWeapon(childWeapons[weaponIndex], damageMask, manager.transform, demoScene);
            }
        }
    }

    private void ConfigureShooterWeapon(vShooterWeapon weapon, LayerMask damageMask, Transform shooterRoot, bool demoScene)
    {
        if (weapon == null)
        {
            return;
        }

        weapon.hitLayer = damageMask;
        weapon.root = shooterRoot;
        if (demoScene)
        {
            weapon.weaponCategory = string.IsNullOrEmpty(weapon.weaponCategory) || weapon.weaponCategory == "MyCategory" ? "Rifle" : weapon.weaponCategory;
            weapon.raycastAimTarget = true;
            weapon.alignRightHandToAim = true;
            weapon.alignRightUpperArmToAim = true;
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
        if (!weapon.ignoreTags.Contains("Player"))
        {
            weapon.ignoreTags.Add("Player");
        }
        if (!weapon.ignoreTags.Contains("CompanionAI"))
        {
            weapon.ignoreTags.Add("CompanionAI");
        }
        weapon.ignoreTags.Remove("Enemy");
    }

    private vWeaponIKAdjustList EnsureDemoRifleIKAdjustList()
    {
        if (demoRifleIKAdjustList != null)
        {
            return demoRifleIKAdjustList;
        }

        demoRifleIKAdjustList = Resources.Load<vWeaponIKAdjustList>("Vbot2.0_IKAdjusts/WeaponIKAdjustList@vBot");
        if (demoRifleIKAdjustList != null)
        {
            return demoRifleIKAdjustList;
        }

        demoRifleIKAdjustList = ScriptableObject.CreateInstance<vWeaponIKAdjustList>();
        demoRifleIKAdjustList.name = "DemoScene1_RuntimeRifleIK";

        vWeaponIKAdjust rifleIK = ScriptableObject.CreateInstance<vWeaponIKAdjust>();
        rifleIK.name = "DemoScene1_RifleIK";
        rifleIK.weaponCategories.Clear();
        rifleIK.weaponCategories.Add("Rifle");
        rifleIK.weaponCategories.Add("AssaultRifle");
        rifleIK.weaponCategories.Add("Machine-Gun");
        rifleIK.weaponCategories.Add("MyCategory");
        rifleIK.ApplyCorretlyName();
        ConfigureRifleIKAdjust(rifleIK.standingRight, false);
        ConfigureRifleIKAdjust(rifleIK.standingAimingRight, true);
        ConfigureRifleIKAdjust(rifleIK.crouchingRight, false);
        ConfigureRifleIKAdjust(rifleIK.crouchingAimingRight, true);
        ConfigureRifleIKAdjust(rifleIK.standingLeft, false);
        ConfigureRifleIKAdjust(rifleIK.standingAimingLeft, true);
        ConfigureRifleIKAdjust(rifleIK.crouchingLeft, false);
        ConfigureRifleIKAdjust(rifleIK.crouchingAimingLeft, true);
        rifleIK.ikAdjustsLeft.Clear();
        rifleIK.ikAdjustsRight.Clear();
        rifleIK.AddDefaultStates();

        demoRifleIKAdjustList.weaponIKAdjusts.Add(rifleIK);
        return demoRifleIKAdjustList;
    }

    private void ConfigureRifleIKAdjust(IKAdjust adjust, bool aiming)
    {
        if (adjust == null)
        {
            return;
        }

        adjust.weaponHandOffset.position = aiming ? new Vector3(0.005f, -0.015f, 0.015f) : Vector3.zero;
        adjust.weaponHandOffset.eulerAngles = aiming ? new Vector3(-5f, 0f, 0f) : Vector3.zero;
        adjust.weaponHintOffset.position = new Vector3(0.05f, -0.04f, 0.08f);
        adjust.weaponHintOffset.eulerAngles = Vector3.zero;
        adjust.supportHandOffset.position = aiming ? new Vector3(0.02f, -0.015f, 0.02f) : Vector3.zero;
        adjust.supportHandOffset.eulerAngles = aiming ? new Vector3(-8f, 4f, -2f) : Vector3.zero;
        adjust.supportHintOffset.position = new Vector3(-0.05f, -0.04f, 0.08f);
        adjust.supportHintOffset.eulerAngles = Vector3.zero;
    }

    private void EnsurePlayerWeaponFix(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        PlayerWeaponPointFix fix = player.GetComponent<PlayerWeaponPointFix>();
        if (fix == null)
        {
            fix = player.AddComponent<PlayerWeaponPointFix>();
        }

        fix.repairShooterReferences = true;
        fix.cameraDotDamageAssist = true;
        fix.requireAimForCameraDotDamage = false;
        fix.stabilizeRifleArmPosition = true;
        fix.forceRifleGripPosition = true;
    }

    private void EnsureDemoRifleEquipped(vShooterManager manager)
    {
        if (manager == null || IsRifleWeapon(manager.CurrentWeapon))
        {
            return;
        }

        vItemManager itemManager = manager.GetComponent<vItemManager>();
        if (itemManager == null || itemManager.inventory == null || itemManager.itemListData == null || itemManager.items == null)
        {
            return;
        }

        if (itemManager.ItemIsInSpecificEquipPoint(DemoRifleItemId, "RightArm"))
        {
            EquipPoint equipPoint = itemManager.equipPoints != null ? itemManager.equipPoints.Find(point => point != null && point.equipPointName == "RightArm") : null;
            if (equipPoint != null && equipPoint.equipmentReference != null && equipPoint.equipmentReference.equipedObject != null)
            {
                manager.SetRightWeapon(equipPoint.equipmentReference.equipedObject);
                SnapEquipmentToHandler(equipPoint.equipmentReference.equipedObject.transform, equipPoint);
            }
            return;
        }

        int rightAreaIndex = FindEquipAreaIndex(itemManager, "RightArm");
        if (rightAreaIndex < 0)
        {
            return;
        }

        vItem rifle = itemManager.GetItem(DemoRifleItemId);
        if (rifle != null)
        {
            EquipItemToArea(itemManager, rifle, rightAreaIndex);
        }
    }

    private void SnapEquippedRifleToHandler(vShooterManager manager)
    {
        if (manager == null || !IsRifleWeapon(manager.rWeapon))
        {
            return;
        }

        vItemManager itemManager = manager.GetComponent<vItemManager>();
        EquipPoint equipPoint = itemManager != null && itemManager.equipPoints != null
            ? itemManager.equipPoints.Find(point => point != null && point.equipPointName == "RightArm")
            : null;
        SnapEquipmentToHandler(manager.rWeapon.transform, equipPoint);
    }

    private void SnapEquipmentToHandler(Transform equipment, EquipPoint equipPoint)
    {
        if (equipment == null || equipPoint == null || equipPoint.handler == null || equipPoint.handler.defaultHandler == null)
        {
            return;
        }

        Transform handler = ResolveEquipmentHandler(equipment, equipPoint);
        if (equipment.parent != handler)
        {
            equipment.SetParent(handler, false);
        }

        equipment.localPosition = Vector3.zero;
        equipment.localRotation = Quaternion.identity;
        equipment.localScale = Vector3.one;
    }

    private Transform ResolveEquipmentHandler(Transform equipment, EquipPoint equipPoint)
    {
        if (equipment.parent == equipPoint.handler.defaultHandler)
        {
            return equipment.parent;
        }

        if (equipPoint.handler.customHandlers != null)
        {
            for (int i = 0; i < equipPoint.handler.customHandlers.Count; i++)
            {
                Transform customHandler = equipPoint.handler.customHandlers[i];
                if (customHandler != null && equipment.parent == customHandler)
                {
                    return customHandler;
                }
            }
        }

        return equipPoint.handler.defaultHandler;
    }

    private int FindEquipAreaIndex(vItemManager itemManager, string equipPointName)
    {
        if (itemManager == null || itemManager.inventory == null || itemManager.inventory.equipAreas == null)
        {
            return -1;
        }

        for (int i = 0; i < itemManager.inventory.equipAreas.Length; i++)
        {
            vEquipArea area = itemManager.inventory.equipAreas[i];
            if (area != null && area.equipPointName == equipPointName)
            {
                return i;
            }
        }

        return -1;
    }

    private void EquipItemToArea(vItemManager itemManager, vItem item, int areaIndex)
    {
        if (itemManager == null || item == null || itemManager.inventory == null || itemManager.inventory.equipAreas == null || areaIndex < 0 || areaIndex >= itemManager.inventory.equipAreas.Length)
        {
            return;
        }

        vEquipArea area = itemManager.inventory.equipAreas[areaIndex];
        if (area == null)
        {
            return;
        }

        for (int i = 0; i < area.equipSlots.Count; i++)
        {
            if (area.equipSlots[i] != null && area.equipSlots[i].item == item)
            {
                area.SetEquipSlot(i);
                area.EquipCurrentSlot();
                return;
            }
        }

        itemManager.AutoEquipItem(item, areaIndex, true, true);
    }

    private bool IsRifleWeapon(vShooterWeapon weapon)
    {
        if (weapon == null)
        {
            return false;
        }

        string category = weapon.weaponCategory;
        return category == "Rifle" || category == "AssaultRifle" || category == "Machine-Gun";
    }

    private bool IsDemoScene1()
    {
        return SceneManager.GetActiveScene().name == DemoSceneName;
    }

    private void ConfigureHumanoid(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.direction = 1;
            capsule.isTrigger = false;
            SyncInvectorColliderCache(root, capsule);
        }
    }

    private void SyncInvectorColliderCache(Transform root, CapsuleCollider capsule)
    {
        if (root == null || capsule == null)
        {
            return;
        }

        vThirdPersonMotor motor = root.GetComponent<vThirdPersonMotor>();
        if (motor != null)
        {
            motor._capsuleCollider = capsule;
            if (Application.isPlaying && IsPlayerRoot(root))
            {
                return;
            }

            motor.colliderCenter = capsule.center;
            motor.colliderRadius = capsule.radius;
            motor.colliderHeight = capsule.height;
            SetProtectedProperty(motor, "colliderCenterDefault", capsule.center);
            SetProtectedProperty(motor, "colliderRadiusDefault", capsule.radius);
            SetProtectedProperty(motor, "colliderHeightDefault", capsule.height);
        }

        vSimpleMeleeAI_Motor aiMotor = root.GetComponent<vSimpleMeleeAI_Motor>();
        if (aiMotor != null)
        {
            aiMotor._capsuleCollider = capsule;
        }
    }

    private bool IsPlayerRoot(Transform root)
    {
        if (root == null)
        {
            return false;
        }

        return root.CompareTag("Player") || root.GetComponent<vThirdPersonController>() != null;
    }

    private void SetProtectedProperty(object target, string propertyName, object value)
    {
        if (target == null || string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
        }
    }

    private void ConfigureSimpleAgent(NavMeshAgent agent, bool companion)
    {
        if (agent == null)
        {
            return;
        }

        agent.radius = 0.4f;
        agent.height = 1.8f;
        agent.speed = companion ? 2.6f : 2.4f;
        agent.angularSpeed = 300f;
        agent.acceleration = 8f;
        agent.stoppingDistance = companion ? 2f : 1.1f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        WarpToNavMesh(agent, 6f);
    }

    private void ConfigureShooterAgent(NavMeshAgent agent)
    {
        if (agent == null)
        {
            return;
        }

        agent.radius = 0.45f;
        agent.height = 1.8f;
        agent.speed = 2.2f;
        agent.angularSpeed = 300f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 2f;
        WarpToNavMesh(agent, 6f);
    }

    private void ConfigurePlayerAgent(NavMeshAgent agent)
    {
        if (agent == null)
        {
            return;
        }

        CharacterRuntimeFix runtimeFix = agent.GetComponent<CharacterRuntimeFix>();
        if (runtimeFix != null && runtimeFix.disableNavMeshAgentOnThisObject)
        {
            if (agent.enabled)
            {
                agent.enabled = false;
            }
            return;
        }

        agent.radius = 0.4f;
        agent.height = 1.8f;
        WarpToNavMesh(agent, 6f);
    }

    private void ConfigureGtaCamera()
    {
        Transform player = ResolvePlayerTransform();
        if (player == null)
        {
            return;
        }

        vThirdPersonCamera invectorCamera = FindObjectOfType<vThirdPersonCamera>(true);
        Camera targetCamera = Camera.main;

        RebelGTAThirdPersonCamera gtaCamera = FindObjectOfType<RebelGTAThirdPersonCamera>(true);
        if (gtaCamera == null && invectorCamera != null)
        {
            gtaCamera = invectorCamera.gameObject.AddComponent<RebelGTAThirdPersonCamera>();
        }
        if (gtaCamera == null)
        {
            return;
        }

        gtaCamera.enabled = true;
        gtaCamera.target = player;
        gtaCamera.invectorCamera = invectorCamera != null ? invectorCamera : gtaCamera.GetComponent<vThirdPersonCamera>();
        if (targetCamera == null)
        {
            targetCamera = gtaCamera.GetComponentInChildren<Camera>(true);
        }
        gtaCamera.targetCamera = targetCamera;
        gtaCamera.useInvectorCameraMovement = false;
        gtaCamera.freezeInvectorCameraMovement = false;
        gtaCamera.stabilizeInvectorRigidbody = true;
        gtaCamera.pivotOffset = new Vector3(0f, 1.55f, 0f);
        gtaCamera.distance = 4.9f;
        gtaCamera.aimingDistance = 3.05f;
        gtaCamera.shoulderOffset = 0.5f;
        gtaCamera.aimingShoulderOffset = 0.68f;
        gtaCamera.followSmooth = 10f;
        gtaCamera.rotationSmooth = 14f;
        gtaCamera.mouseSensitivityX = 3.2f;
        gtaCamera.mouseSensitivityY = 2f;
        gtaCamera.gamepadSensitivityX = 115f;
        gtaCamera.gamepadSensitivityY = 85f;
        gtaCamera.minPitch = -35f;
        gtaCamera.maxPitch = 65f;
        gtaCamera.autoBehindDelay = 0.9f;
        gtaCamera.autoBehindSpeed = 4.5f;
        gtaCamera.zoomMin = 2.6f;
        gtaCamera.zoomMax = 6.4f;
        gtaCamera.fieldOfView = 62f;
        gtaCamera.aimingFieldOfView = 55f;
        gtaCamera.collisionMask = CameraCollisionMask();

        if (gtaCamera.invectorCamera != null)
        {
            gtaCamera.invectorCamera.mainTarget = player;
            gtaCamera.invectorCamera.currentTarget = player;
            gtaCamera.invectorCamera.targetCamera = targetCamera;
            gtaCamera.invectorCamera.smoothBetweenState = 8f;
            gtaCamera.invectorCamera.smoothCameraRotation = 8f;
            gtaCamera.invectorCamera.smoothSwitchSide = 4f;
            gtaCamera.invectorCamera.autoBehindTarget = true;
            gtaCamera.invectorCamera.behindTargetDelay = 0.9f;
            gtaCamera.invectorCamera.behindTargetSmoothRotation = 4f;
            gtaCamera.invectorCamera.offSetPlayerPivot = 0f;
            gtaCamera.invectorCamera.distance = 4.9f;
            gtaCamera.invectorCamera.isFreezed = false;
        }
    }

    private void EnsureDeathGrounder(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        AIDeathGrounder grounder = target.GetComponent<AIDeathGrounder>();
        if (grounder == null)
        {
            grounder = target.AddComponent<AIDeathGrounder>();
        }

        grounder.groundMask = ~0;
        grounder.rayHeight = 2.5f;
        grounder.rayDistance = 8f;
        grounder.groundOffset = 0.02f;
        grounder.settleDuration = 1f;
    }

    private void EnsureTerrainFollower(GameObject target)
    {
        if (target == null || target.CompareTag("Player"))
        {
            return;
        }

        TerrainFollower follower = target.GetComponent<TerrainFollower>();
        if (follower == null)
        {
            follower = target.AddComponent<TerrainFollower>();
        }

        follower.rayDistance = 18f;
        follower.heightOffset = 0.03f;
        follower.smoothSpeed = 35f;
        follower.groundMask = ~0;
    }

    private void EnsureCompanionRuntimeSupport(vSimpleMeleeAI_Companion companion)
    {
        if (companion == null)
        {
            return;
        }

        AICompanionRuntimeSupport support = companion.GetComponent<AICompanionRuntimeSupport>();
        if (support == null)
        {
            support = companion.gameObject.AddComponent<AICompanionRuntimeSupport>();
        }

        support.playerTag = "Player";
        support.enemyMask = LayerMaskFor("Enemy", "BodyPart") | LayerMaskFromLayers(9, 15);
    }

    private void RemoveCompanionGuns(GameObject companion)
    {
        if (companion == null)
        {
            return;
        }

        EnemyGunAttack[] gunAttacks = companion.GetComponentsInChildren<EnemyGunAttack>(true);
        for (int i = 0; i < gunAttacks.Length; i++)
        {
            if (gunAttacks[i] != null)
            {
                Destroy(gunAttacks[i]);
            }
        }

        Transform runtimeGun = companion.transform.Find("EnemyRuntimeGun");
        if (runtimeGun != null)
        {
            Destroy(runtimeGun.gameObject);
        }
    }

    private void EnsureBodyDamageReceivers(GameObject root, string bodyTag, bool companion)
    {
        if (root == null)
        {
            return;
        }

        vHealthController health = root.GetComponent<vHealthController>();
        if (health == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];
            if (targetCollider == null)
            {
                continue;
            }

            bool rootCollider = targetCollider.transform == root.transform;
            if (rootCollider)
            {
                targetCollider.gameObject.layer = companion ? GetLayer("CompanionAI", 10) : GetLayer("Enemy", 9);
            }
            else
            {
                targetCollider.gameObject.layer = GetLayer("BodyPart", targetCollider.gameObject.layer);
                if (!companion && IsDemoScene1())
                {
                    targetCollider.isTrigger = true;
                }
            }

            SetTag(targetCollider.gameObject, bodyTag);
            AIBodyDamageReceiver receiver = targetCollider.GetComponent<AIBodyDamageReceiver>();
            if (receiver == null && targetCollider.GetComponent<vIDamageReceiver>() == null)
            {
                receiver = targetCollider.gameObject.AddComponent<AIBodyDamageReceiver>();
            }

            if (receiver != null)
            {
                receiver.ownerHealth = health;
            }
        }
    }

    private void ConfigureEnemyPassThrough()
    {
        GameObject[] enemies = FindTaggedObjects("Enemy");
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
            {
                continue;
            }

            Collider[] firstColliders = enemies[i].GetComponentsInChildren<Collider>(true);
            for (int j = i + 1; j < enemies.Length; j++)
            {
                if (enemies[j] == null)
                {
                    continue;
                }

                IgnoreColliderPair(firstColliders, enemies[j].GetComponentsInChildren<Collider>(true));
            }
        }
    }

    private void IgnoreColliderPair(Collider[] firstColliders, Collider[] secondColliders)
    {
        if (firstColliders == null || secondColliders == null)
        {
            return;
        }

        for (int i = 0; i < firstColliders.Length; i++)
        {
            Collider first = firstColliders[i];
            if (first == null)
            {
                continue;
            }

            for (int j = 0; j < secondColliders.Length; j++)
            {
                Collider second = secondColliders[j];
                if (second != null)
                {
                    Physics.IgnoreCollision(first, second, true);
                }
            }
        }
    }

    private void WarpToNavMesh(NavMeshAgent agent, float sampleRadius)
    {
        if (agent == null || !agent.enabled || agent.isOnNavMesh)
        {
            return;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(agent.transform.position, out hit, sampleRadius, agent.areaMask))
        {
            agent.Warp(hit.position);
        }
    }

    private LayerMask CameraCollisionMask()
    {
        int mask = Physics.DefaultRaycastLayers;
        mask &= ~LayerMaskFor("Player", "CompanionAI", "BodyPart", "Ignore Raycast", "UI");
        mask &= ~LayerMaskFromLayers(2, 5, 8, 10, 15);
        return mask;
    }

    private LayerMask LayerMaskFor(params string[] layerNames)
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

    private LayerMask LayerMaskFromLayers(params int[] layers)
    {
        int mask = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            int layer = layers[i];
            if (layer >= 0 && layer <= 31)
            {
                mask |= 1 << layer;
            }
        }

        return mask;
    }

    private int GetLayer(string layerName, int fallback)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? layer : fallback;
    }

    private void SetTag(GameObject target, string tagName)
    {
        if (target == null || string.IsNullOrEmpty(tagName))
        {
            return;
        }

        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
        }
    }

    private GameObject[] FindTaggedObjects(string tagName)
    {
        try
        {
            return GameObject.FindGameObjectsWithTag(tagName);
        }
        catch (UnityException)
        {
            return new GameObject[0];
        }
    }

    private Transform ResolvePlayerTransform()
    {
        if (vGameController.instance != null && IsValidPlayer(vGameController.instance.currentPlayer))
        {
            return vGameController.instance.currentPlayer.transform;
        }

        vThirdPersonController[] controllers = FindObjectsOfType<vThirdPersonController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            vThirdPersonController controller = controllers[i];
            if (controller != null && controller.gameObject.activeInHierarchy && controller.currentHealth > 0f)
            {
                return controller.transform;
            }
        }

        GameObject[] players = FindTaggedObjects("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (IsValidPlayer(players[i]))
            {
                return players[i].transform;
            }
        }

        return null;
    }

    private bool IsValidPlayer(GameObject player)
    {
        if (player == null || !player.activeInHierarchy)
        {
            return false;
        }

        vHealthController health = player.GetComponent<vHealthController>();
        if (health == null)
        {
            health = player.GetComponentInChildren<vHealthController>();
        }

        return health == null || (!health.isDead && health.currentHealth > 0f);
    }
}

[DisallowMultipleComponent]
public class AICompanionRuntimeSupport : MonoBehaviour
{
    public string playerTag = "Player";
    public LayerMask enemyMask = ~0;

    private vSimpleMeleeAI_Companion companion;
    private NavMeshAgent agent;
    private float nextPlayerResolveTime;
    private Coroutine startupFollowRoutine;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        CacheComponents();
        if (startupFollowRoutine != null)
        {
            StopCoroutine(startupFollowRoutine);
        }
        startupFollowRoutine = StartCoroutine(ForceStartupFollow());
    }

    private void OnDisable()
    {
        if (startupFollowRoutine != null)
        {
            StopCoroutine(startupFollowRoutine);
            startupFollowRoutine = null;
        }
    }

    private void Start()
    {
        ApplyRuntimeSettings(true);
    }

    private void CacheComponents()
    {
        companion = GetComponent<vSimpleMeleeAI_Companion>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        ApplyRuntimeSettings(false);
    }

    private IEnumerator ForceStartupFollow()
    {
        for (int i = 0; i < 30; i++)
        {
            ApplyRuntimeSettings(true);
            yield return i < 6 ? null : new WaitForSeconds(0.25f);
        }

        startupFollowRoutine = null;
    }

    private void ApplyRuntimeSettings(bool forcePlayerResolve)
    {
        if (companion == null)
        {
            companion = GetComponent<vSimpleMeleeAI_Companion>();
            if (companion == null)
            {
                return;
            }
        }
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        companion.companionTag = playerTag;
        companion.companionState = vSimpleMeleeAI_Companion.CompanionState.Follow;
        Transform player = ResolveCurrentPlayer(forcePlayerResolve);
        if (player != null && companion.companion != player)
        {
            companion.companion = player;
        }

        companion.companionMaxDistance = Mathf.Max(companion.companionMaxDistance, 30f);
        companion.followStopDistance = Mathf.Clamp(companion.followStopDistance, 1.8f, 3f);
        companion.followSpeed = Mathf.Max(companion.followSpeed, 1f);
        companion.tagsToDetect.Clear();
        companion.tagsToDetect.Add("Enemy");
        companion.layersToDetect = enemyMask;
        companion.sortTargetFromDistance = true;
        companion.agressiveAtFirstSight = true;
        companion.passiveToDamage = false;
        companion.maxDetectDistance = Mathf.Max(companion.maxDetectDistance, 18f);
        companion.fieldOfView = Mathf.Max(companion.fieldOfView, 220f);

        SanitizeCurrentTarget();
        KeepFollowingPlayer();
    }

    private Transform ResolveCurrentPlayer(bool force)
    {
        if (!force && Time.time < nextPlayerResolveTime && companion != null && IsValidPlayer(companion.companion))
        {
            return companion.companion;
        }

        nextPlayerResolveTime = Time.time + 0.25f;

        if (vGameController.instance != null && IsValidPlayer(vGameController.instance.currentPlayer))
        {
            return vGameController.instance.currentPlayer.transform;
        }

        vThirdPersonController[] controllers = FindObjectsOfType<vThirdPersonController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            vThirdPersonController controller = controllers[i];
            if (controller != null && controller.gameObject.activeInHierarchy && controller.currentHealth > 0f)
            {
                TrySetTag(controller.gameObject, playerTag);
                controller.gameObject.layer = LayerMask.NameToLayer("Player") >= 0 ? LayerMask.NameToLayer("Player") : controller.gameObject.layer;
                return controller.transform;
            }
        }

        try
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (IsValidPlayer(taggedPlayer))
            {
                return taggedPlayer.transform;
            }
        }
        catch (UnityException)
        {
        }

        return null;
    }

    private void KeepFollowingPlayer()
    {
        if (agent == null || companion == null || companion.companion == null || !agent.enabled)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, companion.companion.position);
        if (HasEnemyTarget() && distance <= companion.companionMaxDistance)
        {
            return;
        }

        if (distance <= companion.followStopDistance)
        {
            return;
        }

        if (!EnsureAgentOnNavMesh())
        {
            return;
        }

        agent.isStopped = false;
        agent.stoppingDistance = companion.followStopDistance;
        agent.SetDestination(companion.companion.position);
    }

    private void SanitizeCurrentTarget()
    {
        if (companion == null || companion.currentTarget.transform == null)
        {
            return;
        }

        GameObject targetObject = companion.currentTarget.transform.gameObject;
        if (targetObject == gameObject || IsPlayerObject(targetObject) || targetObject.CompareTag("CompanionAI") || !IsEnemyTarget(targetObject))
        {
            companion.RemoveCurrentTarget();
        }
    }

    private bool HasEnemyTarget()
    {
        return companion != null && companion.currentTarget.transform != null && IsEnemyTarget(companion.currentTarget.transform.gameObject);
    }

    private bool IsEnemyTarget(GameObject target)
    {
        if (target == null || !target.activeInHierarchy)
        {
            return false;
        }

        if (target.CompareTag("Enemy"))
        {
            return true;
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int bodyPartLayer = LayerMask.NameToLayer("BodyPart");
        return (enemyLayer >= 0 && target.layer == enemyLayer) || (bodyPartLayer >= 0 && target.layer == bodyPartLayer) || target.GetComponentInParent<CompleteEnemyAI>() != null;
    }

    private bool IsPlayerObject(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.CompareTag(playerTag))
        {
            return true;
        }

        vThirdPersonController controller = target.GetComponent<vThirdPersonController>();
        if (controller == null)
        {
            controller = target.GetComponentInParent<vThirdPersonController>();
        }

        return controller != null && IsValidPlayer(controller.gameObject);
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

    private bool IsValidPlayer(Transform player)
    {
        return player != null && IsValidPlayer(player.gameObject);
    }

    private bool IsValidPlayer(GameObject player)
    {
        if (player == null || !player.activeInHierarchy)
        {
            return false;
        }

        vHealthController health = player.GetComponent<vHealthController>();
        if (health == null)
        {
            health = player.GetComponentInChildren<vHealthController>();
        }

        return health == null || (!health.isDead && health.currentHealth > 0f);
    }

    private void TrySetTag(GameObject target, string tagName)
    {
        if (target == null || string.IsNullOrEmpty(tagName))
        {
            return;
        }

        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
        }
    }
}

public class AIBodyDamageReceiver : MonoBehaviour, vIDamageReceiver, vIAttackReceiver
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

        if (ownerHealth == null || ownerHealth.isDead)
        {
            return;
        }

        startReceiveDamage.Invoke(damage);
        damage.receiver = ownerHealth.transform;
        ownerHealth.TakeDamage(damage);
        receiveDamage.Invoke(damage);
    }
}

[DisallowMultipleComponent]
public class AIDeathGrounder : MonoBehaviour
{
    public LayerMask groundMask = ~0;
    public float rayHeight = 2.5f;
    public float rayDistance = 8f;
    public float groundOffset = 0.02f;
    public float settleDuration = 1f;

    private vHealthController health;
    private Coroutine settleRoutine;

    private void Awake()
    {
        health = GetComponent<vHealthController>();
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<vHealthController>();
        }

        if (health != null)
        {
            health.onDead.AddListener(HandleDead);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.onDead.RemoveListener(HandleDead);
        }
    }

    private void HandleDead(GameObject deadObject)
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        SnapToGround();
        if (settleRoutine != null)
        {
            StopCoroutine(settleRoutine);
        }
        settleRoutine = StartCoroutine(SettleOnGround());
    }

    private IEnumerator SettleOnGround()
    {
        float endTime = Time.time + Mathf.Max(0.05f, settleDuration);
        while (Time.time < endTime)
        {
            SnapToGround();
            yield return null;
        }

        SnapToGround();
    }

    public void SnapToGround()
    {
        Vector3 position = transform.position;
        Vector3 groundPosition;
        if (TryGetGroundPosition(position, out groundPosition))
        {
            transform.position = new Vector3(position.x, groundPosition.y + groundOffset, position.z);
        }
    }

    private bool TryGetGroundPosition(Vector3 position, out Vector3 groundPosition)
    {
        Vector3 rayOrigin = position + Vector3.up * Mathf.Max(0.1f, rayHeight);
        float distance = Mathf.Max(0.1f, rayHeight + rayDistance);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore);
        bool foundHit = false;
        float bestDistance = float.MaxValue;
        RaycastHit bestHit = new RaycastHit();

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
        if (NavMesh.SamplePosition(position, out navHit, Mathf.Max(0.1f, rayDistance), NavMesh.AllAreas))
        {
            groundPosition = navHit.position;
            return true;
        }

        groundPosition = position;
        return false;
    }
}
