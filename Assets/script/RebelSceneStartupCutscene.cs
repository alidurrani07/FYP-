using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class RebelSceneStartupCutscene : MonoBehaviour
{
    [Header("Scene")]
    public string rebelSceneName = "RebelScene";
    public string cutsceneParentName = "CutScenesCameras";

    [Header("Cameras")]
    public Camera mainCamera;
    public Camera camera01;
    public Camera camera02;

    [Header("UI")]
    public GameObject camera01UiText;

    [Header("Timing")]
    public float camera01Duration = 5f;
    public float camera02Duration = 5f;
    public bool pauseGameplayDuringCutscene = true;

    [Header("Camera 02 Move")]
    public float camera02MoveDistance = 12f;

    private bool hasPlayed;
    private bool changedTimeScale;
    private float savedTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateForRebelScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "RebelScene")
        {
            return;
        }

        RebelSceneStartupCutscene existing = FindSceneObjectOfType<RebelSceneStartupCutscene>(scene);
        if (existing != null)
        {
            existing.enabled = true;
            return;
        }

        GameObject runner = new GameObject("RebelSceneStartupCutscene");
        runner.AddComponent<RebelSceneStartupCutscene>();
    }

    private void Start()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            scene = SceneManager.GetActiveScene();
        }

        if (scene.name != rebelSceneName || hasPlayed)
        {
            return;
        }

        ResolveReferences(scene);

        if (mainCamera == null || camera01 == null || camera02 == null)
        {
            Debug.LogWarning("RebelSceneStartupCutscene could not find MainCamera, Camera01, or Camera02.");
            return;
        }

        StartCoroutine(PlayCutscene());
    }

    private IEnumerator PlayCutscene()
    {
        hasPlayed = true;

        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            scene = SceneManager.GetActiveScene();
        }

        Vector3 camera02StartPosition = camera02.transform.position;
        float camera01Depth = camera01.depth;
        float camera02Depth = camera02.depth;
        float previousTimeScale = Time.timeScale;

        if (pauseGameplayDuringCutscene)
        {
            savedTimeScale = previousTimeScale;
            changedTimeScale = true;
            Time.timeScale = 0f;
        }

        camera01.depth = mainCamera.depth + 100f;
        camera02.depth = mainCamera.depth + 100f;
        DisableCutsceneAudioListeners(scene);
        SetNamedSceneCamerasActive(scene, "Camera02", null);
        SetNamedSceneCamerasActive(scene, "Camera01", camera01);
        SetObjectActive(camera01UiText, true);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, camera01Duration));

        SetNamedSceneCamerasActive(scene, "Camera01", null);
        SetObjectActive(camera01UiText, false);
        SetNamedSceneCamerasActive(scene, "Camera02", camera02);

        Vector3 startPosition = camera02.transform.position;
        Vector3 endPosition = startPosition + camera02.transform.right * camera02MoveDistance;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, camera02Duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            camera02.transform.position = Vector3.Lerp(startPosition, endPosition, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        camera02.transform.position = endPosition;
        SetNamedSceneCamerasActive(scene, "Camera02", null);

        if (pauseGameplayDuringCutscene)
        {
            Time.timeScale = previousTimeScale;
            changedTimeScale = false;
        }

        camera01.depth = camera01Depth;
        camera02.depth = camera02Depth;
        camera02.transform.position = camera02StartPosition;
    }

    private void OnDisable()
    {
        RestoreTimeScaleIfNeeded();
    }

    private void OnDestroy()
    {
        RestoreTimeScaleIfNeeded();
    }

    private void ResolveReferences(Scene scene)
    {
        Transform cutsceneParent = FindSceneTransform(scene, cutsceneParentName);

        if (mainCamera == null)
        {
            mainCamera = Camera.main != null && Camera.main.gameObject.scene == scene
                ? Camera.main
                : FindSceneCamera(scene, "MainCamera", null);
        }

        if (camera01 == null)
        {
            camera01 = FindSceneCamera(scene, "Camera01", cutsceneParent);
        }

        if (camera02 == null)
        {
            camera02 = FindSceneCamera(scene, "Camera02", cutsceneParent);
        }

        if (camera01UiText == null)
        {
            Transform textTransform = FindSceneTransform(scene, "Camera01UIText");
            if (textTransform == null)
            {
                textTransform = FindSceneTransform(scene, "Camera01UiText");
            }

            if (textTransform != null)
            {
                camera01UiText = textTransform.gameObject;
            }
        }
    }

    private static Camera FindSceneCamera(Scene scene, string cameraName, Transform preferredParent)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();

        if (preferredParent != null)
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate != null &&
                    candidate.gameObject.scene == scene &&
                    candidate.name == cameraName &&
                    candidate.transform.IsChildOf(preferredParent))
                {
                    return candidate;
                }
            }
        }

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

    private static void SetObjectActive(GameObject targetObject, bool active)
    {
        if (targetObject != null)
        {
            targetObject.SetActive(active);
        }
    }

    private static void SetNamedSceneCamerasActive(Scene scene, string cameraName, Camera activeCamera)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || candidate.gameObject.scene != scene || candidate.name != cameraName)
            {
                continue;
            }

            candidate.gameObject.SetActive(candidate == activeCamera);
        }
    }

    private static void DisableCutsceneAudioListeners(Scene scene)
    {
        AudioListener[] listeners = Resources.FindObjectsOfTypeAll<AudioListener>();
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null || listener.gameObject.scene != scene)
            {
                continue;
            }

            Camera listenerCamera = listener.GetComponent<Camera>();
            if (listenerCamera != null && listenerCamera.name != "MainCamera")
            {
                listener.enabled = false;
            }
        }
    }

    private void RestoreTimeScaleIfNeeded()
    {
        if (!changedTimeScale)
        {
            return;
        }

        Time.timeScale = savedTimeScale;
        changedTimeScale = false;
    }
}
