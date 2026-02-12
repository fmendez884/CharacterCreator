using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// For addressable index asset in Assets/Scripts/Addressables
// (no namespace)
#if ENABLE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

[DisallowMultipleComponent]
public class EquipmentSystem : MonoBehaviour
{
    public enum Gender
    {
        Male,
        Female
    }

    public enum Slot
    {
        Head,
        Body,
        Hands,
        Legs,
        Feet,
        Weapon
    }

    [Header("Root")]
    [SerializeField] private Transform characterRoot;
    [SerializeField] private bool autoCreateCharacterRoot;
    [SerializeField] private string characterRootName = "CharacterRoot";
    [SerializeField] private bool parentScanRootsToCharacterRoot = true;

    [Header("Attachment")]
    [SerializeField] private Transform equipmentRoot;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private bool autoFindWeaponSocket = true;
    [SerializeField] private string weaponSocketName = "weapon_r";

    [Header("Auto Collect (Scene)")]
    [SerializeField] private bool autoCollectFromScene = true;
    [SerializeField] private Transform[] scanRoots = Array.Empty<Transform>();
    [SerializeField] private bool autoFindScanRootByPrefix = true;
    [SerializeField] private string scanRootNamePrefix = "FBX_Instances_FFXIV";
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private string maleToken = "c0101";
    [SerializeField] private string femaleToken = "c0201";
    [SerializeField] private string equipmentBaseToken = "e0000";
    [SerializeField] private string headToken = "_met";
    [SerializeField] private string bodyToken = "_top";
    [SerializeField] private string handsToken = "_glv";
    [SerializeField] private string legsToken = "_dwn";
    [SerializeField] private string feetToken = "_sho";
    [SerializeField] private string weaponPrefix = "w";
    [SerializeField] private string[] excludeNameTokens = { " group", " part" };
    [SerializeField] private bool logScanResults = true;

    [Header("Scene Cleanup")]
    [SerializeField] private bool cleanupSceneObjectsWhenNotUsingSceneLists = true;

    [Header("Debug")]
    [SerializeField] private bool logSceneCleanup;
    [SerializeField] private bool logToFile;
    [SerializeField] private string logFileName = "EquipmentSystem-debug.log";

    [Header("Runtime Cleanup")]
    [SerializeField] private bool runtimeCleanupPoll = true;
    [SerializeField] private float runtimeCleanupPollDurationSeconds = 5f;
    [SerializeField] private float runtimeCleanupPollIntervalSeconds = 0.5f;
    [SerializeField] private bool activateNewInstances = false;

    [Header("Selection")]
    [SerializeField] private Gender gender = Gender.Male;
    [SerializeField] private bool includeUnequippedOption = true;
    [SerializeField] private int headIndex;
    [SerializeField] private int bodyIndex;
    [SerializeField] private int handsIndex;
    [SerializeField] private int legsIndex;
    [SerializeField] private int feetIndex;
    [SerializeField] private int weaponIndex;
    [SerializeField] private int lastHeadIndex;
    [SerializeField] private int lastBodyIndex;
    [SerializeField] private int lastHandsIndex;
    [SerializeField] private int lastLegsIndex;
    [SerializeField] private int lastFeetIndex;
    [SerializeField] private int lastWeaponIndex;

    [Header("Behavior")]
    [SerializeField] private bool hideBaseBodyOnEquip = true;

    [Header("Prefab Catalog (Scene-Based)")]
    [SerializeField] private FfxivPrefabCatalog prefabCatalog;
    [SerializeField] private bool usePrefabCatalog = true;

    [Header("Base Body")]
    [SerializeField] private bool manageBaseBodies = true;
    [SerializeField] private bool useAddressablesForBaseBodies = true;
    [SerializeField] private string addressablesLabelBody = "Body";

    [Header("Addressables")]
    [SerializeField] private bool useAddressablesForEquipment = true;
    [SerializeField] private bool useAddressablesForWeapons = true;
    [SerializeField] private bool loadAddressablesOnAwake = true;
    [SerializeField] private string addressablesLabelEquipment = "Equipment";
    [SerializeField] private string addressablesLabelWeapons = "Weapons";
    [SerializeField] private bool logAddressables = true;

    [Header("Addressable Index")]
    [SerializeField] private FfxivRuntimeCatalog runtimeCatalog;
    [SerializeField] private UnityEngine.Object addressableIndex;
    [SerializeField] private bool useAddressableIndex = true;

    public event Action Changed;
    public bool LogSceneCleanup => logSceneCleanup;

    private readonly List<string> maleHeadKeys = new();
    private readonly List<string> maleBodyKeys = new();
    private readonly List<string> maleHandsKeys = new();
    private readonly List<string> maleLegsKeys = new();
    private readonly List<string> maleFeetKeys = new();
    private readonly List<string> femaleHeadKeys = new();
    private readonly List<string> femaleBodyKeys = new();
    private readonly List<string> femaleHandsKeys = new();
    private readonly List<string> femaleLegsKeys = new();
    private readonly List<string> femaleFeetKeys = new();
    private readonly List<string> weaponKeys = new();
    private readonly List<string> maleBaseBodyKeys = new();
    private readonly List<string> femaleBaseBodyKeys = new();
    private readonly HashSet<string> maleBaseBodyPendingKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> femaleBaseBodyPendingKeys = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<GameObject> maleHeadObjects = new();
    private readonly List<GameObject> maleBodyObjects = new();
    private readonly List<GameObject> maleHandsObjects = new();
    private readonly List<GameObject> maleLegsObjects = new();
    private readonly List<GameObject> maleFeetObjects = new();
    private readonly List<GameObject> femaleHeadObjects = new();
    private readonly List<GameObject> femaleBodyObjects = new();
    private readonly List<GameObject> femaleHandsObjects = new();
    private readonly List<GameObject> femaleLegsObjects = new();
    private readonly List<GameObject> femaleFeetObjects = new();
    private readonly List<GameObject> weaponObjects = new();
    private readonly List<GameObject> maleBaseBodyObjects = new();
    private readonly List<GameObject> femaleBaseBodyObjects = new();

#if ENABLE_ADDRESSABLES
    private AsyncOperationHandle<IList<GameObject>> equipmentLoadHandle;
    private AsyncOperationHandle<IList<GameObject>> weaponsLoadHandle;
    private AsyncOperationHandle<IList<GameObject>> bodyLoadHandle;
    private bool equipmentHandleValid;
    private bool weaponsHandleValid;
    private bool bodyHandleValid;
#endif

    private GameObject headInstance;
    private GameObject bodyInstance;
    private GameObject handsInstance;
    private GameObject legsInstance;
    private GameObject feetInstance;
    private GameObject weaponInstance;
    private string pendingHeadKey;
    private string pendingBodyKey;
    private string pendingHandsKey;
    private string pendingLegsKey;
    private string pendingFeetKey;
    private string pendingWeaponKey;
    private readonly Dictionary<string, string> selectedBaseBodyBySlotToken = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GameObject> maleBaseBodyInstances = new();
    private readonly List<GameObject> femaleBaseBodyInstances = new();
    private bool indexKeysLoaded;
    private bool warnedEquipmentAddressablesNotLoaded;
    private bool warnedBodyAddressablesNotLoaded;
    private bool warnedWeaponAddressablesNotLoaded;
    private bool loggedStartupMode;
    private Coroutine runtimeCleanupCoroutine;
    private bool scanRootsCached;
    private Transform[] cachedScanRoots = Array.Empty<Transform>();

    public Gender CurrentGender => gender;
    public bool ManageBaseBodies => manageBaseBodies;

    public Transform CharacterRoot
    {
        get
        {
            EnsureCharacterRoot();
            return characterRoot;
        }
    }

    public int GetSlotCount(Slot slot) => GetSlotNames(slot, gender).Count;

    public int GetSlotPosition(Slot slot)
    {
        int count = GetSlotCount(slot);
        int index = GetSlotIndex(slot);
        if (count <= 0 || index < 0 || index >= count)
            return 0;

        return index + 1;
    }

    public string GetCurrentItemName(Slot slot) => GetItemName(GetSlotNames(slot, gender), GetSlotIndex(slot));
    public string GetSelectedKey(Slot slot) => GetCurrentItemName(slot);

    public void SetManageBaseBodies(bool enabled)
    {
        manageBaseBodies = enabled;
        ApplySelection();
    }

    public void SetAddressableIndex(ScriptableObject index)
    {
        addressableIndex = index;
        indexKeysLoaded = false;
        warnedEquipmentAddressablesNotLoaded = false;
        warnedBodyAddressablesNotLoaded = false;
        warnedWeaponAddressablesNotLoaded = false;
        loggedStartupMode = false;
        ApplySelection();
    }

    public void SetRuntimeCatalog(FfxivRuntimeCatalog catalog)
    {
        runtimeCatalog = catalog;
        indexKeysLoaded = false;
        warnedEquipmentAddressablesNotLoaded = false;
        warnedBodyAddressablesNotLoaded = false;
        warnedWeaponAddressablesNotLoaded = false;
        loggedStartupMode = false;
        ApplySelection();
    }

    private void Awake()
    {
        includeUnequippedOption = true;

        EnsureCharacterRoot();
        EnsureRuntimeDataSources();
        bool usingIndex = UseAddressableIndex();

        if (usePrefabCatalog)
            ApplyPrefabCatalog();
        else if (autoCollectFromScene && !usingIndex)
            AutoCollectFromScene();

        if (usingIndex)
            ApplyIndexKeys();

        if (!usingIndex && loadAddressablesOnAwake && (useAddressablesForEquipment || useAddressablesForWeapons || useAddressablesForBaseBodies))
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
        StopRuntimeCleanupPoll();
        ReleaseAddressables();
    }

    private void OnValidate()
    {
        includeUnequippedOption = true;
        ClampIndices();
    }

    private void StartRuntimeCleanupPoll()
    {
        if (!ShouldRunRuntimeCleanupPoll())
            return;

        if (runtimeCleanupCoroutine != null)
            StopCoroutine(runtimeCleanupCoroutine);

        runtimeCleanupCoroutine = StartCoroutine(RuntimeCleanupPoll());
    }

    private bool ShouldRunRuntimeCleanupPoll()
    {
        if (!cleanupSceneObjectsWhenNotUsingSceneLists || !runtimeCleanupPoll)
            return false;
        if (UseAddressableIndex())
            return false;

        // Polling is only useful while any slot category is scene-list driven.
        bool usesSceneEquipment = !useAddressablesForEquipment && !usePrefabCatalog;
        bool usesSceneWeapons = !useAddressablesForWeapons && !usePrefabCatalog;
        bool usesSceneBaseBodies = manageBaseBodies && !useAddressablesForBaseBodies;
        return usesSceneEquipment || usesSceneWeapons || usesSceneBaseBodies;
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
            Debug.Log($"[EquipmentSystem] Runtime cleanup poll started. Duration={duration:F1}s Interval={interval:F2}s.", this);

        while (Time.realtimeSinceStartup < endTime)
        {
            iteration++;
            scanRootsCached = false;
            cachedScanRoots = Array.Empty<Transform>();

            if (autoCollectFromScene && !usePrefabCatalog && !UseAddressableIndex())
                AutoCollectFromScene();
            else
                CleanupSceneObjectsIfNeeded();

            yield return new WaitForSeconds(interval);
        }

        if (logSceneCleanup)
            Debug.Log($"[EquipmentSystem] Runtime cleanup poll finished. Iterations={iteration}.", this);

        runtimeCleanupCoroutine = null;
    }

    public void SetCharacterRoot(Transform newRoot)
    {
        if (characterRoot == newRoot)
            return;

        characterRoot = newRoot;
        InvalidateScanRootCache("characterRoot changed");
        ReparentRuntimeInstances(ResolveEquipmentRoot(), ResolveWeaponSocket());
        ApplySelection();
    }

    private void InvalidateScanRootCache(string reason)
    {
        scanRootsCached = false;
        cachedScanRoots = Array.Empty<Transform>();
        if (logSceneCleanup)
            Debug.Log($"[EquipmentSystem] Scan root cache invalidated ({reason}).", this);
    }

    public void SetGender(Gender newGender, bool applySelection = true)
    {
        if (gender == newGender)
            return;

        gender = newGender;
        if (applySelection)
            ApplySelection();
    }

    public bool SetSlotByKey(Slot slot, string key, bool applySelection = true)
    {
        EnsureRuntimeReady();

        if (string.IsNullOrWhiteSpace(key))
        {
            SetSlotIndex(slot, -1);
            if (applySelection)
                ApplySelection();
            return true;
        }

        var names = GetSlotNames(slot, gender);
        int resolvedIndex = -1;
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], key, StringComparison.OrdinalIgnoreCase))
            {
                resolvedIndex = i;
                break;
            }
        }

        if (resolvedIndex < 0)
            return false;

        SetSlotIndex(slot, resolvedIndex);
        if (applySelection)
            ApplySelection();
        return true;
    }

    public void EnsureRuntimeReady()
    {
        EnsureRuntimeDataSources();
        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            return;
        }

        if (useAddressablesForEquipment)
            TryEnsureEquipmentKeys();
        if (useAddressablesForWeapons)
            TryEnsureWeaponKeys();
        if (manageBaseBodies && useAddressablesForBaseBodies)
            TryEnsureBaseBodyKeys();
    }

    public void ApplyCurrentSelection(bool invokeChanged = true)
    {
        ApplySelection(invokeChanged);
    }

    public void NextSlot(Slot slot)
    {
        StepSlot(slot, 1);
    }

    public void PreviousSlot(Slot slot)
    {
        StepSlot(slot, -1);
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

        ClearSceneLists();

        AddRange(maleHeadObjects, prefabCatalog.maleHead);
        AddRange(maleBodyObjects, prefabCatalog.maleBody);
        AddRange(maleHandsObjects, prefabCatalog.maleHands);
        AddRange(maleLegsObjects, prefabCatalog.maleLegs);
        AddRange(maleFeetObjects, prefabCatalog.maleFeet);
        AddRange(femaleHeadObjects, prefabCatalog.femaleHead);
        AddRange(femaleBodyObjects, prefabCatalog.femaleBody);
        AddRange(femaleHandsObjects, prefabCatalog.femaleHands);
        AddRange(femaleLegsObjects, prefabCatalog.femaleLegs);
        AddRange(femaleFeetObjects, prefabCatalog.femaleFeet);
        AddRange(weaponObjects, prefabCatalog.weapons);
        AddRange(maleBaseBodyObjects, prefabCatalog.maleBaseBodies);
        AddRange(femaleBaseBodyObjects, prefabCatalog.femaleBaseBodies);

        SortByName(maleHeadObjects);
        SortByName(maleBodyObjects);
        SortByName(maleHandsObjects);
        SortByName(maleLegsObjects);
        SortByName(maleFeetObjects);
        SortByName(femaleHeadObjects);
        SortByName(femaleBodyObjects);
        SortByName(femaleHandsObjects);
        SortByName(femaleLegsObjects);
        SortByName(femaleFeetObjects);
        SortByName(weaponObjects);
        SortByName(maleBaseBodyObjects);
        SortByName(femaleBaseBodyObjects);
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

    [ContextMenu("Load Addressables (Equipment/Weapons)")]
    public void LoadAddressables()
    {
        warnedEquipmentAddressablesNotLoaded = false;
        warnedBodyAddressablesNotLoaded = false;
        warnedWeaponAddressablesNotLoaded = false;

        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            ApplySelection();
            return;
        }

        if (!useAddressablesForEquipment && !useAddressablesForWeapons && !useAddressablesForBaseBodies)
            return;

        ReleaseAddressables();

#if ENABLE_ADDRESSABLES
        if (useAddressablesForEquipment && !string.IsNullOrWhiteSpace(addressablesLabelEquipment))
        {
            equipmentLoadHandle = Addressables.LoadAssetsAsync<GameObject>(addressablesLabelEquipment, null);
            equipmentHandleValid = true;
            equipmentLoadHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    UpdateEquipmentKeys(handle.Result);
                ApplySelection();
            };
        }

        if (useAddressablesForWeapons && !string.IsNullOrWhiteSpace(addressablesLabelWeapons))
        {
            weaponsLoadHandle = Addressables.LoadAssetsAsync<GameObject>(addressablesLabelWeapons, null);
            weaponsHandleValid = true;
            weaponsLoadHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    UpdateWeaponKeys(handle.Result);
                ApplySelection();
            };
        }

        if (manageBaseBodies && useAddressablesForBaseBodies && !string.IsNullOrWhiteSpace(addressablesLabelBody))
        {
            bodyLoadHandle = Addressables.LoadAssetsAsync<GameObject>(addressablesLabelBody, null);
            bodyHandleValid = true;
            bodyLoadHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    UpdateBaseBodyKeys(handle.Result);
                EnsureBaseBodyInstances(gender);
                ApplySelection();
            };
        }
#else
        if (logAddressables)
            Debug.LogWarning("[EquipmentSystem] Addressables package not available. Install Addressables to enable equipment/weapons loading.");
#endif
    }

    private void ApplySelection(bool invokeChanged = true)
    {
        EnsureCharacterRoot();
        ClampIndices();

        if (characterRoot != null && !characterRoot.gameObject.activeSelf)
            characterRoot.gameObject.SetActive(true);

        if (logSceneCleanup)
        {
            Debug.Log(
                $"[EquipmentSystem] ApplySelection start. Gender={gender} UsePrefabCatalog={usePrefabCatalog} UseAddressablesEquip={useAddressablesForEquipment} UseAddressablesWeapons={useAddressablesForWeapons} ManageBaseBodies={manageBaseBodies} UseAddressablesBodies={useAddressablesForBaseBodies}.",
                this
            );
        }

        LogActiveSnapshot("Before cleanup");
        CleanupSceneObjectsIfNeeded();
        LogActiveSnapshot("After cleanup");

        if (manageBaseBodies)
        {
            ApplyBaseBodies();
            LogActiveSnapshot("After ApplyBaseBodies");
        }
        else if (!usePrefabCatalog)
        {
            ShowAllBaseBodySlots();
            LogActiveSnapshot("After ShowAllBaseBodySlots");
        }

        if (useAddressablesForEquipment)
        {
            ApplyAddressableEquipment();
            LogActiveSnapshot("After ApplyAddressableEquipment");
        }
        else if (usePrefabCatalog)
        {
            ApplyCatalogEquipment();
            LogActiveSnapshot("After ApplyCatalogEquipment");
        }
        else
        {
            ApplySceneEquipment();
            LogActiveSnapshot("After ApplySceneEquipment");
        }

        if (useAddressablesForWeapons)
        {
            ApplyAddressableWeapon();
            LogActiveSnapshot("After ApplyAddressableWeapon");
        }
        else if (usePrefabCatalog)
        {
            ApplyCatalogWeapon();
            LogActiveSnapshot("After ApplyCatalogWeapon");
        }
        else
        {
            ApplySceneWeapon();
            LogActiveSnapshot("After ApplySceneWeapon");
        }

        if (!manageBaseBodies || !hideBaseBodyOnEquip)
            selectedBaseBodyBySlotToken.Clear();

        LogActiveSlotSummary("After ApplySelection");
        if (invokeChanged)
            Changed?.Invoke();
    }

    private void ShowAllBaseBodySlots()
    {
        if (characterRoot == null)
            return;

        foreach (var child in characterRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == null)
                continue;

            var go = child.gameObject;
            if (go == null)
                continue;

            string name = go.name;
            if (!MatchesToken(name, equipmentBaseToken))
                continue;
            if (!MatchesCurrentGender(name))
                continue;

            go.SetActive(true);
        }
    }

    private void ApplyAddressableEquipment()
    {
        if (!TryEnsureEquipmentKeys())
        {
            SetInstanceActive(headInstance, false);
            SetInstanceActive(bodyInstance, false);
            SetInstanceActive(handsInstance, false);
            SetInstanceActive(legsInstance, false);
            SetInstanceActive(feetInstance, false);
            return;
        }

        SwapAddressableInstance(
            headInstance,
            instance => headInstance = instance,
            () => headInstance,
            () => GetKeyForSlot(Slot.Head, gender),
            () => pendingHeadKey,
            value => pendingHeadKey = value,
            GetKeyForSlot(Slot.Head, gender),
            "Head",
            ResolveEquipmentRoot()
        );
        SwapAddressableInstance(
            bodyInstance,
            instance => bodyInstance = instance,
            () => bodyInstance,
            () => GetKeyForSlot(Slot.Body, gender),
            () => pendingBodyKey,
            value => pendingBodyKey = value,
            GetKeyForSlot(Slot.Body, gender),
            "Body",
            ResolveEquipmentRoot()
        );
        SwapAddressableInstance(
            handsInstance,
            instance => handsInstance = instance,
            () => handsInstance,
            () => GetKeyForSlot(Slot.Hands, gender),
            () => pendingHandsKey,
            value => pendingHandsKey = value,
            GetKeyForSlot(Slot.Hands, gender),
            "Hands",
            ResolveEquipmentRoot()
        );
        SwapAddressableInstance(
            legsInstance,
            instance => legsInstance = instance,
            () => legsInstance,
            () => GetKeyForSlot(Slot.Legs, gender),
            () => pendingLegsKey,
            value => pendingLegsKey = value,
            GetKeyForSlot(Slot.Legs, gender),
            "Legs",
            ResolveEquipmentRoot()
        );
        SwapAddressableInstance(
            feetInstance,
            instance => feetInstance = instance,
            () => feetInstance,
            () => GetKeyForSlot(Slot.Feet, gender),
            () => pendingFeetKey,
            value => pendingFeetKey = value,
            GetKeyForSlot(Slot.Feet, gender),
            "Feet",
            ResolveEquipmentRoot()
        );

        ApplyAllBaseBodySlotVisibility();
    }

    private void ApplySceneEquipment()
    {
        ApplyList(GetSceneList(Slot.Head, gender), headIndex);
        ApplyList(GetSceneList(Slot.Body, gender), bodyIndex);
        ApplyList(GetSceneList(Slot.Hands, gender), handsIndex);
        ApplyList(GetSceneList(Slot.Legs, gender), legsIndex);
        ApplyList(GetSceneList(Slot.Feet, gender), feetIndex);

        ApplyAllBaseBodySlotVisibility();
    }

    private void ApplyBaseBodies()
    {
        if (useAddressablesForBaseBodies)
            ApplyAddressableBaseBodies();
        else
            ApplySceneBaseBodies();
    }

    private void ApplyAddressableBaseBodies()
    {
        if (!TryEnsureBaseBodyKeys())
        {
            SetInstancesActive(maleBaseBodyInstances, false);
            SetInstancesActive(femaleBaseBodyInstances, false);
            return;
        }

        if (gender == Gender.Male)
        {
            EnsureBaseBodyInstances(Gender.Male);
            ApplyBaseBodyActivation(Gender.Male);
            ReleaseBodyInstances(femaleBaseBodyInstances, femaleBaseBodyPendingKeys);
        }
        else
        {
            EnsureBaseBodyInstances(Gender.Female);
            ApplyBaseBodyActivation(Gender.Female);
            ReleaseBodyInstances(maleBaseBodyInstances, maleBaseBodyPendingKeys);
        }
    }

    private void ApplyBaseBodyActivation(Gender forGender)
    {
        var instances = forGender == Gender.Male ? maleBaseBodyInstances : femaleBaseBodyInstances;
        var keys = forGender == Gender.Male ? maleBaseBodyKeys : femaleBaseBodyKeys;
        string token = forGender == Gender.Male ? maleToken : femaleToken;

        bool hasTokenMatch = !string.IsNullOrWhiteSpace(token)
            && keys.Exists(key => !string.IsNullOrWhiteSpace(key) && MatchesToken(key, token));

        if (logSceneCleanup && !string.IsNullOrWhiteSpace(token) && keys.Count > 0 && !hasTokenMatch)
        {
            Debug.LogWarning(
                $"[EquipmentSystem] No base body keys match token '{token}' for {forGender}. All base bodies will be disabled.",
                this
            );
        }

        if (instances == null)
            return;

        for (int i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            if (instance == null)
                continue;

            bool active;
            if (string.IsNullOrWhiteSpace(token))
                active = true;
            else if (hasTokenMatch)
                active = MatchesToken(instance.name, token);
            else
                active = false;

            SetItemActiveWithHierarchy(instance, active);
        }
    }

    private void ApplySceneBaseBodies()
    {
        var showMale = gender == Gender.Male;
        var showFemale = gender == Gender.Female;

        ApplyGroup(maleBaseBodyObjects, showMale);
        ApplyGroup(femaleBaseBodyObjects, showFemale);
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
            Debug.Log($"[EquipmentSystem] ApplyGroup enable={enable}, Count={list.Count}, Activated={activated}, Deactivated={deactivated}.", this);
    }

    private void ApplyAddressableWeapon()
    {
        if (!TryEnsureWeaponKeys())
        {
            SetInstanceActive(weaponInstance, false);
            return;
        }

        SwapAddressableInstance(
            weaponInstance,
            instance => weaponInstance = instance,
            () => weaponInstance,
            () => GetKeyForSlot(Slot.Weapon, gender),
            () => pendingWeaponKey,
            value => pendingWeaponKey = value,
            GetKeyForSlot(Slot.Weapon, gender),
            "Weapon",
            ResolveWeaponSocket()
        );
    }

    private void ApplySceneWeapon()
    {
        ApplyList(weaponObjects, weaponIndex);
    }

    private void LogActiveSnapshot(string label)
    {
        if (!logSceneCleanup)
            return;

        Transform[] roots = ResolveScanRootsForCleanup();
        if (roots == null || roots.Length == 0)
        {
            Debug.Log($"[EquipmentSystem] {label} snapshot skipped (no scan roots).", this);
            return;
        }

        int scanned = 0;
        int renderable = 0;
        int excluded = 0;
        int activeRenderable = 0;
        int activeEquipment = 0;
        int activeWeapon = 0;
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

                bool isWeapon = IsWeaponName(name);
                bool hasSlot = TryResolveSlot(name, out _);
                bool isBaseBody = hasSlot && MatchesToken(name, equipmentBaseToken);
                bool isEquipment = hasSlot && !isBaseBody;

                if (!go.activeInHierarchy)
                    continue;

                activeRenderable++;
                if (isEquipment)
                    activeEquipment++;
                if (isWeapon)
                    activeWeapon++;
                if (isBaseBody)
                    activeBase++;
            }
        }

        string rootNames = roots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(roots, r => r != null ? r.name : "<null>"));
        Debug.Log(
            $"[EquipmentSystem] {label} snapshot. Roots={rootNames}. Scanned={scanned}, Renderable={renderable}, Excluded={excluded}, ActiveRenderable={activeRenderable}, ActiveEquipment={activeEquipment}, ActiveWeapon={activeWeapon}, ActiveBaseBody={activeBase}.",
            this
        );
    }

    private void LogActiveSlotSummary(string label)
    {
        if (!logSceneCleanup)
            return;

        Transform[] roots = ResolveScanRootsForCleanup();
        if (roots == null || roots.Length == 0)
        {
            Debug.Log($"[EquipmentSystem] {label} slot summary skipped (no scan roots).", this);
            return;
        }

        var counts = new Dictionary<Slot, int>();
        var samples = new Dictionary<Slot, List<string>>();
        foreach (Slot slot in Enum.GetValues(typeof(Slot)))
        {
            if (slot == Slot.Weapon)
                continue;

            counts[slot] = 0;
            samples[slot] = new List<string>(4);
        }

        foreach (var root in roots)
        {
            if (root == null)
                continue;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == null)
                    continue;

                var go = child.gameObject;
                if (go == null || (characterRoot != null && go == characterRoot.gameObject))
                    continue;

                if (!HasRenderable(go) || !go.activeInHierarchy)
                    continue;

                string name = go.name;
                if (HasExcludedToken(name))
                    continue;

                if (!TryResolveSlot(name, out var slot))
                    continue;

                if (MatchesToken(name, equipmentBaseToken))
                    continue;

                counts[slot]++;

                var list = samples[slot];
                if (list.Count < 4)
                {
                    bool isMale = MatchesToken(name, maleToken);
                    bool isFemale = MatchesToken(name, femaleToken);
                    string genderTag = isMale && isFemale ? "MF" : isMale ? "M" : isFemale ? "F" : "None";
                    list.Add($"{name}({genderTag})");
                }
            }
        }

        string summary =
            $"Head={counts[Slot.Head]}, Body={counts[Slot.Body]}, Hands={counts[Slot.Hands]}, Legs={counts[Slot.Legs]}, Feet={counts[Slot.Feet]}";
        string samplesText =
            $"Head=[{string.Join(", ", samples[Slot.Head])}] Body=[{string.Join(", ", samples[Slot.Body])}] " +
            $"Hands=[{string.Join(", ", samples[Slot.Hands])}] Legs=[{string.Join(", ", samples[Slot.Legs])}] " +
            $"Feet=[{string.Join(", ", samples[Slot.Feet])}]";

        Debug.Log($"[EquipmentSystem] {label} slot summary. {summary}. Samples: {samplesText}.", this);
    }

    private void CleanupSceneObjectsIfNeeded()
    {
        if (!cleanupSceneObjectsWhenNotUsingSceneLists)
        {
            if (logSceneCleanup)
                Debug.Log("[EquipmentSystem] Scene cleanup skipped (toggle disabled).", this);
            return;
        }

        bool usingSceneEquipment = !useAddressablesForEquipment && !usePrefabCatalog;
        bool usingSceneWeapons = !useAddressablesForWeapons && !usePrefabCatalog;
        bool usingSceneBaseBodies = manageBaseBodies && !useAddressablesForBaseBodies;

        if (usingSceneEquipment && usingSceneWeapons && (usingSceneBaseBodies || !manageBaseBodies))
        {
            if (logSceneCleanup)
                Debug.Log("[EquipmentSystem] Scene cleanup skipped (using scene lists for equipment/weapons/base bodies).", this);
            return;
        }

        bool cleanupEquipment = !usingSceneEquipment;
        bool cleanupWeapons = !usingSceneWeapons;
        bool cleanupBaseBodies = manageBaseBodies && !usingSceneBaseBodies;

        Transform[] roots = ResolveScanRootsForCleanup();
        if (roots == null || roots.Length == 0)
        {
            if (logSceneCleanup)
                Debug.Log("[EquipmentSystem] Scene cleanup skipped (no scan roots).", this);
            return;
        }

        float startTime = logSceneCleanup ? Time.realtimeSinceStartup : 0f;
        int scanned = 0;
        int renderable = 0;
        int excluded = 0;
        int equipmentMatches = 0;
        int weaponMatches = 0;
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

                bool isWeapon = IsWeaponName(name);
                bool hasSlot = TryResolveSlot(name, out _);
                bool isBaseBody = hasSlot && MatchesToken(name, equipmentBaseToken);
                bool isEquipment = hasSlot && !isBaseBody;

                if (isEquipment)
                    equipmentMatches++;
                if (isWeapon)
                    weaponMatches++;
                if (isBaseBody)
                    baseMatches++;

                if (logSceneCleanup && matchedLogged < sampleLimit && (isEquipment || isWeapon || isBaseBody))
                {
                    string rootName = child.root != null ? child.root.name : "<null>";
                    Debug.Log(
                        $"[EquipmentSystem] Cleanup match sample: '{name}' Root='{rootName}' Equipment={isEquipment} Weapon={isWeapon} BaseBody={isBaseBody} SlotTokenMatch={hasSlot}.",
                        this
                    );
                    matchedLogged++;
                }

                if (logSceneCleanup && unmatchedLogged < sampleLimit && !isEquipment && !isWeapon && !isBaseBody)
                {
                    string rootName = child.root != null ? child.root.name : "<null>";
                    Debug.Log(
                        $"[EquipmentSystem] Cleanup non-match sample: '{name}' Root='{rootName}'.",
                        this
                    );
                    unmatchedLogged++;
                }

                if ((cleanupEquipment && isEquipment) || (cleanupWeapons && isWeapon) || (cleanupBaseBodies && isBaseBody))
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
                $"[EquipmentSystem] Scene cleanup done. Roots={rootNames}. Scanned={scanned}, Renderable={renderable}, Excluded={excluded}, EquipmentMatches={equipmentMatches}, WeaponMatches={weaponMatches}, BaseMatches={baseMatches}, Deactivated={deactivated}, CleanupEquipment={cleanupEquipment}, CleanupWeapons={cleanupWeapons}, CleanupBaseBodies={cleanupBaseBodies}, TimeMs={elapsedMs:F1}.",
                this
            );
        }
    }

    private void ApplyCatalogEquipment()
    {
        var headPrefab = GetSelectedPrefab(GetSceneList(Slot.Head, gender), GetSlotIndex(Slot.Head));
        var bodyPrefab = GetSelectedPrefab(GetSceneList(Slot.Body, gender), GetSlotIndex(Slot.Body));
        var handsPrefab = GetSelectedPrefab(GetSceneList(Slot.Hands, gender), GetSlotIndex(Slot.Hands));
        var legsPrefab = GetSelectedPrefab(GetSceneList(Slot.Legs, gender), GetSlotIndex(Slot.Legs));
        var feetPrefab = GetSelectedPrefab(GetSceneList(Slot.Feet, gender), GetSlotIndex(Slot.Feet));

        if (logSceneCleanup)
        {
            Debug.Log(
                $"[EquipmentSystem] ApplyCatalogEquipment selections: Head='{headPrefab?.name ?? "<none>"}', Body='{bodyPrefab?.name ?? "<none>"}', Hands='{handsPrefab?.name ?? "<none>"}', Legs='{legsPrefab?.name ?? "<none>"}', Feet='{feetPrefab?.name ?? "<none>"}'.",
                this
            );
        }

        var root = ResolveEquipmentRoot();
        SwapPrefabInstance(ref headInstance, headPrefab, root);
        SwapPrefabInstance(ref bodyInstance, bodyPrefab, root);
        SwapPrefabInstance(ref handsInstance, handsPrefab, root);
        SwapPrefabInstance(ref legsInstance, legsPrefab, root);
        SwapPrefabInstance(ref feetInstance, feetPrefab, root);

        SetInstanceActive(headInstance, headPrefab != null);
        SetInstanceActive(bodyInstance, bodyPrefab != null);
        SetInstanceActive(handsInstance, handsPrefab != null);
        SetInstanceActive(legsInstance, legsPrefab != null);
        SetInstanceActive(feetInstance, feetPrefab != null);

        ApplyAllBaseBodySlotVisibility();
    }

    private void ApplyCatalogWeapon()
    {
        var prefab = GetSelectedPrefab(weaponObjects, weaponIndex);
        if (logSceneCleanup)
            Debug.Log($"[EquipmentSystem] ApplyCatalogWeapon selection: Weapon='{prefab?.name ?? "<none>"}'.", this);
        SwapPrefabInstance(ref weaponInstance, prefab, ResolveWeaponSocket());
        SetInstanceActive(weaponInstance, prefab != null);
    }

    private static void SetInstanceActive(GameObject instance, bool active)
    {
        if (instance != null)
            instance.SetActive(active);
    }

    private static GameObject GetSelectedPrefab(List<GameObject> list, int index)
    {
        if (list == null || index < 0 || index >= list.Count)
            return null;
        return list[index];
    }

    private void SwapPrefabInstance(ref GameObject instance, GameObject prefab, Transform parent)
    {
        if (prefab == null)
        {
            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance);
                instance = null;
            }
            return;
        }

        if (instance != null && string.Equals(instance.name, prefab.name, StringComparison.OrdinalIgnoreCase))
        {
            instance.SetActive(true);
            return;
        }

        if (instance != null)
            UnityEngine.Object.Destroy(instance);

        if (parent == null)
            parent = null;

        instance = UnityEngine.Object.Instantiate(prefab, parent);
        instance.name = prefab.name;
        instance.SetActive(false);
        if (activateNewInstances)
            instance.SetActive(true);
    }

    private bool HasSlotSelection(Slot slot)
    {
        int index = GetSlotIndex(slot);
        return index >= 0 && index < GetSlotCount(slot);
    }

    public bool IsSlotEquipped(Slot slot) => HasSlotSelection(slot);
    public bool IsSlotEnabled(Slot slot) => HasSlotSelection(slot);

    public void ToggleSlotEnabled(Slot slot)
    {
        SetSlotEnabled(slot, !IsSlotEnabled(slot));
    }

    public void SetSlotEnabled(Slot slot, bool enabled, bool applySelection = true)
    {
        int count = GetSlotCount(slot);
        if (count <= 0)
        {
            SetSlotIndex(slot, -1);
            if (applySelection)
                ApplySelection();
            return;
        }

        int current = GetSlotIndex(slot);
        if (enabled)
        {
            if (current >= 0)
                return;

            int restore = ClampIndex(GetLastEquippedIndex(slot), count, includeNone: false);
            if (restore < 0)
                restore = 0;
            SetSlotIndex(slot, restore);
        }
        else
        {
            if (current >= 0)
                SetLastEquippedIndex(slot, current);
            SetSlotIndex(slot, -1);
        }

        if (applySelection)
            ApplySelection();
    }

    public string GetSelectedBaseBodyName(Slot slot)
    {
        string token = GetSlotToken(slot);
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        return selectedBaseBodyBySlotToken.TryGetValue(token, out var selected)
            ? selected ?? string.Empty
            : string.Empty;
    }

    private void UpdateBaseBodyVisibility(Slot slot, bool hasEquipment)
    {
        if (!manageBaseBodies)
            return;

        if (!hideBaseBodyOnEquip)
            return;

        string slotToken = GetSlotToken(slot);
        if (string.IsNullOrWhiteSpace(slotToken))
            return;

        LogDiagnostics(
            $"[EquipmentSystem] UpdateBaseBodyVisibility slot={slot} token='{slotToken}' hasEquipment={hasEquipment} gender={gender}."
        );

        SetBaseBodySlotActive(slotToken, !hasEquipment);
    }

    private void ApplyAllBaseBodySlotVisibility()
    {
        UpdateBaseBodyVisibility(Slot.Head, HasSlotSelection(Slot.Head));
        UpdateBaseBodyVisibility(Slot.Body, HasSlotSelection(Slot.Body));
        UpdateBaseBodyVisibility(Slot.Hands, HasSlotSelection(Slot.Hands));
        UpdateBaseBodyVisibility(Slot.Legs, HasSlotSelection(Slot.Legs));
        UpdateBaseBodyVisibility(Slot.Feet, HasSlotSelection(Slot.Feet));
    }

    private void SetBaseBodySlotActive(string slotToken, bool active)
    {
        if (useAddressablesForBaseBodies)
        {
            SetAddressableBaseBodySlotActive(slotToken, active);
            return;
        }

        EnsureCharacterRoot();
        var roots = new List<Transform>(4);
        var equipmentRoot = ResolveEquipmentRoot();
        if (equipmentRoot != null)
            roots.Add(equipmentRoot);
        if (characterRoot != null && characterRoot != equipmentRoot)
            roots.Add(characterRoot);
        var scanRoots = ResolveScanRootsForCleanup();
        if (scanRoots != null && scanRoots.Length > 0)
        {
            for (int i = 0; i < scanRoots.Length; i++)
            {
                var root = scanRoots[i];
                if (root != null && !roots.Contains(root))
                    roots.Add(root);
            }
        }
        if (roots.Count == 0)
            roots.Add(transform);

        var instanceLookup = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        CacheBaseBodyInstances(instanceLookup, maleBaseBodyInstances);
        CacheBaseBodyInstances(instanceLookup, femaleBaseBodyInstances);
        var candidateSet = new HashSet<GameObject>();
        var candidates = new List<GameObject>();

        int toggledCount = 0;
        int rootsWithMatches = 0;
        bool captureSamples = logSceneCleanup || logToFile;
        var sample = captureSamples ? new List<string>(5) : null;

        foreach (var root in roots)
        {
            if (root == null)
                continue;

            int rootMatches = 0;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == null)
                    continue;

                var go = child.gameObject;
                if (go == null)
                    continue;

                string name = go.name;
                if (instanceLookup.TryGetValue(name, out var instance))
                {
                    go = instance;
                    name = go.name;
                }

                if (!MatchesToken(name, equipmentBaseToken))
                    continue;
                if (!MatchesToken(name, slotToken))
                    continue;
                if (!MatchesCurrentGender(name))
                    continue;

                if (!candidateSet.Add(go))
                    continue;

                candidates.Add(go);
                rootMatches++;
            }

            if (rootMatches > 0)
                rootsWithMatches++;
        }

        GameObject selected = active ? SelectPreferredBaseBodyCandidate(candidates, slotToken) : null;
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null)
                continue;

            bool shouldBeActive = active && candidate == selected;
            if (candidate.activeSelf == shouldBeActive)
                continue;

            SetItemActiveWithHierarchy(candidate, shouldBeActive);
            toggledCount++;
            if (sample != null && sample.Count < 5)
                sample.Add($"{candidate.name} -> {(shouldBeActive ? "active" : "inactive")}");
        }

        if (active && selected != null)
            selectedBaseBodyBySlotToken[slotToken] = selected.name;
        else
            selectedBaseBodyBySlotToken.Remove(slotToken);

        if (logSceneCleanup || logToFile)
        {
            string rootNames = string.Join(", ", roots.ConvertAll(r => r != null ? r.name : "<null>"));
            string sampleText = sample is { Count: > 0 } ? string.Join(", ", sample) : "<none>";
            LogDiagnostics(
                $"[EquipmentSystem] SetBaseBodySlotActive token='{slotToken}' active={active} selected='{selected?.name ?? "<none>"}' candidates={candidates.Count} toggled={toggledCount} roots={rootNames} rootsWithMatches={rootsWithMatches} sample={sampleText}."
            );
        }
    }

    private void SetAddressableBaseBodySlotActive(string slotToken, bool active)
    {
        var instances = gender == Gender.Male ? maleBaseBodyInstances : femaleBaseBodyInstances;
        var candidates = new List<GameObject>();

        for (int i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            if (instance == null)
                continue;

            string name = instance.name;
            if (!MatchesToken(name, equipmentBaseToken))
                continue;
            if (!MatchesToken(name, slotToken))
                continue;
            if (!MatchesCurrentGender(name))
                continue;

            candidates.Add(instance);
        }

        GameObject selected = active ? SelectPreferredBaseBodyCandidate(candidates, slotToken) : null;
        int toggled = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            bool shouldBeActive = active && candidate == selected;
            if (candidate != null && candidate.activeSelf != shouldBeActive)
            {
                SetItemActiveWithHierarchy(candidate, shouldBeActive);
                toggled++;
            }
        }

        if (active && selected != null)
            selectedBaseBodyBySlotToken[slotToken] = selected.name;
        else
            selectedBaseBodyBySlotToken.Remove(slotToken);

        if (logSceneCleanup || logToFile)
        {
            LogDiagnostics(
                $"[EquipmentSystem] SetAddressableBaseBodySlotActive token='{slotToken}' active={active} selected='{selected?.name ?? "<none>"}' candidates={candidates.Count} toggled={toggled}."
            );
        }
    }

    private GameObject SelectPreferredBaseBodyCandidate(List<GameObject> candidates, string slotToken)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        string preferredGenderToken = gender == Gender.Male ? maleToken : femaleToken;
        if (string.IsNullOrWhiteSpace(preferredGenderToken))
            return null;

        var preferredCandidates = new List<GameObject>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate == null)
                continue;

            if (MatchesToken(candidate.name, preferredGenderToken))
                preferredCandidates.Add(candidate);
        }

        if (preferredCandidates.Count == 0)
            return null;

        string exactDefault = $"{preferredGenderToken}{equipmentBaseToken}{slotToken}";

        if (!string.IsNullOrWhiteSpace(exactDefault))
        {
            for (int i = 0; i < preferredCandidates.Count; i++)
            {
                var candidate = preferredCandidates[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.name, exactDefault, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }

        GameObject best = null;
        int bestScore = int.MaxValue;
        string bestName = string.Empty;

        for (int i = 0; i < preferredCandidates.Count; i++)
        {
            var candidate = preferredCandidates[i];
            if (candidate == null)
                continue;

            string name = candidate.name ?? string.Empty;
            int score = 0;

            if (!MatchesToken(name, equipmentBaseToken))
                score += 100;
            score += name.Length;

            bool better = best == null
                || score < bestScore
                || (score == bestScore && string.Compare(name, bestName, StringComparison.OrdinalIgnoreCase) < 0);
            if (!better)
                continue;

            best = candidate;
            bestScore = score;
            bestName = name;
        }

        return best;
    }

    private static void CacheBaseBodyInstances(Dictionary<string, GameObject> lookup, List<GameObject> instances)
    {
        if (lookup == null || instances == null)
            return;

        for (int i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            if (instance == null)
                continue;

            string name = instance.name;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            lookup[name] = instance;
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
                Debug.LogWarning($"[EquipmentSystem] Failed to write log file '{logFileName}': {ex.Message}", this);
            logToFile = false;
        }
    }

    private void ReparentRuntimeInstances(Transform newEquipmentRoot, Transform newWeaponRoot)
    {
        if (newEquipmentRoot != null)
        {
            ReparentInstance(headInstance, newEquipmentRoot);
            ReparentInstance(bodyInstance, newEquipmentRoot);
            ReparentInstance(handsInstance, newEquipmentRoot);
            ReparentInstance(legsInstance, newEquipmentRoot);
            ReparentInstance(feetInstance, newEquipmentRoot);
            ReparentInstances(maleBaseBodyInstances, newEquipmentRoot);
            ReparentInstances(femaleBaseBodyInstances, newEquipmentRoot);
        }

        if (newWeaponRoot != null)
            ReparentInstance(weaponInstance, newWeaponRoot);
    }

    private static void ReparentInstances(List<GameObject> instances, Transform newParent)
    {
        if (instances == null || newParent == null)
            return;

        for (int i = 0; i < instances.Count; i++)
            ReparentInstance(instances[i], newParent);
    }

    private static void ReparentInstance(GameObject instance, Transform newParent)
    {
        if (instance == null || newParent == null)
            return;

        var instanceTransform = instance.transform;
        if (instanceTransform.parent == newParent)
            return;

        instanceTransform.SetParent(newParent, true);
    }

    private bool MatchesCurrentGender(string name)
    {
        bool isMale = MatchesToken(name, maleToken);
        bool isFemale = MatchesToken(name, femaleToken);

        if (gender == Gender.Male && isFemale && !isMale)
            return false;
        if (gender == Gender.Female && isMale && !isFemale)
            return false;

        return true;
    }

    private bool TryEnsureEquipmentKeys()
    {
#if ENABLE_ADDRESSABLES
        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            return true;
        }

        if (!equipmentHandleValid)
        {
            if (logAddressables && !warnedEquipmentAddressablesNotLoaded)
            {
                Debug.LogWarning("[EquipmentSystem] Equipment Addressables not loaded. Call LoadAddressables first.");
                warnedEquipmentAddressablesNotLoaded = true;
            }
            return false;
        }

        if (equipmentLoadHandle.IsValid() && equipmentLoadHandle.Status == AsyncOperationStatus.Succeeded)
            UpdateEquipmentKeys(equipmentLoadHandle.Result);

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

    private void EnsureBaseBodyInstances(Gender forGender)
    {
        EnsureCharacterRoot();

        var keys = forGender == Gender.Male ? maleBaseBodyKeys : femaleBaseBodyKeys;
        var instances = forGender == Gender.Male ? maleBaseBodyInstances : femaleBaseBodyInstances;
        var pending = forGender == Gender.Male ? maleBaseBodyPendingKeys : femaleBaseBodyPendingKeys;

        if (keys.Count == 0)
        {
            if (logAddressables)
                Debug.LogWarning($"[EquipmentSystem] No base body options found for {forGender}. Label='{addressablesLabelBody}', BaseToken='{equipmentBaseToken}'.");
            return;
        }

        ReleaseBodyInstancesNotInKeys(instances, pending, keys);

        foreach (var key in keys)
        {
            if (HasBodyInstance(instances, key) || pending.Contains(key))
                continue;

            pending.Add(key);

#if ENABLE_ADDRESSABLES
        Addressables.InstantiateAsync(key, ResolveEquipmentRoot()).Completed += handle =>
        {
            pending.Remove(key);
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                    if (logAddressables)
                        Debug.LogWarning($"[EquipmentSystem] Failed to load Body '{key}'.");
                    return;
                }

            var instance = handle.Result;
            instance.name = key;
            instance.SetActive(false);
            if (gender != forGender)
            {
                Addressables.ReleaseInstance(instance);
                return;
            }

            instances.Add(instance);
            ApplyBaseBodyActivation(forGender);
            ApplyAllBaseBodySlotVisibility();
        };
#endif
    }
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

    private bool TryEnsureBaseBodyKeys()
    {
#if ENABLE_ADDRESSABLES
        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            return true;
        }

        if (!bodyHandleValid)
        {
            if (logAddressables && !warnedBodyAddressablesNotLoaded)
            {
                Debug.LogWarning("[EquipmentSystem] Body Addressables not loaded. Call LoadAddressables first.");
                warnedBodyAddressablesNotLoaded = true;
            }
            return false;
        }

        if (bodyLoadHandle.IsValid() && bodyLoadHandle.Status == AsyncOperationStatus.Succeeded)
            UpdateBaseBodyKeys(bodyLoadHandle.Result);

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

    private bool TryEnsureWeaponKeys()
    {
#if ENABLE_ADDRESSABLES
        if (UseAddressableIndex())
        {
            ApplyIndexKeys();
            return true;
        }

        if (!weaponsHandleValid)
        {
            if (logAddressables && !warnedWeaponAddressablesNotLoaded)
            {
                Debug.LogWarning("[EquipmentSystem] Weapons Addressables not loaded. Call LoadAddressables first.");
                warnedWeaponAddressablesNotLoaded = true;
            }
            return false;
        }

        if (weaponsLoadHandle.IsValid() && weaponsLoadHandle.Status == AsyncOperationStatus.Succeeded)
            UpdateWeaponKeys(weaponsLoadHandle.Result);

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
        return useAddressableIndex && ResolveDataIndexAsset() != null;
    }

    private void ApplyIndexKeys()
    {
        if (indexKeysLoaded)
            return;

        var index = ResolveDataIndexAsset();
        if (index == null)
            return;

        ClearEquipmentKeys();
        ClearBaseBodyKeys();
        weaponKeys.Clear();

        if (!TryCopyIndexList(index, "maleHead", maleHeadKeys)) return;
        if (!TryCopyIndexList(index, "maleBody", maleBodyKeys)) return;
        if (!TryCopyIndexList(index, "maleHands", maleHandsKeys)) return;
        if (!TryCopyIndexList(index, "maleLegs", maleLegsKeys)) return;
        if (!TryCopyIndexList(index, "maleFeet", maleFeetKeys)) return;
        if (!TryCopyIndexList(index, "femaleHead", femaleHeadKeys)) return;
        if (!TryCopyIndexList(index, "femaleBody", femaleBodyKeys)) return;
        if (!TryCopyIndexList(index, "femaleHands", femaleHandsKeys)) return;
        if (!TryCopyIndexList(index, "femaleLegs", femaleLegsKeys)) return;
        if (!TryCopyIndexList(index, "femaleFeet", femaleFeetKeys)) return;
        if (!TryCopyIndexList(index, "maleBaseBodies", maleBaseBodyKeys)) return;
        if (!TryCopyIndexList(index, "femaleBaseBodies", femaleBaseBodyKeys)) return;
        if (!TryCopyIndexList(index, "weapons", weaponKeys)) return;

        SortEquipmentKeys();
        SortByName(maleBaseBodyKeys);
        SortByName(femaleBaseBodyKeys);
        SortByName(weaponKeys);

        indexKeysLoaded = true;
    }

    private void EnsureRuntimeDataSources()
    {
        if (runtimeCatalog == null)
            runtimeCatalog = Resources.Load<FfxivRuntimeCatalog>("FfxivRuntimeCatalog");

        if (addressableIndex == null)
            addressableIndex = Resources.Load<FfxivAddressableIndex>("FfxivAddressableIndex");

        if (loggedStartupMode)
            return;

        bool usingIndex = UseAddressableIndex();
        bool wantsAddressables = useAddressablesForEquipment || useAddressablesForWeapons || useAddressablesForBaseBodies;

        if (usingIndex && logAddressables && wantsAddressables)
        {
            Debug.Log(
                "[EquipmentSystem] useAddressableIndex is enabled. Async label preload handles are skipped; selection keys come from index/runtime catalog.",
                this
            );
        }
        else if (!usingIndex && logAddressables && wantsAddressables && !loadAddressablesOnAwake)
        {
            Debug.LogWarning(
                "[EquipmentSystem] Addressables are enabled while loadAddressablesOnAwake is disabled. Call LoadAddressables() before changing selections.",
                this
            );
        }

        loggedStartupMode = true;
    }

    private ScriptableObject ResolveDataIndexAsset()
    {
        if (runtimeCatalog != null)
            return runtimeCatalog;

        if (addressableIndex is ScriptableObject scriptableIndex)
            return scriptableIndex;

        runtimeCatalog = Resources.Load<FfxivRuntimeCatalog>("FfxivRuntimeCatalog");
        if (runtimeCatalog != null)
            return runtimeCatalog;

        addressableIndex = Resources.Load<FfxivAddressableIndex>("FfxivAddressableIndex");
        return addressableIndex as ScriptableObject;
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

    private void UpdateEquipmentKeys(IList<GameObject> assets)
    {
        ClearEquipmentKeys();
        if (assets == null)
            return;

        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            string name = asset.name;
            if (HasExcludedToken(name))
                continue;
            if (MatchesToken(name, equipmentBaseToken))
                continue;
            if (!TryResolveSlot(name, out var slot))
                continue;

            bool isMale = MatchesToken(name, maleToken);
            bool isFemale = MatchesToken(name, femaleToken);

            if (!isMale && !isFemale)
            {
                AddUnique(GetKeyList(slot, Gender.Male), name);
                AddUnique(GetKeyList(slot, Gender.Female), name);
                continue;
            }

            if (isMale)
                AddUnique(GetKeyList(slot, Gender.Male), name);
            if (isFemale)
                AddUnique(GetKeyList(slot, Gender.Female), name);
        }

        SortEquipmentKeys();
    }

    private void UpdateWeaponKeys(IList<GameObject> assets)
    {
        weaponKeys.Clear();
        if (assets == null)
            return;

        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            string name = asset.name;
            if (HasExcludedToken(name))
                continue;

            AddUnique(weaponKeys, name);
        }

        SortByName(weaponKeys);
    }

    private void UpdateBaseBodyKeys(IList<GameObject> assets)
    {
        ClearBaseBodyKeys();
        if (assets == null)
            return;

        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            string name = asset.name;
            if (HasExcludedToken(name))
                continue;
            if (!MatchesToken(name, equipmentBaseToken))
                continue;
            if (!TryResolveSlot(name, out _))
                continue;

            bool isMale = MatchesToken(name, maleToken);
            bool isFemale = MatchesToken(name, femaleToken);

            if (!isMale && !isFemale)
            {
                AddUnique(maleBaseBodyKeys, name);
                AddUnique(femaleBaseBodyKeys, name);
                continue;
            }

            if (isMale)
                AddUnique(maleBaseBodyKeys, name);
            if (isFemale)
                AddUnique(femaleBaseBodyKeys, name);
        }

        SortByName(maleBaseBodyKeys);
        SortByName(femaleBaseBodyKeys);

        maleBaseBodyPendingKeys.RemoveWhere(key => !maleBaseBodyKeys.Contains(key));
        femaleBaseBodyPendingKeys.RemoveWhere(key => !femaleBaseBodyKeys.Contains(key));
    }

    private void ClearEquipmentKeys()
    {
        maleHeadKeys.Clear();
        maleBodyKeys.Clear();
        maleHandsKeys.Clear();
        maleLegsKeys.Clear();
        maleFeetKeys.Clear();
        femaleHeadKeys.Clear();
        femaleBodyKeys.Clear();
        femaleHandsKeys.Clear();
        femaleLegsKeys.Clear();
        femaleFeetKeys.Clear();
    }

    private void ClearBaseBodyKeys()
    {
        maleBaseBodyKeys.Clear();
        femaleBaseBodyKeys.Clear();
        maleBaseBodyPendingKeys.Clear();
        femaleBaseBodyPendingKeys.Clear();
    }

    private void SortEquipmentKeys()
    {
        SortByName(maleHeadKeys);
        SortByName(maleBodyKeys);
        SortByName(maleHandsKeys);
        SortByName(maleLegsKeys);
        SortByName(maleFeetKeys);
        SortByName(femaleHeadKeys);
        SortByName(femaleBodyKeys);
        SortByName(femaleHandsKeys);
        SortByName(femaleLegsKeys);
        SortByName(femaleFeetKeys);
    }

    private string GetKeyForSlot(Slot slot, Gender forGender)
    {
        var list = GetSlotNames(slot, forGender);
        int index = GetSlotIndex(slot);
        if (index < 0 || index >= list.Count)
            return null;

        return list[index];
    }

    private void SwapAddressableInstance(
        GameObject currentInstance,
        Action<GameObject> assignInstance,
        Func<GameObject> getAssignedInstance,
        Func<string> getExpectedKey,
        Func<string> getPendingKey,
        Action<string> setPendingKey,
        string key,
        string label,
        Transform parent)
    {
        if (string.IsNullOrEmpty(key))
        {
            setPendingKey?.Invoke(null);
            if (currentInstance != null)
                currentInstance.SetActive(false);
            return;
        }

        if (currentInstance != null && string.Equals(currentInstance.name, key, StringComparison.OrdinalIgnoreCase))
        {
            setPendingKey?.Invoke(null);
            currentInstance.SetActive(true);
            return;
        }

        if (string.Equals(getPendingKey?.Invoke(), key, StringComparison.OrdinalIgnoreCase))
            return;

        setPendingKey?.Invoke(key);

#if ENABLE_ADDRESSABLES
        if (currentInstance != null)
        {
            Addressables.ReleaseInstance(currentInstance);
            assignInstance?.Invoke(null);
        }

        if (parent == null)
            parent = transform;

        Addressables.InstantiateAsync(key, parent).Completed += handle =>
        {
            bool stillPending = string.Equals(getPendingKey?.Invoke(), key, StringComparison.OrdinalIgnoreCase);
            if (!stillPending)
            {
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                    Addressables.ReleaseInstance(handle.Result);
                return;
            }

            setPendingKey?.Invoke(null);

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (logAddressables)
                    Debug.LogWarning($"[EquipmentSystem] Failed to load {label} '{key}'.");
                return;
            }

            string expectedKey = getExpectedKey?.Invoke();
            if (!string.Equals(expectedKey, key, StringComparison.OrdinalIgnoreCase))
            {
                Addressables.ReleaseInstance(handle.Result);
                return;
            }

            var instance = handle.Result;
            instance.name = key;

            var assigned = getAssignedInstance?.Invoke();
            if (assigned != null && assigned != instance)
                Addressables.ReleaseInstance(assigned);

            assignInstance?.Invoke(instance);
            SetInstanceActive(instance, true);
        };
#endif
    }

    private void ReleaseAddressables()
    {
#if ENABLE_ADDRESSABLES
        if (headInstance != null)
            Addressables.ReleaseInstance(headInstance);
        if (bodyInstance != null)
            Addressables.ReleaseInstance(bodyInstance);
        if (handsInstance != null)
            Addressables.ReleaseInstance(handsInstance);
        if (legsInstance != null)
            Addressables.ReleaseInstance(legsInstance);
        if (feetInstance != null)
            Addressables.ReleaseInstance(feetInstance);
        if (weaponInstance != null)
            Addressables.ReleaseInstance(weaponInstance);

        ReleaseBodyInstances(maleBaseBodyInstances, maleBaseBodyPendingKeys);
        ReleaseBodyInstances(femaleBaseBodyInstances, femaleBaseBodyPendingKeys);

        headInstance = null;
        bodyInstance = null;
        handsInstance = null;
        legsInstance = null;
        feetInstance = null;
        weaponInstance = null;
        pendingHeadKey = null;
        pendingBodyKey = null;
        pendingHandsKey = null;
        pendingLegsKey = null;
        pendingFeetKey = null;
        pendingWeaponKey = null;

        if (equipmentHandleValid && equipmentLoadHandle.IsValid())
            Addressables.Release(equipmentLoadHandle);
        if (weaponsHandleValid && weaponsLoadHandle.IsValid())
            Addressables.Release(weaponsLoadHandle);
        if (bodyHandleValid && bodyLoadHandle.IsValid())
            Addressables.Release(bodyLoadHandle);
#endif

#if ENABLE_ADDRESSABLES
        equipmentHandleValid = false;
        weaponsHandleValid = false;
        bodyHandleValid = false;
#endif

        ClearEquipmentKeys();
        weaponKeys.Clear();
        ClearBaseBodyKeys();
        selectedBaseBodyBySlotToken.Clear();
    }

    private void StepSlot(Slot slot, int delta)
    {
        int current = GetSlotIndex(slot);
        int next = CycleIndex(current, GetSlotCount(slot), delta, includeUnequippedOption);
        SetSlotIndex(slot, next);
        ApplySelection();
    }

    private void ClampIndices()
    {
        headIndex = ClampIndex(headIndex, GetSlotCount(Slot.Head), includeUnequippedOption);
        bodyIndex = ClampIndex(bodyIndex, GetSlotCount(Slot.Body), includeUnequippedOption);
        handsIndex = ClampIndex(handsIndex, GetSlotCount(Slot.Hands), includeUnequippedOption);
        legsIndex = ClampIndex(legsIndex, GetSlotCount(Slot.Legs), includeUnequippedOption);
        feetIndex = ClampIndex(feetIndex, GetSlotCount(Slot.Feet), includeUnequippedOption);
        weaponIndex = ClampIndex(weaponIndex, GetSlotCount(Slot.Weapon), includeUnequippedOption);
        lastHeadIndex = ClampIndex(lastHeadIndex, GetSlotCount(Slot.Head), includeNone: false);
        lastBodyIndex = ClampIndex(lastBodyIndex, GetSlotCount(Slot.Body), includeNone: false);
        lastHandsIndex = ClampIndex(lastHandsIndex, GetSlotCount(Slot.Hands), includeNone: false);
        lastLegsIndex = ClampIndex(lastLegsIndex, GetSlotCount(Slot.Legs), includeNone: false);
        lastFeetIndex = ClampIndex(lastFeetIndex, GetSlotCount(Slot.Feet), includeNone: false);
        lastWeaponIndex = ClampIndex(lastWeaponIndex, GetSlotCount(Slot.Weapon), includeNone: false);
    }

    private Transform ResolveEquipmentRoot()
    {
        if (equipmentRoot != null)
        {
            if (logSceneCleanup)
                Debug.Log($"[EquipmentSystem] ResolveEquipmentRoot -> equipmentRoot '{equipmentRoot.name}'.", this);
            return equipmentRoot;
        }

        EnsureCharacterRoot();
        if (logSceneCleanup)
        {
            string resolved = characterRoot != null ? characterRoot.name : transform.name;
            string source = characterRoot != null ? "characterRoot" : "self";
            Debug.Log($"[EquipmentSystem] ResolveEquipmentRoot -> {resolved} ({source}).", this);
        }
        return characterRoot != null ? characterRoot : transform;
    }

    private Transform ResolveWeaponSocket()
    {
        if (weaponSocket != null)
            return weaponSocket;

        if (autoFindWeaponSocket && !string.IsNullOrWhiteSpace(weaponSocketName))
        {
            var root = ResolveEquipmentRoot();
            if (root != null)
            {
                var direct = root.Find(weaponSocketName);
                if (direct != null)
                {
                    weaponSocket = direct;
                    return weaponSocket;
                }

                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && string.Equals(child.name, weaponSocketName, StringComparison.OrdinalIgnoreCase))
                    {
                        weaponSocket = child;
                        return weaponSocket;
                    }
                }
            }
        }

        return ResolveEquipmentRoot();
    }

    private int GetSlotIndex(Slot slot)
    {
        return slot switch
        {
            Slot.Head => headIndex,
            Slot.Body => bodyIndex,
            Slot.Hands => handsIndex,
            Slot.Legs => legsIndex,
            Slot.Feet => feetIndex,
            Slot.Weapon => weaponIndex,
            _ => -1
        };
    }

    private void SetSlotIndex(Slot slot, int index)
    {
        switch (slot)
        {
            case Slot.Head:
                headIndex = index;
                if (index >= 0)
                    lastHeadIndex = index;
                break;
            case Slot.Body:
                bodyIndex = index;
                if (index >= 0)
                    lastBodyIndex = index;
                break;
            case Slot.Hands:
                handsIndex = index;
                if (index >= 0)
                    lastHandsIndex = index;
                break;
            case Slot.Legs:
                legsIndex = index;
                if (index >= 0)
                    lastLegsIndex = index;
                break;
            case Slot.Feet:
                feetIndex = index;
                if (index >= 0)
                    lastFeetIndex = index;
                break;
            case Slot.Weapon:
                weaponIndex = index;
                if (index >= 0)
                    lastWeaponIndex = index;
                break;
        }
    }

    private int GetLastEquippedIndex(Slot slot)
    {
        return slot switch
        {
            Slot.Head => lastHeadIndex,
            Slot.Body => lastBodyIndex,
            Slot.Hands => lastHandsIndex,
            Slot.Legs => lastLegsIndex,
            Slot.Feet => lastFeetIndex,
            Slot.Weapon => lastWeaponIndex,
            _ => 0
        };
    }

    private void SetLastEquippedIndex(Slot slot, int index)
    {
        switch (slot)
        {
            case Slot.Head:
                lastHeadIndex = index;
                break;
            case Slot.Body:
                lastBodyIndex = index;
                break;
            case Slot.Hands:
                lastHandsIndex = index;
                break;
            case Slot.Legs:
                lastLegsIndex = index;
                break;
            case Slot.Feet:
                lastFeetIndex = index;
                break;
            case Slot.Weapon:
                lastWeaponIndex = index;
                break;
        }
    }

    private IReadOnlyList<string> GetSlotNames(Slot slot, Gender forGender)
    {
        if (slot == Slot.Weapon)
            return useAddressablesForWeapons ? weaponKeys : GetNameList(weaponObjects);

        if (useAddressablesForEquipment)
            return GetKeyList(slot, forGender);

        return GetNameList(GetSceneList(slot, forGender));
    }

    private List<string> GetKeyList(Slot slot, Gender forGender)
    {
        if (forGender == Gender.Male)
        {
            return slot switch
            {
                Slot.Head => maleHeadKeys,
                Slot.Body => maleBodyKeys,
                Slot.Hands => maleHandsKeys,
                Slot.Legs => maleLegsKeys,
                Slot.Feet => maleFeetKeys,
                _ => weaponKeys
            };
        }

        return slot switch
        {
            Slot.Head => femaleHeadKeys,
            Slot.Body => femaleBodyKeys,
            Slot.Hands => femaleHandsKeys,
            Slot.Legs => femaleLegsKeys,
            Slot.Feet => femaleFeetKeys,
            _ => weaponKeys
        };
    }

    private List<GameObject> GetSceneList(Slot slot, Gender forGender)
    {
        if (slot == Slot.Weapon)
            return weaponObjects;

        if (forGender == Gender.Male)
        {
            return slot switch
            {
                Slot.Head => maleHeadObjects,
                Slot.Body => maleBodyObjects,
                Slot.Hands => maleHandsObjects,
                Slot.Legs => maleLegsObjects,
                Slot.Feet => maleFeetObjects,
                _ => weaponObjects
            };
        }

        return slot switch
        {
            Slot.Head => femaleHeadObjects,
            Slot.Body => femaleBodyObjects,
            Slot.Hands => femaleHandsObjects,
            Slot.Legs => femaleLegsObjects,
            Slot.Feet => femaleFeetObjects,
            _ => weaponObjects
        };
    }

    private void AutoCollectFromScene()
    {
        EnsureCharacterRoot();
        ClearSceneLists();

        Transform[] roots = ResolveScanRoots();

        float startTime = logSceneCleanup ? Time.realtimeSinceStartup : 0f;
        int scanned = 0;
        int renderable = 0;
        int excluded = 0;
        int equipmentMatches = 0;
        int baseMatches = 0;
        int weaponMatches = 0;

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

                if (TryResolveSlot(name, out var slot))
                {
                    if (MatchesToken(name, equipmentBaseToken))
                    {
                        baseMatches++;
                        if (TryResolveBaseBodySlot(name, out var baseSlot))
                            AddBaseBodySceneObject(go, baseSlot, name);
                        continue;
                    }

                    equipmentMatches++;

                    bool isMale = MatchesToken(name, maleToken);
                    bool isFemale = MatchesToken(name, femaleToken);

                    if (!isMale && !isFemale)
                    {
                        AddUnique(GetSceneList(slot, Gender.Male), go);
                        AddUnique(GetSceneList(slot, Gender.Female), go);
                        continue;
                    }

                    if (isMale)
                        AddUnique(GetSceneList(slot, Gender.Male), go);
                    if (isFemale)
                        AddUnique(GetSceneList(slot, Gender.Female), go);

                    continue;
                }

                if (IsWeaponName(name))
                {
                    weaponMatches++;
                    AddUnique(weaponObjects, go);
                }
            }
        }

        SortSceneLists();

        if (logScanResults)
            LogScanSummary(roots);

        if (logSceneCleanup)
        {
            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            string rootNames = roots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(roots, r => r != null ? r.name : "<null>"));
            Debug.Log(
                $"[EquipmentSystem] Auto-collect done. Roots={rootNames}. Scanned={scanned}, Renderable={renderable}, Excluded={excluded}, EquipmentMatches={equipmentMatches}, BaseMatches={baseMatches}, WeaponMatches={weaponMatches}, TimeMs={elapsedMs:F1}.",
                this
            );
        }

        if (cleanupSceneObjectsWhenNotUsingSceneLists)
            CleanupSceneObjectsIfNeeded();
    }

    private void ClearSceneLists()
    {
        maleHeadObjects.Clear();
        maleBodyObjects.Clear();
        maleHandsObjects.Clear();
        maleLegsObjects.Clear();
        maleFeetObjects.Clear();
        femaleHeadObjects.Clear();
        femaleBodyObjects.Clear();
        femaleHandsObjects.Clear();
        femaleLegsObjects.Clear();
        femaleFeetObjects.Clear();
        weaponObjects.Clear();
        maleBaseBodyObjects.Clear();
        femaleBaseBodyObjects.Clear();
    }

    private void SortSceneLists()
    {
        SortByName(maleHeadObjects);
        SortByName(maleBodyObjects);
        SortByName(maleHandsObjects);
        SortByName(maleLegsObjects);
        SortByName(maleFeetObjects);
        SortByName(femaleHeadObjects);
        SortByName(femaleBodyObjects);
        SortByName(femaleHandsObjects);
        SortByName(femaleLegsObjects);
        SortByName(femaleFeetObjects);
        SortByName(weaponObjects);
        SortByName(maleBaseBodyObjects);
        SortByName(femaleBaseBodyObjects);
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
                $"[EquipmentSystem] ApplyList count={list.Count}, selectedIndex={selectedIndex}, selected={selectedName}, childCount={childCount}, rendererCount={rendererCount}.",
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
            Debug.Log($"[EquipmentSystem] ApplyList toggles: Activated={activated}, Deactivated={deactivated}.", this);
    }

    private static void SetItemActiveWithHierarchy(GameObject item, bool active)
    {
        if (item == null)
            return;

        var owner = item.GetComponentInParent<EquipmentSystem>();
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
                $"[EquipmentSystem] SetItemActiveWithHierarchy owner='{ownerName}' item='{item.name}' active={active} wasActive={wasActive} nowActive={item.activeSelf}.",
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

        var owner = item.GetComponentInParent<EquipmentSystem>();
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
                $"[EquipmentSystem] EnsureActiveChildren owner='{ownerName}' item='{item.name}' activatedChildren={activated}.",
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

    private Transform ResolveCharacterRoot()
    {
        if (!string.IsNullOrWhiteSpace(characterRootName))
        {
            var existing = GameObject.Find(characterRootName);
            if (existing != null)
            {
                if (logSceneCleanup)
                    Debug.Log($"[EquipmentSystem] ResolveCharacterRoot -> found '{existing.name}' by name '{characterRootName}'.", this);
                return existing.transform;
            }

            if (logSceneCleanup)
                Debug.Log($"[EquipmentSystem] ResolveCharacterRoot -> name '{characterRootName}' not found.", this);
        }

        var roots = ResolveScanRoots();
        if (roots.Length == 1 && roots[0] != null)
        {
            if (logSceneCleanup)
                Debug.Log($"[EquipmentSystem] ResolveCharacterRoot -> single scan root '{roots[0].name}'.", this);
            return roots[0];
        }

        if (!autoCreateCharacterRoot)
        {
            var fallback = roots.Length > 0 && roots[0] != null ? roots[0] : transform;
            if (logSceneCleanup)
            {
                string fallbackName = fallback != null ? fallback.name : "<null>";
                Debug.Log($"[EquipmentSystem] ResolveCharacterRoot -> auto-create disabled, using '{fallbackName}'.", this);
            }
            return fallback;
        }

        string rootName = string.IsNullOrWhiteSpace(characterRootName) ? "CharacterRoot" : characterRootName;
        var root = new GameObject(rootName).transform;

        if (logSceneCleanup)
        {
            string rootNames = roots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(roots, r => r != null ? r.name : "<null>"));
            Debug.Log(
                $"[EquipmentSystem] ResolveCharacterRoot -> created '{root.name}'. ParentScanRootsToCharacterRoot={parentScanRootsToCharacterRoot}. ScanRoots={rootNames}.",
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
                    Debug.Log($"[EquipmentSystem] ResolveScanRootsForCleanup -> added characterRoot '{characterRoot.name}'. Count={merged.Count}.", this);
            }

            roots = merged.ToArray();
        }

        return roots;
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
                    Debug.Log($"[EquipmentSystem] Using cached scan roots. Count={cachedScanRoots.Length}.", this);
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
                    Debug.Log($"[EquipmentSystem] Scan roots resolved by prefix '{scanRootNamePrefix}'. Count={cachedScanRoots.Length}. Roots={rootNames}.", this);
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
                Debug.Log($"[EquipmentSystem] Scan roots resolved by tokens. Count={cachedScanRoots.Length}. Roots={rootNames}.", this);
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

                    bool isWeapon = IsWeaponName(name);
                    bool hasSlot = TryResolveSlot(name, out _);
                    bool isBaseBody = hasSlot && MatchesToken(name, equipmentBaseToken);
                    bool isEquipment = hasSlot && !isBaseBody;

                if (!isWeapon && !isEquipment && !isBaseBody)
                    continue;

                    var rootTransform = child.root;
                    if (rootTransform == transform && root != null)
                        rootTransform = root.transform;

                    matches.Add(rootTransform);
                }
            }
        }

        return new List<Transform>(matches);
    }

    private void LogScanSummary(Transform[] roots)
    {
        int total = maleHeadObjects.Count + maleBodyObjects.Count + maleHandsObjects.Count + maleLegsObjects.Count + maleFeetObjects.Count
            + femaleHeadObjects.Count + femaleBodyObjects.Count + femaleHandsObjects.Count + femaleLegsObjects.Count + femaleFeetObjects.Count
            + weaponObjects.Count;

        string rootNames = roots.Length == 0 ? "<none>" : string.Join(", ", Array.ConvertAll(roots, r => r != null ? r.name : "<null>"));
        string rootName = characterRoot != null ? characterRoot.name : "<null>";

        if (total == 0)
        {
            Debug.LogWarning(
                $"[EquipmentSystem] No options found. CharacterRoot={rootName}. ScanRoots: {rootNames}. Tokens: male='{maleToken}', female='{femaleToken}'.",
                this
            );
            return;
        }

        Debug.Log(
            $"[EquipmentSystem] Options found. Male head={maleHeadObjects.Count}, body={maleBodyObjects.Count}, hands={maleHandsObjects.Count}, legs={maleLegsObjects.Count}, feet={maleFeetObjects.Count}. " +
            $"Female head={femaleHeadObjects.Count}, body={femaleBodyObjects.Count}, hands={femaleHandsObjects.Count}, legs={femaleLegsObjects.Count}, feet={femaleFeetObjects.Count}. Weapons={weaponObjects.Count}. " +
            $"CharacterRoot={rootName}. ScanRoots: {rootNames}.",
            this
        );
    }

    private bool TryResolveSlot(string name, out Slot slot)
    {
        if (MatchesToken(name, headToken))
        {
            slot = Slot.Head;
            return true;
        }

        if (MatchesToken(name, bodyToken))
        {
            slot = Slot.Body;
            return true;
        }

        if (MatchesToken(name, handsToken))
        {
            slot = Slot.Hands;
            return true;
        }

        if (MatchesToken(name, legsToken))
        {
            slot = Slot.Legs;
            return true;
        }

        if (MatchesToken(name, feetToken))
        {
            slot = Slot.Feet;
            return true;
        }

        slot = default;
        return false;
    }

    private bool TryResolveBaseBodySlot(string name, out Slot slot)
    {
        return TryResolveSlot(name, out slot);
    }

    private void AddBaseBodySceneObject(GameObject go, Slot slot, string name)
    {
        if (go == null)
            return;

        bool isMale = MatchesToken(name, maleToken);
        bool isFemale = MatchesToken(name, femaleToken);

        if (!isMale && !isFemale)
        {
            AddUnique(GetBaseBodyList(Gender.Male), go);
            AddUnique(GetBaseBodyList(Gender.Female), go);
            return;
        }

        if (isMale)
            AddUnique(GetBaseBodyList(Gender.Male), go);
        if (isFemale)
            AddUnique(GetBaseBodyList(Gender.Female), go);
    }

    private List<GameObject> GetBaseBodyList(Gender forGender)
    {
        return forGender == Gender.Male ? maleBaseBodyObjects : femaleBaseBodyObjects;
    }

    private string GetSlotToken(Slot slot)
    {
        return slot switch
        {
            Slot.Head => headToken,
            Slot.Body => bodyToken,
            Slot.Hands => handsToken,
            Slot.Legs => legsToken,
            Slot.Feet => feetToken,
            _ => string.Empty
        };
    }

    private bool IsWeaponName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(weaponPrefix))
            return false;

        return name.StartsWith(weaponPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static int ClampIndex(int index, int count, bool includeNone)
    {
        if (count <= 0)
            return -1;

        return includeNone
            ? Mathf.Clamp(index, -1, count - 1)
            : Mathf.Clamp(index, 0, count - 1);
    }

    private static int CycleIndex(int index, int count, int delta, bool includeNone)
    {
        if (count <= 0)
            return -1;

        if (includeNone)
        {
            int total = count + 1; // include the unequipped state at -1
            int currentPosition = Mathf.Clamp(index + 1, 0, total - 1);
            int nextPosition = (currentPosition + delta) % total;
            if (nextPosition < 0)
                nextPosition += total;

            return nextPosition - 1;
        }

        int current = Mathf.Clamp(index, 0, count - 1);
        int next = (current + delta) % count;
        if (next < 0)
            next += count;

        return next;
    }

    private static void SetInstancesActive(List<GameObject> instances, bool active)
    {
        if (instances == null)
            return;

        for (int i = 0; i < instances.Count; i++)
            SetInstanceActive(instances[i], active);
    }

    private static bool MatchesToken(string name, string token)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(token))
            return false;

        return name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
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

    private static bool HasRenderable(GameObject go)
    {
        if (go == null)
            return false;

        return go.GetComponentInChildren<Renderer>(true) != null;
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
