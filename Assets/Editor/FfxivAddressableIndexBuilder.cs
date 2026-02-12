using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class FfxivAddressableIndexBuilder
{
    private const string DefaultAssetPath = "Assets/Resources/FfxivAddressableIndex.asset";

    [MenuItem("Tools/FFXIV/Addressables/Build Addressable Index")]
    public static void BuildIndex()
    {
        BuildIndex(showDialog: true);
    }

    public static bool BuildIndex(bool showDialog)
    {
        if (!FfxivRuntimeCatalogBuilder.BuildCatalog(showDialog: false))
            return false;

        var runtimeCatalog = FfxivRuntimeCatalogBuilder.LoadOrCreateCatalog();
        if (runtimeCatalog == null)
        {
            Debug.LogError("[FFXIV] Runtime catalog not found. Build runtime catalog first.");
            return false;
        }

        var index = LoadOrCreateIndex();
        index.maleToken = runtimeCatalog.maleToken;
        index.femaleToken = runtimeCatalog.femaleToken;
        index.hairToken = runtimeCatalog.hairToken;
        index.faceToken = runtimeCatalog.faceToken;
        index.equipmentBaseToken = runtimeCatalog.equipmentBaseToken;
        index.headToken = runtimeCatalog.headToken;
        index.bodyToken = runtimeCatalog.bodyToken;
        index.handsToken = runtimeCatalog.handsToken;
        index.legsToken = runtimeCatalog.legsToken;
        index.feetToken = runtimeCatalog.feetToken;
        index.labelHair = runtimeCatalog.labelHair;
        index.labelFace = runtimeCatalog.labelFace;
        index.labelEquipment = runtimeCatalog.labelEquipment;
        index.labelWeapons = runtimeCatalog.labelWeapons;
        index.labelBody = runtimeCatalog.labelBody;

        CopyList(runtimeCatalog.maleHair, index.maleHair);
        CopyList(runtimeCatalog.femaleHair, index.femaleHair);
        CopyList(runtimeCatalog.maleFace, index.maleFace);
        CopyList(runtimeCatalog.femaleFace, index.femaleFace);
        CopyList(runtimeCatalog.maleHead, index.maleHead);
        CopyList(runtimeCatalog.maleBody, index.maleBody);
        CopyList(runtimeCatalog.maleHands, index.maleHands);
        CopyList(runtimeCatalog.maleLegs, index.maleLegs);
        CopyList(runtimeCatalog.maleFeet, index.maleFeet);
        CopyList(runtimeCatalog.femaleHead, index.femaleHead);
        CopyList(runtimeCatalog.femaleBody, index.femaleBody);
        CopyList(runtimeCatalog.femaleHands, index.femaleHands);
        CopyList(runtimeCatalog.femaleLegs, index.femaleLegs);
        CopyList(runtimeCatalog.femaleFeet, index.femaleFeet);
        CopyList(runtimeCatalog.maleBaseBodies, index.maleBaseBodies);
        CopyList(runtimeCatalog.femaleBaseBodies, index.femaleBaseBodies);
        CopyList(runtimeCatalog.weapons, index.weapons);

        EditorUtility.SetDirty(index);
        AssetDatabase.SaveAssets();
        if (showDialog)
            EditorUtility.DisplayDialog("FFXIV Addressables", "Addressable index built from runtime catalog.", "OK");
        return true;
    }

    private static FfxivAddressableIndex LoadOrCreateIndex()
    {
        var index = AssetDatabase.LoadAssetAtPath<FfxivAddressableIndex>(DefaultAssetPath);
        if (index != null)
            return index;

        var folder = System.IO.Path.GetDirectoryName(DefaultAssetPath);
        if (!string.IsNullOrWhiteSpace(folder))
            System.IO.Directory.CreateDirectory(folder);

        index = ScriptableObject.CreateInstance<FfxivAddressableIndex>();
        AssetDatabase.CreateAsset(index, DefaultAssetPath);
        return index;
    }

    private static void CopyList(List<string> source, List<string> target)
    {
        target.Clear();
        if (source == null || source.Count == 0)
            return;

        target.AddRange(source);
    }
}
