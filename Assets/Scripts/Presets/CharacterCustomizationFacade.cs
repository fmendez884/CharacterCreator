using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterCustomizationFacade : MonoBehaviour
{
    [SerializeField] private CharacterCreator characterCreator;
    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private CharacterCreatorEquipmentBridge bridge;
    [SerializeField] private CharacterAvatarMount avatarMount;
    [SerializeField] private bool autoFindReferences = true;
    [SerializeField] private bool logWarnings = true;
    [SerializeField] private string defaultSlotId = "slot_a";

    private readonly HashSet<string> warnedUnknownKeys = new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        ResolveReferences();
    }

    public CharacterPresetData CapturePreset()
    {
        ResolveReferences();
        if (!HasRequiredSystems())
            return null;

        characterCreator.EnsureRuntimeReady();
        equipmentSystem.EnsureRuntimeReady();

        var data = new CharacterPresetData
        {
            version = CharacterPresetData.CurrentVersion,
            gender = characterCreator.CurrentGender.ToString(),
            hairKey = characterCreator.GetCurrentHairKey(),
            faceKey = characterCreator.GetCurrentFaceKey(),
            createdUtc = DateTime.UtcNow.ToString("O"),
            updatedUtc = DateTime.UtcNow.ToString("O")
        };

        foreach (EquipmentSystem.Slot slot in Enum.GetValues(typeof(EquipmentSystem.Slot)))
        {
            string slotName = slot.ToString();
            data.SetEquipmentKey(slotName, equipmentSystem.GetSelectedKey(slot));
            data.SetEquipmentEnabled(slotName, equipmentSystem.IsSlotEnabled(slot));
        }

        return data;
    }

    public void ApplyPreset(CharacterPresetData preset, bool invokeChanged = true)
    {
        if (preset == null)
        {
            Warn("ApplyPreset skipped because preset is null.");
            return;
        }

        ResolveReferences();
        if (!HasRequiredSystems())
            return;

        characterCreator.EnsureRuntimeReady();
        equipmentSystem.EnsureRuntimeReady();

        var creatorGender = ParseCreatorGenderOrDefault(preset.gender, characterCreator.CurrentGender);
        var equipmentGender = creatorGender == CharacterCreator.Gender.Male
            ? EquipmentSystem.Gender.Male
            : EquipmentSystem.Gender.Female;

        characterCreator.SetGender(creatorGender, applySelection: false);
        equipmentSystem.SetGender(equipmentGender, applySelection: false);

        if (!characterCreator.SetHairByKey(preset.hairKey, applySelection: false))
        {
            WarnUnknownKeyOnce("Hair", preset.hairKey);
            int fallback = characterCreator.GetHairCount(creatorGender) > 0 ? 0 : -1;
            characterCreator.SetHairIndex(fallback, applySelection: false);
        }

        if (!characterCreator.SetFaceByKey(preset.faceKey, applySelection: false))
        {
            WarnUnknownKeyOnce("Face", preset.faceKey);
            int fallback = characterCreator.GetFaceCount(creatorGender) > 0 ? 0 : -1;
            characterCreator.SetFaceIndex(fallback, applySelection: false);
        }

        for (int i = 0; i < CharacterPresetData.KnownSlots.Length; i++)
        {
            string slotName = CharacterPresetData.KnownSlots[i];
            if (!Enum.TryParse(slotName, ignoreCase: true, out EquipmentSystem.Slot slot))
                continue;

            string key = preset.GetEquipmentKey(slotName);
            bool slotResolved = equipmentSystem.SetSlotByKey(slot, key, applySelection: false);
            if (!slotResolved)
            {
                WarnUnknownKeyOnce(slotName, key);
                equipmentSystem.SetSlotByKey(slot, string.Empty, applySelection: false);
            }

            if (preset.TryGetEquipmentEnabled(slotName, out bool enabled))
            {
                if (!slotResolved && enabled && !string.IsNullOrWhiteSpace(key))
                    equipmentSystem.SetSlotEnabled(slot, enabled: false, applySelection: false);
                else
                    equipmentSystem.SetSlotEnabled(slot, enabled, applySelection: false);
            }
            else if (string.IsNullOrWhiteSpace(key))
                equipmentSystem.SetSlotEnabled(slot, enabled: false, applySelection: false);
        }

        equipmentSystem.ApplyCurrentSelection(invokeChanged);
        characterCreator.ApplyCurrentSelection(invokeChanged);

        if (bridge != null)
            bridge.Sync();

        if (invokeChanged && avatarMount != null)
            avatarMount.ReattachAvatar();
    }

    public void ResetToDefaults()
    {
        ResolveReferences();
        if (!HasRequiredSystems())
            return;

        var defaultCreatorGender = CharacterCreator.Gender.Male;
        var defaultEquipmentGender = EquipmentSystem.Gender.Male;

        characterCreator.SetGender(defaultCreatorGender, applySelection: false);
        equipmentSystem.SetGender(defaultEquipmentGender, applySelection: false);

        int hairDefault = characterCreator.GetHairCount(defaultCreatorGender) > 0 ? 0 : -1;
        int faceDefault = characterCreator.GetFaceCount(defaultCreatorGender) > 0 ? 0 : -1;
        characterCreator.SetHairIndex(hairDefault, applySelection: false);
        characterCreator.SetFaceIndex(faceDefault, applySelection: false);

        foreach (EquipmentSystem.Slot slot in Enum.GetValues(typeof(EquipmentSystem.Slot)))
        {
            equipmentSystem.SetSlotByKey(slot, string.Empty, applySelection: false);
            equipmentSystem.SetSlotEnabled(slot, enabled: false, applySelection: false);
        }

        equipmentSystem.ApplyCurrentSelection(invokeChanged: true);
        characterCreator.ApplyCurrentSelection(invokeChanged: true);

        if (bridge != null)
            bridge.Sync();
    }

    public bool SaveToSlot(string slotId = null)
    {
        var preset = CapturePreset();
        if (preset == null)
            return false;

        return CharacterPresetStore.Save(ResolveSlotId(slotId), preset);
    }

    public bool LoadFromSlot(string slotId = null, bool invokeChanged = true)
    {
        string resolvedSlot = ResolveSlotId(slotId);
        if (!CharacterPresetStore.TryLoad(resolvedSlot, out var preset))
        {
            Warn($"Load failed. Slot '{resolvedSlot}' does not exist or is invalid.");
            return false;
        }

        ApplyPreset(preset, invokeChanged);
        return true;
    }

    public void SaveSlotA()
    {
        SaveToSlot("slot_a");
    }

    public void LoadSlotA()
    {
        LoadFromSlot("slot_a", invokeChanged: true);
    }

    public void SaveSlotB()
    {
        SaveToSlot("slot_b");
    }

    public void LoadSlotB()
    {
        LoadFromSlot("slot_b", invokeChanged: true);
    }

    private void ResolveReferences()
    {
        if (!autoFindReferences)
            return;

        if (characterCreator == null)
            characterCreator = FindObjectOfType<CharacterCreator>();
        if (equipmentSystem == null)
            equipmentSystem = FindObjectOfType<EquipmentSystem>();
        if (bridge == null)
            bridge = FindObjectOfType<CharacterCreatorEquipmentBridge>();
        if (avatarMount == null)
            avatarMount = FindObjectOfType<CharacterAvatarMount>();
    }

    private bool HasRequiredSystems()
    {
        if (characterCreator != null && equipmentSystem != null)
            return true;

        Warn("Missing CharacterCreator or EquipmentSystem reference.");
        return false;
    }

    private string ResolveSlotId(string slotId)
    {
        return string.IsNullOrWhiteSpace(slotId) ? defaultSlotId : slotId;
    }

    private static CharacterCreator.Gender ParseCreatorGenderOrDefault(string value, CharacterCreator.Gender fallback)
    {
        if (Enum.TryParse(value, ignoreCase: true, out CharacterCreator.Gender parsed))
            return parsed;
        return fallback;
    }

    private void WarnUnknownKeyOnce(string label, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        string token = $"{label}:{key}";
        if (!warnedUnknownKeys.Add(token))
            return;

        Warn($"{label} key '{key}' was not found. Applied fallback.");
    }

    private void Warn(string message)
    {
        if (!logWarnings)
            return;

        Debug.LogWarning($"[CharacterCustomizationFacade] {message}", this);
    }
}
