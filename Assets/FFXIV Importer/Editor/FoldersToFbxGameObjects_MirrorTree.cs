using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FoldersToFbxGameObjects_MirrorTree
{
    [MenuItem("Tools/FFXIV/Create GameObjects (Mirror Folder Tree, Recursive FBX)")]
    public static void CreateMirrorTree()
    {
        var selected = Selection.assetGUIDs
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToArray();

        var rootFolders = selected.Where(AssetDatabase.IsValidFolder).ToArray();
        var directFbx = selected.Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (rootFolders.Length == 0 && directFbx.Length == 0)
        {
            EditorUtility.DisplayDialog("No folders/FBXs selected", "Select one or more folders (and/or FBXs).", "OK");
            return;
        }

        // Find FBXs under selected folders
        var fbxPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in directFbx) fbxPaths.Add(p);

        foreach (var folder in rootFolders)
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    fbxPaths.Add(path);
            }
        }

        var allFbx = fbxPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        if (allFbx.Length == 0)
        {
            EditorUtility.DisplayDialog("No FBXs found", "No .fbx files were found under the selected folders.", "OK");
            return;
        }

        bool unpack = false;
        bool registerUndoForChildren = false;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create FBX Tree Root");

        // Root in scene
        var sceneRoot = new GameObject("FBX_Instances_FFXIV");
        Undo.RegisterCreatedObjectUndo(sceneRoot, "Create FBX Tree Root");

        // Cache created transforms by "tree key"
        var nodeCache = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

        // Create top-level nodes for each selected root folder so multiple selections don't collide weirdly
        foreach (var folder in rootFolders)
        {
            var topName = Path.GetFileName(folder.TrimEnd('/'));
            var topGo = new GameObject(topName);
            if (registerUndoForChildren)
                Undo.RegisterCreatedObjectUndo(topGo, "Create Folder Root Node");
            topGo.transform.SetParent(sceneRoot.transform);
            nodeCache[folder] = topGo.transform;
        }

        int created = 0;

        foreach (var fbxPath in allFbx)
        {
            // Determine which selected root folder this FBX belongs to (deepest match)
            string owningRoot = FindOwningRoot(rootFolders, fbxPath);

            // If it wasn't under a selected folder (direct FBX selection), hang it under scene root
            Transform parent = sceneRoot.transform;
            string relFolder = Path.GetDirectoryName(fbxPath)?.Replace("\\", "/") ?? "";

            if (!string.IsNullOrEmpty(owningRoot))
            {
                // Build tree relative to owningRoot
                parent = nodeCache[owningRoot]; // the top node for that root folder
                relFolder = GetRelativeFolder(owningRoot, fbxPath); // folder path relative to root
                parent = EnsureTree(parent, owningRoot, relFolder, nodeCache, registerUndoForChildren);
            }
            else
            {
                // Direct FBX selection: mirror from its folder name as best effort
                parent = EnsureTree(parent, "__direct__", relFolder, nodeCache, registerUndoForChildren);
            }

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelPrefab == null)
            {
                Debug.LogWarning($"[FFXIV] Could not load FBX as GameObject: {fbxPath}");
                continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, parent);
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(modelPrefab, parent);

            instance.name = Path.GetFileNameWithoutExtension(fbxPath);
            instance.SetActive(false);

            if (unpack)
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            if (registerUndoForChildren)
                Undo.RegisterCreatedObjectUndo(instance, "Create FBX Instance");
            created++;
        }

        Selection.activeGameObject = sceneRoot;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog("Done", $"Created {created} GameObject(s) in a mirrored folder tree.", "OK");
    }

    private static string FindOwningRoot(string[] roots, string assetPath)
    {
        // Pick the deepest root that is a prefix of assetPath
        string best = null;
        int bestLen = -1;

        foreach (var r in roots)
        {
            var rr = r.TrimEnd('/');

            if (assetPath.StartsWith(rr + "/", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assetPath, rr, StringComparison.OrdinalIgnoreCase))
            {
                if (rr.Length > bestLen)
                {
                    best = rr;
                    bestLen = rr.Length;
                }
            }
        }

        return best;
    }

    private static string GetRelativeFolder(string rootFolder, string fbxPath)
    {
        rootFolder = rootFolder.TrimEnd('/');
        string dir = (Path.GetDirectoryName(fbxPath) ?? "").Replace("\\", "/");

        if (dir.Equals(rootFolder, StringComparison.OrdinalIgnoreCase))
            return ""; // FBX directly in root

        if (dir.StartsWith(rootFolder + "/", StringComparison.OrdinalIgnoreCase))
            return dir.Substring(rootFolder.Length + 1);

        return "";
    }

    private static Transform EnsureTree(
        Transform rootTransform,
        string cacheRootKey,
        string relativeFolder,
        Dictionary<string, Transform> cache,
        bool registerUndoForChildren)
    {
        // relativeFolder like "c0101/dwn/fbx" (or "")

        if (string.IsNullOrEmpty(relativeFolder))
            return rootTransform;

        var parts = relativeFolder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        Transform current = rootTransform;
        string key = cacheRootKey;

        foreach (var part in parts)
        {
            key += "/" + part;

            if (!cache.TryGetValue(key, out var node) || node == null)
            {
                var go = new GameObject(part);
                if (registerUndoForChildren)
                    Undo.RegisterCreatedObjectUndo(go, "Create Folder Node");
                go.transform.SetParent(current);
                node = go.transform;
                cache[key] = node;
            }

            current = node;
        }

        return current;
    }

    [MenuItem("Tools/FFXIV/Create GameObjects (Mirror Folder Tree, Recursive FBX)", true)]
    private static bool Validate()
    {
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
