using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerrainTreeRuntimeSanitizer
{
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
                SanitizeTerrainData(terrain.terrainData, visited);
            }
        }
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
