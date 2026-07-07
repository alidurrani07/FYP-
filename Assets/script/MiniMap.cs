using System.Collections;
using System.Collections.Generic;
using Invector;
using Invector.vCharacterController;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-120)]
public class MiniMap : MonoBehaviour
{
    private const string MiniMapImageName = "miniMap";
    private const string PlayerModelName = "3D Model";
    private const float ResolveInterval = 0.5f;
    private const int MaxWorldMarkers = 42;

    private static MiniMap instance;
    private static Sprite circleSprite;
    private static Sprite triangleSprite;
    private static Sprite squareSprite;
    private static Sprite diamondSprite;
    private static Sprite ringSprite;
    private static Sprite targetSprite;
    private static Sprite arrowRightSprite;

    [Header("World Tracking")]
    public Transform player;
    public Transform playerModel;
    public float mapWorldRadius = 105f;
    public bool rotateMapWithPlayer = true;

    [Header("UI")]
    public Image miniMapImage;
    public float mapSize = 184f;
    public Color mapColor = new Color(0.035f, 0.06f, 0.065f, 0.78f);
    public Color ringColor = new Color(0.08f, 0.95f, 0.82f, 0.95f);
    public Color playerColor = new Color(1f, 1f, 1f, 1f);
    public Color letterColor = new Color(1f, 0.86f, 0.18f, 1f);
    public Color structureColor = new Color(0.25f, 0.82f, 0.88f, 0.72f);
    public Color campColor = new Color(1f, 0.42f, 0.2f, 0.82f);

    private RectTransform mapRect;
    private RectTransform rotateRoot;
    private RectTransform staticRoot;
    private RectTransform playerIcon;
    private RectTransform pulseRing;
    private Image directionWedge;
    private Text distanceText;
    private readonly List<MapMarker> markers = new List<MapMarker>();
    private readonly List<Transform> candidates = new List<Transform>();
    private float nextResolveTime;
    private bool configured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeMiniMap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject runner = new GameObject(nameof(MiniMap));
        DontDestroyOnLoad(runner);
        instance = runner.AddComponent<MiniMap>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(DelayedConfigure());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        configured = false;
        markers.Clear();
        candidates.Clear();
        StartCoroutine(DelayedConfigure());
    }

    private IEnumerator DelayedConfigure()
    {
        yield return null;
        ConfigureForCurrentScene();
        yield return new WaitForSeconds(0.35f);
        ConfigureForCurrentScene();
        yield return new WaitForSeconds(1.25f);
        ConfigureForCurrentScene();
    }

    private void Update()
    {
        if (!configured || Time.unscaledTime >= nextResolveTime)
        {
            nextResolveTime = Time.unscaledTime + ResolveInterval;
            ConfigureForCurrentScene();
        }

        bool hasActiveModel = playerModel != null && playerModel.gameObject.activeInHierarchy;
        SetVisible(hasActiveModel);

        if (!hasActiveModel || player == null || mapRect == null)
        {
            return;
        }

        UpdateMapRotation();
        UpdateMarkers();
        UpdateObjectiveDistance();
        UpdatePulse();
    }

    private void ConfigureForCurrentScene()
    {
        ResolvePlayer();
        ResolveMiniMapImage();

        if (miniMapImage == null)
        {
            return;
        }

        BuildUi();
        ResolveWorldMarkers();
        configured = player != null && playerModel != null && miniMapImage != null;
        SetVisible(configured && playerModel != null && playerModel.gameObject.activeInHierarchy);
    }

    private void ResolveMiniMapImage()
    {
        if (miniMapImage != null)
        {
            return;
        }

        Image[] images = FindObjectsOfType<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject.name == MiniMapImageName)
            {
                miniMapImage = images[i];
                return;
            }
        }

        if (miniMapImage == null)
        {
            CreateMiniMapUiFallback();
        }
    }

    private void ResolvePlayer()
    {
        if (player != null && player.gameObject.activeInHierarchy)
        {
            if (playerModel == null || !playerModel.gameObject.activeInHierarchy)
            {
                playerModel = FindChildByName(player, PlayerModelName);
            }

            return;
        }

        if (vGameController.instance != null && vGameController.instance.currentPlayer != null)
        {
            player = vGameController.instance.currentPlayer.transform;
            playerModel = FindPreferredPlayerModel(player);
            return;
        }

        GameObject taggedPlayer = null;
        try
        {
            taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            taggedPlayer = null;
        }

        if (taggedPlayer != null)
        {
            player = taggedPlayer.transform;
            playerModel = FindPreferredPlayerModel(player);
            return;
        }

        vThirdPersonController controller = FindObjectOfType<vThirdPersonController>(true);
        if (controller != null)
        {
            player = controller.transform;
            playerModel = FindPreferredPlayerModel(player);
        }
    }

    private void BuildUi()
    {
        if (miniMapImage == null)
        {
            return;
        }

        mapRect = miniMapImage.rectTransform;
        mapRect.anchorMin = new Vector2(0f, 0f);
        mapRect.anchorMax = new Vector2(0f, 0f);
        mapRect.pivot = new Vector2(0f, 0f);
        mapRect.anchoredPosition = new Vector2(24f, 24f);
        mapRect.sizeDelta = Vector2.one * mapSize;
        mapRect.localScale = Vector3.one;

        miniMapImage.enabled = true;
        miniMapImage.sprite = GetCircleSprite();
        miniMapImage.color = mapColor;
        miniMapImage.type = Image.Type.Simple;
        miniMapImage.preserveAspect = true;
        miniMapImage.raycastTarget = false;

        Mask mask = miniMapImage.GetComponent<Mask>();
        if (mask == null)
        {
            mask = miniMapImage.gameObject.AddComponent<Mask>();
        }

        mask.showMaskGraphic = true;

        rotateRoot = EnsureRect(mapRect, "MiniMap_RotatingWorld");
        StretchToParent(rotateRoot);
        staticRoot = EnsureRect(mapRect, "MiniMap_StaticHud");
        StretchToParent(staticRoot);

        EnsureRadarGrid(rotateRoot);
        EnsureRing(staticRoot);
        EnsurePlayerIcon(staticRoot);
        EnsureObjectivePulse(staticRoot);
        EnsureDirectionWedge(staticRoot);
        EnsureNorthLabel(staticRoot);
        EnsureDistanceText(staticRoot);
    }

    private void CreateMiniMapUiFallback()
    {
        GameObject canvasObject = new GameObject("MiniMapRuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasObject);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject(MiniMapImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        miniMapImage = imageObject.GetComponent<Image>();
    }

    private void EnsureRadarGrid(RectTransform parent)
    {
        for (int i = 1; i <= 3; i++)
        {
            RectTransform ring = EnsureRect(parent, "MiniMap_RangeRing_" + i);
            ring.anchorMin = new Vector2(0.5f, 0.5f);
            ring.anchorMax = new Vector2(0.5f, 0.5f);
            ring.pivot = new Vector2(0.5f, 0.5f);
            ring.anchoredPosition = Vector2.zero;
            ring.sizeDelta = Vector2.one * (mapSize * (0.28f + i * 0.19f));

            Image image = EnsureImage(ring.gameObject);
            image.sprite = GetRingSprite();
            image.color = new Color(0.5f, 1f, 0.9f, 0.09f);
            image.raycastTarget = false;
        }

        EnsureGridLine(parent, "MiniMap_GridHorizontal", true);
        EnsureGridLine(parent, "MiniMap_GridVertical", false);
    }

    private void EnsureGridLine(RectTransform parent, string name, bool horizontal)
    {
        RectTransform line = EnsureRect(parent, name);
        line.anchorMin = new Vector2(0.5f, 0.5f);
        line.anchorMax = new Vector2(0.5f, 0.5f);
        line.pivot = new Vector2(0.5f, 0.5f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = horizontal ? new Vector2(mapSize * 0.84f, 1.5f) : new Vector2(1.5f, mapSize * 0.84f);

        Image image = EnsureImage(line.gameObject);
        image.sprite = null;
        image.color = new Color(0.4f, 1f, 0.92f, 0.08f);
        image.raycastTarget = false;
    }

    private void EnsureRing(RectTransform parent)
    {
        RectTransform ring = EnsureRect(parent, "MiniMap_OuterRing");
        StretchToParent(ring);
        Image image = EnsureImage(ring.gameObject);
        image.sprite = GetRingSprite();
        image.color = ringColor;
        image.raycastTarget = false;
    }

    private void EnsurePlayerIcon(RectTransform parent)
    {
        playerIcon = EnsureRect(parent, "MiniMap_PlayerArrow");
        playerIcon.anchorMin = new Vector2(0.5f, 0.5f);
        playerIcon.anchorMax = new Vector2(0.5f, 0.5f);
        playerIcon.pivot = new Vector2(0.5f, 0.5f);
        playerIcon.anchoredPosition = Vector2.zero;
        playerIcon.sizeDelta = new Vector2(22f, 28f);

        Image image = EnsureImage(playerIcon.gameObject);
        image.sprite = GetTriangleSprite();
        image.color = playerColor;
        image.raycastTarget = false;
    }

    private void EnsureObjectivePulse(RectTransform parent)
    {
        pulseRing = EnsureRect(parent, "MiniMap_ObjectivePulse");
        pulseRing.anchorMin = new Vector2(0.5f, 0.5f);
        pulseRing.anchorMax = new Vector2(0.5f, 0.5f);
        pulseRing.pivot = new Vector2(0.5f, 0.5f);
        pulseRing.sizeDelta = Vector2.one * 42f;

        Image image = EnsureImage(pulseRing.gameObject);
        image.sprite = GetRingSprite();
        image.color = new Color(letterColor.r, letterColor.g, letterColor.b, 0.65f);
        image.raycastTarget = false;
    }

    private void EnsureDirectionWedge(RectTransform parent)
    {
        RectTransform wedge = EnsureRect(parent, "MiniMap_DirectionWedge");
        wedge.SetAsFirstSibling();
        wedge.anchorMin = new Vector2(0.5f, 0.5f);
        wedge.anchorMax = new Vector2(0.5f, 0.5f);
        wedge.pivot = new Vector2(0.5f, 0.12f);
        wedge.anchoredPosition = Vector2.zero;
        wedge.sizeDelta = new Vector2(78f, 84f);

        directionWedge = EnsureImage(wedge.gameObject);
        directionWedge.sprite = GetTriangleSprite();
        directionWedge.color = new Color(0.65f, 1f, 0.92f, 0.11f);
        directionWedge.raycastTarget = false;
    }

    private void EnsureNorthLabel(RectTransform parent)
    {
        RectTransform label = EnsureRect(parent, "MiniMap_NorthLabel");
        label.anchorMin = new Vector2(0.5f, 1f);
        label.anchorMax = new Vector2(0.5f, 1f);
        label.pivot = new Vector2(0.5f, 1f);
        label.anchoredPosition = new Vector2(0f, -5f);
        label.sizeDelta = new Vector2(28f, 18f);

        Text text = label.GetComponent<Text>();
        if (text == null)
        {
            text = label.gameObject.AddComponent<Text>();
        }

        text.text = "N";
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(0.85f, 1f, 0.95f, 0.95f);
        text.raycastTarget = false;
    }

    private void EnsureDistanceText(RectTransform parent)
    {
        RectTransform label = EnsureRect(parent, "MiniMap_ObjectiveDistance");
        label.anchorMin = new Vector2(0.5f, 0f);
        label.anchorMax = new Vector2(0.5f, 0f);
        label.pivot = new Vector2(0.5f, 0f);
        label.anchoredPosition = new Vector2(0f, 8f);
        label.sizeDelta = new Vector2(86f, 18f);

        distanceText = label.GetComponent<Text>();
        if (distanceText == null)
        {
            distanceText = label.gameObject.AddComponent<Text>();
        }

        distanceText.alignment = TextAnchor.MiddleCenter;
        distanceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        distanceText.fontSize = 11;
        distanceText.fontStyle = FontStyle.Bold;
        distanceText.color = new Color(1f, 0.93f, 0.35f, 0.95f);
        distanceText.raycastTarget = false;
    }

    private void ResolveWorldMarkers()
    {
        candidates.Clear();
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform target = transforms[i];
            if (target == null || target == player || target.IsChildOf(transform) || IsUiTransform(target))
            {
                continue;
            }

            MarkerKind kind = Classify(target);
            if (kind == MarkerKind.None)
            {
                continue;
            }

            if (kind == MarkerKind.Cutscene || kind == MarkerKind.Enemy || HasVisibleWorldPresence(target))
            {
                candidates.Add(target);
            }
        }

        candidates.Sort(CompareCandidates);
        RebuildMarkerPool();
    }

    private void RebuildMarkerPool()
    {
        int markerCount = Mathf.Min(candidates.Count, MaxWorldMarkers);
        while (markers.Count < markerCount)
        {
            RectTransform markerRect = EnsureRect(rotateRoot, "MiniMap_WorldMarker_" + markers.Count);
            Image image = EnsureImage(markerRect.gameObject);
            image.raycastTarget = false;
            markers.Add(new MapMarker(markerRect, image));
        }

        for (int i = 0; i < markers.Count; i++)
        {
            if (i < markerCount)
            {
                markers[i].Bind(candidates[i], Classify(candidates[i]));
            }
            else
            {
                markers[i].SetActive(false);
            }
        }
    }

    private int CompareCandidates(Transform left, Transform right)
    {
        if (player == null)
        {
            return 0;
        }

        int rightPriority = GetMarkerPriority(Classify(right));
        int leftPriority = GetMarkerPriority(Classify(left));
        if (leftPriority != rightPriority)
        {
            return rightPriority.CompareTo(leftPriority);
        }

        float leftDistance = (left.position - player.position).sqrMagnitude;
        float rightDistance = (right.position - player.position).sqrMagnitude;
        return leftDistance.CompareTo(rightDistance);
    }

    private void UpdateMapRotation()
    {
        if (rotateRoot != null)
        {
            rotateRoot.localEulerAngles = rotateMapWithPlayer ? new Vector3(0f, 0f, player.eulerAngles.y) : Vector3.zero;
        }

        if (playerIcon != null)
        {
            playerIcon.localEulerAngles = rotateMapWithPlayer ? Vector3.zero : new Vector3(0f, 0f, -player.eulerAngles.y);
        }
    }

    private void UpdateMarkers()
    {
        for (int i = 0; i < markers.Count; i++)
        {
            MapMarker marker = markers[i];
            if (!marker.HasTarget)
            {
                marker.SetActive(false);
                continue;
            }

            Vector3 offset = marker.Target.position - player.position;
            Vector2 flat = new Vector2(offset.x, offset.z);
            float normalizedDistance = flat.magnitude / Mathf.Max(1f, mapWorldRadius);
            if (normalizedDistance > 1.08f)
            {
                marker.SetActive(false);
                continue;
            }

            MarkerKind kind = marker.Kind;
            float pixelRadius = mapSize * 0.43f;
            Vector2 position = Vector2.ClampMagnitude(flat / mapWorldRadius * pixelRadius, pixelRadius);
            marker.Rect.anchoredPosition = new Vector2(position.x, position.y);
            marker.Rect.localEulerAngles = rotateMapWithPlayer ? new Vector3(0f, 0f, -player.eulerAngles.y) : Vector3.zero;
            marker.Rect.sizeDelta = GetMarkerSize(kind, normalizedDistance);
            marker.Image.sprite = GetMarkerSprite(kind);
            marker.Image.color = GetMarkerColor(kind, marker.Target);
            marker.SetActive(marker.Target.gameObject.activeInHierarchy);
        }
    }

    private void UpdateObjectiveDistance()
    {
        Transform objectiveTarget = FindPreferredObjectiveTransform();
        MapMarker objective = FindMarkerForTransform(objectiveTarget);
        if (objectiveTarget == null && objective != null)
        {
            objectiveTarget = objective.Target;
        }

        if (objectiveTarget == null || player == null)
        {
            if (distanceText != null)
            {
                distanceText.text = "";
            }

            if (pulseRing != null)
            {
                pulseRing.gameObject.SetActive(false);
            }

            return;
        }

        Vector3 offset = objectiveTarget.position - player.position;
        float distance = offset.magnitude;
        if (distanceText != null)
        {
            distanceText.text = Mathf.RoundToInt(distance) + " m";
        }

        if (pulseRing != null)
        {
            Vector2 flat = new Vector2(offset.x, offset.z);
            float pixelRadius = mapSize * 0.43f;
            pulseRing.gameObject.SetActive(true);
            pulseRing.anchoredPosition = Vector2.ClampMagnitude(flat / mapWorldRadius * pixelRadius, pixelRadius);
            pulseRing.localEulerAngles = Vector3.zero;
            pulseRing.sizeDelta = objective != null && objective.Kind == MarkerKind.Cutscene ? Vector2.one * 46f : Vector2.one * 42f;
        }
    }

    private void UpdatePulse()
    {
        if (pulseRing == null || !pulseRing.gameObject.activeSelf)
        {
            return;
        }

        float pulse = 0.5f + Mathf.PingPong(Time.unscaledTime * 1.8f, 0.5f);
        pulseRing.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.28f, pulse);
        Image image = pulseRing.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(letterColor.r, letterColor.g, letterColor.b, Mathf.Lerp(0.25f, 0.8f, 1f - pulse));
        }
    }

    private MapMarker GetNearestObjective()
    {
        MapMarker best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < markers.Count; i++)
        {
            if (!markers[i].HasTarget || (markers[i].Kind != MarkerKind.Letter && markers[i].Kind != MarkerKind.Cutscene))
            {
                continue;
            }

            float distance = player != null ? (markers[i].Target.position - player.position).sqrMagnitude : 0f;
            if (best == null ||
                markers[i].Kind == MarkerKind.Letter && best.Kind != MarkerKind.Letter ||
                IsPreferredSceneObjective(markers[i].Target) && !IsPreferredSceneObjective(best.Target) ||
                distance < bestDistance)
            {
                bestDistance = distance;
                best = markers[i];
            }
        }

        return best;
    }

    private Transform FindPreferredObjectiveTransform()
    {
        Transform namedTarget = FindObjectiveByName("Level2 Trigger");
        if (namedTarget != null)
        {
            return namedTarget;
        }

        namedTarget = FindObjectiveByName("Cutscene Trigger");
        if (namedTarget != null)
        {
            return namedTarget;
        }

        MapMarker marker = GetNearestObjective();
        return marker != null ? marker.Target : null;
    }

    private MapMarker FindMarkerForTransform(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i].Target == target)
            {
                return markers[i];
            }
        }

        return null;
    }

    private static Transform FindObjectiveByName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current != null && current.name == targetName)
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsPreferredSceneObjective(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        string name = target.name.ToLowerInvariant();
        return name.Contains("level2 trigger") || name.Contains("cutscene trigger") || name.Contains("cutscene") || name.Contains("mission trigger");
    }

    private void SetVisible(bool visible)
    {
        if (miniMapImage != null && miniMapImage.gameObject.activeSelf != visible)
        {
            miniMapImage.gameObject.SetActive(visible);
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private static RectTransform EnsureRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        RectTransform rect = existing as RectTransform;
        if (rect == null)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        rect.localScale = Vector3.one;
        return rect;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private static Image EnsureImage(GameObject item)
    {
        Image image = item.GetComponent<Image>();
        if (image == null)
        {
            image = item.AddComponent<Image>();
        }

        return image;
    }

    private static bool IsUiTransform(Transform target)
    {
        return target.GetComponentInParent<Canvas>() != null;
    }

    private static bool HasVisibleWorldPresence(Transform target)
    {
        if (target.GetComponentInChildren<Renderer>(true) != null)
        {
            return true;
        }

        Collider collider = target.GetComponentInChildren<Collider>(true);
        return collider != null && !collider.isTrigger;
    }

    private static bool IsPlayerTarget(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.CompareTag("Player"))
        {
            return true;
        }

        return target.GetComponentInParent<vThirdPersonController>() != null || target.GetComponentInChildren<vThirdPersonController>(true) != null;
    }

    private static bool IsCutsceneTrigger(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.GetComponent<CutsceneTrigger>() != null || target.GetComponentInParent<CutsceneTrigger>() != null || target.GetComponentInChildren<CutsceneTrigger>(true) != null)
        {
            return true;
        }

        string name = target.name.ToLowerInvariant();
        return name.Contains("cutscene") || name.Contains("cut scene") || name.Contains("trigger") || name.Contains("level2 trigger");
    }

    private static bool IsEnemyTarget(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.CompareTag("Enemy"))
        {
            return true;
        }

        if (target.GetComponentInParent<CompleteEnemyAI>() != null || target.GetComponentInChildren<CompleteEnemyAI>(true) != null)
        {
            return true;
        }

        if (target.GetComponentInParent<EnemyPatrol>() != null || target.GetComponentInChildren<EnemyPatrol>(true) != null)
        {
            return true;
        }

        if (target.GetComponentInParent<EnemyGunAttack>() != null || target.GetComponentInChildren<EnemyGunAttack>(true) != null)
        {
            return true;
        }

        if (target.GetComponentInParent<SimpleAI>() != null || target.GetComponentInChildren<SimpleAI>(true) != null)
        {
            return true;
        }

        string name = target.name.ToLowerInvariant();
        return name.Contains("enemy") || name.Contains("guard") || name.Contains("soldier") || name.Contains("npc enemy") || name.Contains("patrol");
    }

    private static MarkerKind Classify(Transform target)
    {
        if (target == null)
        {
            return MarkerKind.None;
        }

        if (IsPlayerTarget(target))
        {
            return MarkerKind.None;
        }

        if (IsCutsceneTrigger(target))
        {
            return MarkerKind.Cutscene;
        }

        if (IsEnemyTarget(target))
        {
            return MarkerKind.Enemy;
        }

        string rawName = target.name;
        if (string.IsNullOrEmpty(rawName))
        {
            return MarkerKind.None;
        }

        string name = rawName.ToLowerInvariant();
        if (name.Contains("letter") || name.Contains("paper"))
        {
            return MarkerKind.Letter;
        }

        if (name.Contains("tent") || name.Contains("camp"))
        {
            return MarkerKind.Camp;
        }

        if (name.Contains("tower") || name.Contains("hangar") || name.Contains("garage") || name.Contains("building") ||
            name.Contains("barrack") || name.Contains("container") || name.Contains("base"))
        {
            return MarkerKind.Structure;
        }

        return MarkerKind.None;
    }

    private static Transform FindPreferredPlayerModel(Transform playerRoot)
    {
        Transform namedModel = FindChildByName(playerRoot, PlayerModelName);
        if (namedModel != null)
        {
            return namedModel;
        }

        Transform renderableChild = FindRenderableChild(playerRoot);
        if (renderableChild != null)
        {
            return renderableChild;
        }

        return playerRoot;
    }

    private static Transform FindRenderableChild(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == root)
            {
                continue;
            }

            if (child.GetComponentInChildren<Renderer>(true) != null)
            {
                return child;
            }
        }

        return null;
    }

    private static int GetMarkerPriority(MarkerKind kind)
    {
        switch (kind)
        {
            case MarkerKind.Enemy:
                return 4;
            case MarkerKind.Cutscene:
                return 3;
            case MarkerKind.Letter:
                return 2;
            case MarkerKind.Camp:
                return 1;
            case MarkerKind.Structure:
                return 0;
            default:
                return -1;
        }
    }

    private Color GetMarkerColor(MarkerKind kind, Transform target)
    {
        switch (kind)
        {
            case MarkerKind.Enemy:
                return GetEnemyColor(target);
            case MarkerKind.Cutscene:
                return new Color(1f, 0.78f, 0.18f, 0.98f);
            case MarkerKind.Letter:
                float glow = 0.75f + Mathf.PingPong(Time.unscaledTime * 1.6f, 0.25f);
                return new Color(letterColor.r * glow, letterColor.g * glow, letterColor.b, 1f);
            case MarkerKind.Camp:
                return campColor;
            case MarkerKind.Structure:
                return structureColor;
            default:
                return Color.white;
        }
    }

    private Color GetEnemyColor(Transform target)
    {
        bool moving = IsEnemyMoving(target);
        float pulse = 0.5f + Mathf.PingPong(Time.unscaledTime * (moving ? 4.2f : 1.3f), 0.5f);
        float intensity = moving ? Mathf.Lerp(0.9f, 1.35f, pulse) : Mathf.Lerp(0.7f, 1.05f, pulse);
        float alpha = moving ? Mathf.Lerp(0.72f, 1f, pulse) : Mathf.Lerp(0.48f, 0.82f, pulse);
        return new Color(1f * intensity, 0.12f * intensity, 0.12f * intensity, alpha);
    }

    private static bool IsEnemyMoving(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        Rigidbody body = target.GetComponentInParent<Rigidbody>();
        if (body != null && body.linearVelocity.sqrMagnitude > 0.05f)
        {
            return true;
        }

        NavMeshAgent agent = target.GetComponentInParent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.05f)
        {
            return true;
        }

        EnemyPatrol patrol = target.GetComponentInParent<EnemyPatrol>();
        if (patrol != null)
        {
            return patrol.speed > 0.01f;
        }

        CompleteEnemyAI completeEnemy = target.GetComponentInParent<CompleteEnemyAI>();
        if (completeEnemy != null)
        {
            return completeEnemy.currentState == CompleteEnemyAI.EnemyState.Patrol ||
                completeEnemy.currentState == CompleteEnemyAI.EnemyState.Chase ||
                completeEnemy.currentState == CompleteEnemyAI.EnemyState.AttackBurst;
        }

        Animator animator = target.GetComponentInParent<Animator>();
        if (animator != null)
        {
            return animator.velocity.sqrMagnitude > 0.01f;
        }

        return false;
    }

    private static Vector2 GetMarkerSize(MarkerKind kind, float normalizedDistance)
    {
        float distanceScale = Mathf.Lerp(1f, 0.76f, Mathf.Clamp01(normalizedDistance));
        switch (kind)
        {
            case MarkerKind.Enemy:
                return Vector2.one * (15f * distanceScale);
            case MarkerKind.Cutscene:
                return new Vector2(14f, 14f) * distanceScale;
            case MarkerKind.Letter:
                return Vector2.one * (17f * distanceScale);
            case MarkerKind.Camp:
                return new Vector2(13f, 13f) * distanceScale;
            case MarkerKind.Structure:
                return new Vector2(11f, 11f) * distanceScale;
            default:
                return Vector2.one * 10f;
        }
    }

    private static Sprite GetMarkerSprite(MarkerKind kind)
    {
        switch (kind)
        {
            case MarkerKind.Enemy:
                return GetTargetSprite();
            case MarkerKind.Cutscene:
                return GetArrowRightSprite();
            case MarkerKind.Letter:
                return GetDiamondSprite();
            case MarkerKind.Camp:
                return GetTriangleSprite();
            case MarkerKind.Structure:
                return GetSquareSprite();
            default:
                return GetCircleSprite();
        }
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite == null)
        {
            circleSprite = CreateSprite(96, (x, y, center, radius) =>
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - distance);
                return new Color(1f, 1f, 1f, alpha);
            });
        }

        return circleSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite == null)
        {
            ringSprite = CreateSprite(96, (x, y, center, radius) =>
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(2.2f - Mathf.Abs(distance - radius + 4f));
                return new Color(1f, 1f, 1f, alpha);
            });
        }

        return ringSprite;
    }

    private static Sprite GetTriangleSprite()
    {
        if (triangleSprite == null)
        {
            triangleSprite = CreateSprite(64, (x, y, center, radius) =>
            {
                Vector2 point = new Vector2(x, y);
                Vector2 top = new Vector2(center.x, center.y + radius * 0.78f);
                Vector2 left = new Vector2(center.x - radius * 0.55f, center.y - radius * 0.58f);
                Vector2 right = new Vector2(center.x + radius * 0.55f, center.y - radius * 0.58f);
                return IsPointInTriangle(point, top, left, right) ? Color.white : Color.clear;
            });
        }

        return triangleSprite;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite == null)
        {
            squareSprite = CreateSprite(32, (x, y, center, radius) =>
            {
                float half = radius * 0.56f;
                return Mathf.Abs(x - center.x) <= half && Mathf.Abs(y - center.y) <= half ? Color.white : Color.clear;
            });
        }

        return squareSprite;
    }

    private static Sprite GetDiamondSprite()
    {
        if (diamondSprite == null)
        {
            diamondSprite = CreateSprite(48, (x, y, center, radius) =>
            {
                float distance = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y);
                return distance <= radius * 0.78f ? Color.white : Color.clear;
            });
        }

        return diamondSprite;
    }

    private static Sprite GetTargetSprite()
    {
        if (targetSprite == null)
        {
            targetSprite = CreateSprite(96, (x, y, center, radius) =>
            {
                Vector2 point = new Vector2(x, y);
                float distance = Vector2.Distance(point, center);
                float outer = Mathf.Clamp01(radius - distance);
                float inner = Mathf.Clamp01(10f - distance);
                float cross = Mathf.Abs(point.x - center.x) <= 1.5f || Mathf.Abs(point.y - center.y) <= 1.5f ? 1f : 0f;
                float alpha = Mathf.Clamp01(outer + inner * 0.75f + cross * 0.32f);
                return new Color(1f, 1f, 1f, alpha);
            });
        }

        return targetSprite;
    }

    private static Sprite GetArrowRightSprite()
    {
        if (arrowRightSprite == null)
        {
            arrowRightSprite = CreateSprite(64, (x, y, center, radius) =>
            {
                Vector2 point = new Vector2(x, y);
                Vector2 tip = new Vector2(center.x + radius * 0.78f, center.y);
                Vector2 top = new Vector2(center.x - radius * 0.42f, center.y + radius * 0.5f);
                Vector2 bottom = new Vector2(center.x - radius * 0.42f, center.y - radius * 0.5f);
                return IsPointInTriangle(point, tip, top, bottom) ? Color.white : Color.clear;
            });
        }

        return arrowRightSprite;
    }

    private delegate Color PixelEvaluator(int x, int y, Vector2 center, float radius);

    private static Sprite CreateSprite(int size, PixelEvaluator evaluator)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f - 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, evaluator(x, y, center, radius));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float area = 0.5f * (-b.y * c.x + a.y * (-b.x + c.x) + a.x * (b.y - c.y) + b.x * c.y);
        float sign = area < 0f ? -1f : 1f;
        float s = (a.y * c.x - a.x * c.y + (c.y - a.y) * point.x + (a.x - c.x) * point.y) * sign;
        float t = (a.x * b.y - a.y * b.x + (a.y - b.y) * point.x + (b.x - a.x) * point.y) * sign;
        return s >= 0f && t >= 0f && (s + t) <= 2f * area * sign;
    }

    private enum MarkerKind
    {
        None,
        Enemy,
        Cutscene,
        Structure,
        Camp,
        Letter
    }

    private sealed class MapMarker
    {
        public readonly RectTransform Rect;
        public readonly Image Image;
        public Transform Target { get; private set; }
        public MarkerKind Kind { get; private set; }
        public bool HasTarget
        {
            get { return Target != null; }
        }

        public MapMarker(RectTransform rect, Image image)
        {
            Rect = rect;
            Image = image;
        }

        public void Bind(Transform target, MarkerKind kind)
        {
            Target = target;
            Kind = kind;
            SetActive(target != null);
        }

        public void SetActive(bool active)
        {
            if (Rect != null && Rect.gameObject.activeSelf != active)
            {
                Rect.gameObject.SetActive(active);
            }
        }
    }
}
