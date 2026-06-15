using UnityEngine;
using System.Collections;
using TMPro;

public class CutsceneTrigger : MonoBehaviour
{
    [Header("Trigger")]
    public string playerTag = "Player";
    private bool hasTriggered = false;

    [Header("Objects")]
    public GameObject parentObject;

    [Header("Cameras")]
    public Camera mainCamera;
    public Camera camera01;
    public Camera camera02;

    [Header("Camera Movement")]
    public float moveSpeed = 2f;
    public float moveDuration = 4f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float targetFOV = 30f;

    [Header("UI Text")]
    [TextArea(3, 5)] public string displayText = "Your cutscene dialogue goes here...";
    public float typingSpeed = 0.05f;
    public int fontSize = 48;

    private GameObject canvasGO;
    private TextMeshProUGUI uiText;
    private CanvasGroup canvasGroup;

    void OnTriggerEnter(Collider other)
    {
        if (!hasTriggered && other.CompareTag(playerTag))
        {
            hasTriggered = true;
            StartCoroutine(PlayCutscene());
        }
    }

    IEnumerator PlayCutscene()
    {
        // Activate parent
        parentObject.SetActive(true);

        // Switch cameras
        mainCamera.gameObject.SetActive(false);
        camera01.gameObject.SetActive(true);

        // Create UI
        CreateUI();

        // Fade in UI
        yield return StartCoroutine(FadeUI(0, 1, 1f));

        // Start typing text
        yield return StartCoroutine(TypeText(displayText));

        // Move camera01 forward
        float timer = 0f;
        while (timer < moveDuration)
        {
            camera01.transform.position += camera01.transform.forward * moveSpeed * Time.deltaTime;
            timer += Time.deltaTime;
            yield return null;
        }

        // Switch to camera02
        camera01.gameObject.SetActive(false);
        camera02.gameObject.SetActive(true);

        // Zoom effect
        float startFOV = camera02.fieldOfView;
        float t = 0f;
        while (Mathf.Abs(camera02.fieldOfView - targetFOV) > 0.1f)
        {
            camera02.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t);
            t += Time.deltaTime * zoomSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(1.5f);

        // Fade out UI
        yield return StartCoroutine(FadeUI(1, 0, 1f));

        // Cleanup
        Destroy(canvasGO);
        parentObject.SetActive(false);

        // Return to main camera
        camera02.gameObject.SetActive(false);
        mainCamera.gameObject.SetActive(true);
    }

    void CreateUI()
    {
        canvasGO = new GameObject("CutsceneCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0;

        // 🔳 BLACK PANEL (Background)
        GameObject panelGO = new GameObject("BackgroundPanel");
        panelGO.transform.SetParent(canvasGO.transform);

        UnityEngine.UI.Image panelImage = panelGO.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0, 0, 0, 0.6f); // black with transparency

        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.1f, 0.1f); // adjust area
        panelRect.anchorMax = new Vector2(0.9f, 0.3f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 📝 TEXT
        GameObject textGO = new GameObject("TMP_Text");
        textGO.transform.SetParent(panelGO.transform); // IMPORTANT: child of panel

        uiText = textGO.AddComponent<TextMeshProUGUI>();
        uiText.text = "";
        uiText.fontSize = fontSize;
        uiText.alignment = TextAlignmentOptions.Center;
        uiText.color = Color.white;

        RectTransform rect = uiText.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(20, 10);   // padding
        rect.offsetMax = new Vector2(-20, -10);
    }

    IEnumerator TypeText(string text)
    {
        uiText.text = "";
        foreach (char c in text)
        {
            uiText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
    }

    IEnumerator FadeUI(float start, float end, float duration)
    {
        float t = 0;
        while (t < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(start, end, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = end;
    }
}