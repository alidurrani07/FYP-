using UnityEngine;

public class AutoCharacterFactionMaterial : MonoBehaviour
{
    public enum CharacterStyle
    {
        SWAT,
        Rebel
    }

    [Header("Character Style")]
    [SerializeField] private CharacterStyle style = CharacterStyle.SWAT;

    [Header("Debug")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool showDebugLogs = true;

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyFactionLook();
        }
    }

    [ContextMenu("Apply Faction Look")]
    public void ApplyFactionLook()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            Debug.LogWarning(gameObject.name + " has no Renderer or SkinnedMeshRenderer found.");
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            Material[] newMaterials = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material oldMat = renderer.sharedMaterials[i];

                if (oldMat == null)
                {
                    continue;
                }

                string matName = oldMat.name.ToLower();
                Material newMat = CreateFactionMaterial(oldMat, matName);

                newMaterials[i] = newMat;

                if (showDebugLogs)
                {
                    Debug.Log(gameObject.name + " changed material: " + oldMat.name + " -> " + newMat.name);
                }
            }

            renderer.materials = newMaterials;
        }
    }

    private Material CreateFactionMaterial(Material oldMaterial, string materialName)
    {
        Shader shader = GetBestShader();
        Material mat = new Material(shader);

        mat.name = style.ToString() + "_" + oldMaterial.name;

        Color baseColor = GetColorByMaterialName(materialName);

        SetMaterialColor(mat, baseColor);
        SetMaterialSmoothness(mat, GetSmoothness(materialName));
        SetMaterialMetallic(mat, GetMetallic(materialName));

        Texture oldTexture = GetMainTexture(oldMaterial);

        if (oldTexture != null)
        {
            SetMaterialTexture(mat, oldTexture);
        }

        return mat;
    }

    private Color GetColorByMaterialName(string name)
    {
        if (style == CharacterStyle.SWAT)
        {
            if (ContainsAny(name, "skin", "face", "head", "hand", "arm"))
                return new Color(0.72f, 0.52f, 0.38f);

            if (ContainsAny(name, "helmet", "cap", "hat"))
                return new Color(0.03f, 0.035f, 0.04f);

            if (ContainsAny(name, "vest", "armor", "bodyarmor", "plate"))
                return new Color(0.015f, 0.02f, 0.025f);

            if (ContainsAny(name, "shirt", "torso", "body", "jacket", "uniform"))
                return new Color(0.02f, 0.025f, 0.035f);

            if (ContainsAny(name, "pant", "leg", "trouser"))
                return new Color(0.025f, 0.03f, 0.04f);

            if (ContainsAny(name, "boot", "shoe", "feet"))
                return new Color(0.01f, 0.01f, 0.012f);

            if (ContainsAny(name, "mask", "balaclava"))
                return new Color(0.005f, 0.005f, 0.006f);

            if (ContainsAny(name, "glove"))
                return new Color(0.01f, 0.01f, 0.012f);

            if (ContainsAny(name, "weapon", "gun", "rifle"))
                return new Color(0.025f, 0.025f, 0.025f);

            return new Color(0.025f, 0.03f, 0.035f);
        }
        else
        {
            if (ContainsAny(name, "skin", "face", "head", "hand", "arm"))
                return new Color(0.68f, 0.45f, 0.30f);

            if (ContainsAny(name, "helmet", "cap", "hat", "scarf"))
                return new Color(0.35f, 0.23f, 0.12f);

            if (ContainsAny(name, "vest", "armor", "bodyarmor", "plate"))
                return new Color(0.18f, 0.13f, 0.08f);

            if (ContainsAny(name, "shirt", "torso", "body", "jacket", "uniform"))
                return new Color(0.42f, 0.30f, 0.15f);

            if (ContainsAny(name, "pant", "leg", "trouser"))
                return new Color(0.20f, 0.16f, 0.10f);

            if (ContainsAny(name, "boot", "shoe", "feet"))
                return new Color(0.08f, 0.055f, 0.035f);

            if (ContainsAny(name, "mask", "balaclava"))
                return new Color(0.18f, 0.13f, 0.08f);

            if (ContainsAny(name, "glove"))
                return new Color(0.10f, 0.07f, 0.04f);

            if (ContainsAny(name, "weapon", "gun", "rifle"))
                return new Color(0.08f, 0.075f, 0.065f);

            return new Color(0.32f, 0.23f, 0.12f);
        }
    }

    private Shader GetBestShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader != null)
            return shader;

        shader = Shader.Find("HDRP/Lit");

        if (shader != null)
            return shader;

        shader = Shader.Find("Standard");

        if (shader != null)
            return shader;

        return Shader.Find("Diffuse");
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }

    private void SetMaterialTexture(Material mat, Texture texture)
    {
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", texture);
        }
        else if (mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", texture);
        }
    }

    private Texture GetMainTexture(Material mat)
    {
        if (mat.HasProperty("_BaseMap"))
        {
            return mat.GetTexture("_BaseMap");
        }

        if (mat.HasProperty("_MainTex"))
        {
            return mat.GetTexture("_MainTex");
        }

        return null;
    }

    private void SetMaterialSmoothness(Material mat, float value)
    {
        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", value);
        }
        else if (mat.HasProperty("_Glossiness"))
        {
            mat.SetFloat("_Glossiness", value);
        }
    }

    private void SetMaterialMetallic(Material mat, float value)
    {
        if (mat.HasProperty("_Metallic"))
        {
            mat.SetFloat("_Metallic", value);
        }
    }

    private float GetSmoothness(string name)
    {
        if (ContainsAny(name, "helmet", "armor", "vest", "weapon", "gun", "rifle"))
            return 0.35f;

        if (ContainsAny(name, "skin", "face", "hand"))
            return 0.15f;

        if (ContainsAny(name, "cloth", "shirt", "pant", "jacket", "uniform"))
            return 0.12f;

        return 0.18f;
    }

    private float GetMetallic(string name)
    {
        if (ContainsAny(name, "weapon", "gun", "rifle", "metal", "knife"))
            return 0.55f;

        if (ContainsAny(name, "helmet", "armor", "plate"))
            return 0.15f;

        return 0f;
    }

    private bool ContainsAny(string text, params string[] words)
    {
        for (int i = 0; i < words.Length; i++)
        {
            if (text.Contains(words[i]))
            {
                return true;
            }
        }

        return false;
    }
}