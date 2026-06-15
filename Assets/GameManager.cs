using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Bomb Settings")]
    public int totalBombs = 3;
    private int defusedBombs = 0;

    [Header("Timer")]
    public float time = 30f;
    public Text timerText;

    public ParticleSystem explosionEffect;
    public float delayBeforeFail = 2f;

    [Header("UI")]
    public GameObject gameOverUI;
    public GameObject winUI;

    private bool gameEnded = false;

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        if (gameEnded) return;

        time -= Time.deltaTime;

        if (timerText != null)
            timerText.text = "Time: " + Mathf.Ceil(time).ToString();

        if (time <= 0)
        {
            GameOver();
        }
    }

    public void BombDefused()
    {
        defusedBombs++;

        Debug.Log("Defused: " + defusedBombs);

        if (defusedBombs >= totalBombs)
        {
            Win();
        }
    }

    void GameOver()
    {
        StartCoroutine(GameOverSequence());
    }

    void Win()
    {
        gameEnded = true;
        if (winUI != null)
            winUI.SetActive(true);

        Time.timeScale = 0f;
    }

    System.Collections.IEnumerator GameOverSequence()
    {
        // Blast play
        if (explosionEffect != null)
            explosionEffect.Play();

        // thoda wait
        yield return new WaitForSeconds(delayBeforeFail);

        // Fail UI show
        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        Time.timeScale = 0f;
    }

}