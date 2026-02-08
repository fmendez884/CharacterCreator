using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class FfxivAddressablesConfigurator
{
    private const string ProcessedRoot = "Assets/FFXIV_Processed";
    private static readonly string[] CategoryFolders = { "Hair", "Face", "Equipment", "Weapons" };
    private const string BodyLabel = "Body";
    private const string EquipmentBaseToken = "e0000";

    [MenuItem("Tools/FFXIV/Addressables/Setup Groups (Processed Prefabs)")]
    public static void SetupGroups()
    {
        SetupGroups(showDialog: true);
    }

    public static int SetupGroups(bool showDialog)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[FFXIV] Addressables settings not found. Create Addressables settings first (Window > Asset Management > Addressables > Groups).");
            return 0;
        }

        var groupMap = new Dictionary<string, AddressableAssetGroup>();
        foreach (var category in CategoryFolders)
        {
            string groupName = $"FFXIV/{category}";
            var group = settings.FindGroup(groupName) ?? CreateGroup(settings, groupName);
            EnsureLabel(settings, category);
            EnsureLabel(settings, "FFXIV");
            EnsureLabel(settings, BodyLabel);
            groupMap[category] = group;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
        AssetDatabase.SaveAssets();

        int moved = 0;

        foreach (var category in CategoryFolders)
        {
            string categoryPath = $"{ProcessedRoot}/{category}";
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { categoryPath });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(categoryPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var entry = settings.CreateOrMoveEntry(guid, groupMap[category]);
                entry.address = System.IO.Path.GetFileNameWithoutExtension(path);
                entry.SetLabel(category, true, true);
                entry.SetLabel("FFXIV", true, true);
                if (category.Equals("Equipment", StringComparison.OrdinalIgnoreCase) &&
                    entry.address.IndexOf(EquipmentBaseToken, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    entry.SetLabel(BodyLabel, true, true);
                }
                moved++;
            }
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
        AssetDatabase.SaveAssets();

        if (showDialog)
            EditorUtility.DisplayDialog("FFXIV Addressables", $"Groups updated. Prefab entries updated: {moved}", "OK");
        return moved;
    }

    private static AddressableAssetGroup CreateGroup(AddressableAssetSettings settings, string name)
    {
        var group = settings.CreateGroup(name, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        var bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
        bundledSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;

        return group;
    }

    private static void EnsureLabel(AddressableAssetSettings settings, string label)
    {
        var labels = settings.GetLabels();
        if (!labels.Contains(label))
            settings.AddLabel(label);
    }
}
