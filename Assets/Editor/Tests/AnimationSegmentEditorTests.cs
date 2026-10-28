using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AnimationSegmentEditorTests
{
    private const string Root = "Assets/__AnimationSegmentTests";
    private static int checks;
    private static readonly List<string> report = new List<string>();
    private static readonly EditorCurveBinding Binding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Near(float a, float b, string message, float tolerance = .001f) { Check(Mathf.Abs(a - b) <= tolerance, message + ": " + a + " != " + b); }
    private static void Case(string name, Action run) { run(); report.Add("PASS " + name); Debug.Log("SEGMENT TEST PASS " + name); }
    private static void Reject(Action action, string message)
    {
        bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, message);
    }
    private static AnimationEditSession NewSession(AnimationClip source)
    {
        var edit = ScriptableObject.CreateInstance<AnimationEditSession>(); edit.Initialize(source); return edit;
    }
    public static void Run()
    {
        checks = 0; report.Clear();
        var selection = Selection.objects;
        bool created = false;
        var temporary = new List<Object>();
        try
        {
            if (AssetDatabase.IsValidFolder(Root)) throw new Exception("Refusing to overwrite test folder.");
            AssetDatabase.CreateFolder("Assets", "__AnimationSegmentTests"); created = true;
            AssetDatabase.CreateFolder(Root, "Output");
            var source = new AnimationClip { name = "Motion", frameRate = 30 };
            var sourceCurve = new AnimationCurve(
                new Keyframe(0, 0, 0, 4, .2f, .7f) { weightedMode = WeightedMode.Both },
                new Keyframe(1, 2, -1, 3, .2f, .4f) { weightedMode = WeightedMode.Both },
                new Keyframe(2, 1, -2, 0, .6f, .3f) { weightedMode = WeightedMode.Both });
            AnimationUtility.SetEditorCurve(source, Binding, sourceCurve);
            var settings = AnimationUtility.GetAnimationClipSettings(source); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(source, settings);
            AnimationUtility.SetAnimationEvents(source, new[] {
                new AnimationEvent { time = .5f, functionName = "AtHalf", intParameter = 23, stringParameter = "payload", messageOptions = SendMessageOptions.DontRequireReceiver },
                new AnimationEvent { time = 1, functionName = "AtCut" },
                new AnimationEvent { time = 2, functionName = "AtEnd" } });
            AssetDatabase.CreateAsset(source, Root + "/Source.anim");
            AssetDatabase.SaveAssets();
            string sourceGuid = AssetDatabase.AssetPathToGUID(Root + "/Source.anim");
            string sourceBytes = File.ReadAllText(Root + "/Source.anim");

            Case("Split, frame snapping, metadata and shared boundary", () => {
                var edit = NewSession(source); temporary.Add(edit);
                var left = edit.segments[0];
                Check(edit.Split(left, 0) == null && edit.Split(left, 2) == null, "Reject zero length cuts");
                var right = edit.Split(left, .999f);
                Check(edit.segments.Count == 2, "Two segments");
                Near(left.End, right.start, "Timeline seam"); Near(left.sourceEnd, right.sourceStart, "Source seam");
                Near(edit.Duration, 2, "Split preserves duration");
                Check(left.colorIndex != right.colorIndex && left.name != right.name, "Distinct segment labels/colors");
                var first = AnimationSegmentExporter.Slice(source, left); temporary.Add(first);
                var second = AnimationSegmentExporter.Slice(source, right); temporary.Add(second);
                Check(AnimationUtility.GetAnimationEvents(first).Length == 1, "Cut event excluded from left");
                var events = AnimationUtility.GetAnimationEvents(second);
                Check(events.Length == 2 && events[0].functionName == "AtCut", "Cut event belongs to right once");
                Near(events[0].time, 0, "Event shifted to local zero");
                Near(events[1].time, 1, "Last source event retained");
            });
            Case("Weighted Bezier crop between keys and retiming preserve curve shape", () => {
                foreach (var range in new[] { new Vector2(.27f, .81f), new Vector2(.3f, 1.8f), new Vector2(0, 2) })
                {
                    var segment = new AnimationEditSegment { name = "Weighted", sourceStart = range.x, sourceEnd = range.y, duration = 2.4f };
                    var sliced = AnimationSegmentExporter.Slice(source, segment); temporary.Add(sliced);
                    var curve = AnimationUtility.GetEditorCurve(sliced, Binding);
                    Near(sliced.length, 2.4f, "Output duration");
                    for (int i = 0; i <= 100; i++)
                        Near(curve.Evaluate(segment.duration * i / 100), sourceCurve.Evaluate(Mathf.Lerp(range.x, range.y, i / 100f)), "Cropped shape", .002f);
                    Check(AnimationUtility.GetAnimationClipSettings(sliced).loopTime, "Loop setting preserved");
                    Near(sliced.frameRate, source.frameRate, "FPS preserved");
                }
            });
            Case("Step curves, short tracks and wrapped tracks", () => {
                var step = new AnimationCurve(new Keyframe(0, 2, 0, float.PositiveInfinity), new Keyframe(1, 5, float.PositiveInfinity, 0), new Keyframe(2, 5));
                var cut = AnimationSegmentExporter.SliceCurve(step, .5f, 1.5f, 2, 30);
                Near(cut.Evaluate(.99f), 2, "Step before seam"); Near(cut.Evaluate(1.01f), 5, "Step after seam");
                var shortCurve = new AnimationCurve(new Keyframe(.5f, 3), new Keyframe(1, 3));
                cut = AnimationSegmentExporter.SliceCurve(shortCurve, 0, 2, 2, 30);
                Near(cut.Evaluate(0), 3, "Leading constant"); Near(cut.Evaluate(2), 3, "Trailing constant");
                var wrap = AnimationCurve.Linear(0, 0, 1, 1); wrap.postWrapMode = WrapMode.PingPong;
                cut = AnimationSegmentExporter.SliceCurve(wrap, 0, 2, 2, 30);
                Near(cut.Evaluate(1.5f), .5f, "Wrapped extension sampled");
            });
            Case("Move, reorder, non-overlap, scale clamping and Undo", () => {
                var edit = NewSession(source); temporary.Add(edit);
                var first = edit.segments[0]; var second = edit.Split(first, 1);
                edit.Move(first, 2); edit.Validate();
                Check(edit.segments[0] == second, "Moving beyond neighbour reorders");
                Near(first.start, 2, "Moved first start");
                edit.Move(second, 1.8f); edit.Validate();
                Check(second.End <= first.start || second.start >= first.End, "Collision prevented");
                edit.Move(second, 0); edit.Move(first, 2);
                edit.Resize(second, 10); Near(second.duration, 2, "Resize stops at next segment");
                edit.Resize(second, -5); Near(second.duration, edit.Frame, "Minimum one frame");
                Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(edit, "Test segment move");
                edit.Move(first, 4); Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Near(edit.segments.Single(s => s.name == first.name).start, 2, "Undo restores segment");
            });
            Case("Object references and event payload survive split", () => {
                var clip = Object.Instantiate(source); temporary.Add(clip);
                var sprite = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
                temporary.Add(sprite.texture); temporary.Add(sprite);
                var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] {
                    new ObjectReferenceKeyframe { time = 0, value = sprite }, new ObjectReferenceKeyframe { time = 1.5f, value = null } });
                var segment = new AnimationEditSegment { name = "Objects", sourceStart = .5f, sourceEnd = 2, duration = 3 };
                var sliced = AnimationSegmentExporter.Slice(clip, segment); temporary.Add(sliced);
                var keys = AnimationUtility.GetObjectReferenceCurve(sliced, binding);
                Check(keys[0].value == sprite, "Reference state at start retained"); Near(keys[0].time, 0, "Reference local zero");
                Near(keys[1].time, 2, "Reference key retimed"); Check(keys[1].value == null, "Null transition preserved");
                var e = AnimationUtility.GetAnimationEvents(sliced)[0];
                Check(e.intParameter == 23 && e.stringParameter == "payload" && e.messageOptions == SendMessageOptions.DontRequireReceiver, "Event payload retained");
            });
            Case("Combined timeline frame samples, gaps and hard cuts", () => {
                var edit = NewSession(source); temporary.Add(edit);
                var first = edit.segments[0]; var second = edit.Split(first, 1);
                edit.Move(second, 3); edit.Resize(first, 2);
                var result = AnimationSegmentExporter.Combine(edit); temporary.Add(result);
                var curve = AnimationUtility.GetEditorCurve(result, Binding);
                Near(result.length, 4, "Combined duration");
                for (int frame = 0; frame <= 120; frame++)
                {
                    float time = frame / 30f; var segment = edit.At(time);
                    Near(curve.Evaluate(time), sourceCurve.Evaluate(segment.SourceTime(time)), "Frame matches preview", .003f);
                }
                Near(curve.Evaluate(2.5f), sourceCurve.Evaluate(1), "Gap holds last pose");
                edit.Move(second, 2); edit.Move(first, 3); edit.Move(second, 0); edit.Move(first, 1);
                var reordered = AnimationSegmentExporter.Combine(edit); temporary.Add(reordered);
                var reorderedCurve = AnimationUtility.GetEditorCurve(reordered, Binding);
                Near(reorderedCurve.Evaluate(0), sourceCurve.Evaluate(1), "Reordered first pose");
                Near(reorderedCurve.Evaluate(1), sourceCurve.Evaluate(0), "Hard cut begins next source");
            });
            Case("Export folder, unique names, source immutability and persisted session", () => {
                var edit = NewSession(source); temporary.Add(edit);
                edit.Split(edit.segments[0], 1); edit.segments[0].name = "Walk/Start"; edit.segments[1].name = "Walk/Start";
                var paths = AnimationSegmentExporter.Export(edit, Root + "/Output", true, true);
                Check(paths.Count == 3, "Two segments plus combined");
                Check(paths.All(p => p.StartsWith(Root + "/Output/") && AssetDatabase.LoadAssetAtPath<AnimationClip>(p) != null), "Exported assets load");
                var again = AnimationSegmentExporter.Export(edit, Root + "/Output", true, true);
                Check(!again.Intersect(paths).Any(), "No overwritten files");
                Check(sourceBytes == File.ReadAllText(Root + "/Source.anim") && sourceGuid == AssetDatabase.AssetPathToGUID(Root + "/Source.anim"), "Source bytes and GUID unchanged");
                var saved = Object.Instantiate(edit); AssetDatabase.CreateAsset(saved, Root + "/Session.asset"); AssetDatabase.SaveAssets();
                var loaded = AssetDatabase.LoadAssetAtPath<AnimationEditSession>(Root + "/Session.asset");
                Check(loaded.source == source && loaded.segments.Count == 2 && loaded.segments[0].name == "Walk/Start", "Editing session persisted");
                Reject(() => AnimationSegmentExporter.Export(edit, "", true, true), "Empty export path rejected");
                Reject(() => AnimationSegmentExporter.Export(edit, "../Outside", true, true), "Outside path rejected");
                Reject(() => AnimationSegmentExporter.Export(edit, Root, false, false), "No output mode rejected");
                edit.segments[0].duration = float.NaN; Reject(edit.Validate, "Corrupt session rejected");
            });
            Case("Imported FBX clips can be split and exported", () => {
                const string fixture = "Assets/Editor/Tests/Fixtures/Animated.fbx";
                string fbx = Root + "/Animated.fbx"; Check(AssetDatabase.CopyAsset(fixture, fbx), "Copy FBX");
                var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
                importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = true;
                importer.preserveHierarchy = true; importer.SaveAndReimport();
                var take = importer.defaultClipAnimations[0];
                importer.clipAnimations = new[] {
                    new ModelImporterClipAnimation { name = "First", takeName = take.takeName, firstFrame = take.firstFrame, lastFrame = (take.firstFrame + take.lastFrame) / 2 },
                    new ModelImporterClipAnimation { name = "Second", takeName = take.takeName, firstFrame = (take.firstFrame + take.lastFrame) / 2, lastFrame = take.lastFrame } };
                importer.SaveAndReimport();
                var window = ScriptableObject.CreateInstance<AnimationSegmentEditorWindow>(); temporary.Add(window);
                window.LoadSource(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var options = (AnimationClip[])typeof(AnimationSegmentEditorWindow).GetField("sourceClips", flags).GetValue(window);
                Check(options.Length == 2, "FBX slot exposes both imported takes");
                window.LoadClip(options[1]);
                Check(((AnimationEditSession)typeof(AnimationSegmentEditorWindow).GetField("session", flags).GetValue(window)).source == options[1], "Can select second FBX clip");
                Check(((AnimationSegmentPreview)typeof(AnimationSegmentEditorWindow).GetField("preview", flags).GetValue(window)).HasTarget, "Embedded clip automatically locates FBX model");
                var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => !c.name.Contains("__preview"));
                var edit = NewSession(clip); temporary.Add(edit); edit.Split(edit.segments[0], edit.Snap(clip.length / 2));
                var paths = AnimationSegmentExporter.Export(edit, Root + "/Output", true, true);
                Check(paths.Count == 3, "FBX segments exported");
            });
            Case("Editor window creation, shortcuts and isolated preview sampling", () => {
                var window = ScriptableObject.CreateInstance<AnimationSegmentEditorWindow>();
                temporary.Add(window); window.LoadClip(source);
                var target = new GameObject("PreviewTarget"); temporary.Add(target);
                target.transform.position = new Vector3(99, 0, 0);
                var preview = new AnimationSegmentPreview();
                try
                {
                    preview.SetTarget(target); preview.Sample(source, 1);
                    Check(preview.HasTarget, "Preview clone created"); Near(target.transform.position.x, 99, "Scene target is unchanged");
                }
                finally { preview.Dispose(); }
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    window.position = new Rect(50, 50, 1280, 850); window.Show(); window.Focus();
                    var field = typeof(AnimationSegmentEditorWindow).GetField("tool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    foreach (var entry in new[] { new KeyValuePair<KeyCode, AnimationSegmentEditorWindow.EditTool>(KeyCode.C, AnimationSegmentEditorWindow.EditTool.Cut),
                        new KeyValuePair<KeyCode, AnimationSegmentEditorWindow.EditTool>(KeyCode.W, AnimationSegmentEditorWindow.EditTool.Move),
                        new KeyValuePair<KeyCode, AnimationSegmentEditorWindow.EditTool>(KeyCode.S, AnimationSegmentEditorWindow.EditTool.Scale) })
                    {
                        window.SendEvent(new Event { type = EventType.KeyDown, keyCode = entry.Key });
                        Check((AnimationSegmentEditorWindow.EditTool)field.GetValue(window) == entry.Value, "Tool shortcut " + entry.Key);
                    }
                    window.Close();
                }
            });
            report.Add("PASS: " + report.Count + " cases, " + checks + " assertions; Unity " + Application.unityVersion);
            Directory.CreateDirectory("TestReports"); File.WriteAllLines("TestReports/segment-editor.txt", report);
            Debug.Log("SEGMENT ALL TESTS PASSED " + checks);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory("TestReports"); File.WriteAllText("TestReports/segment-editor.txt", string.Join("\n", report) + "\nFAIL " + ex);
            Debug.LogException(ex); throw;
        }
        finally
        {
            Selection.objects = selection; Undo.ClearAll();
            foreach (var item in temporary) if (item != null && !EditorUtility.IsPersistent(item)) Object.DestroyImmediate(item);
            if (created) AssetDatabase.DeleteAsset(Root);
            AssetDatabase.SaveAssets();
        }
    }
}
