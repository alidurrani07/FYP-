using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PinkMaterialRuntimeFix
{
    private static readonly Dictionary<Material, Material> FixedMaterials = new Dictionary<Material, Material>();
    private static bool isInstalled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (isInstalled)
        {
            return;
        }

        isInstalled = true;
        SceneManager.sceneLoaded += (_, __) => RepairLoadedSceneMaterials();
        RepairLoadedSceneMaterials();
    }

    public static void RepairLoadedSceneMaterials()
    {
        var renderers = Object.FindObjectsOfType<Renderer>(true);
        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            var changed = false;

            for (var i = 0; i < materials.Length; i++)
            {
                if (!NeedsRepair(materials[i]))
                {
                    continue;
                }

                materials[i] = GetOrCreateReplacement(materials[i]);
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }

        var terrains = Object.FindObjectsOfType<Terrain>(true);
        foreach (var terrain in terrains)
        {
            if (terrain.materialTemplate && NeedsRepair(terrain.materialTemplate))
            {
                terrain.materialTemplate = GetOrCreateReplacement(terrain.materialTemplate);
            }
        }
    }

    public static bool NeedsRepair(Material material)
    {
        if (!material || !material.shader)
        {
            return false;
        }

        var shaderName = material.shader.name;
        return shaderName == "Hidden/InternalErrorShader"
            || shaderName == "Standard"
            || shaderName.StartsWith("Legacy Shaders/")
            || shaderName.StartsWith("HDRP/")
            || shaderName.Contains("HDRP")
            || shaderName.Contains("High Definition");
    }

    private static Material GetOrCreateReplacement(Material source)
    {
        if (!source)
        {
            return null;
        }

        if (FixedMaterials.TryGetValue(source, out var fixedMaterial) && fixedMaterial)
        {
            return fixedMaterial;
        }

        var shader = ChooseReplacementShader(source);
        if (!shader)
        {
            FixedMaterials[source] = source;
            return source;
        }

        fixedMaterial = new Material(shader)
        {
            name = source.name + "_URP_Fixed",
            renderQueue = source.renderQueue
        };

        CopyCommonProperties(source, fixedMaterial);
        ConfigureTransparencyIfNeeded(source, fixedMaterial);
        FixedMaterials[source] = fixedMaterial;
        return fixedMaterial;
    }

    private static Shader ChooseReplacementShader(Material source)
    {
        var name = source.name.ToLowerInvariant();
        var shaderName = source.shader ? source.shader.name.ToLowerInvariant() : string.Empty;

        if (shaderName.Contains("textmeshpro") || source.HasProperty("_FaceColor"))
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

    private static void CopyCommonProperties(Material source, Material destination)
    {
        var texture = GetTexture(source, "_BaseMap") ?? GetTexture(source, "_MainTex");
        SetTexture(destination, "_BaseMap", texture);
        SetTexture(destination, "_MainTex", texture);

        var color = GetColor(source, "_BaseColor", Color.white);
        color = GetColor(source, "_Color", color);
        color = GetColor(source, "_TintColor", color);
        color = GetColor(source, "_FaceColor", color);
        SetColor(destination, "_BaseColor", color);
        SetColor(destination, "_Color", color);
        SetColor(destination, "_TintColor", color);
        SetColor(destination, "_FaceColor", color);

        SetTexture(destination, "_BumpMap", GetTexture(source, "_BumpMap"));
        SetTexture(destination, "_NormalMap", GetTexture(source, "_BumpMap") ?? GetTexture(source, "_NormalMap"));
        SetTexture(destination, "_EmissionMap", GetTexture(source, "_EmissionMap"));
        SetColor(destination, "_EmissionColor", GetColor(source, "_EmissionColor", Color.black));
    }

    private static void ConfigureTransparencyIfNeeded(Material source, Material destination)
    {
        var transparent = source.renderQueue >= 2500;
        transparent |= GetColor(source, "_BaseColor", Color.white).a < 0.99f;
        transparent |= GetColor(source, "_Color", Color.white).a < 0.99f;

        if (!transparent)
        {
            return;
        }

        if (destination.HasProperty("_Surface"))
        {
            destination.SetFloat("_Surface", 1f);
        }

        if (destination.HasProperty("_Blend"))
        {
            destination.SetFloat("_Blend", 0f);
        }

        destination.SetOverrideTag("RenderType", "Transparent");
        destination.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        destination.renderQueue = Mathf.Max(source.renderQueue, 3000);
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
