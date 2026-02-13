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
    private static readonly Dictionary<string, PendingMaterialRequest> PendingMaterialRequests = new(StringComparer.OrdinalIgnoreCase);
    private static bool pendingMaterialProcessingScheduled;
    private static bool isProcessingPendingMaterials;

    private sealed class PendingMaterialRequest
    {
        public string modelPath;
        public string materialPath;
        public string materialName;
        public string rendererName;
    }

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
            var persistentMat = GetOrCreatePersistentMaterial(matPath, assetPath, incomingMat, renderer);
            if (persistentMat == null)
                return incomingMat;

            bool changed = ApplyUrpLitAndForceOpaque(persistentMat, isHair);
            if (changed)
                EditorUtility.SetDirty(persistentMat);
            return persistentMat;
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

    private static Material GetOrCreatePersistentMaterial(string matPath, string modelPath, Material incomingMat, Renderer renderer)
    {
        var persistentMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (persistentMat != null)
            return persistentMat;

        QueuePendingMaterialCreation(matPath, modelPath, incomingMat?.name, renderer?.name);
        return null;
    }

    private static void QueuePendingMaterialCreation(string materialPath, string modelPath, string materialName, string rendererName)
    {
        if (string.IsNullOrWhiteSpace(materialPath) ||
            string.IsNullOrWhiteSpace(modelPath) ||
            string.IsNullOrWhiteSpace(materialName))
        {
            return;
        }

        PendingMaterialRequests[materialPath] = new PendingMaterialRequest
        {
            materialPath = materialPath,
            modelPath = modelPath,
            materialName = materialName,
            rendererName = rendererName
        };

        SchedulePendingMaterialProcessing();
    }

    private static void SchedulePendingMaterialProcessing()
    {
        if (pendingMaterialProcessingScheduled)
            return;

        pendingMaterialProcessingScheduled = true;
        EditorApplication.delayCall += ProcessPendingMaterialCreation;
    }

    private static void ProcessPendingMaterialCreation()
    {
        pendingMaterialProcessingScheduled = false;
        if (isProcessingPendingMaterials)
            return;

        if (PendingMaterialRequests.Count == 0)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            SchedulePendingMaterialProcessing();
            return;
        }

        isProcessingPendingMaterials = true;
        try
        {
            var requests = new List<PendingMaterialRequest>(PendingMaterialRequests.Values);
            PendingMaterialRequests.Clear();

            var reimportModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool createdAny = false;

            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.materialPath) ||
                    string.IsNullOrWhiteSpace(request.modelPath) ||
                    string.IsNullOrWhiteSpace(request.materialName))
                {
                    continue;
                }

                var existing = AssetDatabase.LoadAssetAtPath<Material>(request.materialPath);
                if (existing != null)
                    continue;

                string folder = Path.GetDirectoryName(request.materialPath)?.Replace("\\", "/");
                if (!string.IsNullOrWhiteSpace(folder))
                    EnsureFolder(folder);

                var sourceMaterial = ResolveSourceMaterial(request.modelPath, request.materialName);
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader == null)
                    continue;

                var newMaterial = sourceMaterial != null
                    ? new Material(sourceMaterial)
                    : new Material(shader);
                newMaterial.name = request.materialName;

                AssetDatabase.CreateAsset(newMaterial, request.materialPath);
                bool isHair = FfxivMaterialPolicy.IsHairAsset(request.modelPath, request.materialName, request.rendererName);
                ApplyUrpLitAndForceOpaque(newMaterial, isHair);
                EditorUtility.SetDirty(newMaterial);

                createdAny = true;
                reimportModels.Add(request.modelPath);
            }

            if (!createdAny)
                return;

            AssetDatabase.SaveAssets();
            foreach (string modelPath in reimportModels)
            {
                if (!string.IsNullOrWhiteSpace(modelPath))
                    AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            }
        }
        finally
        {
            isProcessingPendingMaterials = false;
            if (PendingMaterialRequests.Count > 0)
                SchedulePendingMaterialProcessing();
        }
    }

    private static Material ResolveSourceMaterial(string modelPath, string materialName)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || string.IsNullOrWhiteSpace(materialName))
            return null;

        var subAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        for (int i = 0; i < subAssets.Length; i++)
        {
            if (!(subAssets[i] is Material material))
                continue;

            if (string.Equals(material.name, materialName, StringComparison.OrdinalIgnoreCase))
                return material;
        }

        return null;
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
