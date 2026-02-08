using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class EquipmentUIBuilder
{
    private static Sprite fallbackSprite;
    private static readonly Color ButtonNormal = new Color(0.95f, 0.95f, 0.95f, 1f);
    private static readonly Color ButtonHighlight = new Color(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Color ButtonSelected = new Color(0.78f, 0.78f, 0.78f, 1f);

    [MenuItem("Tools/FFXIV/Create Equipment UI")]
    public static void CreateEquipmentUI()
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

        var equipment = Object.FindObjectOfType<EquipmentSystem>();
        if (equipment == null)
        {
            var equipmentGo = new GameObject("EquipmentSystem");
            Undo.RegisterCreatedObjectUndo(equipmentGo, "Create EquipmentSystem");
            equipment = equipmentGo.AddComponent<EquipmentSystem>();
        }

        var root = new GameObject("EquipmentUI", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Equipment UI");
        root.transform.SetParent(canvas.transform, false);

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(520f, -100f);

        Sprite fallback = GetOrCreateFallbackSprite();
        Sprite backgroundSprite = fallback;
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

        var title = CreateText(panel.transform, "Title", "Equipment", 18, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

        var genderRow = CreateRow(panel.transform, "GenderRow");
        CreateText(genderRow.transform, "GenderLabel", "Gender", 14, TextAnchor.MiddleLeft)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;

        var genderToggleButton = CreateButton(genderRow.transform, "GenderToggleButton", "Toggle", buttonSprite);
        SetPreferredWidth(genderToggleButton.gameObject, 80f);

        var genderValue = CreateText(genderRow.transform, "GenderValue", "Male", 14, TextAnchor.MiddleLeft);
        genderValue.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;

        var headRow = CreateSlotRow(panel.transform, "HeadRow", "Head", buttonSprite, out var headPrev, out var headLabel, out var headNext);
        var bodyRow = CreateSlotRow(panel.transform, "BodyRow", "Body", buttonSprite, out var bodyPrev, out var bodyLabel, out var bodyNext);
        var handsRow = CreateSlotRow(panel.transform, "HandsRow", "Hands", buttonSprite, out var handsPrev, out var handsLabel, out var handsNext);
        var legsRow = CreateSlotRow(panel.transform, "LegsRow", "Legs", buttonSprite, out var legsPrev, out var legsLabel, out var legsNext);
        var feetRow = CreateSlotRow(panel.transform, "FeetRow", "Feet", buttonSprite, out var feetPrev, out var feetLabel, out var feetNext);
        var weaponRow = CreateSlotRow(panel.transform, "WeaponRow", "Weapon", buttonSprite, out var weaponPrev, out var weaponLabel, out var weaponNext);

        var ui = root.AddComponent<EquipmentSelectionUI>();
        var serializedUi = new SerializedObject(ui);

        serializedUi.FindProperty("equipment").objectReferenceValue = equipment;
        serializedUi.FindProperty("genderToggleButton").objectReferenceValue = genderToggleButton;
        serializedUi.FindProperty("genderValueLabel").objectReferenceValue = genderValue;
        serializedUi.FindProperty("headPrevButton").objectReferenceValue = headPrev;
        serializedUi.FindProperty("headNextButton").objectReferenceValue = headNext;
        serializedUi.FindProperty("headLabel").objectReferenceValue = headLabel;
        serializedUi.FindProperty("bodyPrevButton").objectReferenceValue = bodyPrev;
        serializedUi.FindProperty("bodyNextButton").objectReferenceValue = bodyNext;
        serializedUi.FindProperty("bodyLabel").objectReferenceValue = bodyLabel;
        serializedUi.FindProperty("handsPrevButton").objectReferenceValue = handsPrev;
        serializedUi.FindProperty("handsNextButton").objectReferenceValue = handsNext;
        serializedUi.FindProperty("handsLabel").objectReferenceValue = handsLabel;
        serializedUi.FindProperty("legsPrevButton").objectReferenceValue = legsPrev;
        serializedUi.FindProperty("legsNextButton").objectReferenceValue = legsNext;
        serializedUi.FindProperty("legsLabel").objectReferenceValue = legsLabel;
        serializedUi.FindProperty("feetPrevButton").objectReferenceValue = feetPrev;
        serializedUi.FindProperty("feetNextButton").objectReferenceValue = feetNext;
        serializedUi.FindProperty("feetLabel").objectReferenceValue = feetLabel;
        serializedUi.FindProperty("weaponPrevButton").objectReferenceValue = weaponPrev;
        serializedUi.FindProperty("weaponNextButton").objectReferenceValue = weaponNext;
        serializedUi.FindProperty("weaponLabel").objectReferenceValue = weaponLabel;
        serializedUi.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Tools/FFXIV/Delete Equipment UI")]
    public static void DeleteEquipmentUI()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var uiRoot = GameObject.Find("EquipmentUI");
        if (uiRoot != null)
            Undo.DestroyObjectImmediate(uiRoot);

        var equipment = GameObject.Find("EquipmentSystem");
        if (equipment != null)
            Undo.DestroyObjectImmediate(equipment);

        var eventSystem = GameObject.Find("EventSystem");
        if (eventSystem != null)
            Undo.DestroyObjectImmediate(eventSystem);

        var canvas = GameObject.Find("Canvas");
        if (canvas != null)
            Undo.DestroyObjectImmediate(canvas);

        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Tools/FFXIV/Recreate Equipment UI")]
    public static void RecreateEquipmentUI()
    {
        DeleteEquipmentUI();
        CreateEquipmentUI();
    }

    private static GameObject CreateSlotRow(Transform parent, string rowName, string label, Sprite buttonSprite, out Button prev, out Text valueLabel, out Button next)
    {
        var row = CreateRow(parent, rowName);
        CreateText(row.transform, "Label", label, 14, TextAnchor.MiddleLeft)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;

        prev = CreateButton(row.transform, "PrevButton", "<", buttonSprite);
        SetPreferredWidth(prev.gameObject, 36f);

        valueLabel = CreateText(row.transform, "Value", $"{label}: -", 14, TextAnchor.MiddleLeft);
        valueLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        next = CreateButton(row.transform, "NextButton", ">", buttonSprite);
        SetPreferredWidth(next.gameObject, 36f);

        return row;
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

    private static void SetPreferredWidth(GameObject go, float width)
    {
        var layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
    }
}
