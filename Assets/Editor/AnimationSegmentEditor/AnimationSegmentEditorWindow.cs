using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class AnimationSegmentEditorWindow : EditorWindow
{
    public enum EditTool { Cut, Move, Scale }
    [SerializeField] private AnimationEditSession session;
    [SerializeField] private GameObject previewTarget;
    [SerializeField] private DefaultAsset outputFolder;
    [SerializeField] private EditTool tool = EditTool.Move;
    [SerializeField] private float pixelsPerSecond = 100;
    [SerializeField] private int selected;
    [SerializeField] private float playhead;
    [SerializeField] private bool exportSegments = true, exportCombined = true;
    private Vector2 trackScroll, cardsScroll;
    private string search = "", status;
    private bool playing, scrubbing;
    private double lastUpdate;
    private AnimationSegmentPreview preview;
    private readonly List<Track> tracks = new List<Track>();
    private AnimationEditSegment dragging;
    private float dragMouseTime, dragStart, dragDuration;
    private int dragControl;
    private Rect timelineBounds;
    private Vector2 guiScreenOrigin;
    private GUIStyle clipStyle, cardStyle;
    private const float LabelWidth = 200, RulerHeight = 28, BandHeight = 44, RowHeight = 25;
    private static readonly Color[] Palette = {
        new Color(.20f,.65f,.78f), new Color(.74f,.48f,.87f), new Color(.89f,.63f,.30f),
        new Color(.35f,.72f,.49f), new Color(.87f,.43f,.53f), new Color(.37f,.51f,.91f) };
    private class Track { public string label; public float[] times; public bool objectCurve; }

    [MenuItem("Tools/动画分段编辑器")]
    [MenuItem("Window/Animation/Animation Segment Editor")]
    public static void Open() { GetWindow<AnimationSegmentEditorWindow>("动画分段编辑器"); }

    private AnimationEditSegment Active { get { return session != null && selected >= 0 && selected < session.segments.Count ? session.segments[selected] : null; } }
    private void OnEnable()
    {
        minSize = new Vector2(1000, 650);
        if (session == null) session = CreateInstance<AnimationEditSession>();
        preview = new AnimationSegmentPreview();
        if (previewTarget != null) preview.SetTarget(previewTarget);
        EditorApplication.update += Tick;
        Undo.undoRedoPerformed += UndoChanged;
        RebuildTracks();
    }
    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        Undo.undoRedoPerformed -= UndoChanged;
        if (preview != null) preview.Dispose();
        preview = null;
    }
    private void OnDestroy() { if (session != null && !EditorUtility.IsPersistent(session)) Object.DestroyImmediate(session); }
    private void OnFocus() { RebuildTracks(); }
    private void UndoChanged()
    {
        selected = Mathf.Clamp(selected, 0, session.segments.Count - 1);
        playhead = Mathf.Clamp(playhead, 0, session.Duration);
        RebuildTracks(); Sample(); Repaint();
    }
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (playing && session != null && session.source != null && session.Duration > 0)
        {
            playhead = Mathf.Repeat(playhead + (float)Math.Min(.1, now - lastUpdate), session.Duration);
            Sample(); Repaint();
        }
        lastUpdate = now;
    }
    private void Sample()
    {
        if (preview == null || session == null || session.source == null) return;
        var segment = session.At(playhead);
        if (segment != null) preview.Sample(session.source, segment.SourceTime(playhead));
    }
    public void LoadClip(AnimationClip clip)
    {
        if (clip == null) return;
        var next = CreateInstance<AnimationEditSession>();
        try { next.Initialize(clip); }
        catch { Object.DestroyImmediate(next); throw; }
        if (session != null && !EditorUtility.IsPersistent(session)) Object.DestroyImmediate(session);
        session = next;
        selected = 0; playhead = 0; playing = false; trackScroll = Vector2.zero;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(clip));
        if (model != null) SetPreview(model);
        RebuildTracks(); Sample(); Repaint();
    }
    private void SetPreview(GameObject target)
    {
        previewTarget = target;
        if (preview == null) preview = new AnimationSegmentPreview();
        preview.SetTarget(target); Sample();
    }
    private void RebuildTracks()
    {
        tracks.Clear();
        if (session == null || session.source == null) return;
        foreach (var binding in AnimationUtility.GetCurveBindings(session.source))
            tracks.Add(new Track { label = (string.IsNullOrEmpty(binding.path) ? "(Root)" : binding.path) + " / " + binding.propertyName,
                times = AnimationUtility.GetEditorCurve(session.source, binding).keys.Select(k => k.time).ToArray() });
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(session.source))
            tracks.Add(new Track { label = (string.IsNullOrEmpty(binding.path) ? "(Root)" : binding.path) + " / " + binding.propertyName,
                times = AnimationUtility.GetObjectReferenceCurve(session.source, binding).Select(k => k.time).ToArray(), objectCurve = true });
        tracks.Sort((a, b) => string.CompareOrdinal(a.label, b.label));
    }
    private void Edit(string name, Action change)
    {
        Undo.RegisterCompleteObjectUndo(session, name);
        change(); EditorUtility.SetDirty(session); Sample(); Repaint();
    }

    private void OnGUI()
    {
        if (session == null) return;
        if (Event.current.type == EventType.Repaint) guiScreenOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
        if (clipStyle == null)
        {
            clipStyle = new GUIStyle(EditorStyles.whiteMiniLabel) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(9, 5, 0, 0), clipping = TextClipping.Clip };
            cardStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 8, 7, 7) };
        }
        Shortcuts();
        DrawHeader();
        DrawCards();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(540, position.width * .65f))))
            {
                DrawToolbar();
                var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                Timeline(rect);
            }
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("动画预览", EditorStyles.boldLabel);
                var target = (GameObject)EditorGUILayout.ObjectField(previewTarget, typeof(GameObject), true);
                if (target != previewTarget) SetPreview(target);
                var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                if (preview != null) preview.Draw(rect);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(playing ? "暂停" : "播放", GUILayout.Width(60))) playing = !playing;
                    float time = EditorGUILayout.Slider(playhead, 0, Mathf.Max(.01f, session.Duration));
                    if (time != playhead) { playhead = time; Sample(); }
                }
                DrawInspector();
            }
        }
        DrawExport();
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
    }
    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("ANIMATION CUT", EditorStyles.boldLabel, GUILayout.Width(125));
            var source = (AnimationClip)EditorGUILayout.ObjectField(session.source, typeof(AnimationClip), false, GUILayout.MinWidth(150));
            if (source != null && source != session.source) { try { LoadClip(source); } catch (Exception ex) { status = ex.Message; } }
            GUILayout.Label(session.segments.Count + " 段   |   " + session.Duration.ToString("0.###") + " 秒", GUILayout.Width(170));
            if (GUILayout.Button("载入所选动画", EditorStyles.toolbarButton, GUILayout.Width(105)))
            {
                var clips = AnimationBatchScalerUtility.CollectClips(Selection.objects);
                if (clips.Count > 0) LoadClip(clips[0]); else status = "请先在 Project 中选择 AnimationClip 或 FBX。";
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            var saved = (AnimationEditSession)EditorGUILayout.ObjectField(EditorUtility.IsPersistent(session) ? "编辑工程" : "编辑工程（未保存）", EditorUtility.IsPersistent(session) ? session : null, typeof(AnimationEditSession), false);
            if (saved != null && saved != session)
            {
                if (!EditorUtility.IsPersistent(session)) Object.DestroyImmediate(session);
                session = saved; selected = 0; playhead = 0; playing = false; RebuildTracks(); Sample();
            }
            if (GUILayout.Button("保存编辑工程…", GUILayout.Width(120))) SaveSession();
            GUILayout.Label("源动画保持不变 · 切换源动画会新建编辑工程", EditorStyles.miniLabel);
        }
    }
    private void SaveSession()
    {
        if (session.source == null) { status = "请先载入动画。"; return; }
        if (EditorUtility.IsPersistent(session)) { EditorUtility.SetDirty(session); AssetDatabase.SaveAssetIfDirty(session); status = "编辑工程已保存。"; return; }
        string path = EditorUtility.SaveFilePanelInProject("保存动画编辑工程", session.source.name + "_Edit", "asset", "保存片段名称、位置和时长。");
        if (string.IsNullOrEmpty(path)) return;
        var saved = Instantiate(session); saved.hideFlags = HideFlags.None;
        AssetDatabase.CreateAsset(saved, path); AssetDatabase.SaveAssets();
        Object.DestroyImmediate(session); session = saved; status = "编辑工程已保存：" + path;
    }
    private void DrawCards()
    {
        cardsScroll = EditorGUILayout.BeginScrollView(cardsScroll, GUILayout.Height(87));
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < session.segments.Count; i++)
            {
                int index = i; var segment = session.segments[i];
                var background = GUI.backgroundColor; GUI.backgroundColor = ColorFor(segment);
                using (new EditorGUILayout.VerticalScope(cardStyle, GUILayout.Width(205)))
                {
                    if (GUILayout.Button((selected == i ? "● " : "") + "片段 " + (i + 1) + "  ·  " + segment.duration.ToString("0.###") + "s", EditorStyles.miniButton))
                    { selected = index; playhead = segment.start; Sample(); }
                    string name = EditorGUILayout.TextField(segment.name ?? "");
                    if (name != segment.name) Edit("Rename animation segment", () => segment.name = name);
                    GUILayout.Label("源 " + segment.sourceStart.ToString("0.###") + "—" + segment.sourceEnd.ToString("0.###") + "s", EditorStyles.miniLabel);
                }
                GUI.backgroundColor = background;
            }
        }
        EditorGUILayout.EndScrollView();
    }
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            tool = (EditTool)GUILayout.Toolbar((int)tool, new[] { "切割 C", "移动 W", "缩放 S" }, EditorStyles.toolbarButton, GUILayout.Width(210));
            using (new EditorGUI.DisabledScope(session.source == null))
                if (GUILayout.Button("切播放头", EditorStyles.toolbarButton, GUILayout.Width(80))) Cut(playhead);
            GUILayout.FlexibleSpace();
            GUILayout.Label("缩放", GUILayout.Width(28));
            pixelsPerSecond = GUILayout.HorizontalSlider(pixelsPerSecond, 20, 500, GUILayout.Width(90));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            GUILayout.Label("关键帧轨道 " + tracks.Count + "  ·  " + (session.source == null ? "—" : session.source.frameRate.ToString("0.##") + " fps"), EditorStyles.miniLabel);
        }
    }
    private void DrawInspector()
    {
        var segment = Active;
        EditorGUILayout.LabelField("当前片段", EditorStyles.boldLabel);
        if (segment == null) { GUILayout.Label("载入动画后开始切割。", EditorStyles.wordWrappedLabel); return; }
        string name = EditorGUILayout.DelayedTextField("Name", segment.name);
        if (name != segment.name) Edit("Rename segment", () => segment.name = name);
        float start = EditorGUILayout.DelayedFloatField("时间线位置（秒）", segment.start);
        if (start != segment.start) Edit("Move segment", () => { session.Move(segment, start); selected = session.segments.IndexOf(segment); });
        float duration = EditorGUILayout.DelayedFloatField("片段时长（秒）", segment.duration);
        if (duration != segment.duration) Edit("Retime segment", () => session.Resize(segment, duration));
        GUILayout.Label("源区间 " + segment.sourceStart.ToString("0.###") + " — " + segment.sourceEnd.ToString("0.###") + "s   |   时长倍率 " +
            (segment.duration / (segment.sourceEnd - segment.sourceStart)).ToString("0.###"), EditorStyles.miniLabel);
        if (GUILayout.Button("恢复本段原始时长")) Edit("Reset segment duration", () => session.Resize(segment, segment.sourceEnd - segment.sourceStart));
        using (new EditorGUI.DisabledScope(session.segments.Count < 2))
            if (GUILayout.Button("移除此片段")) Edit("Remove segment", () => { session.segments.Remove(segment); selected = Mathf.Min(selected, session.segments.Count - 1); });
        EditorGUILayout.HelpBox("C：点击时间线切割\nW：拖动片段，可移到空位或重排\nS：拖动片段右边缘调整时长\n滚轮：垂直滚动；Shift+滚轮：横向滚动\n空格：播放/暂停 · Ctrl/Cmd+Z：撤销\n空隙保持上一片段末帧，片段间直接切换。", MessageType.None);
    }
    private void DrawExport()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("输出目录", outputFolder, typeof(DefaultAsset), false, GUILayout.MinWidth(230));
            if (GUILayout.Button("选择…", GUILayout.Width(65)))
            {
                string path = EditorUtility.OpenFolderPanel("选择 Assets 内的输出文件夹", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    string relative = FileUtil.GetProjectRelativePath(path);
                    try
                    {
                        if (string.IsNullOrEmpty(relative)) throw new ArgumentException("请选择当前工程 Assets 内的文件夹。");
                        AnimationBatchScalerUtility.ValidateFolder(relative);
                        outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
                    }
                    catch (Exception ex) { status = ex.Message; }
                }
            }
            exportSegments = GUILayout.Toggle(exportSegments, "各片段 .anim", GUILayout.Width(110));
            exportCombined = GUILayout.Toggle(exportCombined, "完整时间线（按帧）", GUILayout.Width(160));
            using (new EditorGUI.DisabledScope(session.source == null || outputFolder == null || (!exportSegments && !exportCombined)))
                if (GUILayout.Button("一键导出", GUILayout.Width(110), GUILayout.Height(28)))
                {
                    try
                    {
                        var paths = AnimationSegmentExporter.Export(session, AssetDatabase.GetAssetPath(outputFolder), exportSegments, exportCombined);
                        status = "已导出 " + paths.Count + " 个动画，已有文件自动编号：" + AssetDatabase.GetAssetPath(outputFolder);
                        EditorGUIUtility.PingObject(outputFolder);
                    }
                    catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
                }
        }
    }

    private void Shortcuts()
    {
        var e = Event.current;
        if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField || e.control || e.command || e.alt) return;
        if (e.keyCode == KeyCode.C) tool = EditTool.Cut;
        else if (e.keyCode == KeyCode.W) tool = EditTool.Move;
        else if (e.keyCode == KeyCode.S) tool = EditTool.Scale;
        else if (e.keyCode == KeyCode.Space) playing = !playing;
        else return;
        e.Use(); Repaint();
    }
    private void Cut(float time)
    {
        var segment = session.segments.FirstOrDefault(s => time > s.start && time < s.End);
        if (segment == null) return;
        Edit("Split animation segment", () => {
            var right = session.Split(segment, time);
            if (right != null) selected = session.segments.IndexOf(right);
        });
    }
    private Color ColorFor(AnimationEditSegment segment) { return Palette[Mathf.Abs(segment.colorIndex) % Palette.Length]; }
    private float TimeAt(Rect rect, float mouseX) { return Mathf.Max(0, (mouseX - rect.x - LabelWidth + trackScroll.x) / pixelsPerSecond); }

    private void Timeline(Rect rect)
    {
        if (Event.current.type == EventType.Repaint) timelineBounds = rect;
        GUI.Box(rect, GUIContent.none);
        var rows = tracks.Where(t => search.Length == 0 || t.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        float contentWidth = Mathf.Max(rect.width - LabelWidth, (session.Duration + 2) * pixelsPerSecond);
        float contentHeight = Mathf.Max(rect.height - RulerHeight - BandHeight - 17, (rows.Count + 1) * RowHeight);
        var band = new Rect(rect.x + LabelWidth, rect.y + RulerHeight, rect.width - LabelWidth, BandHeight);
        var ruler = new Rect(rect.x + LabelWidth, rect.y, rect.width - LabelWidth, RulerHeight);
        var body = new Rect(rect.x + LabelWidth, band.yMax, rect.width - LabelWidth, rect.height - RulerHeight - BandHeight);
        TimelineInput(rect, ruler, band, body);
        GUI.Label(new Rect(rect.x + 8, rect.y + 5, LabelWidth - 10, 22), "属性 / 关键帧", EditorStyles.boldLabel);
        GUI.Label(new Rect(rect.x + 8, band.y + 12, LabelWidth - 10, 22), "片段轨道", EditorStyles.miniLabel);
        GUI.BeginGroup(ruler);
        EditorGUI.DrawRect(new Rect(0, 0, ruler.width, ruler.height), new Color(.16f, .17f, .20f));
        float tick = pixelsPerSecond > 250 ? .1f : pixelsPerSecond > 70 ? .5f : 2;
        float first = Mathf.Floor(trackScroll.x / pixelsPerSecond / tick) * tick;
        for (float time = first; time < (trackScroll.x + ruler.width) / pixelsPerSecond; time += tick)
        {
            float x = time * pixelsPerSecond - trackScroll.x;
            EditorGUI.DrawRect(new Rect(x, RulerHeight - 7, 1, 7), Color.gray);
            GUI.Label(new Rect(x + 3, 1, 75, 18), time.ToString("0.##") + "s", EditorStyles.miniLabel);
        }
        GUI.EndGroup();
        GUI.BeginGroup(band);
        EditorGUI.DrawRect(new Rect(0, 0, band.width, band.height), new Color(.13f, .14f, .17f));
        foreach (var segment in session.segments)
        {
            var box = new Rect(segment.start * pixelsPerSecond - trackScroll.x + 1, 4, Mathf.Max(2, segment.duration * pixelsPerSecond - 2), BandHeight - 8);
            var color = ColorFor(segment);
            EditorGUI.DrawRect(box, color * (Active == segment ? .75f : .55f));
            if (Active == segment)
            {
                EditorGUI.DrawRect(new Rect(box.x, box.y, box.width, 2), Color.white);
                EditorGUI.DrawRect(new Rect(box.x, box.yMax - 2, box.width, 2), Color.white);
            }
            EditorGUI.DrawRect(new Rect(box.xMax - 5, box.y + 5, 2, box.height - 10), new Color(1, 1, 1, .65f));
            GUI.Label(box, new GUIContent(segment.name + "  " + segment.duration.ToString("0.##") + "s", segment.name), clipStyle);
        }
        GUI.EndGroup();
        trackScroll = GUI.BeginScrollView(body, trackScroll, new Rect(0, 0, contentWidth, contentHeight), true, true);
        float visibleStart = trackScroll.y, visibleEnd = trackScroll.y + body.height;
        for (int row = 0; row <= rows.Count; row++)
        {
            float y = row * RowHeight;
            if (y + RowHeight < visibleStart || y > visibleEnd) continue;
            EditorGUI.DrawRect(new Rect(0, y, contentWidth, RowHeight - 1), row % 2 == 0 ? new Color(.18f, .19f, .22f) : new Color(.15f, .16f, .19f));
            foreach (var segment in session.segments)
            {
                Color tint = ColorFor(segment); tint.a = .12f;
                EditorGUI.DrawRect(new Rect(segment.start * pixelsPerSecond, y, segment.duration * pixelsPerSecond, RowHeight - 1), tint);
                var times = row == 0 ? AnimationUtility.GetAnimationEvents(session.source).Select(e => e.time) : rows[row - 1].times;
                foreach (float key in times)
                {
                    if (key < segment.sourceStart || key > segment.sourceEnd) continue;
                    float time = segment.start + (key - segment.sourceStart) / (segment.sourceEnd - segment.sourceStart) * segment.duration;
                    float x = time * pixelsPerSecond;
                    if (x < trackScroll.x - 8 || x > trackScroll.x + body.width + 8) continue;
                    Diamond(new Vector2(x, y + RowHeight * .5f), row == 0 ? new Color(1, .8f, .3f) : ColorFor(segment));
                }
                if (row > 0)
                {
                    // Boundary samples added by slicing are distinct from original keyframe diamonds.
                    foreach (float time in new[] { segment.start, segment.End })
                    {
                        float x = time * pixelsPerSecond;
                        EditorGUI.DrawRect(new Rect(x - 2, y + RowHeight * .5f - 2, 4, 4), new Color(.9f, .9f, .95f, .7f));
                    }
                }
            }
        }
        EditorGUI.DrawRect(new Rect(playhead * pixelsPerSecond, trackScroll.y, 1, body.height), new Color(1, .36f, .30f));
        GUI.EndScrollView();
        var labels = new Rect(rect.x, body.y, LabelWidth, body.height - 17);
        GUI.BeginGroup(labels);
        for (int i = 0; i <= rows.Count; i++)
        {
            float y = i * RowHeight - trackScroll.y;
            if (y + RowHeight < 0 || y > labels.height) continue;
            string label = i == 0 ? "◆ 动画事件" : (rows[i - 1].objectCurve ? "[Object] " : "") + rows[i - 1].label;
            GUI.Label(new Rect(7, y + 2, LabelWidth - 12, RowHeight), new GUIContent(label, label), EditorStyles.miniLabel);
        }
        GUI.EndGroup();
        float headX = rect.x + LabelWidth + playhead * pixelsPerSecond - trackScroll.x;
        if (headX >= ruler.x && headX <= ruler.xMax)
            EditorGUI.DrawRect(new Rect(headX, rect.y, 2, RulerHeight + BandHeight), new Color(1, .36f, .30f));
        if (session.source == null) GUI.Label(new Rect(body.x + 20, body.y + 20, body.width - 40, 60), "载入一个 AnimationClip 开始编辑。", EditorStyles.boldLabel);
    }
    private void TimelineInput(Rect all, Rect ruler, Rect band, Rect body)
    {
        var e = Event.current;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.ScrollWheel && all.Contains(e.mousePosition) && e.shift)
        { trackScroll.x = Mathf.Max(0, trackScroll.x + e.delta.y * 25); e.Use(); Repaint(); }
        if (session.source == null) return;
        if (tool == EditTool.Cut) EditorGUIUtility.AddCursorRect(new Rect(ruler.x, ruler.y, ruler.width, all.height), MouseCursor.ArrowPlus);
        else EditorGUIUtility.AddCursorRect(band, tool == EditTool.Scale ? MouseCursor.ResizeHorizontal : MouseCursor.MoveArrow);
        if (e.type == EventType.MouseDown && e.button == 0 && (ruler.Contains(e.mousePosition) || band.Contains(e.mousePosition) || body.Contains(e.mousePosition)))
        {
            GUIUtility.keyboardControl = 0; playing = false;
            float time = TimeAt(all, e.mousePosition.x);
            if (tool == EditTool.Cut && !ruler.Contains(e.mousePosition)) Cut(time);
            else if (band.Contains(e.mousePosition))
            {
                var segment = session.segments.FirstOrDefault(s => time >= s.start && time <= s.End);
                if (segment != null)
                {
                    selected = session.segments.IndexOf(segment);
                    bool resizeHandle = Mathf.Abs(e.mousePosition.x - (all.x + LabelWidth + segment.End * pixelsPerSecond - trackScroll.x)) <= 12;
                    if (tool == EditTool.Move || (tool == EditTool.Scale && resizeHandle))
                    {
                        Undo.RegisterCompleteObjectUndo(session, tool == EditTool.Move ? "Move animation segment" : "Retime animation segment");
                        dragging = segment; dragMouseTime = time; dragStart = segment.start; dragDuration = segment.duration;
                        GUIUtility.hotControl = control; dragControl = control;
                    }
                }
            }
            else
            {
                scrubbing = true; GUIUtility.hotControl = control; dragControl = control;
            }
            playhead = Mathf.Clamp(session.Snap(time), 0, session.Duration); Sample(); e.Use(); Repaint();
        }
        if (dragging != null && GUIUtility.hotControl == dragControl)
        {
            if (e.type == EventType.MouseDrag)
            {
                float delta = TimeAt(all, e.mousePosition.x) - dragMouseTime;
                if (tool == EditTool.Move) session.Move(dragging, dragStart + delta);
                else session.Resize(dragging, dragDuration + delta);
                selected = session.segments.IndexOf(dragging); EditorUtility.SetDirty(session); Sample(); e.Use(); Repaint();
            }
            if (e.type == EventType.MouseUp) { dragging = null; GUIUtility.hotControl = 0; e.Use(); }
        }
        if (scrubbing && GUIUtility.hotControl == dragControl)
        {
            if (e.type == EventType.MouseDrag)
            {
                playhead = Mathf.Clamp(session.Snap(TimeAt(all, e.mousePosition.x)), 0, session.Duration);
                Sample(); e.Use(); Repaint();
            }
            if (e.type == EventType.MouseUp) { scrubbing = false; GUIUtility.hotControl = 0; e.Use(); }
        }
    }
    private static void Diamond(Vector2 point, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        Handles.BeginGUI(); Handles.color = color;
        Handles.DrawAAConvexPolygon(new Vector3(point.x, point.y - 3), new Vector3(point.x + 3, point.y),
            new Vector3(point.x, point.y + 3), new Vector3(point.x - 3, point.y));
        Handles.EndGUI();
    }
}
