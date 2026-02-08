using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FFXIV/Addressable Index", fileName = "FfxivAddressableIndex")]
public class FfxivAddressableIndex : ScriptableObject
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

    [Header("Hair")]
    public List<string> maleHair = new();
    public List<string> femaleHair = new();

    [Header("Face")]
    public List<string> maleFace = new();
    public List<string> femaleFace = new();

    [Header("Equipment")]
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

    [Header("Base Bodies")]
    public List<string> maleBaseBodies = new();
    public List<string> femaleBaseBodies = new();

    [Header("Weapons")]
    public List<string> weapons = new();

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
