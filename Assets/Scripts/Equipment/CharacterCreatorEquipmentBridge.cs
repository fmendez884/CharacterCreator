using UnityEngine;

[DisallowMultipleComponent]
public class CharacterCreatorEquipmentBridge : MonoBehaviour
{
    [SerializeField] private CharacterCreator characterCreator;
    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private bool syncOnEnable = true;
    [SerializeField] private bool syncBaseBodies = true;
    [SerializeField] private bool hideHairWhenHelmetEquipped = true;

    private void OnEnable()
    {
        if (!syncOnEnable)
            return;

        Sync();
        Hookup();
    }

    private void OnDisable()
    {
        Unhook();
    }

    public void Sync()
    {
        if (characterCreator == null || equipmentSystem == null)
            return;

        equipmentSystem.SetCharacterRoot(characterCreator.CharacterRoot);
        equipmentSystem.SetGender(characterCreator.CurrentGender == CharacterCreator.Gender.Male
            ? EquipmentSystem.Gender.Male
            : EquipmentSystem.Gender.Female);

        if (syncBaseBodies)
            characterCreator.SetManageBaseBodies(!equipmentSystem.ManageBaseBodies);

        SyncHairVisibility();
    }

    private void Hookup()
    {
        if (characterCreator != null)
            characterCreator.Changed += OnCharacterChanged;
        if (equipmentSystem != null)
            equipmentSystem.Changed += OnEquipmentChanged;
    }

    private void Unhook()
    {
        if (characterCreator != null)
            characterCreator.Changed -= OnCharacterChanged;
        if (equipmentSystem != null)
            equipmentSystem.Changed -= OnEquipmentChanged;
    }

    private void OnCharacterChanged()
    {
        if (characterCreator == null || equipmentSystem == null)
            return;

        equipmentSystem.SetCharacterRoot(characterCreator.CharacterRoot);
        equipmentSystem.SetGender(characterCreator.CurrentGender == CharacterCreator.Gender.Male
            ? EquipmentSystem.Gender.Male
            : EquipmentSystem.Gender.Female);

        if (syncBaseBodies)
            characterCreator.SetManageBaseBodies(!equipmentSystem.ManageBaseBodies);

        SyncHairVisibility();
    }

    private void OnEquipmentChanged()
    {
        SyncHairVisibility();
    }

    private void SyncHairVisibility()
    {
        if (characterCreator == null || equipmentSystem == null)
            return;

        if (characterCreator.LogSceneCleanup)
        {
            Debug.Log(
                $"[CharacterCreatorEquipmentBridge] SyncHairVisibility start. HideHairWhenHelmetEquipped={hideHairWhenHelmetEquipped} HelmetEquipped={equipmentSystem.IsSlotEquipped(EquipmentSystem.Slot.Head)}.",
                this
            );
        }

        if (!hideHairWhenHelmetEquipped)
        {
            characterCreator.SetHairVisibilityOverride(true);
            return;
        }

        bool helmetEquipped = equipmentSystem.IsSlotEquipped(EquipmentSystem.Slot.Head);
        characterCreator.SetHairVisibilityOverride(!helmetEquipped);
    }
}
