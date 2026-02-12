using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EquipmentSelectionUI : MonoBehaviour
{
    [SerializeField] private EquipmentSystem equipment;

    [Header("Gender")]
    [SerializeField] private Button genderToggleButton;
    [SerializeField] private Text genderValueLabel;

    [Header("Head")]
    [SerializeField] private Button headPrevButton;
    [SerializeField] private Button headNextButton;
    [SerializeField] private Text headLabel;

    [Header("Body")]
    [SerializeField] private Button bodyPrevButton;
    [SerializeField] private Button bodyNextButton;
    [SerializeField] private Text bodyLabel;

    [Header("Hands")]
    [SerializeField] private Button handsPrevButton;
    [SerializeField] private Button handsNextButton;
    [SerializeField] private Text handsLabel;

    [Header("Legs")]
    [SerializeField] private Button legsPrevButton;
    [SerializeField] private Button legsNextButton;
    [SerializeField] private Text legsLabel;

    [Header("Feet")]
    [SerializeField] private Button feetPrevButton;
    [SerializeField] private Button feetNextButton;
    [SerializeField] private Text feetLabel;

    [Header("Weapon")]
    [SerializeField] private Button weaponPrevButton;
    [SerializeField] private Button weaponNextButton;
    [SerializeField] private Text weaponLabel;

    private void OnEnable()
    {
        if (equipment == null)
            return;

        Hookup();
        equipment.Changed += Refresh;

        if (genderToggleButton != null)
            genderToggleButton.gameObject.SetActive(false);

        Refresh();
    }

    private void OnDisable()
    {
        if (equipment == null)
            return;

        equipment.Changed -= Refresh;
        Unhook();
    }

    private void Hookup()
    {
        if (headPrevButton != null)
            headPrevButton.onClick.AddListener(OnHeadPrev);
        if (headNextButton != null)
            headNextButton.onClick.AddListener(OnHeadNext);

        if (bodyPrevButton != null)
            bodyPrevButton.onClick.AddListener(OnBodyPrev);
        if (bodyNextButton != null)
            bodyNextButton.onClick.AddListener(OnBodyNext);

        if (handsPrevButton != null)
            handsPrevButton.onClick.AddListener(OnHandsPrev);
        if (handsNextButton != null)
            handsNextButton.onClick.AddListener(OnHandsNext);

        if (legsPrevButton != null)
            legsPrevButton.onClick.AddListener(OnLegsPrev);
        if (legsNextButton != null)
            legsNextButton.onClick.AddListener(OnLegsNext);

        if (feetPrevButton != null)
            feetPrevButton.onClick.AddListener(OnFeetPrev);
        if (feetNextButton != null)
            feetNextButton.onClick.AddListener(OnFeetNext);

        if (weaponPrevButton != null)
            weaponPrevButton.onClick.AddListener(OnWeaponPrev);
        if (weaponNextButton != null)
            weaponNextButton.onClick.AddListener(OnWeaponNext);
    }

    private void Unhook()
    {
        if (headPrevButton != null)
            headPrevButton.onClick.RemoveListener(OnHeadPrev);
        if (headNextButton != null)
            headNextButton.onClick.RemoveListener(OnHeadNext);

        if (bodyPrevButton != null)
            bodyPrevButton.onClick.RemoveListener(OnBodyPrev);
        if (bodyNextButton != null)
            bodyNextButton.onClick.RemoveListener(OnBodyNext);

        if (handsPrevButton != null)
            handsPrevButton.onClick.RemoveListener(OnHandsPrev);
        if (handsNextButton != null)
            handsNextButton.onClick.RemoveListener(OnHandsNext);

        if (legsPrevButton != null)
            legsPrevButton.onClick.RemoveListener(OnLegsPrev);
        if (legsNextButton != null)
            legsNextButton.onClick.RemoveListener(OnLegsNext);

        if (feetPrevButton != null)
            feetPrevButton.onClick.RemoveListener(OnFeetPrev);
        if (feetNextButton != null)
            feetNextButton.onClick.RemoveListener(OnFeetNext);

        if (weaponPrevButton != null)
            weaponPrevButton.onClick.RemoveListener(OnWeaponPrev);
        if (weaponNextButton != null)
            weaponNextButton.onClick.RemoveListener(OnWeaponNext);
    }

    private void OnHeadPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Head);
    private void OnHeadNext() => equipment?.NextSlot(EquipmentSystem.Slot.Head);

    private void OnBodyPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Body);
    private void OnBodyNext() => equipment?.NextSlot(EquipmentSystem.Slot.Body);

    private void OnHandsPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Hands);
    private void OnHandsNext() => equipment?.NextSlot(EquipmentSystem.Slot.Hands);

    private void OnLegsPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Legs);
    private void OnLegsNext() => equipment?.NextSlot(EquipmentSystem.Slot.Legs);

    private void OnFeetPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Feet);
    private void OnFeetNext() => equipment?.NextSlot(EquipmentSystem.Slot.Feet);

    private void OnWeaponPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Weapon);
    private void OnWeaponNext() => equipment?.NextSlot(EquipmentSystem.Slot.Weapon);

    private void Refresh()
    {
        if (equipment == null)
            return;

        if (genderValueLabel != null)
            genderValueLabel.text = equipment.CurrentGender == EquipmentSystem.Gender.Male ? "Male" : "Female";

        if (headLabel != null)
            headLabel.text = FormatLabel("Head", EquipmentSystem.Slot.Head);
        if (bodyLabel != null)
            bodyLabel.text = FormatLabel("Body", EquipmentSystem.Slot.Body);
        if (handsLabel != null)
            handsLabel.text = FormatLabel("Hands", EquipmentSystem.Slot.Hands);
        if (legsLabel != null)
            legsLabel.text = FormatLabel("Legs", EquipmentSystem.Slot.Legs);
        if (feetLabel != null)
            feetLabel.text = FormatLabel("Feet", EquipmentSystem.Slot.Feet);
        if (weaponLabel != null)
            weaponLabel.text = FormatLabel("Weapon", EquipmentSystem.Slot.Weapon);
    }

    private string FormatLabel(string label, EquipmentSystem.Slot slot)
    {
        int position = equipment.GetSlotPosition(slot);
        int count = equipment.GetSlotCount(slot);
        string name = equipment.GetCurrentItemName(slot);

        if (count <= 0 || !equipment.IsSlotEquipped(slot))
            return $"{label}: None";

        if (string.IsNullOrWhiteSpace(name))
            return $"{label}: {position}/{count}";

        return $"{label}: {position}/{count} - {name}";
    }
}
