using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FfxivPrefabCatalogBuilder
{
    private const string ProcessedRoot = "Assets/FFXIV_Processed";
    private const string DefaultAssetPath = "Assets/Resources/FfxivPrefabCatalog.asset";
    private static readonly string[] CategoryFolders = { "Hair", "Face", "Equipment", "Weapons" };

    [MenuItem("Tools/FFXIV/Prefabs/Build Prefab Catalog (Processed Prefabs)")]
    public static void BuildCatalog()
    {
        var catalog = LoadOrCreateCatalog();
        catalog.ClearLists();

        var maleToken = catalog.maleToken;
        var femaleToken = catalog.femaleToken;
        var hairToken = catalog.hairToken;
        var faceToken = catalog.faceToken;
        var baseToken = catalog.equipmentBaseToken;
        var headToken = catalog.headToken;
        var bodyToken = catalog.bodyToken;
        var handsToken = catalog.handsToken;
        var legsToken = catalog.legsToken;
        var feetToken = catalog.feetToken;

        int added = 0;

        foreach (var category in CategoryFolders)
        {
            string categoryPath = $"{ProcessedRoot}/{category}";
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { categoryPath });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(categoryPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                string name = prefab.name;

                if (category.Equals("Hair", StringComparison.OrdinalIgnoreCase))
                {
                    AddGendered(catalog.maleHair, catalog.femaleHair, prefab, name, maleToken, femaleToken);
                    added++;
                    continue;
                }

                if (category.Equals("Face", StringComparison.OrdinalIgnoreCase))
                {
                    AddGendered(catalog.maleFace, catalog.femaleFace, prefab, name, maleToken, femaleToken);
                    added++;
                    continue;
                }

                if (category.Equals("Weapons", StringComparison.OrdinalIgnoreCase))
                {
                    AddUnique(catalog.weapons, prefab);
                    added++;
                    continue;
                }

                // Equipment + Base Bodies
                bool isBase = ContainsToken(name, baseToken);
                bool isHead = ContainsToken(name, headToken);
                bool isBody = ContainsToken(name, bodyToken);
                bool isHands = ContainsToken(name, handsToken);
                bool isLegs = ContainsToken(name, legsToken);
                bool isFeet = ContainsToken(name, feetToken);

                if (isBase)
                {
                    AddGendered(catalog.maleBaseBodies, catalog.femaleBaseBodies, prefab, name, maleToken, femaleToken);
                    added++;
                    continue;
                }

                if (isHead)
                    AddGendered(catalog.maleHead, catalog.femaleHead, prefab, name, maleToken, femaleToken);
                else if (isBody)
                    AddGendered(catalog.maleBody, catalog.femaleBody, prefab, name, maleToken, femaleToken);
                else if (isHands)
                    AddGendered(catalog.maleHands, catalog.femaleHands, prefab, name, maleToken, femaleToken);
                else if (isLegs)
                    AddGendered(catalog.maleLegs, catalog.femaleLegs, prefab, name, maleToken, femaleToken);
                else if (isFeet)
                    AddGendered(catalog.maleFeet, catalog.femaleFeet, prefab, name, maleToken, femaleToken);
                else
                    continue;

                added++;
            }
        }

        SortAll(catalog);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("FFXIV Prefab Catalog", $"Prefab catalog built. Entries updated: {added}", "OK");
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

    private static bool ContainsToken(string value, string token)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(token))
            return false;
        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AddGendered(List<GameObject> maleList, List<GameObject> femaleList, GameObject prefab, string name, string maleToken, string femaleToken)
    {
        bool isMale = ContainsToken(name, maleToken);
        bool isFemale = ContainsToken(name, femaleToken);

        if (!isMale && !isFemale)
        {
            AddUnique(maleList, prefab);
            AddUnique(femaleList, prefab);
            return;
        }

        if (isMale)
            AddUnique(maleList, prefab);
        if (isFemale)
            AddUnique(femaleList, prefab);
    }

    private static void AddUnique(List<GameObject> list, GameObject prefab)
    {
        if (prefab != null && !list.Contains(prefab))
            list.Add(prefab);
    }

    private static void SortAll(FfxivPrefabCatalog catalog)
    {
        SortByName(catalog.maleHair);
        SortByName(catalog.femaleHair);
        SortByName(catalog.maleFace);
        SortByName(catalog.femaleFace);
        SortByName(catalog.maleHead);
        SortByName(catalog.maleBody);
        SortByName(catalog.maleHands);
        SortByName(catalog.maleLegs);
        SortByName(catalog.maleFeet);
        SortByName(catalog.femaleHead);
        SortByName(catalog.femaleBody);
        SortByName(catalog.femaleHands);
        SortByName(catalog.femaleLegs);
        SortByName(catalog.femaleFeet);
        SortByName(catalog.maleBaseBodies);
        SortByName(catalog.femaleBaseBodies);
        SortByName(catalog.weapons);
    }

    private static void SortByName(List<GameObject> list)
    {
        list.Sort((a, b) => string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty, StringComparison.OrdinalIgnoreCase));
    }
}
