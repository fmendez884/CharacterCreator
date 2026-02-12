using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class FfxivProcessedPrefabBuilder
{
    private const string SourceRootKey = "FFXIV.Processed.SourceRoot";
    private const string DefaultSourceRoot = "C:/Users/fmend/Desktop/TEST-EXTRACT-HUMAN-MODELS/chara";
    private const string StagingRoot = "Assets/FFXIV_Imported";
    private const string ProcessedRoot = "Assets/FFXIV_Processed";
    private const string ImportProcessingKey = "FFXIV.ImportProcessingEnabled";
    private const string AutoSetupAddressablesKey = "FFXIV.Processed.AutoSetupAddressables";
    private const string AutoBuildAddressableIndexKey = "FFXIV.Processed.AutoBuildAddressableIndex";
    private const string CleanupStagingKey = "FFXIV.Processed.CleanupStaging";

    private static readonly string[] TextureExtensions =
    {
        ".png", ".tga", ".dds", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"
    };

    private enum ProcessedCategory
    {
        Hair,
        Face,
        Equipment,
        Weapons
    }

    private const int LogEveryNAssets = 100;
    private static bool VerboseLogging = false;
    private static bool SkipUnchanged = true;
    private const string VerboseLoggingKey = "FFXIV.Processed.VerboseLogging";
    private const string SkipUnchangedKey = "FFXIV.Processed.SkipUnchanged";
    private const string MaxFbxKey = "FFXIV.Processed.MaxFbx";
    private const int DefaultMaxFbx = 0;
    private const bool DefaultAutoSetupAddressables = true;
    private const bool DefaultAutoBuildAddressableIndex = true;
    private const bool DefaultCleanupStaging = true;

    [MenuItem("Tools/FFXIV/Processed Prefabs/Build from External Source...")]
    public static void BuildFromSourcePrompt()
    {
        VerboseLogging = EditorPrefs.GetBool(VerboseLoggingKey, false);
        SkipUnchanged = EditorPrefs.GetBool(SkipUnchangedKey, true);
        string current = EditorPrefs.GetString(SourceRootKey, DefaultSourceRoot);
        string sourceRoot = EditorUtility.OpenFolderPanel("Select FFXIV Source Root", current, "");
        if (string.IsNullOrEmpty(sourceRoot))
            return;

        EditorPrefs.SetString(SourceRootKey, sourceRoot);
        BuildFromSourceRoot(sourceRoot);
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Build (Use Last Source)")]
    public static void BuildUsingLastSource()
    {
        BuildUsingLastSource(throwOnCancel: false);
    }

    public static void BuildUsingLastSource(bool throwOnCancel)
    {
        VerboseLogging = EditorPrefs.GetBool(VerboseLoggingKey, false);
        SkipUnchanged = EditorPrefs.GetBool(SkipUnchangedKey, true);
        string sourceRoot = EditorPrefs.GetString(SourceRootKey, DefaultSourceRoot);
        if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            EditorUtility.DisplayDialog("Source Root Missing", "Set a valid source root first.", "OK");
            return;
        }

        BuildFromSourceRoot(sourceRoot, throwOnCancel);
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Build (From Source Root)")]
    public static void BuildFromStoredSourceRoot()
    {
        VerboseLogging = EditorPrefs.GetBool(VerboseLoggingKey, false);
        SkipUnchanged = EditorPrefs.GetBool(SkipUnchangedKey, true);
        string sourceRoot = EditorPrefs.GetString(SourceRootKey, DefaultSourceRoot);
        if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            EditorUtility.DisplayDialog("Source Root Missing", "Set a valid source root first.", "OK");
            return;
        }

        BuildFromSourceRoot(sourceRoot);
    }

    public static void BuildFromSourceRoot(string sourceRoot, bool throwOnCancel = false)
    {
        VerboseLogging = EditorPrefs.GetBool(VerboseLoggingKey, false);
        SkipUnchanged = EditorPrefs.GetBool(SkipUnchangedKey, true);
        int maxFbxToProcess = GetMaxFbxLimit();
        bool autoSetupAddressables = EditorPrefs.GetBool(AutoSetupAddressablesKey, DefaultAutoSetupAddressables);
        bool autoBuildAddressableIndex = EditorPrefs.GetBool(AutoBuildAddressableIndexKey, DefaultAutoBuildAddressableIndex);
        if (autoSetupAddressables)
            autoBuildAddressableIndex = true;
        bool cleanupStaging = EditorPrefs.GetBool(CleanupStagingKey, DefaultCleanupStaging);
        sourceRoot = NormalizePath(sourceRoot);

        EnsureFolder(StagingRoot);
        EnsureFolder(ProcessedRoot);

        bool wasProcessingEnabled = EditorPrefs.GetBool(ImportProcessingKey, false);
        EditorPrefs.SetBool(ImportProcessingKey, true);

        int copiedAssets = 0;
        int copiedFbxs = 0;

        int totalSourceAssets = 0;
        int processedAssets = 0;
        List<string> limitedSourceFiles = null;
        HashSet<string> limitedFbxAssetPaths = null;

        try
        {
            if (maxFbxToProcess > 0)
            {
                limitedSourceFiles = BuildLimitedSourceFileList(sourceRoot, maxFbxToProcess, out limitedFbxAssetPaths);
                totalSourceAssets = limitedSourceFiles.Count;
                Debug.Log($"[FFXIV] Max FBXs limit: {maxFbxToProcess}. Selected FBXs: {limitedFbxAssetPaths.Count}");
            }
            else
            {
                totalSourceAssets = CountSourceAssets(sourceRoot);
            }

            CopySourceToStaging(sourceRoot, totalSourceAssets, out List<string> importedAssetPaths, out List<string> importedFbxPaths, ref copiedAssets, ref copiedFbxs, ref processedAssets, limitedSourceFiles);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var assetPath in importedAssetPaths)
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var fbxPaths = FindAllFbxInStaging();
            if (maxFbxToProcess > 0)
            {
                if (limitedFbxAssetPaths != null && limitedFbxAssetPaths.Count > 0)
                    fbxPaths = fbxPaths.Where(path => limitedFbxAssetPaths.Contains(path)).ToList();
                else
                    fbxPaths = new List<string>();
            }
            int processed = 0;
            int skippedUnchanged = 0;
            int failed = 0;

            int fbxTotal = fbxPaths.Count;
            var fbxStart = DateTime.UtcNow;
            for (int i = 0; i < fbxPaths.Count; i++)
            {
                string fbxPath = fbxPaths[i];
                bool skipped;
                bool ok = ProcessFbx(fbxPath, SkipUnchanged, out skipped);
                if (ok && skipped)
                    skippedUnchanged++;
                else if (ok)
                    processed++;
                else
                    failed++;

                if (VerboseLogging)
                    Debug.Log($"[FFXIV] Processed FBX: {fbxPath}");

                if (i % LogEveryNAssets == 0)
                    Debug.Log($"[FFXIV] Processed FBXs {i + 1}/{fbxTotal}");

                string fbxEta = EstimateEta(fbxStart, i + 1, fbxTotal);
                ThrowIfProgressCanceled(
                    "FFXIV Processed Prefabs",
                    $"Processing FBX {i + 1}/{fbxTotal} {fbxEta}",
                    fbxTotal == 0 ? 1f : (i + 1f) / fbxTotal
                );
            }

            EditorUtility.ClearProgressBar();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int movedAddressables = 0;
            bool addressablesUpdated = false;
            bool runtimeCatalogBuilt = false;
            bool indexBuilt = false;
            bool prefabCatalogBuilt = false;
            bool stagingCleaned = false;

            if (autoSetupAddressables)
            {
                movedAddressables = FfxivAddressablesConfigurator.SetupGroups(showDialog: false);
                addressablesUpdated = true;
            }

            runtimeCatalogBuilt = InvokeEditorBoolBuilder("FfxivRuntimeCatalogBuilder", "BuildCatalog", false);

            if (autoBuildAddressableIndex)
            {
                indexBuilt = InvokeEditorBoolBuilder("FfxivAddressableIndexBuilder", "BuildIndex", false);
                prefabCatalogBuilt = InvokeEditorBoolBuilder("FfxivPrefabCatalogBuilder", "BuildCatalog", false);
            }

            if (cleanupStaging)
                stagingCleaned = CleanupStaging();

            EditorUtility.DisplayDialog(
                "FFXIV Processed Prefabs",
                $"Copied assets: {copiedAssets}\nCopied FBXs: {copiedFbxs}\nProcessed FBXs: {processed}\nSkipped unchanged: {skippedUnchanged}\nFailed: {failed}\n\nAddressables updated: {(addressablesUpdated ? movedAddressables.ToString() : "No")}" +
                $"\nRuntime catalog: {(runtimeCatalogBuilt ? "Built" : "Failed")}" +
                $"\nAddressable index: {(autoBuildAddressableIndex ? (indexBuilt ? "Built" : "Failed") : "No")}" +
                $"\nPrefab catalog: {(autoBuildAddressableIndex ? (prefabCatalogBuilt ? "Built" : "Failed") : "No")}" +
                $"\nStaging cleanup: {(cleanupStaging ? (stagingCleaned ? "Removed" : "Skipped") : "No")}" +
                $"\n\nOutput: {ProcessedRoot}",
                "OK"
            );

            if (processed == 0)
                Debug.LogWarning("[FFXIV] No FBXs were processed. Check the Console for errors and ensure the source root contains .fbx files.");
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[FFXIV] Processed prefab build canceled by user.");
            if (!throwOnCancel)
            {
                EditorUtility.DisplayDialog("FFXIV Processed Prefabs", "Build canceled by user.", "OK");
                return;
            }

            throw;
        }
        finally
        {
            EditorPrefs.SetBool(ImportProcessingKey, wasProcessingEnabled);
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Toggle Verbose Logging")]
    private static void ToggleVerboseLogging()
    {
        bool current = EditorPrefs.GetBool(VerboseLoggingKey, false);
        bool next = !current;
        EditorPrefs.SetBool(VerboseLoggingKey, next);
        Debug.Log($"[FFXIV] Processed Prefabs verbose logging: {(next ? "ON" : "OFF")}");
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Toggle Skip Unchanged")]
    private static void ToggleSkipUnchanged()
    {
        bool current = EditorPrefs.GetBool(SkipUnchangedKey, true);
        bool next = !current;
        EditorPrefs.SetBool(SkipUnchangedKey, next);
        Debug.Log($"[FFXIV] Processed Prefabs skip unchanged: {(next ? "ON" : "OFF")}");
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Toggle Auto Addressables Setup")]
    private static void ToggleAutoAddressablesSetup()
    {
        ToggleBoolPreference(AutoSetupAddressablesKey, DefaultAutoSetupAddressables, "auto setup addressables");
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Toggle Auto Addressable Index Build")]
    private static void ToggleAutoAddressableIndexBuild()
    {
        ToggleBoolPreference(AutoBuildAddressableIndexKey, DefaultAutoBuildAddressableIndex, "auto build addressable index");
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Toggle Cleanup Staging (Remove FBX)")]
    private static void ToggleCleanupStaging()
    {
        ToggleBoolPreference(CleanupStagingKey, DefaultCleanupStaging, "cleanup staging (remove FBX)");
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Cleanup Staging Now")]
    private static void CleanupStagingNow()
    {
        bool removed = CleanupStaging();
        Debug.Log($"[FFXIV] Processed Prefabs staging cleanup: {(removed ? "REMOVED" : "SKIPPED")}");
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Set Max FBXs/20")]
    private static void SetMaxFbx20()
    {
        SetMaxFbxLimit(20);
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Set Max FBXs/50")]
    private static void SetMaxFbx50()
    {
        SetMaxFbxLimit(50);
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Set Max FBXs/Unlimited")]
    private static void SetMaxFbxUnlimited()
    {
        SetMaxFbxLimit(0);
    }

    [MenuItem("Tools/FFXIV/Processed Prefabs/Diagnostics/Log First 20 FBXs")]
    private static void LogFirstFbxPaths()
    {
        string sourceRoot = EditorPrefs.GetString(SourceRootKey, DefaultSourceRoot);
        if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            EditorUtility.DisplayDialog("Source Root Missing", "Set a valid source root first.", "OK");
            return;
        }

        sourceRoot = NormalizePath(sourceRoot);
        const int max = 20;
        int count = 0;

        Debug.Log($"[FFXIV] FBX scan root: {sourceRoot}");

        try
        {
            foreach (var file in EnumerateFilesSafe(sourceRoot, "*.fbx", SearchOption.AllDirectories))
            {
                Debug.Log($"[FFXIV] FBX {count + 1}: {file}");
                count++;
                if (count >= max)
                    break;
            }

            Debug.Log($"[FFXIV] FBX scan finished. Found {count} (showing up to {max}).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FFXIV] FBX scan failed: {ex}");
        }
    }

    private static int GetMaxFbxLimit()
    {
        return Mathf.Max(0, EditorPrefs.GetInt(MaxFbxKey, DefaultMaxFbx));
    }

    private static void ToggleBoolPreference(string key, bool defaultValue, string label)
    {
        bool current = EditorPrefs.GetBool(key, defaultValue);
        bool next = !current;
        EditorPrefs.SetBool(key, next);
        Debug.Log($"[FFXIV] Processed Prefabs {label}: {(next ? "ON" : "OFF")}");
    }

    private static void SetMaxFbxLimit(int value)
    {
        int clamped = Mathf.Max(0, value);
        EditorPrefs.SetInt(MaxFbxKey, clamped);
        Debug.Log($"[FFXIV] Processed Prefabs max FBXs: {(clamped <= 0 ? "Unlimited" : clamped.ToString())}");
    }

    private static bool CleanupStaging()
    {
        if (!AssetDatabase.IsValidFolder(StagingRoot))
            return false;

        bool removed = AssetDatabase.DeleteAsset(StagingRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return removed;
    }

    private static List<string> BuildLimitedSourceFileList(string sourceRoot, int maxFbx, out HashSet<string> limitedFbxAssetPaths)
    {
        limitedFbxAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedFbxAbs = new List<string>();
        var textureRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileRaw in EnumerateFilesSafe(sourceRoot, "*.fbx", SearchOption.AllDirectories))
        {
            string fbxAbs = NormalizePath(fileRaw);
            if (!fbxAbs.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            selectedFbxAbs.Add(fbxAbs);

                string rel = NormalizeSourceRelativePath(fbxAbs, sourceRoot);
                if (!string.IsNullOrEmpty(rel))
                    limitedFbxAssetPaths.Add($"{StagingRoot}/{rel}".Replace("\\", "/"));

            string textureRoot = GetTextureRootForFbx(fileRaw);
            if (!string.IsNullOrEmpty(textureRoot))
                textureRoots.Add(textureRoot);

            if (selectedFbxAbs.Count >= maxFbx)
                break;
        }

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fbx in selectedFbxAbs)
            files.Add(fbx);

        foreach (var root in textureRoots)
        {
            foreach (var fileRaw in EnumerateFilesSafe(root, "*.*", SearchOption.AllDirectories))
            {
                string src = NormalizePath(fileRaw);
                string ext = Path.GetExtension(src).ToLowerInvariant();
                if (TextureExtensions.Contains(ext))
                    files.Add(src);
            }
        }

        return files.ToList();
    }

    private static string GetTextureRootForFbx(string fbxPath)
    {
        string dir = Path.GetDirectoryName(fbxPath);
        if (string.IsNullOrEmpty(dir))
            return string.Empty;

        var parent = Directory.GetParent(dir);
        string root = parent?.FullName ?? dir;
        return root.Replace("\\", "/").TrimEnd('/');
    }

    private static int CountSourceAssets(string sourceRoot)
    {
        int total = 0;
        foreach (var fileRaw in EnumerateFilesSafe(sourceRoot, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(fileRaw).ToLowerInvariant();
            if (ext == ".fbx" || TextureExtensions.Contains(ext))
                total++;
        }

        return total;
    }

    private static void CopySourceToStaging(string sourceRoot, int totalSourceAssets, out List<string> importedAssetPaths, out List<string> importedFbxPaths, ref int copiedAssets, ref int copiedFbxs, ref int processedAssets, IReadOnlyList<string> sourceFiles = null)
    {
        importedAssetPaths = new List<string>();
        importedFbxPaths = new List<string>();

        string assetsAbs = Application.dataPath.Replace("\\", "/").TrimEnd('/');
        string stagingAbs = $"{assetsAbs}/{StagingRoot.Substring("Assets/".Length)}";

        var scanStart = DateTime.UtcNow;
        AssetDatabase.DisallowAutoRefresh();
        try
        {
            IEnumerable<string> files = sourceFiles ?? EnumerateFilesSafe(sourceRoot, "*.*", SearchOption.AllDirectories);
            foreach (var fileRaw in files)
            {
                string src = NormalizePath(fileRaw);
                string ext = Path.GetExtension(src).ToLowerInvariant();

                bool isFbx = ext == ".fbx";
                bool isTexture = TextureExtensions.Contains(ext);

                if (!isFbx && !isTexture)
                    continue;

                processedAssets++;
                if (processedAssets % LogEveryNAssets == 0)
                    Debug.Log($"[FFXIV] Scanning source {processedAssets}/{totalSourceAssets}");

                string scanEta = EstimateEta(scanStart, processedAssets, totalSourceAssets);
                ThrowIfProgressCanceled(
                    "FFXIV Processed Prefabs",
                    $"Scanning source {processedAssets}/{totalSourceAssets} {scanEta}",
                    totalSourceAssets == 0 ? 1f : processedAssets / (float)totalSourceAssets
                );

                string rel = NormalizeSourceRelativePath(src, sourceRoot);
                if (string.IsNullOrEmpty(rel))
                    continue;
                string dstAbs = $"{stagingAbs}/{rel}";
                string dstAssetPath = $"{StagingRoot}/{rel}".Replace("\\", "/");

                EnsureFolderAbsolute(Path.GetDirectoryName(dstAbs).Replace("\\", "/"), assetsAbs);

                bool needsCopy = !FileExistsLong(dstAbs) || !IsSameFile(src, dstAbs);
                if (!needsCopy)
                    continue;

                try
                {
                    FileCopyLong(src, dstAbs, overwrite: true);
                    CopyTimestamp(src, dstAbs);

                    copiedAssets++;
                    if (isFbx)
                        copiedFbxs++;

                    importedAssetPaths.Add(dstAssetPath);
                    if (isFbx)
                        importedFbxPaths.Add(dstAssetPath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FFXIV] Copy failed for '{src}' -> '{dstAbs}': {ex.Message}");
                }
            }
        }
        finally
        {
            AssetDatabase.AllowAutoRefresh();
            EditorUtility.ClearProgressBar();
        }
    }

    private static List<string> FindAllFbxInStaging()
    {
        var guids = AssetDatabase.FindAssets("t:Model", new[] { StagingRoot });
        var paths = new List<string>();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                paths.Add(path);
        }

        return paths;
    }

    private static bool ProcessFbx(string fbxPath, bool skipUnchanged, out bool skipped)
    {
        skipped = false;
        try
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                Debug.LogWarning($"[FFXIV] Could not load FBX model: {fbxPath}");
                return false;
            }

            string prefabName = Path.GetFileNameWithoutExtension(fbxPath);
            var category = ResolveCategory(fbxPath, prefabName);
            string categoryFolder = $"{ProcessedRoot}/{category}";
            string prefabFolder = $"{categoryFolder}/{SanitizeName(prefabName)}";
            string prefabPath = $"{prefabFolder}/{SanitizeName(prefabName)}.prefab";

            if (skipUnchanged && IsProcessedPrefabUpToDate(fbxPath, prefabPath))
            {
                skipped = true;
                if (VerboseLogging)
                    Debug.Log($"[FFXIV] Skipped unchanged FBX: {fbxPath}");
                return true;
            }

            if (AssetDatabase.IsValidFolder(prefabFolder))
                AssetDatabase.DeleteAsset(prefabFolder);

            EnsureFolder(categoryFolder);
            EnsureFolder(prefabFolder);

            string materialsFolder = $"{prefabFolder}/Materials";
            string texturesFolder = $"{materialsFolder}/Textures";
            string meshesFolder = $"{prefabFolder}/Meshes";

            EnsureFolder(materialsFolder);
            EnsureFolder(texturesFolder);
            EnsureFolder(meshesFolder);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(model);

            instance.name = prefabName;
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            var materialMap = new Dictionary<Material, Material>();
            var textureMap = new Dictionary<string, string>();
            var meshMap = new Dictionary<Mesh, Mesh>();

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                bool isHair = FfxivMaterialPolicy.IsHairAsset(fbxPath, null, renderer.name);

            for (int i = 0; i < shared.Length; i++)
            {
                var mat = shared[i];
                if (mat == null)
                    continue;

                if (!materialMap.TryGetValue(mat, out var newMat))
                {
                    newMat = new Material(mat) { name = mat.name };
                    FfxivMaterialPolicy.EnsureUrpLitShader(newMat);
                    FfxivMaterialPolicy.ApplyOpaqueAlphaClip(newMat);
                    if (isHair)
                        FfxivMaterialPolicy.ApplyHairDoubleSided(newMat, true);
                    else
                        FfxivMaterialPolicy.ApplyFrontFaceOnly(newMat);

                    string matPath = AssetDatabase.GenerateUniqueAssetPath($"{materialsFolder}/{SanitizeName(newMat.name)}.mat");
                    AssetDatabase.CreateAsset(newMat, matPath);

                    CopyMaterialTextures(newMat, texturesFolder, textureMap);
                    materialMap.Add(mat, newMat);
                }

                shared[i] = newMat;
            }

                renderer.sharedMaterials = shared;
            }

            foreach (var meshFilter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = meshFilter.sharedMesh;
                if (mesh == null)
                    continue;

            if (!meshMap.TryGetValue(mesh, out var newMesh))
            {
                newMesh = UnityEngine.Object.Instantiate(mesh);
                newMesh.name = mesh.name;
                string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{meshesFolder}/{SanitizeName(newMesh.name)}.asset");
                AssetDatabase.CreateAsset(newMesh, meshPath);
                meshMap.Add(mesh, newMesh);
            }

                meshFilter.sharedMesh = newMesh;
            }

            foreach (var skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skinned.sharedMesh;
                if (mesh == null)
                    continue;

            if (!meshMap.TryGetValue(mesh, out var newMesh))
            {
                newMesh = UnityEngine.Object.Instantiate(mesh);
                newMesh.name = mesh.name;
                string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{meshesFolder}/{SanitizeName(newMesh.name)}.asset");
                AssetDatabase.CreateAsset(newMesh, meshPath);
                meshMap.Add(mesh, newMesh);
            }

                skinned.sharedMesh = newMesh;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FFXIV] ProcessFbx failed for '{fbxPath}': {ex}");
            return false;
        }
    }

    private static void CopyMaterialTextures(Material material, string texturesFolder, Dictionary<string, string> textureMap)
    {
        if (material == null || material.shader == null)
            return;

        int propertyCount = ShaderUtil.GetPropertyCount(material.shader);
        for (int i = 0; i < propertyCount; i++)
        {
            if (ShaderUtil.GetPropertyType(material.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                continue;

            string property = ShaderUtil.GetPropertyName(material.shader, i);
            var tex = material.GetTexture(property);
            if (tex == null)
                continue;

            string srcPath = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(srcPath) || !srcPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!textureMap.TryGetValue(srcPath, out string dstPath))
            {
                string fileName = Path.GetFileName(srcPath);
                dstPath = AssetDatabase.GenerateUniqueAssetPath($"{texturesFolder}/{fileName}");
                AssetDatabase.CopyAsset(srcPath, dstPath);
                textureMap[srcPath] = dstPath;
            }

            var newTex = AssetDatabase.LoadAssetAtPath<Texture>(dstPath);
            if (newTex != null)
                material.SetTexture(property, newTex);
        }
    }

    private static ProcessedCategory ResolveCategory(string fbxPath, string prefabName)
    {
        string name = prefabName.ToLowerInvariant();
        string path = fbxPath.Replace("\\", "/").ToLowerInvariant();

        if (name.Contains("_hir") || path.Contains("/hair/") || path.Contains("/obj/hair/"))
            return ProcessedCategory.Hair;

        if (name.Contains("_fac") || path.Contains("/face/") || path.Contains("/obj/face/"))
            return ProcessedCategory.Face;

        if (name.Contains("_wep") || path.Contains("/wep/") || path.Contains("/weapon/"))
            return ProcessedCategory.Weapons;

        return ProcessedCategory.Equipment;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        var parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void EnsureFolderAbsolute(string absFolder, string assetsAbs)
    {
        absFolder = absFolder.Replace("\\", "/");
        assetsAbs = assetsAbs.Replace("\\", "/");

        if (!absFolder.StartsWith(assetsAbs, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Destination folder must be under this project's Assets folder.");

        string rel = "Assets" + absFolder.Substring(assetsAbs.Length);
        rel = rel.TrimEnd('/');

        EnsureFolder(rel);
    }

    private static bool IsSameFile(string src, string dst)
    {
        var srcInfo = GetFileInfoLong(src);
        var dstInfo = GetFileInfoLong(dst);

        if (!srcInfo.Exists || !dstInfo.Exists)
            return false;

        if (srcInfo.Length != dstInfo.Length)
            return false;

        return srcInfo.LastWriteTimeUtc == dstInfo.LastWriteTimeUtc;
    }

    private static void CopyTimestamp(string src, string dst)
    {
        DateTime srcTime = GetLastWriteTimeUtcLong(src);
        SetLastWriteTimeUtcLong(dst, srcTime);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, string pattern, SearchOption searchOption)
    {
        string rootLong = ToLongPath(root);
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(rootLong, pattern, searchOption);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FFXIV] EnumerateFiles failed for '{root}': {ex}");
            yield break;
        }

        foreach (var file in files)
            yield return NormalizePath(FromLongPath(file));
    }

    private static bool FileExistsLong(string path)
    {
        return File.Exists(ToLongPath(path));
    }

    private static FileInfo GetFileInfoLong(string path)
    {
        return new FileInfo(ToLongPath(path));
    }

    private static void FileCopyLong(string src, string dst, bool overwrite)
    {
        File.Copy(ToLongPath(src), ToLongPath(dst), overwrite);
    }

    private static DateTime GetLastWriteTimeUtcLong(string path)
    {
        return File.GetLastWriteTimeUtc(ToLongPath(path));
    }

    private static void SetLastWriteTimeUtcLong(string path, DateTime time)
    {
        File.SetLastWriteTimeUtc(ToLongPath(path), time);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        return path.Replace("\\", "/").TrimEnd('/');
    }

    private static string NormalizeSourceRelativePath(string absolutePath, string sourceRoot)
    {
        if (string.IsNullOrEmpty(absolutePath) || string.IsNullOrEmpty(sourceRoot))
            return string.Empty;

        string normalizedAbs = NormalizePath(absolutePath);
        string normalizedRoot = NormalizePath(sourceRoot);

        if (!normalizedAbs.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        string rel = normalizedAbs.Substring(normalizedRoot.Length).TrimStart('/');

        const string marker = "/.meta/";
        int metaIndex = rel.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (metaIndex >= 0)
        {
            rel = rel.Substring(metaIndex + marker.Length);
        }

        int duplicateIndex = rel.IndexOf("/chara/", StringComparison.OrdinalIgnoreCase);
        if (duplicateIndex >= 0)
        {
            rel = rel.Substring(duplicateIndex + 1);
        }

        return rel.TrimStart('/');
    }

    private static string ToLongPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch
        {
            full = path;
        }

        if (full.StartsWith(@"\\?\"))
            return full;

        if (full.StartsWith(@"\\"))
            return @"\\?\UNC\" + full.Substring(2);

        if (Path.IsPathRooted(full))
            return @"\\?\" + full;

        return full;
    }

    private static string FromLongPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (path.StartsWith(@"\\?\UNC\"))
            return @"\\" + path.Substring(8);

        if (path.StartsWith(@"\\?\"))
            return path.Substring(4);

        return path;
    }

    private static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    private static string EstimateEta(DateTime start, int completed, int total)
    {
        if (completed <= 0 || total <= 0)
            return string.Empty;

        double elapsedSeconds = (DateTime.UtcNow - start).TotalSeconds;
        double perItem = elapsedSeconds / completed;
        double remaining = Math.Max(0, (total - completed) * perItem);
        var eta = TimeSpan.FromSeconds(remaining);

        if (eta.TotalHours >= 1)
            return $"ETA {eta.Hours:D2}:{eta.Minutes:D2}:{eta.Seconds:D2}";

        return $"ETA {eta.Minutes:D2}:{eta.Seconds:D2}";
    }

    private static bool IsProcessedPrefabUpToDate(string fbxAssetPath, string prefabAssetPath)
    {
        string fbxAbs = AssetPathToAbsolute(fbxAssetPath);
        string prefabAbs = AssetPathToAbsolute(prefabAssetPath);
        if (!FileExistsLong(fbxAbs) || !FileExistsLong(prefabAbs))
            return false;

        DateTime fbxTime = GetLastWriteTimeUtcLong(fbxAbs);
        DateTime prefabTime = GetLastWriteTimeUtcLong(prefabAbs);
        return prefabTime >= fbxTime;
    }

    private static string AssetPathToAbsolute(string assetPath)
    {
        string path = assetPath.Replace("\\", "/");
        if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            return path;

        string assetsAbs = Application.dataPath.Replace("\\", "/");
        return assetsAbs + path.Substring("Assets".Length);
    }

    private static bool InvokeEditorBoolBuilder(string typeName, string methodName, bool showDialog)
    {
        var type = FindTypeByName(typeName);
        if (type == null)
        {
            Debug.LogError($"[FFXIV] Could not find editor builder type '{typeName}'.");
            return false;
        }

        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(bool) },
            modifiers: null
        );

        if (method == null)
        {
            Debug.LogError($"[FFXIV] Could not find method '{typeName}.{methodName}(bool)'.");
            return false;
        }

        try
        {
            object result = method.Invoke(null, new object[] { showDialog });
            if (result is bool boolResult)
                return boolResult;

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FFXIV] Failed invoking '{typeName}.{methodName}': {ex.Message}");
            return false;
        }
    }

    private static Type FindTypeByName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        var direct = Type.GetType(typeName);
        if (direct != null)
            return direct;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            var assembly = assemblies[i];
            var type = assembly.GetType(typeName);
            if (type != null)
                return type;

            Type[] allTypes;
            try
            {
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                allTypes = ex.Types;
            }

            if (allTypes == null)
                continue;

            for (int t = 0; t < allTypes.Length; t++)
            {
                var candidate = allTypes[t];
                if (candidate == null)
                    continue;
                if (string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
                    return candidate;
            }
        }

        return null;
    }

    private static void ThrowIfProgressCanceled(string title, string message, float progress)
    {
        if (EditorUtility.DisplayCancelableProgressBar(title, message, Mathf.Clamp01(progress)))
            throw new OperationCanceledException();
    }
}
