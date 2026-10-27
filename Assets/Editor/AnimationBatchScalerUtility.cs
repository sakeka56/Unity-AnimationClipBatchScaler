using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public enum AnimationScaleMode { TargetDuration, DurationRatio }

public static class AnimationBatchScalerUtility
{
    public static bool IsPositiveFinite(float value)
    {
        return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static List<AnimationClip> CollectClips(Object[] selection)
    {
        var result = new List<AnimationClip>();
        var seen = new HashSet<AnimationClip>();
        foreach (var obj in selection)
        {
            if (obj == null) continue;
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) ||
                !(path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                  path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))) continue;
            // Selecting a clip sub-asset means only that clip, not all clips in its FBX.
            var assets = obj is AnimationClip ? new Object[] { obj } : AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                var clip = asset as AnimationClip;
                if (clip != null && !string.IsNullOrEmpty(clip.name) &&
                    clip.name.IndexOf("_preview", StringComparison.OrdinalIgnoreCase) < 0 && seen.Add(clip))
                    result.Add(clip);
            }
        }
        return result;
    }

    public static string ValidateFolder(string folder)
    {
        folder = (folder ?? "").Replace((char)92, '/').TrimEnd('/');
        if (folder.Length == 0) return "";
        if (!(folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal)) ||
            !AssetDatabase.IsValidFolder(folder) || folder.Contains("/../") || folder.Contains("/./"))
            throw new ArgumentException("Choose an existing output folder inside Assets.");
        return folder;
    }

    public static AnimationClip CreateScaledClip(AnimationClip source, float ratio)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (!IsPositiveFinite(ratio) || !IsPositiveFinite(source.length * ratio))
            throw new ArgumentException("Duration and ratio must be finite and greater than zero.");
        var copy = Object.Instantiate(source);
        copy.hideFlags = HideFlags.None;
        copy.name = source.name;
        try
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    var key = keys[i];
                    key.time *= ratio;
                    key.inTangent /= ratio;
                    key.outTangent /= ratio;
                    keys[i] = key; // Keep weights, weightedMode and tangent-mode metadata.
                }
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(copy, binding, curve);
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
                for (int i = 0; i < keys.Length; i++) keys[i].time *= ratio;
                AnimationUtility.SetObjectReferenceCurve(copy, binding, keys);
            }
            var events = AnimationUtility.GetAnimationEvents(source);
            foreach (var evt in events) evt.time *= ratio;
            AnimationUtility.SetAnimationEvents(copy, events);
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.startTime *= ratio;
            settings.stopTime *= ratio;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            return copy;
        }
        catch
        {
            Object.DestroyImmediate(copy);
            throw;
        }
    }

    public static List<string> Scale(Object[] selection, AnimationScaleMode mode, float value,
        bool overwriteOriginal, string outputFolder)
    {
        if (!IsPositiveFinite(value)) throw new ArgumentException("Enter a finite value greater than zero.");
        outputFolder = ValidateFolder(outputFolder);
        var clips = CollectClips(selection);
        if (clips.Count == 0) throw new ArgumentException("Select AnimationClip (.anim) or FBX assets first.");
        var paths = new List<string>();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Scale animation clips");
        try
        {
            foreach (var clip in clips)
            {
                if (!IsPositiveFinite(clip.length))
                {
                    Debug.LogWarning("Skipped zero-length clip: " + clip.name);
                    continue;
                }
                float ratio = mode == AnimationScaleMode.TargetDuration ? value / clip.length : value;
                if (!IsPositiveFinite(ratio) || !IsPositiveFinite(clip.length * ratio))
                {
                    Debug.LogWarning("Skipped clip with unrepresentable duration: " + clip.name);
                    continue;
                }
                string sourcePath = AssetDatabase.GetAssetPath(clip);
                bool replace = overwriteOriginal && sourcePath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.IsMainAsset(clip) && AssetDatabase.IsOpenForEdit(clip);
                var scaled = CreateScaledClip(clip, ratio);
                try
                {
                    string path;
                    if (replace)
                    {
                        Undo.RegisterCompleteObjectUndo(clip, "Scale animation clip");
                        EditorUtility.CopySerialized(scaled, clip);
                        EditorUtility.SetDirty(clip);
                        path = sourcePath;
                    }
                    else
                    {
                        string folder = outputFolder.Length == 0
                            ? Path.GetDirectoryName(sourcePath).Replace((char)92, '/') : outputFolder;
                        string name = clip.name;
                        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
                        name = name.Replace('/', '_').Replace((char)92, '_');
                        if (string.IsNullOrWhiteSpace(name)) name = "Animation";
                        path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + "_scaled.anim");
                        scaled.name = Path.GetFileNameWithoutExtension(path);
                        AssetDatabase.CreateAsset(scaled, path);
                        Undo.RegisterCreatedObjectUndo(scaled, "Create scaled animation");
                    }
                    paths.Add(path);
                    Debug.Log("Scaled animation: " + path);
                }
                finally
                {
                    if (!EditorUtility.IsPersistent(scaled)) Object.DestroyImmediate(scaled);
                }
            }
            AssetDatabase.SaveAssets();
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
        return paths;
    }
}
