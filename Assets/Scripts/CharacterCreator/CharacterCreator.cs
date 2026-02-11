using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

[DisallowMultipleComponent]
public class CharacterCreator : MonoBehaviour
{
    public enum Gender
    {
        Male,
        Female
    }

    public enum Category
    {
        Hair,
        Face
    }

    [Header("Root")]
    [SerializeField] private Transform characterRoot;
    [SerializeField] private bool autoCreateCharacterRoot = true;
    [SerializeField] private string characterRootName = "CharacterRoot";
    [SerializeField] private bool parentScanRootsToCharacterRoot = true;

    [Header("Auto Collect (Scene)")]
    [SerializeField] private bool autoCollectFromScene = true;
    [SerializeField] private Transform[] scanRoots = Array.Empty<Transform>();
    [SerializeField] private bool autoFindScanRootByPrefix = true;
    [SerializeField] private string scanRootNamePrefix = "FBX_Instances_FFXIV";
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private string maleToken = "c0101";
    [SerializeField] private string femaleToken = "c0201";
    [SerializeField] private string hairToken = "_hir";
    [SerializeField] private string faceToken = "_fac";
    [SerializeField] private string[] bodyTokens = { "_dwn", "_glv", "_sho", "_top" };
    [SerializeField] private string[] excludeNameTokens = { " group", " part" };
    [SerializeField] private bool logScanResults = true;

    [Header("Scene Cleanup")]
    [SerializeField] private bool cleanupSceneObjectsWhenNotUsingSceneLists = true;

    [Header("Debug")]
    [SerializeField] private bool logSceneCleanup;
    [SerializeField] private bool logToFile;
    [SerializeField] private string logFileName = "CharacterCreator-debug.log";

    [Header("Runtime Cleanup")]
    [SerializeField] private bool runtimeCleanupPoll = true;
    [SerializeField] private float runtimeCleanupPollDurationSeconds = 5f;
    [SerializeField] private float runtimeCleanupPollIntervalSeconds = 0.5f;
    [SerializeField] private bool activateNewInstances = false;

    [Header("Force Hide")]
    [SerializeField] private bool forceHideOppositeGenderObjects = true;

    [Header("Prefab Catalog (Scene-Based)")]
    [SerializeField] private FfxivPrefabCatalog prefabCatalog;
    [SerializeField] private bool usePrefabCatalog = true;

    [Header("Male Options")]
    [SerializeField] private List<GameObject> maleHairs = new();
    [SerializeField] private List<GameObject> maleFaces = new();

    [Header("Base Models")]
    [SerializeField] private List<GameObject> maleBases = new();
    [SerializeField] private List<GameObject> femaleBases = new();

    [Header("Female Options")]
    [SerializeField] private List<GameObject> femaleHairs = new();
    [SerializeField] private List<GameObject> femaleFaces = new();

    [Header("Selection")]
    [SerializeField] private Gender gender = Gender.Male;
    [SerializeField] private int maleHairIndex;
    [SerializeField] private int maleFaceIndex;
    [SerializeField] private int femaleHairIndex;
    [SerializeField] private int femaleFaceIndex;

    [Header("Base Bodies")]
    [SerializeField] private bool manageBaseBodies = true;

    [Header("Hair Visibility")]
    [SerializeField] private bool allowExternalHairVisibilityOverride = true;
    [SerializeField] private bool hairVisibleOverride = true;

    [Header("Filters")]
    [SerializeField] private Category category = Category.Hair;
    [SerializeField] private string filterText = string.Empty;

    public event Action Changed;
    public bool LogSceneCleanup => logSceneCleanup;

    [Header("Addressables (Hair/Face)")]
    [SerializeField] private bool useAddressablesForHairFace = true;
    [SerializeField] private bool loadAddressablesOnAwake = true;
    [SerializeField] private string addressablesLabelHair = "Hair";
    [SerializeField] private string addressablesLabelFace = "Face";
    [SerializeField] private float addressablesLoadTimeoutSeconds = 10f;
    [SerializeField] private bool logAddressables = true;

    [Header("Addressables (Body)")]
    [SerializeField] private bool useAddressablesForBodies = true;
    [SerializeField] private string addressablesLabelBody = "Body";
    [SerializeField] private string bodyBaseToken = "e0000";

    [Header("Addressable Index")]
    [SerializeField] private ScriptableObject addressableIndex;
    [SerializeField] private bool useAddressableIndex = true;

    private readonly List<string> maleHairKeys = new();
    private readonly List<string> maleFaceKeys = new();
    private readonly List<string> femaleHairKeys = new();
    private readonly List<string> femaleFaceKeys = new();
    private readonly List<string> maleBodyKeys = new();
    private readonly List<string> femaleBodyKeys = new();
    private readonly HashSet<string> maleBodyPendingKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> femaleBodyPendingKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<GameObject> hiddenMaleObjects = new();
    private readonly HashSet<GameObject> hiddenFemaleObjects = new();
    private bool indexKeysLoaded;
    private Coroutine runtimeCleanupCoroutine;
    private bool scanRootsCached;
    private Transform[] cachedScanRoots = Array.Empty<Transform>();
#if ENABLE_ADDRESSABLES
    private AsyncOperationHandle<IList<GameObject>> hairLoadHandle;
    private AsyncOperationHandle<IList<GameObject>> faceLoadHandle;
    private AsyncOperationHandle<IList<GameObject>> bodyLoadHandle;
    private bool hairHandleValid;
    private bool faceHandleValid;
    private bool bodyHandleValid;
#endif
    private GameObject maleHairInstance;
    private GameObject maleFaceInstance;
    private GameObject femaleHairInstance;
    private GameObject femaleFaceInstance;
    private readonly List<GameObject> maleBodyInstances = new();
    private readonly List<GameObject> femaleBodyInstances = new();

    public Gender CurrentGender => gender;
    public Category CurrentCategory => category;
    public string FilterText => filterText;
    public int CurrentHairIndex => GetHairIndex(gender);
    public int CurrentFaceIndex => GetFaceIndex(gender);
    public Transform CharacterRoot => characterRoot != null ? characterRoot : ResolveCharacterRoot();
    public bool ManageBaseBodies => manageBaseBodies;
    public bool HairVisibleOverride => hairVisibleOverride;

    public int GetHairCount(Gender forGender) => GetOptionCount(Category.Hair, forGender);
    public int GetFaceCount(Gender forGender) => GetOptionCount(Category.Face, forGender);

    public string GetCurrentHairName() => GetItemName(GetOptionNames(Category.Hair, gender), CurrentHairIndex);
    public string GetCurrentFaceName() => GetItemName(GetOptionNames(Category.Face, gender), CurrentFaceIndex);
    public string GetCurrentOptionName() => GetItemName(GetOptionNames(category, gender), GetOptionIndex(category, gender));

    public int GetFilteredCount(Category forCategory) =>
        GetFilteredIndices(GetOptionNames(forCategory, gender), filterText).Count;

    public int GetFilteredPosition(Category forCategory)
    {
        var list = GetOptionNames(forCategory, gender);
        var indices = GetFilteredIndices(list, filterText);
        if (indices.Count == 0)
            return 0;

        int current = GetOptionIndex(forCategory, gender);
        for (int i = 0; i < indices.Count; i++)
        {
            if (indices[i] == current)
                return i + 1;
        }

        return 0;
    }

    private void Awake()
    {
        EnsureCharacterRoot();

        if (usePrefabCatalog)
            ApplyPrefabCatalog();
        else if (autoCollectFromScene)
            AutoCollectFromScene();

        if (loadAddressablesOnAwake && (useAddressablesForHairFace || (useAddressablesForBodies && manageBaseBodies)))
            LoadAddressables();

        ApplySelection();
        StartRuntimeCleanupPoll();
    }

    private void OnEnable()
    {
        StartRuntimeCleanupPoll();
    }

    private void OnDisable()
    {
        StopRuntimeCleanupPoll();
    }

    private void OnDestroy()
    {
        ReleaseAddressables();
    }

    private void OnValidate()
    {
        ClampIndices();
    }

    private void StartRuntimeCleanupPoll()
    {
        if (!cleanupSceneObjectsWhenNotUsingSceneLists || !runtimeCleanupPoll)
            return;

        if (runtimeCleanupCoroutine != null)
            StopCoroutine(runtimeCleanupCoroutine);

        runtimeCleanupCoroutine = StartCoroutine(RuntimeCleanupPoll());
    }

    private void StopRuntimeCleanupPoll()
    {
        if (runtimeCleanupCoroutine == null)
            return;

        StopCoroutine(runtimeCleanupCoroutine);
        runtimeCleanupCoroutine = null;
    }

    private IEnumerator RuntimeCleanupPoll()
    {
        float duration = Mathf.Max(0f, runtimeCleanupPollDurationSeconds);
        float interval = Mathf.Max(0.05f, runtimeCleanupPollIntervalSeconds);
        if (duration <= 0f)
            yield break;

        float endTime = Time.realtimeSinceStartup + duration;
        int iteration = 0;

        if (logSceneCleanup)
            Debug.Log($"[CharacterCreator] Runtime cleanup poll started. Duration={duration:F1}s Interval={interval:F2}s.", this);

        while (Time.realtimeSinceStartup < endTime)
        {
            iteration++;
            scanRootsCached = false;
            cachedScanRoots = Array.Empty<Transform>();

            if (autoCollectFromScene && !usePrefabCatalog)
                AutoCollectFromScene();
            else
                CleanupSceneObjectsIfNeeded();

            yield return new WaitForSeconds(interval);
        }

        if (logSceneCleanup)
            Debug.Log($"[CharacterCreator] Runtime cleanup poll finished. Iterations={iteration}.", this);

        runtimeCleanupCoroutine = null;
    }

    public void SetGender(Gender newGender)
    {
        if (gender != newGender)
            gender = newGender;

        ApplySelection();
    }

    public void SetCategory(Category newCategory)
    {
        if (category != newCategory)
            category = newCategory;

        ApplySelection();
    }

    public void SetFilterText(string value)
    {
        filterText = value ?? string.Empty;
        ApplySelection();
    }

    public void SetHairVisibilityOverride(bool visible)
    {
        if (!allowExternalHairVisibilityOverride)
            return;

        if (hairVisibleOverride == visible)
            return;

        hairVisibleOverride = visible;
        ApplySelection();
    }

    public void SetManageBaseBodies(bool enabled)
    {
        if (manageBaseBodies == enabled)
            return;

        manageBaseBodies = enabled;
        ApplySelection();
    }

    public void SetAddressableIndex(ScriptableObject index)
    {
        addressableIndex = index;
        indexKeysLoaded = false;
        ApplySelection();
    }

    public void SetHairIndex(int index)
    {
        SetOptionIndexForCurrentGender(Category.Hair, index);

        ApplySelection();
    }

    public void SetFaceIndex(int index)
    {
        SetOptionIndexForCurrentGender(Category.Face, index);

        ApplySelection();
    }

    public void NextHair()
    {
        StepOptionUnfiltered(Category.Hair, 1);
    }

    public void PreviousHair()
    {
        StepOptionUnfiltered(Category.Hair, -1);
    }

    public void NextFace()
    {
        StepOptionUnfiltered(Category.Face, 1);
    }

    public void PreviousFace()
    {
        StepOptionUnfiltered(Category.Face, -1);
    }

    public void NextOption()
    {
        StepOption(1);
    }

    public void PreviousOption()
    {
        StepOption(-1);
    }

    public void NextHairFiltered()
    {
        StepOption(Category.Hair, 1);
    }

    public void PreviousHairFiltered()
    {
        StepOption(Category.Hair, -1);
    }

    public void NextFaceFiltered()
    {
        StepOption(Category.Face, 1);
    }

    public void PreviousFaceFiltered()
    {
        StepOption(Category.Face, -1);
    }

    [ContextMenu("Rebuild Options From Scene")]
    public void RebuildFromScene()
    {
        if (usePrefabCatalog)
            ApplyPrefabCatalog();
        else
            AutoCollectFromScene();
        ApplySelection();
    }

    private void ApplyPrefabCatalog()
    {
        if (!usePrefabCatalog)
            return;

        if (prefabCatalog == null)
            prefabCatalog = Resources.Load<FfxivPrefabCatalog>("FfxivPrefabCatalog");
        if (prefabCatalog == null)
            return;

        maleHairs.Clear();
        maleFaces.Clear();
        femaleHairs.Clear();
        femaleFaces.Clear();
        maleBases.Clear();
        femaleBases.Clear();

        AddRange(maleHairs, prefabCatalog.maleHair);
        AddRange(femaleHairs, prefabCatalog.femaleHair);
        AddRange(maleFaces, prefabCatalog.maleFace);
        AddRange(femaleFaces, prefabCatalog.femaleFace);
        AddRange(maleBases, prefabCatalog.maleBaseBodies);
        AddRange(femaleBases, prefabCatalog.femaleBaseBodies);

        SortByName(maleHairs);
        SortByName(femaleHairs);
        SortByName(maleFaces);
        SortByName(femaleFaces);
        SortByName(maleBases);
        SortByName(femaleBases);
    }

    private static void AddRange(List<GameObject> target, List<GameObject> source)
    {
        if (target == null || source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (item != null && !target.Contains(item))
                target.Add(item);
        }
    }

    [ContextMenu("Load Addressables (Hair/Face/Body)")]
    public void LoadAddressables()
    {
        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            ApplySelection();
            return;
        }

        if (!useAddressablesForHairFace && !useAddressablesForBodies)
            return;

        _ = addressablesLoadTimeoutSeconds;

        ReleaseAddressables();

#if ENABLE_ADDRESSABLES

        if (useAddressablesForHairFace && !string.IsNullOrWhiteSpace(addressablesLabelHair))
        {
            hairLoadHandle = Addressables.LoadAssetsAsync<GameObject>(addressablesLabelHair, null);
            hairHandleValid = true;
            hairLoadHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    UpdateAddressableKeys(handle.Result, Category.Hair);
                ApplySelection();
            };
        }

        if (useAddressablesForHairFace && !string.IsNullOrWhiteSpace(addressablesLabelFace))
        {
            faceLoadHandle = Addressables.LoadAssetsAsync<GameObject>(addressablesLabelFace, null);
            faceHandleValid = true;
            faceLoadHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    UpdateAddressableKeys(handle.Result, Category.Face);
                ApplySelection();
            };
        }

        if (manageBaseBodies && useAddressablesForBodies && !string.IsNullOrWhiteSpace(addressablesLabelBody))
        {
            bodyLoadHandle = Addressables.LoadAssetsAsync<GameObject>(addressablesLabelBody, null);
            bodyHandleValid = true;
            bodyLoadHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    UpdateBodyAddressableKeys(handle.Result);
                EnsureBaseBodyInstances(gender);
                ApplySelection();
            };
        }
#else
        if (logAddressables)
            Debug.LogWarning("[CharacterCreator] Addressables package not available. Install Addressables to enable hair/face/body loading.");
#endif
    }

    private void ApplySelection()
    {
        EnsureCharacterRoot();
        ClampIndices();
        EnsureSelectionMatchesFilter(gender);

        if (characterRoot != null && !characterRoot.gameObject.activeSelf)
            characterRoot.gameObject.SetActive(true);

        if (logSceneCleanup)
        {
            Debug.Log(
                $"[CharacterCreator] ApplySelection start. Gender={gender} UsePrefabCatalog={usePrefabCatalog} UseAddressablesHairFace={useAddressablesForHairFace} ManageBaseBodies={manageBaseBodies} UseAddressablesBodies={useAddressablesForBodies} HairVisibleOverride={hairVisibleOverride}.",
                this
            );
        }

        LogActiveSnapshot("Before cleanup");
        ForceHideOppositeGenderObjects();
        CleanupSceneObjectsIfNeeded();
        LogActiveSnapshot("After cleanup");

        if (manageBaseBodies)
        {
            if (useAddressablesForBodies)
            {
                ApplyAddressableBodies();
                LogActiveSnapshot("After ApplyAddressableBodies");
                if (!usePrefabCatalog)
                {
                    ApplyGroup(maleBases, false);
                    ApplyGroup(femaleBases, false);
                    LogActiveSnapshot("After ApplyGroup base bodies (addressables)");
                }
            }
            else if (usePrefabCatalog)
            {
                ApplyCatalogBodies();
                LogActiveSnapshot("After ApplyCatalogBodies");
            }
            else
            {
                ApplyGroup(maleBases, gender == Gender.Male);
                ApplyGroup(femaleBases, gender == Gender.Female);
                LogActiveSnapshot("After ApplyGroup base bodies (scene)");
            }
        }

        if (useAddressablesForHairFace)
        {
            ApplyAddressableSelection();
            LogActiveSnapshot("After ApplyAddressableSelection");
        }
        else if (usePrefabCatalog)
        {
            InstantiateCatalogSelection();
            LogActiveSnapshot("After InstantiateCatalogSelection");
        }
        else
        {
            ApplyList(maleHairs, gender == Gender.Male ? maleHairIndex : -1);
            ApplyList(maleFaces, gender == Gender.Male ? maleFaceIndex : -1);
            ApplyList(femaleHairs, gender == Gender.Female ? femaleHairIndex : -1);
            ApplyList(femaleFaces, gender == Gender.Female ? femaleFaceIndex : -1);
            LogActiveSnapshot("After ApplyList hair/face (scene)");
        }

        if (manageBaseBodies && !useAddressablesForBodies && !usePrefabCatalog)
        {
            SetInstanceActive(maleBodyInstances, false);
            SetInstanceActive(femaleBodyInstances, false);
            LogActiveSnapshot("After SetInstanceActive base bodies (scene)");
        }

        ApplyHairVisibilityOverride();
        LogActiveSnapshot("After ApplyHairVisibilityOverride");

        Changed?.Invoke();
    }

    private void InstantiateCatalogSelection()
    {
        if (!usePrefabCatalog)
            return;

        string hairKey = GetCurrentHairName();
        string faceKey = GetCurrentFaceName();

        if (gender == Gender.Male)
        {
            SwapCatalogInstance(maleHairInstance, instance => maleHairInstance = instance, hairKey);
            SwapCatalogInstance(maleFaceInstance, instance => maleFaceInstance = instance, faceKey);

            SetInstanceActive(femaleHairInstance, false);
            SetInstanceActive(femaleFaceInstance, false);
        }
        else
        {
            SwapCatalogInstance(femaleHairInstance, instance => femaleHairInstance = instance, hairKey);
            SwapCatalogInstance(femaleFaceInstance, instance => femaleFaceInstance = instance, faceKey);

            SetInstanceActive(maleHairInstance, false);
            SetInstanceActive(maleFaceInstance, false);
        }

        if (logSceneCleanup || logToFile)
        {
            string maleHairState = maleHairInstance != null ? $"{maleHairInstance.name} active={maleHairInstance.activeSelf}" : "<none>";
            string maleFaceState = maleFaceInstance != null ? $"{maleFaceInstance.name} active={maleFaceInstance.activeSelf}" : "<none>";
            string femaleHairState = femaleHairInstance != null ? $"{femaleHairInstance.name} active={femaleHairInstance.activeSelf}" : "<none>";
            string femaleFaceState = femaleFaceInstance != null ? $"{femaleFaceInstance.name} active={femaleFaceInstance.activeSelf}" : "<none>";

            LogDiagnostics(
                $"[CharacterCreator] InstantiateCatalogSelection hairKey='{hairKey}' faceKey='{faceKey}' HairVisibleOverride={hairVisibleOverride} " +
                $"MaleHair={maleHairState} MaleFace={maleFaceState} FemaleHair={femaleHairState} FemaleFace={femaleFaceState}."
            );
        }
    }

    private void SwapCatalogInstance(GameObject currentInstance, Action<GameObject> assignInstance, string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
        {
            if (currentInstance != null)
                currentInstance.SetActive(false);
            return;
        }

        if (currentInstance != null && string.Equals(currentInstance.name, prefabName, StringComparison.OrdinalIgnoreCase))
        {
            currentInstance.SetActive(true);
            return;
        }

        if (currentInstance != null)
        {
            UnityEngine.Object.Destroy(currentInstance);
            assignInstance?.Invoke(null);
        }

        var prefab = FindPrefabByName(prefabName);
        if (prefab == null)
            return;

        var instance = UnityEngine.Object.Instantiate(prefab, characterRoot != null ? characterRoot : transform);
        instance.name = prefabName;
        instance.SetActive(true);
        assignInstance?.Invoke(instance);
    }

    private GameObject FindPrefabByName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return null;

        var list = gender == Gender.Male ? GetHairList(Gender.Male) : GetHairList(Gender.Female);
        for (int i = 0; i < list.Count; i++)
        {
            var prefab = list[i];
            if (prefab != null && string.Equals(prefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
                return prefab;
        }

        list = gender == Gender.Male ? GetFaceList(Gender.Male) : GetFaceList(Gender.Female);
        for (int i = 0; i < list.Count; i++)
        {
            var prefab = list[i];
            if (prefab != null && string.Equals(prefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
                return prefab;
        }

        return null;
    }

    private void ApplyCatalogBodies()
    {
        if (!usePrefabCatalog)
            return;

        if (gender == Gender.Male)
        {
            EnsureCatalogBodyInstances(Gender.Male);
            SetInstanceActive(maleBodyInstances, true);
            ReleaseCatalogBodyInstances(femaleBodyInstances, femaleBodyPendingKeys);
        }
        else
        {
            EnsureCatalogBodyInstances(Gender.Female);
            SetInstanceActive(femaleBodyInstances, true);
            ReleaseCatalogBodyInstances(maleBodyInstances, maleBodyPendingKeys);
        }
    }

    private void EnsureCatalogBodyInstances(Gender forGender)
    {
        EnsureCharacterRoot();

        var prefabs = forGender == Gender.Male ? maleBases : femaleBases;
        var instances = forGender == Gender.Male ? maleBodyInstances : femaleBodyInstances;
        var pending = forGender == Gender.Male ? maleBodyPendingKeys : femaleBodyPendingKeys;

        if (prefabs.Count == 0)
        {
            if (logAddressables)
                Debug.LogWarning($"[CharacterCreator] No base body prefabs found for {forGender}.", this);
            return;
        }

        ReleaseCatalogBodyInstancesNotInPrefabs(instances, pending, prefabs);

        for (int i = 0; i < prefabs.Count; i++)
        {
            var prefab = prefabs[i];
            if (prefab == null)
                continue;

            string key = prefab.name;
            if (HasBodyInstance(instances, key) || pending.Contains(key))
                continue;

            pending.Add(key);

            var instance = UnityEngine.Object.Instantiate(prefab, characterRoot != null ? characterRoot : transform);
            instance.name = key;
            instance.SetActive(false);
            instances.Add(instance);
            if (activateNewInstances)
                SetInstanceActive(instance, gender == forGender);
            pending.Remove(key);
        }
    }

    private static void ReleaseCatalogBodyInstancesNotInPrefabs(List<GameObject> instances, HashSet<string> pending, List<GameObject> prefabs)
    {
        if (instances == null)
            return;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < prefabs.Count; i++)
        {
            var prefab = prefabs[i];
            if (prefab != null)
                names.Add(prefab.name);
        }

        for (int i = instances.Count - 1; i >= 0; i--)
        {
            var instance = instances[i];
            if (instance == null || !names.Contains(instance.name))
            {
                if (instance != null)
                    UnityEngine.Object.Destroy(instance);
                instances.RemoveAt(i);
            }
        }

        pending?.RemoveWhere(key => !names.Contains(key));
    }

    private static void ReleaseCatalogBodyInstances(List<GameObject> instances, HashSet<string> pending)
    {
        if (instances == null)
            return;

        for (int i = instances.Count - 1; i >= 0; i--)
        {
            var instance = instances[i];
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
            instances.RemoveAt(i);
        }

        pending?.Clear();
    }

    private void ApplyHairVisibilityOverride()
    {
        if (!allowExternalHairVisibilityOverride)
            return;

        bool show = hairVisibleOverride;
        if (gender == Gender.Male)
        {
            if (maleHairInstance != null)
                maleHairInstance.SetActive(show);
            if (!useAddressablesForHairFace && !usePrefabCatalog)
                ApplyList(maleHairs, show ? maleHairIndex : -1);
        }
        else
        {
            if (femaleHairInstance != null)
                femaleHairInstance.SetActive(show);
            if (!useAddressablesForHairFace && !usePrefabCatalog)
                ApplyList(femaleHairs, show ? femaleHairIndex : -1);
        }
    }

    private void CleanupSceneObjectsIfNeeded()
    {
        if (!cleanupSceneObjectsWhenNotUsingSceneLists)
        {
            if (logSceneCleanup)
                Debug.Log("[CharacterCreator] Scene cleanup skipped (toggle disabled).", this);
            return;
        }

        bool usingSceneHairFace = !useAddressablesForHairFace && !usePrefabCatalog;
        bool usingSceneBodies = manageBaseBodies && !useAddressablesForBodies && !usePrefabCatalog;

        if (usingSceneHairFace && usingSceneBodies)
        {
            if (logSceneCleanup)
                Debug.Log("[CharacterCreator] Scene cleanup skipped (using scene lists for hair/face and base bodies).", this);
            return;
        }

        bool cleanupHairFace = !usingSceneHairFace;
        bool cleanupBodies = manageBaseBodies && !usingSceneBodies;

        Transform[] roots = ResolveScanRootsForCleanup();
        if (roots == null || roots.Length == 0)
        {
            if (logSceneCleanup)
                Debug.Log("[CharacterCreator] Scene cleanup skipped (no scan roots).", this);
            return;
        }

        float startTime = logSceneCleanup ? Time.realtimeSinceStartup : 0f;
        int scanned = 0;
        int renderable = 0;
        int excluded = 0;
        int hairMatches = 0;
        int faceMatches = 0;
        int baseMatches = 0;
        int deactivated = 0;
        int unmatchedLogged = 0;
        int matchedLogged = 0;
        const int sampleLimit = 10;

        foreach (var root in roots)
        {
            if (root == null)
                continue;

            foreach (var child in root.GetComponentsInChildren<Transform>(includeInactive))
            {
                if (child == null)
                    continue;

                scanned++;

                var go = child.gameObject;
                if (go == null || (characterRoot != null && go == characterRoot.gameObject))
                    continue;

                if (!HasRenderable(go))
                    continue;

                renderable++;

                string name = go.name;
                if (HasExcludedToken(name))
                {
                    excluded++;
                    continue;
                }

                bool isHair = MatchesToken(name, hairToken);
                bool isFace = MatchesToken(name, faceToken);
                bool isBaseBody = MatchesToken(name, bodyBaseToken) && MatchesAnyToken(name, bodyTokens);

                if (isHair)
                    hairMatches++;
                if (isFace)
                    faceMatches++;
                if (isBaseBody)
                    baseMatches++;

                if (logSceneCleanup && matchedLogged < sampleLimit && (isHair || isFace || isBaseBody))
                {
                    string rootName = child.root != null ? child.root.name : "<null>";
                    Debug.Log(
                        $"[CharacterCreator] Cleanup match sample: '{name}' Root='{rootName}' Hair={isHair} Face={isFace} BaseBody={isBaseBody}.",
                        this
                    );
                    matchedLogged++;
                }

                if (logSceneCleanup && unmatchedLogged < sampleLimit && !isHair && !isFace && !isBaseBody)
                {
                    string rootName = child.root != null ? child.root.name : "<null>";
                    Debug.Log(
                        $"[CharacterCreator] Cleanup non-match sample: '{name}' Root='{rootName}'.",
                        this
                    );
                    unmatchedLogged++;
                }

                if ((cleanupHairFace && (isHair || isFace)) || (cleanupBodies && isBaseBody))
                {
                    if (go.activeSelf)
                        deactivated++;
                    go.SetActive(false);
                }
            }
        }

        if (logSceneCleanup)
        {
            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            string rootNames = roots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(roots, r => r != null ? r.name : "<null>"));
            Debug.Log(
                $"[CharacterCreator] Scene cleanup done. Roots={rootNames}. Scanned={scanned}, Renderable={renderable}, Excluded={excluded}, HairMatches={hairMatches}, FaceMatches={faceMatches}, BaseMatches={baseMatches}, Deactivated={deactivated}, CleanupHairFace={cleanupHairFace}, CleanupBodies={cleanupBodies}, TimeMs={elapsedMs:F1}.",
                this
            );
        }
    }

    private void ForceHideOppositeGenderObjects()
    {
        if (!forceHideOppositeGenderObjects)
            return;

        EnsureCharacterRoot();

        RestoreHiddenObjectsForGender(gender);

        Transform[] roots = ResolveScanRoots();
        if (roots == null || roots.Length == 0)
            return;

        bool hideMale = gender == Gender.Female;
        bool hideFemale = gender == Gender.Male;

        foreach (var root in roots)
        {
            if (root == null)
                continue;

            foreach (var child in root.GetComponentsInChildren<Transform>(includeInactive))
            {
                if (child == null)
                    continue;

                var go = child.gameObject;
                if (go == null || (characterRoot != null && go == characterRoot.gameObject))
                    continue;

                if (!HasRenderable(go))
                    continue;

                bool isMale = MatchesTokenInHierarchy(child, maleToken);
                bool isFemale = MatchesTokenInHierarchy(child, femaleToken);

                if (isMale && isFemale)
                    continue;

                if ((hideMale && isMale) || (hideFemale && isFemale))
                {
                    if (go.activeSelf)
                    {
                        if (isMale)
                            hiddenMaleObjects.Add(go);
                        else if (isFemale)
                            hiddenFemaleObjects.Add(go);
                    }

                    go.SetActive(false);
                }
            }
        }
    }

    private void RestoreHiddenObjectsForGender(Gender forGender)
    {
        var hidden = forGender == Gender.Male ? hiddenMaleObjects : hiddenFemaleObjects;
        if (hidden.Count == 0)
            return;

        var toRestore = new List<GameObject>(hidden);
        hidden.Clear();

        foreach (var item in toRestore)
        {
            if (item == null)
                continue;

            EnsureActiveHierarchy(item);
            item.SetActive(true);
        }
    }

    private Transform[] ResolveScanRootsForCleanup()
    {
        var roots = ResolveScanRoots();
        bool onlySelf = roots.Length == 1 && roots[0] == transform;
        bool onlyCharacter = roots.Length == 1 && roots[0] == characterRoot;
        bool onlySelfAndCharacter = characterRoot != null && roots.Length == 2
            && ((roots[0] == transform && roots[1] == characterRoot) || (roots[0] == characterRoot && roots[1] == transform));

        if (autoFindScanRootByPrefix && !string.IsNullOrWhiteSpace(scanRootNamePrefix) && parentScanRootsToCharacterRoot && characterRoot != null)
        {
            if (onlySelf || onlyCharacter || onlySelfAndCharacter)
            {
                var matches = new List<Transform>();
                foreach (var child in characterRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && child.name.StartsWith(scanRootNamePrefix, StringComparison.OrdinalIgnoreCase))
                        matches.Add(child);
                }

                if (matches.Count > 0)
                    roots = matches.ToArray();
            }
        }

        if (characterRoot != null && characterRoot != transform)
        {
            var merged = new List<Transform>(roots.Length + 1);
            var seen = new HashSet<Transform>();
            foreach (var root in roots)
            {
                if (root != null && seen.Add(root))
                    merged.Add(root);
            }

            if (seen.Add(characterRoot))
            {
                merged.Add(characterRoot);
                if (logSceneCleanup)
                    Debug.Log($"[CharacterCreator] ResolveScanRootsForCleanup -> added characterRoot '{characterRoot.name}'. Count={merged.Count}.", this);
            }

            roots = merged.ToArray();
        }

        return roots;
    }

    private void AutoCollectFromScene()
    {
        EnsureCharacterRoot();
        maleHairs.Clear();
        maleFaces.Clear();
        femaleHairs.Clear();
        femaleFaces.Clear();
        maleBases.Clear();
        femaleBases.Clear();

        maleHairKeys.Clear();
        maleFaceKeys.Clear();
        femaleHairKeys.Clear();
        femaleFaceKeys.Clear();

        Transform[] roots = ResolveScanRoots();

        float startTime = logSceneCleanup ? Time.realtimeSinceStartup : 0f;
        int scanned = 0;
        int renderable = 0;
        int excluded = 0;
        int hairMatches = 0;
        int faceMatches = 0;
        int bodyMatches = 0;

        foreach (var root in roots)
        {
            if (root == null)
                continue;

            foreach (var child in root.GetComponentsInChildren<Transform>(includeInactive))
            {
                var go = child.gameObject;
                if (go == null || (characterRoot != null && go == characterRoot.gameObject))
                    continue;

                scanned++;

                string name = go.name;

                if (!HasRenderable(go))
                    continue;

                renderable++;

                if (HasExcludedToken(name))
                {
                    excluded++;
                    continue;
                }

                bool isHair = MatchesToken(name, hairToken);
                bool isFace = MatchesToken(name, faceToken);
                bool isBody = MatchesAnyToken(name, bodyTokens);
                bool isMale = MatchesToken(name, maleToken);
                bool isFemale = MatchesToken(name, femaleToken);

                if (isHair)
                    hairMatches++;
                if (isFace)
                    faceMatches++;
                if (isBody)
                    bodyMatches++;

                if (!isMale && !isFemale)
                    continue;

                if (!isHair && !isFace && !isBody)
                    continue;

                if (isMale)
                {
                    if (isHair)
                        AddUnique(maleHairs, go);
                    if (isFace)
                        AddUnique(maleFaces, go);
                    if (isBody)
                        AddUnique(maleBases, go);
                }

                if (isFemale)
                {
                    if (isHair)
                        AddUnique(femaleHairs, go);
                    if (isFace)
                        AddUnique(femaleFaces, go);
                    if (isBody)
                        AddUnique(femaleBases, go);
                }
            }
        }

        SortByName(maleHairs);
        SortByName(maleFaces);
        SortByName(femaleHairs);
        SortByName(femaleFaces);
        SortByName(maleBases);
        SortByName(femaleBases);

        if (logScanResults)
            LogScanSummary(roots);

        if (logSceneCleanup)
        {
            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            string rootNames = roots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(roots, r => r != null ? r.name : "<null>"));
            Debug.Log(
                $"[CharacterCreator] Auto-collect done. Roots={rootNames}. Scanned={scanned}, Renderable={renderable}, Excluded={excluded}, HairMatches={hairMatches}, FaceMatches={faceMatches}, BodyMatches={bodyMatches}, TimeMs={elapsedMs:F1}.",
                this
            );
        }

        if (cleanupSceneObjectsWhenNotUsingSceneLists)
            CleanupSceneObjectsIfNeeded();
    }

    private void ApplyAddressableSelection()
    {
        if (!TryEnsureAddressableKeys())
        {
            SetInstanceActive(maleHairInstance, false);
            SetInstanceActive(maleFaceInstance, false);
            SetInstanceActive(femaleHairInstance, false);
            SetInstanceActive(femaleFaceInstance, false);
            return;
        }

        string hairKey = GetKeyForGender(Category.Hair, gender);
        string faceKey = GetKeyForGender(Category.Face, gender);

        if (gender == Gender.Male)
        {
            SwapAddressableInstance(maleHairInstance, instance => maleHairInstance = instance, hairKey, "Hair");
            SwapAddressableInstance(maleFaceInstance, instance => maleFaceInstance = instance, faceKey, "Face");

            SetInstanceActive(femaleHairInstance, false);
            SetInstanceActive(femaleFaceInstance, false);
        }
        else
        {
            SwapAddressableInstance(femaleHairInstance, instance => femaleHairInstance = instance, hairKey, "Hair");
            SwapAddressableInstance(femaleFaceInstance, instance => femaleFaceInstance = instance, faceKey, "Face");

            SetInstanceActive(maleHairInstance, false);
            SetInstanceActive(maleFaceInstance, false);
        }

        if (logSceneCleanup || logToFile)
        {
            string maleHairState = maleHairInstance != null ? $"{maleHairInstance.name} active={maleHairInstance.activeSelf}" : "<none>";
            string maleFaceState = maleFaceInstance != null ? $"{maleFaceInstance.name} active={maleFaceInstance.activeSelf}" : "<none>";
            string femaleHairState = femaleHairInstance != null ? $"{femaleHairInstance.name} active={femaleHairInstance.activeSelf}" : "<none>";
            string femaleFaceState = femaleFaceInstance != null ? $"{femaleFaceInstance.name} active={femaleFaceInstance.activeSelf}" : "<none>";

            LogDiagnostics(
                $"[CharacterCreator] ApplyAddressableSelection hairKey='{hairKey}' faceKey='{faceKey}' HairVisibleOverride={hairVisibleOverride} " +
                $"MaleHair={maleHairState} MaleFace={maleFaceState} FemaleHair={femaleHairState} FemaleFace={femaleFaceState}."
            );
        }
    }

    private void LogDiagnostics(string message)
    {
        if (logSceneCleanup)
            Debug.Log(message, this);

        if (!logToFile)
            return;

        AppendLogToFile(message);
    }

    private void AppendLogToFile(string message)
    {
        if (string.IsNullOrWhiteSpace(logFileName))
            return;

        try
        {
            string logDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            string path = Path.Combine(logDir, logFileName);
            File.AppendAllText(path, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            if (logSceneCleanup)
                Debug.LogWarning($"[CharacterCreator] Failed to write log file '{logFileName}': {ex.Message}", this);
            logToFile = false;
        }
    }

    private void ApplyAddressableBodies()
    {
        if (!TryEnsureBodyAddressableKeys())
        {
            SetInstanceActive(maleBodyInstances, false);
            SetInstanceActive(femaleBodyInstances, false);
            return;
        }

        if (gender == Gender.Male)
        {
            EnsureBaseBodyInstances(Gender.Male);
            SetInstanceActive(maleBodyInstances, true);
            ReleaseBodyInstances(femaleBodyInstances, femaleBodyPendingKeys);
        }
        else
        {
            EnsureBaseBodyInstances(Gender.Female);
            SetInstanceActive(femaleBodyInstances, true);
            ReleaseBodyInstances(maleBodyInstances, maleBodyPendingKeys);
        }
    }

    private bool TryEnsureAddressableKeys()
    {
#if ENABLE_ADDRESSABLES
        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            return true;
        }

        if (!hairHandleValid && !faceHandleValid)
        {
            if (logAddressables)
                Debug.LogWarning("[CharacterCreator] Addressables not loaded. Call LoadAddressables first.");
            return false;
        }

        if (hairHandleValid && hairLoadHandle.IsValid() && hairLoadHandle.Status == AsyncOperationStatus.Succeeded)
            UpdateAddressableKeys(hairLoadHandle.Result, Category.Hair);

        if (faceHandleValid && faceLoadHandle.IsValid() && faceLoadHandle.Status == AsyncOperationStatus.Succeeded)
            UpdateAddressableKeys(faceLoadHandle.Result, Category.Face);

        return true;
#else
        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            return true;
        }

        return false;
#endif
    }

    private bool TryEnsureBodyAddressableKeys()
    {
#if ENABLE_ADDRESSABLES
        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            return true;
        }

        if (!bodyHandleValid)
        {
            if (logAddressables)
                Debug.LogWarning("[CharacterCreator] Body Addressables not loaded. Call LoadAddressables first.");
            return false;
        }

        if (bodyLoadHandle.IsValid() && bodyLoadHandle.Status == AsyncOperationStatus.Succeeded)
            UpdateBodyAddressableKeys(bodyLoadHandle.Result);

        return true;
#else
        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            return true;
        }

        return false;
#endif
    }

    private bool UseAddressableIndex()
    {
        return useAddressableIndex && addressableIndex != null;
    }

    private void ApplyIndexKeys()
    {
        if (indexKeysLoaded)
            return;

        var index = addressableIndex as ScriptableObject;
        if (index == null)
            return;

        maleHairKeys.Clear();
        femaleHairKeys.Clear();
        maleFaceKeys.Clear();
        femaleFaceKeys.Clear();
        maleBodyKeys.Clear();
        femaleBodyKeys.Clear();

        if (!TryCopyIndexList(index, "maleHair", maleHairKeys)) return;
        if (!TryCopyIndexList(index, "femaleHair", femaleHairKeys)) return;
        if (!TryCopyIndexList(index, "maleFace", maleFaceKeys)) return;
        if (!TryCopyIndexList(index, "femaleFace", femaleFaceKeys)) return;
        if (!TryCopyIndexList(index, "maleBaseBodies", maleBodyKeys)) return;
        if (!TryCopyIndexList(index, "femaleBaseBodies", femaleBodyKeys)) return;

        SortByName(maleHairKeys);
        SortByName(femaleHairKeys);
        SortByName(maleFaceKeys);
        SortByName(femaleFaceKeys);
        SortByName(maleBodyKeys);
        SortByName(femaleBodyKeys);

        indexKeysLoaded = true;
    }

    private bool TryCopyIndexList(ScriptableObject index, string fieldName, List<string> target)
    {
        if (index == null || target == null)
            return false;

        var field = index.GetType().GetField(fieldName);
        if (field == null)
            return false;

        if (field.GetValue(index) is List<string> source)
        {
            target.Clear();
            target.AddRange(source);
            return true;
        }

        return false;
    }

    private void UpdateAddressableKeys(IList<GameObject> assets, Category category)
    {
        if (assets == null)
            return;

        var maleKeys = category == Category.Hair ? maleHairKeys : maleFaceKeys;
        var femaleKeys = category == Category.Hair ? femaleHairKeys : femaleFaceKeys;

        maleKeys.Clear();
        femaleKeys.Clear();

        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            string name = asset.name;
            bool isHair = MatchesToken(name, hairToken);
            bool isFace = MatchesToken(name, faceToken);
            bool isMale = MatchesToken(name, maleToken);
            bool isFemale = MatchesToken(name, femaleToken);

            if (!isMale && !isFemale)
                continue;

            if (category == Category.Hair && !isHair)
                continue;

            if (category == Category.Face && !isFace)
                continue;

            if (isMale)
                AddUnique(maleKeys, name);

            if (isFemale)
                AddUnique(femaleKeys, name);
        }

        SortByName(maleKeys);
        SortByName(femaleKeys);
    }

    private void UpdateBodyAddressableKeys(IList<GameObject> assets)
    {
        if (assets == null)
            return;

        maleBodyKeys.Clear();
        femaleBodyKeys.Clear();

        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            string name = asset.name;
            bool isMale = MatchesToken(name, maleToken);
            bool isFemale = MatchesToken(name, femaleToken);
            bool isBase = MatchesToken(name, bodyBaseToken);
            bool isBody = MatchesAnyToken(name, bodyTokens);

            if (!isMale && !isFemale)
                continue;

            if (!isBase || !isBody)
                continue;

            if (isMale)
                AddUnique(maleBodyKeys, name);

            if (isFemale)
                AddUnique(femaleBodyKeys, name);
        }

        SortByName(maleBodyKeys);
        SortByName(femaleBodyKeys);

        maleBodyPendingKeys.RemoveWhere(key => !maleBodyKeys.Contains(key));
        femaleBodyPendingKeys.RemoveWhere(key => !femaleBodyKeys.Contains(key));
    }

    private string GetKeyForGender(Category forCategory, Gender forGender)
    {
        var list = GetKeyList(forCategory, forGender);
        int index = GetOptionIndex(forCategory, forGender);
        if (index < 0 || index >= list.Count)
            return null;

        return list[index];
    }

    private void SwapAddressableInstance(GameObject currentInstance, Action<GameObject> assignInstance, string key, string label)
    {
        if (string.IsNullOrEmpty(key))
        {
            if (currentInstance != null)
                currentInstance.SetActive(false);
            return;
        }

        if (currentInstance != null && string.Equals(currentInstance.name, key, StringComparison.OrdinalIgnoreCase))
        {
            currentInstance.SetActive(true);
            return;
        }

#if ENABLE_ADDRESSABLES
        if (currentInstance != null)
        {
            Addressables.ReleaseInstance(currentInstance);
            assignInstance?.Invoke(null);
        }

        Addressables.InstantiateAsync(key, characterRoot).Completed += handle =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (logAddressables)
                    Debug.LogWarning($"[CharacterCreator] Failed to load {label} '{key}'.");
                return;
            }

            var instance = handle.Result;
            instance.name = key;
            instance.SetActive(false);
            assignInstance?.Invoke(instance);
            if (activateNewInstances)
                SetInstanceActive(instance, true);
        };
#endif
    }

    private void EnsureBaseBodyInstances(Gender forGender)
    {
        EnsureCharacterRoot();

        var keys = forGender == Gender.Male ? maleBodyKeys : femaleBodyKeys;
        var instances = forGender == Gender.Male ? maleBodyInstances : femaleBodyInstances;
        var pending = forGender == Gender.Male ? maleBodyPendingKeys : femaleBodyPendingKeys;

        if (keys.Count == 0)
        {
            if (logAddressables)
                Debug.LogWarning($"[CharacterCreator] No base body options found for {forGender}. Label='{addressablesLabelBody}', BaseToken='{bodyBaseToken}'.");
            return;
        }

        ReleaseBodyInstancesNotInKeys(instances, pending, keys);

        foreach (var key in keys)
        {
            if (HasBodyInstance(instances, key) || pending.Contains(key))
                continue;

            pending.Add(key);

#if ENABLE_ADDRESSABLES
        Addressables.InstantiateAsync(key, characterRoot).Completed += handle =>
        {
            pending.Remove(key);
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                    if (logAddressables)
                        Debug.LogWarning($"[CharacterCreator] Failed to load Body '{key}'.");
                    return;
                }

            var instance = handle.Result;
            instance.name = key;
            instance.SetActive(false);
            instances.Add(instance);
            if (activateNewInstances)
                SetInstanceActive(instance, gender == forGender);
        };
#endif
    }
    }

    private void ReleaseAddressables()
    {
#if ENABLE_ADDRESSABLES
        if (maleHairInstance != null)
            Addressables.ReleaseInstance(maleHairInstance);
        if (maleFaceInstance != null)
            Addressables.ReleaseInstance(maleFaceInstance);
        if (femaleHairInstance != null)
            Addressables.ReleaseInstance(femaleHairInstance);
        if (femaleFaceInstance != null)
            Addressables.ReleaseInstance(femaleFaceInstance);

        ReleaseBodyInstances(maleBodyInstances, maleBodyPendingKeys);
        ReleaseBodyInstances(femaleBodyInstances, femaleBodyPendingKeys);

        maleHairInstance = null;
        maleFaceInstance = null;
        femaleHairInstance = null;
        femaleFaceInstance = null;

        if (hairHandleValid && hairLoadHandle.IsValid())
            Addressables.Release(hairLoadHandle);
        if (faceHandleValid && faceLoadHandle.IsValid())
            Addressables.Release(faceLoadHandle);
        if (bodyHandleValid && bodyLoadHandle.IsValid())
            Addressables.Release(bodyLoadHandle);

#endif

        if (usePrefabCatalog)
        {
            if (maleHairInstance != null)
                UnityEngine.Object.Destroy(maleHairInstance);
            if (maleFaceInstance != null)
                UnityEngine.Object.Destroy(maleFaceInstance);
            if (femaleHairInstance != null)
                UnityEngine.Object.Destroy(femaleHairInstance);
            if (femaleFaceInstance != null)
                UnityEngine.Object.Destroy(femaleFaceInstance);

            ReleaseCatalogBodyInstances(maleBodyInstances, maleBodyPendingKeys);
            ReleaseCatalogBodyInstances(femaleBodyInstances, femaleBodyPendingKeys);
        }

#if ENABLE_ADDRESSABLES
        hairHandleValid = false;
        faceHandleValid = false;
        bodyHandleValid = false;
#endif

        maleHairKeys.Clear();
        maleFaceKeys.Clear();
        femaleHairKeys.Clear();
        femaleFaceKeys.Clear();
        maleBodyKeys.Clear();
        femaleBodyKeys.Clear();
        maleBodyPendingKeys.Clear();
        femaleBodyPendingKeys.Clear();
    }

    private static void SetInstanceActive(GameObject instance, bool active)
    {
        if (instance != null)
            instance.SetActive(active);
    }

    private static void SetInstanceActive(List<GameObject> instances, bool active)
    {
        if (instances == null)
            return;

        for (int i = 0; i < instances.Count; i++)
            SetInstanceActive(instances[i], active);
    }

    private static bool HasBodyInstance(List<GameObject> instances, string key)
    {
        if (instances == null)
            return false;

        for (int i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            if (instance != null && string.Equals(instance.name, key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void ReleaseBodyInstances(List<GameObject> instances, HashSet<string> pending)
    {
        if (instances == null)
            return;

#if ENABLE_ADDRESSABLES
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            var instance = instances[i];
            if (instance != null)
                Addressables.ReleaseInstance(instance);
            instances.RemoveAt(i);
        }
#else
        instances.Clear();
#endif

        pending?.Clear();
    }

    private static void ReleaseBodyInstancesNotInKeys(List<GameObject> instances, HashSet<string> pending, List<string> keys)
    {
        if (instances == null)
            return;

        for (int i = instances.Count - 1; i >= 0; i--)
        {
            var instance = instances[i];
            if (instance == null)
            {
                instances.RemoveAt(i);
                continue;
            }

            bool keep = keys.Exists(key => string.Equals(key, instance.name, StringComparison.OrdinalIgnoreCase));
            if (keep)
                continue;

#if ENABLE_ADDRESSABLES
            Addressables.ReleaseInstance(instance);
#endif
            instances.RemoveAt(i);
        }

        if (pending != null)
            pending.RemoveWhere(key => !keys.Contains(key));
    }

    private void ApplyList(List<GameObject> list, int selectedIndex)
    {
        if (logSceneCleanup)
        {
            GameObject selected = selectedIndex >= 0 && selectedIndex < list.Count ? list[selectedIndex] : null;
            int childCount = selected != null ? selected.GetComponentsInChildren<Transform>(true).Length : 0;
            int rendererCount = selected != null ? selected.GetComponentsInChildren<Renderer>(true).Length : 0;
            string selectedName = selected != null ? selected.name : "<none>";
            Debug.Log(
                $"[CharacterCreator] ApplyList count={list.Count}, selectedIndex={selectedIndex}, selected={selectedName}, childCount={childCount}, rendererCount={rendererCount}.",
                this
            );
        }

        int activated = 0;
        int deactivated = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null)
                continue;

            bool targetActive = i == selectedIndex;
            if (logSceneCleanup)
            {
                if (targetActive && !item.activeSelf)
                    activated++;
                else if (!targetActive && item.activeSelf)
                    deactivated++;
            }

            SetItemActiveWithHierarchy(item, targetActive);
        }

        if (logSceneCleanup)
            Debug.Log($"[CharacterCreator] ApplyList toggles: Activated={activated}, Deactivated={deactivated}.", this);
    }

    private void ApplyGroup(List<GameObject> list, bool enable)
    {
        int activated = 0;
        int deactivated = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null)
                continue;

            if (logSceneCleanup)
            {
                if (enable && !item.activeSelf)
                    activated++;
                else if (!enable && item.activeSelf)
                    deactivated++;
            }

            SetItemActiveWithHierarchy(item, enable);
        }

        if (logSceneCleanup)
            Debug.Log($"[CharacterCreator] ApplyGroup enable={enable}, Count={list.Count}, Activated={activated}, Deactivated={deactivated}.", this);
    }

    private void LogActiveSnapshot(string label)
    {
        if (!logSceneCleanup)
            return;

        Transform[] roots = ResolveScanRootsForCleanup();
        if (roots == null || roots.Length == 0)
        {
            Debug.Log($"[CharacterCreator] {label} snapshot skipped (no scan roots).", this);
            return;
        }

        int scanned = 0;
        int renderable = 0;
        int excluded = 0;
        int activeRenderable = 0;
        int activeHair = 0;
        int activeFace = 0;
        int activeBase = 0;

        foreach (var root in roots)
        {
            if (root == null)
                continue;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == null)
                    continue;

                scanned++;

                var go = child.gameObject;
                if (go == null || (characterRoot != null && go == characterRoot.gameObject))
                    continue;

                if (!HasRenderable(go))
                    continue;

                renderable++;

                string name = go.name;
                if (HasExcludedToken(name))
                {
                    excluded++;
                    continue;
                }

                bool isHair = MatchesToken(name, hairToken);
                bool isFace = MatchesToken(name, faceToken);
                bool isBaseBody = MatchesToken(name, bodyBaseToken) && MatchesAnyToken(name, bodyTokens);

                if (!go.activeInHierarchy)
                    continue;

                activeRenderable++;
                if (isHair)
                    activeHair++;
                if (isFace)
                    activeFace++;
                if (isBaseBody)
                    activeBase++;
            }
        }

        string rootNames = roots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(roots, r => r != null ? r.name : "<null>"));
        Debug.Log(
            $"[CharacterCreator] {label} snapshot. Roots={rootNames}. Scanned={scanned}, Renderable={renderable}, Excluded={excluded}, ActiveRenderable={activeRenderable}, ActiveHair={activeHair}, ActiveFace={activeFace}, ActiveBaseBody={activeBase}.",
            this
        );
    }

    private static void SetItemActiveWithHierarchy(GameObject item, bool active)
    {
        if (item == null)
            return;

        var owner = item.GetComponentInParent<CharacterCreator>();
        bool shouldLog = owner != null && owner.LogSceneCleanup;
        bool wasActive = item.activeSelf;

        if (active)
        {
            EnsureActiveHierarchy(item);
            item.SetActive(true);
            EnsureActiveChildren(item);
        }
        else
        {
            item.SetActive(false);
        }

        if (shouldLog && wasActive != item.activeSelf)
        {
            string ownerName = owner != null ? owner.name : "<null>";
            Debug.Log(
                $"[CharacterCreator] SetItemActiveWithHierarchy owner='{ownerName}' item='{item.name}' active={active} wasActive={wasActive} nowActive={item.activeSelf}.",
                owner
            );
        }
    }

    private static void EnsureActiveHierarchy(GameObject item)
    {
        if (item == null)
            return;

        var current = item.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);

            current = current.parent;
        }
    }

    private static void EnsureActiveChildren(GameObject item)
    {
        if (item == null)
            return;

        var owner = item.GetComponentInParent<CharacterCreator>();
        bool shouldLog = owner != null && owner.LogSceneCleanup;
        int activated = 0;

        foreach (var child in item.GetComponentsInChildren<Transform>(true))
        {
            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
                activated++;
            }
        }

        if (shouldLog && activated > 0)
        {
            string ownerName = owner != null ? owner.name : "<null>";
            Debug.Log(
                $"[CharacterCreator] EnsureActiveChildren owner='{ownerName}' item='{item.name}' activatedChildren={activated}.",
                owner
            );
        }
    }

    private void EnsureCharacterRoot()
    {
        if (characterRoot != null)
            return;

        characterRoot = ResolveCharacterRoot();
    }

    public void SetCharacterRoot(Transform newRoot)
    {
        if (characterRoot == newRoot)
            return;

        characterRoot = newRoot;
        InvalidateScanRootCache("characterRoot changed");
        ApplySelection();
    }

    private void InvalidateScanRootCache(string reason)
    {
        scanRootsCached = false;
        cachedScanRoots = Array.Empty<Transform>();
        if (logSceneCleanup)
            Debug.Log($"[CharacterCreator] Scan root cache invalidated ({reason}).", this);
    }

    private Transform ResolveCharacterRoot()
    {
        if (scanRootsCached && cachedScanRoots.Length == 1 && cachedScanRoots[0] == transform)
            InvalidateScanRootCache("resolve character root");

        if (!string.IsNullOrWhiteSpace(characterRootName))
        {
            var existing = GameObject.Find(characterRootName);
            if (existing != null)
            {
                if (logSceneCleanup)
                    Debug.Log($"[CharacterCreator] ResolveCharacterRoot -> found '{existing.name}' by name '{characterRootName}'.", this);
                return existing.transform;
            }

            if (logSceneCleanup)
                Debug.Log($"[CharacterCreator] ResolveCharacterRoot -> name '{characterRootName}' not found.", this);
        }

        var roots = ResolveScanRoots();
        if (roots.Length == 1 && roots[0] != null)
        {
            if (logSceneCleanup)
                Debug.Log($"[CharacterCreator] ResolveCharacterRoot -> single scan root '{roots[0].name}'.", this);
            return roots[0];
        }

        if (!autoCreateCharacterRoot)
        {
            var fallback = roots.Length > 0 && roots[0] != null ? roots[0] : transform;
            if (logSceneCleanup)
            {
                string fallbackName = fallback != null ? fallback.name : "<null>";
                Debug.Log($"[CharacterCreator] ResolveCharacterRoot -> auto-create disabled, using '{fallbackName}'.", this);
            }
            return fallback;
        }

        string rootName = string.IsNullOrWhiteSpace(characterRootName) ? "CharacterRoot" : characterRootName;
        var root = new GameObject(rootName).transform;

        if (logSceneCleanup)
        {
            string rootNames = roots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(roots, r => r != null ? r.name : "<null>"));
            Debug.Log(
                $"[CharacterCreator] ResolveCharacterRoot -> created '{root.name}'. ParentScanRootsToCharacterRoot={parentScanRootsToCharacterRoot}. ScanRoots={rootNames}.",
                this
            );
        }

        if (parentScanRootsToCharacterRoot)
        {
            foreach (var scanRoot in roots)
            {
                if (scanRoot == null || scanRoot == root)
                    continue;

                scanRoot.SetParent(root, true);
            }
        }

        return root;
    }

    private void ClampIndices()
    {
        maleHairIndex = ClampIndex(maleHairIndex, GetOptionCount(Category.Hair, Gender.Male));
        maleFaceIndex = ClampIndex(maleFaceIndex, GetOptionCount(Category.Face, Gender.Male));
        femaleHairIndex = ClampIndex(femaleHairIndex, GetOptionCount(Category.Hair, Gender.Female));
        femaleFaceIndex = ClampIndex(femaleFaceIndex, GetOptionCount(Category.Face, Gender.Female));
    }

    private void EnsureSelectionMatchesFilter(Gender forGender)
    {
        EnsureSelectionMatchesFilter(forGender, Category.Hair);
        EnsureSelectionMatchesFilter(forGender, Category.Face);
    }

    private void EnsureSelectionMatchesFilter(Gender forGender, Category forCategory)
    {
        var list = GetOptionNames(forCategory, forGender);
        var indices = GetFilteredIndices(list, filterText);
        if (indices.Count == 0)
        {
            SetOptionIndex(forCategory, forGender, -1);
            return;
        }

        int current = GetOptionIndex(forCategory, forGender);
        for (int i = 0; i < indices.Count; i++)
        {
            if (indices[i] == current)
                return;
        }

        SetOptionIndex(forCategory, forGender, indices[0]);
    }

    private static int ClampIndex(int index, int count)
    {
        return count <= 0 ? -1 : Mathf.Clamp(index, 0, count - 1);
    }

    private static int CycleIndex(int index, int count, int delta)
    {
        if (count <= 0)
            return -1;

        int current = Mathf.Clamp(index, 0, count - 1);
        int next = (current + delta) % count;
        if (next < 0)
            next += count;

        return next;
    }

    private void StepOption(int delta)
    {
        StepOption(category, delta);
    }

    private void StepOptionUnfiltered(Category forCategory, int delta)
    {
        int current = GetOptionIndex(forCategory, gender);
        int next = CycleIndex(current, GetOptionCount(forCategory, gender), delta);
        SetOptionIndex(forCategory, gender, next);
        ApplySelection();
    }

    private void StepOption(Category forCategory, int delta)
    {
        var list = GetOptionNames(forCategory, gender);
        var indices = GetFilteredIndices(list, filterText);
        if (indices.Count == 0)
        {
            SetOptionIndex(forCategory, gender, -1);
            ApplySelection();
            return;
        }

        int current = GetOptionIndex(forCategory, gender);
        int position = 0;

        for (int i = 0; i < indices.Count; i++)
        {
            if (indices[i] == current)
            {
                position = i;
                break;
            }
        }

        int nextPosition = (position + delta) % indices.Count;
        if (nextPosition < 0)
            nextPosition += indices.Count;

        SetOptionIndex(forCategory, gender, indices[nextPosition]);
        ApplySelection();
    }

    private static bool MatchesToken(string name, string token)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(token))
            return false;

        return name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MatchesFilter(string name, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MatchesAnyToken(string name, string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(name) || tokens == null || tokens.Length == 0)
            return false;

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
                continue;

            if (MatchesToken(name, token))
                return true;
        }

        return false;
    }

    private static bool MatchesTokenInHierarchy(Transform current, string token)
    {
        while (current != null)
        {
            if (MatchesToken(current.name, token))
                return true;

            current = current.parent;
        }

        return false;
    }

    private static bool HasRenderable(GameObject go)
    {
        if (go == null)
            return false;

        return go.GetComponentInChildren<Renderer>(true) != null;
    }

    private bool HasExcludedToken(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || excludeNameTokens == null || excludeNameTokens.Length == 0)
            return false;

        foreach (var token in excludeNameTokens)
        {
            if (string.IsNullOrWhiteSpace(token))
                continue;

            if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static void AddUnique(List<GameObject> list, GameObject go)
    {
        if (go != null && !list.Contains(go))
            list.Add(go);
    }

    private static void AddUnique(List<string> list, string key)
    {
        if (!string.IsNullOrEmpty(key) && !list.Contains(key))
            list.Add(key);
    }

    private static void SortByName(List<GameObject> list)
    {
        list.Sort((a, b) => string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private static void SortByName(List<string> list)
    {
        list.Sort((a, b) => string.Compare(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private Transform[] ResolveScanRoots()
    {
        bool hasManualRoot = scanRoots is { Length: > 0 } && Array.Exists(scanRoots, root => root != null);
        if (hasManualRoot)
        {
            scanRootsCached = false;
            cachedScanRoots = scanRoots;
            return scanRoots;
        }

        if (scanRootsCached && cachedScanRoots is { Length: > 0 })
        {
            if (characterRoot != null && characterRoot != transform && cachedScanRoots.Length == 1 && cachedScanRoots[0] == transform)
            {
                InvalidateScanRootCache("characterRoot available");
            }
            else
            {
                if (logSceneCleanup)
                    Debug.Log($"[CharacterCreator] Using cached scan roots. Count={cachedScanRoots.Length}.", this);
                return cachedScanRoots;
            }
        }

        if (autoFindScanRootByPrefix && !string.IsNullOrWhiteSpace(scanRootNamePrefix))
        {
            var matches = FindScanRootsByPrefix(scanRootNamePrefix);
            if (matches.Count > 0)
            {
                cachedScanRoots = matches.ToArray();
                scanRootsCached = true;
                if (logSceneCleanup)
                {
                    string rootNames = cachedScanRoots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(cachedScanRoots, r => r != null ? r.name : "<null>"));
                    Debug.Log($"[CharacterCreator] Scan roots resolved by prefix '{scanRootNamePrefix}'. Count={cachedScanRoots.Length}. Roots={rootNames}.", this);
                }
                return cachedScanRoots;
            }
        }

        var tokenMatches = FindScanRootsByTokens();
        if (tokenMatches.Count > 0)
        {
            cachedScanRoots = tokenMatches.ToArray();
            scanRootsCached = true;
            if (logSceneCleanup)
            {
                string rootNames = cachedScanRoots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(cachedScanRoots, r => r != null ? r.name : "<null>"));
                Debug.Log($"[CharacterCreator] Scan roots resolved by tokens. Count={cachedScanRoots.Length}. Roots={rootNames}.", this);
            }
            return cachedScanRoots;
        }

        return new[] { characterRoot != null ? characterRoot : transform };
    }

    private List<Transform> FindScanRootsByPrefix(string prefix)
    {
        var matches = new List<Transform>();
        if (string.IsNullOrWhiteSpace(prefix))
            return matches;

        var seen = new HashSet<Transform>();
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid())
                continue;

            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var root = roots[r];
                if (root == null)
                    continue;

                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || !seen.Add(child))
                        continue;

                    if (child.name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                        matches.Add(child);
                }
            }
        }

        return matches;
    }

    private List<Transform> FindScanRootsByTokens()
    {
        var matches = new HashSet<Transform>();
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid())
                continue;

            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var root = roots[r];
                if (root == null)
                    continue;

                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null)
                        continue;

                    var go = child.gameObject;
                    if (go == null)
                        continue;

                    if (!HasRenderable(go))
                        continue;

                    string name = go.name;
                    if (HasExcludedToken(name))
                        continue;

                    bool isHair = MatchesToken(name, hairToken);
                    bool isFace = MatchesToken(name, faceToken);
                    bool isBody = MatchesAnyToken(name, bodyTokens) || MatchesToken(name, bodyBaseToken);

                if (!isHair && !isFace && !isBody)
                    continue;

                    var rootTransform = child.root;
                    if (rootTransform == transform)
                        rootTransform = root.transform;

                    matches.Add(rootTransform);
                }
            }
        }

        return new List<Transform>(matches);
    }

    private void LogScanSummary(Transform[] roots)
    {
        int total = maleHairs.Count + maleFaces.Count + femaleHairs.Count + femaleFaces.Count + maleBases.Count + femaleBases.Count;
        string rootNames = roots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(roots, r => r != null ? r.name : "<null>"));
        string rootName = characterRoot != null ? characterRoot.name : "<null>";

        if (total == 0)
        {
            Debug.LogWarning(
                $"[CharacterCreator] No options found. CharacterRoot={rootName}. ScanRoots: {rootNames}. Tokens: male='{maleToken}', female='{femaleToken}', hair='{hairToken}', face='{faceToken}'. IncludeInactive={includeInactive}.",
                this
            );
            return;
        }

        Debug.Log(
            $"[CharacterCreator] Options found. Male base={maleBases.Count}, Male hair={maleHairs.Count}, Male face={maleFaces.Count}, Female base={femaleBases.Count}, Female hair={femaleHairs.Count}, Female face={femaleFaces.Count}. CharacterRoot={rootName}. ScanRoots: {rootNames}.",
            this
        );
    }

    private List<GameObject> GetHairList(Gender forGender) =>
        forGender == Gender.Male ? maleHairs : femaleHairs;

    private List<GameObject> GetFaceList(Gender forGender) =>
        forGender == Gender.Male ? maleFaces : femaleFaces;

    private int GetHairIndex(Gender forGender) =>
        forGender == Gender.Male ? maleHairIndex : femaleHairIndex;

    private int GetFaceIndex(Gender forGender) =>
        forGender == Gender.Male ? maleFaceIndex : femaleFaceIndex;

    private IReadOnlyList<string> GetOptionNames(Category forCategory, Gender forGender)
    {
        if (useAddressablesForHairFace)
            return GetKeyList(forCategory, forGender);

        return forCategory == Category.Hair ? GetNameList(GetHairList(forGender)) : GetNameList(GetFaceList(forGender));
    }

    private int GetOptionCount(Category forCategory, Gender forGender)
    {
        if (useAddressablesForHairFace)
            return GetKeyList(forCategory, forGender).Count;

        return forCategory == Category.Hair ? GetHairList(forGender).Count : GetFaceList(forGender).Count;
    }

    private List<string> GetKeyList(Category forCategory, Gender forGender)
    {
        if (forCategory == Category.Hair)
            return forGender == Gender.Male ? maleHairKeys : femaleHairKeys;

        return forGender == Gender.Male ? maleFaceKeys : femaleFaceKeys;
    }

    private int GetOptionIndex(Category forCategory, Gender forGender) =>
        forCategory == Category.Hair ? GetHairIndex(forGender) : GetFaceIndex(forGender);

    private void SetOptionIndexForCurrentGender(Category forCategory, int index)
    {
        int clamped = ClampIndex(index, GetOptionCount(forCategory, gender));
        SetOptionIndex(forCategory, gender, clamped);
    }

    private void SetOptionIndex(Category forCategory, Gender forGender, int index)
    {
        if (forCategory == Category.Hair)
        {
            if (forGender == Gender.Male)
                maleHairIndex = index;
            else
                femaleHairIndex = index;
        }
        else
        {
            if (forGender == Gender.Male)
                maleFaceIndex = index;
            else
                femaleFaceIndex = index;
        }
    }

    private static List<int> GetFilteredIndices(IReadOnlyList<string> list, string filter)
    {
        var indices = new List<int>();
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (string.IsNullOrEmpty(item))
                continue;

            if (MatchesFilter(item, filter))
                indices.Add(i);
        }

        return indices;
    }

    private static string GetItemName(IReadOnlyList<string> list, int index)
    {
        if (index < 0 || index >= list.Count)
            return string.Empty;

        return list[index] ?? string.Empty;
    }

    private static List<string> GetNameList(List<GameObject> list)
    {
        var names = new List<string>(list.Count);
        foreach (var item in list)
            names.Add(item != null ? item.name : null);
        return names;
    }
}
