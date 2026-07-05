using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DemoSceneBombMission : MonoBehaviour
{
    private const string DemoSceneOneName = "Demo Scene 1";
    private const string DemoSceneTwoName = "Demo Scene 2";
    private const string MissionTextObjectName = "missionUi";
    private const string BombTemplateName = "Bomb";
    private const float HoldSeconds = 2.25f;
    private const float BombGroundRayHeight = 25f;
    private const float BombGroundRayDistance = 80f;

    private readonly List<BombSpot> bombSpots = new List<BombSpot>();

    private Text missionText;
    private TMP_Text missionTmpText;
    private GameObject runtimeMissionCanvas;
    private GameObject bombTemplate;
    private BombSpot activeSpot;
    private DemoSceneExtractionSequence extractionSequence;
    private Image progressGraphic;
    private GameObject progressRoot;
    private Text progressText;
    private float holdTimer;
    private int placedBombs;
    private bool missionComplete;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<DemoSceneBombMissionBootstrap>() != null)
            return;

        GameObject bootstrapObject = new GameObject("DemoSceneBombMissionBootstrap");
        DontDestroyOnLoad(bootstrapObject);
        bootstrapObject.AddComponent<DemoSceneBombMissionBootstrap>();
    }

    private void Start()
    {
        CacheMissionText();
        CacheBombTemplate();
        RegisterBombSpots();
        extractionSequence = DemoSceneExtractionSequence.EnsureForActiveScene();
        if (extractionSequence != null)
            extractionSequence.PrepareExitObjective(this);
        CreateProgressUI();

        SetProgressVisible(false);
        UpdateMissionText();
    }

    private void Update()
    {
        if (IsCompleteMissionCheatPressed())
        {
            CompleteBombMissionForTesting();
            return;
        }

        if (missionComplete)
            return;

        if (activeSpot == null || activeSpot.IsPlaced)
        {
            ResetPlantProgress();
            return;
        }

        UpdateMissionText();
        SetProgressVisible(true);

        if (IsPlantButtonHeld())
        {
            holdTimer += Time.deltaTime;
            SetProgress(holdTimer / HoldSeconds);
            SetProgressText("PLACING BOMB");

            if (holdTimer >= HoldSeconds)
                PlaceBomb(activeSpot);
        }
        else
        {
            holdTimer = 0f;
            SetProgress(0f);
            SetProgressText("HOLD B");
        }
    }

    public void EnterSpot(BombSpot spot, GameObject player)
    {
        if (spot == null || spot.IsPlaced || missionComplete)
            return;

        activeSpot = spot;
        holdTimer = 0f;
        UpdateMissionText();
    }

    public void ExitSpot(BombSpot spot, GameObject player)
    {
        if (activeSpot != spot)
            return;

        activeSpot = null;
        ResetPlantProgress();
        UpdateMissionText();
    }

    public bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        Transform root = other.transform.root;
        return root != null && root.GetComponentInChildren<PlayerMovement>() != null;
    }

    private void CacheMissionText()
    {
        GameObject missionObject = GameObject.Find(MissionTextObjectName);
        if (missionObject != null)
        {
            missionText = missionObject.GetComponent<Text>();
            missionTmpText = missionObject.GetComponent<TMP_Text>();
            SetParentsActive(missionObject.transform);
        }

        if (missionText == null && missionTmpText == null)
            FindSceneMissionText();

        if (missionText == null && missionTmpText == null)
            CreateRuntimeMissionText();
    }

    private void FindSceneMissionText()
    {
        Text[] textComponents = Resources.FindObjectsOfTypeAll<Text>();
        for (int i = 0; i < textComponents.Length; i++)
        {
            Text text = textComponents[i];
            if (!IsSceneUiText(text) || !LooksLikeMissionText(text.gameObject.name, text.text))
                continue;

            missionText = text;
            SetParentsActive(text.transform);
            return;
        }

        TMP_Text[] tmpTextComponents = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < tmpTextComponents.Length; i++)
        {
            TMP_Text text = tmpTextComponents[i];
            if (!IsSceneUiText(text) || !LooksLikeMissionText(text.gameObject.name, text.text))
                continue;

            missionTmpText = text;
            SetParentsActive(text.transform);
            return;
        }
    }

    private bool IsSceneUiText(Component component)
    {
        if (component == null || component.gameObject.scene != gameObject.scene)
            return false;

        return component.GetComponentInParent<Canvas>(true) != null;
    }

    private bool LooksLikeMissionText(string objectName, string text)
    {
        string lowerName = string.IsNullOrEmpty(objectName) ? string.Empty : objectName.ToLowerInvariant();
        string lowerText = string.IsNullOrEmpty(text) ? string.Empty : text.ToLowerInvariant();

        return lowerName.Contains("mission")
            || lowerName.Contains("objective")
            || lowerText.Contains("bomb")
            || lowerText.Contains("mission")
            || lowerText.Contains("objective");
    }

    private void CreateRuntimeMissionText()
    {
        runtimeMissionCanvas = new GameObject("DemoMissionRuntimeCanvas");
        runtimeMissionCanvas.transform.SetParent(transform, false);

        Canvas canvas = runtimeMissionCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32750;

        CanvasScaler scaler = runtimeMissionCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        runtimeMissionCanvas.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("MissionPanel");
        panelObject.transform.SetParent(runtimeMissionCanvas.transform, false);
        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, 0.56f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -30f);
        panelRect.sizeDelta = new Vector2(980f, 74f);

        GameObject textObject = new GameObject(MissionTextObjectName);
        textObject.transform.SetParent(panelObject.transform, false);
        missionText = textObject.AddComponent<Text>();
        missionText.alignment = TextAnchor.MiddleCenter;
        missionText.color = Color.white;
        missionText.fontSize = 28;
        missionText.fontStyle = FontStyle.Bold;
        missionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        missionText.verticalOverflow = VerticalWrapMode.Overflow;
        missionText.raycastTarget = false;

        Font missionFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (missionFont == null)
            missionFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (missionFont != null)
            missionText.font = missionFont;

        RectTransform textRect = missionText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 8f);
        textRect.offsetMax = new Vector2(-24f, -8f);
    }

    private void SetParentsActive(Transform child)
    {
        Transform current = child;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);

            current = current.parent;
        }
    }

    private void CacheBombTemplate()
    {
        bombTemplate = GameObject.Find(BombTemplateName);

        if (bombTemplate != null)
            bombTemplate.SetActive(false);
    }

    private void RegisterBombSpots()
    {
        bombSpots.Clear();

        for (int index = 1; index <= 5; index++)
        {
            GameObject triggerObject = GameObject.Find("bombTrigger" + index.ToString("00"));
            if (triggerObject == null)
                continue;

            BombSpot spot = new BombSpot(triggerObject);
            bombSpots.Add(spot);
            PrepareTrigger(spot);
            PrepareMarker(spot);
        }
    }

    private void PrepareTrigger(BombSpot spot)
    {
        Collider[] colliders = GetTriggerColliders(spot.Trigger);
        if (colliders.Length == 0)
        {
            BoxCollider box = spot.Trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3.5f, 2.2f, 3.5f);
            colliders = new Collider[] { box };
        }

        foreach (Collider collider in colliders)
        {
            collider.isTrigger = true;

            BombTriggerRelay relay = collider.GetComponent<BombTriggerRelay>();
            if (relay == null)
                relay = collider.gameObject.AddComponent<BombTriggerRelay>();

            relay.Initialize(this, spot);
        }
    }

    private void PrepareMarker(BombSpot spot)
    {
        DisableExistingMarkerRenderers(spot.Trigger.transform);

        GameObject markerObject = new GameObject("VCStyleBombMarker");
        markerObject.transform.SetParent(spot.Trigger.transform, false);
        markerObject.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        markerObject.transform.localRotation = Quaternion.identity;
        markerObject.transform.localScale = Vector3.one;

        Material markerMaterial = CreatePinkMarkerMaterial();
        CreateMarkerRing(markerObject.transform, "RingHorizontal", markerMaterial, Quaternion.Euler(90f, 0f, 0f), 1.15f, 0.07f);
        CreateMarkerRing(markerObject.transform, "RingVerticalX", markerMaterial, Quaternion.Euler(0f, 90f, 0f), 1.15f, 0.055f);
        CreateMarkerRing(markerObject.transform, "RingVerticalZ", markerMaterial, Quaternion.identity, 1.15f, 0.055f);

        markerObject.AddComponent<BombMarkerRotator>();

        spot.Marker = markerObject;
    }

    private void CreateProgressUI()
    {
        GameObject canvasObject = new GameObject("BombPlantRuntimeCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        progressRoot = new GameObject("BombPlantCircularProgress");
        progressRoot.transform.SetParent(canvasObject.transform, false);

        RectTransform rootRect = progressRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.42f);
        rootRect.anchorMax = new Vector2(0.5f, 0.42f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(118f, 118f);

        Sprite circleSprite = CreateCircleSprite(128);

        GameObject backgroundObject = new GameObject("BackCircle");
        backgroundObject.transform.SetParent(progressRoot.transform, false);
        Image backgroundGraphic = backgroundObject.AddComponent<Image>();
        backgroundGraphic.sprite = circleSprite;
        backgroundGraphic.type = Image.Type.Simple;
        backgroundGraphic.color = new Color(0f, 0f, 0f, 0.5f);
        StretchToParent(backgroundObject.GetComponent<RectTransform>());

        GameObject fillObject = new GameObject("FillCircle");
        fillObject.transform.SetParent(progressRoot.transform, false);
        progressGraphic = fillObject.AddComponent<Image>();
        progressGraphic.sprite = circleSprite;
        progressGraphic.type = Image.Type.Filled;
        progressGraphic.fillMethod = Image.FillMethod.Radial360;
        progressGraphic.fillOrigin = (int)Image.Origin360.Top;
        progressGraphic.fillClockwise = true;
        progressGraphic.fillAmount = 0f;
        progressGraphic.color = new Color(1f, 0.48f, 0.04f, 0.95f);
        StretchToParent(fillObject.GetComponent<RectTransform>());

        GameObject centerObject = new GameObject("CenterCutout");
        centerObject.transform.SetParent(progressRoot.transform, false);
        Image centerGraphic = centerObject.AddComponent<Image>();
        centerGraphic.sprite = circleSprite;
        centerGraphic.color = new Color(0f, 0f, 0f, 0.72f);

        RectTransform centerRect = centerObject.GetComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.anchoredPosition = Vector2.zero;
        centerRect.sizeDelta = new Vector2(72f, 72f);

        GameObject textObject = new GameObject("HoldText");
        textObject.transform.SetParent(progressRoot.transform, false);
        progressText = textObject.AddComponent<Text>();
        progressText.text = "HOLD B";
        progressText.alignment = TextAnchor.MiddleCenter;
        progressText.color = Color.white;
        progressText.fontSize = 18;
        progressText.fontStyle = FontStyle.Bold;
        progressText.raycastTarget = false;

        Font holdFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (holdFont == null)
            holdFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (holdFont != null)
            progressText.font = holdFont;

        RectTransform textRect = progressText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void PlaceBomb(BombSpot spot)
    {
        spot.IsPlaced = true;
        placedBombs++;

        if (bombTemplate != null)
        {
            Vector3 placementPosition = GetGroundPlacementPosition(spot.Trigger.transform.position);
            GameObject plantedBomb = CreateVisiblePlantedBomb(spot, placementPosition);
            SnapBottomToGround(plantedBomb, placementPosition.y);
        }

        if (spot.Marker != null)
            spot.Marker.SetActive(false);

        activeSpot = null;
        ResetPlantProgress();

        if (placedBombs >= bombSpots.Count)
        {
            missionComplete = true;
            SetMissionText("All bombs placed. Go to the exit point.");
            if (extractionSequence == null)
                extractionSequence = DemoSceneExtractionSequence.EnsureForActiveScene();
            if (extractionSequence != null)
                extractionSequence.ActivateExitObjective(this);
        }
        else
        {
            UpdateMissionText();
        }
    }

    private void CompleteBombMissionForTesting()
    {
        if (missionComplete)
            return;

        for (int i = 0; i < bombSpots.Count; i++)
        {
            BombSpot spot = bombSpots[i];
            if (spot == null || spot.IsPlaced)
                continue;

            spot.IsPlaced = true;
            placedBombs++;

            if (bombTemplate != null)
            {
                Vector3 placementPosition = GetGroundPlacementPosition(spot.Trigger.transform.position);
                GameObject plantedBomb = CreateVisiblePlantedBomb(spot, placementPosition);
                SnapBottomToGround(plantedBomb, placementPosition.y);
            }

            if (spot.Marker != null)
                spot.Marker.SetActive(false);
        }

        activeSpot = null;
        ResetPlantProgress();
        missionComplete = true;

        if (extractionSequence == null)
            extractionSequence = DemoSceneExtractionSequence.EnsureForActiveScene();
        if (extractionSequence != null)
            extractionSequence.ActivateExitObjective(this);

        SetMissionText("Test cheat: all bombs placed. Reach the pink exit marker for extraction.");
    }

    private void UpdateMissionText()
    {
        if (missionComplete)
            return;

        if (bombSpots.Count == 0)
        {
            SetMissionText("Bomb mission setup missing. Add bombTrigger01 to bombTrigger05.");
            return;
        }

        if (activeSpot != null)
        {
            SetMissionText("Crouch and Hold \"B\" to Place Bomb");
            return;
        }

        SetMissionText("Drop Bomb in Each Side of the Corner. Go in each corner and place bombs: " + placedBombs + "/" + bombSpots.Count);
    }

    public void SetMissionStatus(string message)
    {
        SetMissionText(message);
    }

    public bool BombsCompleted
    {
        get { return missionComplete; }
    }

    private void SetMissionText(string message)
    {
        if (missionText == null && missionTmpText == null)
            CacheMissionText();

        if (missionText != null)
            missionText.text = message;

        if (missionTmpText != null)
            missionTmpText.text = message;
    }

    private bool IsPlantButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.bKey.isPressed)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.B);
#else
        return false;
#endif
    }

    private bool IsCompleteMissionCheatPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.digit7Key.wasPressedThisFrame)
            return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Alpha7);
#else
        return false;
#endif
    }

    private void ResetPlantProgress()
    {
        holdTimer = 0f;
        SetProgress(0f);
        SetProgressText("HOLD B");
        SetProgressVisible(false);
    }

    private void SetProgress(float amount)
    {
        if (progressGraphic != null)
            progressGraphic.fillAmount = Mathf.Clamp01(amount);
    }

    private void SetProgressVisible(bool visible)
    {
        if (progressRoot != null && progressRoot.activeSelf != visible)
            progressRoot.SetActive(visible);
    }

    private void SetProgressText(string message)
    {
        if (progressText != null)
            progressText.text = message;
    }

    private void DisableExistingMarkerRenderers(Transform triggerRoot)
    {
        foreach (Renderer renderer in triggerRoot.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;

            Collider markerCollider = renderer.GetComponent<Collider>();
            if (markerCollider != null)
                markerCollider.enabled = false;
        }
    }

    private Collider[] GetTriggerColliders(GameObject triggerObject)
    {
        Collider[] directColliders = triggerObject.GetComponents<Collider>();
        if (directColliders.Length > 0)
            return directColliders;

        List<Collider> triggerColliders = new List<Collider>();
        foreach (Collider childCollider in triggerObject.GetComponentsInChildren<Collider>(true))
        {
            if (childCollider.GetComponent<Renderer>() != null)
                continue;

            triggerColliders.Add(childCollider);
        }

        return triggerColliders.ToArray();
    }

    private Material CreatePinkMarkerMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = new Color(1f, 0.02f, 0.85f, 0.95f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1f, 0.05f, 0.85f) * 1.8f);
        }

        return material;
    }

    private void CreateMarkerRing(Transform parent, string name, Material material, Quaternion localRotation, float radius, float width)
    {
        GameObject ringObject = new GameObject(name);
        ringObject.transform.SetParent(parent, false);
        ringObject.transform.localRotation = localRotation;

        LineRenderer lineRenderer = ringObject.AddComponent<LineRenderer>();
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.material = material;
        lineRenderer.startColor = new Color(1f, 0.02f, 0.85f, 0.95f);
        lineRenderer.endColor = new Color(1f, 0.02f, 0.85f, 0.95f);
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.positionCount = 80;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        for (int index = 0; index < lineRenderer.positionCount; index++)
        {
            float angle = index / (float)lineRenderer.positionCount * Mathf.PI * 2f;
            lineRenderer.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.name = "BombPlantCircleSprite";
        texture.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float radius = center;
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius - distance + 1f);
                texture.SetPixel(x, y, alpha > 0f ? new Color(solid.r, solid.g, solid.b, alpha) : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Vector3 GetGroundPlacementPosition(Vector3 triggerPosition)
    {
        Vector3 rayOrigin = triggerPosition + Vector3.up * BombGroundRayHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, BombGroundRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point;

        return triggerPosition;
    }

    private GameObject CreateVisiblePlantedBomb(BombSpot spot, Vector3 placementPosition)
    {
        GameObject plantedBomb = new GameObject("Placed_" + spot.Trigger.name);
        plantedBomb.tag = BombTemplateName;
        plantedBomb.transform.position = placementPosition;
        plantedBomb.transform.rotation = bombTemplate.transform.rotation;
        plantedBomb.transform.localScale = bombTemplate.transform.lossyScale;

        int copiedMeshes = CopyBombMeshes(bombTemplate.transform, plantedBomb.transform);
        if (copiedMeshes == 0)
            CreateFallbackBombModel(plantedBomb.transform);

        plantedBomb.SetActive(true);
        return plantedBomb;
    }

    private int CopyBombMeshes(Transform sourceRoot, Transform targetRoot)
    {
        MeshFilter[] sourceFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(true);
        int copiedMeshes = 0;

        foreach (MeshFilter sourceFilter in sourceFilters)
        {
            MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
            if (sourceRenderer == null || sourceFilter.sharedMesh == null)
                continue;

            GameObject meshObject = new GameObject(sourceFilter.gameObject.name + "_Visual");
            meshObject.transform.SetParent(targetRoot, false);
            ApplyRelativeTransform(sourceRoot, sourceFilter.transform, meshObject.transform);

            MeshFilter targetFilter = meshObject.AddComponent<MeshFilter>();
            targetFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer targetRenderer = meshObject.AddComponent<MeshRenderer>();
            targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            targetRenderer.enabled = true;
            targetRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
            targetRenderer.receiveShadows = sourceRenderer.receiveShadows;

            copiedMeshes++;
        }

        return copiedMeshes;
    }

    private void ApplyRelativeTransform(Transform sourceRoot, Transform sourceTransform, Transform targetTransform)
    {
        if (sourceTransform == sourceRoot)
        {
            targetTransform.localPosition = Vector3.zero;
            targetTransform.localRotation = Quaternion.identity;
            targetTransform.localScale = Vector3.one;
            return;
        }

        targetTransform.localPosition = sourceRoot.InverseTransformPoint(sourceTransform.position);
        targetTransform.localRotation = Quaternion.Inverse(sourceRoot.rotation) * sourceTransform.rotation;
        targetTransform.localScale = DivideScale(sourceTransform.lossyScale, sourceRoot.lossyScale);
    }

    private Vector3 DivideScale(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            divisor.x == 0f ? value.x : value.x / divisor.x,
            divisor.y == 0f ? value.y : value.y / divisor.y,
            divisor.z == 0f ? value.z : value.z / divisor.z);
    }

    private void CreateFallbackBombModel(Transform parent)
    {
        Material darkMaterial = new Material(Shader.Find("Standard"));
        darkMaterial.color = new Color(0.04f, 0.04f, 0.04f, 1f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "FallbackBombBody";
        body.transform.SetParent(parent, false);
        body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        body.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
        body.GetComponent<Renderer>().material = darkMaterial;

        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
            Destroy(bodyCollider);
    }

    private void SnapBottomToGround(GameObject plantedBomb, float groundY)
    {
        plantedBomb.SetActive(true);

        Renderer renderer = plantedBomb.GetComponentInChildren<Renderer>();
        if (renderer == null)
            return;

        float bottomOffset = renderer.bounds.min.y - plantedBomb.transform.position.y;
        plantedBomb.transform.position = new Vector3(
            plantedBomb.transform.position.x,
            groundY - bottomOffset,
            plantedBomb.transform.position.z);
    }

    private void PreparePlantedBomb(GameObject plantedBomb)
    {
        foreach (BombAutoDefuse autoDefuse in plantedBomb.GetComponentsInChildren<BombAutoDefuse>(true))
            Destroy(autoDefuse);

        foreach (Renderer renderer in plantedBomb.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;

        foreach (Collider collider in plantedBomb.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = true;
            collider.isTrigger = false;
        }

        SetChildrenActive(plantedBomb.transform);
        plantedBomb.SetActive(true);
    }

    private void SetChildrenActive(Transform root)
    {
        foreach (Transform child in root)
        {
            child.gameObject.SetActive(true);
            SetChildrenActive(child);
        }
    }

    public sealed class BombSpot
    {
        public BombSpot(GameObject trigger)
        {
            Trigger = trigger;
        }

        public GameObject Trigger { get; private set; }
        public GameObject Marker { get; set; }
        public bool IsPlaced { get; set; }
    }

    private sealed class DemoSceneBombMissionBootstrap : MonoBehaviour
    {
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            EnsureMissionForScene(SceneManager.GetActiveScene());
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureMissionForScene(scene);
        }

        private void EnsureMissionForScene(Scene scene)
        {
            if (!IsDemoScene(scene.name))
                return;

            if (FindObjectOfType<DemoSceneBombMission>() != null)
                return;

            GameObject missionObject = new GameObject("DemoSceneBombMission");
            missionObject.AddComponent<DemoSceneBombMission>();
        }

        private static bool IsDemoScene(string sceneName)
        {
            return sceneName == DemoSceneOneName || sceneName == DemoSceneTwoName;
        }
    }
}

public class BombTriggerRelay : MonoBehaviour
{
    private DemoSceneBombMission mission;
    private DemoSceneBombMission.BombSpot spot;

    public void Initialize(DemoSceneBombMission missionController, DemoSceneBombMission.BombSpot bombSpot)
    {
        mission = missionController;
        spot = bombSpot;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (mission != null && mission.IsPlayer(other))
            mission.EnterSpot(spot, other.transform.root.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (mission != null && mission.IsPlayer(other))
            mission.ExitSpot(spot, other.transform.root.gameObject);
    }
}

public class BombMarkerRotator : MonoBehaviour
{
    public float rotationSpeed = 90f;

    private void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);
    }
}

public class BombPlantProgressGraphic : MaskableGraphic
{
    [Range(0f, 1f)]
    [SerializeField] private float fillAmount = 1f;
    [SerializeField] private float thickness = 18f;

    public float FillAmount
    {
        get { return fillAmount; }
        set
        {
            float clampedValue = Mathf.Clamp01(value);
            if (Mathf.Approximately(fillAmount, clampedValue))
                return;

            fillAmount = clampedValue;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        if (fillAmount <= 0f)
            return;

        Rect rect = rectTransform.rect;
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float innerRadius = Mathf.Max(0f, outerRadius - thickness);
        Vector2 center = rect.center;

        int segmentCount = Mathf.Max(3, Mathf.CeilToInt(72f * fillAmount));
        float startAngle = 90f;
        float angleRange = 360f * fillAmount;

        for (int index = 0; index <= segmentCount; index++)
        {
            float normalized = index / (float)segmentCount;
            float angle = (startAngle - angleRange * normalized) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            AddVertex(vertexHelper, center + direction * outerRadius);
            AddVertex(vertexHelper, center + direction * innerRadius);
        }

        for (int index = 0; index < segmentCount; index++)
        {
            int baseIndex = index * 2;
            vertexHelper.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
            vertexHelper.AddTriangle(baseIndex + 2, baseIndex + 1, baseIndex + 3);
        }
    }

    private void AddVertex(VertexHelper vertexHelper, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = position;
        vertexHelper.AddVert(vertex);
    }
}
