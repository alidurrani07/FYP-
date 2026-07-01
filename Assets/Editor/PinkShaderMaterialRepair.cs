using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PinkShaderMaterialRepair
{
    private const string SessionKey = "PinkShaderMaterialRepair.AutoRan";

    static PinkShaderMaterialRepair()
    {
        EditorApplication.delayCall += AutoRepairOnce;
    }

    [MenuItem("Tools/Fix Pink Materials (URP)")]
    public static void RepairAllMaterials()
    {
        var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        var changed = 0;

        foreach (var guid in materialGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material || !PinkMaterialRuntimeFix.NeedsRepair(material))
            {
                continue;
            }

            if (RepairMaterial(material))
            {
                EditorUtility.SetDirty(material);
                changed++;
            }
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Pink shader repair finished. Materials changed: {changed}");
    }

    private static void AutoRepairOnce()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);
        RepairAllMaterials();
    }

    private static bool RepairMaterial(Material material)
    {
        var replacement = ChooseReplacementShader(material);
        if (!replacement)
        {
            return false;
        }

        var mainTexture = GetTexture(material, "_BaseMap") ?? GetTexture(material, "_MainTex");
        var color = GetColor(material, "_BaseColor", Color.white);
        color = GetColor(material, "_Color", color);
        color = GetColor(material, "_TintColor", color);
        color = GetColor(material, "_FaceColor", color);
        var queue = material.renderQueue;
        var transparent = queue >= 2500 || color.a < 0.99f;

        material.shader = replacement;
        SetTexture(material, "_BaseMap", mainTexture);
        SetTexture(material, "_MainTex", mainTexture);
        SetColor(material, "_BaseColor", color);
        SetColor(material, "_Color", color);
        SetColor(material, "_TintColor", color);
        SetColor(material, "_FaceColor", color);

        if (transparent)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = Mathf.Max(queue, 3000);
        }

        return true;
    }

    private static Shader ChooseReplacementShader(Material material)
    {
        var name = material.name.ToLowerInvariant();
        var shaderName = material.shader ? material.shader.name.ToLowerInvariant() : string.Empty;

        if (shaderName.Contains("textmeshpro") || material.HasProperty("_FaceColor"))
        {
            return Shader.Find("TextMeshPro/Distance Field")
                ?? Shader.Find("TextMeshPro/Mobile/Distance Field")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (name.Contains("terrain"))
        {
            return Shader.Find("Universal Render Pipeline/Terrain/Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit");
        }

        if (LooksLikeEffect(name) || shaderName.Contains("particle"))
        {
            return Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (name.Contains("ui") || name.Contains("preview") || name.Contains("logo"))
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit");
        }

        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Universal Render Pipeline/Unlit");
    }

    private static bool LooksLikeEffect(string name)
    {
        return name.Contains("particle")
            || name.Contains("smoke")
            || name.Contains("fire")
            || name.Contains("spark")
            || name.Contains("spray")
            || name.Contains("trail")
            || name.Contains("cloud")
            || name.Contains("rain")
            || name.Contains("snow")
            || name.Contains("storm")
            || name.Contains("lightning")
            || name.Contains("explosion")
            || name.Contains("dust")
            || name.Contains("blood")
            || name.Contains("glass")
            || name.Contains("wood")
            || name.Contains("concrete");
    }

    private static Texture GetTexture(Material material, string property)
    {
        return material.HasProperty(property) ? material.GetTexture(property) : null;
    }

    private static void SetTexture(Material material, string property, Texture texture)
    {
        if (texture && material.HasProperty(property))
        {
            material.SetTexture(property, texture);
        }
    }

    private static Color GetColor(Material material, string property, Color fallback)
    {
        return material.HasProperty(property) ? material.GetColor(property) : fallback;
    }

    private static void SetColor(Material material, string property, Color color)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, color);
        }
    }
}
