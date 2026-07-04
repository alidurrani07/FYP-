using UnityEngine;

public static class FinalSceneRuntimeEffects
{
    private static GameObject cachedExTemplate;
    private static Material cachedFlashMaterial;
    private static Material cachedSmokeMaterial;

    public static GameObject FindTemplate(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject resource = Resources.Load<GameObject>(objectName);
        if (resource != null)
        {
            return resource;
        }

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate;
            }
        }

        return objectName == "ex" ? GetOrCreateExTemplate() : null;
    }

    public static GameObject GetOrCreateExTemplate()
    {
        if (cachedExTemplate != null)
        {
            return cachedExTemplate;
        }

        cachedExTemplate = new GameObject("ex");
        cachedExTemplate.SetActive(false);
        Object.DontDestroyOnLoad(cachedExTemplate);

        ParticleSystem flash = cachedExTemplate.AddComponent<ParticleSystem>();
        ConfigureFlash(flash);
        ParticleSystemRenderer flashRenderer = cachedExTemplate.GetComponent<ParticleSystemRenderer>();
        flashRenderer.sharedMaterial = GetFlashMaterial();
        flashRenderer.maxParticleSize = 0.45f;

        GameObject smokeObject = new GameObject("ex_smoke");
        smokeObject.transform.SetParent(cachedExTemplate.transform, false);
        ParticleSystem smoke = smokeObject.AddComponent<ParticleSystem>();
        ConfigureSmoke(smoke);
        ParticleSystemRenderer smokeRenderer = smokeObject.GetComponent<ParticleSystemRenderer>();
        smokeRenderer.sharedMaterial = GetSmokeMaterial();
        smokeRenderer.maxParticleSize = 0.8f;

        return cachedExTemplate;
    }

    public static GameObject SpawnEx(Vector3 position, Quaternion rotation, float scale = 1f, float lifetime = 2f, Transform parent = null)
    {
        GameObject template = GetOrCreateExTemplate();
        GameObject instance = Object.Instantiate(template, position, rotation, parent);
        instance.name = "ex";
        instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        instance.SetActive(true);

        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }

        Object.Destroy(instance, lifetime);
        return instance;
    }

    private static void ConfigureFlash(ParticleSystem particle)
    {
        ParticleSystem.MainModule main = particle.main;
        main.duration = 0.22f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.12f, 0.95f), new Color(1f, 0.18f, 0.05f, 0.55f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 16) });

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.025f;

        ParticleSystem.ColorOverLifetimeModule color = particle.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.75f, 0.2f), 0f), new GradientColorKey(new Color(0.9f, 0.12f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
    }

    private static void ConfigureSmoke(ParticleSystem particle)
    {
        ParticleSystem.MainModule main = particle.main;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.2f, 0.2f, 0.46f), new Color(0.55f, 0.52f, 0.46f, 0.24f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.03f, 8, 14) });

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.04f;

        ParticleSystem.SizeOverLifetimeModule size = particle.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 1.8f)));

        ParticleSystem.ColorOverLifetimeModule color = particle.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.45f, 0.43f, 0.38f), 0f), new GradientColorKey(new Color(0.12f, 0.12f, 0.12f), 1f) },
            new[] { new GradientAlphaKey(0.42f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
    }

    private static Material GetFlashMaterial()
    {
        if (cachedFlashMaterial != null)
        {
            return cachedFlashMaterial;
        }

        cachedFlashMaterial = CreateParticleMaterial("FinalScene_ex_flash", new Color(1f, 0.42f, 0.08f, 0.8f), true);
        return cachedFlashMaterial;
    }

    private static Material GetSmokeMaterial()
    {
        if (cachedSmokeMaterial != null)
        {
            return cachedSmokeMaterial;
        }

        cachedSmokeMaterial = CreateParticleMaterial("FinalScene_ex_smoke", new Color(0.36f, 0.34f, 0.3f, 0.34f), false);
        return cachedSmokeMaterial;
    }

    private static Material CreateParticleMaterial(string materialName, Color color, bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Legacy Shaders/Particles/Additive");
        Material material = new Material(shader);
        material.name = materialName;
        SetColor(material, "_BaseColor", color);
        SetColor(material, "_Color", color);
        SetColor(material, "_TintColor", color);
        SetFloat(material, "_Surface", 1f);
        SetFloat(material, "_Blend", additive ? 2f : 0f);
        SetFloat(material, "_ZWrite", 0f);
        SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloat(material, "_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private static void SetColor(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }
}
