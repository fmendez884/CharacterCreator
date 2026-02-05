using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class FfxivImportProfile : AssetPostprocessor
{
    private const string TargetRoot = "Assets/FFXIV_Imported/";
    private const string ImportProcessingKey = "FFXIV.ImportProcessingEnabled";
    private const bool ImportProcessingDefault = false;

    private static readonly Dictionary<string, Material> PendingMaterials = new Dictionary<string, Material>();
    private static bool PendingFlushScheduled = false;

    private static bool IsImportProcessingEnabled() =>
        EditorPrefs.GetBool(ImportProcessingKey, ImportProcessingDefault);

    private static bool IsTarget(string path) =>
        path.StartsWith(TargetRoot, StringComparison.OrdinalIgnoreCase);

    // --------------------
    // MODEL IMPORT DEFAULTS (your #3 settings)
    // --------------------
    void OnPreprocessModel()
    {
        if (!IsImportProcessingEnabled()) return;
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
        if (!IsImportProcessingEnabled()) return incomingMat;
        if (!IsTarget(assetPath) || incomingMat == null)
            return incomingMat;
        try
        {
            string fbxDir = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(fbxDir))
                return incomingMat;

            string materialsDir = $"{fbxDir}/Materials";
            EnsureFolder(materialsDir);

            string matPath = BuildMaterialPath(materialsDir, assetPath, incomingMat, renderer);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                bool isHairExisting = FfxivMaterialPolicy.IsHairAsset(assetPath, existing?.name, renderer?.name);
                bool changedExisting = ApplyUrpLitAndForceOpaque(existing, isHairExisting);
                if (changedExisting)
                    EditorUtility.SetDirty(existing);
                return existing;
            }

            bool isHair = FfxivMaterialPolicy.IsHairAsset(assetPath, incomingMat?.name, renderer?.name);
            ApplyUrpLitAndForceOpaque(incomingMat, isHair);

            var pendingMat = new Material(incomingMat) { name = incomingMat.name };
            ApplyUrpLitAndForceOpaque(pendingMat, isHair);
            QueueMaterialCreate(matPath, pendingMat);

            return incomingMat;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FFXIV] OnAssignMaterialModel failed for {assetPath}: {ex}");
            return incomingMat;
        }
    }

    // --------------------
    // TEXTURE IMPORT DEFAULTS (fix normal map spam)
    // --------------------
    void OnPreprocessTexture()
    {
        if (!IsImportProcessingEnabled()) return;
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
    private static bool ApplyUrpLitAndForceOpaque(Material mat, bool isHair)
    {
        if (mat == null)
            return false;

        var previousShader = mat.shader;

        FfxivMaterialPolicy.EnsureUrpLitShader(mat);
        bool changed = previousShader != mat.shader;

        changed |= FfxivMaterialPolicy.ApplyOpaqueAlphaClip(mat);

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

    private static void QueueMaterialCreate(string matPath, Material material)
    {
        if (string.IsNullOrEmpty(matPath) || material == null)
            return;

        if (!PendingMaterials.ContainsKey(matPath))
            PendingMaterials.Add(matPath, material);

        if (PendingFlushScheduled)
            return;

        PendingFlushScheduled = true;
        EditorApplication.delayCall += FlushPendingMaterials;
    }

    private static void FlushPendingMaterials()
    {
        PendingFlushScheduled = false;

        if (PendingMaterials.Count == 0)
            return;

        foreach (var kvp in PendingMaterials)
        {
            string matPath = kvp.Key;
            Material material = kvp.Value;
            if (material == null)
                continue;

            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                continue;

            try
            {
                AssetDatabase.CreateAsset(material, matPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FFXIV] Deferred CreateAsset failed for '{matPath}': {ex.Message}");
            }
        }

        PendingMaterials.Clear();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
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

    private static string BuildMaterialPath(string materialsDir, string modelPath, Material incomingMat, Renderer renderer)
    {
        string safeName = SanitizeName(incomingMat.name);
        string seed = BuildMaterialSeed(modelPath, incomingMat, renderer);
        string hash = Hash128.Compute(seed).ToString();
        return $"{materialsDir}/{safeName}_{hash}.mat";
    }

    private static string BuildMaterialSeed(string modelPath, Material incomingMat, Renderer renderer)
    {
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(incomingMat, out string guid, out long localId) &&
            !string.IsNullOrEmpty(guid))
        {
            return $"{guid}:{localId}";
        }

        string rendererName = renderer != null ? renderer.name : string.Empty;
        return $"{modelPath}|{incomingMat.name}|{rendererName}";
    }

    private static Material GetOrCreatePersistentMaterial(string matPath, string modelPath, Material incomingMat, out bool created)
    {
        created = false;

        var persistentMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (persistentMat != null)
            return persistentMat;

        if (File.Exists(matPath))
        {
            return CreateFallbackMaterial(modelPath, incomingMat);
        }

        try
        {
            persistentMat = new Material(incomingMat) { name = incomingMat.name };
            AssetDatabase.CreateAsset(persistentMat, matPath);
            created = true;
            return persistentMat;
        }
        catch
        {
            return CreateFallbackMaterial(modelPath, incomingMat);
        }
    }

    private static Material CreateFallbackMaterial(string modelPath, Material incomingMat)
    {
        const string fallbackDir = "Assets/FFXIV_Imported/_Materials";
        EnsureFolder(fallbackDir);

        string safeName = SanitizeName(incomingMat.name);
        string seed = BuildMaterialSeed(modelPath, incomingMat, null);
        string hash = Hash128.Compute(seed).ToString();
        string fallbackPath = AssetDatabase.GenerateUniqueAssetPath($"{fallbackDir}/{safeName}_{hash}.mat");

        var fallbackMat = new Material(incomingMat) { name = incomingMat.name };
        AssetDatabase.CreateAsset(fallbackMat, fallbackPath);
        return fallbackMat;
    }

    [MenuItem("Tools/FFXIV/Import Processing/Enable")]
    private static void EnableImportProcessing()
    {
        EditorPrefs.SetBool(ImportProcessingKey, true);
    }

    [MenuItem("Tools/FFXIV/Import Processing/Disable")]
    private static void DisableImportProcessing()
    {
        EditorPrefs.SetBool(ImportProcessingKey, false);
    }

    [MenuItem("Tools/FFXIV/Import Processing/Toggle")]
    private static void ToggleImportProcessing()
    {
        bool enabled = IsImportProcessingEnabled();
        EditorPrefs.SetBool(ImportProcessingKey, !enabled);
    }
}
