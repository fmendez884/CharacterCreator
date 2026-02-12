using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CharacterPresetQuickActionsUI : MonoBehaviour
{
    [SerializeField] private CharacterCustomizationFacade facade;
    [SerializeField] private Button saveSlotAButton;
    [SerializeField] private Button loadSlotAButton;
    [SerializeField] private Button saveSlotBButton;
    [SerializeField] private Button loadSlotBButton;
    [SerializeField] private Text statusLabel;

    private bool listenersHooked;

    private void OnEnable()
    {
        if (facade == null)
            facade = FindObjectOfType<CharacterCustomizationFacade>();

        Hook();
        SetStatus(string.Empty);
    }

    private void OnDisable()
    {
        Unhook();
    }

    private void Hook()
    {
        if (listenersHooked)
            return;

        if (saveSlotAButton != null)
            saveSlotAButton.onClick.AddListener(OnSaveSlotA);
        if (loadSlotAButton != null)
            loadSlotAButton.onClick.AddListener(OnLoadSlotA);
        if (saveSlotBButton != null)
            saveSlotBButton.onClick.AddListener(OnSaveSlotB);
        if (loadSlotBButton != null)
            loadSlotBButton.onClick.AddListener(OnLoadSlotB);

        listenersHooked = true;
    }

    private void Unhook()
    {
        if (!listenersHooked)
            return;

        if (saveSlotAButton != null)
            saveSlotAButton.onClick.RemoveListener(OnSaveSlotA);
        if (loadSlotAButton != null)
            loadSlotAButton.onClick.RemoveListener(OnLoadSlotA);
        if (saveSlotBButton != null)
            saveSlotBButton.onClick.RemoveListener(OnSaveSlotB);
        if (loadSlotBButton != null)
            loadSlotBButton.onClick.RemoveListener(OnLoadSlotB);

        listenersHooked = false;
    }

    private void OnSaveSlotA()
    {
        bool ok = facade != null && facade.SaveToSlot("slot_a");
        SetStatus(ok ? "Saved Slot A" : "Save Slot A failed");
    }

    private void OnLoadSlotA()
    {
        bool ok = facade != null && facade.LoadFromSlot("slot_a", invokeChanged: true);
        SetStatus(ok ? "Loaded Slot A" : "Load Slot A failed");
    }

    private void OnSaveSlotB()
    {
        bool ok = facade != null && facade.SaveToSlot("slot_b");
        SetStatus(ok ? "Saved Slot B" : "Save Slot B failed");
    }

    private void OnLoadSlotB()
    {
        bool ok = facade != null && facade.LoadFromSlot("slot_b", invokeChanged: true);
        SetStatus(ok ? "Loaded Slot B" : "Load Slot B failed");
    }

    private void SetStatus(string value)
    {
        if (statusLabel != null)
            statusLabel.text = value ?? string.Empty;
    }
}
