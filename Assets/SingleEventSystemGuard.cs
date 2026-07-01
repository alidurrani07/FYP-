using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class SingleEventSystemGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RunAfterSceneSettles();
    }

    private static void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        RunAfterSceneSettles();
    }

    private static void RunAfterSceneSettles()
    {
        RemoveDuplicateEventSystems();
        EventSystemRunner.EnsureExists().StartCoroutine(RemoveDuplicatesNextFrame());
    }

    private static IEnumerator RemoveDuplicatesNextFrame()
    {
        yield return null;
        RemoveDuplicateEventSystems();
    }

    private static void RemoveDuplicateEventSystems()
    {
        EventSystem[] eventSystems = Object.FindObjectsOfType<EventSystem>();
        if (eventSystems.Length <= 1)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        EventSystem keep = null;

        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem.gameObject.scene == activeScene)
            {
                keep = eventSystem;
                break;
            }
        }

        if (keep == null)
        {
            keep = EventSystem.current != null ? EventSystem.current : eventSystems[0];
        }

        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem != keep)
            {
                Object.Destroy(eventSystem.gameObject);
            }
        }

        EventSystem.current = keep;
    }

    private sealed class EventSystemRunner : MonoBehaviour
    {
        private static EventSystemRunner instance;

        public static EventSystemRunner EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject runner = new GameObject(nameof(SingleEventSystemGuard));
            Object.DontDestroyOnLoad(runner);
            instance = runner.AddComponent<EventSystemRunner>();
            return instance;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
