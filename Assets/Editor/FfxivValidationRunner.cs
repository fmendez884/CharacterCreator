using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FfxivValidationRunner
{
    private const string SourceRootKey = "FFXIV.Processed.SourceRoot";
    private const string MenuPath = "Tools/FFXIV/Validate/Run Full Validation (Processed + Addressables + Catalog)";
    private const int MaxLoggedFbx = 10;
    private const string ProgressTitle = "FFXIV Validation";

    [MenuItem(MenuPath)]
    public static void RunFullValidation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("FFXIV Validation", "Exit Play Mode before running validation.", "OK");
            return;
        }

        string sourceRoot = EditorPrefs.GetString(SourceRootKey, string.Empty);
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            EditorUtility.DisplayDialog("FFXIV Validation", "Set a valid processed source root before running validation.", "OK");
            return;
        }

        var issues = new List<string>();
        var startUtc = DateTime.UtcNow;
        const int totalSteps = 9;

        try
        {
            ThrowIfCanceled(startUtc, 0, totalSteps, "Building processed prefabs");
            FfxivProcessedPrefabBuilder.BuildUsingLastSource(throwOnCancel: true);

            ThrowIfCanceled(startUtc, 1, totalSteps, "Configuring Addressables groups");
            FfxivAddressablesConfigurator.SetupGroups(showDialog: false);

            ThrowIfCanceled(startUtc, 2, totalSteps, "Building runtime catalog");
            if (!FfxivRuntimeCatalogBuilder.BuildCatalog(showDialog: false))
                issues.Add("Runtime catalog build failed.");

            ThrowIfCanceled(startUtc, 3, totalSteps, "Building addressable index adapter");
            if (!FfxivAddressableIndexBuilder.BuildIndex(showDialog: false))
                issues.Add("Addressable index build failed.");

            ThrowIfCanceled(startUtc, 4, totalSteps, "Building prefab catalog adapter");
            if (!FfxivPrefabCatalogBuilder.BuildCatalog(showDialog: false))
                issues.Add("Prefab catalog build failed.");

            ThrowIfCanceled(startUtc, 5, totalSteps, "Validating CharacterCreator scene");
            if (!CharacterCreatorSceneBuilder.ValidateScene(showDialog: false))
                issues.Add("CharacterCreator scene validation failed.");

            ThrowIfCanceled(startUtc, 6, totalSteps, "Validating preset schema and portable wiring");
            ValidatePresetSchemaAndPortableWiring(issues);

            ThrowIfCanceled(startUtc, 7, totalSteps, "Validating catalog parity and integrity");
            ValidateCatalogParity(issues);

            ThrowIfCanceled(startUtc, 8, totalSteps, "Scanning Assets for FBX files");
            int fbxCount = ScanForFbx(startUtc, 8, totalSteps, out List<string> samplePaths);
            if (fbxCount > 0)
            {
                issues.Add($"Found {fbxCount} .fbx files under Assets.");
                foreach (var path in samplePaths)
                    Debug.LogWarning($"[FFXIV] FBX: {path}");
            }

            ThrowIfCanceled(startUtc, totalSteps, totalSteps, "Finalizing validation report");

            if (issues.Count == 0)
            {
                Debug.Log("[FFXIV] Validation PASS.");
                EditorUtility.DisplayDialog("FFXIV Validation", "Validation PASS.", "OK");
            }
            else
            {
                foreach (var issue in issues)
                    Debug.LogError($"[FFXIV] Validation issue: {issue}");
                EditorUtility.DisplayDialog("FFXIV Validation", $"Validation FAIL. Issues: {issues.Count}\nSee Console for details.", "OK");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[FFXIV] Validation canceled by user.");
            EditorUtility.DisplayDialog("FFXIV Validation", "Validation canceled by user.", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static void ThrowIfCanceled(DateTime startUtc, int stepIndex, int totalSteps, string stepLabel)
    {
        if (UpdateValidationProgress(startUtc, stepIndex, totalSteps, stepLabel))
            throw new OperationCanceledException();
    }

    private static bool UpdateValidationProgress(DateTime startUtc, int stepIndex, int totalSteps, string stepLabel)
    {
        int currentStep = Mathf.Clamp(stepIndex + 1, 1, totalSteps);
        float progress = Mathf.Clamp01(stepIndex / (float)totalSteps);
        string eta = FormatEta(startUtc, stepIndex, totalSteps);
        string message = $"ETA: {eta} | {stepLabel}\nStep {currentStep}/{totalSteps} | ETA: {eta}";
        return EditorUtility.DisplayCancelableProgressBar(ProgressTitle, message, progress);
    }

    private static string FormatEta(DateTime startUtc, int completedSteps, int totalSteps)
    {
        if (completedSteps <= 0)
            return "estimating...";

        double elapsedSeconds = (DateTime.UtcNow - startUtc).TotalSeconds;
        if (elapsedSeconds <= 0d)
            return "estimating...";

        double avgSecondsPerStep = elapsedSeconds / completedSteps;
        int remainingSteps = Mathf.Max(0, totalSteps - completedSteps);
        double remainingSeconds = avgSecondsPerStep * remainingSteps;
        return $"{Mathf.CeilToInt((float)remainingSeconds)}s";
    }

    private static void ValidateCatalogParity(List<string> issues)
    {
        var runtime = AssetDatabase.LoadAssetAtPath<FfxivRuntimeCatalog>("Assets/Resources/FfxivRuntimeCatalog.asset");
        var index = AssetDatabase.LoadAssetAtPath<FfxivAddressableIndex>("Assets/Resources/FfxivAddressableIndex.asset");
        var prefabs = AssetDatabase.LoadAssetAtPath<FfxivPrefabCatalog>("Assets/Resources/FfxivPrefabCatalog.asset");

        if (runtime == null)
        {
            issues.Add("Missing runtime catalog asset at Assets/Resources/FfxivRuntimeCatalog.asset.");
            return;
        }

        if (index == null)
        {
            issues.Add("Missing addressable index asset at Assets/Resources/FfxivAddressableIndex.asset.");
            return;
        }

        if (prefabs == null)
        {
            issues.Add("Missing prefab catalog asset at Assets/Resources/FfxivPrefabCatalog.asset.");
            return;
        }

        ValidateRequiredNonEmptyKeys(runtime.maleHair, "runtime.maleHair", issues);
        ValidateRequiredNonEmptyKeys(runtime.femaleHair, "runtime.femaleHair", issues);
        ValidateRequiredNonEmptyKeys(runtime.maleFace, "runtime.maleFace", issues);
        ValidateRequiredNonEmptyKeys(runtime.femaleFace, "runtime.femaleFace", issues);

        ValidateRequiredNonEmptyKeys(index.maleHair, "index.maleHair", issues);
        ValidateRequiredNonEmptyKeys(index.femaleHair, "index.femaleHair", issues);
        ValidateRequiredNonEmptyKeys(index.maleFace, "index.maleFace", issues);
        ValidateRequiredNonEmptyKeys(index.femaleFace, "index.femaleFace", issues);

        CompareCounts("maleHair", runtime.maleHair.Count, index.maleHair.Count, issues);
        CompareCounts("femaleHair", runtime.femaleHair.Count, index.femaleHair.Count, issues);
        CompareCounts("maleFace", runtime.maleFace.Count, index.maleFace.Count, issues);
        CompareCounts("femaleFace", runtime.femaleFace.Count, index.femaleFace.Count, issues);
        CompareCounts("maleHead", runtime.maleHead.Count, index.maleHead.Count, issues);
        CompareCounts("maleBody", runtime.maleBody.Count, index.maleBody.Count, issues);
        CompareCounts("maleHands", runtime.maleHands.Count, index.maleHands.Count, issues);
        CompareCounts("maleLegs", runtime.maleLegs.Count, index.maleLegs.Count, issues);
        CompareCounts("maleFeet", runtime.maleFeet.Count, index.maleFeet.Count, issues);
        CompareCounts("femaleHead", runtime.femaleHead.Count, index.femaleHead.Count, issues);
        CompareCounts("femaleBody", runtime.femaleBody.Count, index.femaleBody.Count, issues);
        CompareCounts("femaleHands", runtime.femaleHands.Count, index.femaleHands.Count, issues);
        CompareCounts("femaleLegs", runtime.femaleLegs.Count, index.femaleLegs.Count, issues);
        CompareCounts("femaleFeet", runtime.femaleFeet.Count, index.femaleFeet.Count, issues);
        CompareCounts("maleBaseBodies", runtime.maleBaseBodies.Count, index.maleBaseBodies.Count, issues);
        CompareCounts("femaleBaseBodies", runtime.femaleBaseBodies.Count, index.femaleBaseBodies.Count, issues);
        CompareCounts("weapons", runtime.weapons.Count, index.weapons.Count, issues);

        CompareCounts("maleHairPrefabs", runtime.maleHairPrefabs.Count, prefabs.maleHair.Count, issues);
        CompareCounts("femaleHairPrefabs", runtime.femaleHairPrefabs.Count, prefabs.femaleHair.Count, issues);
        CompareCounts("maleFacePrefabs", runtime.maleFacePrefabs.Count, prefabs.maleFace.Count, issues);
        CompareCounts("femaleFacePrefabs", runtime.femaleFacePrefabs.Count, prefabs.femaleFace.Count, issues);
        CompareCounts("maleHeadPrefabs", runtime.maleHeadPrefabs.Count, prefabs.maleHead.Count, issues);
        CompareCounts("maleBodyPrefabs", runtime.maleBodyPrefabs.Count, prefabs.maleBody.Count, issues);
        CompareCounts("maleHandsPrefabs", runtime.maleHandsPrefabs.Count, prefabs.maleHands.Count, issues);
        CompareCounts("maleLegsPrefabs", runtime.maleLegsPrefabs.Count, prefabs.maleLegs.Count, issues);
        CompareCounts("maleFeetPrefabs", runtime.maleFeetPrefabs.Count, prefabs.maleFeet.Count, issues);
        CompareCounts("femaleHeadPrefabs", runtime.femaleHeadPrefabs.Count, prefabs.femaleHead.Count, issues);
        CompareCounts("femaleBodyPrefabs", runtime.femaleBodyPrefabs.Count, prefabs.femaleBody.Count, issues);
        CompareCounts("femaleHandsPrefabs", runtime.femaleHandsPrefabs.Count, prefabs.femaleHands.Count, issues);
        CompareCounts("femaleLegsPrefabs", runtime.femaleLegsPrefabs.Count, prefabs.femaleLegs.Count, issues);
        CompareCounts("femaleFeetPrefabs", runtime.femaleFeetPrefabs.Count, prefabs.femaleFeet.Count, issues);
        CompareCounts("maleBaseBodyPrefabs", runtime.maleBaseBodyPrefabs.Count, prefabs.maleBaseBodies.Count, issues);
        CompareCounts("femaleBaseBodyPrefabs", runtime.femaleBaseBodyPrefabs.Count, prefabs.femaleBaseBodies.Count, issues);
        CompareCounts("weaponPrefabs", runtime.weaponPrefabs.Count, prefabs.weapons.Count, issues);

        ValidateDuplicateKeys(runtime.maleHair, "runtime.maleHair", issues);
        ValidateDuplicateKeys(runtime.femaleHair, "runtime.femaleHair", issues);
        ValidateDuplicateKeys(runtime.maleFace, "runtime.maleFace", issues);
        ValidateDuplicateKeys(runtime.femaleFace, "runtime.femaleFace", issues);
        ValidateDuplicateKeys(runtime.maleHead, "runtime.maleHead", issues);
        ValidateDuplicateKeys(runtime.femaleHead, "runtime.femaleHead", issues);
        ValidateDuplicateKeys(runtime.maleBody, "runtime.maleBody", issues);
        ValidateDuplicateKeys(runtime.femaleBody, "runtime.femaleBody", issues);
        ValidateDuplicateKeys(runtime.maleHands, "runtime.maleHands", issues);
        ValidateDuplicateKeys(runtime.femaleHands, "runtime.femaleHands", issues);
        ValidateDuplicateKeys(runtime.maleLegs, "runtime.maleLegs", issues);
        ValidateDuplicateKeys(runtime.femaleLegs, "runtime.femaleLegs", issues);
        ValidateDuplicateKeys(runtime.maleFeet, "runtime.maleFeet", issues);
        ValidateDuplicateKeys(runtime.femaleFeet, "runtime.femaleFeet", issues);
        ValidateDuplicateKeys(runtime.maleBaseBodies, "runtime.maleBaseBodies", issues);
        ValidateDuplicateKeys(runtime.femaleBaseBodies, "runtime.femaleBaseBodies", issues);
        ValidateDuplicateKeys(runtime.weapons, "runtime.weapons", issues);

        ValidateCrossGenderLeak(runtime.maleHair, runtime.femaleToken, "runtime.maleHair", issues);
        ValidateCrossGenderLeak(runtime.femaleHair, runtime.maleToken, "runtime.femaleHair", issues);
        ValidateCrossGenderLeak(runtime.maleFace, runtime.femaleToken, "runtime.maleFace", issues);
        ValidateCrossGenderLeak(runtime.femaleFace, runtime.maleToken, "runtime.femaleFace", issues);

        ValidateNullPrefabRefs(runtime.maleHairPrefabs, "runtime.maleHairPrefabs", issues);
        ValidateNullPrefabRefs(runtime.femaleHairPrefabs, "runtime.femaleHairPrefabs", issues);
        ValidateNullPrefabRefs(runtime.maleFacePrefabs, "runtime.maleFacePrefabs", issues);
        ValidateNullPrefabRefs(runtime.femaleFacePrefabs, "runtime.femaleFacePrefabs", issues);
        ValidateNullPrefabRefs(runtime.maleHeadPrefabs, "runtime.maleHeadPrefabs", issues);
        ValidateNullPrefabRefs(runtime.femaleHeadPrefabs, "runtime.femaleHeadPrefabs", issues);
        ValidateNullPrefabRefs(runtime.weaponPrefabs, "runtime.weaponPrefabs", issues);

        if (string.IsNullOrWhiteSpace(runtime.labelHair) ||
            string.IsNullOrWhiteSpace(runtime.labelFace) ||
            string.IsNullOrWhiteSpace(runtime.labelEquipment) ||
            string.IsNullOrWhiteSpace(runtime.labelWeapons) ||
            string.IsNullOrWhiteSpace(runtime.labelBody))
        {
            issues.Add("Runtime catalog labels are not fully configured.");
        }
    }

    private static void ValidatePresetSchemaAndPortableWiring(List<string> issues)
    {
        ValidatePresetSchema(issues);
        const string verticalSliceScenePath = "Assets/Scenes/CharacterVerticalSlice.unity";
        if (File.Exists(verticalSliceScenePath) && !CharacterVerticalSliceSceneBuilder.Validate(showDialog: false))
            issues.Add("CharacterVerticalSlice scene validation failed.");
        ValidatePortableWiring(issues);
    }

    private static void ValidatePresetSchema(List<string> issues)
    {
        if (CharacterPresetData.CurrentVersion <= 0)
            issues.Add("CharacterPresetData.CurrentVersion must be > 0.");

        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Head", "Body", "Hands", "Legs", "Feet", "Weapon"
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < CharacterPresetData.KnownSlots.Length; i++)
        {
            string slot = CharacterPresetData.KnownSlots[i];
            if (string.IsNullOrWhiteSpace(slot))
            {
                issues.Add($"CharacterPresetData.KnownSlots contains empty value at index {i}.");
                continue;
            }

            if (!seen.Add(slot))
                issues.Add($"CharacterPresetData.KnownSlots contains duplicate slot '{slot}'.");
        }

        foreach (var slot in required)
        {
            if (!seen.Contains(slot))
                issues.Add($"CharacterPresetData.KnownSlots is missing required slot '{slot}'.");
        }
    }

    private static void ValidatePortableWiring(List<string> issues)
    {
        var facades = UnityEngine.Object.FindObjectsOfType<CharacterCustomizationFacade>(true);
        for (int i = 0; i < facades.Length; i++)
        {
            var so = new SerializedObject(facades[i]);
            if (so.FindProperty("characterCreator").objectReferenceValue == null)
                issues.Add("CharacterCustomizationFacade.characterCreator is not wired.");
            if (so.FindProperty("equipmentSystem").objectReferenceValue == null)
                issues.Add("CharacterCustomizationFacade.equipmentSystem is not wired.");
        }

        var mounts = UnityEngine.Object.FindObjectsOfType<CharacterAvatarMount>(true);
        for (int i = 0; i < mounts.Length; i++)
        {
            var so = new SerializedObject(mounts[i]);
            if (so.FindProperty("characterCreator").objectReferenceValue == null)
                issues.Add("CharacterAvatarMount.characterCreator is not wired.");
            if (so.FindProperty("equipmentSystem").objectReferenceValue == null)
                issues.Add("CharacterAvatarMount.equipmentSystem is not wired.");
            if (so.FindProperty("hostAdapter").objectReferenceValue == null)
                issues.Add("CharacterAvatarMount.hostAdapter is not wired.");
        }

        var hosts = UnityEngine.Object.FindObjectsOfType<CharacterControllerAvatarHost>(true);
        for (int i = 0; i < hosts.Length; i++)
        {
            var so = new SerializedObject(hosts[i]);
            if (so.FindProperty("avatarAnchor").objectReferenceValue == null)
                issues.Add("CharacterControllerAvatarHost.avatarAnchor is not wired.");
        }
    }

    private static void CompareCounts(string label, int runtimeCount, int legacyCount, List<string> issues)
    {
        if (runtimeCount != legacyCount)
            issues.Add($"Count mismatch for {label}: runtime={runtimeCount}, legacy={legacyCount}.");
    }

    private static void ValidateDuplicateKeys(List<string> values, string label, List<string> issues)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add($"{label} contains empty key at index {i}.");
                continue;
            }

            if (!set.Add(value))
                issues.Add($"{label} contains duplicate key '{value}'.");
        }
    }

    private static void ValidateCrossGenderLeak(List<string> values, string oppositeToken, string label, List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(oppositeToken))
            return;

        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (value.IndexOf(oppositeToken, StringComparison.OrdinalIgnoreCase) >= 0)
                issues.Add($"{label} includes opposite-gender token '{oppositeToken}' in '{value}'.");
        }
    }

    private static void ValidateNullPrefabRefs(List<GameObject> prefabs, string label, List<string> issues)
    {
        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == null)
                issues.Add($"{label} contains null prefab reference at index {i}.");
        }
    }

    private static void ValidateRequiredNonEmptyKeys(List<string> values, string label, List<string> issues)
    {
        if (values == null || values.Count == 0)
            issues.Add($"{label} must contain at least one key.");
    }

    private static int ScanForFbx(DateTime startUtc, int stepIndex, int totalSteps, out List<string> samplePaths)
    {
        samplePaths = new List<string>();
        string assetsPath = Application.dataPath;
        int count = 0;
        int scanned = 0;

        foreach (var path in Directory.EnumerateFiles(assetsPath, "*.*", SearchOption.AllDirectories))
        {
            scanned++;
            if (scanned % 500 == 0)
                ThrowIfCanceled(startUtc, stepIndex, totalSteps, $"Scanning Assets for FBX files ({scanned:n0} checked)");

            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                continue;

            count++;
            if (samplePaths.Count < MaxLoggedFbx)
                samplePaths.Add(ToAssetRelativePath(path));
        }

        return count;
    }

    private static string ToAssetRelativePath(string absolutePath)
    {
        string assetsPath = Application.dataPath;
        if (!absolutePath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
            return absolutePath.Replace('\\', '/');

        string relative = "Assets" + absolutePath.Substring(assetsPath.Length);
        return relative.Replace('\\', '/');
    }
}
