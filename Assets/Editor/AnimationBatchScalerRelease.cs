using System.IO;
using UnityEditor;

public static class AnimationBatchScalerRelease
{
    public const string Version = "1.0.0";

    public static void Export()
    {
        Directory.CreateDirectory("Build");
        AssetDatabase.ExportPackage(new[] {
            "Assets/Editor/AnimationBatchScaler_CN.cs",
            "Assets/Editor/AnimationBatchScaler_EN.cs",
            "Assets/Editor/AnimationBatchScalerUtility.cs",
            "Assets/Editor/AnimationBatchScalerWindow.cs",
            "Assets/Editor/AnimationSegmentEditor"
        }, "Build/UnityAnimationTools-" + Version + ".unitypackage", ExportPackageOptions.Recurse);
    }
}
