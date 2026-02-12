using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FfxivPrefabCatalogBuilder
{
    private const string DefaultAssetPath = "Assets/Resources/FfxivPrefabCatalog.asset";

    [MenuItem("Tools/FFXIV/Prefabs/Build Prefab Catalog (Processed Prefabs)")]
    public static void BuildCatalog()
    {
        BuildCatalog(showDialog: true);
    }

    public static bool BuildCatalog(bool showDialog)
    {
        if (!FfxivRuntimeCatalogBuilder.BuildCatalog(showDialog: false))
            return false;

        var runtimeCatalog = FfxivRuntimeCatalogBuilder.LoadOrCreateCatalog();
        if (runtimeCatalog == null)
        {
            Debug.LogError("[FFXIV] Runtime catalog not found. Build runtime catalog first.");
            return false;
        }

        var catalog = LoadOrCreateCatalog();
        catalog.maleToken = runtimeCatalog.maleToken;
        catalog.femaleToken = runtimeCatalog.femaleToken;
        catalog.hairToken = runtimeCatalog.hairToken;
        catalog.faceToken = runtimeCatalog.faceToken;
        catalog.equipmentBaseToken = runtimeCatalog.equipmentBaseToken;
        catalog.headToken = runtimeCatalog.headToken;
        catalog.bodyToken = runtimeCatalog.bodyToken;
        catalog.handsToken = runtimeCatalog.handsToken;
        catalog.legsToken = runtimeCatalog.legsToken;
        catalog.feetToken = runtimeCatalog.feetToken;

        CopyList(runtimeCatalog.maleHairPrefabs, catalog.maleHair);
        CopyList(runtimeCatalog.femaleHairPrefabs, catalog.femaleHair);
        CopyList(runtimeCatalog.maleFacePrefabs, catalog.maleFace);
        CopyList(runtimeCatalog.femaleFacePrefabs, catalog.femaleFace);
        CopyList(runtimeCatalog.maleHeadPrefabs, catalog.maleHead);
        CopyList(runtimeCatalog.maleBodyPrefabs, catalog.maleBody);
        CopyList(runtimeCatalog.maleHandsPrefabs, catalog.maleHands);
        CopyList(runtimeCatalog.maleLegsPrefabs, catalog.maleLegs);
        CopyList(runtimeCatalog.maleFeetPrefabs, catalog.maleFeet);
        CopyList(runtimeCatalog.femaleHeadPrefabs, catalog.femaleHead);
        CopyList(runtimeCatalog.femaleBodyPrefabs, catalog.femaleBody);
        CopyList(runtimeCatalog.femaleHandsPrefabs, catalog.femaleHands);
        CopyList(runtimeCatalog.femaleLegsPrefabs, catalog.femaleLegs);
        CopyList(runtimeCatalog.femaleFeetPrefabs, catalog.femaleFeet);
        CopyList(runtimeCatalog.maleBaseBodyPrefabs, catalog.maleBaseBodies);
        CopyList(runtimeCatalog.femaleBaseBodyPrefabs, catalog.femaleBaseBodies);
        CopyList(runtimeCatalog.weaponPrefabs, catalog.weapons);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        if (showDialog)
            EditorUtility.DisplayDialog("FFXIV Prefab Catalog", "Prefab catalog built from runtime catalog.", "OK");

        return true;
    }

    private static FfxivPrefabCatalog LoadOrCreateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<FfxivPrefabCatalog>(DefaultAssetPath);
        if (catalog != null)
            return catalog;

        var folder = Path.GetDirectoryName(DefaultAssetPath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        catalog = ScriptableObject.CreateInstance<FfxivPrefabCatalog>();
        AssetDatabase.CreateAsset(catalog, DefaultAssetPath);
        return catalog;
    }

    private static void CopyList(List<GameObject> source, List<GameObject> target)
    {
        target.Clear();
        if (source == null || source.Count == 0)
            return;

        target.AddRange(source);
    }
}
