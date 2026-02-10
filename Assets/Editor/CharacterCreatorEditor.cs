using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterCreator))]
public class CharacterCreatorEditor : Editor
{
    private SerializedProperty characterRoot;
    private SerializedProperty autoCreateCharacterRoot;
    private SerializedProperty characterRootName;
    private SerializedProperty parentScanRootsToCharacterRoot;
    private SerializedProperty autoCollectFromScene;
    private SerializedProperty scanRoots;
    private SerializedProperty autoFindScanRootByPrefix;
    private SerializedProperty scanRootNamePrefix;
    private SerializedProperty includeInactive;
    private SerializedProperty maleToken;
    private SerializedProperty femaleToken;
    private SerializedProperty hairToken;
    private SerializedProperty faceToken;
    private SerializedProperty bodyTokens;
    private SerializedProperty logScanResults;
    private SerializedProperty cleanupSceneObjectsWhenNotUsingSceneLists;
    private SerializedProperty logSceneCleanup;
    private SerializedProperty runtimeCleanupPoll;
    private SerializedProperty runtimeCleanupPollDurationSeconds;
    private SerializedProperty runtimeCleanupPollIntervalSeconds;
    private SerializedProperty activateNewInstances;
    private SerializedProperty gender;
    private SerializedProperty maleHairIndex;
    private SerializedProperty maleFaceIndex;
    private SerializedProperty femaleHairIndex;
    private SerializedProperty femaleFaceIndex;
    private SerializedProperty manageBaseBodies;
    private SerializedProperty allowExternalHairVisibilityOverride;
    private SerializedProperty hairVisibleOverride;
    private SerializedProperty useAddressablesForHairFace;
    private SerializedProperty loadAddressablesOnAwake;
    private SerializedProperty addressablesLabelHair;
    private SerializedProperty addressablesLabelFace;
    private SerializedProperty addressablesLoadTimeoutSeconds;
    private SerializedProperty logAddressables;
    private SerializedProperty useAddressablesForBodies;
    private SerializedProperty addressablesLabelBody;
    private SerializedProperty bodyBaseToken;
    private SerializedProperty addressableIndex;
    private SerializedProperty useAddressableIndex;
    private SerializedProperty prefabCatalog;
    private SerializedProperty usePrefabCatalog;

    private void OnEnable()
    {
        characterRoot = serializedObject.FindProperty("characterRoot");
        autoCreateCharacterRoot = serializedObject.FindProperty("autoCreateCharacterRoot");
        characterRootName = serializedObject.FindProperty("characterRootName");
        parentScanRootsToCharacterRoot = serializedObject.FindProperty("parentScanRootsToCharacterRoot");
        autoCollectFromScene = serializedObject.FindProperty("autoCollectFromScene");
        scanRoots = serializedObject.FindProperty("scanRoots");
        autoFindScanRootByPrefix = serializedObject.FindProperty("autoFindScanRootByPrefix");
        scanRootNamePrefix = serializedObject.FindProperty("scanRootNamePrefix");
        includeInactive = serializedObject.FindProperty("includeInactive");
        maleToken = serializedObject.FindProperty("maleToken");
        femaleToken = serializedObject.FindProperty("femaleToken");
        hairToken = serializedObject.FindProperty("hairToken");
        faceToken = serializedObject.FindProperty("faceToken");
        bodyTokens = serializedObject.FindProperty("bodyTokens");
        logScanResults = serializedObject.FindProperty("logScanResults");
        cleanupSceneObjectsWhenNotUsingSceneLists = serializedObject.FindProperty("cleanupSceneObjectsWhenNotUsingSceneLists");
        logSceneCleanup = serializedObject.FindProperty("logSceneCleanup");
        runtimeCleanupPoll = serializedObject.FindProperty("runtimeCleanupPoll");
        runtimeCleanupPollDurationSeconds = serializedObject.FindProperty("runtimeCleanupPollDurationSeconds");
        runtimeCleanupPollIntervalSeconds = serializedObject.FindProperty("runtimeCleanupPollIntervalSeconds");
        activateNewInstances = serializedObject.FindProperty("activateNewInstances");
        gender = serializedObject.FindProperty("gender");
        maleHairIndex = serializedObject.FindProperty("maleHairIndex");
        maleFaceIndex = serializedObject.FindProperty("maleFaceIndex");
        femaleHairIndex = serializedObject.FindProperty("femaleHairIndex");
        femaleFaceIndex = serializedObject.FindProperty("femaleFaceIndex");
        manageBaseBodies = serializedObject.FindProperty("manageBaseBodies");
        allowExternalHairVisibilityOverride = serializedObject.FindProperty("allowExternalHairVisibilityOverride");
        hairVisibleOverride = serializedObject.FindProperty("hairVisibleOverride");
        useAddressablesForHairFace = serializedObject.FindProperty("useAddressablesForHairFace");
        loadAddressablesOnAwake = serializedObject.FindProperty("loadAddressablesOnAwake");
        addressablesLabelHair = serializedObject.FindProperty("addressablesLabelHair");
        addressablesLabelFace = serializedObject.FindProperty("addressablesLabelFace");
        addressablesLoadTimeoutSeconds = serializedObject.FindProperty("addressablesLoadTimeoutSeconds");
        logAddressables = serializedObject.FindProperty("logAddressables");
        useAddressablesForBodies = serializedObject.FindProperty("useAddressablesForBodies");
        addressablesLabelBody = serializedObject.FindProperty("addressablesLabelBody");
        bodyBaseToken = serializedObject.FindProperty("bodyBaseToken");
        addressableIndex = serializedObject.FindProperty("addressableIndex");
        useAddressableIndex = serializedObject.FindProperty("useAddressableIndex");
        prefabCatalog = serializedObject.FindProperty("prefabCatalog");
        usePrefabCatalog = serializedObject.FindProperty("usePrefabCatalog");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Roots", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(characterRoot);
        EditorGUILayout.PropertyField(autoCreateCharacterRoot);
        EditorGUILayout.PropertyField(characterRootName);
        EditorGUILayout.PropertyField(parentScanRootsToCharacterRoot);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Auto Collect (Scene)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(usePrefabCatalog);
        if (usePrefabCatalog.boolValue)
            EditorGUILayout.PropertyField(prefabCatalog);
        EditorGUILayout.PropertyField(autoCollectFromScene);
        if (autoCollectFromScene.boolValue)
        {
            EditorGUILayout.PropertyField(scanRoots, true);
            EditorGUILayout.PropertyField(autoFindScanRootByPrefix);
            if (autoFindScanRootByPrefix.boolValue)
                EditorGUILayout.PropertyField(scanRootNamePrefix);
            EditorGUILayout.PropertyField(includeInactive);
            EditorGUILayout.PropertyField(maleToken);
            EditorGUILayout.PropertyField(femaleToken);
            EditorGUILayout.PropertyField(hairToken);
            EditorGUILayout.PropertyField(faceToken);
            EditorGUILayout.PropertyField(bodyTokens, true);
            EditorGUILayout.PropertyField(logScanResults);

            if (GUILayout.Button("Rebuild Options From Scene"))
            {
                foreach (var targetObject in targets)
                {
                    if (targetObject is CharacterCreator creator)
                        creator.RebuildFromScene();
                }
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Scene Cleanup", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cleanupSceneObjectsWhenNotUsingSceneLists);
        EditorGUILayout.PropertyField(logSceneCleanup);
        EditorGUILayout.PropertyField(activateNewInstances);
        EditorGUILayout.PropertyField(runtimeCleanupPoll);
        if (runtimeCleanupPoll.boolValue)
        {
            EditorGUILayout.PropertyField(runtimeCleanupPollDurationSeconds);
            EditorGUILayout.PropertyField(runtimeCleanupPollIntervalSeconds);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(gender);
        EditorGUILayout.PropertyField(maleHairIndex);
        EditorGUILayout.PropertyField(maleFaceIndex);
        EditorGUILayout.PropertyField(femaleHairIndex);
        EditorGUILayout.PropertyField(femaleFaceIndex);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Base Bodies", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(manageBaseBodies);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Hair Visibility", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(allowExternalHairVisibilityOverride);
        if (allowExternalHairVisibilityOverride.boolValue)
            EditorGUILayout.PropertyField(hairVisibleOverride);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Addressables (Hair/Face)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useAddressableIndex);
        EditorGUILayout.PropertyField(addressableIndex);
        EditorGUILayout.PropertyField(useAddressablesForHairFace);
        if (useAddressablesForHairFace.boolValue && !useAddressableIndex.boolValue)
        {
            EditorGUILayout.PropertyField(loadAddressablesOnAwake);
            EditorGUILayout.PropertyField(addressablesLabelHair);
            EditorGUILayout.PropertyField(addressablesLabelFace);
            EditorGUILayout.PropertyField(addressablesLoadTimeoutSeconds);
            EditorGUILayout.PropertyField(logAddressables);

            if (GUILayout.Button("Load Addressables Now"))
            {
                foreach (var targetObject in targets)
                {
                    if (targetObject is CharacterCreator creator)
                        creator.LoadAddressables();
                }
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Addressables (Body)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useAddressablesForBodies);
        if (useAddressablesForBodies.boolValue && manageBaseBodies.boolValue && !useAddressableIndex.boolValue)
        {
            EditorGUILayout.PropertyField(addressablesLabelBody);
            EditorGUILayout.PropertyField(bodyBaseToken);
        }

        EditorGUILayout.HelpBox("Hair/face lists are auto-collected at runtime based on name tokens.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}
