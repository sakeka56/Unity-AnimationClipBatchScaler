using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AnimationSegmentExporter
{
    private struct Cubic
    {
        public Vector2 a, b, c, d;
        public Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d) { this.a = a; this.b = b; this.c = c; this.d = d; }
        public void Split(float t, out Cubic left, out Cubic right)
        {
            var ab = Vector2.Lerp(a, b, t); var bc = Vector2.Lerp(b, c, t); var cd = Vector2.Lerp(c, d, t);
            var abc = Vector2.Lerp(ab, bc, t); var bcd = Vector2.Lerp(bc, cd, t); var point = Vector2.Lerp(abc, bcd, t);
            left = new Cubic(a, ab, abc, point); right = new Cubic(point, bcd, cd, d);
        }
        public float Parameter(float time)
        {
            float lo = 0, hi = 1;
            for (int i = 0; i < 32; i++)
            {
                float t = (lo + hi) * .5f;
                float v = 1 - t;
                float x = v * v * v * a.x + 3 * v * v * t * b.x + 3 * v * t * t * c.x + t * t * t * d.x;
                if (x < time) lo = t; else hi = t;
            }
            return (lo + hi) * .5f;
        }
    }

    public static AnimationCurve SliceCurve(AnimationCurve source, float start, float end, float duration, float fps)
    {
        if (end <= start || duration <= 0) throw new ArgumentException("Invalid curve interval.");
        var original = source.keys;
        if (original.Length == 0) return new AnimationCurve();
        // Wrapped curves outside their key range are baked at frame boundaries, since a crop can span many cycles.
        bool wrapped = (start < original[0].time && IsWrapped(source.preWrapMode)) ||
            (end > original[original.Length - 1].time && IsWrapped(source.postWrapMode));
        if (wrapped)
        {
            int count = SampleCount(duration, fps);
            var samples = new List<Keyframe>();
            for (int i = 0; i <= count; i++)
            {
                float t = duration * i / count;
                samples.Add(new Keyframe(t, source.Evaluate(Mathf.Lerp(start, end, t / duration))));
            }
            return Linear(samples);
        }
        float ratio = duration / (end - start);
        var keys = new List<Keyframe>();
        if (start < original[0].time || original.Length == 1)
            keys.Add(new Keyframe(0, source.Evaluate(start), 0, 0));
        for (int i = 0; i < original.Length - 1; i++)
        {
            var a = original[i]; var b = original[i + 1];
            float leftTime = Mathf.Max(start, a.time), rightTime = Mathf.Min(end, b.time);
            if (rightTime <= leftTime) continue;
            Keyframe left, right;
            if (float.IsInfinity(a.outTangent) || float.IsInfinity(b.inTangent))
            {
                left = new Keyframe((leftTime - start) * ratio, a.value, 0, float.PositiveInfinity);
                right = new Keyframe((rightTime - start) * ratio, rightTime >= b.time ? b.value : a.value, float.PositiveInfinity, 0);
            }
            else
            {
                float dt = b.time - a.time;
                float outWeight = (a.weightedMode & WeightedMode.Out) != 0 ? a.outWeight : 1f / 3;
                float inWeight = (b.weightedMode & WeightedMode.In) != 0 ? b.inWeight : 1f / 3;
                var cubic = new Cubic(new Vector2(a.time, a.value),
                    new Vector2(a.time + dt * outWeight, a.value + dt * outWeight * a.outTangent),
                    new Vector2(b.time - dt * inWeight, b.value - dt * inWeight * b.inTangent), new Vector2(b.time, b.value));
                Cubic ignored;
                if (leftTime > a.time) cubic.Split(cubic.Parameter(leftTime), out ignored, out cubic);
                if (rightTime < b.time) cubic.Split(cubic.Parameter(rightTime), out cubic, out ignored);
                float span = rightTime - leftTime;
                float outDx = cubic.b.x - cubic.a.x, inDx = cubic.d.x - cubic.c.x;
                left = new Keyframe((leftTime - start) * ratio, cubic.a.y, 0,
                    outDx > 1e-8f ? (cubic.b.y - cubic.a.y) / outDx / ratio : a.outTangent / ratio,
                    1f / 3, Mathf.Clamp01(outDx / span)) { weightedMode = WeightedMode.Both };
                right = new Keyframe((rightTime - start) * ratio, cubic.d.y,
                    inDx > 1e-8f ? (cubic.d.y - cubic.c.y) / inDx / ratio : b.inTangent / ratio, 0,
                    Mathf.Clamp01(inDx / span), 1f / 3) { weightedMode = WeightedMode.Both };
            }
            if (keys.Count > 0 && Mathf.Abs(keys[keys.Count - 1].time - left.time) < .000001f)
            {
                var previous = keys[keys.Count - 1];
                left.inTangent = previous.inTangent;
                left.inWeight = previous.inWeight;
                keys[keys.Count - 1] = left;
            }
            else keys.Add(left);
            keys.Add(right);
        }
        if (keys.Count == 0) keys.Add(new Keyframe(0, source.Evaluate(start)));
        if (keys[0].time > 0) keys.Insert(0, new Keyframe(0, source.Evaluate(start)));
        if (keys[keys.Count - 1].time < duration - .000001f)
            keys.Add(new Keyframe(duration, source.Evaluate(end)));
        return new AnimationCurve(keys.ToArray()) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
    }

    private static bool IsWrapped(WrapMode mode) { return mode == WrapMode.Loop || mode == WrapMode.PingPong; }
    private static int SampleCount(float duration, float fps)
    {
        double count = Math.Ceiling((double)duration * Mathf.Max(1, fps));
        if (double.IsInfinity(count) || count > 1000000) throw new ArgumentException("导出采样量过大，请缩短时间线。");
        return Mathf.Max(1, (int)count);
    }
    private static AnimationCurve Linear(List<Keyframe> keys)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            var k = keys[i];
            k.inTangent = i == 0 ? 0 : (k.value - keys[i - 1].value) / (k.time - keys[i - 1].time);
            k.outTangent = i == keys.Count - 1 ? 0 : (keys[i + 1].value - k.value) / (keys[i + 1].time - k.time);
            keys[i] = k;
        }
        return new AnimationCurve(keys.ToArray());
    }
    private static AnimationClip EmptyCopy(AnimationClip source)
    {
        var clip = Object.Instantiate(source);
        clip.hideFlags = HideFlags.None;
        clip.ClearCurves();
        AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
        return clip;
    }
    private static void Duration(AnimationClip source, AnimationClip target, float duration)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.startTime = 0;
        settings.stopTime = duration;
        AnimationUtility.SetAnimationClipSettings(target, settings);
    }

    public static AnimationClip Slice(AnimationClip source, AnimationEditSegment segment)
    {
        if (source == null || segment.sourceStart < 0 || segment.sourceEnd > source.length + .0001f ||
            segment.sourceEnd <= segment.sourceStart || !AnimationBatchScalerUtility.IsPositiveFinite(segment.duration))
            throw new ArgumentException("Invalid segment.");
        var clip = EmptyCopy(source);
        try
        {
            clip.name = segment.name;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
                AnimationUtility.SetEditorCurve(clip, binding, SliceCurve(AnimationUtility.GetEditorCurve(source, binding),
                    segment.sourceStart, segment.sourceEnd, segment.duration, source.frameRate));
            float ratio = segment.duration / (segment.sourceEnd - segment.sourceStart);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
                var output = new List<ObjectReferenceKeyframe>();
                Object value = keys.Where(k => k.time <= segment.sourceStart).Select(k => k.value).LastOrDefault();
                output.Add(new ObjectReferenceKeyframe { time = 0, value = value });
                foreach (var key in keys.Where(k => k.time > segment.sourceStart && k.time < segment.sourceEnd))
                    output.Add(new ObjectReferenceKeyframe { time = (key.time - segment.sourceStart) * ratio, value = key.value });
                output.Add(new ObjectReferenceKeyframe { time = segment.duration, value = output[output.Count - 1].value });
                AnimationUtility.SetObjectReferenceCurve(clip, binding, output.ToArray());
            }
            var events = AnimationUtility.GetAnimationEvents(source).Where(e => e.time >= segment.sourceStart &&
                (e.time < segment.sourceEnd || (Mathf.Approximately(segment.sourceEnd, source.length) && e.time <= segment.sourceEnd))).ToArray();
            foreach (var e in events) e.time = (e.time - segment.sourceStart) * ratio;
            AnimationUtility.SetAnimationEvents(clip, events);
            Duration(source, clip, segment.duration);
            return clip;
        }
        catch { Object.DestroyImmediate(clip); throw; }
    }

    public static AnimationClip Combine(AnimationEditSession session)
    {
        session.Validate();
        int count = SampleCount(session.Duration, session.source.frameRate);
        var clip = EmptyCopy(session.source);
        var parts = new Dictionary<AnimationEditSegment, AnimationClip>();
        try
        {
            foreach (var segment in session.segments) parts[segment] = Slice(session.source, segment);
            foreach (var binding in AnimationUtility.GetCurveBindings(session.source))
            {
                var curves = parts.ToDictionary(p => p.Key, p => AnimationUtility.GetEditorCurve(p.Value, binding));
                var times = new SortedSet<float> { 0, session.Duration };
                for (int i = 0; i <= count; i++) times.Add(Mathf.Min(session.Duration, i / Mathf.Max(1, session.source.frameRate)));
                // Keep cuts as hard transitions instead of interpolating across two different source poses.
                foreach (var segment in session.segments)
                {
                    times.Add(segment.start); times.Add(segment.End);
                    if (segment.start > 0) times.Add(Mathf.Max(0, segment.start - Mathf.Min(.00001f, session.Frame * .001f)));
                }
                var keys = new List<Keyframe>();
                foreach (float time in times)
                {
                    var segment = session.At(time);
                    float local = Mathf.Clamp(time - segment.start, 0, segment.duration);
                    keys.Add(new Keyframe(time, curves[segment].Evaluate(local)));
                }
                AnimationUtility.SetEditorCurve(clip, binding, Linear(keys));
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(session.source))
            {
                var keys = new SortedDictionary<float, ObjectReferenceKeyframe>();
                var first = session.segments.OrderBy(s => s.start).First();
                var firstKeys = AnimationUtility.GetObjectReferenceCurve(parts[first], binding);
                keys[0] = new ObjectReferenceKeyframe { time = 0, value = firstKeys[0].value };
                foreach (var segment in session.segments.OrderBy(s => s.start))
                    foreach (var key in AnimationUtility.GetObjectReferenceCurve(parts[segment], binding))
                    {
                        var shifted = key; shifted.time += segment.start; keys[shifted.time] = shifted;
                    }
                keys[session.Duration] = new ObjectReferenceKeyframe { time = session.Duration, value = keys.Last().Value.value };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.Values.ToArray());
            }
            var events = new List<AnimationEvent>();
            foreach (var segment in session.segments)
                foreach (var e in AnimationUtility.GetAnimationEvents(parts[segment])) { e.time += segment.start; events.Add(e); }
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());
            clip.name = session.source.name + "_Edited";
            Duration(session.source, clip, session.Duration);
            return clip;
        }
        catch { Object.DestroyImmediate(clip); throw; }
        finally { foreach (var part in parts.Values) Object.DestroyImmediate(part); }
    }

    public static List<string> Export(AnimationEditSession session, string folder, bool segments, bool combined)
    {
        session.Validate();
        folder = AnimationBatchScalerUtility.ValidateFolder(folder);
        if (folder.Length == 0) throw new ArgumentException("请选择 Assets 内的输出目录。");
        if (!segments && !combined) throw new ArgumentException("至少选择一种导出内容。");
        var pending = new List<AnimationClip>();
        var paths = new List<string>();
        try
        {
            if (segments)
            {
                int index = 1;
                foreach (var segment in session.segments.OrderBy(s => s.start))
                {
                    var clip = Slice(session.source, segment);
                    clip.name = index++.ToString("D2") + "_" + SafeName(segment.name);
                    pending.Add(clip);
                }
            }
            if (combined) pending.Add(Combine(session));
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Export animation segments");
            try
            {
                foreach (var clip in pending)
                {
                    string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(clip.name) + ".anim");
                    AssetDatabase.CreateAsset(clip, path);
                    paths.Add(path);
                    Undo.RegisterCreatedObjectUndo(clip, "Export animation segment");
                }
                AssetDatabase.SaveAssets();
            }
            catch
            {
                foreach (string path in paths) AssetDatabase.DeleteAsset(path);
                throw;
            }
            finally { Undo.CollapseUndoOperations(undo); }
            return paths;
        }
        finally { foreach (var clip in pending) if (clip != null && !EditorUtility.IsPersistent(clip)) Object.DestroyImmediate(clip); }
    }

    private static string SafeName(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Segment" : name.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return name.Replace('/', '_').Replace((char)92, '_');
    }
}
