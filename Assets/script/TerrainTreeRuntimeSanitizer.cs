using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerrainTreeRuntimeSanitizer
{
    private const float MaxTreeDistance = 280f;
    private const float MaxDetailDistance = 65f;
    private const float MaxBasemapDistance = 650f;
    private const float BillboardStart = 45f;
    private const float TreeFadeLength = 16f;
    private const int MaxFullLodTrees = 30;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SanitizeLoadedTerrains();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SanitizeLoadedTerrains();
    }

    private static void SanitizeLoadedTerrains()
    {
        Terrain[] terrains = Object.FindObjectsOfType<Terrain>(true);
        var visited = new HashSet<TerrainData>();

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain != null)
            {
                ApplyPerformanceCaps(terrain);
                SanitizeTerrainData(terrain.terrainData, visited);
            }
        }
    }

    private static void ApplyPerformanceCaps(Terrain terrain)
    {
        terrain.treeDistance = Mathf.Min(terrain.treeDistance, MaxTreeDistance);
        terrain.treeBillboardDistance = Mathf.Min(terrain.treeBillboardDistance, BillboardStart);
        terrain.treeCrossFadeLength = Mathf.Max(terrain.treeCrossFadeLength, TreeFadeLength);
        terrain.treeMaximumFullLODCount = Mathf.Min(terrain.treeMaximumFullLODCount, MaxFullLodTrees);
        terrain.detailObjectDistance = Mathf.Min(terrain.detailObjectDistance, MaxDetailDistance);
        terrain.basemapDistance = Mathf.Min(terrain.basemapDistance, MaxBasemapDistance);
    }

    private static void SanitizeTerrainData(TerrainData terrainData, HashSet<TerrainData> visited)
    {
        if (terrainData == null || !visited.Add(terrainData))
        {
            return;
        }

        TreeInstance[] treeInstances = terrainData.treeInstances;
        if (treeInstances == null || treeInstances.Length == 0)
        {
            return;
        }

        TreePrototype[] treePrototypes = terrainData.treePrototypes;
        int prototypeCount = treePrototypes == null ? 0 : treePrototypes.Length;
        var validTrees = new List<TreeInstance>(treeInstances.Length);

        for (int i = 0; i < treeInstances.Length; i++)
        {
            TreeInstance tree = treeInstances[i];
            if (tree.prototypeIndex >= 0
                && tree.prototypeIndex < prototypeCount
                && treePrototypes[tree.prototypeIndex] != null
                && treePrototypes[tree.prototypeIndex].prefab != null)
            {
                validTrees.Add(tree);
            }
        }

        if (validTrees.Count != treeInstances.Length)
        {
            terrainData.treeInstances = validTrees.ToArray();
        }
    }
}
