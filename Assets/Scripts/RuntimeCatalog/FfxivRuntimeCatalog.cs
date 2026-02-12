using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FFXIV/Runtime Catalog", fileName = "FfxivRuntimeCatalog")]
public class FfxivRuntimeCatalog : ScriptableObject
{
    [Header("Tokens")]
    public string maleToken = "c0101";
    public string femaleToken = "c0201";
    public string hairToken = "_hir";
    public string faceToken = "_fac";
    public string equipmentBaseToken = "e0000";
    public string headToken = "_met";
    public string bodyToken = "_top";
    public string handsToken = "_glv";
    public string legsToken = "_dwn";
    public string feetToken = "_sho";

    [Header("Labels")]
    public string labelHair = "Hair";
    public string labelFace = "Face";
    public string labelEquipment = "Equipment";
    public string labelWeapons = "Weapons";
    public string labelBody = "Body";

    [Header("Hair Keys")]
    public List<string> maleHair = new();
    public List<string> femaleHair = new();

    [Header("Face Keys")]
    public List<string> maleFace = new();
    public List<string> femaleFace = new();

    [Header("Equipment Keys")]
    public List<string> maleHead = new();
    public List<string> maleBody = new();
    public List<string> maleHands = new();
    public List<string> maleLegs = new();
    public List<string> maleFeet = new();
    public List<string> femaleHead = new();
    public List<string> femaleBody = new();
    public List<string> femaleHands = new();
    public List<string> femaleLegs = new();
    public List<string> femaleFeet = new();

    [Header("Base Body Keys")]
    public List<string> maleBaseBodies = new();
    public List<string> femaleBaseBodies = new();

    [Header("Weapon Keys")]
    public List<string> weapons = new();

    [Header("Hair Prefabs")]
    public List<GameObject> maleHairPrefabs = new();
    public List<GameObject> femaleHairPrefabs = new();

    [Header("Face Prefabs")]
    public List<GameObject> maleFacePrefabs = new();
    public List<GameObject> femaleFacePrefabs = new();

    [Header("Equipment Prefabs")]
    public List<GameObject> maleHeadPrefabs = new();
    public List<GameObject> maleBodyPrefabs = new();
    public List<GameObject> maleHandsPrefabs = new();
    public List<GameObject> maleLegsPrefabs = new();
    public List<GameObject> maleFeetPrefabs = new();
    public List<GameObject> femaleHeadPrefabs = new();
    public List<GameObject> femaleBodyPrefabs = new();
    public List<GameObject> femaleHandsPrefabs = new();
    public List<GameObject> femaleLegsPrefabs = new();
    public List<GameObject> femaleFeetPrefabs = new();

    [Header("Base Body Prefabs")]
    public List<GameObject> maleBaseBodyPrefabs = new();
    public List<GameObject> femaleBaseBodyPrefabs = new();

    [Header("Weapon Prefabs")]
    public List<GameObject> weaponPrefabs = new();

    public void ClearAll()
    {
        ClearKeyLists();
        ClearPrefabLists();
    }

    public void ClearKeyLists()
    {
        maleHair.Clear();
        femaleHair.Clear();
        maleFace.Clear();
        femaleFace.Clear();
        maleHead.Clear();
        maleBody.Clear();
        maleHands.Clear();
        maleLegs.Clear();
        maleFeet.Clear();
        femaleHead.Clear();
        femaleBody.Clear();
        femaleHands.Clear();
        femaleLegs.Clear();
        femaleFeet.Clear();
        maleBaseBodies.Clear();
        femaleBaseBodies.Clear();
        weapons.Clear();
    }

    public void ClearPrefabLists()
    {
        maleHairPrefabs.Clear();
        femaleHairPrefabs.Clear();
        maleFacePrefabs.Clear();
        femaleFacePrefabs.Clear();
        maleHeadPrefabs.Clear();
        maleBodyPrefabs.Clear();
        maleHandsPrefabs.Clear();
        maleLegsPrefabs.Clear();
        maleFeetPrefabs.Clear();
        femaleHeadPrefabs.Clear();
        femaleBodyPrefabs.Clear();
        femaleHandsPrefabs.Clear();
        femaleLegsPrefabs.Clear();
        femaleFeetPrefabs.Clear();
        maleBaseBodyPrefabs.Clear();
        femaleBaseBodyPrefabs.Clear();
        weaponPrefabs.Clear();
    }

    public void SortAll()
    {
        var stringComparer = StringComparer.OrdinalIgnoreCase;
        maleHair.Sort(stringComparer);
        femaleHair.Sort(stringComparer);
        maleFace.Sort(stringComparer);
        femaleFace.Sort(stringComparer);
        maleHead.Sort(stringComparer);
        maleBody.Sort(stringComparer);
        maleHands.Sort(stringComparer);
        maleLegs.Sort(stringComparer);
        maleFeet.Sort(stringComparer);
        femaleHead.Sort(stringComparer);
        femaleBody.Sort(stringComparer);
        femaleHands.Sort(stringComparer);
        femaleLegs.Sort(stringComparer);
        femaleFeet.Sort(stringComparer);
        maleBaseBodies.Sort(stringComparer);
        femaleBaseBodies.Sort(stringComparer);
        weapons.Sort(stringComparer);

        SortPrefabList(maleHairPrefabs);
        SortPrefabList(femaleHairPrefabs);
        SortPrefabList(maleFacePrefabs);
        SortPrefabList(femaleFacePrefabs);
        SortPrefabList(maleHeadPrefabs);
        SortPrefabList(maleBodyPrefabs);
        SortPrefabList(maleHandsPrefabs);
        SortPrefabList(maleLegsPrefabs);
        SortPrefabList(maleFeetPrefabs);
        SortPrefabList(femaleHeadPrefabs);
        SortPrefabList(femaleBodyPrefabs);
        SortPrefabList(femaleHandsPrefabs);
        SortPrefabList(femaleLegsPrefabs);
        SortPrefabList(femaleFeetPrefabs);
        SortPrefabList(maleBaseBodyPrefabs);
        SortPrefabList(femaleBaseBodyPrefabs);
        SortPrefabList(weaponPrefabs);
    }

    private static void SortPrefabList(List<GameObject> list)
    {
        list.Sort((a, b) => string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty, StringComparison.OrdinalIgnoreCase));
    }
}
