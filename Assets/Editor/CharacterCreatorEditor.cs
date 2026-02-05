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
    private SerializedProperty gender;
    private SerializedProperty maleHairIndex;
    private SerializedProperty maleFaceIndex;
    private SerializedProperty femaleHairIndex;
    private SerializedProperty femaleFaceIndex;
    private SerializedProperty useAddressablesForHairFace;
    private SerializedProperty loadAddressablesOnAwake;
    private SerializedProperty addressablesLabelHair;
    private SerializedProperty addressablesLabelFace;
    private SerializedProperty addressablesLoadTimeoutSeconds;
    private SerializedProperty logAddressables;
    private SerializedProperty useAddressablesForBodies;
    private SerializedProperty addressablesLabelBody;
    private SerializedProperty bodyBaseToken;

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
        gender = serializedObject.FindProperty("gender");
        maleHairIndex = serializedObject.FindProperty("maleHairIndex");
        maleFaceIndex = serializedObject.FindProperty("maleFaceIndex");
        femaleHairIndex = serializedObject.FindProperty("femaleHairIndex");
        femaleFaceIndex = serializedObject.FindProperty("femaleFaceIndex");
        useAddressablesForHairFace = serializedObject.FindProperty("useAddressablesForHairFace");
        loadAddressablesOnAwake = serializedObject.FindProperty("loadAddressablesOnAwake");
        addressablesLabelHair = serializedObject.FindProperty("addressablesLabelHair");
        addressablesLabelFace = serializedObject.FindProperty("addressablesLabelFace");
        addressablesLoadTimeoutSeconds = serializedObject.FindProperty("addressablesLoadTimeoutSeconds");
        logAddressables = serializedObject.FindProperty("logAddressables");
        useAddressablesForBodies = serializedObject.FindProperty("useAddressablesForBodies");
        addressablesLabelBody = serializedObject.FindProperty("addressablesLabelBody");
        bodyBaseToken = serializedObject.FindProperty("bodyBaseToken");
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
        EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(gender);
        EditorGUILayout.PropertyField(maleHairIndex);
        EditorGUILayout.PropertyField(maleFaceIndex);
        EditorGUILayout.PropertyField(femaleHairIndex);
        EditorGUILayout.PropertyField(femaleFaceIndex);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Addressables (Hair/Face)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useAddressablesForHairFace);
        if (useAddressablesForHairFace.boolValue)
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
        if (useAddressablesForBodies.boolValue)
        {
            EditorGUILayout.PropertyField(addressablesLabelBody);
            EditorGUILayout.PropertyField(bodyBaseToken);
        }

        EditorGUILayout.HelpBox("Hair/face lists are auto-collected at runtime based on name tokens.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}
