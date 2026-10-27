using System.IO;
using UnityEditor;

public static class AnimationBatchScalerRelease
{
    public static void Export()
    {
        Directory.CreateDirectory("Build");
        AssetDatabase.ExportPackage(new[] {
            "Assets/Editor/AnimationBatchScaler_CN.cs",
            "Assets/Editor/AnimationBatchScaler_EN.cs",
            "Assets/Editor/AnimationBatchScalerUtility.cs",
            "Assets/Editor/AnimationBatchScalerWindow.cs"
        }, "Build/AnimationClipBatchScaler-0.0.1.unitypackage", ExportPackageOptions.Default);
    }
}
