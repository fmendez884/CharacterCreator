using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FfxivValidationRunner
{
    private const string SourceRootKey = "FFXIV.Processed.SourceRoot";
    private const string MenuPath = "Tools/FFXIV/Validate/Run Full Validation (Processed + Addressables + Catalog)";
    private const int MaxLoggedFbx = 10;

    [MenuItem(MenuPath)]
    public static void RunFullValidation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("FFXIV Validation", "Exit Play Mode before running validation.", "OK");
            return;
        }

        string sourceRoot = EditorPrefs.GetString(SourceRootKey, string.Empty);
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            EditorUtility.DisplayDialog("FFXIV Validation", "Set a valid processed source root before running validation.", "OK");
            return;
        }

        try
        {
            FfxivProcessedPrefabBuilder.BuildUsingLastSource();
            FfxivAddressablesConfigurator.SetupGroups(showDialog: false);
            FfxivAddressableIndexBuilder.BuildIndex(showDialog: false);
            FfxivPrefabCatalogBuilder.BuildCatalog();

            int fbxCount = ScanForFbx(out List<string> samplePaths);
            if (fbxCount == 0)
            {
                Debug.Log("[FFXIV] Validation PASS: No .fbx files found under Assets.");
                EditorUtility.DisplayDialog("FFXIV Validation", "Validation PASS: No .fbx files found under Assets.", "OK");
            }
            else
            {
                Debug.LogWarning($"[FFXIV] Validation FAIL: Found {fbxCount} .fbx files under Assets.");
                foreach (var path in samplePaths)
                    Debug.LogWarning($"[FFXIV] FBX: {path}");
                EditorUtility.DisplayDialog("FFXIV Validation", $"Validation FAIL: Found {fbxCount} .fbx files under Assets. See Console for details.", "OK");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static int ScanForFbx(out List<string> samplePaths)
    {
        samplePaths = new List<string>();
        string assetsPath = Application.dataPath;
        int count = 0;

        foreach (var path in Directory.EnumerateFiles(assetsPath, "*.*", SearchOption.AllDirectories))
        {
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                continue;

            count++;
            if (samplePaths.Count < MaxLoggedFbx)
                samplePaths.Add(ToAssetRelativePath(path));
        }

        return count;
    }

    private static string ToAssetRelativePath(string absolutePath)
    {
        string assetsPath = Application.dataPath;
        if (!absolutePath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
            return absolutePath.Replace('\\', '/');

        string relative = "Assets" + absolutePath.Substring(assetsPath.Length);
        return relative.Replace('\\', '/');
    }
}
