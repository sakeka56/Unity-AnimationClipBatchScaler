# Unity Animation Tools · AnimationClipBatchScaler

面向 Unity 的动画编辑工具集，支持独立 `.anim` 和 FBX 内嵌动画。工程使用 **Unity 6000.3.22f1（6.3.22）**。

| 工具 | 菜单入口 | 主要功能 |
| --- | --- | --- |
| 动画批量缩放器 | Tools > 动画批量缩放器 / Animation Batch Scaler | 批量指定目标时长或时长比例，选择输出目录 |
| 动画分段编辑器 | Tools > 动画分段编辑器 | 关键帧时间线、模型预览、切割、移动重排、分段变速与导出 |

## 安装

- **使用完整工程**：克隆仓库后，通过 Unity Hub 添加工程目录，用 Unity 6000.3.22f1 打开。
- **导入已有工程**：复制 `Assets/Editor` 下的四个批量缩放脚本（`AnimationBatchScaler_CN.cs`、`AnimationBatchScaler_EN.cs`、`AnimationBatchScalerUtility.cs`、`AnimationBatchScalerWindow.cs`）及 `AnimationSegmentEditor` 文件夹，保留对应 `.meta`。不需要复制 `Tests` 或发布包导出脚本。
- **使用发布包**：[0.0.1 Release](https://github.com/sakeka56/Unity-AnimationClipBatchScaler/releases/tag/0.0.1) 包含批量缩放器；新增分段编辑器请使用当前仓库源码。

## 动画分段编辑器

入口：**Tools > 动画分段编辑器**，或 **Window > Animation > Animation Segment Editor**。

1. 指定 AnimationClip，或在 Project 中选中 `.anim` / FBX 动画子资源，点击“载入所选动画”。
2. 左侧查看关键帧、对象引用轨道和动画事件；点击或拖动时间尺可调整播放头。可搜索轨道、缩放和滚动时间线。
3. 右侧指定匹配的模型 / Prefab 预览。FBX 动画会尝试自动使用同文件模型；独立 `.anim` 需要自行指定模型。
4. 使用下列工具编辑片段，上方卡片可填写各片段的 **Name**，并显示片段数量、片段时长和总时长。
5. 保存“编辑工程”以便下次继续；选择 Assets 内的输出文件夹，一键导出动画。

| 快捷键 | 操作 | 效果 |
| --- | --- | --- |
| C | 点击片段或关键帧区域 | 按最近帧切割 |
| W | 拖动片段色块 | 移动到空位或重排 |
| S | 拖动片段右边缘 | 固定左端，改变时长和播放速度 |
| 空格 | 播放 / 暂停 | 预览当前时间线 |
| Ctrl/Cmd+Z | 撤销 | 撤销片段编辑或导出 |

片段以不同颜色区分，支持右侧数值精调。单轨道不允许重叠，空隙保持上一段末帧。源动画不被改写。

导出内容可分别勾选：

- **各片段 `.anim`**：按时间线顺序生成 `01_Name.anim`、`02_Name.anim` 等，每段从零秒开始。
- **完整时间线（按帧）**：生成 `SourceName_Edited.anim`，保留片段顺序、位置、变速和空隙；浮点曲线按源帧率采样。

同名文件自动编号。保存的 `AnimationEditSession.asset` 保留分段编辑信息；导出的 `.anim` 用于播放。

[分段编辑器完整使用说明](Documentation/AnimationSegmentEditor.md) · [技术设计与维护说明](Documentation/TechnicalDesign.md)

## 动画批量缩放器

1. 打开 **Tools > 动画批量缩放器**，或英文入口 **Tools > Animation Batch Scaler**。
2. 在 Project 中选中一个或多个 `.anim` / FBX。选中 FBX 会处理其有效动画；选中某个动画子资源时只处理该剪辑。重复选择去重，预览剪辑跳过。
3. 选择“目标时长”或“时长比例”。比例含义为 **新时长 = 原时长 × 比例**：`0.5` 时长减半、速度加倍；`2` 时长加倍、速度减半。
4. 选择 Assets 内已存在的输出文件夹；留空时保存在源文件旁。
5. 点击执行缩放。

默认生成新文件，重名自动编号。勾选“覆盖原始 .anim”会原地更新可写的独立 `.anim`，保留 GUID 和已有引用，支持撤销；FBX 内嵌或只读剪辑仍导出为新文件。

缩放保留关键帧加权信息、曲线循环方式、对象引用、动画事件及参数、帧率和剪辑设置；关键帧与事件时间同步缩放，切线按比例调整。无效数值被拒绝，零时长剪辑跳过。此操作调整时间轴，不缩放位置等属性值。

## 验证与限制

已在 Unity **6000.3.22f1** 验证：

| 检查 | 结果 | 报告 |
| --- | --- | --- |
| 原批量缩放器回归 | 8 个场景，68 项断言通过 | [regression.txt](TestReports/regression.txt) |
| 分段编辑器回归 | 9 个场景，479 项断言通过 | [segment-editor.txt](TestReports/segment-editor.txt) |
| 独立图形窗口验收 | C 鼠标切割、W 拖动重排、S 右边缘变速、预览渲染通过 | [segment-editor-ui.txt](TestReports/segment-editor-ui.txt) |

回归不依赖额外测试包。关闭本工程的 Unity 编辑器后，在工程根目录运行：

```powershell
unity run . --editor-path "<本地 Unity.exe 的绝对路径>" --timeout 240 --log-file Logs/regression.log --no-tail -- -nographics -executeMethod AnimationBatchScalerTests.Run
unity run . --editor-path "<本地 Unity.exe 的绝对路径>" --timeout 240 --log-file Logs/segment-tests.log --no-tail -- -executeMethod AnimationSegmentEditorTests.Run
```

第二条命令启用图形设备，用于验证窗口与快捷键。图形窗口鼠标验收的单独运行方式见[技术文档](Documentation/TechnicalDesign.md#测试与复现)。

FBX 测试使用合成夹具，尚未覆盖所有第三方 DCC 文件、Humanoid 根运动和 IK/约束系统。分段编辑器展示关键帧并编辑片段，不提供 Unity Animation 窗口完整的逐关键帧数值编辑功能。完整时间线采用按帧采样，帧间插值可能与原曲线不同。

## 文档

- [分段编辑器使用说明](Documentation/AnimationSegmentEditor.md)
- [技术设计、数据模型与导出算法](Documentation/TechnicalDesign.md)
- [原工具修改前后的验证记录](TestReports/verification.md)
- [许可证](LICENSE.txt)

---

## English

This repository contains two Unity Editor tools, developed and verified with **Unity 6000.3.22f1**.

| Tool | Menu | Features |
| --- | --- | --- |
| Animation Batch Scaler | Tools > Animation Batch Scaler | Batch retiming by target duration or duration ratio, configurable output folder |
| Animation Segment Editor | Window > Animation > Animation Segment Editor | Keyframe timeline, model preview, cuts, movement/reordering, per-segment retiming, editable names and export |

Clone this repository to use both tools. The **0.0.1 release package contains only the batch scaler**. To install the current tools in another project, copy the four batch-scaler scripts listed above and the `AnimationSegmentEditor` folder, including their `.meta` files. Tests and the release-export helper are optional.

For segment editing, load an AnimationClip and assign a compatible model/prefab for preview. **C** selects the cutting tool, **W** moves segments, and **S** retimes a segment by dragging its right edge. Space toggles playback. Name fields, segment counts and durations appear above the timeline. Shortcuts do not switch tools while editing text.

Save an `AnimationEditSession.asset` to retain the editable arrangement. Export individual `.anim` segments, a combined timeline, or both to an existing folder inside Assets. Exports use unique filenames and keep the source asset unchanged. The combined timeline samples float curves at the source frame rate; gaps hold the preceding pose and segments do not overlap.

The batch scaler supports standalone `.anim` and embedded FBX clips. A duration ratio of `0.5` doubles playback speed; `2` halves it. Writable standalone clips can be updated in place while retaining GUIDs and references. Embedded/read-only clips export as new files.

Verification: **8 batch-scaler cases / 68 assertions**, **9 segment-editor cases / 479 assertions**, and a graphical mouse-interaction/preview test passed. Synthetic FBX fixtures do not cover all DCC, Humanoid, root-motion or IK workflows. The segment editor visualizes keyframes but does not implement the full per-key editing interface of Unity's Animation window.

[User guide (Chinese)](Documentation/AnimationSegmentEditor.md) · [Technical design (Chinese)](Documentation/TechnicalDesign.md) · [License](LICENSE.txt)
