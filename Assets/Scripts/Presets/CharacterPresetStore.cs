using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CharacterPresetStore
{
    private const string RootFolderName = "character-presets";

    public static bool Save(string slotId, CharacterPresetData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[CharacterPresetStore] Save failed: data is null.");
            return false;
        }

        slotId = SanitizeSlotId(slotId);
        if (string.IsNullOrWhiteSpace(slotId))
        {
            Debug.LogWarning("[CharacterPresetStore] Save failed: slot id is empty.");
            return false;
        }

        try
        {
            EnsureStoreFolder();
            data.version = Mathf.Max(1, data.version);
            if (string.IsNullOrWhiteSpace(data.createdUtc))
                data.createdUtc = DateTime.UtcNow.ToString("O");
            data.updatedUtc = DateTime.UtcNow.ToString("O");

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(GetSlotPath(slotId), json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CharacterPresetStore] Save failed for slot '{slotId}': {ex.Message}");
            return false;
        }
    }

    public static bool TryLoad(string slotId, out CharacterPresetData data)
    {
        data = null;
        slotId = SanitizeSlotId(slotId);
        if (string.IsNullOrWhiteSpace(slotId))
            return false;

        string path = GetSlotPath(slotId);
        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            data = JsonUtility.FromJson<CharacterPresetData>(json);
            if (data == null)
                return false;

            data.version = Mathf.Max(1, data.version);
            data.equipmentKeys ??= new List<EquipmentKeyEntry>();
            data.equipmentEnabled ??= new List<EquipmentEnabledEntry>();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CharacterPresetStore] Load failed for slot '{slotId}': {ex.Message}");
            data = null;
            return false;
        }
    }

    public static IReadOnlyList<string> ListSlots()
    {
        var slots = new List<string>();
        string folder = GetStoreFolder();
        if (!Directory.Exists(folder))
            return slots;

        try
        {
            var files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                slots.Add(Path.GetFileNameWithoutExtension(file));
            }

            slots.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CharacterPresetStore] ListSlots failed: {ex.Message}");
        }

        return slots;
    }

    public static bool Delete(string slotId)
    {
        slotId = SanitizeSlotId(slotId);
        if (string.IsNullOrWhiteSpace(slotId))
            return false;

        string path = GetSlotPath(slotId);
        if (!File.Exists(path))
            return false;

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CharacterPresetStore] Delete failed for slot '{slotId}': {ex.Message}");
            return false;
        }
    }

    private static void EnsureStoreFolder()
    {
        string folder = GetStoreFolder();
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
    }

    private static string GetStoreFolder()
    {
        return Path.Combine(Application.persistentDataPath, RootFolderName);
    }

    private static string GetSlotPath(string slotId)
    {
        return Path.Combine(GetStoreFolder(), $"{slotId}.json");
    }

    private static string SanitizeSlotId(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return string.Empty;

        slotId = slotId.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
            slotId = slotId.Replace(invalid[i], '_');

        return slotId;
    }
}
