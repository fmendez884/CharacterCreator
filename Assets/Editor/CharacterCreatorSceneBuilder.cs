using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CharacterCreatorSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/CharacterCreator.unity";

    [MenuItem("Tools/FFXIV/Scene/Rebuild CharacterCreator Scene")]
    public static void RebuildScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("FFXIV Scene Builder", "Exit Play Mode before rebuilding the scene.", "OK");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CharacterCreatorUIBuilder.DeleteCharacterCreatorUI();
        EquipmentUIBuilder.DeleteEquipmentUI();
        CharacterCreatorUIBuilder.CreateCharacterCreatorUI();
        EquipmentUIBuilder.CreateEquipmentUI();

        var creator = UnityEngine.Object.FindObjectOfType<CharacterCreator>();
        var equipment = UnityEngine.Object.FindObjectOfType<EquipmentSystem>();
        if (creator == null || equipment == null)
        {
            EditorUtility.DisplayDialog("FFXIV Scene Builder", "Failed to create CharacterCreator and EquipmentSystem.", "OK");
            return;
        }

        var bridge = creator.GetComponent<CharacterCreatorEquipmentBridge>();
        if (bridge == null)
            bridge = creator.gameObject.AddComponent<CharacterCreatorEquipmentBridge>();

        EnsureCamera(creator);
        ApplyCanonicalConfiguration(creator, equipment, bridge);

        EnsureSceneFolderExists();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            EditorUtility.DisplayDialog("FFXIV Scene Builder", "Failed to save CharacterCreator scene.", "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidateScene(showDialog: true);
    }

    [MenuItem("Tools/FFXIV/Scene/Validate CharacterCreator Scene")]
    public static void ValidateSceneMenu()
    {
        ValidateScene(showDialog: true);
    }

    public static bool ValidateScene(bool showDialog)
    {
        var issues = new List<string>();

        if (!File.Exists(ScenePath))
        {
            issues.Add($"Scene missing: {ScenePath}");
            return ReportValidation(issues, showDialog);
        }

        ValidateSceneYamlDuplicates(ScenePath, issues);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            issues.Add($"Failed to open scene: {ScenePath}");
            return ReportValidation(issues, showDialog);
        }

        var creator = UnityEngine.Object.FindObjectOfType<CharacterCreator>();
        var equipment = UnityEngine.Object.FindObjectOfType<EquipmentSystem>();
        var bridge = UnityEngine.Object.FindObjectOfType<CharacterCreatorEquipmentBridge>();
        var creatorUi = UnityEngine.Object.FindObjectOfType<CharacterCreatorUI>();
        var equipmentUi = UnityEngine.Object.FindObjectOfType<EquipmentSelectionUI>();
        var cameraController = UnityEngine.Object.FindObjectOfType<CharacterCreatorCameraController>();

        if (creator == null) issues.Add("Missing CharacterCreator component.");
        if (equipment == null) issues.Add("Missing EquipmentSystem component.");
        if (bridge == null) issues.Add("Missing CharacterCreatorEquipmentBridge component.");
        if (creatorUi == null) issues.Add("Missing CharacterCreatorUI component.");
        if (equipmentUi == null) issues.Add("Missing EquipmentSelectionUI component.");
        if (cameraController == null) issues.Add("Missing CharacterCreatorCameraController component.");

        if (bridge != null && creator != null && equipment != null)
        {
            var bridgeSo = new SerializedObject(bridge);
            if (bridgeSo.FindProperty("characterCreator").objectReferenceValue != creator)
                issues.Add("Bridge.characterCreator is not wired to scene CharacterCreator.");
            if (bridgeSo.FindProperty("equipmentSystem").objectReferenceValue != equipment)
                issues.Add("Bridge.equipmentSystem is not wired to scene EquipmentSystem.");
        }

        if (creatorUi != null)
        {
            var so = new SerializedObject(creatorUi);
            if (creator != null && so.FindProperty("creator").objectReferenceValue != creator)
                issues.Add("CharacterCreatorUI.creator is not wired.");
            if (so.FindProperty("hairPrevButton").objectReferenceValue == null)
                issues.Add("CharacterCreatorUI.hairPrevButton is not wired.");
            if (so.FindProperty("hairNextButton").objectReferenceValue == null)
                issues.Add("CharacterCreatorUI.hairNextButton is not wired.");
            if (so.FindProperty("facePrevButton").objectReferenceValue == null)
                issues.Add("CharacterCreatorUI.facePrevButton is not wired.");
            if (so.FindProperty("faceNextButton").objectReferenceValue == null)
                issues.Add("CharacterCreatorUI.faceNextButton is not wired.");
        }

        if (equipmentUi != null && equipment != null)
        {
            var so = new SerializedObject(equipmentUi);
            if (so.FindProperty("equipment").objectReferenceValue != equipment)
                issues.Add("EquipmentSelectionUI.equipment is not wired.");
        }

        if (creator != null)
        {
            var so = new SerializedObject(creator);
            bool useIndex = so.FindProperty("useAddressableIndex").boolValue;
            bool collectFromScene = so.FindProperty("autoCollectFromScene").boolValue;
            if (useIndex && collectFromScene)
                issues.Add("CharacterCreator.autoCollectFromScene must be disabled when useAddressableIndex is enabled.");
        }

        return ReportValidation(issues, showDialog);
    }

    private static bool ReportValidation(List<string> issues, bool showDialog)
    {
        bool ok = issues.Count == 0;
        if (ok)
        {
            Debug.Log("[FFXIV] CharacterCreator scene validation passed.");
            if (showDialog)
                EditorUtility.DisplayDialog("FFXIV Scene Validation", "Validation PASS.", "OK");
            return true;
        }

        foreach (var issue in issues)
            Debug.LogError($"[FFXIV] Scene validation issue: {issue}");

        if (showDialog)
            EditorUtility.DisplayDialog("FFXIV Scene Validation", $"Validation FAIL. Issues: {issues.Count}\nSee Console for details.", "OK");
        return false;
    }

    private static void ValidateSceneYamlDuplicates(string scenePath, List<string> issues)
    {
        var lines = File.ReadAllLines(scenePath);
        bool inMono = false;
        int blockStart = 0;
        var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        void FlushBlock()
        {
            foreach (var kvp in keyCounts)
            {
                if (kvp.Value > 1)
                    issues.Add($"Duplicate serialized key '{kvp.Key}' in MonoBehaviour block starting near line {blockStart}.");
            }
            keyCounts.Clear();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("--- !u!114 "))
            {
                if (inMono)
                    FlushBlock();
                inMono = true;
                blockStart = i + 1;
                continue;
            }

            if (inMono && line.StartsWith("--- !u!"))
            {
                FlushBlock();
                inMono = false;
                continue;
            }

            if (!inMono || !line.StartsWith("  "))
                continue;

            int colon = line.IndexOf(':');
            if (colon <= 2)
                continue;

            string key = line.Substring(2, colon - 2);
            if (key.Contains(" "))
                continue;

            if (!keyCounts.TryGetValue(key, out int count))
                keyCounts[key] = 1;
            else
                keyCounts[key] = count + 1;
        }

        if (inMono)
            FlushBlock();
    }

    private static void ApplyCanonicalConfiguration(CharacterCreator creator, EquipmentSystem equipment, CharacterCreatorEquipmentBridge bridge)
    {
        var runtimeCatalog = Resources.Load<FfxivRuntimeCatalog>("FfxivRuntimeCatalog");
        var addressableIndex = Resources.Load<FfxivAddressableIndex>("FfxivAddressableIndex");

        var creatorSo = new SerializedObject(creator);
        creatorSo.FindProperty("runtimeCatalog").objectReferenceValue = runtimeCatalog;
        creatorSo.FindProperty("addressableIndex").objectReferenceValue = addressableIndex;
        creatorSo.FindProperty("useAddressableIndex").boolValue = true;
        creatorSo.FindProperty("autoCollectFromScene").boolValue = false;
        creatorSo.FindProperty("useAddressablesForHairFace").boolValue = true;
        creatorSo.FindProperty("useAddressablesForBodies").boolValue = true;
        creatorSo.FindProperty("usePrefabCatalog").boolValue = false;
        creatorSo.FindProperty("loadAddressablesOnAwake").boolValue = true;
        creatorSo.FindProperty("logSceneCleanup").boolValue = false;
        creatorSo.FindProperty("logToFile").boolValue = false;
        creatorSo.ApplyModifiedPropertiesWithoutUndo();

        var equipmentSo = new SerializedObject(equipment);
        equipmentSo.FindProperty("runtimeCatalog").objectReferenceValue = runtimeCatalog;
        equipmentSo.FindProperty("addressableIndex").objectReferenceValue = addressableIndex;
        equipmentSo.FindProperty("useAddressableIndex").boolValue = true;
        equipmentSo.FindProperty("autoCollectFromScene").boolValue = false;
        equipmentSo.FindProperty("useAddressablesForEquipment").boolValue = true;
        equipmentSo.FindProperty("useAddressablesForWeapons").boolValue = true;
        equipmentSo.FindProperty("useAddressablesForBaseBodies").boolValue = true;
        equipmentSo.FindProperty("includeUnequippedOption").boolValue = true;
        equipmentSo.FindProperty("usePrefabCatalog").boolValue = false;
        equipmentSo.FindProperty("loadAddressablesOnAwake").boolValue = true;
        equipmentSo.FindProperty("logSceneCleanup").boolValue = false;
        equipmentSo.FindProperty("logToFile").boolValue = false;
        equipmentSo.ApplyModifiedPropertiesWithoutUndo();

        var bridgeSo = new SerializedObject(bridge);
        bridgeSo.FindProperty("characterCreator").objectReferenceValue = creator;
        bridgeSo.FindProperty("equipmentSystem").objectReferenceValue = equipment;
        bridgeSo.FindProperty("syncOnEnable").boolValue = true;
        bridgeSo.FindProperty("syncBaseBodies").boolValue = true;
        bridgeSo.FindProperty("hideHairWhenHelmetEquipped").boolValue = true;
        bridgeSo.ApplyModifiedPropertiesWithoutUndo();
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
