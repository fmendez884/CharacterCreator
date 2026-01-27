using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class FfxivImportProfile : AssetPostprocessor
{
    private const string TargetRoot = "Assets/FFXIV_Imported/";

    private static bool IsTarget(string path) =>
        path.StartsWith(TargetRoot, StringComparison.OrdinalIgnoreCase);

    // --------------------
    // MODEL IMPORT DEFAULTS (your #3 settings)
    // --------------------
    void OnPreprocessModel()
    {
        if (!IsTarget(assetPath)) return;

        var importer = (ModelImporter)assetImporter;

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation   = ModelImporterMaterialLocation.External;
        importer.materialName       = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch     = ModelImporterMaterialSearch.RecursiveUp;

        importer.importNormals  = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
    }

    // IMPORTANT: return a PERSISTENT material asset (fixes your error + makes model render correctly)
    Material OnAssignMaterialModel(Material incomingMat, Renderer renderer)
    {
        if (!IsTarget(assetPath) || incomingMat == null)
            return incomingMat;

        string fbxDir = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(fbxDir))
            return incomingMat;

        string materialsDir = $"{fbxDir}/Materials";
        EnsureFolder(materialsDir);

        string safeMatName = SanitizeName(incomingMat.name);
        string matPath = $"{materialsDir}/{safeMatName}.mat";

        // Load or create persistent material asset
        var persistentMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (persistentMat == null)
        {
            persistentMat = new Material(incomingMat) { name = incomingMat.name };
            AssetDatabase.CreateAsset(persistentMat, matPath);
        }
        else
        {
            // Update it to match incoming (keeps texture links Unity discovered)
            EditorUtility.CopySerialized(incomingMat, persistentMat);
        }

        ApplyUrpLitAndForceOpaque(persistentMat);
        EditorUtility.SetDirty(persistentMat);

        return persistentMat;
    }

    // --------------------
    // TEXTURE IMPORT DEFAULTS (fix normal map spam)
    // --------------------
    void OnPreprocessTexture()
    {
        if (!IsTarget(assetPath)) return;

        var importer = (TextureImporter)assetImporter;
        string file = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

        bool looksNormal =
            file.EndsWith("_n") ||
            file.Contains("_normal") ||
            file.Contains("norm");

        if (looksNormal)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
        }
    }

    // --------------------
    // Material policy (your gripe: Surface Type Opaque)
    // --------------------
    private static void ApplyUrpLitAndForceOpaque(Material mat)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit != null && mat.shader != urpLit)
            mat.shader = urpLit;

        // --- Surface ---
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 0f); // Opaque

        // --- Alpha Clipping ---
        if (mat.HasProperty("_AlphaClip"))
        {
            mat.SetFloat("_AlphaClip", 1f);   // enable alpha clip
            mat.EnableKeyword("_ALPHATEST_ON");
        }

        if (mat.HasProperty("_Cutoff"))
        {
            mat.SetFloat("_Cutoff", 0.5f);    // default cutoff, tweak later
        }

        // --- Render state ---
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = (int)RenderQueue.Geometry;

        // --- Disable transparency (keep clip only) ---
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }


    // --------------------
    // Helpers
    // --------------------
    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        // folderPath like "Assets/FFXIV_Imported/X/Materials"
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

    private static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
