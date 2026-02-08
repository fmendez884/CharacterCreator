using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FFXIV/Prefab Catalog", fileName = "FfxivPrefabCatalog")]
public class FfxivPrefabCatalog : ScriptableObject
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

    [Header("Hair")]
    public List<GameObject> maleHair = new();
    public List<GameObject> femaleHair = new();

    [Header("Face")]
    public List<GameObject> maleFace = new();
    public List<GameObject> femaleFace = new();

    [Header("Equipment")]
    public List<GameObject> maleHead = new();
    public List<GameObject> maleBody = new();
    public List<GameObject> maleHands = new();
    public List<GameObject> maleLegs = new();
    public List<GameObject> maleFeet = new();
    public List<GameObject> femaleHead = new();
    public List<GameObject> femaleBody = new();
    public List<GameObject> femaleHands = new();
    public List<GameObject> femaleLegs = new();
    public List<GameObject> femaleFeet = new();

    [Header("Base Bodies")]
    public List<GameObject> maleBaseBodies = new();
    public List<GameObject> femaleBaseBodies = new();

    [Header("Weapons")]
    public List<GameObject> weapons = new();

    public void ClearLists()
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
}
