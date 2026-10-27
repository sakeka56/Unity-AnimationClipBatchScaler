using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class AnimationEditSegment
{
    public string name;
    public float sourceStart;
    public float sourceEnd;
    public float start;
    public float duration;
    public int colorIndex;
    public float End { get { return start + duration; } }
    public float SourceTime(float time) { return Mathf.Lerp(sourceStart, sourceEnd, Mathf.Clamp01((time - start) / duration)); }
}

public class AnimationEditSession : ScriptableObject
{
    public AnimationClip source;
    public List<AnimationEditSegment> segments = new List<AnimationEditSegment>();
    public float Duration { get { return segments.Count == 0 ? 0 : segments.Max(s => s.End); } }
    public float Frame { get { return 1f / Mathf.Max(1, source == null ? 30 : source.frameRate); } }

    public void Initialize(AnimationClip clip)
    {
        if (clip == null || clip.length <= 0) throw new ArgumentException("请选择有效且有时长的 AnimationClip。");
        source = clip;
        segments = new List<AnimationEditSegment> { new AnimationEditSegment {
            name = clip.name, sourceStart = 0, sourceEnd = clip.length, start = 0, duration = clip.length, colorIndex = 0 } };
    }

    public float Snap(float time) { return Mathf.Round(time / Frame) * Frame; }

    public AnimationEditSegment Split(AnimationEditSegment segment, float time)
    {
        time = Snap(time);
        float minimum = Frame * .5f;
        if (segment == null || !segments.Contains(segment) || time - segment.start < minimum || segment.End - time < minimum)
            return null;
        float sourceCut = segment.SourceTime(time);
        var right = new AnimationEditSegment { name = segment.name + "_B", sourceStart = sourceCut,
            sourceEnd = segment.sourceEnd, start = time, duration = segment.End - time,
            colorIndex = segments.Max(s => s.colorIndex) + 1 };
        segment.name += "_A";
        segment.sourceEnd = sourceCut;
        segment.duration = time - segment.start;
        segments.Add(right);
        Sort();
        return right;
    }

    public void Move(AnimationEditSegment segment, float requestedStart)
    {
        if (segment == null || !segments.Contains(segment) || float.IsNaN(requestedStart) || float.IsInfinity(requestedStart)) return;
        float desired = Mathf.Max(0, Snap(requestedStart));
        var candidates = new List<float> { desired, 0 };
        foreach (var other in segments)
        {
            if (other == segment) continue;
            candidates.Add(other.End);
            candidates.Add(Mathf.Max(0, other.start - segment.duration));
        }
        // Snap to the nearest free location. Dragging beyond a neighbour allows reordering.
        var valid = candidates.Where(t => segments.All(s => s == segment ||
            t + segment.duration <= s.start + .00001f || t >= s.End - .00001f));
        segment.start = valid.OrderBy(t => Mathf.Abs(t - desired)).First();
        Sort();
    }

    public void Resize(AnimationEditSegment segment, float requestedDuration)
    {
        if (segment == null || !segments.Contains(segment) || float.IsNaN(requestedDuration) || float.IsInfinity(requestedDuration)) return;
        float duration = Mathf.Max(Frame, Snap(requestedDuration));
        float next = segments.Where(s => s != segment && s.start >= segment.End - .00001f)
            .Select(s => s.start).DefaultIfEmpty(float.PositiveInfinity).Min();
        segment.duration = Mathf.Min(duration, next - segment.start);
    }

    public AnimationEditSegment At(float time)
    {
        var hit = segments.FirstOrDefault(s => time >= s.start && time < s.End);
        if (hit != null) return hit;
        // Gaps hold the last pose; before the first segment, hold its initial pose.
        return segments.Where(s => s.End <= time).OrderByDescending(s => s.End).FirstOrDefault()
            ?? segments.OrderBy(s => s.start).FirstOrDefault();
    }

    public void Sort() { segments = segments.OrderBy(s => s.start).ToList(); }

    public void Validate()
    {
        if (source == null || segments.Count == 0) throw new ArgumentException("请先载入动画。");
        float end = 0;
        foreach (var segment in segments.OrderBy(s => s.start))
        {
            if (!Finite(segment.start) || !Finite(segment.duration) || !Finite(segment.sourceStart) || !Finite(segment.sourceEnd)
                || segment.start < 0 || segment.start < end - .00001f || segment.duration <= 0
                || segment.sourceStart < 0 || segment.sourceEnd > source.length + .0001f || segment.sourceEnd <= segment.sourceStart)
                throw new ArgumentException("片段时间无效或存在重叠，请检查编辑工程。");
            end = segment.End;
        }
        if (!Finite(end)) throw new ArgumentException("时间线超出可表示范围。");
    }

    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
}
