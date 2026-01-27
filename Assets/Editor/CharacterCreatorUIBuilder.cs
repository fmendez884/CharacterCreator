using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CharacterCreatorUIBuilder
{
    private static Sprite fallbackSprite;
    private static readonly Color ButtonNormal = new Color(0.95f, 0.95f, 0.95f, 1f);
    private static readonly Color ButtonHighlight = new Color(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Color ButtonSelected = new Color(0.78f, 0.78f, 0.78f, 1f);

    [MenuItem("Tools/FFXIV/Create Character Creator UI")]
    public static void CreateCharacterCreatorUI()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        var creator = Object.FindObjectOfType<CharacterCreator>();
        if (creator == null)
        {
            var creatorGo = new GameObject("CharacterCreator");
            Undo.RegisterCreatedObjectUndo(creatorGo, "Create CharacterCreator");
            creator = creatorGo.AddComponent<CharacterCreator>();
        }

        var root = new GameObject("CharacterCreatorUI", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Character Creator UI");
        root.transform.SetParent(canvas.transform, false);

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(150f, -100f);

        Sprite fallback = GetOrCreateFallbackSprite();
        Sprite backgroundSprite = fallback;
        Sprite checkmarkSprite = fallback;
        Sprite buttonSprite = GetButtonSprite(fallback);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(root.transform, false);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(360f, 0f);

        var panelImage = panel.GetComponent<Image>();
        ApplySprite(panelImage, backgroundSprite);
        panelImage.color = new Color(0f, 0f, 0f, 0.6f);

        var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(12, 12, 12, 12);
        panelLayout.spacing = 8;
        panelLayout.childAlignment = TextAnchor.UpperLeft;
        panelLayout.childControlHeight = true;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = true;

        var panelFitter = panel.GetComponent<ContentSizeFitter>();
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = CreateText(panel.transform, "Title", "Character Creator", 18, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

        var genderRow = CreateRow(panel.transform, "GenderRow");
        CreateText(genderRow.transform, "GenderLabel", "Gender", 14, TextAnchor.MiddleLeft)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;

        var genderToggleButton = CreateButton(genderRow.transform, "GenderToggleButton", "Toggle", buttonSprite);
        SetPreferredWidth(genderToggleButton.gameObject, 80f);

        var genderValue = CreateText(genderRow.transform, "GenderValue", "Male", 14, TextAnchor.MiddleLeft);
        genderValue.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;

        var filterRow = CreateRow(panel.transform, "FilterRow");
        CreateText(filterRow.transform, "FilterLabel", "Filter", 14, TextAnchor.MiddleLeft)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;

        var filterInput = CreateInputField(filterRow.transform, "FilterInput", backgroundSprite);
        filterInput.text = string.Empty;

        var hairRow = CreateRow(panel.transform, "HairRow");
        CreateText(hairRow.transform, "HairLabelStatic", "Hair", 14, TextAnchor.MiddleLeft)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;

        var hairPrev = CreateButton(hairRow.transform, "HairPrevButton", "<", buttonSprite);
        SetPreferredWidth(hairPrev.gameObject, 36f);

        var hairLabel = CreateText(hairRow.transform, "HairLabel", "Hair: -", 14, TextAnchor.MiddleLeft);
        hairLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var hairNext = CreateButton(hairRow.transform, "HairNextButton", ">", buttonSprite);
        SetPreferredWidth(hairNext.gameObject, 36f);

        var faceRow = CreateRow(panel.transform, "FaceRow");
        CreateText(faceRow.transform, "FaceLabelStatic", "Face", 14, TextAnchor.MiddleLeft)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;

        var facePrev = CreateButton(faceRow.transform, "FacePrevButton", "<", buttonSprite);
        SetPreferredWidth(facePrev.gameObject, 36f);

        var faceLabel = CreateText(faceRow.transform, "FaceLabel", "Face: -", 14, TextAnchor.MiddleLeft);
        faceLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var faceNext = CreateButton(faceRow.transform, "FaceNextButton", ">", buttonSprite);
        SetPreferredWidth(faceNext.gameObject, 36f);

        var creatorUi = root.AddComponent<CharacterCreatorUI>();
        var serializedUi = new SerializedObject(creatorUi);

        serializedUi.FindProperty("creator").objectReferenceValue = creator;
        serializedUi.FindProperty("genderToggleButton").objectReferenceValue = genderToggleButton;
        serializedUi.FindProperty("genderValueLabel").objectReferenceValue = genderValue;
        serializedUi.FindProperty("filterInput").objectReferenceValue = filterInput;
        serializedUi.FindProperty("hairPrevButton").objectReferenceValue = hairPrev;
        serializedUi.FindProperty("hairNextButton").objectReferenceValue = hairNext;
        serializedUi.FindProperty("hairLabel").objectReferenceValue = hairLabel;
        serializedUi.FindProperty("facePrevButton").objectReferenceValue = facePrev;
        serializedUi.FindProperty("faceNextButton").objectReferenceValue = faceNext;
        serializedUi.FindProperty("faceLabel").objectReferenceValue = faceLabel;
        serializedUi.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Tools/FFXIV/Delete Character Creator UI")]
    public static void DeleteCharacterCreatorUI()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var uiRoot = GameObject.Find("CharacterCreatorUI");
        if (uiRoot != null)
            Undo.DestroyObjectImmediate(uiRoot);

        var creator = GameObject.Find("CharacterCreator");
        if (creator != null)
            Undo.DestroyObjectImmediate(creator);

        var eventSystem = GameObject.Find("EventSystem");
        if (eventSystem != null)
            Undo.DestroyObjectImmediate(eventSystem);

        var canvas = GameObject.Find("Canvas");
        if (canvas != null)
            Undo.DestroyObjectImmediate(canvas);

        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Tools/FFXIV/Recreate Character Creator UI")]
    public static void RecreateCharacterCreatorUI()
    {
        DeleteCharacterCreatorUI();
        CreateCharacterCreatorUI();
    }

    private static GameObject CreateRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(parent, false);

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        var fitter = row.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        row.AddComponent<LayoutElement>().preferredHeight = 28f;
        return row;
    }

    private static Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var label = go.GetComponent<Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.color = Color.white;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return label;
    }

    private static Button CreateButton(Transform parent, string name, string label, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        ApplySprite(image, sprite);
        image.color = ButtonNormal;

        var button = go.GetComponent<Button>();
        button.colors = CreateButtonColors();

        var text = CreateText(go.transform, "Text", label, 14, TextAnchor.MiddleCenter);
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return button;
    }

    private static ColorBlock CreateButtonColors()
    {
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = ButtonNormal;
        colors.highlightedColor = ButtonHighlight;
        colors.pressedColor = ButtonSelected;
        colors.selectedColor = ButtonSelected;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        return colors;
    }

    private static Sprite GetButtonSprite(Sprite fallback)
    {
        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        return sprite != null ? sprite : fallback;
    }

    private static Toggle CreateToggle(Transform parent, string name, string label, Sprite backgroundSprite, Sprite checkmarkSprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        go.transform.SetParent(parent, false);

        var toggle = go.GetComponent<Toggle>();

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(go.transform, false);
        var backgroundImage = background.GetComponent<Image>();
        ApplySprite(backgroundImage, backgroundSprite);
        backgroundImage.color = Color.white;

        var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkmark.transform.SetParent(background.transform, false);
        var checkmarkImage = checkmark.GetComponent<Image>();
        ApplySprite(checkmarkImage, checkmarkSprite);
        checkmarkImage.color = Color.white;

        var labelText = CreateText(go.transform, "Label", label, 14, TextAnchor.MiddleLeft);
        labelText.color = Color.white;

        var backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(18f, 18f);
        backgroundRect.anchoredPosition = new Vector2(8f, 0f);

        var checkmarkRect = checkmark.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkmarkRect.sizeDelta = new Vector2(10f, 10f);

        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(28f, 0f);
        labelRect.offsetMax = new Vector2(0f, 0f);

        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;

        return toggle;
    }

    private static InputField CreateInputField(Transform parent, string name, Sprite backgroundSprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        ApplySprite(image, backgroundSprite);
        image.color = Color.white;

        var input = go.GetComponent<InputField>();

        var text = CreateText(go.transform, "Text", string.Empty, 14, TextAnchor.MiddleLeft);
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);
        text.supportRichText = false;

        var placeholder = CreateText(go.transform, "Placeholder", "Search...", 14, TextAnchor.MiddleLeft);
        placeholder.color = new Color(1f, 1f, 1f, 0.5f);
        var placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(8f, 2f);
        placeholderRect.offsetMax = new Vector2(-8f, -2f);

        input.textComponent = text;
        input.placeholder = placeholder;

        var layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        layout.preferredWidth = 220f;
        layout.minWidth = 140f;

        return input;
    }

    private static void SetPreferredWidth(GameObject go, float width)
    {
        var layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
    }

    private static Sprite GetOrCreateFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;

        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        fallbackSprite.hideFlags = HideFlags.HideAndDontSave;
        return fallbackSprite;
    }

    private static void ApplySprite(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
    }
}
