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

    private void OnEnable()
    {
        if (creator == null)
            return;

        Hookup();
        creator.RebuildFromScene();
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
    }

    private void Unhook()
    {
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
}
