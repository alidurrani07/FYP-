using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Invector.vCamera;
using Invector.vCharacterController;
using Invector.vCharacterController.AI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DemoSceneExtractionSequence : MonoBehaviour
{
    private const string DemoSceneOneName = "Demo Scene 1";
    private const string DemoSceneTwoName = "Demo Scene 2";
    private const string ExitTriggerName = "exitTrigger";
    private const string ExitCamera01Name = "exitCamera01";
    private const string ExitCamera02Name = "exitCamera02";
    private const string CompanionTriggerName = "companionTrigger";
    private const string TruckName = "Truck";
    private const string FinalSceneName = "FinalScene";
    private const string FinalSceneOneName = "FinalScene 1";
    private const string ExplosionSoundResourceName = "explosion";

    [Header("Timing")]
    public float exitCamera01Duration = 6f;
    public float exitCamera02Duration = 2.8f;
    public float playerRunTimeout = 10f;
    public float companionBoardTimeout = 7f;
    public float truckDriveDuration = 4.25f;
    public float explosionShotDuration = 4f;
    [Range(0f, 1f)]
    public float explosionSoundVolume = 1f;

    [Header("Movement")]
    public float playerBoardDistance = 0.9f;
    public float companionBoardDistance = 1.8f;
    public float playerCinematicRunSpeed = 5.5f;
    public float companionCinematicRunSpeed = 4.5f;
    public float navMeshSampleRadius = 8f;
    public float truckDriveDistance = 75f;

    private DemoSceneBombMission mission;
    private GameObject exitTrigger;
    private GameObject companionTrigger;
    private Transform doorPoint;
    private Transform seatPoint;
    private Transform truck;
    private Transform explosion01;
    private Transform explosion02;
    private Transform bombArea01;
    private Transform bombArea02;
    private Camera explosionCamera01;
    private Camera explosionCamera02;
    private AudioClip explosionSound;
    private GameObject exitMarker;
    private bool exitObjectiveActive;
    private bool sequenceStarted;
    private Camera mainCamera;
    private readonly List<Camera> cinematicCameras = new List<Camera>();
    private readonly List<Camera> runtimeCameras = new List<Camera>();
    private readonly List<Tween> activeTweens = new List<Tween>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateForDemoScene()
    {
        EnsureForActiveScene();
    }

    private void Awake()
    {
        if (!IsDemoScene(gameObject.scene.name))
            return;

        mainCamera = Camera.main;
        LoadExplosionSound();
        ResolveSceneReferences();
    }

    public static DemoSceneExtractionSequence EnsureForActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !IsDemoScene(scene.name))
            return null;

        DemoSceneExtractionSequence existing = FindSceneObjectOfType<DemoSceneExtractionSequence>(scene);
        if (existing != null)
            return existing;

        GameObject runner = new GameObject("DemoSceneExtractionSequence");
        SceneManager.MoveGameObjectToScene(runner, scene);
        return runner.AddComponent<DemoSceneExtractionSequence>();
    }

    private void Start()
    {
        if (!IsDemoScene(gameObject.scene.name))
            return;

        PrepareExitObjective(FindObjectOfType<DemoSceneBombMission>());
    }

    private void OnDisable()
    {
        KillActiveTweens();
    }

    public void PrepareExitObjective(DemoSceneBombMission missionSource)
    {
        mission = missionSource != null ? missionSource : mission;
        ResolveSceneReferences();

        if (exitTrigger == null)
        {
            Debug.LogWarning("DemoSceneExtractionSequence could not find exitTrigger.");
            return;
        }

        PrepareTrigger(exitTrigger, true);
        if (exitMarker == null)
            exitMarker = CreatePinkMarker(exitTrigger.transform, "VCStyleExitMarker", 1.35f);

        exitMarker.SetActive(false);
        exitObjectiveActive = false;
    }

    public void ActivateExitObjective(DemoSceneBombMission missionSource)
    {
        mission = missionSource != null ? missionSource : mission;
        PrepareExitObjective(mission);

        exitObjectiveActive = true;
        if (exitMarker != null)
            exitMarker.SetActive(true);

        if (mission != null)
            mission.SetMissionStatus("All bombs placed. Reach the pink exit marker for extraction.");
    }

    public void HandleExitTrigger(Collider other)
    {
        if (!exitObjectiveActive || sequenceStarted || !IsPlayer(other))
            return;

        GameObject player = ResolvePlayerObject(other);
        if (player == null)
            return;

        sequenceStarted = true;
        exitObjectiveActive = false;
        if (exitMarker != null)
            exitMarker.SetActive(false);

        StartCoroutine(PlayExtraction(player));
    }

    private IEnumerator PlayExtraction(GameObject player)
    {
        ResolveSceneReferences();

        if (truck == null)
        {
            Debug.LogWarning("DemoSceneExtractionSequence could not find Truck.");
            sequenceStarted = false;
            yield break;
        }

        EnsureDoorAndSeatPoints();
        if (mission != null)
            mission.SetMissionStatus("Extraction started. Move to the truck.");

        mainCamera = Camera.main;
        CreateCinematicCameras(player.transform);
        CanvasGroup letterbox = CreateLetterbox();

        vThirdPersonInput playerInput = player.GetComponent<vThirdPersonInput>();
        RebelGTAThirdPersonCamera gtaCamera = FindObjectOfType<RebelGTAThirdPersonCamera>();
        vThirdPersonCamera invectorCamera = FindObjectOfType<vThirdPersonCamera>();

        ForcePlayerIdentity(player);

        if (playerInput != null)
        {
            playerInput.SetLockAllInput(true);
            playerInput.SetLockCameraInput(true);
        }
        if (gtaCamera != null)
            gtaCamera.enabled = false;
        if (invectorCamera != null)
            invectorCamera.isFreezed = true;

        List<vSimpleMeleeAI_Companion> companions = CollectCompanions();
        RespawnCompanionsAtExitTrigger(companions);
        SendCompanionsToTruck(companions);

        SetMainCameraEnabled(false);
        yield return FadeLetterbox(letterbox, 0f, 1f, 0.2f);
        Coroutine playerRunRoutine = StartCoroutine(RunPlayerToDoorAndDisable(player));
        Coroutine companionRunRoutine = StartCoroutine(SteerCompanionsToTruck(companions));
        SetActiveCinematicCamera(0);
        yield return FadeLetterbox(letterbox, 1f, 0f, 0.35f);
        yield return PlayCameraShot(0, exitCamera01Duration, GetApproachLookAt(player.transform));
        yield return SlideFadeToCamera(letterbox, 1, 0.55f);
        yield return PlayCameraShot(1, exitCamera02Duration, doorPoint.position);
        if (playerRunRoutine != null)
            yield return playerRunRoutine;

        yield return BoardCompanions(companions);
        if (companionRunRoutine != null)
            StopCoroutine(companionRunRoutine);
        HideRemainingCompanions(companions);

        if (mission != null)
            mission.SetMissionStatus("Squad aboard. Extraction complete.");

        yield return SlideFadeToCamera(letterbox, 2, 0.45f);
        yield return DriveTruckAway();
        yield return PlayExplosionShowcase(letterbox);

        CleanupCinematicCameras();
        KillActiveTweens();
        if (letterbox != null)
            Destroy(letterbox.gameObject);

        SceneManager.LoadScene(GetFinalSceneName(SceneManager.GetActiveScene().name));
    }

    private static bool IsDemoScene(string sceneName)
    {
        return sceneName == DemoSceneOneName || sceneName == DemoSceneTwoName;
    }

    private static string GetFinalSceneName(string demoSceneName)
    {
        return demoSceneName == DemoSceneTwoName ? FinalSceneOneName : FinalSceneName;
    }

    private void ResolveSceneReferences()
    {
        Scene scene = SceneManager.GetActiveScene();
        exitTrigger = FindSceneGameObject(scene, ExitTriggerName);
        companionTrigger = FindSceneGameObject(scene, CompanionTriggerName);

        Transform truckTransform = FindSceneTransform(scene, TruckName);
        if (truckTransform != null)
            truck = truckTransform;

        doorPoint = FindSceneTransformAnyCase(scene, "doorTrigger", "DoorTrigger");
        explosion01 = FindSceneTransformLoose(scene, "ex01", "ex01particle", "ex01 particle", "ex01particles", "ex01 particles");
        explosion02 = FindSceneTransformLoose(scene, "ex02", "ex02particle", "ex02 particle", "ex02particles", "ex02 particles");
        bombArea01 = FindSceneTransformLoose(scene, "bomb1area", "bomb01area", "bomb 1 area", "bomb1 area");
        bombArea02 = FindSceneTransformLoose(scene, "bomb2area", "bomb02area", "bomb 2 area", "bomb2 area");
        explosionCamera01 = FindSceneCameraLoose(scene, "ex01camera", "ex01 camera", "ex01cam", "ex01 cam");
        explosionCamera02 = FindSceneCameraLoose(scene, "ex02camera", "ex02 camera", "ex02cam", "ex02 cam");

        PrepareExplosionObject(explosion01);
        PrepareExplosionObject(explosion02);
        PrepareExplosionCamera(explosionCamera01);
        PrepareExplosionCamera(explosionCamera02);
    }

    private void EnsureDoorAndSeatPoints()
    {
        if (truck == null)
            return;

        Bounds bounds = GetBounds(truck);
        Vector3 basePosition = bounds.size == Vector3.zero ? truck.position : bounds.center;
        Vector3 right = FlatDirection(truck.right, Vector3.right);
        Vector3 forward = FlatDirection(truck.forward, Vector3.forward);
        float sideDistance = Mathf.Max(1.5f, bounds.extents.x + 0.9f);

        if (doorPoint == null)
        {
            GameObject doorObject = new GameObject("doorTrigger");
            doorObject.transform.SetParent(truck, true);
            doorObject.transform.position = basePosition - right * sideDistance + forward * 0.4f;
            doorObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            BoxCollider doorCollider = doorObject.AddComponent<BoxCollider>();
            doorCollider.isTrigger = true;
            doorCollider.size = new Vector3(2.2f, 2.6f, 2.2f);
            doorPoint = doorObject.transform;
        }

        if (seatPoint == null)
        {
            GameObject seatObject = new GameObject("RuntimeTruckSeatPoint");
            seatObject.transform.SetParent(truck, true);
            seatObject.transform.position = basePosition + Vector3.up * 0.65f + forward * Mathf.Max(0.2f, bounds.extents.z * 0.25f);
            seatObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            seatPoint = seatObject.transform;
        }
    }

    private IEnumerator RunPlayerToDoorAndDisable(GameObject player)
    {
        if (player == null || doorPoint == null)
            yield break;

        PreparePlayerForExtractionNav(player);

        ExtractionCompanionNavDriver driver = player.GetComponent<ExtractionCompanionNavDriver>();
        if (driver == null)
            driver = player.AddComponent<ExtractionCompanionNavDriver>();

        driver.Initialize(doorPoint, playerCinematicRunSpeed, playerBoardDistance, navMeshSampleRadius);

        float elapsed = 0f;
        while (player != null && player.activeInHierarchy && driver != null && !driver.HasArrived && elapsed < playerRunTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (player != null && player.activeSelf)
        {
            SetRunAnimation(player.GetComponentInChildren<Animator>(), false);
            player.SetActive(false);
        }
    }

    private void PreparePlayerForExtractionNav(GameObject player)
    {
        if (player == null)
            return;

        foreach (vThirdPersonInput input in player.GetComponentsInChildren<vThirdPersonInput>(true))
        {
            if (input == null)
                continue;

            input.SetLockAllInput(true);
            input.SetLockCameraInput(true);
            input.enabled = false;
        }

        foreach (vThirdPersonController controller in player.GetComponentsInChildren<vThirdPersonController>(true))
        {
            if (controller == null)
                continue;

            controller.StopCharacter();
            controller.enabled = false;
        }

        EnsurePlayerNavAgent(player);
    }

    private IEnumerator RunPlayerToDoor(GameObject player, vThirdPersonController controller, Animator animator, Vector3 destination)
    {
        float elapsed = 0f;
        NavMeshAgent agent = EnsurePlayerNavAgent(player);
        Vector3 navDestination = SampleNavMeshPosition(destination);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.speed = Mathf.Max(agent.speed, playerCinematicRunSpeed);
            agent.acceleration = Mathf.Max(agent.acceleration, 18f);
            agent.angularSpeed = Mathf.Max(agent.angularSpeed, 360f);
            agent.stoppingDistance = playerBoardDistance;
            agent.SetDestination(navDestination);
        }

        while (player != null && elapsed < playerRunTimeout)
        {
            Vector3 flatDelta = navDestination - player.transform.position;
            flatDelta.y = 0f;
            if (flatDelta.magnitude <= playerBoardDistance)
                break;

            SetRunAnimation(animator, true);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(navDestination);
                Vector3 velocity = agent.desiredVelocity.sqrMagnitude > 0.05f ? agent.desiredVelocity : agent.velocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > 0.05f)
                    player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(velocity.normalized), Time.deltaTime * 10f);
            }
            else if (controller != null)
            {
                controller.MoveToPosition(navDestination);
                controller.Sprint(true);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        SetRunAnimation(animator, false);
    }

    private IEnumerator RunPlayerToDoorAndBoard(GameObject player, vThirdPersonController controller, Animator animator, Vector3 destination)
    {
        yield return RunPlayerToDoor(player, controller, animator, destination);
        BoardPlayer(player, controller);
    }

    private void BoardPlayer(GameObject player, vThirdPersonController controller)
    {
        if (player == null || seatPoint == null)
            return;

        if (controller != null)
            controller.StopCharacter();

        player.transform.SetParent(truck, true);
        player.transform.position = seatPoint.position;
        player.transform.rotation = seatPoint.rotation;

        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled)
                renderer.enabled = false;
        }
    }

    private List<vSimpleMeleeAI_Companion> CollectCompanions()
    {
        List<vSimpleMeleeAI_Companion> companions = new List<vSimpleMeleeAI_Companion>();
        vSimpleMeleeAI_Companion[] allCompanions = FindObjectsOfType<vSimpleMeleeAI_Companion>(true);
        foreach (vSimpleMeleeAI_Companion companion in allCompanions)
        {
            if (companion != null && companion.gameObject.activeInHierarchy)
                companions.Add(companion);
        }

        return companions;
    }

    private void RespawnCompanionsAtExitTrigger(List<vSimpleMeleeAI_Companion> companions)
    {
        if (exitTrigger == null || companions == null)
            return;

        Vector3 center = SampleNavMeshPosition(exitTrigger.transform.position);
        for (int i = 0; i < companions.Count; i++)
        {
            vSimpleMeleeAI_Companion companion = companions[i];
            if (companion == null)
                continue;

            companion.gameObject.SetActive(true);
            companion.RemoveCurrentTarget();
            companion.agressiveAtFirstSight = false;

            float angle = companions.Count <= 1 ? 0f : (Mathf.PI * 2f * i) / companions.Count;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f;
            Vector3 spawnPosition = SampleNavMeshPosition(center + offset);

            NavMeshAgent agent = EnsureCompanionNavAgent(companion);
            if (agent != null && agent.enabled)
            {
                agent.Warp(spawnPosition);
                agent.isStopped = false;
                agent.ResetPath();
            }
            else
            {
                companion.transform.position = spawnPosition;
            }

            companion.transform.rotation = Quaternion.LookRotation(FlatDirection((companionTrigger != null ? companionTrigger.transform.position : truck.position) - spawnPosition, Vector3.forward), Vector3.up);
        }
    }

    private void SendCompanionsToTruck(List<vSimpleMeleeAI_Companion> companions)
    {
        if (companionTrigger == null || companions == null)
            return;

        Vector3 navDestination = SampleNavMeshPosition(companionTrigger.transform.position);
        foreach (vSimpleMeleeAI_Companion companion in companions)
        {
            if (companion == null)
                continue;

            foreach (AICompanionRuntimeSupport support in companion.GetComponents<AICompanionRuntimeSupport>())
            {
                if (support != null && support.enabled)
                    support.enabled = false;
            }

            companion.agressiveAtFirstSight = false;
            companion.RemoveCurrentTarget();
            companion.SetMoveTo(companionTrigger.transform);
            companion.enabled = false;

            NavMeshAgent agent = EnsureCompanionNavAgent(companion);
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                continue;

            agent.isStopped = false;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.speed = Mathf.Max(agent.speed, companionCinematicRunSpeed);
            agent.acceleration = Mathf.Max(agent.acceleration, 16f);
            agent.angularSpeed = Mathf.Max(agent.angularSpeed, 360f);
            agent.stoppingDistance = 0.25f;
            agent.SetDestination(navDestination);
            SetRunAnimation(companion.GetComponentInChildren<Animator>(), true);
            ExtractionCompanionNavDriver driver = companion.GetComponent<ExtractionCompanionNavDriver>();
            if (driver == null)
                driver = companion.gameObject.AddComponent<ExtractionCompanionNavDriver>();
            driver.Initialize(companionTrigger.transform, companionCinematicRunSpeed, companionBoardDistance, navMeshSampleRadius);
        }
    }

    private IEnumerator SteerCompanionsToTruck(List<vSimpleMeleeAI_Companion> companions)
    {
        while (sequenceStarted && companionTrigger != null && companions != null)
        {
            SendCompanionsToTruck(companions);
            yield return null;
        }
    }

    private IEnumerator BoardCompanions(List<vSimpleMeleeAI_Companion> companions)
    {
        if (companionTrigger == null || companions == null || companions.Count == 0)
            yield break;

        float elapsed = 0f;
        while (elapsed < companionBoardTimeout)
        {
            bool allBoarded = true;
            foreach (vSimpleMeleeAI_Companion companion in companions)
            {
                if (companion == null || !companion.gameObject.activeSelf)
                    continue;

                companion.agressiveAtFirstSight = false;
                companion.RemoveCurrentTarget();
                companion.SetMoveTo(companionTrigger.transform);
                companion.enabled = false;
                NavMeshAgent agent = EnsureCompanionNavAgent(companion);
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.stoppingDistance = 0.25f;
                    agent.SetDestination(SampleNavMeshPosition(companionTrigger.transform.position));
                }

                float distance = Vector3.Distance(FlatPosition(companion.transform.position), FlatPosition(companionTrigger.transform.position));
                if (distance <= companionBoardDistance)
                {
                    SetRunAnimation(companion.GetComponentInChildren<Animator>(), false);
                    companion.gameObject.SetActive(false);
                }
                else
                {
                    allBoarded = false;
                }
            }

            if (allBoarded)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void HideRemainingCompanions(List<vSimpleMeleeAI_Companion> companions)
    {
        if (companions == null)
            return;

        foreach (vSimpleMeleeAI_Companion companion in companions)
        {
            if (companion != null && companion.gameObject.activeSelf)
                companion.gameObject.SetActive(false);
        }
    }

    private IEnumerator DriveTruckAway()
    {
        if (truck == null)
            yield break;

        Vector3 start = truck.position;
        Vector3 driveDirection = Vector3.right;
        Vector3 end = start + driveDirection * truckDriveDistance;

        KillActiveTweens();
        Tween moveTween = truck.DOMove(end, truckDriveDuration).SetEase(Ease.InOutSine);
        activeTweens.Add(moveTween);

        float elapsed = 0f;
        while (elapsed < truckDriveDuration)
        {
            elapsed += Time.deltaTime;

            if (cinematicCameras.Count > 2 && cinematicCameras[2] != null)
                LookAt(cinematicCameras[2].transform, truck.position + Vector3.up * 1.2f);

            yield return null;
        }

        truck.position = end;
        KillActiveTweens();
    }

    private IEnumerator PlayExplosionShowcase(CanvasGroup letterbox)
    {
        ResolveExplosionReferences();

        if (mission != null)
            mission.SetMissionStatus("Charges detonating.");

        yield return PlayExplosionShot(explosion01, explosionCamera01, bombArea01, letterbox, "ExplosionCam_01");
        yield return PlayExplosionShot(explosion02, explosionCamera02, bombArea02, letterbox, "ExplosionCam_02");
        yield return FadeLetterbox(letterbox, 0f, 1f, 0.35f);
    }

    private IEnumerator PlayExplosionShot(Transform particleRoot, Camera shotCamera, Transform bombArea, CanvasGroup letterbox, string fallbackCameraName)
    {
        RotateBombAreaChildren(bombArea);
        Camera activeCamera = shotCamera != null ? shotCamera : CreateFallbackExplosionCamera(fallbackCameraName, particleRoot, bombArea);
        Transform focus = particleRoot != null ? particleRoot : bombArea;
        if (activeCamera == null && focus == null)
            yield break;

        yield return FadeLetterbox(letterbox, 0f, 1f, 0.25f);
        SetOnlyCinematicCamera(activeCamera);
        yield return FadeLetterbox(letterbox, 1f, 0f, 0.25f);

        PlayExplosionObject(particleRoot);
        PlayExplosionSound(focus != null ? GetFocusPoint(focus) : (activeCamera != null ? activeCamera.transform.position : Vector3.zero));

        float elapsed = 0f;
        while (elapsed < explosionShotDuration)
        {
            if (activeCamera != null && focus != null)
                LookAt(activeCamera.transform, GetFocusPoint(focus));

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopExplosionObject(particleRoot);
        if (particleRoot != null)
            particleRoot.gameObject.SetActive(false);
    }

    private void ResolveExplosionReferences()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (explosion01 == null)
            explosion01 = FindSceneTransformLoose(scene, "ex01", "ex01particle", "ex01 particle", "ex01particles", "ex01 particles");
        if (explosion02 == null)
            explosion02 = FindSceneTransformLoose(scene, "ex02", "ex02particle", "ex02 particle", "ex02particles", "ex02 particles");
        if (bombArea01 == null)
            bombArea01 = FindSceneTransformLoose(scene, "bomb1area", "bomb01area", "bomb 1 area", "bomb1 area");
        if (bombArea02 == null)
            bombArea02 = FindSceneTransformLoose(scene, "bomb2area", "bomb02area", "bomb 2 area", "bomb2 area");
        if (explosionCamera01 == null)
            explosionCamera01 = FindSceneCameraLoose(scene, "ex01camera", "ex01 camera", "ex01cam", "ex01 cam");
        if (explosionCamera02 == null)
            explosionCamera02 = FindSceneCameraLoose(scene, "ex02camera", "ex02 camera", "ex02cam", "ex02 cam");

        PrepareExplosionCamera(explosionCamera01);
        PrepareExplosionCamera(explosionCamera02);
        LoadExplosionSound();
    }

    private void RotateBombAreaChildren(Transform bombArea)
    {
        if (bombArea == null)
            return;

        foreach (Transform child in bombArea.GetComponentsInChildren<Transform>(true))
        {
            if (child == bombArea)
                continue;

            Vector3 eulerAngles = child.localEulerAngles;
            child.localEulerAngles = new Vector3(24f, eulerAngles.y, 76f);
        }
    }

    private void PrepareExplosionObject(Transform particleRoot)
    {
        if (particleRoot == null)
            return;

        StopExplosionObject(particleRoot);
        particleRoot.gameObject.SetActive(false);
    }

    private void PlayExplosionObject(Transform particleRoot)
    {
        if (particleRoot == null)
            return;

        particleRoot.gameObject.SetActive(true);
        ParticleSystem[] particles = particleRoot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Clear(true);
            particle.Play(true);
        }
    }

    private void StopExplosionObject(Transform particleRoot)
    {
        if (particleRoot == null)
            return;

        ParticleSystem[] particles = particleRoot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void LoadExplosionSound()
    {
        if (explosionSound != null)
            return;

        explosionSound = Resources.Load<AudioClip>(ExplosionSoundResourceName);
        if (explosionSound == null)
            Debug.LogWarning("DemoSceneExtractionSequence could not load Resources/" + ExplosionSoundResourceName + " as an AudioClip.");
    }

    private void PlayExplosionSound(Vector3 position)
    {
        LoadExplosionSound();
        if (explosionSound == null || explosionSoundVolume <= 0f)
            return;

        GameObject audioObject = new GameObject("RuntimeExplosionSound");
        audioObject.transform.position = position;

        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = explosionSound;
        audioSource.volume = explosionSoundVolume;
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.Play();

        Destroy(audioObject, explosionSound.length + 0.25f);
    }

    private void PrepareExplosionCamera(Camera camera)
    {
        if (camera == null)
            return;

        ConfigureAuthoredSceneCamera(camera);
        camera.depth = mainCamera != null ? mainCamera.depth + 120f : 120f;
    }

    private Camera CreateFallbackExplosionCamera(string cameraName, Transform particleRoot, Transform bombArea)
    {
        Transform focus = particleRoot != null ? particleRoot : bombArea;
        if (focus == null)
            return null;

        Bounds bounds = GetBounds(focus);
        Vector3 focusPoint = bounds.size == Vector3.zero ? focus.position : bounds.center;
        Vector3 position = focusPoint + new Vector3(-7f, 4f, -7f);

        GameObject cameraObject = new GameObject(cameraName);
        cameraObject.transform.position = position;
        LookAt(cameraObject.transform, focusPoint);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 46f;
        camera.depth = mainCamera != null ? mainCamera.depth + 120f : 120f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = mainCamera != null ? mainCamera.farClipPlane : 1000f;
        camera.enabled = false;
        runtimeCameras.Add(camera);
        return camera;
    }

    private void SetOnlyCinematicCamera(Camera activeCamera)
    {
        SetMainCameraEnabled(false);
        foreach (Camera camera in cinematicCameras)
        {
            if (camera != null)
                camera.enabled = false;
        }

        if (explosionCamera01 != null)
            explosionCamera01.enabled = false;
        if (explosionCamera02 != null)
            explosionCamera02.enabled = false;
        if (activeCamera != null)
        {
            activeCamera.gameObject.SetActive(true);
            activeCamera.enabled = true;
        }
    }

    private Vector3 GetFocusPoint(Transform target)
    {
        Bounds bounds = GetBounds(target);
        if (bounds.size != Vector3.zero)
            return bounds.center;

        return target.position;
    }

    private void CreateCinematicCameras(Transform player)
    {
        CleanupCinematicCameras();

        Bounds bounds = GetBounds(truck);
        Vector3 truckCenter = bounds.size == Vector3.zero ? truck.position : bounds.center;
        Vector3 forward = FlatDirection(truck.forward, Vector3.forward);
        Vector3 right = FlatDirection(truck.right, Vector3.right);
        float mainFov = mainCamera != null ? mainCamera.fieldOfView : 60f;
        float truckRadius = Mathf.Max(5f, Mathf.Max(bounds.extents.x, bounds.extents.z));

        Camera exitCamera01 = FindSceneCamera(SceneManager.GetActiveScene(), ExitCamera01Name);
        if (exitCamera01 != null)
        {
            ConfigureAuthoredSceneCamera(exitCamera01);
            cinematicCameras.Add(exitCamera01);
        }
        else
        {
            AddCinematicCamera("ExtractionCam_Approach",
                GetCameraPositionOutsideBounds(truckCenter - forward * (truckRadius + 10f) + right * (truckRadius + 5f) + Vector3.up * 5f, truckCenter, bounds),
                GetApproachLookAt(player),
                54f);
        }

        Camera exitCamera02 = FindSceneCamera(SceneManager.GetActiveScene(), ExitCamera02Name);
        if (exitCamera02 != null)
        {
            ConfigureAuthoredSceneCamera(exitCamera02);
            cinematicCameras.Add(exitCamera02);
        }
        else
        {
            AddCinematicCamera("ExtractionCam_DoorSlide",
                GetCameraPositionOutsideBounds(doorPoint.position - forward * (truckRadius + 5f) - right * (truckRadius + 3f) + Vector3.up * 2.8f, truckCenter, bounds),
                doorPoint.position + Vector3.up * 1.25f,
                46f);
        }

        AddCinematicCamera("ExtractionCam_DriveAway",
            GetCameraPositionOutsideBounds(truckCenter + forward * (truckRadius + 12f) + right * (truckRadius + 4f) + Vector3.up * 3.8f, truckCenter, bounds),
            truckCenter + Vector3.up * 1.2f,
            Mathf.Min(mainFov, 50f));

        SetActiveCinematicCamera(-1);
    }

    private void AddCinematicCamera(string name, Vector3 position, Vector3 lookAt, float fov)
    {
        GameObject cameraObject = new GameObject(name);
        cameraObject.transform.position = position;
        LookAt(cameraObject.transform, lookAt);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = fov;
        camera.depth = mainCamera != null ? mainCamera.depth + 100f : 100f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = mainCamera != null ? mainCamera.farClipPlane : 1000f;
        camera.enabled = false;
        cinematicCameras.Add(camera);
        runtimeCameras.Add(camera);
    }

    private IEnumerator PlayCameraShot(int cameraIndex, float duration, Vector3 lookAt)
    {
        SetActiveCinematicCamera(cameraIndex);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (cameraIndex >= 0 && cameraIndex < cinematicCameras.Count && cinematicCameras[cameraIndex] != null)
            {
                Camera camera = cinematicCameras[cameraIndex];
                if (camera.name == ExitCamera01Name || camera.name == ExitCamera02Name)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                    continue;
                }

                float slideSpeed = cameraIndex == 0 ? 0f : cameraIndex == 1 ? 0.6f : 0.35f;
                if (slideSpeed > 0f)
                    camera.transform.position += camera.transform.right * Time.deltaTime * slideSpeed;
                LookAt(camera.transform, lookAt + Vector3.up * 1.2f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void SetActiveCinematicCamera(int index)
    {
        for (int i = 0; i < cinematicCameras.Count; i++)
        {
            if (cinematicCameras[i] != null)
                cinematicCameras[i].enabled = i == index;
        }
    }

    private void SetMainCameraEnabled(bool enabled)
    {
        if (mainCamera != null)
            mainCamera.enabled = enabled;
    }

    private void RestoreMainCameraState(RebelGTAThirdPersonCamera gtaCamera, vThirdPersonCamera invectorCamera)
    {
        SetMainCameraEnabled(true);
        if (gtaCamera != null)
            gtaCamera.enabled = true;
        if (invectorCamera != null)
            invectorCamera.isFreezed = false;
    }

    private void CleanupCinematicCameras()
    {
        foreach (Camera camera in cinematicCameras)
        {
            if (camera != null)
                camera.enabled = false;
        }

        cinematicCameras.Clear();

        foreach (Camera camera in runtimeCameras)
        {
            if (camera != null)
                Destroy(camera.gameObject);
        }

        runtimeCameras.Clear();
    }

    private void KillActiveTweens()
    {
        foreach (Tween tween in activeTweens)
        {
            if (tween != null && tween.IsActive())
                tween.Kill();
        }

        activeTweens.Clear();
    }

    private void ConfigureSceneCamera(Camera camera, Vector3 position, Vector3 lookAt, float fov)
    {
        if (camera == null)
            return;

        camera.gameObject.SetActive(true);
        camera.transform.position = position;
        LookAt(camera.transform, lookAt);
        camera.fieldOfView = fov;
        camera.depth = mainCamera != null ? mainCamera.depth + 100f : 100f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = mainCamera != null ? mainCamera.farClipPlane : 1000f;
        camera.enabled = false;
    }

    private void ConfigureAuthoredSceneCamera(Camera camera)
    {
        if (camera == null)
            return;

        camera.gameObject.SetActive(true);
        camera.enabled = false;
    }

    private Vector3 GetApproachLookAt(Transform player)
    {
        Vector3 focus = player != null ? player.position : (exitTrigger != null ? exitTrigger.transform.position : Vector3.zero);
        int count = player != null ? 1 : 0;

        if (doorPoint != null)
        {
            focus += doorPoint.position;
            count++;
        }

        if (companionTrigger != null)
        {
            focus += companionTrigger.transform.position;
            count++;
        }

        if (count > 1)
            focus /= count;

        return focus;
    }

    private Vector3 GetCameraPositionOutsideBounds(Vector3 wantedPosition, Vector3 lookAt, Bounds truckBounds)
    {
        if (truckBounds.size == Vector3.zero)
            return wantedPosition;

        Bounds paddedBounds = truckBounds;
        paddedBounds.Expand(3f);

        Vector3 position = wantedPosition;
        Vector3 awayFromTruck = position - truckBounds.center;
        if (awayFromTruck.sqrMagnitude < 0.01f)
            awayFromTruck = position - lookAt;
        if (awayFromTruck.sqrMagnitude < 0.01f)
            awayFromTruck = Vector3.back + Vector3.up * 0.35f;

        awayFromTruck.Normalize();
        int guard = 0;
        while (paddedBounds.Contains(position) && guard < 12)
        {
            position += awayFromTruck * 2f;
            guard++;
        }

        if (position.y < truckBounds.max.y + 1.2f)
            position.y = truckBounds.max.y + 1.2f;

        return position;
    }

    private Vector3 GetUsableCameraPosition(Camera camera, Vector3 fallbackPosition, Bounds truckBounds)
    {
        if (camera == null || truckBounds.size == Vector3.zero)
            return fallbackPosition;

        Bounds paddedBounds = truckBounds;
        paddedBounds.Expand(1.5f);
        return paddedBounds.Contains(camera.transform.position) ? fallbackPosition : camera.transform.position;
    }

    private CanvasGroup CreateLetterbox()
    {
        GameObject canvasObject = new GameObject("ExtractionLetterboxCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32765;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasGroup group = canvasObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        CreateLetterboxBar(canvasObject.transform, "TopBar", new Vector2(0f, 0.86f), new Vector2(1f, 1f));
        CreateLetterboxBar(canvasObject.transform, "BottomBar", Vector2.zero, new Vector2(1f, 0.14f));
        return group;
    }

    private void CreateLetterboxBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject barObject = new GameObject(name);
        barObject.transform.SetParent(parent, false);
        Image image = barObject.AddComponent<Image>();
        image.color = Color.black;

        RectTransform rect = image.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private IEnumerator FadeLetterbox(CanvasGroup group, float start, float end, float duration)
    {
        if (group == null)
            yield break;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        group.alpha = end;
    }

    private IEnumerator SlideFadeToCamera(CanvasGroup group, int cameraIndex, float duration)
    {
        yield return FadeLetterbox(group, 0f, 1f, duration * 0.5f);
        SetActiveCinematicCamera(cameraIndex);
        yield return FadeLetterbox(group, 1f, 0f, duration * 0.5f);
    }

    private void PrepareTrigger(GameObject triggerObject, bool exit)
    {
        Collider[] colliders = triggerObject.GetComponents<Collider>();
        if (colliders.Length == 0)
        {
            BoxCollider box = triggerObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4.5f, 4f, 4.5f);
            colliders = new Collider[] { box };
        }

        foreach (Collider collider in colliders)
        {
            collider.isTrigger = true;
            if (exit)
            {
                DemoSceneExitTriggerRelay relay = collider.GetComponent<DemoSceneExitTriggerRelay>();
                if (relay == null)
                    relay = collider.gameObject.AddComponent<DemoSceneExitTriggerRelay>();
                relay.Initialize(this);
            }
        }
    }

    private GameObject CreatePinkMarker(Transform parent, string markerName, float radius)
    {
        GameObject markerObject = new GameObject(markerName);
        markerObject.transform.SetParent(parent, false);
        markerObject.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        markerObject.transform.localRotation = Quaternion.identity;
        markerObject.transform.localScale = Vector3.one;

        Material markerMaterial = CreatePinkMarkerMaterial();
        CreateMarkerRing(markerObject.transform, "RingHorizontal", markerMaterial, Quaternion.Euler(90f, 0f, 0f), radius, 0.075f);
        CreateMarkerRing(markerObject.transform, "RingVerticalX", markerMaterial, Quaternion.Euler(0f, 90f, 0f), radius, 0.06f);
        CreateMarkerRing(markerObject.transform, "RingVerticalZ", markerMaterial, Quaternion.identity, radius, 0.06f);
        markerObject.AddComponent<BombMarkerRotator>();
        return markerObject;
    }

    private Material CreatePinkMarkerMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = new Color(1f, 0.02f, 0.85f, 0.95f);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1f, 0.05f, 0.85f) * 1.8f);
        }

        return material;
    }

    private void CreateMarkerRing(Transform parent, string name, Material material, Quaternion localRotation, float radius, float width)
    {
        GameObject ringObject = new GameObject(name);
        ringObject.transform.SetParent(parent, false);
        ringObject.transform.localRotation = localRotation;

        LineRenderer lineRenderer = ringObject.AddComponent<LineRenderer>();
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.material = material;
        lineRenderer.startColor = new Color(1f, 0.02f, 0.85f, 0.95f);
        lineRenderer.endColor = new Color(1f, 0.02f, 0.85f, 0.95f);
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.positionCount = 80;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        for (int index = 0; index < lineRenderer.positionCount; index++)
        {
            float angle = index / (float)lineRenderer.positionCount * Mathf.PI * 2f;
            lineRenderer.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private bool IsPlayer(Collider other)
    {
        return ResolvePlayerObject(other) != null;
    }

    private GameObject ResolvePlayerObject(Collider other)
    {
        if (other == null)
            return null;

        if (other.CompareTag("Player"))
            return other.gameObject;

        Transform root = other.transform.root;
        if (root != null)
        {
            if (root.CompareTag("Player") || root.GetComponentInChildren<vThirdPersonController>() != null || root.GetComponentInChildren<PlayerMovement>() != null)
                return root.gameObject;
        }

        vThirdPersonController controller = other.GetComponentInParent<vThirdPersonController>();
        return controller != null ? controller.gameObject : null;
    }

    private Bounds GetBounds(Transform root)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        Bounds bounds = new Bounds(root != null ? root.position : Vector3.zero, Vector3.zero);
        bool initialized = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private Vector3 FlatPosition(Vector3 position)
    {
        return new Vector3(position.x, 0f, position.z);
    }

    private Vector3 FlatDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            fallback.y = 0f;
            direction = fallback.sqrMagnitude < 0.001f ? Vector3.forward : fallback;
        }

        return direction.normalized;
    }

    private void ForcePlayerIdentity(GameObject player)
    {
        if (player == null)
            return;

        TrySetTag(player, "Player");
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0)
            return;

        player.layer = playerLayer;
        foreach (Collider collider in player.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null && collider.gameObject.layer != LayerMask.NameToLayer("UI"))
                collider.gameObject.layer = playerLayer;
        }
    }

    private void TrySetTag(GameObject target, string tagName)
    {
        if (target == null || string.IsNullOrEmpty(tagName))
            return;

        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
        }
    }

    private NavMeshAgent EnsurePlayerNavAgent(GameObject player)
    {
        if (player == null)
            return null;

        NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = player.AddComponent<NavMeshAgent>();

        if (!agent.enabled)
            agent.enabled = true;

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        if (!agent.isOnNavMesh)
        {
            Vector3 navPosition = SampleNavMeshPosition(player.transform.position);
            agent.Warp(navPosition);
        }

        agent.radius = Mathf.Clamp(agent.radius <= 0f ? 0.35f : agent.radius, 0.25f, 0.6f);
        agent.height = Mathf.Max(agent.height, 1.8f);
        return agent;
    }

    private NavMeshAgent EnsureCompanionNavAgent(vSimpleMeleeAI_Companion companion)
    {
        if (companion == null)
            return null;

        NavMeshAgent agent = companion.GetComponent<NavMeshAgent>();
        if (agent == null)
            return null;

        if (!agent.enabled)
            agent.enabled = true;

        if (!agent.isOnNavMesh)
        {
            Vector3 navPosition = SampleNavMeshPosition(companion.transform.position);
            agent.Warp(navPosition);
        }

        return agent;
    }

    private Vector3 SampleNavMeshPosition(Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return position;
    }

    private void SetRunAnimation(Animator animator, bool running)
    {
        if (animator == null)
            return;

        SetAnimatorBool(animator, "isRunning", running);
        SetAnimatorBool(animator, "isWalking", false);
        SetAnimatorBool(animator, "isStopWalking", !running);
        SetAnimatorFloat(animator, "InputMagnitude", running ? 1f : 0f);
        SetAnimatorFloat(animator, "InputVertical", running ? 1f : 0f);
        SetAnimatorFloat(animator, "InputHorizontal", 0f);
        SetAnimatorFloat(animator, "Speed", running ? 1f : 0f);
    }

    private void SetAnimatorBool(Animator animator, string parameterName, bool value)
    {
        if (HasAnimatorParameter(animator, parameterName, AnimatorControllerParameterType.Bool))
            animator.SetBool(parameterName, value);
    }

    private void SetAnimatorFloat(Animator animator, string parameterName, float value)
    {
        if (HasAnimatorParameter(animator, parameterName, AnimatorControllerParameterType.Float))
            animator.SetFloat(parameterName, value);
    }

    private bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }

    private void LookAt(Transform target, Vector3 point)
    {
        Vector3 direction = point - target.position;
        if (direction.sqrMagnitude > 0.001f)
            target.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static GameObject FindSceneGameObject(Scene scene, string objectName)
    {
        Transform transform = FindSceneTransform(scene, objectName);
        return transform != null ? transform.gameObject : null;
    }

    private static Camera FindSceneCamera(Scene scene, string cameraName)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.gameObject.scene == scene && camera.name == cameraName)
                return camera;
        }

        return null;
    }

    private static Camera FindSceneCameraLoose(Scene scene, params string[] cameraNames)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.gameObject.scene != scene)
                continue;

            foreach (string cameraName in cameraNames)
            {
                if (NamesMatchLoose(camera.name, cameraName))
                    return camera;
            }
        }

        return null;
    }

    private static Transform FindSceneTransform(Scene scene, string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject candidate in objects)
        {
            if (candidate != null && candidate.scene == scene && candidate.name == objectName)
                return candidate.transform;
        }

        return null;
    }

    private static Transform FindSceneTransformAnyCase(Scene scene, params string[] objectNames)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject candidate in objects)
        {
            if (candidate == null || candidate.scene != scene)
                continue;

            foreach (string objectName in objectNames)
            {
                if (string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                    return candidate.transform;
            }
        }

        return null;
    }

    private static Transform FindSceneTransformLoose(Scene scene, params string[] objectNames)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject candidate in objects)
        {
            if (candidate == null || candidate.scene != scene)
                continue;

            foreach (string objectName in objectNames)
            {
                if (NamesMatchLoose(candidate.name, objectName))
                    return candidate.transform;
            }
        }

        return null;
    }

    private static bool NamesMatchLoose(string actualName, string expectedName)
    {
        return NormalizeSceneName(actualName) == NormalizeSceneName(expectedName);
    }

    private static string NormalizeSceneName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static T FindSceneObjectOfType<T>(Scene scene) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        foreach (T component in components)
        {
            if (component != null && component.gameObject.scene == scene)
                return component;
        }

        return null;
    }
}

public sealed class DemoSceneExitTriggerRelay : MonoBehaviour
{
    private DemoSceneExtractionSequence sequence;

    public void Initialize(DemoSceneExtractionSequence owner)
    {
        sequence = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (sequence != null)
            sequence.HandleExitTrigger(other);
    }
}

[DefaultExecutionOrder(10000)]
public sealed class ExtractionCompanionNavDriver : MonoBehaviour
{
    private Transform target;
    private NavMeshAgent agent;
    private Animator animator;
    private Rigidbody body;
    private float speed = 4.5f;
    private float arriveDistance = 1.8f;
    private float sampleRadius = 8f;

    public bool HasArrived { get; private set; }

    public void Initialize(Transform targetTransform, float moveSpeed, float disableDistance, float navSampleRadius)
    {
        target = targetTransform;
        speed = Mathf.Max(0.1f, moveSpeed);
        arriveDistance = Mathf.Max(0.1f, disableDistance);
        sampleRadius = Mathf.Max(0.1f, navSampleRadius);
        HasArrived = false;
        CacheComponents();
        ConfigureAgent();
        SetRunning(true);
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        CacheComponents();
        ConfigureAgent();

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        Vector3 destination = SampleNavMeshPosition(target.position);
        agent.isStopped = false;
        agent.SetDestination(destination);
        SetRunning(true);

        Vector3 flatDelta = new Vector3(target.position.x - transform.position.x, 0f, target.position.z - transform.position.z);
        if (flatDelta.magnitude <= arriveDistance)
        {
            HasArrived = true;
            SetRunning(false);
            gameObject.SetActive(false);
        }
    }

    private void CacheComponents()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (body == null)
            body = GetComponent<Rigidbody>();
    }

    private void ConfigureAgent()
    {
        if (agent == null)
            return;

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        if (!agent.enabled)
            agent.enabled = true;

        if (!agent.isOnNavMesh)
            agent.Warp(SampleNavMeshPosition(transform.position));

        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.radius = Mathf.Clamp(agent.radius <= 0f ? 0.35f : agent.radius, 0.25f, 0.8f);
        agent.height = Mathf.Max(agent.height, 1.8f);
        agent.speed = Mathf.Max(agent.speed, speed);
        agent.acceleration = Mathf.Max(agent.acceleration, 16f);
        agent.angularSpeed = Mathf.Max(agent.angularSpeed, 360f);
        agent.stoppingDistance = 0.25f;
    }

    private Vector3 SampleNavMeshPosition(Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            return hit.position;

        return position;
    }

    private void SetRunning(bool running)
    {
        if (animator == null)
            return;

        SetBool("isRunning", running);
        SetBool("isWalking", false);
        SetBool("isStopWalking", !running);
        SetFloat("InputMagnitude", running ? 1f : 0f);
        SetFloat("InputVertical", running ? 1f : 0f);
        SetFloat("InputHorizontal", 0f);
        SetFloat("Speed", running ? 1f : 0f);
    }

    private void SetBool(string parameterName, bool value)
    {
        if (HasParameter(parameterName, AnimatorControllerParameterType.Bool))
            animator.SetBool(parameterName, value);
    }

    private void SetFloat(string parameterName, float value)
    {
        if (HasParameter(parameterName, AnimatorControllerParameterType.Float))
            animator.SetFloat(parameterName, value);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
