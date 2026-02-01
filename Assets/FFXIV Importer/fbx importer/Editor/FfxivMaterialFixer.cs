using UnityEditor;
using UnityEngine;

public static class FfxivMaterialFixer
{
    private const string TargetRoot = "Assets/FFXIV_Imported";

    [MenuItem("Tools/FFXIV/Fix Materials (Alpha Clip + Hair Double-Sided)")]
    private static void FixMaterials()
    {
        var guids = AssetDatabase.FindAssets("t:Material", new[] { TargetRoot });
        int scanned = 0;
        int changed = 0;
        int alphaChanged = 0;
        int hairChanged = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                continue;

            scanned++;

            bool isHair = FfxivMaterialPolicy.IsHairAsset(path, mat.name);
            bool localChanged = false;

            if (FfxivMaterialPolicy.ApplyOpaqueAlphaClip(mat))
            {
                alphaChanged++;
                localChanged = true;
            }

            if (isHair)
            {
                if (FfxivMaterialPolicy.ApplyHairDoubleSided(mat, true))
                {
                    hairChanged++;
                    localChanged = true;
                }
            }
            else
            {
                if (FfxivMaterialPolicy.ApplyFrontFaceOnly(mat))
                {
                    localChanged = true;
                }
            }

            if (localChanged)
            {
                EditorUtility.SetDirty(mat);
                changed++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "FFXIV Materials Fixed",
            $"Scanned: {scanned}\nChanged: {changed}\nAlpha Clip Enabled: {alphaChanged}\nHair Double-Sided: {hairChanged}",
            "OK"
        );
    }

    [MenuItem("Tools/FFXIV/Fix Materials (Alpha Clip + Hair Double-Sided)", true)]
    private static bool FixMaterialsValidate()
    {
        return AssetDatabase.IsValidFolder(TargetRoot);
    }
}
