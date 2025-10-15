using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AnimationBatchScaler_EN : EditorWindow
{
    private float targetLength = 1f;       // Target animation duration in seconds
    private bool overrideOriginal = false; // Whether to overwrite the original animation

    [MenuItem("Tools/Animation Batch Scaler")]
    public static void ShowWindow()
    {
        GetWindow<AnimationBatchScaler_EN>("Animation Batch Scaler");
    }

    private void OnGUI()
    {
        GUILayout.Label("Animation Batch Scaling Tool", EditorStyles.boldLabel);
        // Input target duration
        targetLength = EditorGUILayout.FloatField("Target Duration (seconds)", targetLength);
        // Choose whether to overwrite the original animation (default: create new file)
        overrideOriginal = EditorGUILayout.Toggle("Overwrite Original Animation", overrideOriginal);
        EditorGUILayout.Space();

        if (GUILayout.Button("Scale Animations"))
        {
            ScaleSelectedAnimations();
        }
        // Version info
        string versionText = "Version 1.0.0";
        var style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = Color.gray;
        style.alignment = TextAnchor.LowerRight;
        style.fontSize = 10;

        // Get window size
        Rect windowRect = position;
        float padding = 8f;
        Vector2 textSize = style.CalcSize(new GUIContent(versionText));
        Rect labelRect = new Rect(windowRect.width - textSize.x - padding, windowRect.height - textSize.y - padding, textSize.x, textSize.y);

        // Clickable version info (simulates label appearance)
        if (GUI.Button(labelRect, versionText, style))
        {
            // Action on click, e.g. open webpage or popup
            Application.OpenURL("https://your-version-info-url.com");
        }
    }

    private void ScaleSelectedAnimations()
    {
        if (targetLength <= 0f)
        {
            Debug.LogWarning("Target duration must be greater than 0");
            return;
        }

        // Get currently selected asset objects
        Object[] selectedAssets = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
        if (selectedAssets.Length == 0)
        {
            Debug.LogWarning("No asset files selected");
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
                    continue; // Only process FBX and anim files
                }
            }

            // Load all sub-assets from FBX file
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object subAsset in subAssets)
            {
                AnimationClip clip = subAsset as AnimationClip;
                if (clip == null) continue;
                // Skip unnamed default clips and preview clips
                if (string.IsNullOrEmpty(clip.name) 
                    || clip.name.Contains("_preview",System.StringComparison.OrdinalIgnoreCase)) continue;

                // Calculate scale factor
                float originalLength = clip.length;
                if (originalLength <= 0f)
                {
                    Debug.LogWarning($"Skipped animation clip '{clip.name}', original duration is 0");
                    continue;
                }
                float scaleFactor = targetLength / originalLength;

                // Create new AnimationClip and set properties
                AnimationClip newClip = new AnimationClip();
                newClip.name = clip.name + (overrideOriginal ? "" : "_scaled");
                newClip.frameRate = clip.frameRate;
                newClip.wrapMode = clip.wrapMode;
                newClip.legacy = clip.legacy;
                newClip.localBounds = clip.localBounds;

                // Scale all animation curves
                // Handle float value curves
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null) continue;
                    AnimationCurve newCurve = new AnimationCurve();
                    // Scale each keyframe's time and tangents
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

                // Handle ObjectReference curves (e.g. sprite switching)
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
                }

                // Handle animation events
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
                }

                // Determine new file save path
                string directory = Path.GetDirectoryName(assetPath);
                string newFileName = newClip.name + ".anim";
                string newAssetPath = Path.Combine(directory, newFileName).Replace("\\", "/");

                // If file exists, decide whether to delete based on overwrite option
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(newAssetPath) != null)
                {
                    if (overrideOriginal)
                    {
                        AssetDatabase.DeleteAsset(newAssetPath);
                    }
                    else
                    {
                        // If not overwriting, skip already existing scaled animation
                        Debug.LogWarning($"Target file already exists, skipped: {newAssetPath}");
                        continue;
                    }
                }

                // Create and save new animation asset
                AssetDatabase.CreateAsset(newClip, newAssetPath);
                Debug.Log($"Created scaled animation: {newAssetPath}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Animation scaling completed!");
    }
}