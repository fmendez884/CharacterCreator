using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BatchImportFbxFromDisk
{
    private const string TargetRoot = "Assets/FFXIV_Imported";

    [MenuItem("Tools/FFXIV/Batch Import FBXs (Skip Unchanged + Apply Import Profile)")]
    public static void ImportFolderRecursiveOverwrite()
    {
        string sourceRoot = EditorUtility.OpenFolderPanel("Select folder containing FBXs", "", "");
        if (string.IsNullOrEmpty(sourceRoot))
            return;

        EnsureFolder(TargetRoot);

        sourceRoot = sourceRoot.Replace('\\', '/').TrimEnd('/');
        string assetsAbs = Application.dataPath.Replace('\\', '/').TrimEnd('/');
        string targetAbs = $"{assetsAbs}/{TargetRoot.Substring("Assets/".Length)}";

        var importedAssetPaths = new List<string>();
        int copied = 0;
        int skippedExisting = 0;
        int skippedUnchanged = 0;

        AssetDatabase.DisallowAutoRefresh();
        try
        {
            foreach (var srcRaw in Directory.EnumerateFiles(sourceRoot, "*.fbx", SearchOption.AllDirectories))
            {
                string src = srcRaw.Replace('\\', '/');

                // Keep relative structure
                string rel = src.Substring(sourceRoot.Length).TrimStart('/');
                string dstAbs = $"{targetAbs}/{rel}";
                string dstAssetPath = $"{TargetRoot}/{rel}".Replace('\\', '/');

                EnsureFolderAbsolute(Path.GetDirectoryName(dstAbs).Replace('\\', '/'), assetsAbs);

                if (File.Exists(dstAbs))
                {
                    if (IsSameFile(src, dstAbs))
                    {
                        skippedUnchanged++;
                        continue;
                    }

                    skippedExisting++;
                    continue;
                }

                File.Copy(src, dstAbs, overwrite: false);
                CopyTimestamp(src, dstAbs);
                copied++;

                importedAssetPaths.Add(dstAssetPath);
            }
        }
        finally
        {
            AssetDatabase.AllowAutoRefresh();
        }

        // Import all copied FBXs and force update so postprocessors run
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var assetPath in importedAssetPaths)
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Batch Import Complete",
            $"Copied & imported: {copied} FBX file(s)\n" +
            $"Skipped unchanged: {skippedUnchanged}\n" +
            $"Skipped existing (different): {skippedExisting}\n\n" +
            $"Into:\n{TargetRoot}",
            "OK"
        );
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

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

    private static void EnsureFolderAbsolute(string absFolder, string assetsAbs)
    {
        absFolder = absFolder.Replace('\\', '/');
        assetsAbs = assetsAbs.Replace('\\', '/');

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

        DateTime srcTime = srcInfo.LastWriteTimeUtc;
        DateTime dstTime = dstInfo.LastWriteTimeUtc;

        return srcTime == dstTime;
    }

    private static void CopyTimestamp(string src, string dst)
    {
        DateTime srcTime = File.GetLastWriteTimeUtc(src);
        File.SetLastWriteTimeUtc(dst, srcTime);
    }
}
