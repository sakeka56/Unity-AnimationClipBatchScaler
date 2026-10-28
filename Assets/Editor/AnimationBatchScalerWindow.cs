using System;
using UnityEditor;
using UnityEngine;

public abstract class AnimationBatchScalerWindow : EditorWindow
{
    [SerializeField] private AnimationScaleMode mode;
    [SerializeField] private float targetLength = 1f;
    [SerializeField] private float durationRatio = 1f;
    [SerializeField] private bool overwriteOriginal;
    [SerializeField] private DefaultAsset outputFolder;
    private string status;
    private MessageType statusType;
    protected abstract bool Chinese { get; }
    private string Text(string cn, string en) { return Chinese ? cn : en; }

    protected virtual void OnEnable() { minSize = new Vector2(440, 330); }
    protected virtual void OnSelectionChange() { Repaint(); }

    protected virtual void OnGUI()
    {
        EditorGUILayout.LabelField(Text("批量动画缩放工具", "Animation Batch Scaler"), EditorStyles.boldLabel);
        mode = (AnimationScaleMode)EditorGUILayout.Popup(Text("缩放方式", "Scale mode"), (int)mode,
            new[] { Text("目标时长", "Target duration"), Text("时长比例", "Duration ratio") });
        if (mode == AnimationScaleMode.TargetDuration)
            targetLength = EditorGUILayout.FloatField(Text("目标时长（秒）", "Target duration (s)"), targetLength);
        else
        {
            durationRatio = EditorGUILayout.FloatField(Text("时长比例", "Duration ratio"), durationRatio);
            EditorGUILayout.HelpBox(Text("新时长 = 原时长 × 比例。0.5 为两倍速度，2 为一半速度。",
                "New duration = original duration × ratio. 0.5 doubles speed; 2 halves speed."), MessageType.Info);
        }
        overwriteOriginal = EditorGUILayout.Toggle(Text("覆盖原始 .anim", "Overwrite original .anim"), overwriteOriginal);
        var chosen = (DefaultAsset)EditorGUILayout.ObjectField(Text("输出文件夹", "Output folder"),
            outputFolder, typeof(DefaultAsset), false);
        if (chosen == null || AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(chosen))) outputFolder = chosen;
        if (GUILayout.Button(Text("选择输出文件夹…", "Browse output folder…")))
        {
            string absolute = EditorUtility.OpenFolderPanel(Text("选择 Assets 内的文件夹", "Choose a folder inside Assets"),
                Application.dataPath, "");
            if (!string.IsNullOrEmpty(absolute))
            {
                string relative = FileUtil.GetProjectRelativePath(absolute);
                try
                {
                    if (string.IsNullOrEmpty(relative)) throw new ArgumentException();
                    AnimationBatchScalerUtility.ValidateFolder(relative);
                    outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
                }
                catch { status = Text("请选择当前项目 Assets 内的文件夹。", "Choose a folder inside this project's Assets."); statusType = MessageType.Warning; }
            }
        }
        EditorGUILayout.HelpBox(overwriteOriginal
            ? Text("独立 .anim 原地修改并保留引用。FBX 内嵌或只读剪辑导出为新文件，可使用撤销。",
                "Standalone .anim assets are updated in place. Embedded/read-only clips export as new files. Undo is supported.")
            : Text("不选文件夹时保存在源文件旁。同名输出自动编号，不覆盖已有文件。",
                "Leave the folder empty to save beside the source. Existing filenames receive a unique suffix."), MessageType.Info);
        int count = AnimationBatchScalerUtility.CollectClips(Selection.objects).Count;
        EditorGUILayout.LabelField(Text("已选择动画剪辑", "Selected clips"), count.ToString());
        float value = mode == AnimationScaleMode.TargetDuration ? targetLength : durationRatio;
        bool valid = AnimationBatchScalerUtility.IsPositiveFinite(value);
        if (!valid) EditorGUILayout.HelpBox(Text("请输入大于 0 的有限数值。", "Enter a finite value greater than zero."), MessageType.Warning);
        using (new EditorGUI.DisabledScope(!valid || count == 0))
        {
            if (GUILayout.Button(Text("执行缩放", "Scale animations")))
            {
                try
                {
                    var paths = AnimationBatchScalerUtility.Scale(Selection.objects, mode, value, overwriteOriginal,
                        outputFolder == null ? "" : AssetDatabase.GetAssetPath(outputFolder));
                    status = Text("已处理动画：", "Processed clips: ") + paths.Count;
                    statusType = paths.Count > 0 ? MessageType.Info : MessageType.Warning;
                }
                catch (Exception ex) { status = ex.Message; statusType = MessageType.Error; Debug.LogException(ex); }
            }
        }
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("1.0.0", EditorStyles.miniLabel);
    }
}
