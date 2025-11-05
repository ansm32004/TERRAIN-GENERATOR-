#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using YOLOGRAM;

// Editor utility to validate and fix chunk prefab assets.
// Run from the menu: Tools -> Validate & Fix Chunk Prefab
public static class PrefabValidation
{
    [MenuItem("Tools/Validate & Fix Chunk Prefab")]
    public static void ValidateAndFixChunkPrefab()
    {
        int fixedCount = 0;
        int checkedCount = 0;

        var managers = Object.FindObjectsOfType<TerrainManager>();
        if (managers == null || managers.Length == 0)
        {
            Debug.LogWarning("PrefabValidation: No TerrainManager instances found in the open scenes.");
            return;
        }

        foreach (var mgr in managers)
        {
            if (mgr == null) continue;
            var prefab = mgr.chunkPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"PrefabValidation: TerrainManager on '{mgr.gameObject.name}' has no chunkPrefab assigned.");
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"PrefabValidation: chunkPrefab for TerrainManager '{mgr.gameObject.name}' is not a prefab asset (path empty). Make sure you assign a prefab asset from the Project window.");
                continue;
            }

            checkedCount++;

            // Load the prefab contents so we can edit the asset safely.
            GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null)
            {
                Debug.LogError($"PrefabValidation: Failed to load prefab at '{assetPath}'.");
                continue;
            }

            // Check for Chunk component
            var chunkComp = root.GetComponent<Chunk>();
            if (chunkComp == null)
            {
                Debug.Log($"PrefabValidation: Adding Chunk component to prefab asset at '{assetPath}'.");
                root.AddComponent<Chunk>();
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                fixedCount++;
            }
            else
            {
                Debug.Log($"PrefabValidation: Prefab at '{assetPath}' already contains a Chunk component.");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"PrefabValidation: Checked {checkedCount} prefab(s), fixed {fixedCount} prefab(s).");
    }
}
#endif