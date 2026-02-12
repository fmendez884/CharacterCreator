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
    [SerializeField] private Button headToggleButton;
    [SerializeField] private Text headLabel;

    [Header("Body")]
    [SerializeField] private Button bodyPrevButton;
    [SerializeField] private Button bodyNextButton;
    [SerializeField] private Button bodyToggleButton;
    [SerializeField] private Text bodyLabel;

    [Header("Hands")]
    [SerializeField] private Button handsPrevButton;
    [SerializeField] private Button handsNextButton;
    [SerializeField] private Button handsToggleButton;
    [SerializeField] private Text handsLabel;

    [Header("Legs")]
    [SerializeField] private Button legsPrevButton;
    [SerializeField] private Button legsNextButton;
    [SerializeField] private Button legsToggleButton;
    [SerializeField] private Text legsLabel;

    [Header("Feet")]
    [SerializeField] private Button feetPrevButton;
    [SerializeField] private Button feetNextButton;
    [SerializeField] private Button feetToggleButton;
    [SerializeField] private Text feetLabel;

    [Header("Weapon")]
    [SerializeField] private Button weaponPrevButton;
    [SerializeField] private Button weaponNextButton;
    [SerializeField] private Button weaponToggleButton;
    [SerializeField] private Text weaponLabel;

    private void OnEnable()
    {
        NormalizeUiLayoutAndRaycasts();

        if (equipment == null)
            equipment = FindObjectOfType<EquipmentSystem>();

        if (equipment == null)
        {
            Debug.LogWarning("[EquipmentSelectionUI] Missing EquipmentSystem reference.", this);
            return;
        }

        Hookup();
        equipment.Changed += Refresh;

        if (genderToggleButton != null && genderToggleButton.transform.IsChildOf(transform))
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
        if (headToggleButton != null)
            headToggleButton.onClick.AddListener(OnHeadToggle);

        if (bodyPrevButton != null)
            bodyPrevButton.onClick.AddListener(OnBodyPrev);
        if (bodyNextButton != null)
            bodyNextButton.onClick.AddListener(OnBodyNext);
        if (bodyToggleButton != null)
            bodyToggleButton.onClick.AddListener(OnBodyToggle);

        if (handsPrevButton != null)
            handsPrevButton.onClick.AddListener(OnHandsPrev);
        if (handsNextButton != null)
            handsNextButton.onClick.AddListener(OnHandsNext);
        if (handsToggleButton != null)
            handsToggleButton.onClick.AddListener(OnHandsToggle);

        if (legsPrevButton != null)
            legsPrevButton.onClick.AddListener(OnLegsPrev);
        if (legsNextButton != null)
            legsNextButton.onClick.AddListener(OnLegsNext);
        if (legsToggleButton != null)
            legsToggleButton.onClick.AddListener(OnLegsToggle);

        if (feetPrevButton != null)
            feetPrevButton.onClick.AddListener(OnFeetPrev);
        if (feetNextButton != null)
            feetNextButton.onClick.AddListener(OnFeetNext);
        if (feetToggleButton != null)
            feetToggleButton.onClick.AddListener(OnFeetToggle);

        if (weaponPrevButton != null)
            weaponPrevButton.onClick.AddListener(OnWeaponPrev);
        if (weaponNextButton != null)
            weaponNextButton.onClick.AddListener(OnWeaponNext);
        if (weaponToggleButton != null)
            weaponToggleButton.onClick.AddListener(OnWeaponToggle);
    }

    private void Unhook()
    {
        if (headPrevButton != null)
            headPrevButton.onClick.RemoveListener(OnHeadPrev);
        if (headNextButton != null)
            headNextButton.onClick.RemoveListener(OnHeadNext);
        if (headToggleButton != null)
            headToggleButton.onClick.RemoveListener(OnHeadToggle);

        if (bodyPrevButton != null)
            bodyPrevButton.onClick.RemoveListener(OnBodyPrev);
        if (bodyNextButton != null)
            bodyNextButton.onClick.RemoveListener(OnBodyNext);
        if (bodyToggleButton != null)
            bodyToggleButton.onClick.RemoveListener(OnBodyToggle);

        if (handsPrevButton != null)
            handsPrevButton.onClick.RemoveListener(OnHandsPrev);
        if (handsNextButton != null)
            handsNextButton.onClick.RemoveListener(OnHandsNext);
        if (handsToggleButton != null)
            handsToggleButton.onClick.RemoveListener(OnHandsToggle);

        if (legsPrevButton != null)
            legsPrevButton.onClick.RemoveListener(OnLegsPrev);
        if (legsNextButton != null)
            legsNextButton.onClick.RemoveListener(OnLegsNext);
        if (legsToggleButton != null)
            legsToggleButton.onClick.RemoveListener(OnLegsToggle);

        if (feetPrevButton != null)
            feetPrevButton.onClick.RemoveListener(OnFeetPrev);
        if (feetNextButton != null)
            feetNextButton.onClick.RemoveListener(OnFeetNext);
        if (feetToggleButton != null)
            feetToggleButton.onClick.RemoveListener(OnFeetToggle);

        if (weaponPrevButton != null)
            weaponPrevButton.onClick.RemoveListener(OnWeaponPrev);
        if (weaponNextButton != null)
            weaponNextButton.onClick.RemoveListener(OnWeaponNext);
        if (weaponToggleButton != null)
            weaponToggleButton.onClick.RemoveListener(OnWeaponToggle);
    }

    private void OnHeadPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Head);
    private void OnHeadNext() => equipment?.NextSlot(EquipmentSystem.Slot.Head);
    private void OnHeadToggle() => equipment?.ToggleSlotEnabled(EquipmentSystem.Slot.Head);

    private void OnBodyPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Body);
    private void OnBodyNext() => equipment?.NextSlot(EquipmentSystem.Slot.Body);
    private void OnBodyToggle() => equipment?.ToggleSlotEnabled(EquipmentSystem.Slot.Body);

    private void OnHandsPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Hands);
    private void OnHandsNext() => equipment?.NextSlot(EquipmentSystem.Slot.Hands);
    private void OnHandsToggle() => equipment?.ToggleSlotEnabled(EquipmentSystem.Slot.Hands);

    private void OnLegsPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Legs);
    private void OnLegsNext() => equipment?.NextSlot(EquipmentSystem.Slot.Legs);
    private void OnLegsToggle() => equipment?.ToggleSlotEnabled(EquipmentSystem.Slot.Legs);

    private void OnFeetPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Feet);
    private void OnFeetNext() => equipment?.NextSlot(EquipmentSystem.Slot.Feet);
    private void OnFeetToggle() => equipment?.ToggleSlotEnabled(EquipmentSystem.Slot.Feet);

    private void OnWeaponPrev() => equipment?.PreviousSlot(EquipmentSystem.Slot.Weapon);
    private void OnWeaponNext() => equipment?.NextSlot(EquipmentSystem.Slot.Weapon);
    private void OnWeaponToggle() => equipment?.ToggleSlotEnabled(EquipmentSystem.Slot.Weapon);

    private void Refresh()
    {
        if (equipment == null)
            return;

        if (genderValueLabel != null)
            genderValueLabel.text = equipment.CurrentGender == EquipmentSystem.Gender.Male ? "Male" : "Female";

        if (headLabel != null)
            headLabel.text = FormatLabel("Head", EquipmentSystem.Slot.Head);
        UpdateToggleButton(headToggleButton, EquipmentSystem.Slot.Head);
        if (bodyLabel != null)
            bodyLabel.text = FormatLabel("Body", EquipmentSystem.Slot.Body);
        UpdateToggleButton(bodyToggleButton, EquipmentSystem.Slot.Body);
        if (handsLabel != null)
            handsLabel.text = FormatLabel("Hands", EquipmentSystem.Slot.Hands);
        UpdateToggleButton(handsToggleButton, EquipmentSystem.Slot.Hands);
        if (legsLabel != null)
            legsLabel.text = FormatLabel("Legs", EquipmentSystem.Slot.Legs);
        UpdateToggleButton(legsToggleButton, EquipmentSystem.Slot.Legs);
        if (feetLabel != null)
            feetLabel.text = FormatLabel("Feet", EquipmentSystem.Slot.Feet);
        UpdateToggleButton(feetToggleButton, EquipmentSystem.Slot.Feet);
        if (weaponLabel != null)
            weaponLabel.text = FormatLabel("Weapon", EquipmentSystem.Slot.Weapon);
        UpdateToggleButton(weaponToggleButton, EquipmentSystem.Slot.Weapon);
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

    private void UpdateToggleButton(Button button, EquipmentSystem.Slot slot)
    {
        if (button == null || equipment == null)
            return;

        bool enabled = equipment.IsSlotEnabled(slot);
        button.interactable = equipment.GetSlotCount(slot) > 0;
        SetButtonText(button, enabled ? "On" : "Off");
    }

    private static void SetButtonText(Button button, string value)
    {
        if (button == null)
            return;

        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
            label.text = value;
    }

    private void NormalizeUiLayoutAndRaycasts()
    {
        if (transform.Find("Panel") is RectTransform panel)
        {
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = Vector2.zero;
        }

        var texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                texts[i].raycastTarget = false;
        }

        var images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null)
                continue;

            if (image.GetComponent<Button>() != null)
                continue;
            if (image.GetComponent<Toggle>() != null)
                continue;
            if (image.GetComponent<InputField>() != null)
                continue;

            image.raycastTarget = false;
        }
    }
}
