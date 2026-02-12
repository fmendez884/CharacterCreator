using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAvatarMount : MonoBehaviour
{
    [SerializeField] private CharacterCreator characterCreator;
    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private CharacterCreatorEquipmentBridge bridge;
    [SerializeField] private CharacterAvatarHostAdapter hostAdapter;
    [SerializeField] private bool autoFindReferences = true;
    [SerializeField] private bool syncOnEnable = true;
    [SerializeField] private bool logWarnings = true;

    private bool warnedMissingRefs;

    private void OnEnable()
    {
        if (!syncOnEnable)
            return;

        ReattachAvatar();
    }

    public void ReattachAvatar()
    {
        ResolveReferences();

        if (characterCreator == null || equipmentSystem == null || hostAdapter == null)
        {
            WarnMissingReferences();
            return;
        }

        characterCreator.EnsureRuntimeReady();
        equipmentSystem.EnsureRuntimeReady();

        Transform avatarRoot = characterCreator.CharacterRoot;
        if (avatarRoot == null)
        {
            Warn("CharacterCreator.CharacterRoot is null, cannot attach avatar.");
            return;
        }

        hostAdapter.AttachAvatar(avatarRoot);
        equipmentSystem.SetCharacterRoot(avatarRoot);

        if (bridge != null)
            bridge.Sync();
        else
            equipmentSystem.SetGender(
                characterCreator.CurrentGender == CharacterCreator.Gender.Male
                    ? EquipmentSystem.Gender.Male
                    : EquipmentSystem.Gender.Female,
                applySelection: false
            );

        equipmentSystem.ApplyCurrentSelection();
        characterCreator.ApplyCurrentSelection();
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
        if (hostAdapter == null)
            hostAdapter = FindObjectOfType<CharacterAvatarHostAdapter>();
    }

    private void WarnMissingReferences()
    {
        if (warnedMissingRefs)
            return;

        warnedMissingRefs = true;
        Warn(
            "Missing required references. CharacterAvatarMount needs CharacterCreator, EquipmentSystem, and CharacterAvatarHostAdapter."
        );
    }

    private void Warn(string message)
    {
        if (!logWarnings)
            return;

        Debug.LogWarning($"[CharacterAvatarMount] {message}", this);
    }
}
