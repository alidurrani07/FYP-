using UnityEngine;
using UnityEngine.UI;

public class CampFireInteraction : MonoBehaviour
{
    [Header("Only Drag This")]
    public GameObject fireObject;

    [Header("Settings")]
    public KeyCode interactKey = KeyCode.E;

    private bool playerInRange = false;
    private bool fireOn = false;

    private GameObject canvasObj;
    private Text interactionText;

    void Start()
    {
        fireObject.SetActive(false);
        CreateUI();
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            ToggleFire();
        }
    }

    void CreateUI()
    {
        // Create Canvas
        canvasObj = new GameObject("InteractionCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create Text
        GameObject textObj = new GameObject("InteractionText");
        textObj.transform.SetParent(canvasObj.transform);

        interactionText = textObj.AddComponent<Text>();
        interactionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        interactionText.fontSize = 28;
        interactionText.alignment = TextAnchor.MiddleCenter;
        interactionText.color = Color.white;

        // Position (bottom center)
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 100);
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 100);

        canvasObj.SetActive(false);
    }

    void ToggleFire()
    {
        fireOn = !fireOn;
        fireObject.SetActive(fireOn);
        UpdateText();
    }

    void UpdateText()
    {
        interactionText.text = fireOn
            ? "Press E to Close Fire"
            : "Press E to Light Fire";
    }

    // ✅ 3D Trigger Enter
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            canvasObj.SetActive(true);
            UpdateText();
        }
    }

    // ✅ 3D Trigger Exit
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            canvasObj.SetActive(false);
        }
    }
}