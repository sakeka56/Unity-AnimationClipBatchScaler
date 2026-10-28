using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in a dedicated normal Editor process, not with -batchmode/-quit.
public static class AnimationSegmentEditorVisualTests
{
    private const string Root = "Assets/__AnimationSegmentVisual";
    private static AnimationSegmentEditorWindow window;
    private static int ticks;
    private static bool created;
    private static string failure;
    private static string Reports { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../TestReports")); } }
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(string name) { return (T)typeof(AnimationSegmentEditorWindow).GetField(name, Private).GetValue(window); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Run()
    {
        if (Application.isBatchMode) throw new Exception("Visual tests require a dedicated normal Editor.");
        if (AssetDatabase.IsValidFolder(Root)) throw new Exception("Refusing to replace existing visual test assets.");
        AssetDatabase.CreateFolder("Assets", "__AnimationSegmentVisual"); created = true;
        try
        {
            var clip = new AnimationClip { name = "Robot_Walk", frameRate = 30 };
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
                new AnimationCurve(new Keyframe(0, -.25f), new Keyframe(1, .25f), new Keyframe(2, -.25f), new Keyframe(3, .25f), new Keyframe(4, -.25f)));
            foreach (string part in new[] { "LeftArm", "RightArm", "LeftLeg", "RightLeg" })
            {
                float sign = part.StartsWith("Left") ? 1 : -1;
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(part, typeof(Transform), "localEulerAnglesRaw.z"),
                    new AnimationCurve(new Keyframe(0, 0), new Keyframe(.5f, 25 * sign), new Keyframe(1, -25 * sign),
                        new Keyframe(1.5f, 25 * sign), new Keyframe(2, -25 * sign), new Keyframe(2.5f, 25 * sign),
                        new Keyframe(3, -25 * sign), new Keyframe(3.5f, 25 * sign), new Keyframe(4, 0)));
            }
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = .5f, functionName = "Footstep" },
                new AnimationEvent { time = 1.5f, functionName = "Footstep" }, new AnimationEvent { time = 2.5f, functionName = "Footstep" } });
            AssetDatabase.CreateAsset(clip, Root + "/Walk.anim");
            var robot = new GameObject("Robot");
            var material = new Material(Shader.Find("Standard")) { color = new Color(.20f, .65f, .82f) };
            AssetDatabase.CreateAsset(material, Root + "/Robot.mat");
            Part(robot, "Body", PrimitiveType.Cube, new Vector3(0, 1.4f, 0), new Vector3(.8f, 1.1f, .4f), material);
            Part(robot, "Head", PrimitiveType.Sphere, new Vector3(0, 2.2f, 0), Vector3.one * .65f, material);
            Part(robot, "LeftArm", PrimitiveType.Cube, new Vector3(-.62f, 1.35f, 0), new Vector3(.25f, 1.05f, .3f), material);
            Part(robot, "RightArm", PrimitiveType.Cube, new Vector3(.62f, 1.35f, 0), new Vector3(.25f, 1.05f, .3f), material);
            Part(robot, "LeftLeg", PrimitiveType.Cube, new Vector3(-.23f, .45f, 0), new Vector3(.3f, .85f, .3f), material);
            Part(robot, "RightLeg", PrimitiveType.Cube, new Vector3(.23f, .45f, 0), new Vector3(.3f, .85f, .3f), material);
            var prefab = PrefabUtility.SaveAsPrefabAsset(robot, Root + "/Robot.prefab"); Object.DestroyImmediate(robot);
            AnimationSegmentEditorWindow.Open();
            window = EditorWindow.GetWindow<AnimationSegmentEditorWindow>(); window.position = new Rect(40, 70, 1440, 880);
            window.LoadClip(clip);
            typeof(AnimationSegmentEditorWindow).GetMethod("SetPreview", Private).Invoke(window, new object[] { prefab });
            window.Show(); window.Focus(); ticks = 0; failure = null;
            Application.logMessageReceived += Log;
            EditorApplication.update += Update;
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Reports); File.WriteAllText(Path.Combine(Reports, "segment-editor-ui.txt"), "FAIL " + ex);
            Debug.LogException(ex); Finish(1);
        }
    }
    private static void Part(GameObject parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(parent.transform);
        part.transform.localPosition = position; part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }
    private static void Key(KeyCode key)
    {
        EditorGUIUtility.editingTextField = false;
        window.SendEvent(new Event { type = EventType.KeyDown, keyCode = key });
    }
    private static Vector2 Point(float time)
    {
        Rect rect = Field<Rect>("timelineBounds");
        return new Vector2(rect.x + 200 + time * Field<float>("pixelsPerSecond") - Field<Vector2>("trackScroll").x, rect.y + 28 + 20)
            + Field<Vector2>("guiScreenOrigin") - window.position.position;
    }
    private static void Mouse(EventType type, Vector2 point, Vector2 delta = default(Vector2))
    {
        window.SendEvent(new Event { type = type, mousePosition = point, button = 0, delta = delta });
    }
    private static void Click(float time) { var p = Point(time); Mouse(EventType.MouseDown, p); Mouse(EventType.MouseUp, p); }
    private static void Drag(float from, float to)
    {
        var start = Point(from); var end = Point(to);
        Mouse(EventType.MouseDown, start); Mouse(EventType.MouseDrag, end, end - start); Mouse(EventType.MouseUp, end);
    }
    private static void Log(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) &&
            (message.Contains("GUI") || stack.Contains("AnimationSegment"))) failure = message + "\n" + stack;
    }
    private static void Update()
    {
        try
        {
            ticks++; window.Repaint();
            if (ticks == 20)
            {
                Check(Field<Rect>("timelineBounds").width > 500, "Timeline has layout");
                Key(KeyCode.C); Click(1);
                var edit = Field<AnimationEditSession>("session");
                Check(edit.segments.Count == 2, "First C cut; count=" + edit.segments.Count + ", tool=" + Field<AnimationSegmentEditorWindow.EditTool>("tool") + ", bounds=" + Field<Rect>("timelineBounds"));
            }
            if (ticks == 30)
            {
                Click(2);
                var edit = Field<AnimationEditSession>("session");
                Check(edit.segments.Count == 3, "Second C cut creates three segments; count=" + edit.segments.Count);
                edit.segments[0].name = "起步 / Intro"; edit.segments[1].name = "循环 / Loop"; edit.segments[2].name = "收尾 / Outro";
            }
            if (ticks == 40)
            {
                var edit = Field<AnimationEditSession>("session");
                Key(KeyCode.W); Drag(.5f, 4.5f);
                var moved = edit.segments.Find(s => s.name.StartsWith("起步"));
                Check(Mathf.Abs(moved.start - 4) < .001f, "Mouse W moves segment to tail");
            }
            if (ticks == 50)
            {
                var edit = Field<AnimationEditSession>("session");
                var moved = edit.segments.Find(s => s.name.StartsWith("起步"));
                Key(KeyCode.S); Drag(4.98f, 6.48f);
                Check(Mathf.Abs(moved.duration - 2.5f) < .04f, "Mouse S retimes right edge");
                typeof(AnimationSegmentEditorWindow).GetField("playhead", Private).SetValue(window, 2.5f);
                typeof(AnimationSegmentEditorWindow).GetMethod("Sample", Private).Invoke(window, null);
                edit.Validate();
            }
            if (ticks == 60)
            {
                var edit = Field<AnimationEditSession>("session");
                var moved = edit.segments.Find(s => s.name.StartsWith("起步"));
                Drag(5, 5.5f);
                Check(Mathf.Abs(moved.duration - 3) < .04f, "Second S drag scales from segment body");
                Check(GUIUtility.hotControl == 0, "Scale releases hot control");
                typeof(AnimationSegmentEditorWindow).GetField("pixelsPerSecond", Private).SetValue(window, 300f);
            }
            if (ticks == 70)
            {
                var rect = Field<Rect>("timelineBounds");
                var offset = Field<Vector2>("guiScreenOrigin") - window.position.position;
                var start = new Vector2(rect.x + 230, rect.yMax - 8) + offset;
                var end = start + new Vector2(110, 0);
                float before = Field<float>("playhead");
                Mouse(EventType.MouseDown, start); Mouse(EventType.MouseDrag, end, end - start); Mouse(EventType.MouseUp, end);
                Check(Field<Vector2>("trackScroll").x > 0, "Horizontal scrollbar thumb drags");
                Check(Mathf.Abs(Field<float>("playhead") - before) < .001f, "Scrollbar does not scrub playhead");
                var preview = Field<AnimationSegmentPreview>("preview");
                typeof(AnimationSegmentPreview).GetField("zoom", Private).SetValue(preview, 8f);
                Key(KeyCode.F);
                Check(Mathf.Abs((float)typeof(AnimationSegmentPreview).GetField("zoom", Private).GetValue(preview) - 1) < .001f, "F resets zoom to fit model");
            }
            if (ticks == 85)
            {
                Check(failure == null, failure);
                Capture();
                File.WriteAllText(Path.Combine(Reports, "segment-editor-ui.txt"), "PASS graphical editor layout, C mouse cuts, W mouse reorder, repeated S body resize, horizontal scrollbar drag without scrubbing, F framing, preview sampling; Unity " + Application.unityVersion);
                Finish(0);
            }
        }
        catch (Exception ex)
        {
            Capture();
            Directory.CreateDirectory(Reports); File.WriteAllText(Path.Combine(Reports, "segment-editor-ui.txt"), "FAIL " + ex);
            Debug.LogException(ex); Finish(1);
        }
    }
    private static void Capture()
    {
        if (window == null) return;
        var preview = Field<AnimationSegmentPreview>("preview");
        var frame = (Texture)typeof(AnimationSegmentPreview).GetField("lastFrame", Private).GetValue(preview);
        Check(frame != null, "Preview produced a rendered texture");
        var render = RenderTexture.GetTemporary(frame.width, frame.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var image = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
        try
        {
            Graphics.Blit(frame, render); RenderTexture.active = render;
            image.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); image.Apply();
            var colors = image.GetPixels32(); int distinct = 0;
            for (int i = 0; i < colors.Length; i++) if (!colors[i].Equals(colors[0])) distinct++;
            Check(distinct > colors.Length / 50, "Preview contains model pixels, not only background");
            Directory.CreateDirectory(Reports); File.WriteAllBytes(Path.Combine(Reports, "segment-preview.png"), image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render); Object.DestroyImmediate(image); }
    }
    private static void Finish(int exit)
    {
        EditorApplication.update -= Update; Application.logMessageReceived -= Log;
        if (window != null) window.Close();
        if (created) AssetDatabase.DeleteAsset(Root);
        AssetDatabase.SaveAssets(); EditorApplication.Exit(exit);
    }
}
