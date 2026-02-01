using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class FfxivProcessedBuildStep : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!EditorPrefs.HasKey("FFXIV.Processed.SourceRoot"))
            return;

        string sourceRoot = EditorPrefs.GetString("FFXIV.Processed.SourceRoot", string.Empty);
        if (string.IsNullOrEmpty(sourceRoot))
            return;

        FfxivProcessedPrefabBuilder.BuildFromSourceRoot(sourceRoot);
    }
}
