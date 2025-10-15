using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AnimationBatchScaler_CN : EditorWindow
{
    private float targetLength = 1f;       // 目标动画时长，单位秒
    private bool overrideOriginal = false; // 是否覆盖原始动画

    [MenuItem("Tools/动画批量缩放器")]
    public static void ShowWindow()
    {
        GetWindow<AnimationBatchScaler_CN>("动画批量缩放器");
    }

    private void OnGUI()
    {
        GUILayout.Label("批量动画缩放工具", EditorStyles.boldLabel);
        // 输入目标时长
        targetLength = EditorGUILayout.FloatField("目标时长 (秒)", targetLength);
        // 选择是否覆盖原始动画（默认生成新文件）
        overrideOriginal = EditorGUILayout.Toggle("覆盖原始动画", overrideOriginal);
        EditorGUILayout.Space();

        if (GUILayout.Button("执行缩放"))
        {
            ScaleSelectedAnimations();
        }
        // 版本信息
        string versionText = "版本 1.0.0";
        var style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = Color.gray;
        style.alignment = TextAnchor.LowerRight;
        style.fontSize = 10;

        // 获取窗口尺寸
        Rect windowRect = position;
        float padding = 8f;
        Vector2 textSize = style.CalcSize(new GUIContent(versionText));
        Rect labelRect = new Rect(windowRect.width - textSize.x - padding, windowRect.height - textSize.y - padding, textSize.x, textSize.y);

        // 可点击的版本信息（模拟Label外观）
        if (GUI.Button(labelRect, versionText, style))
        {
            // 点击后可执行操作，比如打开网页或弹窗
            Application.OpenURL("https://your-version-info-url.com");
        }
    }

    private void ScaleSelectedAnimations()
    {
        if (targetLength <= 0f)
        {
            Debug.LogWarning("目标时长必须大于 0");
            return;
        }

        // 获取当前选中的资产（资源）对象
        Object[] selectedAssets = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
        if (selectedAssets.Length == 0)
        {
            Debug.LogWarning("未选择任何资源文件");
            return;
        }

        foreach (Object obj in selectedAssets)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath))
            {
                if(!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)
                || !assetPath.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue; // 只处理 FBX和anim 文件
                }
                
            }
            

            // 加载 FBX 文件中的所有子资源
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object subAsset in subAssets)
            {
                AnimationClip clip = subAsset as AnimationClip;
                if (clip == null) continue;
                // 跳过没有名称的默认剪辑和预览剪辑
                if (string.IsNullOrEmpty(clip.name) 
                    || clip.name.Contains("_preview",System.StringComparison.OrdinalIgnoreCase)) continue;

                // 计算缩放因子
                float originalLength = clip.length;
                if (originalLength <= 0f)
                {
                    Debug.LogWarning($"跳过动画剪辑 '{clip.name}'，原始时长为 0");
                    continue;
                }
                float scaleFactor = targetLength / originalLength;

                // 创建新的 AnimationClip 并设置属性
                AnimationClip newClip = new AnimationClip();
                newClip.name = clip.name + (overrideOriginal ? "" : "_scaled");
                newClip.frameRate = clip.frameRate;
                newClip.wrapMode = clip.wrapMode;
                newClip.legacy = clip.legacy;
                newClip.localBounds = clip.localBounds;

                // 缩放所有动画曲线
                // 处理 float 值曲线
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null) continue;
                    AnimationCurve newCurve = new AnimationCurve();
                    // 缩放每个关键帧的时间和切线
                    foreach (Keyframe key in curve.keys)
                    {
                        float newTime = key.time * scaleFactor;
                        float newInTangent = key.inTangent / scaleFactor;
                        float newOutTangent = key.outTangent / scaleFactor;
                        Keyframe newKey = new Keyframe(newTime, key.value, newInTangent, newOutTangent);
                        newCurve.AddKey(newKey);
                    }
                    newCurve.preWrapMode = curve.preWrapMode;
                    newCurve.postWrapMode = curve.postWrapMode;
                    AnimationUtility.SetEditorCurve(newClip, binding, newCurve);
                }

                // 处理 ObjectReference 曲线（例如精灵切换等）
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    ObjectReferenceKeyframe[] refKeyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (refKeyframes == null || refKeyframes.Length == 0) continue;
                    var newRefKeyframes = new ObjectReferenceKeyframe[refKeyframes.Length];
                    for (int i = 0; i < refKeyframes.Length; i++)
                    {
                        newRefKeyframes[i] = new ObjectReferenceKeyframe()
                        {
                            time = refKeyframes[i].time * scaleFactor,
                            value = refKeyframes[i].value
                        };
                    }
                    AnimationUtility.SetObjectReferenceCurve(newClip, binding, newRefKeyframes);
                    Debug.Log("B");
                }

                // 处理动画事件
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
                if (events != null && events.Length > 0)
                {
                    var newEvents = new List<AnimationEvent>();
                    foreach (AnimationEvent evt in events)
                    {
                        AnimationEvent newEvt = new AnimationEvent();
                        newEvt.functionName = evt.functionName;
                        newEvt.stringParameter = evt.stringParameter;
                        newEvt.floatParameter = evt.floatParameter;
                        newEvt.intParameter = evt.intParameter;
                        newEvt.objectReferenceParameter = evt.objectReferenceParameter;
                        newEvt.time = evt.time * scaleFactor;
                        newEvents.Add(newEvt);
                    }
                    AnimationUtility.SetAnimationEvents(newClip, newEvents.ToArray());
                    Debug.Log("C");
                }

                // 确定新文件的保存路径
                string directory = Path.GetDirectoryName(assetPath);
                string newFileName = newClip.name + ".anim";
                string newAssetPath = Path.Combine(directory, newFileName).Replace("\\", "/");

                // 如果文件已存在，根据覆盖选项决定是否删除
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(newAssetPath) != null)
                {
                    if (overrideOriginal)
                    {
                        AssetDatabase.DeleteAsset(newAssetPath);
                    }
                    else
                    {
                        // 如果不覆盖原始动画，则跳过已经存在的缩放动画
                        Debug.LogWarning($"已存在目标文件，跳过: {newAssetPath}");
                        continue;
                    }
                }

                // 创建并保存新的动画资源
                AssetDatabase.CreateAsset(newClip, newAssetPath);
                Debug.Log($"已创建缩放动画: {newAssetPath}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("动画缩放处理完成！");
    }
}
