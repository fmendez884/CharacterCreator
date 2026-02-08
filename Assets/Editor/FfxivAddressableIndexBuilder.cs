using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
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
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[FFXIV] Addressables settings not found. Create Addressables settings first.");
            return false;
        }

        var index = LoadOrCreateIndex();
        index.ClearLists();

        AddCategory(settings, index.labelHair, index, AddressableCategory.Hair);
        AddCategory(settings, index.labelFace, index, AddressableCategory.Face);
        AddCategory(settings, index.labelEquipment, index, AddressableCategory.Equipment);
        AddCategory(settings, index.labelWeapons, index, AddressableCategory.Weapons);
        AddCategory(settings, index.labelBody, index, AddressableCategory.Body);

        SortAll(index);

        EditorUtility.SetDirty(index);
        AssetDatabase.SaveAssets();
        if (showDialog)
            EditorUtility.DisplayDialog("FFXIV Addressables", "Addressable index built.", "OK");
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

    private static void AddCategory(AddressableAssetSettings settings, string label, FfxivAddressableIndex index, AddressableCategory category)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        var entries = new List<AddressableAssetEntry>();
        settings.GetAllAssets(entries, includeSubObjects: false, entryFilter: entry => entry != null && entry.labels.Contains(label));
        foreach (var entry in entries)
        {
            if (entry == null || entry.IsFolder)
                continue;

            var address = entry.address;
            if (string.IsNullOrWhiteSpace(address))
                continue;

            switch (category)
            {
                case AddressableCategory.Hair:
                    AddGendered(index.maleHair, index.femaleHair, address, index.maleToken, index.femaleToken);
                    break;
                case AddressableCategory.Face:
                    AddGendered(index.maleFace, index.femaleFace, address, index.maleToken, index.femaleToken);
                    break;
                case AddressableCategory.Weapons:
                    AddUnique(index.weapons, address);
                    break;
                case AddressableCategory.Body:
                    if (!ContainsToken(address, index.equipmentBaseToken))
                        break;
                    AddGendered(index.maleBaseBodies, index.femaleBaseBodies, address, index.maleToken, index.femaleToken);
                    break;
                case AddressableCategory.Equipment:
                    if (ContainsToken(address, index.equipmentBaseToken))
                        break;

                    if (ContainsToken(address, index.headToken))
                        AddGendered(index.maleHead, index.femaleHead, address, index.maleToken, index.femaleToken);
                    else if (ContainsToken(address, index.bodyToken))
                        AddGendered(index.maleBody, index.femaleBody, address, index.maleToken, index.femaleToken);
                    else if (ContainsToken(address, index.handsToken))
                        AddGendered(index.maleHands, index.femaleHands, address, index.maleToken, index.femaleToken);
                    else if (ContainsToken(address, index.legsToken))
                        AddGendered(index.maleLegs, index.femaleLegs, address, index.maleToken, index.femaleToken);
                    else if (ContainsToken(address, index.feetToken))
                        AddGendered(index.maleFeet, index.femaleFeet, address, index.maleToken, index.femaleToken);
                    break;
            }
        }
    }

    private static void AddGendered(List<string> maleList, List<string> femaleList, string address, string maleToken, string femaleToken)
    {
        bool isMale = ContainsToken(address, maleToken);
        bool isFemale = ContainsToken(address, femaleToken);

        if (!isMale && !isFemale)
        {
            AddUnique(maleList, address);
            AddUnique(femaleList, address);
            return;
        }

        if (isMale)
            AddUnique(maleList, address);
        if (isFemale)
            AddUnique(femaleList, address);
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

    private static void SortAll(FfxivAddressableIndex index)
    {
        index.maleHair.Sort(StringComparer.OrdinalIgnoreCase);
        index.femaleHair.Sort(StringComparer.OrdinalIgnoreCase);
        index.maleFace.Sort(StringComparer.OrdinalIgnoreCase);
        index.femaleFace.Sort(StringComparer.OrdinalIgnoreCase);
        index.maleHead.Sort(StringComparer.OrdinalIgnoreCase);
        index.femaleHead.Sort(StringComparer.OrdinalIgnoreCase);
        index.maleBody.Sort(StringComparer.OrdinalIgnoreCase);
        index.femaleBody.Sort(StringComparer.OrdinalIgnoreCase);
        index.maleHands.Sort(StringComparer.OrdinalIgnoreCase);
        index.femaleHands.Sort(StringComparer.OrdinalIgnoreCase);
        index.maleLegs.Sort(StringComparer.OrdinalIgnoreCase);
        index.femaleLegs.Sort(StringComparer.OrdinalIgnoreCase);
        index.maleFeet.Sort(StringComparer.OrdinalIgnoreCase);
        index.femaleFeet.Sort(StringComparer.OrdinalIgnoreCase);
        index.maleBaseBodies.Sort(StringComparer.OrdinalIgnoreCase);
        index.femaleBaseBodies.Sort(StringComparer.OrdinalIgnoreCase);
        index.weapons.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private enum AddressableCategory
    {
        Hair,
        Face,
        Equipment,
        Weapons,
        Body
    }
}
