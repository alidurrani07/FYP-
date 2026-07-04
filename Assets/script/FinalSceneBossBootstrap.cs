using System.Collections;
using Invector;
using Invector.vCamera;
using Invector.vCharacterController.AI;
using Invector.vEventSystems;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FinalSceneBossBootstrap : MonoBehaviour
{
    private const string FinalSceneName = "FinalScene";
    private const string BossObjectName = "Rhea Malik";
    private const string PlayerObjectName = "vShooterController_Swat";
    private const string PlayerObjectTag = "Player";
    private const string CutsceneCameraName = "Camera";
    private const string GameplayCameraName = "MainCamera";
    private const string GameplayCameraRigName = "vThirdPersonCamera";
    private const int BossHealth = 3000;
    private const int BossGunDamage = 15;
    private const int BossPunchDamage = 18;
    private const float FallbackCutsceneDuration = 75f;
    private const float CameraPositionThreshold = 1f;
    private const float CameraHandoffMaxWait = 120f;
    private const float CameraHandoffNormalizedTime = 0.985f;
    private const float FallbackMinimumCutsceneTime = 60f;
    private const float PlayerHandoffInvulnerabilitySeconds = 5f;
    private static readonly Vector3 CameraHandoffPosition = new Vector3(40.8f, 5f, 22.3f);

    private GameObject player;
    private vHealthController playerHealth;
    private FinalScenePlayerDamageGate playerDamageGate;
    private Camera cutsceneCamera;
    private Camera gameplayCamera;
    private vThirdPersonCamera gameplayCameraRig;
    private FinalSceneBossHealthBar bossHealthBar;
    private FinalSceneBossFightDirector bossFightDirector;
    private bool hasStarted;
    private bool hasHandedOffToGameplay;
    private bool cutsceneCameraMovedAwayFromHandoff;
    private bool hasCachedPlayerImmortal;
    private bool originalPlayerImmortal;
    private float cutsceneStartTime;
    private Coroutine playerProtectionRoutine;
    private GameObject skipPromptObject;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void PrepareInitialScene()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != FinalSceneName)
        {
            return;
        }

        FinalSceneBossBootstrap existing = FindSceneObjectOfType<FinalSceneBossBootstrap>(scene);
        if (existing != null)
        {
            existing.enabled = true;
            existing.Initialize(scene);
            return;
        }

        GameObject runner = new GameObject("FinalSceneBossBootstrap");
        SceneManager.MoveGameObjectToScene(runner, scene);
        FinalSceneBossBootstrap bootstrap = runner.AddComponent<FinalSceneBossBootstrap>();
        bootstrap.Initialize(scene);
    }

    private void Start()
    {
        Initialize(gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene());
    }

    private void Initialize(Scene scene)
    {
        if (scene.name != FinalSceneName || hasStarted)
        {
            return;
        }

        hasStarted = true;
        cutsceneStartTime = Time.time;
        cutsceneCameraMovedAwayFromHandoff = false;
        ResolveReferences(scene);
        ProtectPlayerForCutscene();
        ConfigureBoss(scene);
        SetGameplayActive(false);
        SetCutsceneCameraActive(true);
        CreateSkipPrompt(scene);
        StartCoroutine(PlayOpeningGate(scene));
    }

    private void Update()
    {
        if (!hasStarted || hasHandedOffToGameplay)
        {
            return;
        }

        Scene scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
        if (scene.name != FinalSceneName)
        {
            return;
        }

        if (cutsceneCamera == null)
        {
            cutsceneCamera = FindCutsceneCamera(scene);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            HandoffToGameplay(scene);
            return;
        }

        if (HasCutsceneCameraReachedHandoff())
        {
            HandoffToGameplay(scene);
        }
    }

    private void ResolveReferences(Scene scene)
    {
        player = FindSceneGameObject(scene, PlayerObjectName);
        if (player == null)
        {
            player = FindSceneObjectWithTag(scene, PlayerObjectTag);
        }

        cutsceneCamera = FindCutsceneCamera(scene);
        gameplayCamera = FindSceneCamera(scene, GameplayCameraName);

        Transform rigTransform = FindSceneTransform(scene, GameplayCameraRigName);
        if (rigTransform != null)
        {
            gameplayCameraRig = rigTransform.GetComponent<vThirdPersonCamera>();
        }

        playerHealth = ResolvePlayerHealth();
    }

    private void ConfigureBoss(Scene scene)
    {
        Transform boss = FindSceneTransform(scene, BossObjectName);
        if (boss == null)
        {
            Debug.LogWarning("FinalSceneBossBootstrap could not find Rhea Malik.");
            return;
        }

        vHealthController health = boss.GetComponent<vHealthController>();
        if (health == null)
        {
            health = boss.gameObject.AddComponent<vHealthController>();
        }

        health.isImmortal = false;
        health.healthRecovery = 0f;
        health.healthRecoveryDelay = 0f;
        health.fillHealthOnStart = false;
        health.maxHealth = BossHealth;
        health.ResetHealth(BossHealth);
        FinalSceneBossDamageGate damageGate = boss.GetComponent<FinalSceneBossDamageGate>();
        if (damageGate == null)
        {
            damageGate = boss.gameObject.AddComponent<FinalSceneBossDamageGate>();
        }
        damageGate.Bind(health);

        TrySetEnemyTagAndLayer(boss.gameObject);
        EnsureDamageReceivers(boss, health);
        ConfigureBossMeleeDamage(boss);
        ConfigureBossGun(boss);
        EnsurePlayerDamageGate(scene, boss);
        bossHealthBar = EnsureBossHealthBar(scene, boss, health);
        bossHealthBar.SetVisibleAllowed(false);
        bossFightDirector = EnsureBossFightDirector(scene, boss, health);
        bossFightDirector.SetFightActive(false);
    }

    private IEnumerator PlayOpeningGate(Scene scene)
    {
        if (cutsceneCamera != null)
        {
            yield return WaitForCutsceneCameraHandoffPosition(scene);
        }
        else
        {
            yield return new WaitForSeconds(FallbackCutsceneDuration);
        }

        yield return new WaitForSeconds(0.25f);

        HandoffToGameplay(scene);
    }

    private IEnumerator WaitForCutsceneCameraHandoffPosition(Scene scene)
    {
        if (cutsceneCamera == null)
        {
            cutsceneCamera = FindCutsceneCamera(scene);
        }

        if (cutsceneCamera == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (cutsceneCamera != null && elapsed < CameraHandoffMaxWait)
        {
            if (HasCutsceneCameraReachedHandoff())
            {
                yield break;
            }

            yield return null;

            elapsed += Time.deltaTime;
        }
    }

    private bool HasCutsceneCameraReachedHandoff()
    {
        if (cutsceneCamera == null)
        {
            return false;
        }

        Transform cameraTransform = cutsceneCamera.transform;
        bool localAtHandoff = IsAtHandoffPosition(cameraTransform.localPosition);
        bool worldAtHandoff = IsAtHandoffPosition(cameraTransform.position);
        bool atHandoff = localAtHandoff || worldAtHandoff;

        if (!atHandoff)
        {
            cutsceneCameraMovedAwayFromHandoff = true;
            return false;
        }

        return cutsceneCameraMovedAwayFromHandoff && HasCutscenePlaybackReachedEnd();
    }

    private bool HasCutscenePlaybackReachedEnd()
    {
        Animator animator = cutsceneCamera != null ? cutsceneCamera.GetComponent<Animator>() : null;
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return Time.time - cutsceneStartTime >= FallbackMinimumCutsceneTime;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return !animator.IsInTransition(0) && state.normalizedTime >= CameraHandoffNormalizedTime;
    }

    private static bool IsAtHandoffPosition(Vector3 position)
    {
        if ((position - CameraHandoffPosition).sqrMagnitude <= CameraPositionThreshold * CameraPositionThreshold)
        {
            return true;
        }

        bool xReached = Mathf.Abs(position.x - CameraHandoffPosition.x) <= CameraPositionThreshold;
        bool yReached = Mathf.Abs(position.y - CameraHandoffPosition.y) <= CameraPositionThreshold;
        bool zReached = Mathf.Abs(position.z - CameraHandoffPosition.z) <= CameraPositionThreshold;
        return xReached && yReached && zReached;
    }

    private void HandoffToGameplay(Scene scene)
    {
        if (hasHandedOffToGameplay)
        {
            return;
        }

        hasHandedOffToGameplay = true;
        DestroySkipPrompt();
        StartPlayerHandoffProtection();
        SetGameplayActive(true);
        SetCutsceneCameraActive(false);

        if (bossHealthBar != null)
        {
            bossHealthBar.SetVisibleAllowed(true);
        }

        if (bossFightDirector != null)
        {
            bossFightDirector.SetFightActive(true);
        }

        if (playerDamageGate != null)
        {
            playerDamageGate.SetFightActive(true);
        }

        ForceGameplayCamera(scene);
    }

    private void CreateSkipPrompt(Scene scene)
    {
        DestroySkipPrompt();

        skipPromptObject = new GameObject("FinalScene Skip Prompt");
        SceneManager.MoveGameObjectToScene(skipPromptObject, scene);

        Canvas canvas = skipPromptObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1200;

        CanvasScaler scaler = skipPromptObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        skipPromptObject.AddComponent<GraphicRaycaster>();

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(skipPromptObject.transform, false);

        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Press E To Skip";
        label.font = FinalSceneRuntimeFont.GetBossFont();
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.92f, 0.74f, 0.96f);
        label.raycastTarget = false;
        label.enableWordWrapping = false;

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.36f, 0.9f);
        rect.anchorMax = new Vector2(0.64f, 0.97f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void DestroySkipPrompt()
    {
        if (skipPromptObject == null)
        {
            return;
        }

        Destroy(skipPromptObject);
        skipPromptObject = null;
    }

    private void ProtectPlayerForCutscene()
    {
        vHealthController health = ResolvePlayerHealth();
        if (health == null)
        {
            return;
        }

        CachePlayerImmortalState(health);
        health.isDead = false;
        health.isImmortal = true;
        health.ResetHealth(Mathf.Max(1, health.MaxHealth));
    }

    private void StartPlayerHandoffProtection()
    {
        if (playerProtectionRoutine != null)
        {
            StopCoroutine(playerProtectionRoutine);
        }

        playerProtectionRoutine = StartCoroutine(ProtectPlayerAfterHandoff());
    }

    private IEnumerator ProtectPlayerAfterHandoff()
    {
        vHealthController health = ResolvePlayerHealth();
        if (health != null)
        {
            CachePlayerImmortalState(health);
            health.isDead = false;
            health.isImmortal = true;
            health.ResetHealth(Mathf.Max(1, health.MaxHealth));
        }

        yield return new WaitForSeconds(PlayerHandoffInvulnerabilitySeconds);

        health = ResolvePlayerHealth();
        if (health != null && hasCachedPlayerImmortal && playerDamageGate == null)
        {
            health.isImmortal = originalPlayerImmortal;
        }

        playerProtectionRoutine = null;
    }

    private void CachePlayerImmortalState(vHealthController health)
    {
        if (health == null || hasCachedPlayerImmortal)
        {
            return;
        }

        originalPlayerImmortal = health.isImmortal;
        hasCachedPlayerImmortal = true;
    }

    private IEnumerator WaitForAnimatorToFinish(Animator animator)
    {
        float timeout = Mathf.Max(FallbackCutsceneDuration + 20f, GetLongestClipLength(animator) + 20f);

        while (animator != null && timeout > 0f)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!animator.IsInTransition(0) && state.normalizedTime >= 0.995f)
            {
                yield break;
            }

            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    private static float GetLongestClipLength(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return FallbackCutsceneDuration;
        }

        float length = 0f;
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                length = Mathf.Max(length, clips[i].length);
            }
        }

        return length > 0f ? length : FallbackCutsceneDuration;
    }

    private void SetGameplayActive(bool active)
    {
        if (active)
        {
            ResolveReferences(gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene());
        }

        if (player != null)
        {
            if (active)
            {
                SetParentsActive(player.transform);
            }
            player.SetActive(active);
        }

        if (gameplayCameraRig != null)
        {
            if (active)
            {
                SetParentsActive(gameplayCameraRig.transform);
            }
            gameplayCameraRig.gameObject.SetActive(active);
            gameplayCameraRig.enabled = active;
            if (active && player != null)
            {
                gameplayCameraRig.mainTarget = player.transform;
                gameplayCameraRig.currentTarget = player.transform;
            }
        }

        if (gameplayCamera != null)
        {
            if (active)
            {
                SetParentsActive(gameplayCamera.transform);
            }
            gameplayCamera.gameObject.SetActive(active);
            gameplayCamera.enabled = active;
            AudioListener listener = gameplayCamera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = active;
            }
        }

        if (active && gameplayCameraRig != null)
        {
            Camera[] rigCameras = gameplayCameraRig.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < rigCameras.Length; i++)
            {
                Camera rigCamera = rigCameras[i];
                if (rigCamera == null)
                {
                    continue;
                }

                rigCamera.gameObject.SetActive(true);
                rigCamera.enabled = rigCamera.name == GameplayCameraName;
            }
        }
    }

    private void ForceGameplayCamera(Scene scene)
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = FindSceneCamera(scene, GameplayCameraName);
        }

        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || candidate.gameObject.scene != scene)
            {
                continue;
            }

            bool isGameplayCamera = candidate.name == GameplayCameraName || (gameplayCamera != null && candidate == gameplayCamera);
            candidate.enabled = isGameplayCamera;

            AudioListener listener = candidate.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = isGameplayCamera;
            }
        }

        if (gameplayCamera != null)
        {
            SetParentsActive(gameplayCamera.transform);
            gameplayCamera.gameObject.SetActive(true);
            gameplayCamera.tag = "MainCamera";
            gameplayCamera.depth = 10f;
            gameplayCamera.enabled = true;
            Camera.SetupCurrent(gameplayCamera);
        }
    }

    private vHealthController ResolvePlayerHealth()
    {
        if (player == null)
        {
            Scene scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
            player = FindSceneGameObject(scene, PlayerObjectName);
            if (player == null)
            {
                player = FindSceneObjectWithTag(scene, PlayerObjectTag);
            }
        }

        playerHealth = null;
        if (player == null)
        {
            return null;
        }

        playerHealth = player.GetComponent<vHealthController>();
        if (playerHealth == null)
        {
            playerHealth = player.GetComponentInChildren<vHealthController>(true);
        }
        if (playerHealth == null)
        {
            playerHealth = player.GetComponentInParent<vHealthController>(true);
        }

        return playerHealth;
    }

    private void SetCutsceneCameraActive(bool active)
    {
        if (cutsceneCamera == null)
        {
            return;
        }

        cutsceneCamera.gameObject.SetActive(active);
        cutsceneCamera.enabled = active;

        AudioListener listener = cutsceneCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = active;
        }
    }

    private static void TrySetEnemyTagAndLayer(GameObject boss)
    {
        try
        {
            boss.tag = "Enemy";
        }
        catch (UnityException)
        {
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            boss.layer = enemyLayer;
        }
    }

    private static void EnsureDamageReceivers(Transform boss, vHealthController health)
    {
        Collider[] colliders = boss.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider target = colliders[i];
            if (target == null)
            {
                continue;
            }

            FinalSceneBossDamageReceiver receiver = target.GetComponent<FinalSceneBossDamageReceiver>();
            if (receiver == null && target.GetComponent<vIDamageReceiver>() == null && target.GetComponent<vIAttackReceiver>() == null)
            {
                receiver = target.gameObject.AddComponent<FinalSceneBossDamageReceiver>();
            }

            if (receiver != null)
            {
                receiver.ownerHealth = health;
                receiver.onlyPlayerShotDamage = true;
            }
        }
    }

    private static void ConfigureBossMeleeDamage(Transform boss)
    {
        Invector.vMelee.vMeleeManager meleeManager = boss != null ? boss.GetComponent<Invector.vMelee.vMeleeManager>() : null;
        if (meleeManager == null)
        {
            return;
        }

        meleeManager.defaultDamage.damageValue = BossPunchDamage;
        meleeManager.defaultDamage.hitReaction = false;
        meleeManager.defaultDamage.activeRagdoll = false;
        meleeManager.defaultDamage.senselessTime = 0f;
        meleeManager.defaultDamage.reaction_id = -1;
        meleeManager.defaultDamage.recoil_id = -1;
        meleeManager.defaultDamage.ignoreDefense = true;
        if (meleeManager.hitProperties != null)
        {
            meleeManager.hitProperties.hitDamageTags.Clear();
            meleeManager.hitProperties.hitDamageTags.Add(PlayerObjectTag);
        }
    }

    private static void ConfigureBossGun(Transform boss)
    {
        if (boss == null)
        {
            return;
        }

        EnemyGunAttack gunAttack = boss.GetComponent<EnemyGunAttack>();
        if (gunAttack == null)
        {
            gunAttack = boss.gameObject.AddComponent<EnemyGunAttack>();
        }

        gunAttack.playerTag = PlayerObjectTag;
        gunAttack.targetTags = new[] { PlayerObjectTag };
        gunAttack.detectionRange = 160f;
        gunAttack.closeRange = 5f;
        gunAttack.bulletDamage = BossGunDamage;
        gunAttack.fireInterval = 0.85f;
        gunAttack.aimHeight = 1.25f;
        gunAttack.requireLineOfSight = false;
        gunAttack.lineOfSightMask = Physics.DefaultRaycastLayers;
        gunAttack.useBurstCombatCycle = true;
        gunAttack.shootBurstDuration = 4f;
        gunAttack.restDuration = 5f;
        gunAttack.restRepositionRadius = 11f;
        gunAttack.restRepositionInterval = 1.1f;
        gunAttack.minimumPlayerDistance = 10f;
        gunAttack.preferredPlayerDistance = 15f;
        gunAttack.bulletVisualPrefab = FinalSceneRuntimeEffects.FindTemplate("BulletEnemy");
        gunAttack.bulletVisualSpeed = 95f;
        gunAttack.bulletVisualLifetime = 8f;
        gunAttack.applyDamageOnBulletImpact = true;
        gunAttack.dodgeableProjectiles = true;
        gunAttack.projectileCollisionRadius = 0.36f;
        gunAttack.projectileHitMask = ~0;
        gunAttack.muzzleParticlePrefab = FinalSceneRuntimeEffects.FindTemplate("ex");
        gunAttack.hitParticlePrefab = FinalSceneRuntimeEffects.FindTemplate("ex");
        gunAttack.createFallbackGun = true;
        gunAttack.hideGunWhileMoving = false;
        gunAttack.drawDebugRay = false;
    }

    private static GameObject FindRuntimeTemplate(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject resource = Resources.Load<GameObject>(objectName);
        if (resource != null)
        {
            return resource;
        }

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void EnsurePlayerDamageGate(Scene scene, Transform boss)
    {
        vHealthController health = ResolvePlayerHealth();
        if (health == null)
        {
            return;
        }

        playerDamageGate = health.GetComponent<FinalScenePlayerDamageGate>();
        if (playerDamageGate == null)
        {
            playerDamageGate = health.gameObject.AddComponent<FinalScenePlayerDamageGate>();
        }

        playerDamageGate.Bind(health, boss, originalPlayerImmortal, hasCachedPlayerImmortal);
        playerDamageGate.SetFightActive(hasHandedOffToGameplay);
    }

    private static FinalSceneBossHealthBar EnsureBossHealthBar(Scene scene, Transform boss, vHealthController health)
    {
        FinalSceneBossHealthBar existing = boss != null ? boss.GetComponentInChildren<FinalSceneBossHealthBar>(true) : null;
        if (existing == null)
        {
            existing = FindSceneObjectOfType<FinalSceneBossHealthBar>(scene);
        }

        if (existing != null)
        {
            existing.Bind(boss, health, BossObjectName);
            return existing;
        }

        GameObject barObject = new GameObject("Rhea Malik Boss Health Bar");
        SceneManager.MoveGameObjectToScene(barObject, scene);
        FinalSceneBossHealthBar healthBar = barObject.AddComponent<FinalSceneBossHealthBar>();
        healthBar.Bind(boss, health, BossObjectName);
        return healthBar;
    }

    private static FinalSceneBossFightDirector EnsureBossFightDirector(Scene scene, Transform boss, vHealthController health)
    {
        FinalSceneBossFightDirector director = boss != null ? boss.GetComponent<FinalSceneBossFightDirector>() : null;
        if (director == null)
        {
            director = FindSceneObjectOfType<FinalSceneBossFightDirector>(scene);
        }
        if (director == null && boss != null)
        {
            director = boss.gameObject.AddComponent<FinalSceneBossFightDirector>();
        }

        if (director != null)
        {
            director.Bind(boss, health);
        }

        return director;
    }

    private static Camera FindSceneCamera(Scene scene, string cameraName)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null && candidate.gameObject.scene == scene && candidate.name == cameraName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static Camera FindCutsceneCamera(Scene scene)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        Camera namedFallback = null;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || candidate.gameObject.scene != scene || candidate.name != CutsceneCameraName)
            {
                continue;
            }

            if (namedFallback == null)
            {
                namedFallback = candidate;
            }

            if (candidate.GetComponent<FinalSceneRifleCutsceneShooter>() != null)
            {
                return candidate;
            }

            if (candidate.GetComponent<Animator>() != null)
            {
                return candidate;
            }
        }

        return namedFallback;
    }

    private static GameObject FindSceneGameObject(Scene scene, string objectName)
    {
        Transform transform = FindSceneTransform(scene, objectName);
        return transform != null ? transform.gameObject : null;
    }

    private static Transform FindSceneTransform(Scene scene, string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null && candidate.scene == scene && candidate.name == objectName)
            {
                return candidate.transform;
            }
        }

        return null;
    }

    private static void SetParentsActive(Transform child)
    {
        Transform current = child != null ? child.parent : null;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            current = current.parent;
        }
    }

    private static GameObject FindSceneObjectWithTag(Scene scene, string tagName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate == null || candidate.scene != scene)
            {
                continue;
            }

            try
            {
                if (candidate.CompareTag(tagName))
                {
                    return candidate;
                }
            }
            catch (UnityException)
            {
                return null;
            }
        }

        return null;
    }

    private static T FindSceneObjectOfType<T>(Scene scene) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.gameObject.scene == scene)
            {
                return component;
            }
        }

        return null;
    }
}

public class FinalSceneBossHealthBar : MonoBehaviour
{
    private static readonly Vector3 WorldOffset = new Vector3(0f, 2.55f, 0f);
    private static readonly Vector2 CanvasSize = new Vector2(240f, 58f);
    private const float WorldScale = 0.01f;

    private Transform boss;
    private vHealthController health;
    private Slider slider;
    private TMP_Text titleText;
    private TMP_Text valueText;
    private Canvas canvas;
    private Camera targetCamera;
    private bool visibleAllowed = true;

    public void Bind(Transform bossTransform, vHealthController targetHealth, string bossName)
    {
        boss = bossTransform;
        health = targetHealth;

        if (boss != null && transform.parent != boss)
        {
            transform.SetParent(boss, false);
            transform.localPosition = WorldOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * WorldScale;
        }

        BuildUi(bossName);
        UpdateUi();
    }

    public void SetVisibleAllowed(bool allowed)
    {
        visibleAllowed = allowed;
        UpdateUi();
    }

    private void LateUpdate()
    {
        UpdateUi();

        if (canvas == null || !canvas.enabled)
        {
            return;
        }

        if (boss != null)
        {
            transform.position = boss.position + WorldOffset;
        }

        if (targetCamera == null || !targetCamera.enabled)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position, Vector3.up);
        }
    }

    private void BuildUi(string bossName)
    {
        if (canvas != null)
        {
            if (titleText != null)
            {
                titleText.text = "FINAL BOSS  " + bossName;
            }
            return;
        }

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 24f;

        RectTransform root = gameObject.GetComponent<RectTransform>();
        root.sizeDelta = CanvasSize;

        GameObject frameObject = CreateImage("Frame", transform, new Color(0.01f, 0.012f, 0.015f, 0.88f));
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        GameObject accentObject = CreateImage("Accent", frameObject.transform, new Color(0.72f, 0.06f, 0.03f, 1f));
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0.88f);
        accentRect.anchorMax = Vector2.one;
        accentRect.offsetMin = Vector2.zero;
        accentRect.offsetMax = Vector2.zero;

        titleText = CreateText("Title", frameObject.transform, "FINAL BOSS  " + bossName, 18, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.72f, 1f));
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.46f);
        titleRect.anchorMax = new Vector2(1f, 0.88f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        titleRect.anchoredPosition = Vector2.zero;

        GameObject backgroundObject = CreateImage("Background", frameObject.transform, new Color(0.07f, 0.07f, 0.075f, 1f));
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.04f, 0.1f);
        backgroundRect.anchorMax = new Vector2(0.96f, 0.42f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject fillObject = CreateImage("Fill", backgroundObject.transform, new Color(0.85f, 0.02f, 0.03f, 1f));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        slider = backgroundObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f;
        slider.targetGraphic = fillObject.GetComponent<Image>();
        slider.fillRect = fillRect;

        valueText = CreateText("Value", backgroundObject.transform, string.Empty, 12, TextAlignmentOptions.Center, Color.white);
        RectTransform valueRect = valueText.GetComponent<RectTransform>();
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = Vector2.zero;

        targetCamera = Camera.main;
    }

    private void UpdateUi()
    {
        if (health == null)
        {
            return;
        }

        bool visible = visibleAllowed && !health.isDead && health.currentHealth > 0f;
        if (canvas != null && canvas.enabled != visible)
        {
            canvas.enabled = visible;
        }

        if (!visible || slider == null)
        {
            return;
        }

        slider.maxValue = Mathf.Max(1f, health.MaxHealth);
        slider.value = health.currentHealth;

        if (valueText != null)
        {
            valueText.text = Mathf.CeilToInt(health.currentHealth) + " / " + health.MaxHealth;
        }
    }

    private static GameObject CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return imageObject;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, string text, int size, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        TMP_Text uiText = textObject.AddComponent<TextMeshProUGUI>();
        uiText.text = text;
        uiText.font = FinalSceneRuntimeFont.GetBossFont();
        uiText.fontSize = size;
        uiText.alignment = alignment;
        uiText.color = color;
        uiText.raycastTarget = false;
        uiText.enableWordWrapping = false;
        uiText.overflowMode = TextOverflowModes.Ellipsis;
        return uiText;
    }
}

[DisallowMultipleComponent]
public class FinalSceneBossFightDirector : MonoBehaviour
{
    private const int GuardHealth = 80;
    private const float EnrageHealthPercent = 0.4f;

    private Transform boss;
    private vHealthController bossHealth;
    private vSimpleMeleeAI_Controller bossAi;
    private bool fightActive;
    private bool enraged;
    private bool victoryStarted;
    private GameObject arenaRoot;
    private GameObject alarmObject;

    public void Bind(Transform bossTransform, vHealthController health)
    {
        if (bossHealth != null)
        {
            bossHealth.onDead.RemoveListener(HandleBossDead);
        }

        boss = bossTransform;
        bossHealth = health;
        bossAi = boss != null ? boss.GetComponent<vSimpleMeleeAI_Controller>() : null;
        ConfigureBossAi(false);

        if (bossHealth != null)
        {
            bossHealth.onDead.AddListener(HandleBossDead);
        }
    }

    public void SetFightActive(bool active)
    {
        fightActive = active;
        if (!active)
        {
            return;
        }

        ConfigureBossAi(false);
        CreateWarningAlarm();
    }

    private void OnEnable()
    {
        if (bossHealth != null)
        {
            bossHealth.onDead.AddListener(HandleBossDead);
        }
    }

    private void OnDisable()
    {
        if (bossHealth != null)
        {
            bossHealth.onDead.RemoveListener(HandleBossDead);
        }
    }

    private void Update()
    {
        if (!fightActive || bossHealth == null || bossHealth.isDead)
        {
            return;
        }

        float healthPercent = bossHealth.currentHealth / Mathf.Max(1f, bossHealth.MaxHealth);
        if (!enraged && healthPercent <= EnrageHealthPercent)
        {
            enraged = true;
            ConfigureBossAi(true);
            IntensifyAlarm();
        }
    }

    private void ConfigureBossAi(bool enrage)
    {
        if (bossAi == null)
        {
            return;
        }

        bossAi.tagsToDetect.Clear();
        bossAi.tagsToDetect.Add("Player");
        bossAi.layersToDetect = LayerMaskFor("Player", "BodyPart") | LayerMaskFromLayers(8, 15);
        bossAi.agressiveAtFirstSight = true;
        bossAi.sortTargetFromDistance = true;
        bossAi.fieldOfView = 220f;
        bossAi.maxDetectDistance = enrage ? 170f : 160f;
        bossAi.minDetectDistance = 5f;
        bossAi.distanceToLostTarget = enrage ? 180f : 170f;
        bossAi.chaseStopDistance = enrage ? 13f : 15f;
        bossAi.chaseSpeed = enrage ? 1.8f : 1.25f;
        bossAi.strafeDistance = enrage ? 15f : 17f;
        bossAi.strafeSpeed = enrage ? 2.1f : 1.45f;
        bossAi.attackRotationSpeed = enrage ? 3.2f : 1.8f;
        bossAi.firstAttackDelay = enrage ? 0.6f : 1.2f;
        bossAi.minTimeToAttack = enrage ? 3f : 4.5f;
        bossAi.maxTimeToAttack = enrage ? 4.5f : 6.5f;
        bossAi.maxAttackCount = enrage ? 2 : 1;
        bossAi.randomAttackCount = true;
        bossAi.chanceToBlockAttack = enrage ? 0.25f : 0.1f;

        Invector.vMelee.vMeleeManager meleeManager = boss.GetComponent<Invector.vMelee.vMeleeManager>();
        if (meleeManager != null)
        {
            meleeManager.defaultAttackDistance = enrage ? 10f : 12f;
        }
    }

    private void SpawnBossGuards()
    {
        if (boss == null)
        {
            return;
        }

        Vector3[] offsets =
        {
            boss.right * 3.5f - boss.forward * 2f,
            -boss.right * 3.5f - boss.forward * 2f
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject guard = Instantiate(boss.gameObject, boss.position + offsets[i], boss.rotation);
            guard.name = "Rhea Malik Guard " + (i + 1);
            guard.transform.localScale = boss.localScale * 0.88f;

            FinalSceneBossFightDirector clonedDirector = guard.GetComponent<FinalSceneBossFightDirector>();
            if (clonedDirector != null)
            {
                Destroy(clonedDirector);
            }

            FinalSceneBossHealthBar[] clonedBars = guard.GetComponentsInChildren<FinalSceneBossHealthBar>(true);
            for (int barIndex = 0; barIndex < clonedBars.Length; barIndex++)
            {
                if (clonedBars[barIndex] != null)
                {
                    Destroy(clonedBars[barIndex].gameObject);
                }
            }

            vHealthController guardHealth = guard.GetComponent<vHealthController>();
            if (guardHealth != null)
            {
                guardHealth.isImmortal = false;
                guardHealth.fillHealthOnStart = false;
                guardHealth.maxHealth = GuardHealth;
                guardHealth.ResetHealth(GuardHealth);
            }

            vSimpleMeleeAI_Controller guardAi = guard.GetComponent<vSimpleMeleeAI_Controller>();
            if (guardAi != null)
            {
                guardAi.tagsToDetect.Clear();
                guardAi.tagsToDetect.Add("Player");
                guardAi.layersToDetect = LayerMaskFor("Player", "BodyPart") | LayerMaskFromLayers(8, 15);
                guardAi.maxDetectDistance = 22f;
                guardAi.minDetectDistance = 1.25f;
                guardAi.chaseSpeed = 1.7f;
                guardAi.strafeSpeed = 1.35f;
                guardAi.firstAttackDelay = 0.2f;
                guardAi.minTimeToAttack = 1.8f;
                guardAi.maxTimeToAttack = 2.8f;
                guardAi.agressiveAtFirstSight = true;
            }

            NavMeshAgent agent = guard.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(guard.transform.position, out hit, 5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }

            FinalSceneBossBootstrapHelper.SetEnemyIdentity(guard);
        }
    }

    private void CreateArenaSetup()
    {
        if (arenaRoot != null || boss == null)
        {
            return;
        }

        arenaRoot = new GameObject("FinalScene Boss Arena Runtime Props");
        SceneManager.MoveGameObjectToScene(arenaRoot, gameObject.scene);

        Transform player = FindPlayer();
        Vector3 center = boss.position;
        if (player != null)
        {
            center = (boss.position + player.position) * 0.5f;
        }

        float halfX = 11f;
        float halfZ = 11f;
        if (player != null)
        {
            Vector3 localPlayer = player.position - center;
            Vector3 localBoss = boss.position - center;
            halfX = Mathf.Max(halfX, Mathf.Abs(localPlayer.x), Mathf.Abs(localBoss.x)) + 5f;
            halfZ = Mathf.Max(halfZ, Mathf.Abs(localPlayer.z), Mathf.Abs(localBoss.z)) + 5f;
        }

        CreateWall(center + new Vector3(0f, 1.2f, halfZ), new Vector3(halfX * 2f, 2.4f, 0.45f));
        CreateWall(center + new Vector3(0f, 1.2f, -halfZ), new Vector3(halfX * 2f, 2.4f, 0.45f));
        CreateWall(center + new Vector3(halfX, 1.2f, 0f), new Vector3(0.45f, 2.4f, halfZ * 2f));
        CreateWall(center + new Vector3(-halfX, 1.2f, 0f), new Vector3(0.45f, 2.4f, halfZ * 2f));

        CreateCover(center + new Vector3(-3.5f, 0.75f, 2.8f), new Vector3(3.1f, 1.5f, 0.8f));
        CreateCover(center + new Vector3(4.2f, 0.75f, -1.8f), new Vector3(3.4f, 1.5f, 0.8f));
        CreateCover(center + new Vector3(0.8f, 0.65f, 4.8f), new Vector3(2.1f, 1.3f, 0.8f));

        CreateExplosiveBarrel(center + new Vector3(-5.3f, 0.75f, -3.3f));
        CreateExplosiveBarrel(center + new Vector3(5.5f, 0.75f, 3.2f));
    }

    private void CreateWall(Vector3 position, Vector3 scale)
    {
        GameObject wall = CreatePrimitiveProp("Arena Lock Barrier", PrimitiveType.Cube, position, scale, new Color(0.12f, 0.13f, 0.14f, 0.9f));
        wall.transform.SetParent(arenaRoot.transform, true);
    }

    private void CreateCover(Vector3 position, Vector3 scale)
    {
        GameObject cover = CreatePrimitiveProp("Tactical Cover", PrimitiveType.Cube, position, scale, new Color(0.28f, 0.28f, 0.25f, 1f));
        cover.transform.SetParent(arenaRoot.transform, true);
    }

    private void CreateExplosiveBarrel(Vector3 position)
    {
        GameObject barrel = CreatePrimitiveProp("Explosive Barrel", PrimitiveType.Cylinder, position, new Vector3(0.85f, 0.75f, 0.85f), new Color(0.7f, 0.05f, 0.02f, 1f));
        barrel.transform.SetParent(arenaRoot.transform, true);
        FinalSceneBossBootstrapHelper.SetEnemyIdentity(barrel);

        vHealthController health = barrel.AddComponent<vHealthController>();
        health.fillHealthOnStart = false;
        health.maxHealth = 35;
        health.ResetHealth(35);

        FinalSceneBossDamageReceiver receiver = barrel.AddComponent<FinalSceneBossDamageReceiver>();
        receiver.ownerHealth = health;

        FinalSceneExplosiveBarrel explosive = barrel.AddComponent<FinalSceneExplosiveBarrel>();
        explosive.enemyDamage = 85;
        explosive.radius = 5.2f;
    }

    private GameObject CreatePrimitiveProp(string objectName, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color)
    {
        GameObject prop = GameObject.CreatePrimitive(primitive);
        prop.name = objectName;
        prop.transform.position = position;
        prop.transform.localScale = scale;

        Renderer renderer = prop.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader standardShader = Shader.Find("Standard");
            if (standardShader != null)
            {
                renderer.material = new Material(standardShader);
            }
            renderer.material.color = color;
        }

        return prop;
    }

    private void CreateWarningAlarm()
    {
        if (alarmObject != null || boss == null)
        {
            return;
        }

        alarmObject = new GameObject("FinalScene Boss Warning Alarm");
        SceneManager.MoveGameObjectToScene(alarmObject, gameObject.scene);
        alarmObject.transform.position = boss.position + Vector3.up * 4.5f;

        Light warningLight = alarmObject.AddComponent<Light>();
        warningLight.type = LightType.Point;
        warningLight.color = Color.red;
        warningLight.range = 16f;
        warningLight.intensity = 2.5f;

        AudioSource audio = alarmObject.AddComponent<AudioSource>();
        audio.clip = FinalSceneSirenClip.Create();
        audio.loop = true;
        audio.spatialBlend = 0.55f;
        audio.volume = 0.35f;
        audio.Play();

        FinalSceneBossAlarm alarm = alarmObject.AddComponent<FinalSceneBossAlarm>();
        alarm.warningLight = warningLight;
        alarm.audioSource = audio;
    }

    private void IntensifyAlarm()
    {
        if (alarmObject == null)
        {
            return;
        }

        FinalSceneBossAlarm alarm = alarmObject.GetComponent<FinalSceneBossAlarm>();
        if (alarm != null)
        {
            alarm.Intensify();
        }
    }

    private void HandleBossDead(GameObject deadBoss)
    {
        if (victoryStarted)
        {
            return;
        }

        victoryStarted = true;
        fightActive = false;
        StartCoroutine(PlayVictorySequence());
    }

    private IEnumerator PlayVictorySequence()
    {
        if (alarmObject != null)
        {
            AudioSource audio = alarmObject.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.Stop();
            }
        }

        Camera previousCamera = Camera.main;
        GameObject victoryCameraObject = new GameObject("FinalScene Victory Camera");
        SceneManager.MoveGameObjectToScene(victoryCameraObject, gameObject.scene);
        Camera victoryCamera = victoryCameraObject.AddComponent<Camera>();
        AudioListener listener = victoryCameraObject.AddComponent<AudioListener>();

        if (boss != null)
        {
            Vector3 focus = boss.position + Vector3.up * 1.1f;
            victoryCameraObject.transform.position = focus + new Vector3(0f, 3f, -5.5f);
            victoryCameraObject.transform.LookAt(focus);
        }

        if (previousCamera != null)
        {
            previousCamera.enabled = false;
            AudioListener previousListener = previousCamera.GetComponent<AudioListener>();
            if (previousListener != null)
            {
                previousListener.enabled = false;
            }
        }

        victoryCamera.enabled = true;
        listener.enabled = true;
        CreateMissionCompleteUi();

        yield return new WaitForSeconds(4f);

        if (previousCamera != null)
        {
            previousCamera.enabled = true;
            AudioListener previousListener = previousCamera.GetComponent<AudioListener>();
            if (previousListener != null)
            {
                previousListener.enabled = true;
            }
        }

        Destroy(victoryCameraObject);
    }

    private void CreateMissionCompleteUi()
    {
        GameObject canvasObject = new GameObject("FinalScene Mission Complete UI");
        SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, 0.56f);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        TMP_Text title = CreateUiText("Mission Complete", panelObject.transform, "MISSION COMPLETE", 54, new Color(1f, 0.92f, 0.72f, 1f));
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.2f, 0.52f);
        titleRect.anchorMax = new Vector2(0.8f, 0.64f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        TMP_Text subtitle = CreateUiText("Subtitle", panelObject.transform, "Rhea Malik defeated", 28, Color.white);
        RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.2f, 0.45f);
        subtitleRect.anchorMax = new Vector2(0.8f, 0.52f);
        subtitleRect.offsetMin = Vector2.zero;
        subtitleRect.offsetMax = Vector2.zero;
    }

    private TMP_Text CreateUiText(string objectName, Transform parent, string text, int size, Color color)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        TMP_Text uiText = textObject.AddComponent<TextMeshProUGUI>();
        uiText.text = text;
        uiText.font = FinalSceneRuntimeFont.GetBossFont();
        uiText.fontSize = size;
        uiText.alignment = TextAlignmentOptions.Center;
        uiText.color = color;
        uiText.raycastTarget = false;
        uiText.enableWordWrapping = false;
        return uiText;
    }

    private Transform FindPlayer()
    {
        GameObject player = null;
        try
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
        }

        return player != null ? player.transform : null;
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

    private static LayerMask LayerMaskFromLayers(params int[] layers)
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
}

public static class FinalSceneRuntimeFont
{
    private static TMP_FontAsset cachedBossFont;

    public static TMP_FontAsset GetBossFont()
    {
        if (cachedBossFont != null)
        {
            return cachedBossFont;
        }

        cachedBossFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (cachedBossFont == null)
        {
            cachedBossFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF");
        }
        if (cachedBossFont == null)
        {
            cachedBossFont = TMP_Settings.defaultFontAsset;
        }

        return cachedBossFont;
    }
}

public class FinalSceneBossDamageGate : MonoBehaviour
{
    private vHealthController health;

    public void Bind(vHealthController targetHealth)
    {
        if (health != null)
        {
            health.onStartReceiveDamage.RemoveListener(FilterDamage);
        }

        health = targetHealth;
        if (health != null)
        {
            health.onStartReceiveDamage.AddListener(FilterDamage);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.onStartReceiveDamage.RemoveListener(FilterDamage);
        }
    }

    private void FilterDamage(vDamage damage)
    {
        if (!FinalSceneBossDamageRules.IsPlayerShotDamage(damage))
        {
            damage.damageValue = 0f;
        }
    }
}

public class FinalScenePlayerDamageGate : MonoBehaviour
{
    private vHealthController playerHealth;
    private Transform finalBoss;
    private bool originalImmortal;
    private bool hasOriginalImmortal;
    private bool fightActive;
    private bool applyingApprovedDamage;

    public void Bind(vHealthController health, Transform boss, bool cachedOriginalImmortal, bool hasCachedOriginal)
    {
        if (playerHealth != null)
        {
            playerHealth.onStartReceiveDamage.RemoveListener(HandleIncomingDamage);
        }

        playerHealth = health;
        finalBoss = boss;
        originalImmortal = cachedOriginalImmortal;
        hasOriginalImmortal = hasCachedOriginal;

        if (playerHealth == null)
        {
            return;
        }

        playerHealth.isImmortal = true;
        playerHealth.onStartReceiveDamage.AddListener(HandleIncomingDamage);
    }

    public void SetFightActive(bool active)
    {
        fightActive = active;
    }

    public bool ApplyApprovedBossDamage(vDamage damage, Vector3 hitPosition)
    {
        if (playerHealth == null || damage == null || damage.damageValue <= 0f || !IsFinalBossDamage(damage))
        {
            return false;
        }

        MakeDamageFlowSafe(damage);
        damage.receiver = playerHealth.transform;
        damage.hitPosition = hitPosition;
        ApplyHealthChange(damage.damageValue);
        return true;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.onStartReceiveDamage.RemoveListener(HandleIncomingDamage);
            if (hasOriginalImmortal)
            {
                playerHealth.isImmortal = originalImmortal;
            }
        }
    }

    private void HandleIncomingDamage(vDamage damage)
    {
        if (applyingApprovedDamage || playerHealth == null || damage == null || damage.damageValue <= 0f)
        {
            return;
        }

        if (!fightActive || !IsFinalBossDamage(damage))
        {
            damage.damageValue = 0f;
            return;
        }

        MakeDamageFlowSafe(damage);
        ApplyHealthChange(damage.damageValue);
        damage.damageValue = 0f;
    }

    private void ApplyHealthChange(float damageValue)
    {
        float newHealth = Mathf.Max(0f, playerHealth.currentHealth - damageValue);
        applyingApprovedDamage = true;
        playerHealth.ChangeHealth(Mathf.RoundToInt(newHealth));
        applyingApprovedDamage = false;
    }

    private static void MakeDamageFlowSafe(vDamage damage)
    {
        if (damage == null)
        {
            return;
        }

        damage.hitReaction = false;
        damage.activeRagdoll = false;
        damage.senselessTime = 0f;
        damage.reaction_id = -1;
        damage.recoil_id = -1;
        damage.ignoreDefense = true;
    }

    private bool IsFinalBossDamage(vDamage damage)
    {
        Transform sender = damage.sender;
        if (sender == null)
        {
            return false;
        }

        if (finalBoss != null && (sender == finalBoss || sender.IsChildOf(finalBoss)))
        {
            return true;
        }

        Transform current = sender;
        while (current != null)
        {
            if (current.GetComponent<FinalSceneBossFightDirector>() != null || current.GetComponent<EnemyGunAttack>() != null)
            {
                return IsFinalEnemyName(current.name);
            }

            if (IsFinalEnemyName(current.name))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsFinalEnemyName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName) &&
               (objectName.Contains("Rhea Malik") || objectName.Contains("FInalEnemy") || objectName.Contains("FinalEnemy"));
    }
}

public static class FinalSceneBossBootstrapHelper
{
    public static void SetEnemyIdentity(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        try
        {
            target.tag = "Enemy";
        }
        catch (UnityException)
        {
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            target.layer = enemyLayer;
        }
    }
}

public class FinalSceneBossAlarm : MonoBehaviour
{
    public Light warningLight;
    public AudioSource audioSource;

    private float baseIntensity = 2.5f;
    private float pulseSpeed = 4f;

    private void Update()
    {
        if (warningLight == null)
        {
            return;
        }

        warningLight.intensity = baseIntensity + Mathf.Abs(Mathf.Sin(Time.time * pulseSpeed)) * baseIntensity;
    }

    public void Intensify()
    {
        baseIntensity = 4.5f;
        pulseSpeed = 7f;
        if (audioSource != null)
        {
            audioSource.volume = 0.55f;
            audioSource.pitch = 1.15f;
        }
    }
}

public static class FinalSceneSirenClip
{
    private static AudioClip cachedClip;

    public static AudioClip Create()
    {
        if (cachedClip != null)
        {
            return cachedClip;
        }

        const int sampleRate = 22050;
        const float duration = 1.25f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float sweep = Mathf.PingPong(t * 2f, 1f);
            float frequency = Mathf.Lerp(540f, 880f, sweep);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.22f;
        }

        cachedClip = AudioClip.Create("FinalSceneRuntimeSiren", sampleCount, 1, sampleRate, false);
        cachedClip.SetData(samples, 0);
        return cachedClip;
    }
}

public class FinalSceneExplosiveBarrel : MonoBehaviour
{
    public float radius = 5f;
    public int enemyDamage = 80;

    private bool exploded;
    private vHealthController health;

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
            health.onDead.AddListener(Explode);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.onDead.RemoveListener(Explode);
        }
    }

    private void Explode(GameObject deadBarrel)
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            vHealthController targetHealth = hit.GetComponentInParent<vHealthController>();
            if (targetHealth == null || targetHealth == health || targetHealth.isDead)
            {
                continue;
            }

            if (!IsEnemyTarget(targetHealth.gameObject))
            {
                continue;
            }

            vDamage damage = new vDamage
            {
                damageValue = enemyDamage,
                sender = transform,
                receiver = targetHealth.transform,
                hitPosition = hit.transform.position,
                force = (hit.transform.position - transform.position).normalized * enemyDamage
            };
            targetHealth.TakeDamage(damage);
        }

        StartCoroutine(ExplosionVisual());
    }

    private IEnumerator ExplosionVisual()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }

        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        GameObject flashObject = new GameObject("Explosion Flash");
        flashObject.transform.position = transform.position + Vector3.up * 0.6f;
        Light flash = flashObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.35f, 0.05f, 1f);
        flash.range = radius * 1.5f;
        flash.intensity = 8f;

        float endTime = Time.time + 0.35f;
        while (Time.time < endTime)
        {
            flash.intensity = Mathf.Lerp(8f, 0f, (Time.time - (endTime - 0.35f)) / 0.35f);
            yield return null;
        }

        Destroy(flashObject);
        Destroy(gameObject);
    }

    private static bool IsEnemyTarget(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        try
        {
            if (target.CompareTag("Enemy"))
            {
                return true;
            }
        }
        catch (UnityException)
        {
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        return enemyLayer >= 0 && target.layer == enemyLayer;
    }
}

public class FinalSceneBossDamageReceiver : MonoBehaviour, vIDamageReceiver, vIAttackReceiver
{
    public vHealthController ownerHealth;
    public bool onlyPlayerShotDamage;

    private readonly OnReceiveDamage startReceiveDamage = new OnReceiveDamage();
    private readonly OnReceiveDamage receiveDamage = new OnReceiveDamage();

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

        if (ownerHealth == null || damage == null || ownerHealth.isDead)
        {
            return;
        }

        if (onlyPlayerShotDamage && !FinalSceneBossDamageRules.IsPlayerShotDamage(damage))
        {
            return;
        }

        startReceiveDamage.Invoke(damage);
        damage.receiver = ownerHealth.transform;
        ownerHealth.TakeDamage(damage);
        receiveDamage.Invoke(damage);
    }

}

public static class FinalSceneBossDamageRules
{
    public static bool IsPlayerShotDamage(vDamage damage)
    {
        if (damage == null || damage.sender == null)
        {
            return false;
        }

        Transform sender = damage.sender;
        if (sender.GetComponentInParent<PlayerWeaponPointFix>() != null)
        {
            return true;
        }

        try
        {
            if (sender.CompareTag("Player") || sender.root.CompareTag("Player"))
            {
                return true;
            }
        }
        catch (UnityException)
        {
        }

        return false;
    }
}
