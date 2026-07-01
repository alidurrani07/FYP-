using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "Level-1";
    [SerializeField] private bl_SceneLoader loadingScreenPrefab;

    public void PlayGame()
    {
        EnsureLoadingScreen();

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("Target scene name is empty.", this);
            return;
        }

        if (FindFirstObjectByType<bl_SceneLoader>() != null)
        {
            bl_SceneLoaderManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogWarning("Loading screen prefab is missing, loading scene directly.", this);
            SceneManager.LoadScene(targetSceneName);
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureLoadingScreen()
    {
        if (FindFirstObjectByType<bl_SceneLoader>() != null || loadingScreenPrefab == null)
        {
            return;
        }

        bl_SceneLoader loadingScreen = Instantiate(loadingScreenPrefab, transform);
        loadingScreen.transform.SetAsLastSibling();
    }
}
