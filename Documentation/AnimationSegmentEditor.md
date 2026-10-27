# 动画分段编辑器

入口：**Tools > 动画分段编辑器**，也可以从 Window > Animation > Animation Segment Editor 打开。
使用 Unity 6000.3.22f1 开发和验证。

安装及工具总览见 [README](../README.md)；数据结构、裁切算法和维护方式见 [技术设计文档](TechnicalDesign.md)。新增分段编辑器包含在当前源码中，已有的 0.0.1 Release 安装包只包含批量缩放器。

## 开始编辑

1. 在窗口左上角指定 AnimationClip，或选中 Project 中的 .anim / FBX 后点击“载入所选动画”。FBX 有多个剪辑时，先展开并选中要编辑的动画子资源。
2. 左侧显示属性轨道、所有浮点和对象引用关键帧，以及动画事件。菱形对应源动画关键点，位置随片段移动和时长变化同步更新。顶部时间尺以秒为单位，可缩放和横向滚动；轨道列表可搜索和纵向滚动。
3. 右侧指定预览模型 / Prefab。FBX 动画会自动尝试使用同文件的模型。独立 .anim 无法推断适用模型，需要自行指定。模型层级和绑定路径应与动画相匹配；Humanoid 动画需要匹配的 Avatar。
4. 点击或拖动时间尺、关键帧区域可拖动播放头；空格播放或暂停。预览区域拖拽旋转视角、滚轮缩放。预览只采样模型副本，不修改场景中的原对象；副本不运行自定义 MonoBehaviour。

## 三种工具

| 工具 | 操作 | 结果 |
| --- | --- | --- |
| 切割 C | 点击片段或关键帧区域 | 按最近帧把所在片段分成两段 |
| 移动 W | 拖动片段色块 | 移动至最近可用位置，可拖过相邻片段重排 |
| 缩放 S | 拖动片段右边缘 | 左端固定，调整本段时长，源区间保持不变，实现变速 |

也可以点击“切播放头”在当前播放头处切割。快捷键在编辑文本时不会触发工具切换。

- 每个片段有独立颜色、名称和时长，上方卡片显示片段数量、总时长及可直接编辑的 Name。
- 点击卡片或色块选中片段，右侧可精确填写时间线位置与时长。
- 时间调整按源动画帧率吸附，缩放通常至少保留一帧。为避免覆盖其他片段，缩放到下一个片段起点时停止；可以先把后面的片段移开。源动画末尾不在整帧或邻段间距不足一帧时，边界片段可能短于一帧。
- 单轨道不允许重叠，不进行片段间混合。片段间空隙保持前一段末帧，首段之前保持首段初始姿态，总时长包含空隙。
- 支持 Unity Undo/Redo，可撤销切割、移动、缩放、命名、移除片段和导出资源。

## 保存编辑工程

点击“保存编辑工程…”生成 AnimationEditSession `.asset`，保存源动画引用、片段名称、源区间、排列位置和时长。
下次在“编辑工程”栏载入该资源即可继续。编辑工程只记录编辑数据，源 .anim / FBX 不会被改写。预览模型与窗口状态不包含在该资产中；`.asset` 为编辑器专用，运行时播放应使用导出的 `.anim`。

新建编辑工程尚未保存时，栏位会标记“未保存”。关闭窗口前请保存；切换源动画会新建工程。

## 一键导出

选择当前工程 **Assets 内已存在的文件夹**，然后点击“一键导出”。可分别勾选：

- **各片段 .anim**：按照时间线顺序导出为 `01_Name.anim`、`02_Name.anim` 等，每段从零秒开始，包含裁切后的曲线、对象引用、动画事件及剪辑设置。
- **完整时间线（按帧）**：导出 `SourceName_Edited.anim`，包含片段排序、位置、变速和空隙。浮点曲线按源帧率采样，切换处额外加入边界采样；不承诺帧间插值与原曲线完全相同。

同名文件自动编号，不覆盖已有资源。Name 中不允许用于文件名的字符会自动替换。
边界事件仅归属右侧片段，源动画末尾事件保留在最后源区间；同一事件不会因为一次切割而重复。
独立片段导出通过贝塞尔裁切保留普通/加权曲线的边界形状；若某条曲线在关键帧范围外使用 Loop/PingPong，则该扩展区间按帧采样。

导出不会自动保存编辑工程。要保留后续可继续调整的分段信息，请同时保存 `.asset`。

## 验证

数据与图形设备回归（关闭本工程后执行，其他工程不受影响）：

```powershell
unity run . --editor-path "D:/Program Files/UnityHub/6000.3.22f1/Editor/Unity.exe" --timeout 240 --log-file Logs/segment-tests.log --no-tail -- -executeMethod AnimationSegmentEditorTests.Run
```

原批量缩放器回归入口仍为 `AnimationBatchScalerTests.Run`。

图形窗口交互验收使用独立、普通 Unity 编辑器进程，会临时建立测试模型和动画，模拟 C 切割、W 拖动及 S 变速，完成后删除临时资源并退出该进程。可按 [技术文档中的命令](TechnicalDesign.md#测试与复现) 运行，或在本机路径不含空格时使用下列命令：

```powershell
unity open . --editor-path "D:/Program Files/UnityHub/6000.3.22f1/Editor/Unity.exe" --args "-executeMethod AnimationSegmentEditorVisualTests.Run -logFile D:/MyFiles/Unity/Pro/AnimationClipBatchScaler/Logs/segment-visual.log"
```

不要在包含未保存工作、已经打开的编辑器实例上运行该验收入口。数据回归和图形验收分别拒绝覆盖已存在的 `Assets/__AnimationSegmentTests` 与 `Assets/__AnimationSegmentVisual` 目录。

报告位于 `TestReports/segment-editor.txt` 和 `TestReports/segment-editor-ui.txt`。`TestReports/segment-preview.png` 直接从预览渲染纹理生成，用于检查模型取景。
当前记录为分段回归 9 个场景、479 项断言及图形交互验收通过；原批量缩放器回归为 8 个场景、68 项断言通过。
FBX 回归使用合成夹具，尚未覆盖所有第三方模型、Humanoid 根运动和 IK/约束系统。预览针对动画属性采样，不执行游戏逻辑、动画事件接收器或运行时状态机。
