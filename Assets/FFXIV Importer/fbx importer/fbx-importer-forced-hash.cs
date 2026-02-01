using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FfxivImportMenu
{
    // Where the imported FBX will be copied inside the project
    private const string TargetFolder = "Assets/ImportedFbxAlphaHashed";

    [MenuItem("Tools/Import FBX (Default Unity)...")]
    private static void ImportDefaultUnity()
    {
        // This just triggers Unity's built-in "Import New Asset" window
        EditorApplication.ExecuteMenuItem("Assets/Import New Asset...");
    }

    [MenuItem("Tools/Import FBX (Alpha Hashed)...")]
    private static void ImportFbxAlphaHashed()
    {
        var srcPath = EditorUtility.OpenFilePanel("Import FBX (Alpha Hashed)", "", "fbx");
        if (string.IsNullOrEmpty(srcPath))
            return;

        EnsureFolder(TargetFolder);

        var fileName = Path.GetFileName(srcPath);
        var dstPath = Path.Combine(TargetFolder, fileName).Replace("\\", "/");

        // Copy into project (overwrite if exists)
        File.Copy(srcPath, dstPath, overwrite: true);

        // Import it (normal Unity import happens here)
        AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceSynchronousImport);

        // After import, force materials to your desired mode
        int touched = ForceAlphaHashedLikeOnMaterialsForAsset(dstPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FFXIV] Imported '{fileName}' and adjusted {touched} material(s).");
    }

    private static void EnsureFolder(string folderPath)
    {
        // Create nested folders as needed (Assets/..../....)
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parts = folderPath.Split('/');
        string current = parts[0]; // "Assets"

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static int ForceAlphaHashedLikeOnMaterialsForAsset(string assetPath)
    {
        // Grab dependency materials Unity created/uses for this FBX
        var deps = AssetDatabase.GetDependencies(assetPath, recursive: true);
        int count = 0;

        foreach (var dep in deps)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(dep);
            if (mat == null) continue;

            bool isHair = FfxivMaterialPolicy.IsHairAsset(dep, mat.name);
            if (ForceAlphaHashedLikeUrp(mat, isHair))
            {
                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        return count;
    }

    // URP "Alpha Hashed-like" approximation:
    // - Opaque surface (depth write)
    // - Alpha clip ON (stable, avoids sorting/see-through weirdness)
    private static bool ForceAlphaHashedLikeUrp(Material mat, bool isHair)
    {
        bool changed = FfxivMaterialPolicy.ApplyOpaqueAlphaClip(mat);

        if (isHair)
        {
            changed |= FfxivMaterialPolicy.ApplyHairDoubleSided(mat, true);
        }
        else
        {
            changed |= FfxivMaterialPolicy.ApplyFrontFaceOnly(mat);
        }

        return changed;
    }
}
