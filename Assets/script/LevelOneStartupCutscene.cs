using System.Collections;
using System.Collections.Generic;
using Invector.vCharacterController;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelOneStartupCutscene : MonoBehaviour
{
    private const string SceneName = "Level-1";
    private const string CameraOneName = "cutsceneCamera01";
    private const string CameraTwoName = "cutSceneCamera02";

    private const float CameraOneDuration = 10f;
    private const float CharacterDelay = 0.028f;
    private const float CameraTwoFovPush = 26f;
    private const string BriefingVoiceResource = "i";

    private const string SpeakerName = "AAREN VALE";
    private const string BriefingText =
        "Aaren Vale, the mission starts now. The targets on this board are dangerous, and they will not wait for us to make the first move.\n\n" +
        "We have to eliminate them before their forces grow stronger, but how you complete this mission is your choice. You can move through the shadows, gather intel, and strike silently, or face them directly with full force.\n\n" +
        "Hmm, choose your path carefully, because every decision you make from here will shape the outcome of this war.";

    private Camera cameraOne;
    private Camera cameraTwo;
    private CanvasGroup overlayGroup;
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
        LockPlayerInput(true);
        SetPlayerObjectsActive(false);
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
        RestoreSceneStates();
        SetPlayerObjectsActive(true);
        SetCutsceneCameraActive(cameraOne, false);
        SetCutsceneCameraActive(cameraTwo, false);
        LockPlayerInput(false);
        UnlockAllPlayerInputsInScene(scene);
        CleanupVoiceSource();
        Destroy(overlayGroup.gameObject);
        DestroyCutsceneCameras();
        isPlaying = false;
        Destroy(gameObject);
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
        HashSet<GameObject> players = new HashSet<GameObject>();

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null || transform.gameObject.scene != scene)
            {
                continue;
            }

            if (transform.CompareTag("Player") || transform.GetComponent<vThirdPersonInput>() != null)
            {
                players.Add(transform.gameObject);
            }
        }

        foreach (GameObject player in players)
        {
            playerStates.Add(new PlayerObjectState(player));
        }
    }

    private void RestorePlayerStates()
    {
        for (int i = 0; i < playerStates.Count; i++)
        {
            playerStates[i].Restore();
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
