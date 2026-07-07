using System.Collections;
using System.Collections.Generic;
using Invector.vCharacterController;
using Invector.vCamera;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelOneStartupCutscene : MonoBehaviour
{
    private const string SceneName = "Level-1";
    private const string CameraOneName = "cutsceneCamera01";
    private const string CameraTwoName = "cutSceneCamera02";
    private const string PlaneName = "plane";
    private const string LandPointName = "landPoint";
    private const string IdleParachuteName = "IdleP";
    private const string PlaneCameraName = "startPlaneCamera";

    private const float CameraOneDuration = 10f;
    private const float CharacterDelay = 0.028f;
    private const float CameraTwoFovPush = 26f;
    private const float PlaneFlightDuration = 6f;
    private const float ParachuteFallDuration = 7f;
    private const float LandingFadeDuration = 2f;
    private const float PlanePitch = -88f;
    private const string BriefingVoiceResource = "i";

    private const string SpeakerName = "AAREN VALE";
    private const string BriefingText =
        "Aaren Vale, the mission starts now. The targets on this board are dangerous, and they will not wait for us to make the first move.\n\n" +
        "We have to eliminate them before their forces grow stronger, but how you complete this mission is your choice. You can move through the shadows, gather intel, and strike silently, or face them directly with full force.\n\n" +
        "Hmm, choose your path carefully, because every decision you make from here will shape the outcome of this war.";

    private Camera cameraOne;
    private Camera cameraTwo;
    private Camera planeCamera;
    private GameObject planeObject;
    private GameObject idleParachute;
    private Transform landPoint;
    private Transform playerRoot;
    private vThirdPersonCamera invectorCameraRig;
    private RebelGTAThirdPersonCamera gtaCameraDriver;
    private Camera gameplayRenderCamera;
    private Transform cameraRigTransform;
    private Transform gameplayCameraTransform;
    private Vector3 cameraRigLocalPosition;
    private Quaternion cameraRigLocalRotation;
    private Vector3 cameraRigLocalScale;
    private Vector3 gameplayCameraLocalPosition;
    private Quaternion gameplayCameraLocalRotation;
    private Vector3 gameplayCameraLocalScale;
    private bool gtaCameraDriverWasEnabled;
    private CanvasGroup overlayGroup;
    private CanvasGroup blackFadeGroup;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI statusText;
    private AudioSource briefingVoiceSource;
    private AudioClip briefingVoiceClip;
    private readonly List<CameraState> cameraStates = new List<CameraState>();
    private readonly List<ListenerState> listenerStates = new List<ListenerState>();
    private readonly List<InputState> inputStates = new List<InputState>();
    private readonly List<PlayerObjectState> playerStates = new List<PlayerObjectState>();
    private bool isPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
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
        if (!scene.IsValid() || scene.name != SceneName)
        {
            return;
        }

        if (FindSceneObjectOfType<LevelOneStartupCutscene>(scene) != null)
        {
            return;
        }

        GameObject runner = new GameObject(nameof(LevelOneStartupCutscene));
        runner.AddComponent<LevelOneStartupCutscene>();
    }

    private void Start()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Destroy(gameObject);
            return;
        }

        cameraOne = FindSceneCamera(scene, CameraOneName);
        cameraTwo = FindSceneCamera(scene, CameraTwoName);
        planeCamera = FindSceneCamera(scene, PlaneCameraName);
        planeObject = FindSceneGameObject(scene, PlaneName);
        idleParachute = FindSceneGameObject(scene, IdleParachuteName);
        GameObject landPointObject = FindSceneGameObject(scene, LandPointName);
        landPoint = landPointObject != null ? landPointObject.transform : null;
        briefingVoiceClip = Resources.Load<AudioClip>(BriefingVoiceResource);

        if (cameraOne == null || cameraTwo == null)
        {
            Debug.LogWarning("LevelOneStartupCutscene could not find cutsceneCamera01 and cutSceneCamera02.");
            Destroy(gameObject);
            return;
        }

        StartCoroutine(PlayIntro(scene));
    }

    private IEnumerator PlayIntro(Scene scene)
    {
        isPlaying = true;
        CacheSceneStates(scene);
        CacheGameplayRig(scene);
        LockPlayerInput(true);
        SetPlayerObjectsActive(false);
        SetGameplayCameraRigsActive(scene, false);
        SetObjectActive(idleParachute, false);
        BuildOverlay();

        float cameraTwoStartFov = cameraTwo.fieldOfView;
        float cameraTwoTargetFov = Mathf.Clamp(cameraTwoStartFov + CameraTwoFovPush, 1f, 95f);

        SetGameplayCamerasActive(false);
        SetCutsceneCameraActive(cameraOne, true);
        SetCutsceneCameraActive(cameraTwo, false);
        SetOnlyListener(cameraOne);

        yield return FadeOverlay(1f, 0.45f);
        statusText.text = "MISSION BRIEFING";

        PlayBriefingVoice();
        float briefingDuration = GetBriefingDuration();
        yield return TypeBriefing(cameraTwoStartFov, cameraTwoTargetFov, briefingDuration);
        yield return WaitForVoiceToFinish();
        yield return new WaitForSecondsRealtime(0.65f);
        yield return FadeOverlay(0f, 0.45f);

        cameraTwo.fieldOfView = cameraTwoStartFov;
        CleanupVoiceSource();
        Destroy(overlayGroup.gameObject);
        overlayGroup = null;

        yield return PlayPlaneLandingSequence(scene);

        MovePlayersToLandPoint();
        SetPlayerObjectsActive(true);
        Camera gameplayCamera = RestoreGameplayCamera(scene);
        ResetPlayerRuntimeState(scene, gameplayCamera);
        SetCutsceneCameraActive(cameraOne, false);
        SetCutsceneCameraActive(cameraTwo, false);
        SetCutsceneCameraActive(planeCamera, false);
        DisableSceneCamerasExcept(scene, gameplayCamera);
        SetObjectActive(idleParachute, false);
        LockPlayerInput(false);
        UnlockAllPlayerInputsInScene(scene);
        yield return new WaitForEndOfFrame();
        CleanupBlackFade();
        DestroyCutsceneCameras();
        isPlaying = false;
        Destroy(gameObject);
    }

    private IEnumerator PlayPlaneLandingSequence(Scene scene)
    {
        if (planeObject == null || idleParachute == null || landPoint == null)
        {
            Debug.LogWarning("LevelOneStartupCutscene landing sequence needs plane, IdleP, and landPoint in the Level-1 scene.");
            yield break;
        }

        SetGameplayCamerasActive(false);
        SetCutsceneCameraActive(cameraOne, false);
        SetCutsceneCameraActive(cameraTwo, false);

        SetObjectActive(planeObject, true);
        SetObjectActive(idleParachute, false);

        Vector3 planeStart = planeObject.transform.position;
        Vector3 dropPoint = new Vector3(landPoint.position.x, planeStart.y, landPoint.position.z);
        Vector3 cameraOffset = planeCamera != null ? planeCamera.transform.position - planeStart : Vector3.zero;
        planeObject.transform.rotation = GetPlaneFlightRotation(dropPoint - planeStart);

        if (planeCamera != null)
        {
            SetCutsceneCameraActive(planeCamera, true);
            SetOnlyListener(planeCamera);
        }

        yield return MovePlaneToDropPoint(planeStart, dropPoint, cameraOffset);

        SetObjectActive(idleParachute, true);
        idleParachute.transform.position = dropPoint;
        FaceDirection(idleParachute.transform, dropPoint - planeStart);

        Animator parachuteAnimator = idleParachute.GetComponentInChildren<Animator>();
        if (parachuteAnimator != null)
        {
            parachuteAnimator.enabled = true;
        }

        SetObjectActive(planeObject, false);
        yield return FollowParachuteToLand(dropPoint, landPoint.position);
    }

    private IEnumerator MovePlaneToDropPoint(Vector3 start, Vector3 end, Vector3 cameraOffset)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, PlaneFlightDuration);
        Quaternion targetRotation = GetPlaneFlightRotation(end - start);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = SmootherStep(t);
            planeObject.transform.position = Vector3.Lerp(start, end, eased);
            planeObject.transform.rotation = targetRotation;

            UpdatePlaneCamera(planeObject.transform, cameraOffset);
            yield return null;
        }

        planeObject.transform.position = end;
        planeObject.transform.rotation = targetRotation;
    }

    private IEnumerator FollowParachuteToLand(Vector3 start, Vector3 end)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, ParachuteFallDuration);
        float fadeStartTime = Mathf.Max(0f, safeDuration - LandingFadeDuration);
        Vector3 cameraOffset = planeCamera != null ? planeCamera.transform.position - start : Vector3.zero;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = SmootherStep(t);
            idleParachute.transform.position = Vector3.Lerp(start, end, eased);
            FaceDirection(idleParachute.transform, Vector3.ProjectOnPlane(end - start, Vector3.up));
            UpdatePlaneCamera(idleParachute.transform, cameraOffset);

            if (elapsed >= fadeStartTime)
            {
                BuildBlackFadeOverlay();
                blackFadeGroup.alpha = Mathf.Clamp01((elapsed - fadeStartTime) / LandingFadeDuration);
            }

            yield return null;
        }

        idleParachute.transform.position = end;
        BuildBlackFadeOverlay();
        blackFadeGroup.alpha = 1f;
    }

    private void UpdatePlaneCamera(Transform target, Vector3 worldOffset)
    {
        if (planeCamera == null || target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + worldOffset;
        float positionBlend = 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime);
        float rotationBlend = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);

        planeCamera.transform.position = Vector3.Lerp(planeCamera.transform.position, desiredPosition, positionBlend);
        Vector3 lookDirection = target.position + Vector3.up * 1.8f - planeCamera.transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            planeCamera.transform.rotation = Quaternion.Slerp(
                planeCamera.transform.rotation,
                Quaternion.LookRotation(lookDirection, Vector3.up),
                rotationBlend);
        }
    }

    private static float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (6f * t - 15f) + 10f);
    }

    private static void FaceDirection(Transform target, Vector3 direction)
    {
        Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (flatDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        target.rotation = Quaternion.Slerp(target.rotation, Quaternion.LookRotation(flatDirection.normalized, Vector3.up), 0.18f);
    }

    private static Quaternion GetPlaneFlightRotation(Vector3 direction)
    {
        Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (flatDirection.sqrMagnitude <= 0.001f)
        {
            return Quaternion.Euler(PlanePitch, 0f, 0f);
        }

        Vector3 euler = Quaternion.LookRotation(flatDirection.normalized, Vector3.up).eulerAngles;
        euler.x = PlanePitch;
        euler.z = 0f;
        return Quaternion.Euler(euler);
    }

    private IEnumerator TypeBriefing(float startFov, float targetFov, float duration)
    {
        dialogueText.text = BriefingText;
        dialogueText.maxVisibleCharacters = 0;
        int total = BriefingText.Length;
        float elapsed = 0f;
        bool switchedToCameraTwo = false;
        float cameraTwoFovDuration = Mathf.Max(0.01f, duration - CameraOneDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            dialogueText.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(progress * total), 0, total);

            if (!switchedToCameraTwo && elapsed >= CameraOneDuration)
            {
                switchedToCameraTwo = true;
                SetCutsceneCameraActive(cameraOne, false);
                SetCutsceneCameraActive(cameraTwo, true);
                SetOnlyListener(cameraTwo);
            }

            if (switchedToCameraTwo)
            {
                float fovProgress = Mathf.Clamp01((elapsed - CameraOneDuration) / cameraTwoFovDuration);
                cameraTwo.fieldOfView = Mathf.Lerp(startFov, targetFov, Mathf.SmoothStep(0f, 1f, fovProgress));
            }

            statusText.text = "SECURE CHANNEL  " + Mathf.RoundToInt(progress * 100f) + "%";
            yield return null;
        }

        dialogueText.maxVisibleCharacters = total;
        if (switchedToCameraTwo)
        {
            cameraTwo.fieldOfView = targetFov;
        }
        statusText.text = "ORDERS RECEIVED";
    }

    private void PlayBriefingVoice()
    {
        if (briefingVoiceClip == null)
        {
            Debug.LogWarning("LevelOneStartupCutscene could not load Resources/i audio clip.");
            return;
        }

        GameObject sourceObject = new GameObject("AarenValeBriefingVoice");
        briefingVoiceSource = sourceObject.AddComponent<AudioSource>();
        briefingVoiceSource.clip = briefingVoiceClip;
        briefingVoiceSource.playOnAwake = false;
        briefingVoiceSource.loop = false;
        briefingVoiceSource.spatialBlend = 0f;
        briefingVoiceSource.volume = 1f;
        briefingVoiceSource.Play();
    }

    private float GetBriefingDuration()
    {
        if (briefingVoiceClip != null && briefingVoiceClip.length > 0.1f)
        {
            return briefingVoiceClip.length;
        }

        return Mathf.Max(1f, BriefingText.Length * CharacterDelay);
    }

    private IEnumerator WaitForVoiceToFinish()
    {
        while (briefingVoiceSource != null && briefingVoiceSource.isPlaying)
        {
            yield return null;
        }
    }

    private void CleanupVoiceSource()
    {
        if (briefingVoiceSource == null)
        {
            return;
        }

        briefingVoiceSource.Stop();
        Destroy(briefingVoiceSource.gameObject);
        briefingVoiceSource = null;
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("LevelOneBriefingCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        overlayGroup = canvasObject.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;

        Texture2D solidTexture = Texture2D.whiteTexture;
        Sprite solidSprite = Sprite.Create(solidTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);

        Image panel = CreateImage("DialoguePanel", canvasObject.transform, solidSprite, new Color(0.03f, 0.035f, 0.03f, 0.72f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(42f, 42f);
        panelRect.sizeDelta = new Vector2(760f, 310f);

        Image accent = CreateImage("GoldAccent", panel.transform, solidSprite, new Color(0.9f, 0.58f, 0.18f, 1f));
        Stretch(accent.rectTransform, new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 4f));

        TextMeshProUGUI speaker = CreateText("Speaker", panel.transform, SpeakerName, 22, FontStyles.Bold, new Color(0.96f, 0.66f, 0.24f, 1f));
        speaker.rectTransform.anchorMin = new Vector2(0.03f, 0.74f);
        speaker.rectTransform.anchorMax = new Vector2(0.35f, 0.94f);
        speaker.rectTransform.offsetMin = Vector2.zero;
        speaker.rectTransform.offsetMax = Vector2.zero;

        statusText = CreateText("Status", panel.transform, "LINKING...", 15, FontStyles.Bold, new Color(0.66f, 0.78f, 0.7f, 0.95f));
        statusText.alignment = TextAlignmentOptions.Right;
        statusText.rectTransform.anchorMin = new Vector2(0.62f, 0.76f);
        statusText.rectTransform.anchorMax = new Vector2(0.97f, 0.94f);
        statusText.rectTransform.offsetMin = Vector2.zero;
        statusText.rectTransform.offsetMax = Vector2.zero;

        dialogueText = CreateText("Dialogue", panel.transform, "", 18, FontStyles.Normal, new Color(0.93f, 0.92f, 0.86f, 1f));
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.lineSpacing = 2f;
        dialogueText.enableWordWrapping = true;
        dialogueText.rectTransform.anchorMin = new Vector2(0.03f, 0.08f);
        dialogueText.rectTransform.anchorMax = new Vector2(0.97f, 0.72f);
        dialogueText.rectTransform.offsetMin = Vector2.zero;
        dialogueText.rectTransform.offsetMax = Vector2.zero;
    }

    private IEnumerator FadeOverlay(float target, float duration)
    {
        float start = overlayGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        overlayGroup.alpha = target;
    }

    private void BuildBlackFadeOverlay()
    {
        if (blackFadeGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("LevelOneLandingFadeCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        blackFadeGroup = canvasObject.AddComponent<CanvasGroup>();
        blackFadeGroup.alpha = 0f;
        blackFadeGroup.interactable = false;
        blackFadeGroup.blocksRaycasts = false;

        Image blackImage = CreateImage("BlackFade", canvasObject.transform, null, Color.black);
        Stretch(blackImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private IEnumerator FadeBlack(float target, float duration)
    {
        if (blackFadeGroup == null)
        {
            yield break;
        }

        float start = blackFadeGroup.alpha;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            blackFadeGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / safeDuration));
            yield return null;
        }

        blackFadeGroup.alpha = target;
    }

    private void CleanupBlackFade()
    {
        if (blackFadeGroup == null)
        {
            return;
        }

        Destroy(blackFadeGroup.gameObject);
        blackFadeGroup = null;
    }

    private void CacheSceneStates(Scene scene)
    {
        cameraStates.Clear();
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.gameObject.scene == scene)
            {
                cameraStates.Add(new CameraState(camera));
            }
        }

        listenerStates.Clear();
        AudioListener[] listeners = Resources.FindObjectsOfTypeAll<AudioListener>();
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null && listener.gameObject.scene == scene)
            {
                listenerStates.Add(new ListenerState(listener));
            }
        }

        inputStates.Clear();
        vThirdPersonInput[] inputs = Resources.FindObjectsOfTypeAll<vThirdPersonInput>();
        for (int i = 0; i < inputs.Length; i++)
        {
            vThirdPersonInput input = inputs[i];
            if (input != null && input.gameObject.scene == scene)
            {
                inputStates.Add(new InputState(input));
            }
        }

        CachePlayerStates(scene);
    }

    private void SetGameplayCamerasActive(bool active)
    {
        for (int i = 0; i < cameraStates.Count; i++)
        {
            Camera camera = cameraStates[i].Camera;
            if (camera == null || camera == cameraOne || camera == cameraTwo)
            {
                continue;
            }

            camera.enabled = active;
        }
    }

    private void SetCutsceneCameraActive(Camera target, bool active)
    {
        if (target == null)
        {
            return;
        }

        target.gameObject.SetActive(active);
        target.enabled = active;
    }

    private void SetOnlyListener(Camera targetCamera)
    {
        for (int i = 0; i < listenerStates.Count; i++)
        {
            AudioListener listener = listenerStates[i].Listener;
            if (listener != null)
            {
                listener.enabled = targetCamera != null && listener.gameObject == targetCamera.gameObject;
            }
        }
    }

    private void LockPlayerInput(bool locked)
    {
        for (int i = 0; i < inputStates.Count; i++)
        {
            vThirdPersonInput input = inputStates[i].Input;
            if (input == null)
            {
                continue;
            }

            input.SetLockAllInput(locked);
            input.SetLockCameraInput(locked);
        }
    }

    private void SetPlayerObjectsActive(bool active)
    {
        for (int i = 0; i < playerStates.Count; i++)
        {
            GameObject player = playerStates[i].Player;
            if (player != null)
            {
                player.SetActive(active);
            }
        }
    }

    private void CachePlayerStates(Scene scene)
    {
        playerStates.Clear();
        List<GameObject> players = new List<GameObject>();

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null || transform.gameObject.scene != scene)
            {
                continue;
            }

            GameObject player = GetPlayerRootObject(transform);
            if (player != null && player.scene == scene && !players.Contains(player))
            {
                players.Add(player);
            }
        }

        players.Sort(ComparePreferredPlayerObjects);

        for (int i = 0; i < players.Count; i++)
        {
            playerStates.Add(new PlayerObjectState(players[i]));
        }
    }

    private static GameObject GetPlayerRootObject(Transform transform)
    {
        vThirdPersonInput input = GetComponentInParentIncludingInactive<vThirdPersonInput>(transform);
        if (input != null)
        {
            return input.gameObject;
        }

        vThirdPersonController controller = GetComponentInParentIncludingInactive<vThirdPersonController>(transform);
        if (controller != null)
        {
            return controller.gameObject;
        }

        return transform.CompareTag("Player") ? transform.gameObject : null;
    }

    private static int ComparePreferredPlayerObjects(GameObject left, GameObject right)
    {
        int rightScore = GetPlayerPreferenceScore(right);
        int leftScore = GetPlayerPreferenceScore(left);
        return rightScore.CompareTo(leftScore);
    }

    private static int GetPlayerPreferenceScore(GameObject player)
    {
        if (player == null)
        {
            return int.MinValue;
        }

        int score = 0;
        if (player.activeSelf)
        {
            score += 100000;
        }

        if (player.activeInHierarchy)
        {
            score += 50000;
        }

        if (player.CompareTag("Player"))
        {
            score += 10000;
        }

        if (player.GetComponent<vThirdPersonInput>() != null)
        {
            score += 5000;
        }

        return score + GetHierarchyOrderScore(player.transform);
    }

    private static vThirdPersonCamera FindPreferredInvectorCamera(Scene scene, Transform target)
    {
        vThirdPersonCamera[] cameras = Resources.FindObjectsOfTypeAll<vThirdPersonCamera>();
        vThirdPersonCamera bestCamera = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < cameras.Length; i++)
        {
            vThirdPersonCamera camera = cameras[i];
            if (camera == null || camera.gameObject.scene != scene)
            {
                continue;
            }

            int score = GetCameraRigPreferenceScore(camera.gameObject);
            if (target != null && (camera.mainTarget == target || camera.currentTarget == target))
            {
                score += 100000;
            }

            if (camera.targetCamera != null && camera.targetCamera.gameObject.activeSelf)
            {
                score += 1000;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCamera = camera;
            }
        }

        return bestCamera;
    }

    private static RebelGTAThirdPersonCamera FindPreferredGtaCamera(Scene scene, Transform target, vThirdPersonCamera invectorCamera)
    {
        RebelGTAThirdPersonCamera[] cameras = Resources.FindObjectsOfTypeAll<RebelGTAThirdPersonCamera>();
        RebelGTAThirdPersonCamera bestCamera = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < cameras.Length; i++)
        {
            RebelGTAThirdPersonCamera camera = cameras[i];
            if (camera == null || camera.gameObject.scene != scene)
            {
                continue;
            }

            int score = GetCameraRigPreferenceScore(camera.gameObject);
            if (target != null && camera.target == target)
            {
                score += 100000;
            }

            if (invectorCamera != null && camera.invectorCamera == invectorCamera)
            {
                score += 50000;
            }

            if (camera.targetCamera != null && camera.targetCamera.gameObject.activeSelf)
            {
                score += 1000;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCamera = camera;
            }
        }

        return bestCamera;
    }

    private static int GetCameraRigPreferenceScore(GameObject cameraRig)
    {
        if (cameraRig == null)
        {
            return int.MinValue;
        }

        int score = 0;
        if (cameraRig.activeSelf)
        {
            score += 10000;
        }

        if (cameraRig.activeInHierarchy)
        {
            score += 5000;
        }

        return score + GetHierarchyOrderScore(cameraRig.transform);
    }

    private static int GetHierarchyOrderScore(Transform transform)
    {
        int score = 0;
        int multiplier = 1;
        Transform current = transform;
        while (current != null)
        {
            score += current.GetSiblingIndex() * multiplier;
            multiplier *= 100;
            current = current.parent;
        }

        return score;
    }

    private static T GetComponentInParentIncludingInactive<T>(Transform transform) where T : Component
    {
        Transform current = transform;
        while (current != null)
        {
            T component = current.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            current = current.parent;
        }

        return null;
    }

    private void CacheGameplayRig(Scene scene)
    {
        playerRoot = GetPrimaryPlayerTransform();
        invectorCameraRig = FindPreferredInvectorCamera(scene, playerRoot);
        gtaCameraDriver = FindPreferredGtaCamera(scene, playerRoot, invectorCameraRig);
        gameplayRenderCamera = null;

        if (invectorCameraRig != null)
        {
            cameraRigTransform = invectorCameraRig.transform;
            cameraRigLocalPosition = cameraRigTransform.localPosition;
            cameraRigLocalRotation = cameraRigTransform.localRotation;
            cameraRigLocalScale = cameraRigTransform.localScale;
            gameplayRenderCamera = invectorCameraRig.GetComponentInChildren<Camera>(true);
        }

        if (gameplayRenderCamera == null && gtaCameraDriver != null)
        {
            gameplayRenderCamera = gtaCameraDriver.targetCamera != null
                ? gtaCameraDriver.targetCamera
                : gtaCameraDriver.GetComponentInChildren<Camera>(true);
        }

        if (gameplayRenderCamera != null)
        {
            gameplayCameraTransform = gameplayRenderCamera.transform;
            gameplayCameraLocalPosition = gameplayCameraTransform.localPosition;
            gameplayCameraLocalRotation = gameplayCameraTransform.localRotation;
            gameplayCameraLocalScale = gameplayCameraTransform.localScale;
        }

        if (gtaCameraDriver != null)
        {
            gtaCameraDriverWasEnabled = gtaCameraDriver.enabled;
        }
    }

    private void RestorePlayerStates()
    {
        for (int i = 0; i < playerStates.Count; i++)
        {
            playerStates[i].Restore();
        }
    }

    private void MovePlayersToLandPoint()
    {
        if (landPoint == null)
        {
            return;
        }

        for (int i = 0; i < playerStates.Count; i++)
        {
            GameObject player = playerStates[i].Player;
            if (player != null)
            {
                player.transform.position = landPoint.position;
            }
        }
    }

    private void SetGameplayCameraRigsActive(Scene scene, bool active)
    {
        vThirdPersonCamera[] invectorCameras = Resources.FindObjectsOfTypeAll<vThirdPersonCamera>();
        for (int i = 0; i < invectorCameras.Length; i++)
        {
            vThirdPersonCamera invectorCamera = invectorCameras[i];
            if (invectorCamera != null && invectorCamera.gameObject.scene == scene)
            {
                invectorCamera.gameObject.SetActive(active);
                Camera renderCamera = invectorCamera.GetComponentInChildren<Camera>(true);
                if (renderCamera != null)
                {
                    renderCamera.gameObject.SetActive(active);
                    renderCamera.enabled = active;
                }
            }
        }

        RebelGTAThirdPersonCamera[] gtaCameras = Resources.FindObjectsOfTypeAll<RebelGTAThirdPersonCamera>();
        for (int i = 0; i < gtaCameras.Length; i++)
        {
            RebelGTAThirdPersonCamera gtaCamera = gtaCameras[i];
            if (gtaCamera != null && gtaCamera.gameObject.scene == scene)
            {
                gtaCamera.gameObject.SetActive(active);
                if (gtaCamera.targetCamera != null)
                {
                    gtaCamera.targetCamera.gameObject.SetActive(active);
                    gtaCamera.targetCamera.enabled = active;
                }
            }
        }
    }

    private Camera RestoreGameplayCamera(Scene scene)
    {
        Transform playerTarget = playerRoot != null ? playerRoot : GetPrimaryPlayerTransform();
        Camera gameplayCamera = gameplayRenderCamera;
        vThirdPersonCamera[] invectorCameras = Resources.FindObjectsOfTypeAll<vThirdPersonCamera>();
        for (int i = 0; i < invectorCameras.Length; i++)
        {
            vThirdPersonCamera invectorCamera = invectorCameras[i];
            if (invectorCamera == null || invectorCamera.gameObject.scene != scene)
            {
                continue;
            }

            bool shouldEnable = invectorCameraRig == null || invectorCamera == invectorCameraRig;
            invectorCamera.gameObject.SetActive(shouldEnable);
            invectorCamera.enabled = shouldEnable;
            if (!shouldEnable)
            {
                continue;
            }

            if (invectorCamera == invectorCameraRig && cameraRigTransform != null)
            {
                cameraRigTransform.localPosition = cameraRigLocalPosition;
                cameraRigTransform.localRotation = cameraRigLocalRotation;
                cameraRigTransform.localScale = cameraRigLocalScale;
            }

            Camera renderCamera = invectorCamera.GetComponentInChildren<Camera>(true);
            if (renderCamera != null)
            {
                renderCamera.gameObject.SetActive(true);
                renderCamera.enabled = true;
                gameplayCamera = renderCamera;

                if (renderCamera == gameplayRenderCamera && gameplayCameraTransform != null)
                {
                    gameplayCameraTransform.localPosition = gameplayCameraLocalPosition;
                    gameplayCameraTransform.localRotation = gameplayCameraLocalRotation;
                    gameplayCameraTransform.localScale = gameplayCameraLocalScale;
                }
            }

            if (playerTarget != null)
            {
                invectorCamera.SetMainTarget(playerTarget);
            }

            if (gameplayCamera != null)
            {
                invectorCamera.targetCamera = gameplayCamera;
            }

            invectorCamera.isFreezed = false;
            invectorCamera.ResetTarget();
            invectorCamera.ResetAngle();
        }

        RestoreExistingCameraDrivers(scene, playerTarget, gameplayCamera);
        RestoreCachedGameplayTransforms();

        if (gameplayCamera != null)
        {
            SetOnlyListener(gameplayCamera);
        }

        return gameplayCamera;
    }

    private void RestoreExistingCameraDrivers(Scene scene, Transform playerTarget, Camera gameplayCamera)
    {
        RebelGTAThirdPersonCamera[] gtaCameras = Resources.FindObjectsOfTypeAll<RebelGTAThirdPersonCamera>();
        for (int i = 0; i < gtaCameras.Length; i++)
        {
            RebelGTAThirdPersonCamera gtaCamera = gtaCameras[i];
            if (gtaCamera == null || gtaCamera.gameObject.scene != scene)
            {
                continue;
            }

            bool shouldEnable = gtaCameraDriver == null || gtaCamera == gtaCameraDriver;
            gtaCamera.gameObject.SetActive(shouldEnable);
            if (!shouldEnable)
            {
                continue;
            }

            gtaCamera.enabled = gtaCamera == gtaCameraDriver ? gtaCameraDriverWasEnabled : gtaCamera.enabled;

            if (playerTarget != null)
            {
                gtaCamera.target = playerTarget;
            }

            if (gameplayCamera != null)
            {
                gtaCamera.targetCamera = gameplayCamera;
            }

            if (gtaCamera.invectorCamera == null)
            {
                gtaCamera.invectorCamera = invectorCameraRig != null
                    ? invectorCameraRig
                    : FindSceneObjectOfType<vThirdPersonCamera>(scene);
            }

            gtaCamera.SnapBehindTarget();
        }
    }

    private void RestoreCachedGameplayTransforms()
    {
        if (cameraRigTransform != null)
        {
            cameraRigTransform.localPosition = cameraRigLocalPosition;
            cameraRigTransform.localRotation = cameraRigLocalRotation;
            cameraRigTransform.localScale = cameraRigLocalScale;
        }

        if (gameplayCameraTransform != null)
        {
            gameplayCameraTransform.localPosition = gameplayCameraLocalPosition;
            gameplayCameraTransform.localRotation = gameplayCameraLocalRotation;
            gameplayCameraTransform.localScale = gameplayCameraLocalScale;
        }
    }

    private void ResetPlayerRuntimeState(Scene scene, Camera gameplayCamera)
    {
        for (int i = 0; i < playerStates.Count; i++)
        {
            GameObject player = playerStates[i].Player;
            if (player == null)
            {
                continue;
            }

            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            vThirdPersonController controller = player.GetComponent<vThirdPersonController>();
            if (controller != null)
            {
                controller.EnableGravityAndCollision();
                controller.ResetControllerSpeedMultiplier();
                controller.StopCharacter();
            }

            vThirdPersonInput input = player.GetComponent<vThirdPersonInput>();
            if (input != null)
            {
                input.lockMoveInput = false;
                input.SetLockAllInput(false);
                input.SetLockCameraInput(false);
                input.SetLockUpdateMoveDirection(false);

                if (gameplayCamera != null)
                {
                    input.cameraMain = gameplayCamera;
                }

                vThirdPersonCamera invectorCamera = FindSceneObjectOfType<vThirdPersonCamera>(scene);
                if (invectorCamera != null)
                {
                    input.tpCamera = invectorCameraRig != null ? invectorCameraRig : invectorCamera;
                }
            }
        }
    }

    private Transform GetPrimaryPlayerTransform()
    {
        for (int i = 0; i < playerStates.Count; i++)
        {
            GameObject player = playerStates[i].Player;
            if (player != null && player.GetComponent<vThirdPersonInput>() != null)
            {
                return player.transform;
            }
        }

        for (int i = 0; i < playerStates.Count; i++)
        {
            GameObject player = playerStates[i].Player;
            if (player != null)
            {
                return player.transform;
            }
        }

        return null;
    }

    private void DisableSceneCamerasExcept(Scene scene, Camera activeCamera)
    {
        if (activeCamera == null)
        {
            Debug.LogWarning("LevelOneStartupCutscene could not find the gameplay camera to restore after landing.");
            return;
        }

        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.gameObject.scene != scene)
            {
                continue;
            }

            bool shouldEnable = camera == activeCamera;
            camera.gameObject.SetActive(shouldEnable);
            camera.enabled = shouldEnable;

            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = shouldEnable;
            }
        }
    }

    private void DestroyCutsceneCameras()
    {
        Camera firstCamera = cameraOne;
        Camera secondCamera = cameraTwo;

        cameraOne = null;
        cameraTwo = null;

        DestroyCutsceneCamera(firstCamera);
        if (secondCamera != firstCamera)
        {
            DestroyCutsceneCamera(secondCamera);
        }
    }

    private static void DestroyCutsceneCamera(Camera target)
    {
        if (target != null)
        {
            Destroy(target.gameObject);
        }
    }

    private void RestoreSceneStates()
    {
        for (int i = 0; i < cameraStates.Count; i++)
        {
            cameraStates[i].Restore();
        }

        for (int i = 0; i < listenerStates.Count; i++)
        {
            listenerStates[i].Restore();
        }

        for (int i = 0; i < inputStates.Count; i++)
        {
            inputStates[i].Restore();
        }
    }

    private void OnDisable()
    {
        if (isPlaying)
        {
            Scene scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
            RestoreSceneStates();
            RestorePlayerStates();
            LockPlayerInput(false);
            UnlockAllPlayerInputsInScene(scene);
            CleanupVoiceSource();
            CleanupBlackFade();
        }
    }

    private static Camera FindSceneCamera(Scene scene, string cameraName)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.gameObject.scene == scene && camera.name == cameraName)
            {
                return camera;
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

    private static GameObject FindSceneGameObject(Scene scene, string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null && candidate.scene == scene && candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static void SetObjectActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private static void UnlockAllPlayerInputsInScene(Scene scene)
    {
        vThirdPersonInput[] inputs = Resources.FindObjectsOfTypeAll<vThirdPersonInput>();
        for (int i = 0; i < inputs.Length; i++)
        {
            vThirdPersonInput input = inputs[i];
            if (input == null || input.gameObject.scene != scene)
            {
                continue;
            }

            input.lockMoveInput = false;
            input.SetLockAllInput(false);
            input.SetLockCameraInput(false);
        }
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, int size, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private struct CameraState
    {
        public readonly Camera Camera;
        private readonly bool activeSelf;
        private readonly bool enabled;
        private readonly float fieldOfView;

        public CameraState(Camera camera)
        {
            Camera = camera;
            activeSelf = camera.gameObject.activeSelf;
            enabled = camera.enabled;
            fieldOfView = camera.fieldOfView;
        }

        public void Restore()
        {
            if (Camera == null)
            {
                return;
            }

            Camera.gameObject.SetActive(activeSelf);
            Camera.enabled = enabled;
            Camera.fieldOfView = fieldOfView;
        }
    }

    private struct ListenerState
    {
        public readonly AudioListener Listener;
        private readonly bool enabled;

        public ListenerState(AudioListener listener)
        {
            Listener = listener;
            enabled = listener.enabled;
        }

        public void Restore()
        {
            if (Listener != null)
            {
                Listener.enabled = enabled;
            }
        }
    }

    private struct InputState
    {
        public readonly vThirdPersonInput Input;
        private readonly bool lockInput;
        private readonly bool lockCameraInput;

        public InputState(vThirdPersonInput input)
        {
            Input = input;
            lockInput = input.lockInput;
            lockCameraInput = input.lockCameraInput;
        }

        public void Restore()
        {
            if (Input == null)
            {
                return;
            }

            Input.SetLockAllInput(lockInput);
            Input.SetLockCameraInput(lockCameraInput);
        }
    }

    private struct PlayerObjectState
    {
        public readonly GameObject Player;
        private readonly bool activeSelf;

        public PlayerObjectState(GameObject player)
        {
            Player = player;
            activeSelf = player.activeSelf;
        }

        public void Restore()
        {
            if (Player != null)
            {
                Player.SetActive(activeSelf);
            }
        }
    }
}
