using UnityEditor;

public class AnimationBatchScaler_EN : AnimationBatchScalerWindow
{
    protected override bool Chinese { get { return false; } }
    [MenuItem("Tools/Animation Batch Scaler")]
    public static void ShowWindow() { GetWindow<AnimationBatchScaler_EN>("Animation Batch Scaler"); }
}
