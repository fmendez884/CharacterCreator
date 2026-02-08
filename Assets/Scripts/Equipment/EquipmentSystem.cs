using System;
using System.Collections.Generic;
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

    [Header("Selection")]
    [SerializeField] private Gender gender = Gender.Male;
    [SerializeField] private int headIndex;
    [SerializeField] private int bodyIndex;
    [SerializeField] private int handsIndex;
    [SerializeField] private int legsIndex;
    [SerializeField] private int feetIndex;
    [SerializeField] private int weaponIndex;

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
    [SerializeField] private UnityEngine.Object addressableIndex;
    [SerializeField] private bool useAddressableIndex = true;

    public event Action Changed;

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
    private readonly List<GameObject> maleBaseBodyInstances = new();
    private readonly List<GameObject> femaleBaseBodyInstances = new();
    private bool indexKeysLoaded;

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

    public void SetManageBaseBodies(bool enabled)
    {
        manageBaseBodies = enabled;
        ApplySelection();
    }

    public void SetAddressableIndex(ScriptableObject index)
    {
        addressableIndex = index;
        indexKeysLoaded = false;
        ApplySelection();
    }

    private void Awake()
    {
        EnsureCharacterRoot();

        if (usePrefabCatalog)
            ApplyPrefabCatalog();
        else if (autoCollectFromScene)
            AutoCollectFromScene();

        if (UseAddressableIndex())
            ApplyIndexKeys();

        if (UseAddressableIndex())
            ApplyIndexKeys();

        if (!UseAddressableIndex() && loadAddressablesOnAwake && (useAddressablesForEquipment || useAddressablesForWeapons))
            LoadAddressables();

        ApplySelection();
    }

    private void OnDestroy()
    {
        ReleaseAddressables();
    }

    private void OnValidate()
    {
        ClampIndices();
    }

    public void SetCharacterRoot(Transform newRoot)
    {
        if (characterRoot == newRoot)
            return;

        characterRoot = newRoot;
        ApplySelection();
    }

    public void SetGender(Gender newGender)
    {
        if (gender != newGender)
            gender = newGender;

        ApplySelection();
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

    private void ApplySelection()
    {
        EnsureCharacterRoot();
        ClampIndices();

        if (characterRoot != null && !characterRoot.gameObject.activeSelf)
            characterRoot.gameObject.SetActive(true);

        if (manageBaseBodies)
            ApplyBaseBodies();
        else if (!usePrefabCatalog)
            ShowAllBaseBodySlots();

        if (useAddressablesForEquipment)
            ApplyAddressableEquipment();
        else if (usePrefabCatalog)
            ApplyCatalogEquipment();
        else
            ApplySceneEquipment();

        if (useAddressablesForWeapons)
            ApplyAddressableWeapon();
        else if (usePrefabCatalog)
            ApplyCatalogWeapon();
        else
            ApplySceneWeapon();

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

        SwapAddressableInstance(headInstance, instance => headInstance = instance, GetKeyForSlot(Slot.Head, gender), "Head", ResolveEquipmentRoot());
        SwapAddressableInstance(bodyInstance, instance => bodyInstance = instance, GetKeyForSlot(Slot.Body, gender), "Body", ResolveEquipmentRoot());
        SwapAddressableInstance(handsInstance, instance => handsInstance = instance, GetKeyForSlot(Slot.Hands, gender), "Hands", ResolveEquipmentRoot());
        SwapAddressableInstance(legsInstance, instance => legsInstance = instance, GetKeyForSlot(Slot.Legs, gender), "Legs", ResolveEquipmentRoot());
        SwapAddressableInstance(feetInstance, instance => feetInstance = instance, GetKeyForSlot(Slot.Feet, gender), "Feet", ResolveEquipmentRoot());

        UpdateBaseBodyVisibility(Slot.Head, HasSlotSelection(Slot.Head));
        UpdateBaseBodyVisibility(Slot.Body, HasSlotSelection(Slot.Body));
        UpdateBaseBodyVisibility(Slot.Hands, HasSlotSelection(Slot.Hands));
        UpdateBaseBodyVisibility(Slot.Legs, HasSlotSelection(Slot.Legs));
        UpdateBaseBodyVisibility(Slot.Feet, HasSlotSelection(Slot.Feet));
    }

    private void ApplySceneEquipment()
    {
        ApplyList(GetSceneList(Slot.Head, gender), headIndex);
        ApplyList(GetSceneList(Slot.Body, gender), bodyIndex);
        ApplyList(GetSceneList(Slot.Hands, gender), handsIndex);
        ApplyList(GetSceneList(Slot.Legs, gender), legsIndex);
        ApplyList(GetSceneList(Slot.Feet, gender), feetIndex);

        UpdateBaseBodyVisibility(Slot.Head, HasSlotSelection(Slot.Head));
        UpdateBaseBodyVisibility(Slot.Body, HasSlotSelection(Slot.Body));
        UpdateBaseBodyVisibility(Slot.Hands, HasSlotSelection(Slot.Hands));
        UpdateBaseBodyVisibility(Slot.Legs, HasSlotSelection(Slot.Legs));
        UpdateBaseBodyVisibility(Slot.Feet, HasSlotSelection(Slot.Feet));
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
            SetInstancesActive(maleBaseBodyInstances, true);
            ReleaseBodyInstances(femaleBaseBodyInstances, femaleBaseBodyPendingKeys);
        }
        else
        {
            EnsureBaseBodyInstances(Gender.Female);
            SetInstancesActive(femaleBaseBodyInstances, true);
            ReleaseBodyInstances(maleBaseBodyInstances, maleBaseBodyPendingKeys);
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
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null)
                continue;

            SetItemActiveWithHierarchy(item, enable);
        }
    }

    private void ApplyAddressableWeapon()
    {
        if (!TryEnsureWeaponKeys())
        {
            SetInstanceActive(weaponInstance, false);
            return;
        }

        SwapAddressableInstance(weaponInstance, instance => weaponInstance = instance, GetKeyForSlot(Slot.Weapon, gender), "Weapon", ResolveWeaponSocket());
    }

    private void ApplySceneWeapon()
    {
        ApplyList(weaponObjects, weaponIndex);
    }

    private void ApplyCatalogEquipment()
    {
        var headPrefab = GetSelectedPrefab(GetSceneList(Slot.Head, gender), GetSlotIndex(Slot.Head));
        var bodyPrefab = GetSelectedPrefab(GetSceneList(Slot.Body, gender), GetSlotIndex(Slot.Body));
        var handsPrefab = GetSelectedPrefab(GetSceneList(Slot.Hands, gender), GetSlotIndex(Slot.Hands));
        var legsPrefab = GetSelectedPrefab(GetSceneList(Slot.Legs, gender), GetSlotIndex(Slot.Legs));
        var feetPrefab = GetSelectedPrefab(GetSceneList(Slot.Feet, gender), GetSlotIndex(Slot.Feet));

        SwapPrefabInstance(ref headInstance, headPrefab, ResolveEquipmentRoot());
        SwapPrefabInstance(ref bodyInstance, bodyPrefab, ResolveEquipmentRoot());
        SwapPrefabInstance(ref handsInstance, handsPrefab, ResolveEquipmentRoot());
        SwapPrefabInstance(ref legsInstance, legsPrefab, ResolveEquipmentRoot());
        SwapPrefabInstance(ref feetInstance, feetPrefab, ResolveEquipmentRoot());

        UpdateBaseBodyVisibility(Slot.Head, headPrefab != null);
        UpdateBaseBodyVisibility(Slot.Body, bodyPrefab != null);
        UpdateBaseBodyVisibility(Slot.Hands, handsPrefab != null);
        UpdateBaseBodyVisibility(Slot.Legs, legsPrefab != null);
        UpdateBaseBodyVisibility(Slot.Feet, feetPrefab != null);
    }

    private void ApplyCatalogWeapon()
    {
        var prefab = GetSelectedPrefab(weaponObjects, weaponIndex);
        SwapPrefabInstance(ref weaponInstance, prefab, ResolveWeaponSocket());
    }

    private static GameObject GetSelectedPrefab(List<GameObject> list, int index)
    {
        if (list == null || index < 0 || index >= list.Count)
            return null;
        return list[index];
    }

    private static void SwapPrefabInstance(ref GameObject instance, GameObject prefab, Transform parent)
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
    }

    private bool HasSlotSelection(Slot slot)
    {
        int index = GetSlotIndex(slot);
        return index >= 0 && index < GetSlotCount(slot);
    }

    public bool IsSlotEquipped(Slot slot) => HasSlotSelection(slot);

    private void UpdateBaseBodyVisibility(Slot slot, bool hasEquipment)
    {
        if (!manageBaseBodies)
            return;

        if (!hideBaseBodyOnEquip)
            return;

        string slotToken = GetSlotToken(slot);
        if (string.IsNullOrWhiteSpace(slotToken))
            return;

        SetBaseBodySlotActive(slotToken, !hasEquipment);
    }

    private void SetBaseBodySlotActive(string slotToken, bool active)
    {
        EnsureCharacterRoot();
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
            if (!MatchesToken(name, slotToken))
                continue;
            if (!MatchesCurrentGender(name))
                continue;

            go.SetActive(active);
        }
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
            if (logAddressables)
                Debug.LogWarning("[EquipmentSystem] Equipment Addressables not loaded. Call LoadAddressables first.");
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
                instances.Add(instance);
                SetInstanceActive(instance, gender == forGender);
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
            if (logAddressables)
                Debug.LogWarning("[EquipmentSystem] Body Addressables not loaded. Call LoadAddressables first.");
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
            if (logAddressables)
                Debug.LogWarning("[EquipmentSystem] Weapons Addressables not loaded. Call LoadAddressables first.");
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
        return useAddressableIndex && addressableIndex != null;
    }

    private void ApplyIndexKeys()
    {
        if (indexKeysLoaded)
            return;

        var index = addressableIndex as ScriptableObject;
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

    private void SwapAddressableInstance(GameObject currentInstance, Action<GameObject> assignInstance, string key, string label, Transform parent)
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

        if (parent == null)
            parent = transform;

        Addressables.InstantiateAsync(key, parent).Completed += handle =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (logAddressables)
                    Debug.LogWarning($"[EquipmentSystem] Failed to load {label} '{key}'.");
                return;
            }

            var instance = handle.Result;
            instance.name = key;
            assignInstance?.Invoke(instance);
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
    }

    private void StepSlot(Slot slot, int delta)
    {
        int current = GetSlotIndex(slot);
        int next = CycleIndex(current, GetSlotCount(slot), delta);
        SetSlotIndex(slot, next);
        ApplySelection();
    }

    private void ClampIndices()
    {
        headIndex = ClampIndex(headIndex, GetSlotCount(Slot.Head));
        bodyIndex = ClampIndex(bodyIndex, GetSlotCount(Slot.Body));
        handsIndex = ClampIndex(handsIndex, GetSlotCount(Slot.Hands));
        legsIndex = ClampIndex(legsIndex, GetSlotCount(Slot.Legs));
        feetIndex = ClampIndex(feetIndex, GetSlotCount(Slot.Feet));
        weaponIndex = ClampIndex(weaponIndex, GetSlotCount(Slot.Weapon));
    }

    private Transform ResolveEquipmentRoot()
    {
        if (equipmentRoot != null)
            return equipmentRoot;

        EnsureCharacterRoot();
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
                break;
            case Slot.Body:
                bodyIndex = index;
                break;
            case Slot.Hands:
                handsIndex = index;
                break;
            case Slot.Legs:
                legsIndex = index;
                break;
            case Slot.Feet:
                feetIndex = index;
                break;
            case Slot.Weapon:
                weaponIndex = index;
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
        foreach (var root in roots)
        {
            if (root == null)
                continue;

            foreach (var child in root.GetComponentsInChildren<Transform>(includeInactive))
            {
                var go = child.gameObject;
                if (go == null || (characterRoot != null && go == characterRoot.gameObject))
                    continue;

                string name = go.name;
                if (!HasRenderable(go))
                    continue;

                if (HasExcludedToken(name))
                    continue;

                if (TryResolveSlot(name, out var slot))
                {
                    if (MatchesToken(name, equipmentBaseToken))
                    {
                        if (TryResolveBaseBodySlot(name, out var baseSlot))
                            AddBaseBodySceneObject(go, baseSlot, name);
                        continue;
                    }

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
                    AddUnique(weaponObjects, go);
                }
            }
        }

        SortSceneLists();

        if (logScanResults)
            LogScanSummary(roots);
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
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null)
                continue;

            SetItemActiveWithHierarchy(item, i == selectedIndex);
        }
    }

    private static void SetItemActiveWithHierarchy(GameObject item, bool active)
    {
        if (item == null)
            return;

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

        foreach (var child in item.GetComponentsInChildren<Transform>(true))
        {
            if (!child.gameObject.activeSelf)
                child.gameObject.SetActive(true);
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
                return existing.transform;
        }

        var roots = ResolveScanRoots();
        if (roots.Length == 1 && roots[0] != null)
            return roots[0];

        if (!autoCreateCharacterRoot)
            return roots.Length > 0 && roots[0] != null ? roots[0] : transform;

        string rootName = string.IsNullOrWhiteSpace(characterRootName) ? "CharacterRoot" : characterRootName;
        var root = new GameObject(rootName).transform;

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

    private Transform[] ResolveScanRoots()
    {
        bool hasManualRoot = scanRoots is { Length: > 0 } && Array.Exists(scanRoots, root => root != null);
        if (hasManualRoot)
            return scanRoots;

        if (autoFindScanRootByPrefix && !string.IsNullOrWhiteSpace(scanRootNamePrefix))
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                var roots = scene.GetRootGameObjects();
                var matches = new List<Transform>();

                foreach (var root in roots)
                {
                    if (root == null)
                        continue;

                    if (root.name.StartsWith(scanRootNamePrefix, StringComparison.OrdinalIgnoreCase))
                        matches.Add(root.transform);
                }

                if (matches.Count > 0)
                    return matches.ToArray();
            }
        }

        return new[] { transform };
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

    private static void SetInstancesActive(List<GameObject> instances, bool active)
    {
        if (instances == null)
            return;

        for (int i = 0; i < instances.Count; i++)
            SetInstanceActive(instances[i], active);
    }

    private static void SetInstanceActive(GameObject instance, bool active)
    {
        if (instance != null)
            instance.SetActive(active);
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
