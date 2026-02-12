using System;
using System.Collections.Generic;

[Serializable]
public sealed class CharacterPresetData
{
    public const int CurrentVersion = 1;
    public static readonly string[] KnownSlots = { "Head", "Body", "Hands", "Legs", "Feet", "Weapon" };

    public int version = CurrentVersion;
    public string gender = "Male";
    public string hairKey = string.Empty;
    public string faceKey = string.Empty;
    public List<EquipmentKeyEntry> equipmentKeys = new();
    public List<EquipmentEnabledEntry> equipmentEnabled = new();
    public string createdUtc = string.Empty;
    public string updatedUtc = string.Empty;

    public string GetEquipmentKey(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName) || equipmentKeys == null)
            return string.Empty;

        for (int i = 0; i < equipmentKeys.Count; i++)
        {
            var entry = equipmentKeys[i];
            if (entry == null)
                continue;
            if (string.Equals(entry.slot, slotName, StringComparison.OrdinalIgnoreCase))
                return entry.key ?? string.Empty;
        }

        return string.Empty;
    }

    public void SetEquipmentKey(string slotName, string key)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            return;

        if (equipmentKeys == null)
            equipmentKeys = new List<EquipmentKeyEntry>();

        for (int i = 0; i < equipmentKeys.Count; i++)
        {
            var entry = equipmentKeys[i];
            if (entry == null)
                continue;
            if (!string.Equals(entry.slot, slotName, StringComparison.OrdinalIgnoreCase))
                continue;

            entry.key = key ?? string.Empty;
            return;
        }

        equipmentKeys.Add(new EquipmentKeyEntry
        {
            slot = slotName,
            key = key ?? string.Empty
        });
    }

    public bool TryGetEquipmentEnabled(string slotName, out bool enabled)
    {
        enabled = false;
        if (string.IsNullOrWhiteSpace(slotName) || equipmentEnabled == null)
            return false;

        for (int i = 0; i < equipmentEnabled.Count; i++)
        {
            var entry = equipmentEnabled[i];
            if (entry == null)
                continue;
            if (!string.Equals(entry.slot, slotName, StringComparison.OrdinalIgnoreCase))
                continue;

            enabled = entry.enabled;
            return true;
        }

        return false;
    }

    public void SetEquipmentEnabled(string slotName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            return;

        if (equipmentEnabled == null)
            equipmentEnabled = new List<EquipmentEnabledEntry>();

        for (int i = 0; i < equipmentEnabled.Count; i++)
        {
            var entry = equipmentEnabled[i];
            if (entry == null)
                continue;
            if (!string.Equals(entry.slot, slotName, StringComparison.OrdinalIgnoreCase))
                continue;

            entry.enabled = enabled;
            return;
        }

        equipmentEnabled.Add(new EquipmentEnabledEntry
        {
            slot = slotName,
            enabled = enabled
        });
    }
}

[Serializable]
public sealed class EquipmentKeyEntry
{
    public string slot = string.Empty;
    public string key = string.Empty;
}

[Serializable]
public sealed class EquipmentEnabledEntry
{
    public string slot = string.Empty;
    public bool enabled;
}
