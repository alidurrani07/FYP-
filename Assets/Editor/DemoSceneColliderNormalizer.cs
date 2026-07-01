using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DemoSceneColliderNormalizer
{
    private const string DemoScenePath = "Assets/Scenes/Demo Scene 1.unity";
    private const string BombTriggerPrefix = "bombTrigger";

    [MenuItem("Tools/Scenes/Normalize Demo Scene 1 Box Colliders")]
    public static void NormalizeDemoScene1BoxColliders()
    {
        Scene scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);

        int removedBoxColliders = 0;
        int removedMeshColliders = 0;
        int addedBoxColliders = 0;
        int skippedObjects = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject gameObject = transform.gameObject;

                if (ShouldPreserveTrigger(gameObject))
                    continue;

                removedBoxColliders += RemoveNonTriggerColliders<BoxCollider>(gameObject);
                removedMeshColliders += RemoveNonTriggerColliders<MeshCollider>(gameObject);
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject gameObject = transform.gameObject;

                if (ShouldSkipObject(gameObject))
                {
                    skippedObjects++;
                    continue;
                }

                MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshRenderer == null || meshFilter == null || meshFilter.sharedMesh == null)
                {
                    skippedObjects++;
                    continue;
                }

                BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                FitBoxColliderToRenderer(boxCollider, meshRenderer);
                addedBoxColliders++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            "Demo Scene 1 collider normalization complete. " +
            "Removed BoxColliders: " + removedBoxColliders +
            ", Removed MeshColliders: " + removedMeshColliders +
            ", Added BoxColliders: " + addedBoxColliders +
            ", Skipped non-physical/UI/helper objects: " + skippedObjects);
    }

    private static bool ShouldPreserveTrigger(GameObject gameObject)
    {
        return gameObject.name.StartsWith(BombTriggerPrefix) && HasTriggerCollider(gameObject);
    }

    private static bool ShouldSkipObject(GameObject gameObject)
    {
        if (!gameObject.activeInHierarchy)
            return true;

        if (ShouldPreserveTrigger(gameObject))
            return true;

        if (gameObject.layer == 5)
            return true;

        if (gameObject.GetComponent<Canvas>() != null ||
            gameObject.GetComponent<Camera>() != null ||
            gameObject.GetComponent<Light>() != null ||
            gameObject.GetComponent<ParticleSystem>() != null ||
            gameObject.GetComponent<TrailRenderer>() != null ||
            gameObject.GetComponent<LineRenderer>() != null ||
            gameObject.GetComponent<SkinnedMeshRenderer>() != null ||
            gameObject.GetComponent<Terrain>() != null)
        {
            return true;
        }

        string lowerName = gameObject.name.ToLowerInvariant();
        return lowerName.Contains("marker") ||
               lowerName.Contains("trigger") ||
               lowerName.Contains("canvas") ||
               lowerName.Contains("camera") ||
               lowerName.Contains("light");
    }

    private static int RemoveNonTriggerColliders<TCollider>(GameObject gameObject) where TCollider : Collider
    {
        int removed = 0;
        TCollider[] colliders = gameObject.GetComponents<TCollider>();
        for (int index = colliders.Length - 1; index >= 0; index--)
        {
            if (colliders[index] == null || colliders[index].isTrigger)
                continue;

            Object.DestroyImmediate(colliders[index]);
            removed++;
        }

        return removed;
    }

    private static bool HasTriggerCollider(GameObject gameObject)
    {
        Collider[] colliders = gameObject.GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider != null && collider.isTrigger)
                return true;
        }

        return false;
    }

    private static void FitBoxColliderToRenderer(BoxCollider boxCollider, Renderer renderer)
    {
        Bounds worldBounds = renderer.bounds;
        Transform transform = boxCollider.transform;

        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localMin = transform.InverseTransformPoint(worldBounds.min);
        Vector3 localMax = transform.InverseTransformPoint(worldBounds.max);

        Vector3 localSize = new Vector3(
            Mathf.Abs(localMax.x - localMin.x),
            Mathf.Abs(localMax.y - localMin.y),
            Mathf.Abs(localMax.z - localMin.z));

        boxCollider.center = localCenter;
        boxCollider.size = new Vector3(
            Mathf.Max(localSize.x, 0.05f),
            Mathf.Max(localSize.y, 0.05f),
            Mathf.Max(localSize.z, 0.05f));
        boxCollider.isTrigger = false;
    }
}
