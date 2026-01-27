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

            if (ForceAlphaHashedLikeUrp(mat))
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
    private static bool ForceAlphaHashedLikeUrp(Material mat)
    {
        if (!mat.HasProperty("_Surface"))
            return false; // not URP Lit/Simple Lit style

        bool changed = false;

        changed |= SetFloat(mat, "_Surface", 0f); // 0 = Opaque
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

        if (mat.HasProperty("_AlphaClip"))
            changed |= SetFloat(mat, "_AlphaClip", 1f);

        if (mat.HasProperty("_Cutoff"))
            changed |= SetFloat(mat, "_Cutoff", 0.5f);

        mat.EnableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        return changed;
    }

    private static bool SetFloat(Material mat, string prop, float value)
    {
        if (!mat.HasProperty(prop)) return false;
        if (Mathf.Approximately(mat.GetFloat(prop), value)) return false;
        mat.SetFloat(prop, value);
        return true;
    }
}
