using UnityEditor;

public class AnimationBatchScaler_CN : AnimationBatchScalerWindow
{
    protected override bool Chinese { get { return true; } }
    [MenuItem("Tools/动画批量缩放器")]
    public static void ShowWindow() { GetWindow<AnimationBatchScaler_CN>("动画批量缩放器"); }
}
