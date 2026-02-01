using System;
using System.Collections.Generic;
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

    [Header("Filters")]
    [SerializeField] private Category category = Category.Hair;
    [SerializeField] private string filterText = string.Empty;

    public event Action Changed;

    [Header("Addressables (Hair/Face)")]
    [SerializeField] private bool useAddressablesForHairFace = true;
    [SerializeField] private bool loadAddressablesOnAwake = true;
    [SerializeField] private string addressablesLabelHair = "Hair";
    [SerializeField] private string addressablesLabelFace = "Face";
    [SerializeField] private float addressablesLoadTimeoutSeconds = 10f;
    [SerializeField] private bool logAddressables = true;

    private readonly List<string> maleHairKeys = new();
    private readonly List<string> maleFaceKeys = new();
    private readonly List<string> femaleHairKeys = new();
    private readonly List<string> femaleFaceKeys = new();
#if ENABLE_ADDRESSABLES
    private AsyncOperationHandle<IList<GameObject>> hairLoadHandle;
    private AsyncOperationHandle<IList<GameObject>> faceLoadHandle;
    private bool hairHandleValid;
    private bool faceHandleValid;
#endif
    private GameObject maleHairInstance;
    private GameObject maleFaceInstance;
    private GameObject femaleHairInstance;
    private GameObject femaleFaceInstance;

    public Gender CurrentGender => gender;
    public Category CurrentCategory => category;
    public string FilterText => filterText;
    public int CurrentHairIndex => GetHairIndex(gender);
    public int CurrentFaceIndex => GetFaceIndex(gender);

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

        if (autoCollectFromScene)
            AutoCollectFromScene();

        if (useAddressablesForHairFace && loadAddressablesOnAwake)
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

    public void SetGender(Gender newGender)
    {
        if (gender == newGender)
        {
            ApplySelection();
            return;
        }

        gender = newGender;
        ApplySelection();
    }

    public void SetCategory(Category newCategory)
    {
        if (category == newCategory)
        {
            ApplySelection();
            return;
        }

        category = newCategory;
        ApplySelection();
    }

    public void SetFilterText(string value)
    {
        filterText = value ?? string.Empty;
        ApplySelection();
    }

    public void SetHairIndex(int index)
    {
        if (gender == Gender.Male)
        {
            maleHairIndex = ClampIndex(index, GetOptionCount(Category.Hair, Gender.Male));
        }
        else
        {
            femaleHairIndex = ClampIndex(index, GetOptionCount(Category.Hair, Gender.Female));
        }

        ApplySelection();
    }

    public void SetFaceIndex(int index)
    {
        if (gender == Gender.Male)
        {
            maleFaceIndex = ClampIndex(index, GetOptionCount(Category.Face, Gender.Male));
        }
        else
        {
            femaleFaceIndex = ClampIndex(index, GetOptionCount(Category.Face, Gender.Female));
        }

        ApplySelection();
    }

    public void NextHair()
    {
        if (gender == Gender.Male)
        {
            maleHairIndex = CycleIndex(maleHairIndex, GetOptionCount(Category.Hair, Gender.Male), 1);
        }
        else
        {
            femaleHairIndex = CycleIndex(femaleHairIndex, GetOptionCount(Category.Hair, Gender.Female), 1);
        }

        ApplySelection();
    }

    public void PreviousHair()
    {
        if (gender == Gender.Male)
        {
            maleHairIndex = CycleIndex(maleHairIndex, GetOptionCount(Category.Hair, Gender.Male), -1);
        }
        else
        {
            femaleHairIndex = CycleIndex(femaleHairIndex, GetOptionCount(Category.Hair, Gender.Female), -1);
        }

        ApplySelection();
    }

    public void NextFace()
    {
        if (gender == Gender.Male)
        {
            maleFaceIndex = CycleIndex(maleFaceIndex, GetOptionCount(Category.Face, Gender.Male), 1);
        }
        else
        {
            femaleFaceIndex = CycleIndex(femaleFaceIndex, GetOptionCount(Category.Face, Gender.Female), 1);
        }

        ApplySelection();
    }

    public void PreviousFace()
    {
        if (gender == Gender.Male)
        {
            maleFaceIndex = CycleIndex(maleFaceIndex, GetOptionCount(Category.Face, Gender.Male), -1);
        }
        else
        {
            femaleFaceIndex = CycleIndex(femaleFaceIndex, GetOptionCount(Category.Face, Gender.Female), -1);
        }

        ApplySelection();
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
        AutoCollectFromScene();
        ApplySelection();
    }

    [ContextMenu("Load Addressables (Hair/Face)")]
    public void LoadAddressables()
    {
        if (!useAddressablesForHairFace)
            return;

        _ = addressablesLoadTimeoutSeconds;

        ReleaseAddressables();

#if ENABLE_ADDRESSABLES

        if (!string.IsNullOrWhiteSpace(addressablesLabelHair))
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

        if (!string.IsNullOrWhiteSpace(addressablesLabelFace))
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
#else
        if (logAddressables)
            Debug.LogWarning("[CharacterCreator] Addressables package not available. Install Addressables to enable hair/face loading.");
#endif
    }

    private void ApplySelection()
    {
        EnsureCharacterRoot();
        ClampIndices();
        EnsureSelectionMatchesFilter(gender);

        if (characterRoot != null && !characterRoot.gameObject.activeSelf)
            characterRoot.gameObject.SetActive(true);

        ApplyGroup(maleBases, gender == Gender.Male);
        ApplyGroup(femaleBases, gender == Gender.Female);

        if (useAddressablesForHairFace)
        {
            ApplyAddressableSelection();
        }
        else
        {
            ApplyList(maleHairs, gender == Gender.Male ? maleHairIndex : -1);
            ApplyList(maleFaces, gender == Gender.Male ? maleFaceIndex : -1);
            ApplyList(femaleHairs, gender == Gender.Female ? femaleHairIndex : -1);
            ApplyList(femaleFaces, gender == Gender.Female ? femaleFaceIndex : -1);
        }

        Changed?.Invoke();
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

                bool isHair = MatchesToken(name, hairToken);
                bool isFace = MatchesToken(name, faceToken);
                bool isBody = MatchesAnyToken(name, bodyTokens);
                bool isMale = MatchesToken(name, maleToken);
                bool isFemale = MatchesToken(name, femaleToken);

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
    }

    private void ApplyAddressableSelection()
    {
        if (!TryEnsureAddressableKeys())
            return;

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
    }

    private bool TryEnsureAddressableKeys()
    {
#if ENABLE_ADDRESSABLES
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
        return false;
#endif
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
            assignInstance?.Invoke(instance);
        };
#endif
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

        maleHairInstance = null;
        maleFaceInstance = null;
        femaleHairInstance = null;
        femaleFaceInstance = null;

        if (hairHandleValid && hairLoadHandle.IsValid())
            Addressables.Release(hairLoadHandle);
        if (faceHandleValid && faceLoadHandle.IsValid())
            Addressables.Release(faceLoadHandle);

#endif

#if ENABLE_ADDRESSABLES
        hairHandleValid = false;
        faceHandleValid = false;
#endif

        maleHairKeys.Clear();
        maleFaceKeys.Clear();
        femaleHairKeys.Clear();
        femaleFaceKeys.Clear();
    }

    private static void SetInstanceActive(GameObject instance, bool active)
    {
        if (instance != null)
            instance.SetActive(active);
    }

    private void ApplyList(List<GameObject> list, int selectedIndex)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null)
                continue;

            if (i == selectedIndex)
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
    }

    private void ApplyGroup(List<GameObject> list, bool enable)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null)
                continue;

            if (enable)
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
