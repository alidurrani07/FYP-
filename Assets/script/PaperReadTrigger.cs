using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PaperReadTrigger : MonoBehaviour
{
    [Header("UI Elements")]
    public CanvasGroup panelGroup;
    public Text typingText;
    public GameObject buttonsGroup;

    [Header("Typing Settings")]
    [TextArea(3, 8)]
    public string fullText;
    public float typingSpeed = 0.05f;

    [Header("Presentation")]
    public bool buildPolishedRuntimeUI = true;
    public float revealDelay = 1.1f;
    public bool showCursorForChoice = true;
    public KeyCode interactKey = KeyCode.E;

    private bool triggered = false;
    private bool playerInRange = false;
    private bool canChoose = false;
    private Sprite parchmentSprite;
    private Sprite woodSprite;
    private Sprite vignetteSprite;
    private Sprite circleSprite;
    private Font runtimeFont;

    private const string DefaultChoiceText = "Where would you like to join?";

    private void Start()
    {
        if (buildPolishedRuntimeUI)
        {
            BuildPolishedRuntimeUI();
        }

        if (panelGroup != null)
        {
            panelGroup.gameObject.SetActive(false);
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (typingText != null)
        {
            typingText.text = "";
        }

        if (buttonsGroup != null)
        {
            buttonsGroup.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerInRange && !triggered && Input.GetKeyDown(interactKey))
        {
            triggered = true;
            StartCoroutine(Sequence());
        }

        if (!canChoose) return;

        if (Input.GetKeyDown(KeyCode.A))
        {
            LoadArmyScene();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            LoadRebelScene();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    private IEnumerator Sequence()
    {
        yield return new WaitForSeconds(revealDelay);

        if (panelGroup == null || typingText == null || buttonsGroup == null)
        {
            Debug.LogWarning("PaperReadTrigger is missing UI references.");
            yield break;
        }

        panelGroup.gameObject.SetActive(true);
        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = true;
        panelGroup.transform.localScale = Vector3.one;

        panelGroup.DOFade(1f, 0.85f).SetEase(Ease.OutQuad);
        panelGroup.transform.DOScale(1f, 0.85f)
            .From(0.78f)
            .SetEase(Ease.OutBack);

        yield return new WaitForSeconds(0.45f);

        StartCoroutine(TypeText());
    }

    private IEnumerator TypeText()
    {
        typingText.text = "";

        foreach (char c in GetChoiceText())
        {
            typingText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }

        buttonsGroup.SetActive(true);

        CanvasGroup buttonsCanvas = buttonsGroup.GetComponent<CanvasGroup>();
        if (buttonsCanvas != null)
        {
            buttonsCanvas.alpha = 0f;
            buttonsCanvas.DOFade(1f, 0.35f).SetEase(Ease.OutQuad);
        }

        foreach (Transform child in buttonsGroup.transform)
        {
            child.localScale = Vector3.one * 0.92f;
            child.DOScale(1f, 0.45f).SetEase(Ease.OutBack);
        }

        panelGroup.interactable = true;
        canChoose = true;

        if (showCursorForChoice)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        Selectable firstButton = buttonsGroup.GetComponentInChildren<Selectable>();
        if (EventSystem.current != null && firstButton != null)
        {
            EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
        }
    }

    public void LoadRebelScene()
    {
        ChooseScene("RebelScene");
    }

    public void LoadArmyScene()
    {
        ChooseScene("ArmyScene");
    }

    private void ChooseScene(string sceneName)
    {
        if (!canChoose && panelGroup != null && panelGroup.gameObject.activeInHierarchy)
        {
            return;
        }

        canChoose = false;

        if (!isActiveAndEnabled)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        StartCoroutine(LoadSceneAfterChoice(sceneName));
    }

    private IEnumerator LoadSceneAfterChoice(string sceneName)
    {
        if (panelGroup != null)
        {
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = true;
            panelGroup.DOFade(0f, 0.28f).SetEase(Ease.InQuad);
            panelGroup.transform.DOScale(0.96f, 0.28f).SetEase(Ease.InQuad);
            yield return new WaitForSeconds(0.3f);
        }

        SceneManager.LoadScene(sceneName);
    }

    private string GetChoiceText()
    {
        string text = string.IsNullOrWhiteSpace(fullText) ? DefaultChoiceText : fullText.Trim();

        if (text == "Go To Army Side" || text == "Go To Rebel Side")
        {
            return DefaultChoiceText;
        }

        return text;
    }

    private void BuildPolishedRuntimeUI()
    {
        Canvas canvas = CreateRuntimeCanvas();
        EnsureEventSystem();

        if (panelGroup != null)
        {
            panelGroup.gameObject.SetActive(false);
        }

        runtimeFont = ResolveFont();
        parchmentSprite = CreateParchmentSprite();
        woodSprite = CreateWoodSprite();
        vignetteSprite = CreateVignetteSprite();
        circleSprite = CreateCircleSprite();

        GameObject root = CreateUIObject("Allegiance Choice - Polished Runtime UI", canvas.transform);
        root.layer = 5;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
        panelGroup = rootGroup;

        Image backdrop = AddImage(root.transform, "War Room Backdrop", null, new Color(0.015f, 0.014f, 0.012f, 0.76f));
        Stretch(backdrop.rectTransform);

        Image vignette = AddImage(root.transform, "Lens Vignette", vignetteSprite, Color.white);
        Stretch(vignette.rectTransform);
        vignette.raycastTarget = false;

        Image shadow = AddImage(root.transform, "Board Shadow", null, new Color(0f, 0f, 0f, 0.5f));
        Center(shadow.rectTransform, new Vector2(930f, 590f), new Vector2(0f, -18f));
        shadow.raycastTarget = false;

        Image parchment = AddImage(root.transform, "Parchment Paper", parchmentSprite, Color.white);
        Center(parchment.rectTransform, new Vector2(900f, 570f), Vector2.zero);
        parchment.type = Image.Type.Sliced;
        parchment.raycastTarget = false;

        AddWoodBorder(root.transform);
        AddCornerStuds(root.transform);

        Image header = AddImage(root.transform, "Carved Wooden Header", woodSprite, new Color(0.55f, 0.34f, 0.16f, 1f));
        Center(header.rectTransform, new Vector2(640f, 92f), new Vector2(0f, 207f));
        header.type = Image.Type.Sliced;
        AddOutline(header.gameObject, new Color(0.09f, 0.045f, 0.02f, 0.9f), new Vector2(4f, -4f));

        Text title = AddText(root.transform, "Header Title", "WHERE WILL YOU STAND?", 40, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.96f, 0.82f, 0.45f, 1f), new Vector2(0f, 208f), new Vector2(620f, 72f));
        AddTextShadow(title, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -3f));

        Text subtitle = AddText(root.transform, "Dispatch Subtitle", "A sealed order lies in your hands. One answer decides your war.", 23, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.22f, 0.12f, 0.055f, 1f), new Vector2(0f, 135f), new Vector2(720f, 44f));
        subtitle.resizeTextForBestFit = true;
        subtitle.resizeTextMinSize = 16;
        subtitle.resizeTextMaxSize = 23;

        typingText = AddText(root.transform, "Typed Choice Question", "", 36, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.11f, 0.07f, 0.035f, 1f), new Vector2(0f, 67f), new Vector2(725f, 82f));
        typingText.resizeTextForBestFit = true;
        typingText.resizeTextMinSize = 22;
        typingText.resizeTextMaxSize = 36;

        buttonsGroup = CreateUIObject("Faction Choice Cards", root.transform);
        RectTransform buttonsRect = buttonsGroup.GetComponent<RectTransform>();
        Center(buttonsRect, new Vector2(760f, 250f), new Vector2(0f, -95f));
        buttonsGroup.AddComponent<CanvasGroup>();

        CreateFactionButton(buttonsGroup.transform, "Army Side", "Discipline. Supply lines. Heavy firepower.", "A", new Vector2(-205f, 15f), new Color(0.15f, 0.23f, 0.14f, 1f), new Color(0.83f, 0.69f, 0.32f, 1f), LoadArmyScene);
        CreateFactionButton(buttonsGroup.transform, "Rebel Side", "Ambush tactics. Hidden routes. Fast strikes.", "R", new Vector2(205f, 15f), new Color(0.27f, 0.07f, 0.07f, 1f), new Color(0.95f, 0.53f, 0.36f, 1f), LoadRebelScene);

        Text hint = AddText(buttonsGroup.transform, "Keyboard Hint", "Press A for Army Side     |     Press R for Rebel Side", 19, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.18f, 0.11f, 0.06f, 0.92f), new Vector2(0f, -112f), new Vector2(720f, 32f));
        hint.resizeTextForBestFit = true;
        hint.resizeTextMinSize = 13;
        hint.resizeTextMaxSize = 19;

        buttonsGroup.SetActive(false);
    }

    private Canvas CreateRuntimeCanvas()
    {
        GameObject canvasObject = new GameObject("Paper Choice Runtime Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetAsLastSibling();
    }

    private void AddWoodBorder(Transform parent)
    {
        Image top = AddImage(parent, "Top Wooden Rail", woodSprite, new Color(0.45f, 0.27f, 0.12f, 1f));
        Center(top.rectTransform, new Vector2(960f, 58f), new Vector2(0f, 285f));
        top.type = Image.Type.Sliced;

        Image bottom = AddImage(parent, "Bottom Wooden Rail", woodSprite, new Color(0.36f, 0.2f, 0.09f, 1f));
        Center(bottom.rectTransform, new Vector2(960f, 58f), new Vector2(0f, -285f));
        bottom.type = Image.Type.Sliced;

        Image left = AddImage(parent, "Left Wooden Rail", woodSprite, new Color(0.39f, 0.23f, 0.11f, 1f));
        Center(left.rectTransform, new Vector2(58f, 600f), new Vector2(-450f, 0f));
        left.type = Image.Type.Sliced;

        Image right = AddImage(parent, "Right Wooden Rail", woodSprite, new Color(0.39f, 0.23f, 0.11f, 1f));
        Center(right.rectTransform, new Vector2(58f, 600f), new Vector2(450f, 0f));
        right.type = Image.Type.Sliced;
    }

    private void AddCornerStuds(Transform parent)
    {
        Vector2[] positions =
        {
            new Vector2(-430f, 265f),
            new Vector2(430f, 265f),
            new Vector2(-430f, -265f),
            new Vector2(430f, -265f)
        };

        foreach (Vector2 position in positions)
        {
            Image stud = AddImage(parent, "Iron Corner Stud", circleSprite, new Color(0.12f, 0.11f, 0.1f, 1f));
            Center(stud.rectTransform, new Vector2(38f, 38f), position);
            AddOutline(stud.gameObject, new Color(0.72f, 0.62f, 0.46f, 0.6f), new Vector2(2f, -2f));
        }
    }

    private void CreateFactionButton(Transform parent, string title, string description, string key, Vector2 position, Color baseColor, Color accentColor, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateUIObject(title + " Button", parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        Center(rect, new Vector2(342f, 190f), position);

        Image background = buttonObject.AddComponent<Image>();
        background.sprite = parchmentSprite;
        background.type = Image.Type.Sliced;
        background.color = Color.Lerp(baseColor, Color.white, 0.08f);
        background.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = background.color;
        colors.highlightedColor = Color.Lerp(background.color, accentColor, 0.35f);
        colors.pressedColor = Color.Lerp(background.color, Color.black, 0.2f);
        colors.selectedColor = Color.Lerp(background.color, accentColor, 0.25f);
        colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.6f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.onClick.AddListener(action);

        AddOutline(buttonObject, new Color(0.05f, 0.035f, 0.02f, 0.9f), new Vector2(4f, -4f));

        Image topStripe = AddImage(buttonObject.transform, "Faction Accent", null, accentColor);
        Center(topStripe.rectTransform, new Vector2(310f, 8f), new Vector2(0f, 77f));

        Text keyText = AddText(buttonObject.transform, "Shortcut Key", key, 22, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.08f, 0.055f, 0.03f, 1f), new Vector2(-130f, 44f), new Vector2(48f, 42f));
        Image keyBacking = AddImage(buttonObject.transform, "Shortcut Key Backing", circleSprite, accentColor);
        Center(keyBacking.rectTransform, new Vector2(48f, 48f), new Vector2(-130f, 44f));
        keyBacking.transform.SetAsFirstSibling();

        Text titleText = AddText(buttonObject.transform, "Faction Title", title.ToUpperInvariant(), 31, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.98f, 0.89f, 0.63f, 1f), new Vector2(36f, 45f), new Vector2(210f, 48f));
        AddTextShadow(titleText, new Color(0f, 0f, 0f, 0.7f), new Vector2(2f, -2f));

        Text descriptionText = AddText(buttonObject.transform, "Faction Description", description, 20, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.92f, 0.82f, 0.61f, 0.95f), new Vector2(0f, -30f), new Vector2(268f, 70f));
        descriptionText.resizeTextForBestFit = true;
        descriptionText.resizeTextMinSize = 14;
        descriptionText.resizeTextMaxSize = 20;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject imageObject = CreateUIObject(name, parent);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private Text AddText(Transform parent, string name, string text, int size, FontStyle style, TextAnchor alignment, Color color, Vector2 position, Vector2 rectSize)
    {
        GameObject textObject = CreateUIObject(name, parent);
        Text uiText = textObject.AddComponent<Text>();
        uiText.font = runtimeFont;
        uiText.text = text;
        uiText.fontSize = size;
        uiText.fontStyle = style;
        uiText.alignment = alignment;
        uiText.color = color;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Truncate;
        uiText.raycastTarget = false;
        Center(uiText.rectTransform, rectSize, position);
        return uiText;
    }

    private void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private void AddTextShadow(Text target, Color color, Vector2 distance)
    {
        Shadow shadow = target.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private Font ResolveFont()
    {
        if (typingText != null && typingText.font != null)
        {
            return typingText.font;
        }

        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return font;
    }

    private Sprite CreateParchmentSprite()
    {
        Texture2D texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float nx = x / (float)(texture.width - 1);
                float ny = y / (float)(texture.height - 1);
                float edge = Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(ny, 1f - ny));
                float noise = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.1f;
                float stain = Mathf.PerlinNoise((x + 91f) * 0.018f, (y + 37f) * 0.018f) * 0.08f;
                float burn = Mathf.SmoothStep(0.03f, 0.18f, edge);
                Color color = Color.Lerp(new Color(0.43f, 0.25f, 0.11f, 1f), new Color(0.86f + noise, 0.72f + noise * 0.45f, 0.47f + stain, 1f), burn);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32f, 32f, 32f, 32f));
    }

    private Sprite CreateWoodSprite()
    {
        Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float grain = Mathf.PerlinNoise(x * 0.07f, y * 0.018f);
                float rings = Mathf.Sin((x * 0.17f) + grain * 5.5f) * 0.06f;
                float value = Mathf.Clamp01(0.48f + grain * 0.22f + rings);
                texture.SetPixel(x, y, new Color(0.31f + value * 0.24f, 0.16f + value * 0.15f, 0.065f + value * 0.08f, 1f));
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18f, 18f, 18f, 18f));
    }

    private Sprite CreateVignetteSprite()
    {
        Texture2D texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(127.5f, 127.5f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / 128f;
                float alpha = Mathf.SmoothStep(0.25f, 1f, distance) * 0.55f;
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateCircleSprite()
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(31.5f, 31.5f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.SmoothStep(29f, 32f, distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}
