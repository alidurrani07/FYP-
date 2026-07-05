using System.Collections;
using System.Collections.Generic;
using Invector;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeathSceneRestarter : MonoBehaviour
{
    private const float RestartDelay = 1.25f;
    private static PlayerDeathSceneRestarter instance;
    private static bool restartScheduled;

    private readonly List<vHealthController> subscribedHealthControllers = new List<vHealthController>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureInstance();
        instance.StartCoroutine(instance.BindPlayersAfterFrame());
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject runner = new GameObject(nameof(PlayerDeathSceneRestarter));
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<PlayerDeathSceneRestarter>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeAll();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        restartScheduled = false;
        StartCoroutine(BindPlayersAfterFrame());
    }

    private IEnumerator BindPlayersAfterFrame()
    {
        yield return null;
        BindPlayerHealthControllers();
    }

    private void BindPlayerHealthControllers()
    {
        UnsubscribeAll();

        foreach (vHealthController health in FindPlayerHealthControllers())
        {
            if (health == null || subscribedHealthControllers.Contains(health))
            {
                continue;
            }

            health.onDead.AddListener(HandlePlayerDead);
            subscribedHealthControllers.Add(health);
        }
    }

    private IEnumerable<vHealthController> FindPlayerHealthControllers()
    {
        var results = new List<vHealthController>();

        GameObject[] players = FindPlayerTaggedObjects();
        for (int i = 0; i < players.Length; i++)
        {
            AddPlayerHealth(players[i], results);
        }

        if (results.Count == 0)
        {
            vHealthController[] allHealthControllers = FindObjectsOfType<vHealthController>(true);
            for (int i = 0; i < allHealthControllers.Length; i++)
            {
                vHealthController health = allHealthControllers[i];
                if (health != null && health.CompareTag("Player"))
                {
                    results.Add(health);
                }
            }
        }

        return results;
    }

    private static GameObject[] FindPlayerTaggedObjects()
    {
        try
        {
            return GameObject.FindGameObjectsWithTag("Player");
        }
        catch (UnityException)
        {
            return new GameObject[0];
        }
    }

    private static void AddPlayerHealth(GameObject player, List<vHealthController> results)
    {
        if (player == null)
        {
            return;
        }

        vHealthController health = player.GetComponent<vHealthController>();
        if (health == null)
        {
            health = player.GetComponentInChildren<vHealthController>(true);
        }

        if (health == null)
        {
            health = player.GetComponentInParent<vHealthController>();
        }

        if (health != null && !results.Contains(health))
        {
            results.Add(health);
        }
    }

    private void HandlePlayerDead(GameObject deadPlayer)
    {
        if (restartScheduled)
        {
            return;
        }

        restartScheduled = true;
        StartCoroutine(RestartCurrentScene());
    }

    private IEnumerator RestartCurrentScene()
    {
        yield return new WaitForSecondsRealtime(RestartDelay);

        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
        }
        else
        {
            SceneManager.LoadScene(activeScene.name);
        }
    }

    private void UnsubscribeAll()
    {
        for (int i = 0; i < subscribedHealthControllers.Count; i++)
        {
            vHealthController health = subscribedHealthControllers[i];
            if (health != null)
            {
                health.onDead.RemoveListener(HandlePlayerDead);
            }
        }

        subscribedHealthControllers.Clear();
    }
}
