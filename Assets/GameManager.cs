using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Bomb Settings")]
    public int totalBombs = 3;

    [Header("Timer")]
    public float time = 30f;
    public Text timerText;

    public ParticleSystem explosionEffect;
    public float delayBeforeFail = 2f;

    [Header("UI")]
    public GameObject gameOverUI;
    public GameObject winUI;

    void Awake()
    {
        instance = this;

        Time.timeScale = 1f;
        HideOldMissionUI();
    }

    public void BombDefused()
    {
        Debug.Log("BombDefused ignored. Demo Scene 1 uses DemoSceneBombMission now.");
    }

    private void HideOldMissionUI()
    {
        if (timerText != null)
            timerText.gameObject.SetActive(false);

        if (winUI != null)
            winUI.SetActive(false);

        if (gameOverUI != null)
            gameOverUI.SetActive(false);
    }
}
