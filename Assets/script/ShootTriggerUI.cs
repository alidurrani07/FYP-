using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ShootTriggerUI : MonoBehaviour
{
    [Header("Player Settings")]
    public string playerTag = "Player";
    public Text scenename;
    [Header("Drain Settings")]
    public float drainDuration = 12f;

    private float currentValue = 1f;
    private float drainSpeed;

    private bool playerInside;
    private bool isActive;

    private GameObject canvasGO;
    private Image yellowBar;
    private TextMeshProUGUI uiText;

    void Start()
    {
        drainSpeed = 1f / drainDuration;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = true;

            if (!isActive)
            {
                CreateUI();
                isActive = true;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInside = false;
        }
    }

    void Update()
    {
        if (!isActive) return;

        if (playerInside)
        {
            currentValue -= drainSpeed * Time.deltaTime;
            currentValue = Mathf.Clamp01(currentValue);

            UpdateBar();

            if (currentValue <= 0f)
            {
                OnBarEmpty();
                Cleanup();
            }
        }
    }

    // ================= UI =================
    void CreateUI()
    {
        canvasGO = new GameObject("RuntimeCanvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // ================= TEXT (TMP) =================
        GameObject textObj = new GameObject("ObjectiveText");
        textObj.transform.SetParent(canvasGO.transform, false);

        uiText = textObj.AddComponent<TextMeshProUGUI>();
        uiText.text = "STAY IN CAMP TO CONQUER AREA\nMOVE TO ENEMY AREA";
        uiText.fontSize = 28;
        uiText.alignment = TextAlignmentOptions.Center;
        uiText.color = Color.white;

        RectTransform textRect = uiText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.3f, 0.95f);
        textRect.anchorMax = new Vector2(0.7f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // ================= RED BACKGROUND =================
        GameObject bg = new GameObject("RedBG");
        bg.transform.SetParent(canvasGO.transform, false);

        Image redBar = bg.AddComponent<Image>();
        redBar.color = Color.red;

        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.3f, 0.9f);
        bgRect.anchorMax = new Vector2(0.7f, 0.95f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // ================= YELLOW FILL =================
        GameObject fill = new GameObject("YellowBar");
        fill.transform.SetParent(bg.transform, false);

        yellowBar = fill.AddComponent<Image>();
        yellowBar.color = Color.yellow;
        yellowBar.type = Image.Type.Simple;

        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    void UpdateBar()
    {
        if (yellowBar != null)
        {
            yellowBar.rectTransform.anchorMax = new Vector2(currentValue, 1f);
        }
    }

    void Cleanup()
    {
        isActive = false;
        playerInside = false;

        if (canvasGO != null)
            Destroy(canvasGO);

        currentValue = 1f;
    }

    void OnBarEmpty()
    {
        Debug.Log("✔ AREA CONQUERED → TRIGGER YOUR EVENT HERE");
        var currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "ArmyScene")
        {
            SceneManager.LoadScene("Demo Scene 2");
        }
        else if (currentScene == "RebelScene")
        {
            SceneManager.LoadScene("Demo Scene 1");
        }
        else
        {
            Debug.LogWarning($"Unknown scene '{currentScene}'. No demo scene loaded.");
        }
    }
}
