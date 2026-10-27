using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Dependency-free EditMode regression runner. Run with -executeMethod AnimationBatchScalerTests.Run.
public static class AnimationBatchScalerTests
{
    private const string Root = "Assets/__AnimationScalerRegression";
    private static int assertions;
    private static readonly List<string> results = new List<string>();
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    private static void Near(float actual, float expected, string message)
    {
        Check(Mathf.Abs(actual - expected) < .001f, message + ": " + actual + " != " + expected);
    }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, message);
    }
    private static AnimationClip Source(string name, float duration)
    {
        var clip = new AnimationClip { name = name, frameRate = 30, wrapMode = WrapMode.Loop };
        var last = new Keyframe(duration, 2, 2, 3, .2f, .7f) { weightedMode = WeightedMode.Both };
        var curve = new AnimationCurve(new Keyframe(0, 0, 1, 1), last)
            { preWrapMode = WrapMode.PingPong, postWrapMode = WrapMode.Loop };
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), curve);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, new[] {
            new AnimationEvent { time = duration / 2, functionName = "TestEvent",
                intParameter = 42, stringParameter = "payload", floatParameter = .25f,
                messageOptions = SendMessageOptions.DontRequireReceiver } });
        AssetDatabase.CreateAsset(clip, Root + "/" + name + ".anim");
        return clip;
    }
    private static void Case(string name, Action action)
    {
        action();
        results.Add("PASS " + name);
        Debug.Log("SCALER TEST PASS " + name);
    }

    public static void Run()
    {
        assertions = 0;
        results.Clear();
        var selection = Selection.objects;
        bool created = false;
        try
        {
            if (AssetDatabase.IsValidFolder(Root)) throw new Exception("Refusing to overwrite existing test folder.");
            AssetDatabase.CreateFolder("Assets", "__AnimationScalerRegression");
            created = true;
            AssetDatabase.CreateFolder(Root, "Output");
            var clip = Source("Source", 2);
            var binding = AnimationUtility.GetCurveBindings(clip)[0];
            Case("Target duration and source preservation", () => {
                var paths = AnimationBatchScalerUtility.Scale(new Object[] { clip }, AnimationScaleMode.TargetDuration, 1, false, "");
                Check(paths.Count == 1, "One result");
                Near(AssetDatabase.LoadAssetAtPath<AnimationClip>(paths[0]).length, 1, "Target duration");
                Near(clip.length, 2, "Source unchanged");
                Check(paths[0] == Root + "/Source_scaled.anim", "Default output beside source");
            });
            Case("Weighted curve shape, loop settings and events", () => {
                var scaled = AnimationBatchScalerUtility.CreateScaledClip(clip, .5f);
                try
                {
                    var original = AnimationUtility.GetEditorCurve(clip, binding);
                    var curve = AnimationUtility.GetEditorCurve(scaled, binding);
                    var key = curve.keys[1];
                    Near(key.inTangent, original.keys[1].inTangent / .5f, "In tangent");
                    Near(key.outTangent, original.keys[1].outTangent / .5f, "Out tangent");
                    Near(key.inWeight, .2f, "In weight");
                    Near(key.outWeight, .7f, "Out weight");
                    Check(key.weightedMode == WeightedMode.Both, "Weighted mode preserved");
                    Check(curve.preWrapMode == original.preWrapMode && curve.postWrapMode == original.postWrapMode, "Wrap modes preserved");
                    for (int i = 0; i <= 20; i++)
                        Near(curve.Evaluate(i / 20f), original.Evaluate(i / 10f), "Sampled curve shape");
                    Check(AnimationUtility.GetAnimationClipSettings(scaled).loopTime, "Loop time preserved");
                    var evt = AnimationUtility.GetAnimationEvents(scaled)[0];
                    Near(evt.time, .5f, "Event time");
                    Check(evt.functionName == "TestEvent" && evt.intParameter == 42 && evt.stringParameter == "payload"
                        && evt.messageOptions == SendMessageOptions.DontRequireReceiver, "Event payload");
                    Near(evt.floatParameter, .25f, "Event float");
                    Near(scaled.frameRate, clip.frameRate, "Frame rate");
                }
                finally { Object.DestroyImmediate(scaled); }
            });
            Case("Ratio batch and custom output with unique filenames", () => {
                var second = Source("Second", 4);
                var paths = AnimationBatchScalerUtility.Scale(new Object[] { clip, second, clip },
                    AnimationScaleMode.DurationRatio, 2, false, Root + "/Output");
                Check(paths.Count == 2, "Duplicate selections deduplicated");
                Near(AssetDatabase.LoadAssetAtPath<AnimationClip>(paths[0]).length, 4, "First ratio");
                Near(AssetDatabase.LoadAssetAtPath<AnimationClip>(paths[1]).length, 8, "Second ratio");
                var again = AnimationBatchScalerUtility.Scale(new Object[] { clip }, AnimationScaleMode.DurationRatio, .5f, false, Root + "/Output");
                Check(again[0] != paths[0] && again[0].StartsWith(Root + "/Output/"), "Unique output path");
                Near(AssetDatabase.LoadAssetAtPath<AnimationClip>(paths[0]).length, 4, "Existing output unchanged");
            });
            Case("Object reference curve scaling", () => {
                var objectClip = Source("Objects", 2);
                var texture = new Texture2D(2, 2);
                AssetDatabase.CreateAsset(texture, Root + "/Texture.asset");
                var objectBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                // Use actual sprite references for the SpriteRenderer curve.
                var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
                AssetDatabase.AddObjectToAsset(sprite, texture);
                AnimationUtility.SetObjectReferenceCurve(objectClip, objectBinding, new[] {
                    new ObjectReferenceKeyframe { time = 0, value = sprite },
                    new ObjectReferenceKeyframe { time = 2, value = sprite } });
                var scaled = AnimationBatchScalerUtility.CreateScaledClip(objectClip, 2);
                try
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(scaled, objectBinding);
                    Near(keys[1].time, 4, "Reference key time");
                    Check(keys[1].value == sprite, "Reference value");
                }
                finally { Object.DestroyImmediate(scaled); }
            });
            Case("Overwrite preserves GUID and references; Undo restores duration", () => {
                string path = AssetDatabase.GetAssetPath(clip);
                string guid = AssetDatabase.AssetPathToGUID(path);
                Undo.IncrementCurrentGroup();
                var paths = AnimationBatchScalerUtility.Scale(new Object[] { clip }, AnimationScaleMode.TargetDuration, 3, true, "");
                Check(paths[0] == path && AssetDatabase.AssetPathToGUID(path) == guid, "GUID preserved");
                Check(AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == clip, "Object identity preserved");
                Near(clip.length, 3, "Overwrite duration");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Near(clip.length, 2, "Undo restores original duration");
            });
            Case("Invalid values, folders and unsupported selections", () => {
                foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                    Reject(() => AnimationBatchScalerUtility.Scale(new Object[] { clip }, AnimationScaleMode.DurationRatio, value, false, ""), "Invalid value rejected");
                Reject(() => AnimationBatchScalerUtility.ValidateFolder("../Outside"), "Outside folder rejected");
                Reject(() => AnimationBatchScalerUtility.ValidateFolder("AssetsMissing"), "Prefix rejected");
                Reject(() => AnimationBatchScalerUtility.ValidateFolder("Assets/DoesNotExist"), "Missing folder rejected");
                Reject(() => AnimationBatchScalerUtility.Scale(new Object[0], AnimationScaleMode.DurationRatio, 1, false, ""), "Empty selection rejected");
                var zero = new AnimationClip { name = "Zero" };
                AnimationUtility.SetEditorCurve(zero, binding, new AnimationCurve(new Keyframe(0, 0)));
                var zeroSettings = AnimationUtility.GetAnimationClipSettings(zero);
                zeroSettings.stopTime = 0;
                AnimationUtility.SetAnimationClipSettings(zero, zeroSettings);
                AssetDatabase.CreateAsset(zero, Root + "/Zero.anim");
                Check(AnimationBatchScalerUtility.Scale(new Object[] { zero }, AnimationScaleMode.DurationRatio, 1, false, "").Count == 0, "Zero duration skipped");
                Check(AnimationBatchScalerUtility.CollectClips(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(Root), null }).Count == 0, "Folder ignored");
            });
            Case("FBX discovery, embedded clip export and source preservation", () => {
                const string fixture = "Assets/Editor/Tests/Fixtures/Animated.fbx";
                string fbx = Root + "/Animated.fbx";
                Check(AssetDatabase.CopyAsset(fixture, fbx), "Copy synthetic FBX fixture");
                var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.preserveHierarchy = true;
                importer.clipAnimations = importer.defaultClipAnimations;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadMainAssetAtPath(fbx);
                var clips = AnimationBatchScalerUtility.CollectClips(new Object[] { model });
                Check(clips.Count > 0, "FBX animation discovered; default takes=" + importer.defaultClipAnimations.Length);
                float length = clips[0].length;
                Check(length > 0, "FBX duration");
                Check(AnimationBatchScalerUtility.CollectClips(new Object[] { model, clips[0] }).Count == clips.Count, "FBX selection deduplicated");
                Check(AnimationBatchScalerUtility.CollectClips(new Object[] { clips[0] }).Count == 1, "Single embedded clip selection");
                string hash = AssetDatabase.GetAssetDependencyHash(fbx).ToString();
                var paths = AnimationBatchScalerUtility.Scale(new Object[] { model }, AnimationScaleMode.DurationRatio, .5f, true, Root + "/Output");
                Check(paths.Count == clips.Count, "All FBX clips exported");
                Check(paths[0].StartsWith(Root + "/Output/") && paths[0].EndsWith(".anim"), "Embedded overwrite exports to folder");
                Near(AssetDatabase.LoadAssetAtPath<AnimationClip>(paths[0]).length, length * .5f, "FBX export duration");
                Near(clips[0].length, length, "FBX clip unchanged");
                Check(hash == AssetDatabase.GetAssetDependencyHash(fbx).ToString(), "FBX asset unchanged");
            });
            Case("Chinese and English window creation", () => {
                var cn = ScriptableObject.CreateInstance<AnimationBatchScaler_CN>();
                var en = ScriptableObject.CreateInstance<AnimationBatchScaler_EN>();
                Check(cn != null && en != null, "Both windows load");
                Object.DestroyImmediate(cn);
                Object.DestroyImmediate(en);
            });
            results.Add("PASS: " + results.Count + " cases, " + assertions + " assertions; Unity " + Application.unityVersion);
            Directory.CreateDirectory("TestReports");
            File.WriteAllLines("TestReports/regression.txt", results);
            Debug.Log("SCALER ALL TESTS PASSED: " + assertions + " assertions");
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory("TestReports");
            File.WriteAllText("TestReports/regression.txt", string.Join("\n", results) + "\nFAIL " + ex);
            Debug.LogException(ex);
            throw;
        }
        finally
        {
            Selection.objects = selection;
            Undo.ClearAll();
            if (created) AssetDatabase.DeleteAsset(Root);
            AssetDatabase.SaveAssets();
        }
    }
}
