# Unity Animation Tools

[English](README.md) | [简体中文](README.zh-CN.md)

Two Unity Editor tools for working with standalone `.anim` files and animations embedded in FBX files: cut and rearrange a clip with **Animation Segment Editor**, or retime multiple clips with **Animation Batch Scaler**.

## Choose a tool

| What you want to do | Tool | Open from |
| --- | --- | --- |
| Split a clip, rearrange sections, or change the speed of one section | Animation Segment Editor | `Window > Animation > Animation Segment Editor` |
| Give multiple clips a target duration or apply the same duration ratio | Animation Batch Scaler | `Tools > Animation Batch Scaler` |

![Animation Segment Editor demonstration](Documentation/Media/segment-editor-demo.gif)

[Watch the full video](Documentation/Media/segment-editor-demo.mp4)

## Install

Developed and verified with **Unity 6000.3.22f1**. Other Unity versions have not been verified. The tools run in the Editor and require no additional UPM packages.

### Import the 1.0.0 package

1. Download `UnityAnimationTools-1.0.0.unitypackage` from the [1.0.0 release page](https://github.com/sakeka56/Unity-AnimationClipTools/releases/tag/1.0.0).
2. In Unity, choose `Assets > Import Package > Custom Package…`, select the package, and import all contents.
3. Wait for compilation, then open the tools from Tools or Window. The 1.0.0 package includes both tools.

### Install from source in your existing project

1. Download or clone this repository.
2. Copy the following files and folder into your project's `Assets/Editor`, including their `.meta` files:

   ```text
   Assets/Editor/
   ├── AnimationBatchScaler_CN.cs
   ├── AnimationBatchScaler_EN.cs
   ├── AnimationBatchScalerUtility.cs
   ├── AnimationBatchScalerWindow.cs
   └── AnimationSegmentEditor/       (entire folder)
   ```

3. Return to Unity and wait for script compilation. Open either tool from the menus above.

`Tests` and `AnimationBatchScalerRelease.cs` are not required. If you only need the segment editor, copy `AnimationSegmentEditor/` and `AnimationBatchScalerUtility.cs` with their `.meta` files.

### Open the example project

Clone the repository, add its root folder in Unity Hub, and open it with Unity **6000.3.22f1**.

The [0.0.1 release package](https://github.com/sakeka56/Unity-AnimationClipTools/releases/tag/0.0.1) contains **only the batch scaler**. Use the current source to install both tools.

## Animation Segment Editor

Open `Window > Animation > Animation Segment Editor`, or `Tools > 动画分段编辑器`.

The segment editor currently has a Chinese interface. The instructions below include the labels you will see in the window.

### 1. Load an animation

Drag a `.anim` or FBX asset from the Project window into the source slot at the top left. If the FBX contains multiple clips, choose one from the dropdown beside it. Alternatively, select the asset in Project and click **载入所选动画** (Load selected animation).

The left side displays the clip's keyframe tracks and animation events. Click or drag the ruler to move the playhead. Use the search field to find a track.

**Save your current editing session before switching the source clip:** switching creates a new session.

### 2. Preview on a model

Assign a compatible model or Prefab in the right-hand preview panel. For FBX clips, the tool first tries the model in the same file. For standalone clips, it also tries the selected model or scene objects that reference the clip.

- **自动定位模型** (Auto locate model): try finding a compatible preview target again.
- **模型居中 / F** (Frame model): center the camera on the current pose.
- Drag in the preview to rotate the view; scroll to zoom.
- Press **Space** or click **播放** (Play) to play or pause.

The model hierarchy and animation binding paths must match. A skeleton-only FBX needs a separate matching model with a mesh; Humanoid clips need a compatible Avatar.

For clips without their own renderable model, a manually assigned model is remembered as this project's default preview target. FBX files with a renderable model continue to use their own model. Preview samples a copy and does not modify the scene object.

### 3. Cut, rearrange, and retime

| Tool | How to use it | Result |
| --- | --- | --- |
| **C — 切割** (Cut) | Click a segment or its keyframe area | Split at the nearest frame |
| **W — 移动** (Move) | Drag a colored segment | Move into free space or past another segment to reorder |
| **S — 缩放** (Retime) | Drag anywhere on a colored segment; repeated drags are supported | Keep the left edge fixed and change duration / playback speed |

You can also position the playhead and click **切播放头** (Cut at playhead).

Select a segment to edit **Name**, **时间线位置（秒）** (Timeline position in seconds), and **片段时长（秒）** (Segment duration in seconds) on the right. Names can also be edited in the cards above the timeline. Use **恢复本段原始时长** to restore its source duration, or **移除此片段** to remove it from the arrangement.

For example, cut a 3-second clip at 1 s and 2 s. Select the middle segment and set its duration to 0.5 s: that section now plays at twice its original speed. Use W to move the following segment if you want to close the gap.

Segments cannot overlap. Retiming stops at the next segment, so move that segment away first if you need more space. Moving a segment does not automatically shift later segments. Gaps hold the previous segment's final pose; cuts switch directly without blending.

See the demo at the top of this guide, or [watch the full video](Documentation/Media/segment-editor-demo.mp4).

### 4. Save an editable session

Click **保存编辑工程…** (Save editing session) to create an `AnimationEditSession` `.asset`. It stores the source clip reference, segment names, source ranges, timeline positions, and durations.

To continue later, assign this asset to **编辑工程** (Editing session) near the top of the window. Save before closing the window or switching clips. The session asset does not include the preview model or window layout.

### 5. Export animations

1. Choose a folder inside the current project's **Assets** in **输出目录** (Output directory). The **选择…** (Browse) dialog also supports creating a new folder.
2. Enable one or both export options:

   | Option | Output | When to use it |
   | --- | --- | --- |
   | **各片段 .anim** (Individual segments) | `01_Name.anim`, `02_Name.anim`, …; each starts at 0 s | Use each section as a separate clip |
   | **完整时间线（按帧）** (Full timeline, sampled per frame) | `SourceName_Edited.anim` | Use the entire edited arrangement as one clip |

3. Click **一键导出** (Export). Use the resulting `.anim` assets in your animation workflow, such as an Animator Controller.

Exports keep the source unchanged and use unique filenames when a file already exists. The combined clip includes ordering, retiming, and gaps; float curves are sampled at the source frame rate, so interpolation between frames may differ from the original curves.

**Exporting does not save the editing session.** Save the `.asset` as well if you want to keep editing later. The session is Editor-only; use the exported `.anim` files for runtime playback.

![Saved animation editing session and segment list](Documentation/Images/edit-session.png)

The `.asset` shown stores the source reference and three segments. Load it in 编辑工程 to resume editing.

### Timeline shortcuts

| Input | Action |
| --- | --- |
| C / W / S | Select Cut / Move / Retime |
| Space | Play / pause |
| F | Frame the preview model |
| Alt + mouse wheel | Zoom around the time under the pointer |
| Middle-button drag | Pan horizontally and scroll tracks vertically |
| Mouse wheel / Shift + mouse wheel | Scroll vertically / horizontally |
| Ctrl/Cmd + Z | Undo edits or export |

Tool shortcuts do not switch tools while you are editing text. Unity Undo/Redo is supported for segment editing and export.

## Animation Batch Scaler

1. Open `Tools > Animation Batch Scaler` (English), or `Tools > 动画批量缩放器` (Chinese).
2. Select one or more `.anim` / FBX assets in the **Project** window. Selecting an FBX processes its valid clips; selecting an individual clip sub-asset processes only that clip. Duplicate selections and preview clips are skipped.
3. Choose **Target duration** or **Duration ratio**, then enter a value greater than zero.
4. Choose an existing output folder inside **Assets**, or leave it empty to save beside each source asset.
5. Click **Scale animations**.

| Mode | Example | Result |
| --- | --- | --- |
| Target duration | Enter `1.5` | Every processed clip becomes 1.5 seconds long |
| Duration ratio | Enter `0.5` | Half the duration, twice the speed |
| Duration ratio | Enter `2` | Twice the duration, half the speed |

**New duration = original duration × ratio.** This changes animation timing, not property values such as position or model size.

By default, the tool creates `ClipName_scaled.anim` files and adds a unique suffix if needed. Enable **Overwrite original .anim** to update writable standalone `.anim` assets in place, preserving GUIDs and existing references. Embedded FBX clips and read-only clips still export as new files. Undo is supported.

Retiming preserves curve weights, wrap modes, object reference tracks, event parameters, frame rate, and clip settings. Keyframe and event times are scaled together. Zero-length clips are skipped.

<img src="Documentation/Images/batch-scaler.png" alt="Animation Batch Scaler interface in Chinese" width="520" />

No clips are selected in this screenshot, so the action button is disabled. Select animation assets in Project to continue.

## Common questions

| Problem | What to check |
| --- | --- |
| Menus do not appear | Keep the scripts under `Assets/Editor`, include the required files, and resolve Console compilation errors |
| Preview is empty or the model does not animate | Assign a matching model with a mesh, check binding paths / Avatar, then press F |
| A segment will not become longer | Move the next segment away; segments cannot overlap |
| Export is disabled | Load a source, select an Assets folder, and enable at least one export option |
| Scale animations is disabled | Select `.anim` / FBX assets in Project and enter a positive finite value |
| Changes are missing when reopening | Load the saved session `.asset`; exported `.anim` files do not store the editable arrangement |

The segment editor displays keyframes and edits segments; use Unity's Animation window for individual keyframe value editing. Preview does not execute gameplay scripts, animation event receivers, or a runtime state machine. Third-party DCC files, Humanoid root motion, and IK / constraint workflows need validation with your own assets.

## Further documentation

- [Chinese quick-start Word guide with offline video](https://github.com/sakeka56/Unity-AnimationClipTools/releases/download/1.0.0/UnityAnimationTools-1.0.0-Guide.zh-CN.zip)
- [1.0.0 release validation (Chinese)](Documentation/Releases/1.0.0.md)

- [Detailed segment editor guide (Chinese)](Documentation/AnimationSegmentEditor.md)
- [Technical design and maintenance (Chinese)](Documentation/TechnicalDesign.md)
- Existing regression reports: [batch scaler](TestReports/regression.txt), [segment editor](TestReports/segment-editor.txt), [navigation and folders](TestReports/segment-navigation.txt), [graphical interaction](TestReports/segment-editor-ui.txt). These are repository verification records for Unity 6000.3.22f1.
- [License](LICENSE.txt)

## Support

If these tools help your workflow, you can support their development by scanning the code below. Thank you!

<img src="Documentation/Images/sponsor.jpg" alt="WeChat appreciation code for supporting the author" width="300" />
