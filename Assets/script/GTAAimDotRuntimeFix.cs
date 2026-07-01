using System.Collections;
using Invector.vShooter;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-250)]
public class GTAAimDotRuntimeFix : MonoBehaviour
{
    private const string DotName = "GTAStyleAimDot";
    private static GTAAimDotRuntimeFix instance;
    private static Sprite dotSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeFix()
    {
        if (instance != null)
        {
            return;
        }

        GameObject fixerObject = new GameObject("GTAAimDotRuntimeFix");
        DontDestroyOnLoad(fixerObject);
        instance = fixerObject.AddComponent<GTAAimDotRuntimeFix>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(ConfigureRepeatedly());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ConfigureRepeatedly());
    }

    private IEnumerator ConfigureRepeatedly()
    {
        yield return null;
        ConfigureAllAimCanvases();
        yield return new WaitForSeconds(0.5f);
        ConfigureAllAimCanvases();
        yield return new WaitForSeconds(2f);
        ConfigureAllAimCanvases();
    }

    private void ConfigureAllAimCanvases()
    {
        vControlAimCanvas[] controls = FindObjectsOfType<vControlAimCanvas>(true);
        for (int i = 0; i < controls.Length; i++)
        {
            ConfigureControl(controls[i]);
        }

        vAimCanvas[] canvases = FindObjectsOfType<vAimCanvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            ConfigureAimCanvas(canvases[i]);
        }
    }

    private void ConfigureControl(vControlAimCanvas control)
    {
        if (control == null || control.aimCanvasCollection == null)
        {
            return;
        }

        for (int i = 0; i < control.aimCanvasCollection.Count; i++)
        {
            ConfigureAimCanvas(control.aimCanvasCollection[i]);
        }
    }

    private void ConfigureAimCanvas(vAimCanvas aimCanvas)
    {
        if (aimCanvas == null || aimCanvas.aimCenter == null)
        {
            return;
        }

        aimCanvas.scaleAimWithMovement = false;
        aimCanvas.aimCenterToAimTarget = true;

        if (aimCanvas.aimTarget != null)
        {
            aimCanvas.aimTarget.sizeDelta = Vector2.one * 5f;
            aimCanvas.sizeDeltaTarget = aimCanvas.aimTarget.sizeDelta;
            DisableCrossbarImages(aimCanvas.aimTarget);
        }

        aimCanvas.aimCenter.sizeDelta = Vector2.one * 5f;
        aimCanvas.sizeDeltaCenter = aimCanvas.aimCenter.sizeDelta;
        DisableCrossbarImages(aimCanvas.aimCenter);
        EnsureDot(aimCanvas.aimCenter);
    }

    private void DisableCrossbarImages(RectTransform root)
    {
        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.gameObject.name == DotName)
            {
                continue;
            }

            image.enabled = false;
        }
    }

    private void EnsureDot(RectTransform parent)
    {
        Transform existing = parent.Find(DotName);
        RectTransform dotTransform = existing as RectTransform;
        if (dotTransform == null)
        {
            GameObject dot = new GameObject(DotName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dotTransform = dot.GetComponent<RectTransform>();
            dotTransform.SetParent(parent, false);
        }

        dotTransform.anchorMin = new Vector2(0.5f, 0.5f);
        dotTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dotTransform.pivot = new Vector2(0.5f, 0.5f);
        dotTransform.anchoredPosition = Vector2.zero;
        dotTransform.sizeDelta = Vector2.one * 5f;
        dotTransform.localScale = Vector3.one;

        Image dotImage = dotTransform.GetComponent<Image>();
        dotImage.enabled = true;
        dotImage.raycastTarget = false;
        dotImage.color = Color.white;
        dotImage.sprite = GetDotSprite();
        dotImage.type = Image.Type.Simple;
        dotImage.preserveAspect = true;
    }

    private Sprite GetDotSprite()
    {
        if (dotSprite != null)
        {
            return dotSprite;
        }

        dotSprite = Resources.Load<Sprite>("small_circle");
        if (dotSprite != null)
        {
            return dotSprite;
        }

        Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Vector2 centerOffset = new Vector2(x - 3.5f, y - 3.5f);
                texture.SetPixel(x, y, centerOffset.magnitude <= 3.5f ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        dotSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return dotSprite;
    }
}
