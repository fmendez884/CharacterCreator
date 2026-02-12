using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class FfxivRuntimeCatalogBuilder
{
    private const string ProcessedRoot = "Assets/FFXIV_Processed";
    private const string DefaultAssetPath = "Assets/Resources/FfxivRuntimeCatalog.asset";
    private static readonly string[] CategoryFolders = { "Hair", "Face", "Equipment", "Weapons" };

    private enum AddressableCategory
    {
        Hair,
        Face,
        Equipment,
        Weapons,
        Body
    }

    [MenuItem("Tools/FFXIV/Catalog/Build Runtime Catalog")]
    public static void BuildCatalog()
    {
        BuildCatalog(showDialog: true);
    }

    public static bool BuildCatalog(bool showDialog)
    {
        var catalog = LoadOrCreateCatalog();
        if (catalog == null)
            return false;

        catalog.ClearAll();

        bool hasAddressables = BuildFromAddressables(catalog);
        BuildFromProcessedPrefabs(catalog);
        BackfillKeysFromPrefabLists(catalog);
        catalog.SortAll();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        if (showDialog)
        {
            string mode = hasAddressables ? "Addressables + Processed Prefabs" : "Processed Prefabs (Addressables missing)";
            EditorUtility.DisplayDialog(
                "FFXIV Runtime Catalog",
                $"Runtime catalog built.\nSource: {mode}\nKey entries: {CountKeys(catalog)}",
                "OK"
            );
        }

        return true;
    }

    public static FfxivRuntimeCatalog LoadOrCreateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<FfxivRuntimeCatalog>(DefaultAssetPath);
        if (catalog != null)
            return catalog;

        var folder = Path.GetDirectoryName(DefaultAssetPath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        catalog = ScriptableObject.CreateInstance<FfxivRuntimeCatalog>();
        AssetDatabase.CreateAsset(catalog, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    private static bool BuildFromAddressables(FfxivRuntimeCatalog catalog)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[FFXIV] Addressables settings not found. Runtime catalog keys will be sourced from processed prefabs only.");
            return false;
        }

        AddAddressableCategory(settings, catalog.labelHair, catalog, AddressableCategory.Hair);
        AddAddressableCategory(settings, catalog.labelFace, catalog, AddressableCategory.Face);
        AddAddressableCategory(settings, catalog.labelEquipment, catalog, AddressableCategory.Equipment);
        AddAddressableCategory(settings, catalog.labelWeapons, catalog, AddressableCategory.Weapons);
        AddAddressableCategory(settings, catalog.labelBody, catalog, AddressableCategory.Body);
        return true;
    }

    private static void BuildFromProcessedPrefabs(FfxivRuntimeCatalog catalog)
    {
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
                    AddGenderedPrefab(catalog.maleHairPrefabs, catalog.femaleHairPrefabs, prefab, name, catalog.maleToken, catalog.femaleToken);
                    continue;
                }

                if (category.Equals("Face", StringComparison.OrdinalIgnoreCase))
                {
                    AddGenderedPrefab(catalog.maleFacePrefabs, catalog.femaleFacePrefabs, prefab, name, catalog.maleToken, catalog.femaleToken);
                    continue;
                }

                if (category.Equals("Weapons", StringComparison.OrdinalIgnoreCase))
                {
                    AddUnique(catalog.weaponPrefabs, prefab);
                    AddUnique(catalog.weapons, name);
                    continue;
                }

                bool isBase = ContainsToken(name, catalog.equipmentBaseToken);
                if (isBase)
                {
                    AddGenderedPrefab(catalog.maleBaseBodyPrefabs, catalog.femaleBaseBodyPrefabs, prefab, name, catalog.maleToken, catalog.femaleToken);
                    AddGenderedKey(catalog.maleBaseBodies, catalog.femaleBaseBodies, name, catalog.maleToken, catalog.femaleToken);
                    continue;
                }

                if (ContainsToken(name, catalog.headToken))
                {
                    AddGenderedPrefab(catalog.maleHeadPrefabs, catalog.femaleHeadPrefabs, prefab, name, catalog.maleToken, catalog.femaleToken);
                    continue;
                }

                if (ContainsToken(name, catalog.bodyToken))
                {
                    AddGenderedPrefab(catalog.maleBodyPrefabs, catalog.femaleBodyPrefabs, prefab, name, catalog.maleToken, catalog.femaleToken);
                    continue;
                }

                if (ContainsToken(name, catalog.handsToken))
                {
                    AddGenderedPrefab(catalog.maleHandsPrefabs, catalog.femaleHandsPrefabs, prefab, name, catalog.maleToken, catalog.femaleToken);
                    continue;
                }

                if (ContainsToken(name, catalog.legsToken))
                {
                    AddGenderedPrefab(catalog.maleLegsPrefabs, catalog.femaleLegsPrefabs, prefab, name, catalog.maleToken, catalog.femaleToken);
                    continue;
                }

                if (ContainsToken(name, catalog.feetToken))
                {
                    AddGenderedPrefab(catalog.maleFeetPrefabs, catalog.femaleFeetPrefabs, prefab, name, catalog.maleToken, catalog.femaleToken);
                    continue;
                }
            }
        }
    }

    private static void AddAddressableCategory(AddressableAssetSettings settings, string label, FfxivRuntimeCatalog catalog, AddressableCategory category)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        var entries = new List<AddressableAssetEntry>();
        settings.GetAllAssets(entries, includeSubObjects: false, entryFilter: entry => entry != null && entry.labels.Contains(label));
        foreach (var entry in entries)
        {
            if (entry == null || entry.IsFolder)
                continue;

            string address = entry.address;
            if (string.IsNullOrWhiteSpace(address))
                continue;

            switch (category)
            {
                case AddressableCategory.Hair:
                    AddGenderedKey(catalog.maleHair, catalog.femaleHair, address, catalog.maleToken, catalog.femaleToken);
                    break;
                case AddressableCategory.Face:
                    AddGenderedKey(catalog.maleFace, catalog.femaleFace, address, catalog.maleToken, catalog.femaleToken);
                    break;
                case AddressableCategory.Weapons:
                    AddUnique(catalog.weapons, address);
                    break;
                case AddressableCategory.Body:
                    if (!ContainsToken(address, catalog.equipmentBaseToken))
                        break;
                    AddGenderedKey(catalog.maleBaseBodies, catalog.femaleBaseBodies, address, catalog.maleToken, catalog.femaleToken);
                    break;
                case AddressableCategory.Equipment:
                    if (ContainsToken(address, catalog.equipmentBaseToken))
                        break;

                    if (ContainsToken(address, catalog.headToken))
                        AddGenderedKey(catalog.maleHead, catalog.femaleHead, address, catalog.maleToken, catalog.femaleToken);
                    else if (ContainsToken(address, catalog.bodyToken))
                        AddGenderedKey(catalog.maleBody, catalog.femaleBody, address, catalog.maleToken, catalog.femaleToken);
                    else if (ContainsToken(address, catalog.handsToken))
                        AddGenderedKey(catalog.maleHands, catalog.femaleHands, address, catalog.maleToken, catalog.femaleToken);
                    else if (ContainsToken(address, catalog.legsToken))
                        AddGenderedKey(catalog.maleLegs, catalog.femaleLegs, address, catalog.maleToken, catalog.femaleToken);
                    else if (ContainsToken(address, catalog.feetToken))
                        AddGenderedKey(catalog.maleFeet, catalog.femaleFeet, address, catalog.maleToken, catalog.femaleToken);
                    break;
            }
        }
    }

    private static void BackfillKeysFromPrefabLists(FfxivRuntimeCatalog catalog)
    {
        CopyPrefabNames(catalog.maleHairPrefabs, catalog.maleHair);
        CopyPrefabNames(catalog.femaleHairPrefabs, catalog.femaleHair);
        CopyPrefabNames(catalog.maleFacePrefabs, catalog.maleFace);
        CopyPrefabNames(catalog.femaleFacePrefabs, catalog.femaleFace);
        CopyPrefabNames(catalog.maleHeadPrefabs, catalog.maleHead);
        CopyPrefabNames(catalog.femaleHeadPrefabs, catalog.femaleHead);
        CopyPrefabNames(catalog.maleBodyPrefabs, catalog.maleBody);
        CopyPrefabNames(catalog.femaleBodyPrefabs, catalog.femaleBody);
        CopyPrefabNames(catalog.maleHandsPrefabs, catalog.maleHands);
        CopyPrefabNames(catalog.femaleHandsPrefabs, catalog.femaleHands);
        CopyPrefabNames(catalog.maleLegsPrefabs, catalog.maleLegs);
        CopyPrefabNames(catalog.femaleLegsPrefabs, catalog.femaleLegs);
        CopyPrefabNames(catalog.maleFeetPrefabs, catalog.maleFeet);
        CopyPrefabNames(catalog.femaleFeetPrefabs, catalog.femaleFeet);
        CopyPrefabNames(catalog.maleBaseBodyPrefabs, catalog.maleBaseBodies);
        CopyPrefabNames(catalog.femaleBaseBodyPrefabs, catalog.femaleBaseBodies);
        CopyPrefabNames(catalog.weaponPrefabs, catalog.weapons);
    }

    private static void CopyPrefabNames(List<GameObject> prefabs, List<string> keys)
    {
        if (prefabs == null || keys == null)
            return;

        foreach (var prefab in prefabs)
        {
            if (prefab == null)
                continue;

            AddUnique(keys, prefab.name);
        }
    }

    private static void AddGenderedKey(List<string> maleList, List<string> femaleList, string value, string maleToken, string femaleToken)
    {
        bool isMale = ContainsToken(value, maleToken);
        bool isFemale = ContainsToken(value, femaleToken);

        if (!isMale && !isFemale)
        {
            AddUnique(maleList, value);
            AddUnique(femaleList, value);
            return;
        }

        if (isMale)
            AddUnique(maleList, value);
        if (isFemale)
            AddUnique(femaleList, value);
    }

    private static void AddGenderedPrefab(List<GameObject> maleList, List<GameObject> femaleList, GameObject prefab, string name, string maleToken, string femaleToken)
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

    private static bool ContainsToken(string value, string token)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(token))
            return false;

        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !list.Contains(value))
            list.Add(value);
    }

    private static void AddUnique(List<GameObject> list, GameObject value)
    {
        if (value != null && !list.Contains(value))
            list.Add(value);
    }

    public static int CountKeys(FfxivRuntimeCatalog catalog)
    {
        if (catalog == null)
            return 0;

        return catalog.maleHair.Count + catalog.femaleHair.Count +
               catalog.maleFace.Count + catalog.femaleFace.Count +
               catalog.maleHead.Count + catalog.femaleHead.Count +
               catalog.maleBody.Count + catalog.femaleBody.Count +
               catalog.maleHands.Count + catalog.femaleHands.Count +
               catalog.maleLegs.Count + catalog.femaleLegs.Count +
               catalog.maleFeet.Count + catalog.femaleFeet.Count +
               catalog.maleBaseBodies.Count + catalog.femaleBaseBodies.Count +
               catalog.weapons.Count;
    }
}
