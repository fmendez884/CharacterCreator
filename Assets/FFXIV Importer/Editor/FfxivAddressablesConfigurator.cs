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
    private const string GroupPrefix = "FFXIV-";

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
        var consolidationWarnings = new List<string>();
        foreach (var category in CategoryFolders)
        {
            var group = GetOrCreateCanonicalGroup(settings, category, consolidationWarnings);
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

        for (int i = 0; i < consolidationWarnings.Count; i++)
            Debug.LogWarning(consolidationWarnings[i]);

        if (showDialog)
        {
            string extra = consolidationWarnings.Count > 0
                ? $"\nConsolidated duplicate groups: {consolidationWarnings.Count}"
                : string.Empty;
            EditorUtility.DisplayDialog("FFXIV Addressables", $"Groups updated. Prefab entries updated: {moved}{extra}", "OK");
        }
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

    private static AddressableAssetGroup GetOrCreateCanonicalGroup(AddressableAssetSettings settings, string category, List<string> warnings)
    {
        string canonicalName = GetCanonicalGroupName(category);
        var relatedGroups = FindRelatedGroups(settings, category);

        AddressableAssetGroup canonical = settings.FindGroup(canonicalName);
        if (canonical == null)
            canonical = CreateGroup(settings, canonicalName);

        for (int i = 0; i < relatedGroups.Count; i++)
        {
            var group = relatedGroups[i];
            if (group == null || group == canonical)
                continue;

            MoveEntriesToGroup(settings, group, canonical);
            warnings?.Add($"[FFXIV] Consolidated duplicate Addressables group '{group.Name}' into '{canonical.Name}'.");
            settings.RemoveGroup(group);
        }

        return canonical;
    }

    private static string GetCanonicalGroupName(string category) => $"{GroupPrefix}{category}";

    private static List<AddressableAssetGroup> FindRelatedGroups(AddressableAssetSettings settings, string category)
    {
        var groups = new List<AddressableAssetGroup>();
        if (settings?.groups == null)
            return groups;

        string canonical = GetCanonicalGroupName(category);
        string legacy = $"FFXIV/{category}";

        for (int i = 0; i < settings.groups.Count; i++)
        {
            var group = settings.groups[i];
            if (group == null || string.IsNullOrWhiteSpace(group.Name))
                continue;

            if (group.Name.StartsWith(canonical, StringComparison.OrdinalIgnoreCase) ||
                group.Name.StartsWith(legacy, StringComparison.OrdinalIgnoreCase))
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    private static void MoveEntriesToGroup(AddressableAssetSettings settings, AddressableAssetGroup from, AddressableAssetGroup to)
    {
        if (settings == null || from == null || to == null || from == to)
            return;

        var entries = new List<AddressableAssetEntry>(from.entries);
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.guid))
                continue;

            settings.CreateOrMoveEntry(entry.guid, to);
        }
    }
}
