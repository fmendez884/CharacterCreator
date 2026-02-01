using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class FfxivImportExtractOpaqueWindow : EditorWindow
{
    private const string RootFolder = "Assets/FFXIV_Imported";

    [MenuItem("Tools/FFXIV/Import FBX + Extract Materials + Force Opaque (URP)...")]
    public static void Open()
    {
        GetWindow<FfxivImportExtractOpaqueWindow>("FFXIV FBX Import");
    }

    private void OnGUI()
    {
        GUILayout.Label("Import FBX → Extract materials → Force URP Surface Opaque", EditorStyles.boldLabel);
        GUILayout.Space(6);

        EditorGUILayout.HelpBox(
            "Click the button, choose an .fbx file.\n\n" +
            "It will:\n" +
            "• Create Assets/FFXIV_Imported/<FbxName>/\n" +
            "• Copy the FBX there\n" +
            "• Create <FbxName>/Materials/\n" +
            "• Extract/duplicate embedded materials into Materials/\n" +
            "• Force those materials to URP Surface Type = Opaque\n" +
            "• Remap the model importer to use the extracted materials\n",
            MessageType.Info
        );

        GUILayout.Space(10);

        if (GUILayout.Button("Import FBX + Extract Materials + Force Opaque", GUILayout.Height(38)))
        {
            ImportExtractAndFix();
        }

        GUILayout.Space(8);
        GUILayout.Label($"Root folder: {RootFolder}", EditorStyles.miniLabel);
    }

    private static void ImportExtractAndFix()
    {
        var srcPath = EditorUtility.OpenFilePanel("Select FBX", "", "fbx");
        if (string.IsNullOrEmpty(srcPath))
            return;

        EnsureFolder(RootFolder);

        string baseName = Path.GetFileNameWithoutExtension(srcPath);
        string modelFolder = $"{RootFolder}/{SanitizeName(baseName)}";
        string materialsFolder = $"{modelFolder}/Materials";

        EnsureFolder(modelFolder);
        EnsureFolder(materialsFolder);

        // Copy FBX into its own folder
        string dstFbxPath = $"{modelFolder}/{SanitizeName(baseName)}.fbx";
        File.Copy(srcPath, dstFbxPath, overwrite: true);

        // First import (creates the model asset + embedded sub-assets)
        AssetDatabase.ImportAsset(dstFbxPath, ImportAssetOptions.ForceSynchronousImport);

        // Get importer and set it up for remapping
        var importer = AssetImporter.GetAtPath(dstFbxPath) as ModelImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not get ModelImporter for: " + dstFbxPath, "OK");
            return;
        }

        // Ensure Unity will use external materials once remapped
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.Local;

        // Load embedded materials from the FBX
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(dstFbxPath);

        int extractedCount = 0;
        int opaqueCount = 0;
        int remapCount = 0;

        foreach (var obj in subAssets)
        {
            if (obj is not Material embeddedMat)
                continue;

            // Create an external .mat asset in Materials/
            string matAssetPath = $"{materialsFolder}/{SanitizeName(embeddedMat.name)}.mat";
            Material externalMat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);

            if (externalMat == null)
            {
                externalMat = new Material(embeddedMat) { name = embeddedMat.name };
                AssetDatabase.CreateAsset(externalMat, matAssetPath);
                extractedCount++;
            }

            bool isHair = FfxivMaterialPolicy.IsHairAsset(dstFbxPath, embeddedMat.name);

            // Force URP opaque + alpha clip; double-sided for hair, front-only for others
            if (ForceOpaqueUrp(externalMat, isHair))
            {
                EditorUtility.SetDirty(externalMat);
                opaqueCount++;
            }

            // Remap: tell the importer to use our extracted material instead of embedded
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), embeddedMat.name);
            importer.AddRemap(id, externalMat);
            remapCount++;
        }

        // Reimport so the remaps take effect
        importer.SaveAndReimport();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Done",
            $"Imported:\n{dstFbxPath}\n\n" +
            $"Materials folder:\n{materialsFolder}\n\n" +
            $"Extracted/created: {extractedCount}\n" +
            $"Forced Opaque: {opaqueCount}\n" +
            $"Remapped: {remapCount}",
            "OK"
        );
    }

    private static bool ForceOpaqueUrp(Material mat, bool isHair)
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

    private static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
