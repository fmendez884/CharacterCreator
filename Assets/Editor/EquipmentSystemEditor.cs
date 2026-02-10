using UnityEditor;

[CustomEditor(typeof(EquipmentSystem))]
public class EquipmentSystemEditor : Editor
{
    private SerializedProperty addressableIndex;
    private SerializedProperty useAddressableIndex;
    private SerializedProperty useAddressablesForEquipment;
    private SerializedProperty useAddressablesForWeapons;
    private SerializedProperty loadAddressablesOnAwake;
    private SerializedProperty addressablesLabelEquipment;
    private SerializedProperty addressablesLabelWeapons;
    private SerializedProperty logAddressables;
    private SerializedProperty prefabCatalog;
    private SerializedProperty usePrefabCatalog;
    private SerializedProperty cleanupSceneObjectsWhenNotUsingSceneLists;
    private SerializedProperty logSceneCleanup;
    private SerializedProperty runtimeCleanupPoll;
    private SerializedProperty runtimeCleanupPollDurationSeconds;
    private SerializedProperty runtimeCleanupPollIntervalSeconds;
    private SerializedProperty activateNewInstances;

    private void OnEnable()
    {
        addressableIndex = serializedObject.FindProperty("addressableIndex");
        useAddressableIndex = serializedObject.FindProperty("useAddressableIndex");
        useAddressablesForEquipment = serializedObject.FindProperty("useAddressablesForEquipment");
        useAddressablesForWeapons = serializedObject.FindProperty("useAddressablesForWeapons");
        loadAddressablesOnAwake = serializedObject.FindProperty("loadAddressablesOnAwake");
        addressablesLabelEquipment = serializedObject.FindProperty("addressablesLabelEquipment");
        addressablesLabelWeapons = serializedObject.FindProperty("addressablesLabelWeapons");
        logAddressables = serializedObject.FindProperty("logAddressables");
        prefabCatalog = serializedObject.FindProperty("prefabCatalog");
        usePrefabCatalog = serializedObject.FindProperty("usePrefabCatalog");
        cleanupSceneObjectsWhenNotUsingSceneLists = serializedObject.FindProperty("cleanupSceneObjectsWhenNotUsingSceneLists");
        logSceneCleanup = serializedObject.FindProperty("logSceneCleanup");
        runtimeCleanupPoll = serializedObject.FindProperty("runtimeCleanupPoll");
        runtimeCleanupPollDurationSeconds = serializedObject.FindProperty("runtimeCleanupPollDurationSeconds");
        runtimeCleanupPollIntervalSeconds = serializedObject.FindProperty("runtimeCleanupPollIntervalSeconds");
        activateNewInstances = serializedObject.FindProperty("activateNewInstances");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Addressable Index", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useAddressableIndex);
        EditorGUILayout.PropertyField(addressableIndex);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Prefab Catalog (Scene-Based)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(usePrefabCatalog);
        if (usePrefabCatalog.boolValue)
            EditorGUILayout.PropertyField(prefabCatalog);

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
        EditorGUILayout.LabelField("Addressables", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useAddressablesForEquipment);
        EditorGUILayout.PropertyField(useAddressablesForWeapons);

        if (!useAddressableIndex.boolValue)
        {
            EditorGUILayout.PropertyField(loadAddressablesOnAwake);
            EditorGUILayout.PropertyField(addressablesLabelEquipment);
            EditorGUILayout.PropertyField(addressablesLabelWeapons);
            EditorGUILayout.PropertyField(logAddressables);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
