using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class EnemyVisualChanger : MonoBehaviour
{
    [Header("Enemy Model")]
    public GameObject enemyModelPrefab;
    public bool useDefaultEnemyVisualWhenMissing = true;
    public Transform modelParent;
    public bool hideOriginalRenderersWhenModelIsAssigned = true;
    public Vector3 modelLocalPosition = Vector3.zero;
    public Vector3 modelLocalEulerAngles = Vector3.zero;
    public Vector3 modelLocalScale = Vector3.one;

    [Header("Optional Materials")]
    public Material[] modelMaterials;
    public bool applyMaterialsToCurrentModelIfNoPrefab = true;

    [Header("Rifle")]
    public GameObject riflePrefab;
    public float rifleVisualScale = 1.25f;
    public Vector3 rifleShoulderOffset = new Vector3(0.22f, -0.08f, 0.32f);

    [Header("Bullet Visuals")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 60f;
    public float bulletLifetime = 2f;

    [Header("Particles")]
    public ParticleSystem[] muzzleParticlesOnEnemy;
    public GameObject muzzleParticlePrefab;
    public GameObject hitParticlePrefab;
    public string embeddedGunName = "GunENEMY";
    public string embeddedBulletName = "BulletEnemy";
    public bool useEmbeddedGunAndBulletFromModel = true;

    [Header("Apply")]
    public bool applyOnAwake = true;

    private const string ManagedModelName = "__EnemyVisual_Model";
    private const string DefaultEnemyVisualPath = "Assets/New/EnemyVisual.prefab";
    private GameObject spawnedModel;

    private void Awake()
    {
        if (applyOnAwake)
        {
            ApplyVisuals();
        }
    }

    [ContextMenu("Apply Enemy Visuals")]
    public void ApplyVisuals()
    {
        ResolveDefaultEnemyVisual();
        Transform activeModelRoot = SetupEnemyModel();
        ApplyModelMaterials(activeModelRoot);
        SetupGunAttack(activeModelRoot);
    }

    [ContextMenu("Clear Spawned Enemy Model")]
    public void ClearSpawnedEnemyModel()
    {
        Transform parent = modelParent != null ? modelParent : transform;
        Transform existingModel = parent.Find(ManagedModelName);
        if (existingModel != null)
        {
            DestroySafely(existingModel.gameObject);
        }

        spawnedModel = null;
    }

    private Transform SetupEnemyModel()
    {
        ClearSpawnedEnemyModel();

        if (enemyModelPrefab == null)
        {
            return applyMaterialsToCurrentModelIfNoPrefab ? transform : null;
        }

        Transform parent = modelParent != null ? modelParent : transform;
        spawnedModel = Instantiate(enemyModelPrefab, parent);
        spawnedModel.name = ManagedModelName;
        spawnedModel.transform.localPosition = modelLocalPosition;
        spawnedModel.transform.localRotation = Quaternion.Euler(modelLocalEulerAngles);
        spawnedModel.transform.localScale = modelLocalScale;
        PrepareSpawnedVisual(spawnedModel.transform);

        if (hideOriginalRenderersWhenModelIsAssigned)
        {
            HideOriginalRenderers(spawnedModel.transform);
        }

        return spawnedModel.transform;
    }

    private void ResolveDefaultEnemyVisual()
    {
        if (enemyModelPrefab != null || !useDefaultEnemyVisualWhenMissing)
        {
            return;
        }

#if UNITY_EDITOR
        enemyModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultEnemyVisualPath);
#endif
    }

    private void PrepareSpawnedVisual(Transform activeModelRoot)
    {
        if (activeModelRoot == null)
        {
            return;
        }

        Animator[] animators = activeModelRoot.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] == null)
            {
                continue;
            }

            animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animators[i].speed = 1f;
        }

        Renderer[] renderers = activeModelRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
            }
        }
    }

    private void ApplyModelMaterials(Transform activeModelRoot)
    {
        if (activeModelRoot == null || modelMaterials == null || modelMaterials.Length == 0)
        {
            return;
        }

        Renderer[] renderers = activeModelRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || targetRenderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (modelMaterials.Length == 1)
            {
                targetRenderer.sharedMaterial = modelMaterials[0];
                continue;
            }

            Material[] sharedMaterials = targetRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
            {
                sharedMaterials[materialIndex] = modelMaterials[Mathf.Min(materialIndex, modelMaterials.Length - 1)];
            }

            targetRenderer.sharedMaterials = sharedMaterials;
        }
    }

    private void HideOriginalRenderers(Transform activeModelRoot)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || targetRenderer.transform.IsChildOf(activeModelRoot) || targetRenderer is ParticleSystemRenderer)
            {
                continue;
            }

            targetRenderer.enabled = false;
        }
    }

    private void SetupGunAttack(Transform activeModelRoot)
    {
        EnemyGunAttack gunAttack = GetComponent<EnemyGunAttack>();
        if (gunAttack == null)
        {
            gunAttack = GetComponentInChildren<EnemyGunAttack>();
        }

        if (gunAttack == null)
        {
            return;
        }

        gunAttack.ApplyVisualSetup(riflePrefab, bulletPrefab, muzzleParticlesOnEnemy, muzzleParticlePrefab, hitParticlePrefab);
        gunAttack.UseVisualRoot(activeModelRoot);

        if (useEmbeddedGunAndBulletFromModel && activeModelRoot != null)
        {
            Transform embeddedGun = FindChildByName(activeModelRoot, embeddedGunName);
            Transform embeddedBullet = FindChildByName(activeModelRoot, embeddedBulletName);
            if (embeddedBullet != null)
            {
                embeddedBullet.gameObject.SetActive(false);
            }

            gunAttack.UseExistingGunVisual(embeddedGun, embeddedBullet != null ? embeddedBullet.gameObject : null);
        }

        gunAttack.gunVisualScale = rifleVisualScale;
        gunAttack.gunShoulderOffset = rifleShoulderOffset;
        gunAttack.bulletVisualSpeed = bulletSpeed;
        gunAttack.bulletVisualLifetime = bulletLifetime;
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return children[i];
            }
        }

        return null;
    }

    private void DestroySafely(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
