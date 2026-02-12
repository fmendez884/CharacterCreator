using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CharacterCreatorUI : MonoBehaviour
{
    [SerializeField] private CharacterCreator creator;

    [Header("Gender")]
    [SerializeField] private Button genderToggleButton;
    [SerializeField] private Text genderValueLabel;

    [Header("Filter")]
    [SerializeField] private InputField filterInput;

    [Header("Hair")]
    [SerializeField] private Button hairPrevButton;
    [SerializeField] private Button hairNextButton;
    [SerializeField] private Text hairLabel;

    [Header("Face")]
    [SerializeField] private Button facePrevButton;
    [SerializeField] private Button faceNextButton;
    [SerializeField] private Text faceLabel;

    private bool listenersHooked;
    private bool warnedMissingSelectionButtons;

    private void OnEnable()
    {
        NormalizeUiLayoutAndRaycasts();

        if (creator == null)
            creator = FindObjectOfType<CharacterCreator>();

        if (creator == null)
        {
            Debug.LogWarning("[CharacterCreatorUI] Missing CharacterCreator reference.", this);
            return;
        }

        EnsureSelectionButtonBindings();
        Hookup();
        creator.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (creator == null)
            return;

        creator.Changed -= Refresh;
        Unhook();
    }

    private void Hookup()
    {
        if (listenersHooked)
            return;

        if (genderToggleButton != null)
            genderToggleButton.onClick.AddListener(OnGenderToggle);

        if (filterInput != null)
            filterInput.onValueChanged.AddListener(OnFilterChanged);

        if (hairPrevButton != null)
            hairPrevButton.onClick.AddListener(OnHairPrev);
        if (hairNextButton != null)
            hairNextButton.onClick.AddListener(OnHairNext);

        if (facePrevButton != null)
            facePrevButton.onClick.AddListener(OnFacePrev);
        if (faceNextButton != null)
            faceNextButton.onClick.AddListener(OnFaceNext);

        listenersHooked = true;
    }

    private void Unhook()
    {
        if (!listenersHooked)
            return;

        if (genderToggleButton != null)
            genderToggleButton.onClick.RemoveListener(OnGenderToggle);

        if (filterInput != null)
            filterInput.onValueChanged.RemoveListener(OnFilterChanged);

        if (hairPrevButton != null)
            hairPrevButton.onClick.RemoveListener(OnHairPrev);
        if (hairNextButton != null)
            hairNextButton.onClick.RemoveListener(OnHairNext);

        if (facePrevButton != null)
            facePrevButton.onClick.RemoveListener(OnFacePrev);
        if (faceNextButton != null)
            faceNextButton.onClick.RemoveListener(OnFaceNext);

        listenersHooked = false;
    }

    private void OnGenderToggle()
    {
        if (creator == null)
            return;

        var next = creator.CurrentGender == CharacterCreator.Gender.Male
            ? CharacterCreator.Gender.Female
            : CharacterCreator.Gender.Male;
        creator.SetGender(next);
    }

    private void OnFilterChanged(string value)
    {
        creator.SetFilterText(value);
    }

    private void OnHairPrev()
    {
        creator.PreviousHairFiltered();
    }

    private void OnHairNext()
    {
        creator.NextHairFiltered();
    }

    private void OnFacePrev()
    {
        creator.PreviousFaceFiltered();
    }

    private void OnFaceNext()
    {
        creator.NextFaceFiltered();
    }

    private void Refresh()
    {
        if (creator == null)
            return;

        if (genderValueLabel != null)
            genderValueLabel.text = creator.CurrentGender == CharacterCreator.Gender.Male ? "Male" : "Female";

        if (filterInput != null && filterInput.text != creator.FilterText)
            filterInput.SetTextWithoutNotify(creator.FilterText);

        if (hairLabel != null)
        {
            int position = creator.GetFilteredPosition(CharacterCreator.Category.Hair);
            int count = creator.GetFilteredCount(CharacterCreator.Category.Hair);
            string name = creator.GetCurrentHairName();
            hairLabel.text = FormatLabel("Hair", position, count, name);
        }

        if (faceLabel != null)
        {
            int position = creator.GetFilteredPosition(CharacterCreator.Category.Face);
            int count = creator.GetFilteredCount(CharacterCreator.Category.Face);
            string name = creator.GetCurrentFaceName();
            faceLabel.text = FormatLabel("Face", position, count, name);
        }
    }

    private static string FormatLabel(string label, int index, int count, string name)
    {
        if (count <= 0)
            return $"{label}: None";

        if (string.IsNullOrWhiteSpace(name))
            return $"{label}: {index}/{count}";

        return $"{label}: {index}/{count} - {name}";
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

    private void EnsureSelectionButtonBindings()
    {
        hairPrevButton = ResolveButtonReference(hairPrevButton, "Panel/HairRow/HairPrevButton", "HairPrevButton");
        hairNextButton = ResolveButtonReference(hairNextButton, "Panel/HairRow/HairNextButton", "HairNextButton");
        facePrevButton = ResolveButtonReference(facePrevButton, "Panel/FaceRow/FacePrevButton", "FacePrevButton");
        faceNextButton = ResolveButtonReference(faceNextButton, "Panel/FaceRow/FaceNextButton", "FaceNextButton");

        var missing = new List<string>(4);
        if (hairPrevButton == null) missing.Add(nameof(hairPrevButton));
        if (hairNextButton == null) missing.Add(nameof(hairNextButton));
        if (facePrevButton == null) missing.Add(nameof(facePrevButton));
        if (faceNextButton == null) missing.Add(nameof(faceNextButton));

        if (missing.Count == 0 || warnedMissingSelectionButtons)
            return;

        warnedMissingSelectionButtons = true;
        Debug.LogWarning(
            $"[CharacterCreatorUI] Missing required hair/face button bindings: {string.Join(", ", missing)}.",
            this
        );
    }

    private Button ResolveButtonReference(Button current, string hierarchyPath, string fallbackName)
    {
        if (current != null)
            return current;

        var byPath = transform.Find(hierarchyPath);
        if (byPath != null && byPath.TryGetComponent<Button>(out var byPathButton))
            return byPathButton;

        var allButtons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < allButtons.Length; i++)
        {
            var button = allButtons[i];
            if (button != null && string.Equals(button.name, fallbackName, StringComparison.OrdinalIgnoreCase))
                return button;
        }

        return null;
    }
}
