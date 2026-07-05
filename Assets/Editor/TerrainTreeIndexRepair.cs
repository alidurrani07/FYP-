using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TerrainTreeIndexRepair
{
    private const string SessionKey = "TerrainTreeIndexRepair.AutoRan";
    private const string LevelOneTerrainPath = "Assets/AngeloMaN87/Natural Environment (Mobile)/Models/Terrain.asset";

    static TerrainTreeIndexRepair()
    {
        EditorApplication.delayCall += AutoRepairOnce;
    }

    [MenuItem("Tools/Fix Invalid Terrain Tree Indices")]
    public static void RepairInvalidTreeIndices()
    {
        var changed = 0;
        var visited = new HashSet<TerrainData>();
        TerrainData levelOneTerrain = AssetDatabase.LoadAssetAtPath<TerrainData>(LevelOneTerrainPath);

        if (RepairTerrainData(levelOneTerrain, visited))
        {
            changed++;
        }

        foreach (Terrain terrain in Resources.FindObjectsOfTypeAll<Terrain>())
        {
            if (terrain != null && RepairTerrainData(terrain.terrainData, visited))
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Terrain tree index repair finished. TerrainData assets changed: {changed}");
    }

    private static void AutoRepairOnce()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);
        RepairInvalidTreeIndices();
    }

    private static bool RepairTerrainData(TerrainData terrainData, HashSet<TerrainData> visited)
    {
        if (terrainData == null || !visited.Add(terrainData))
        {
            return false;
        }

        TreeInstance[] treeInstances = terrainData.treeInstances;
        if (treeInstances == null || treeInstances.Length == 0)
        {
            return false;
        }

        TreePrototype[] treePrototypes = terrainData.treePrototypes;
        int prototypeCount = treePrototypes == null ? 0 : treePrototypes.Length;
        var validTrees = new List<TreeInstance>(treeInstances.Length);

        for (int i = 0; i < treeInstances.Length; i++)
        {
            TreeInstance tree = treeInstances[i];
            if (IsValidTree(tree, treePrototypes, prototypeCount))
            {
                validTrees.Add(tree);
            }
        }

        if (validTrees.Count == treeInstances.Length)
        {
            return false;
        }

        terrainData.treeInstances = validTrees.ToArray();
        EditorUtility.SetDirty(terrainData);
        Debug.LogWarning($"Removed {treeInstances.Length - validTrees.Count} invalid terrain tree instances from {terrainData.name}.");
        return true;
    }

    private static bool IsValidTree(TreeInstance tree, TreePrototype[] treePrototypes, int prototypeCount)
    {
        if (tree.prototypeIndex < 0 || tree.prototypeIndex >= prototypeCount)
        {
            return false;
        }

        TreePrototype prototype = treePrototypes[tree.prototypeIndex];
        return prototype != null && prototype.prefab != null;
    }
}
