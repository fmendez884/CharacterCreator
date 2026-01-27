using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SelectedFbxToGameObjects
{
    [MenuItem("Tools/FFXIV/Create GameObjects from Selected FBXs")]
    public static void CreateGameObjects()
    {
        var modelPaths = Selection.assetGUIDs
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();

        if (modelPaths.Length == 0)
        {
            EditorUtility.DisplayDialog("No FBXs selected", "Select one or more .fbx assets in the Project window.", "OK");
            return;
        }

        // Parent to keep things tidy
        var root = new GameObject($"ImportedModels_{DateTime.Now:yyyyMMdd_HHmmss}");
        Undo.RegisterCreatedObjectUndo(root, "Create import root");

        int created = 0;

        foreach (var path in modelPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[FFXIV] Could not load model as GameObject: {path}");
                continue;
            }

            // Instantiate into scene
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            if (instance == null)
            {
                // Fallback (rare)
                instance = UnityEngine.Object.Instantiate(prefab, root.transform);
            }

            instance.name = Path.GetFileNameWithoutExtension(path);

            // Make it editable (optional). Comment out if you want it to remain a prefab instance.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            Undo.RegisterCreatedObjectUndo(instance, "Create model instance");
            created++;
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Done", $"Created {created} GameObject(s) in the current scene.", "OK");
    }

    [MenuItem("Tools/FFXIV/Create Prefabs from Selected FBXs...")]
    public static void CreatePrefabs()
    {
        var modelPaths = Selection.assetGUIDs
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();

        if (modelPaths.Length == 0)
        {
            EditorUtility.DisplayDialog("No FBXs selected", "Select one or more .fbx assets in the Project window.", "OK");
            return;
        }

        var targetFolder = EditorUtility.SaveFolderPanel("Choose Prefab Output Folder (inside Assets)", "Assets", "Prefabs");
        if (string.IsNullOrEmpty(targetFolder))
            return;

        // Ensure it's inside Assets/
        targetFolder = targetFolder.Replace("\\", "/");
        string assetsAbs = Application.dataPath.Replace("\\", "/");
        if (!targetFolder.StartsWith(assetsAbs, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Invalid folder", "Please choose a folder inside this project's Assets/ directory.", "OK");
            return;
        }

        // Convert absolute path to "Assets/..."
        string targetAssetFolder = "Assets" + targetFolder.Substring(assetsAbs.Length);

        int created = 0;

        foreach (var path in modelPaths)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                continue;

            string name = Path.GetFileNameWithoutExtension(path);
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{targetAssetFolder}/{name}.prefab");

            // Create a prefab that references the model (no instantiation needed)
            PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Done", $"Created {created} prefab(s) in:\n{targetAssetFolder}", "OK");
    }
}
