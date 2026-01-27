using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FoldersToFbxGameObjects
{
    [MenuItem("Tools/FFXIV/Create GameObjects from Selected Folders (Recursive FBX)")]
    public static void CreateFromFolders()
    {
        // Gather selected folders and any directly selected FBXs
        var selectedPaths = Selection.assetGUIDs
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToArray();

        if (selectedPaths.Length == 0)
        {
            EditorUtility.DisplayDialog("Nothing selected", "Select one or more folders (and/or FBXs) in the Project window.", "OK");
            return;
        }

        var folders = selectedPaths.Where(AssetDatabase.IsValidFolder).ToArray();
        var directFbx = selectedPaths
            .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (folders.Length == 0 && directFbx.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "No folders/FBXs selected",
                "Select one or more folders, or select FBX assets directly.",
                "OK"
            );
            return;
        }

        // Find all FBXs under selected folders (recursive)
        var fbxPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add directly selected FBXs
        foreach (var p in directFbx)
            fbxPaths.Add(p);

        // Add FBXs found under folders
        if (folders.Length > 0)
        {
            // Find all models under these folders
            var guids = AssetDatabase.FindAssets("t:Model", folders);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    fbxPaths.Add(path);
            }
        }

        var ordered = fbxPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

        if (ordered.Length == 0)
        {
            EditorUtility.DisplayDialog("No FBXs found", "No .fbx files were found in the selected folders.", "OK");
            return;
        }

        // Options
        bool unpack = true; // set false if you want prefab instances linked to the FBX
        bool groupByFolder = true;

        // Root parent
        var root = new GameObject($"FBX_Instances_{DateTime.Now:yyyyMMdd_HHmmss}");
        Undo.RegisterCreatedObjectUndo(root, "Create FBX Instances Root");

        int created = 0;

        // Optional grouping by folder
        Dictionary<string, Transform> folderParents = new(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ordered)
        {
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (modelPrefab == null)
            {
                Debug.LogWarning($"[FFXIV] Could not load FBX as GameObject: {path}");
                continue;
            }

            Transform parent = root.transform;

            if (groupByFolder)
            {
                string folder = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Unknown";
                if (!folderParents.TryGetValue(folder, out var t) || t == null)
                {
                    var folderGo = new GameObject(folder.Split('/').Last());
                    Undo.RegisterCreatedObjectUndo(folderGo, "Create Folder Group");
                    folderGo.transform.SetParent(root.transform);
                    t = folderGo.transform;
                    folderParents[folder] = t;
                }
                parent = t;
            }

            // Instantiate in scene
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, parent);
            if (instance == null)
            {
                // Rare fallback
                instance = UnityEngine.Object.Instantiate(modelPrefab, parent);
            }

            instance.name = Path.GetFileNameWithoutExtension(path);

            if (unpack)
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            Undo.RegisterCreatedObjectUndo(instance, "Create FBX Instance");
            created++;
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Done", $"Created {created} GameObject(s) from {ordered.Length} FBX file(s).", "OK");
    }

    [MenuItem("Tools/FFXIV/Create GameObjects from Selected Folders (Recursive FBX)", true)]
    private static bool Validate()
    {
        // Enable if selection contains at least one folder or one FBX
        foreach (var guid in Selection.assetGUIDs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            if (AssetDatabase.IsValidFolder(path)) return true;
            if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
