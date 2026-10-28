using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AnimationSegmentPreview : IDisposable
{
    private PreviewRenderUtility preview;
    private GameObject instance;
    private GameObject container;
    private Vector3 lastRootPosition;
    private Bounds bounds;
    private Texture lastFrame;
    private Vector2 orbit = new Vector2(135, 15);
    private float zoom = 1;
    public bool HasTarget { get { return instance != null; } }
    public bool HasRenderers { get { return instance != null && instance.GetComponentsInChildren<Renderer>(true).Length > 0; } }

    public void SetTarget(GameObject target)
    {
        Dispose();
        if (target == null) return;
        preview = new PreviewRenderUtility();
        container = new GameObject("AnimationPreviewSceneRoot") { hideFlags = HideFlags.HideAndDontSave };
        container.SetActive(false);
        preview.AddSingleGO(container);
        // Instantiate under an inactive parent so ExecuteAlways scripts cannot enable before removal.
        instance = Object.Instantiate(target, container.transform, false);
        instance.name = "AnimationSegmentPreview";
        instance.hideFlags = HideFlags.HideAndDontSave;
        foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true)) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        instance.SetActive(true);
        container.SetActive(true);
        foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Frame();
    }

    public void Frame()
    {
        if (instance == null) return;
        bounds = new Bounds(instance.transform.position, Vector3.one);
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        }
        zoom = 1;
        lastRootPosition = instance.transform.position;
    }

    public void Sample(AnimationClip clip, float time)
    {
        if (instance != null && clip != null)
        {
            clip.SampleAnimation(instance, Mathf.Clamp(time, 0, clip.length));
            bounds.center += instance.transform.position - lastRootPosition;
            lastRootPosition = instance.transform.position;
        }
    }

    public Texture Render(Rect rect)
    {
        if (instance == null || rect.width < 2 || rect.height < 2) return null;
        preview.BeginPreview(rect, GUIStyle.none);
        Texture texture;
        try
        {
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(.12f, .13f, .16f);
            float radius = Mathf.Max(.1f, bounds.extents.magnitude);
            float aspect = Mathf.Max(.1f, rect.width / rect.height);
            float halfFov = Mathf.Atan(Mathf.Tan(15 * Mathf.Deg2Rad) * Mathf.Min(1, aspect));
            float distance = radius / Mathf.Sin(halfFov) * 1.15f * zoom;
            var rotation = Quaternion.Euler(orbit.y, orbit.x, 0);
            preview.camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
            preview.camera.nearClipPlane = Mathf.Max(.001f, distance / 1000);
            preview.camera.farClipPlane = distance + radius * 12;
            preview.camera.fieldOfView = 30;
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, 35, 0);
            preview.lights[1].intensity = .7f;
            preview.ambientColor = new Color(.45f, .45f, .45f);
            preview.Render(true);
        }
        finally { texture = preview.EndPreview(); }
        lastFrame = texture;
        return texture;
    }

    public void Draw(Rect rect)
    {
        var evt = Event.current;
        if (rect.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.MouseDrag && (evt.button == 0 || evt.button == 1))
            {
                orbit += new Vector2(evt.delta.x, evt.delta.y) * .5f;
                orbit.y = Mathf.Clamp(orbit.y, -85, 85);
                evt.Use();
            }
            if (evt.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom * (1 + evt.delta.y * .06f), .15f, 12);
                evt.Use();
            }
        }
        if (evt.type == EventType.Repaint)
        {
            var texture = Render(rect);
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            else EditorGUI.DrawRect(rect, new Color(.12f, .13f, .16f));
        }
        if (instance == null) GUI.Label(new Rect(rect.x + 16, rect.y + 16, rect.width - 32, 64),
            "指定预览模型 / Prefab 后可预览。\nFBX 动画会自动尝试使用其原始模型。", EditorStyles.wordWrappedLabel);
        GUI.Label(new Rect(rect.x + 10, rect.yMax - 25, rect.width - 20, 20), "拖拽旋转 · 滚轮缩放 · 仅采样预览副本", EditorStyles.miniLabel);
    }

    public void Dispose()
    {
        if (instance != null) Object.DestroyImmediate(instance);
        instance = null;
        if (container != null) Object.DestroyImmediate(container);
        container = null;
        if (preview != null) preview.Cleanup();
        preview = null;
        lastFrame = null;
    }
}
