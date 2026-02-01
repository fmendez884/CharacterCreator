using System;
using UnityEngine;
using UnityEngine.Rendering;

public static class FfxivMaterialPolicy
{
    public const float DefaultCutoff = 0.5f;

    public static bool ApplyOpaqueAlphaClip(Material mat, float cutoff = DefaultCutoff)
    {
        if (mat == null)
            return false;

        bool changed = false;

        bool hasSurface = mat.HasProperty("_Surface");
        bool hasAlphaClip = mat.HasProperty("_AlphaClip");
        bool hasCutoff = mat.HasProperty("_Cutoff");

        if (hasSurface)
            changed |= SetFloat(mat, "_Surface", 0f); // Opaque

        if (hasAlphaClip)
            changed |= SetFloat(mat, "_AlphaClip", 1f);

        if (hasCutoff)
            changed |= SetFloat(mat, "_Cutoff", cutoff);

        if (hasSurface)
        {
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = (int)RenderQueue.Geometry;

            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        if (hasAlphaClip)
            mat.EnableKeyword("_ALPHATEST_ON");

        return changed;
    }

    public static bool ApplyHairDoubleSided(Material mat, bool enable)
    {
        if (mat == null || !mat.HasProperty("_Cull"))
            return false;

        float target = enable ? 0f : 2f;
        return SetFloat(mat, "_Cull", target);
    }

    public static bool ApplyFrontFaceOnly(Material mat)
    {
        if (mat == null || !mat.HasProperty("_Cull"))
            return false;

        return SetFloat(mat, "_Cull", 2f);
    }

    public static void EnsureUrpLitShader(Material mat)
    {
        if (mat == null)
            return;

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit != null && mat.shader != urpLit)
            mat.shader = urpLit;
    }

    public static bool IsHairAsset(string assetPath, string materialName = null, string rendererName = null)
    {
        string path = assetPath?.Replace("\\", "/") ?? string.Empty;

        if (path.IndexOf("/obj/hair/", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (path.IndexOf("_hir", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (!string.IsNullOrEmpty(materialName) && materialName.IndexOf("_hir", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (!string.IsNullOrEmpty(rendererName) && rendererName.IndexOf("_hir", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private static bool SetFloat(Material mat, string prop, float value)
    {
        if (!mat.HasProperty(prop))
            return false;
        if (Mathf.Approximately(mat.GetFloat(prop), value))
            return false;
        mat.SetFloat(prop, value);
        return true;
    }
}
