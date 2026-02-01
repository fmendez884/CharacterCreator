using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FfxivProcessedPrefabBuilder
{
    private const string SourceRootKey = "FFXIV.Processed.SourceRoot";
    private const string DefaultSourceRoot = "C:/Users/fmend/Desktop/TEST-EXTRACT-HUMAN-MODELS/chara";
    private const string StagingRoot = "Assets/FFXIV_Imported";
    private const string ProcessedRoot = "Assets/FFXIV_Processed";
    private const string ImportProcessingKey = "FFXIV.ImportProcessingEnabled";

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

    public static void BuildFromSourceRoot(string sourceRoot)
    {
        VerboseLogging = EditorPrefs.GetBool(VerboseLoggingKey, false);
        SkipUnchanged = EditorPrefs.GetBool(SkipUnchangedKey, true);
        sourceRoot = sourceRoot.Replace("\\", "/").TrimEnd('/');

        EnsureFolder(StagingRoot);
        EnsureFolder(ProcessedRoot);

        bool wasProcessingEnabled = EditorPrefs.GetBool(ImportProcessingKey, false);
        EditorPrefs.SetBool(ImportProcessingKey, true);

        int copiedAssets = 0;
        int copiedFbxs = 0;

        int totalSourceAssets = 0;
        int processedAssets = 0;

        try
        {
            totalSourceAssets = CountSourceAssets(sourceRoot);
            CopySourceToStaging(sourceRoot, totalSourceAssets, out List<string> importedAssetPaths, out List<string> importedFbxPaths, ref copiedAssets, ref copiedFbxs, ref processedAssets);

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
                EditorUtility.DisplayProgressBar("FFXIV Processed Prefabs", $"Processing FBX {i + 1}/{fbxTotal} {fbxEta}", fbxTotal == 0 ? 1f : (i + 1f) / fbxTotal);
            }

            EditorUtility.ClearProgressBar();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "FFXIV Processed Prefabs",
                $"Copied assets: {copiedAssets}\nCopied FBXs: {copiedFbxs}\nProcessed FBXs: {processed}\nSkipped unchanged: {skippedUnchanged}\nFailed: {failed}\n\nOutput: {ProcessedRoot}",
                "OK"
            );

            if (processed == 0)
                Debug.LogWarning("[FFXIV] No FBXs were processed. Check the Console for errors and ensure the source root contains .fbx files.");
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

    private static int CountSourceAssets(string sourceRoot)
    {
        int total = 0;
        foreach (var fileRaw in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(fileRaw).ToLowerInvariant();
            if (ext == ".fbx" || TextureExtensions.Contains(ext))
                total++;
        }

        return total;
    }

    private static void CopySourceToStaging(string sourceRoot, int totalSourceAssets, out List<string> importedAssetPaths, out List<string> importedFbxPaths, ref int copiedAssets, ref int copiedFbxs, ref int processedAssets)
    {
        importedAssetPaths = new List<string>();
        importedFbxPaths = new List<string>();

        string assetsAbs = Application.dataPath.Replace("\\", "/").TrimEnd('/');
        string stagingAbs = $"{assetsAbs}/{StagingRoot.Substring("Assets/".Length)}";

        var scanStart = DateTime.UtcNow;
        AssetDatabase.DisallowAutoRefresh();
        try
        {
            foreach (var fileRaw in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories))
            {
                string src = fileRaw.Replace("\\", "/");
                string ext = Path.GetExtension(src).ToLowerInvariant();

                bool isFbx = ext == ".fbx";
                bool isTexture = TextureExtensions.Contains(ext);

                if (!isFbx && !isTexture)
                    continue;

                processedAssets++;
                if (processedAssets % LogEveryNAssets == 0)
                    Debug.Log($"[FFXIV] Scanning source {processedAssets}/{totalSourceAssets}");

                string scanEta = EstimateEta(scanStart, processedAssets, totalSourceAssets);
                EditorUtility.DisplayProgressBar("FFXIV Processed Prefabs", $"Scanning source {processedAssets}/{totalSourceAssets} {scanEta}", totalSourceAssets == 0 ? 1f : processedAssets / (float)totalSourceAssets);

                string rel = src.Substring(sourceRoot.Length).TrimStart('/');
                string dstAbs = $"{stagingAbs}/{rel}";
                string dstAssetPath = $"{StagingRoot}/{rel}".Replace("\\", "/");

                EnsureFolderAbsolute(Path.GetDirectoryName(dstAbs).Replace("\\", "/"), assetsAbs);

                bool needsCopy = !File.Exists(dstAbs) || !IsSameFile(src, dstAbs);
                if (!needsCopy)
                    continue;

                try
                {
                    File.Copy(src, dstAbs, overwrite: true);
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
        var srcInfo = new FileInfo(src);
        var dstInfo = new FileInfo(dst);

        if (!srcInfo.Exists || !dstInfo.Exists)
            return false;

        if (srcInfo.Length != dstInfo.Length)
            return false;

        return srcInfo.LastWriteTimeUtc == dstInfo.LastWriteTimeUtc;
    }

    private static void CopyTimestamp(string src, string dst)
    {
        DateTime srcTime = File.GetLastWriteTimeUtc(src);
        File.SetLastWriteTimeUtc(dst, srcTime);
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
        if (!File.Exists(fbxAbs) || !File.Exists(prefabAbs))
            return false;

        DateTime fbxTime = File.GetLastWriteTimeUtc(fbxAbs);
        DateTime prefabTime = File.GetLastWriteTimeUtc(prefabAbs);
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
}
