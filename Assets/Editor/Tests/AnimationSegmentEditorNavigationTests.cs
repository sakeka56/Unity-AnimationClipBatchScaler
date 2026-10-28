using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs inside the current Editor. Never exits Unity, clears Undo, or reuses a user's editor window.
public static class AnimationSegmentEditorNavigationTests
{
    private const string Root = "Assets/__AnimationSegmentNavigationTests";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static AnimationSegmentEditorWindow window;
    private static AnimationClip clip;
    private static GameObject cube, prefab;
    private static Object[] selection;
    private static EditorWindow previousFocus;
    private static string preferenceKey, previousDefault;
    private static bool hadDefault;
    private static int previousInstance, ticks, checks;
    private static bool created, running;
    public static bool IsRunning { get { return running; } }
    private static float wheelStartY;
    private static string failure;
    private static string Report { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../TestReports/segment-navigation.txt")); } }
    private static T Field<T>(string name) { return (T)typeof(AnimationSegmentEditorWindow).GetField(name, Private).GetValue(window); }
    private static void Set(string name, object value) { typeof(AnimationSegmentEditorWindow).GetField(name, Private).SetValue(window, value); }
    private static void Call(string name, params object[] args) { typeof(AnimationSegmentEditorWindow).GetMethod(name, Private).Invoke(window, args); }
    private static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    private static void Near(float a, float b, string label) { Check(Mathf.Abs(a - b) < .02f, label + ": " + a + " != " + b); }

    [MenuItem("Tools/Animation Segment Tests/Navigation, folders and default preview")]
    public static void Run()
    {
        if (running || window != null || Directory.Exists(Root)) throw new Exception("Navigation tests are running or their folder already exists.");
        running = true;
        EditorApplication.update -= Update; Application.logMessageReceived -= Log;
        selection = Selection.objects; previousFocus = EditorWindow.focusedWindow;
        preferenceKey = "AnimationSegmentEditor.DefaultPreview." + Application.dataPath;
        hadDefault = EditorPrefs.HasKey(preferenceKey); previousDefault = EditorPrefs.GetString(preferenceKey, "");
        previousInstance = SessionState.GetInt(preferenceKey, 0);
        checks = ticks = 0; created = false; failure = null;
        try
        {
            EditorPrefs.DeleteKey(preferenceKey); SessionState.SetInt(preferenceKey, 0); Selection.objects = new Object[0];
            AssetDatabase.CreateFolder("Assets", "__AnimationSegmentNavigationTests"); created = true;
            clip = new AnimationClip { name = "Navigation", frameRate = 30 };
            for (int i = 0; i < 80; i++)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Track" + i, typeof(Transform), "m_LocalPosition.x"),
                    AnimationCurve.Linear(0, 0, 8, 1));
            AssetDatabase.CreateAsset(clip, Root + "/Navigation.anim");
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab = PrefabUtility.SaveAsPrefabAsset(cube, Root + "/Preview.prefab");
            Object.DestroyImmediate(cube); cube = null;
            window = ScriptableObject.CreateInstance<AnimationSegmentEditorWindow>();
            window.titleContent = new GUIContent("Navigation regression");
            window.position = new Rect(60, 60, 1440, 900); window.LoadClip(clip);
            Call("SetPreviewManually", prefab);
            Check(EditorPrefs.GetString(preferenceKey, "").Length > 0, "Manual preview for standalone clip is persisted");
            window.Show(); window.Close(); window = ScriptableObject.CreateInstance<AnimationSegmentEditorWindow>();
            // Clear the session-only lookup to prove the persistent object identity is sufficient.
            SessionState.SetInt(preferenceKey, 0);
            window.Show(); window.Close(); window = ScriptableObject.CreateInstance<AnimationSegmentEditorWindow>();
            window.titleContent = new GUIContent("Navigation regression");
            window.position = new Rect(60, 60, 1440, 900); window.LoadClip(clip);
            Check(Field<GameObject>("previewTarget") == prefab, "Default model restored after window close/reopen");
            cube = Object.Instantiate(prefab);
            var replacement = PrefabUtility.SaveAsPrefabAsset(cube, Root + "/Replacement.prefab");
            Object.DestroyImmediate(cube); cube = null;
            Call("SetPreviewManually", replacement);
            string manualDefault = EditorPrefs.GetString(preferenceKey);
            Check(manualDefault.Length > 0 && manualDefault != previousDefault, "Manual replacement records default");
            Call("SetPreviewManually", (object)null);
            Check(EditorPrefs.GetString(preferenceKey) == manualDefault, "Clearing preview does not erase default");
            Call("SetPreview", prefab);
            Check(EditorPrefs.GetString(preferenceKey) == manualDefault, "Automatic preview does not overwrite default");
            Call("AutoLocatePreview");
            Check(Field<GameObject>("previewTarget") == replacement, "Standalone clip prefers saved default");
            TestEmbeddedModelGuard(manualDefault, replacement);
            window.LoadClip(clip);
            Check(Field<GameObject>("previewTarget") == replacement, "Default survives switching away from embedded model");

            string nested = Root + "/New Folder/子目录";
            Directory.CreateDirectory(nested);
            Check(!File.Exists(nested + ".meta"), "New native-dialog folder initially has no meta");
            Call("SelectOutputFolder", Path.GetFullPath(nested));
            Check(AssetDatabase.GetAssetPath(Field<DefaultAsset>("outputFolder")) == nested, "New folder assigned to output slot immediately");
            Check(File.Exists(nested + ".meta") && AssetDatabase.IsValidFolder(nested), "New nested folder is imported synchronously");
            var folder = Field<DefaultAsset>("outputFolder");
            try { Call("SelectOutputFolder", Path.GetFullPath("Packages")); throw new Exception("Outside Assets accepted"); }
            catch (TargetInvocationException ex) { Check(ex.InnerException is ArgumentException, "Outside Assets rejected"); }
            Check(Field<DefaultAsset>("outputFolder") == folder, "Invalid folder keeps previous selection");
            window.Show(); window.Focus();
            Application.logMessageReceived += Log;
            EditorApplication.update += Update;
        }
        catch (Exception ex) { Finish(ex); }
    }
    private static void TestEmbeddedModelGuard(string manualDefault, GameObject replacement)
    {
        string data = File.ReadAllText("Assets/Editor/Tests/Fixtures/Animated.fbx");
        data = data.Replace("Model::Animated\", \"Null\"", "Model::Animated\", \"Mesh\"");
        data = data.Replace("    Count: 6", "    Count: 7").Replace("    ObjectType: \"Model\"", "    ObjectType: \"Geometry\" {\n        Count: 1\n    }\n    ObjectType: \"Model\"");
        data = data.Replace("    AnimationStack: 10,", "    Geometry: 3, \"Geometry::Triangle\", \"Mesh\" {\n        Vertices: *9 {\n            a: 0,0,0,1,0,0,0,1,0\n        }\n        PolygonVertexIndex: *3 {\n            a: 0,1,-3\n        }\n        GeometryVersion: 124\n    }\n    AnimationStack: 10,");
        data = data.Replace("Connections:  {", "Connections:  {\n    C: \"OO\",3,2");
        string fbx = Root + "/Rendered.fbx"; File.WriteAllText(fbx, data);
        AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        importer.importAnimation = true; importer.animationType = ModelImporterAnimationType.Generic; importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        Check(model.GetComponentsInChildren<Renderer>(true).Length > 0, "Synthetic FBX contains visible mesh");
        var embedded = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => !c.name.Contains("__preview"));
        window.LoadClip(embedded);
        Check(Field<GameObject>("previewTarget") == model, "Embedded FBX model takes precedence over default");
        Call("SetPreviewManually", replacement);
        Check(EditorPrefs.GetString(preferenceKey) == manualDefault, "Manual preview on model-bearing clip does not replace default");
    }
    private static Vector2 Point(float x = 380, float y = 130)
    {
        return Field<Rect>("timelineBounds").position + new Vector2(x, y)
            + Field<Vector2>("guiScreenOrigin") - window.position.position;
    }
    private static void Send(EventType type, Vector2 point, Vector2 delta, int button = 0, bool alt = false, bool shift = false)
    {
        window.SendEvent(new Event { type = type, mousePosition = point, delta = delta, button = button, alt = alt, shift = shift });
    }
    private static void Log(string message, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && stack.Contains("AnimationSegment")) failure = message + "\n" + stack;
    }
    private static void Update()
    {
        if (window == null) { EditorApplication.update -= Update; return; }
        try
        {
            ticks++; window.Repaint();
            if (ticks == 20)
            {
                Set("pixelsPerSecond", 200f); Set("trackScroll", new Vector2(200, 100));
                float head = Field<float>("playhead");
                var point = Point(); float beforeTime = (200 + 180) / 200f;
                Send(EventType.ScrollWheel, point, new Vector2(0, -3), alt: true);
                Check(Field<float>("pixelsPerSecond") > 200, "Alt+wheel zooms in");
                Near((Field<Vector2>("trackScroll").x + 180) / Field<float>("pixelsPerSecond"), beforeTime, "Zoom is anchored at mouse time");
                Near(Field<Vector2>("trackScroll").y, 100, "Zoom does not scroll vertically");
                Near(Field<float>("playhead"), head, "Zoom does not scrub");
            }
            if (ticks == 25)
            {
                var point = Point();
                Send(EventType.ScrollWheel, point, new Vector2(0, 3), alt: true);
                Near(Field<float>("pixelsPerSecond"), 200, "Alt+wheel zooms out");
                Near(Field<Vector2>("trackScroll").x, 200, "Inverse zoom restores scroll; bounds=" + Field<Rect>("timelineBounds") + "; duration=" + Field<AnimationEditSession>("session").Duration);
            }
            if (ticks == 30)
            {
                var point = Point(); float head = Field<float>("playhead");
                var edit = Field<AnimationEditSession>("session"); float duration = edit.Duration;
                Send(EventType.MouseDown, point, Vector2.zero, 2);
                var end = point - new Vector2(60, 80);
                Send(EventType.MouseDrag, end, end - point, 2);
                Check(Field<Vector2>("trackScroll").x > 250 && Field<Vector2>("trackScroll").y > 170, "Middle drag pans both axes");
                // Release outside the timeline to verify capture is released too.
                Send(EventType.MouseUp, Point(20, -15), Vector2.zero, 2);
                Check(!Field<bool>("panning") && GUIUtility.hotControl == 0, "Middle drag releases outside timeline");
                Near(Field<float>("playhead"), head, "Pan does not scrub"); Near(edit.Duration, duration, "Pan does not edit segments");
            }
            if (ticks == 40)
            {
                var point = Point(); wheelStartY = Field<Vector2>("trackScroll").y;
                Send(EventType.ScrollWheel, point, new Vector2(0, 3));
            }
            if (ticks == 43)
            {
                var point = Point();
                Check(Field<Vector2>("trackScroll").y > wheelStartY, "Plain wheel still scrolls vertically");
                var before = Field<Vector2>("trackScroll"); Send(EventType.ScrollWheel, point, new Vector2(0, 2), shift: true);
                Check(Field<Vector2>("trackScroll").x > before.x, "Shift+wheel still scrolls horizontally");
                Set("pixelsPerSecond", 499f);
                Send(EventType.ScrollWheel, point, new Vector2(0, -100), alt: true);
                Near(Field<float>("pixelsPerSecond"), 500, "Zoom upper limit");
                Send(EventType.ScrollWheel, point, new Vector2(0, 100), alt: true);
                Near(Field<float>("pixelsPerSecond"), 20, "Zoom lower limit");
            }
            if (ticks == 50) { Check(failure == null, failure); Finish(null); }
        }
        catch (Exception ex) { Finish(ex); }
    }
    private static void Finish(Exception error)
    {
        running = false;
        EditorApplication.update -= Update; Application.logMessageReceived -= Log;
        if (window != null) Object.DestroyImmediate(window); window = null;
        if (cube != null) Object.DestroyImmediate(cube);
        if (hadDefault) EditorPrefs.SetString(preferenceKey, previousDefault); else EditorPrefs.DeleteKey(preferenceKey);
        SessionState.SetInt(preferenceKey, previousInstance); Selection.objects = selection;
        if (created) AssetDatabase.DeleteAsset(Root); created = false;
        if (previousFocus != null) previousFocus.Focus();
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, error == null ? "PASS: " + checks + " assertions; Alt+wheel anchored zoom, middle-button two-axis pan, existing wheel navigation, native new-folder import, conditional default model persistence, model-bearing FBX precedence; Unity " + Application.unityVersion + "; in existing Editor (no restart/exit)." : "FAIL after " + checks + " assertions: " + error);
        if (error != null) Debug.LogException(error); else Debug.Log("NAVIGATION TESTS PASSED " + checks);
    }
}
