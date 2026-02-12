using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CharacterVerticalSliceSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/CharacterVerticalSlice.unity";

    [MenuItem("Tools/FFXIV/Scene/Rebuild Character Vertical Slice")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("FFXIV Vertical Slice", "Exit Play Mode before rebuilding the scene.", "OK");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CharacterCreatorUIBuilder.DeleteCharacterCreatorUI();
        EquipmentUIBuilder.DeleteEquipmentUI();
        CharacterCreatorUIBuilder.CreateCharacterCreatorUI();
        EquipmentUIBuilder.CreateEquipmentUI();

        var creator = Object.FindObjectOfType<CharacterCreator>();
        var equipment = Object.FindObjectOfType<EquipmentSystem>();
        if (creator == null || equipment == null)
        {
            EditorUtility.DisplayDialog("FFXIV Vertical Slice", "Failed to create CharacterCreator or EquipmentSystem.", "OK");
            return;
        }

        var bridge = creator.GetComponent<CharacterCreatorEquipmentBridge>();
        if (bridge == null)
            bridge = creator.gameObject.AddComponent<CharacterCreatorEquipmentBridge>();

        ApplyCanonicalRuntimeConfig(creator, equipment, bridge);
        EnsureCamera(creator);
        var host = EnsureSampleControllerHost();
        var mount = EnsureAvatarMount(creator, equipment, bridge, host);
        var facade = EnsureFacade(creator, equipment, bridge, mount);
        EnsureQuickActionsUi(facade);

        EnsureSceneFolderExists();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            EditorUtility.DisplayDialog("FFXIV Vertical Slice", "Failed to save CharacterVerticalSlice scene.", "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate(showDialog: true);
    }

    [MenuItem("Tools/FFXIV/Scene/Validate Character Vertical Slice")]
    public static void ValidateMenu()
    {
        Validate(showDialog: true);
    }

    public static bool Validate(bool showDialog)
    {
        if (!File.Exists(ScenePath))
        {
            if (showDialog)
                EditorUtility.DisplayDialog("FFXIV Vertical Slice", $"Scene missing: {ScenePath}", "OK");
            return false;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
            return false;

        var facade = Object.FindObjectOfType<CharacterCustomizationFacade>();
        var mount = Object.FindObjectOfType<CharacterAvatarMount>();
        var host = Object.FindObjectOfType<CharacterControllerAvatarHost>();
        var creator = Object.FindObjectOfType<CharacterCreator>();
        var equipment = Object.FindObjectOfType<EquipmentSystem>();

        var issues = new System.Collections.Generic.List<string>();
        if (facade == null) issues.Add("Missing CharacterCustomizationFacade.");
        if (mount == null) issues.Add("Missing CharacterAvatarMount.");
        if (host == null) issues.Add("Missing CharacterControllerAvatarHost.");
        if (creator == null) issues.Add("Missing CharacterCreator.");
        if (equipment == null) issues.Add("Missing EquipmentSystem.");

        if (facade != null)
        {
            var so = new SerializedObject(facade);
            if (so.FindProperty("characterCreator").objectReferenceValue == null)
                issues.Add("CharacterCustomizationFacade.characterCreator is not wired.");
            if (so.FindProperty("equipmentSystem").objectReferenceValue == null)
                issues.Add("CharacterCustomizationFacade.equipmentSystem is not wired.");
        }

        if (mount != null)
        {
            var so = new SerializedObject(mount);
            if (so.FindProperty("characterCreator").objectReferenceValue == null)
                issues.Add("CharacterAvatarMount.characterCreator is not wired.");
            if (so.FindProperty("equipmentSystem").objectReferenceValue == null)
                issues.Add("CharacterAvatarMount.equipmentSystem is not wired.");
            if (so.FindProperty("hostAdapter").objectReferenceValue == null)
                issues.Add("CharacterAvatarMount.hostAdapter is not wired.");
        }

        if (host != null)
        {
            var so = new SerializedObject(host);
            if (so.FindProperty("avatarAnchor").objectReferenceValue == null)
                issues.Add("CharacterControllerAvatarHost.avatarAnchor is not wired.");
        }

        if (issues.Count == 0)
        {
            Debug.Log("[FFXIV] Character vertical slice validation passed.");
            if (showDialog)
                EditorUtility.DisplayDialog("FFXIV Vertical Slice", "Validation PASS.", "OK");
            return true;
        }

        for (int i = 0; i < issues.Count; i++)
            Debug.LogError($"[FFXIV] Vertical slice issue: {issues[i]}");

        if (showDialog)
            EditorUtility.DisplayDialog("FFXIV Vertical Slice", $"Validation FAIL. Issues: {issues.Count}", "OK");
        return false;
    }

    private static void ApplyCanonicalRuntimeConfig(CharacterCreator creator, EquipmentSystem equipment, CharacterCreatorEquipmentBridge bridge)
    {
        var runtimeCatalog = Resources.Load<FfxivRuntimeCatalog>("FfxivRuntimeCatalog");
        var addressableIndex = Resources.Load<FfxivAddressableIndex>("FfxivAddressableIndex");

        var creatorSo = new SerializedObject(creator);
        creatorSo.FindProperty("runtimeCatalog").objectReferenceValue = runtimeCatalog;
        creatorSo.FindProperty("addressableIndex").objectReferenceValue = addressableIndex;
        creatorSo.FindProperty("useAddressableIndex").boolValue = true;
        creatorSo.FindProperty("autoCollectFromScene").boolValue = false;
        creatorSo.FindProperty("usePrefabCatalog").boolValue = false;
        creatorSo.FindProperty("useAddressablesForHairFace").boolValue = true;
        creatorSo.FindProperty("useAddressablesForBodies").boolValue = true;
        creatorSo.ApplyModifiedPropertiesWithoutUndo();

        var equipmentSo = new SerializedObject(equipment);
        equipmentSo.FindProperty("runtimeCatalog").objectReferenceValue = runtimeCatalog;
        equipmentSo.FindProperty("addressableIndex").objectReferenceValue = addressableIndex;
        equipmentSo.FindProperty("useAddressableIndex").boolValue = true;
        equipmentSo.FindProperty("autoCollectFromScene").boolValue = false;
        equipmentSo.FindProperty("usePrefabCatalog").boolValue = false;
        equipmentSo.FindProperty("useAddressablesForEquipment").boolValue = true;
        equipmentSo.FindProperty("useAddressablesForWeapons").boolValue = true;
        equipmentSo.FindProperty("useAddressablesForBaseBodies").boolValue = true;
        equipmentSo.ApplyModifiedPropertiesWithoutUndo();

        var bridgeSo = new SerializedObject(bridge);
        bridgeSo.FindProperty("characterCreator").objectReferenceValue = creator;
        bridgeSo.FindProperty("equipmentSystem").objectReferenceValue = equipment;
        bridgeSo.FindProperty("syncOnEnable").boolValue = true;
        bridgeSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static CharacterControllerAvatarHost EnsureSampleControllerHost()
    {
        const string hostName = "SampleCharacterControllerHost";
        var go = GameObject.Find(hostName);
        if (go == null)
            go = new GameObject(hostName);

        var controller = go.GetComponent<CharacterController>();
        if (controller == null)
            controller = go.AddComponent<CharacterController>();

        controller.height = 1.8f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        var host = go.GetComponent<CharacterControllerAvatarHost>();
        if (host == null)
            host = go.AddComponent<CharacterControllerAvatarHost>();

        var anchor = go.transform.Find("AvatarAnchor");
        if (anchor == null)
        {
            var anchorGo = new GameObject("AvatarAnchor");
            anchor = anchorGo.transform;
            anchor.SetParent(go.transform, worldPositionStays: false);
        }

        var hostSo = new SerializedObject(host);
        hostSo.FindProperty("avatarAnchor").objectReferenceValue = anchor;
        hostSo.ApplyModifiedPropertiesWithoutUndo();

        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        return host;
    }

    private static CharacterAvatarMount EnsureAvatarMount(
        CharacterCreator creator,
        EquipmentSystem equipment,
        CharacterCreatorEquipmentBridge bridge,
        CharacterControllerAvatarHost host)
    {
        var mount = creator.GetComponent<CharacterAvatarMount>();
        if (mount == null)
            mount = creator.gameObject.AddComponent<CharacterAvatarMount>();

        var so = new SerializedObject(mount);
        so.FindProperty("characterCreator").objectReferenceValue = creator;
        so.FindProperty("equipmentSystem").objectReferenceValue = equipment;
        so.FindProperty("bridge").objectReferenceValue = bridge;
        so.FindProperty("hostAdapter").objectReferenceValue = host;
        so.FindProperty("autoFindReferences").boolValue = true;
        so.FindProperty("syncOnEnable").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return mount;
    }

    private static CharacterCustomizationFacade EnsureFacade(
        CharacterCreator creator,
        EquipmentSystem equipment,
        CharacterCreatorEquipmentBridge bridge,
        CharacterAvatarMount mount)
    {
        var facade = creator.GetComponent<CharacterCustomizationFacade>();
        if (facade == null)
            facade = creator.gameObject.AddComponent<CharacterCustomizationFacade>();

        var so = new SerializedObject(facade);
        so.FindProperty("characterCreator").objectReferenceValue = creator;
        so.FindProperty("equipmentSystem").objectReferenceValue = equipment;
        so.FindProperty("bridge").objectReferenceValue = bridge;
        so.FindProperty("avatarMount").objectReferenceValue = mount;
        so.FindProperty("defaultSlotId").stringValue = "slot_a";
        so.ApplyModifiedPropertiesWithoutUndo();
        return facade;
    }

    private static void EnsureQuickActionsUi(CharacterCustomizationFacade facade)
    {
        const string canvasName = "PresetActionsCanvas";
        const string panelName = "PresetActionsPanel";
        const string statusName = "StatusLabel";

        var canvasGo = GameObject.Find(canvasName);
        if (canvasGo == null)
        {
            canvasGo = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        var panel = canvasGo.transform.Find(panelName) as RectTransform;
        if (panel == null)
        {
            var panelGo = new GameObject(panelName, typeof(RectTransform), typeof(Image), typeof(CharacterPresetQuickActionsUI));
            panel = panelGo.GetComponent<RectTransform>();
            panel.SetParent(canvasGo.transform, worldPositionStays: false);
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = new Vector2(240f, 220f);
            panel.anchoredPosition = new Vector2(-20f, -20f);
            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        }

        var saveA = EnsureButton(panel, "SaveSlotAButton", "Save Slot A", new Vector2(0f, -24f));
        var loadA = EnsureButton(panel, "LoadSlotAButton", "Load Slot A", new Vector2(0f, -68f));
        var saveB = EnsureButton(panel, "SaveSlotBButton", "Save Slot B", new Vector2(0f, -112f));
        var loadB = EnsureButton(panel, "LoadSlotBButton", "Load Slot B", new Vector2(0f, -156f));
        var status = EnsureLabel(panel, statusName, new Vector2(0f, -198f), "Ready");

        var quick = panel.GetComponent<CharacterPresetQuickActionsUI>();
        var so = new SerializedObject(quick);
        so.FindProperty("facade").objectReferenceValue = facade;
        so.FindProperty("saveSlotAButton").objectReferenceValue = saveA;
        so.FindProperty("loadSlotAButton").objectReferenceValue = loadA;
        so.FindProperty("saveSlotBButton").objectReferenceValue = saveB;
        so.FindProperty("loadSlotBButton").objectReferenceValue = loadB;
        so.FindProperty("statusLabel").objectReferenceValue = status;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button EnsureButton(RectTransform parent, string name, string label, Vector2 anchoredPosition)
    {
        var t = parent.Find(name) as RectTransform;
        Button button;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            t = go.GetComponent<RectTransform>();
            t.SetParent(parent, worldPositionStays: false);
            t.anchorMin = new Vector2(0.5f, 1f);
            t.anchorMax = new Vector2(0.5f, 1f);
            t.pivot = new Vector2(0.5f, 1f);
            t.sizeDelta = new Vector2(200f, 34f);
            button = go.GetComponent<Button>();
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(t, worldPositionStays: false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.font = GetBuiltinFont();
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = label;
        }
        else
        {
            button = t.GetComponent<Button>();
            var text = t.GetComponentInChildren<Text>(true);
            if (text != null)
                text.text = label;
        }

        t.anchoredPosition = anchoredPosition;
        return button;
    }

    private static Text EnsureLabel(RectTransform parent, string name, Vector2 anchoredPosition, string textValue)
    {
        var t = parent.Find(name) as RectTransform;
        Text text;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            t = go.GetComponent<RectTransform>();
            t.SetParent(parent, worldPositionStays: false);
            t.anchorMin = new Vector2(0.5f, 1f);
            t.anchorMax = new Vector2(0.5f, 1f);
            t.pivot = new Vector2(0.5f, 1f);
            t.sizeDelta = new Vector2(200f, 20f);
            text = go.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.font = GetBuiltinFont();
            text.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            text.raycastTarget = false;
        }
        else
        {
            text = t.GetComponent<Text>();
        }

        t.anchoredPosition = anchoredPosition;
        text.text = textValue;
        return text;
    }

    private static Font GetBuiltinFont()
    {
        var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (legacy != null)
            return legacy;

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void EnsureCamera(CharacterCreator creator)
    {
        var camera = Camera.main;
        if (camera == null)
        {
            var cameraGo = new GameObject("Main Camera");
            camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.4f, -3f);
            camera.transform.rotation = Quaternion.identity;
        }

        var controller = camera.GetComponent<CharacterCreatorCameraController>();
        if (controller == null)
            controller = camera.gameObject.AddComponent<CharacterCreatorCameraController>();

        var so = new SerializedObject(controller);
        so.FindProperty("target").objectReferenceValue = creator != null ? creator.transform : null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureSceneFolderExists()
    {
        string folder = Path.GetDirectoryName(ScenePath)?.Replace("\\", "/");
        if (string.IsNullOrWhiteSpace(folder))
            return;

        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
