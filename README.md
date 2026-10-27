# AnimationClipBatchScaler

Unity 动画时间轴批量缩放工具。支持独立 AnimationClip（.anim）和 FBX 内嵌动画，提供中英文窗口。
工程版本：Unity **6000.3.22f1（6.3.22）**。

## 使用说明

1. 用 Unity 打开本工程；也可以把 Assets/Editor 内的工具脚本及对应 .meta 导入其他工程。
2. 打开菜单 Tools > 动画批量缩放器，或 Tools > Animation Batch Scaler。
3. 在 Project 窗口选中一个或多个 .anim / FBX。选中 FBX 会处理其中全部有效剪辑；仅选中 FBX 的某个动画子资源时只处理该剪辑。重复选择会去重，预览剪辑自动跳过。
4. 选择缩放方式：
   - **目标时长**：每个剪辑都缩放到指定秒数。
   - **时长比例**：新时长 = 原时长 × 比例。例如 0.5 表示时长减半、速度加倍；2 表示时长加倍、速度减半。此比例修改时间轴，不修改位置或缩放属性的数值。
5. 选择输出文件夹：可拖入 Project 中的文件夹，也可点击选择按钮。只支持当前工程 Assets 内已存在的文件夹；留空时保存在源文件旁。
6. 点击执行缩放。输出为独立 .anim；同名文件自动编号，避免覆盖已有资源。

### 覆盖与撤销

- 默认保留原动画并生成新文件。
- 勾选“覆盖原始 .anim”后，独立可写 .anim 原地修改，保留 GUID 和已有引用；此时输出文件夹不影响这类资源。
- FBX 内嵌动画和只读剪辑仍导出为新 .anim，使用所选输出目录或源目录，不改写 FBX。
- 支持 Unity Edit > Undo 撤销一次批量处理。
- 无效数值（0、负数、NaN、无穷大）会被拒绝，零时长剪辑会被跳过。

### 缩放时保留的数据

浮点动画曲线、关键帧切线及加权信息、曲线循环方式、对象引用曲线（例如精灵动画）、动画事件及参数、帧率和剪辑循环等设置。事件时间与关键帧时间同步缩放。

## 已完成功能（0.0.1）

- 输出目录选择。
- 时长比例输入。
- 中英文窗口共用处理逻辑。
- 修复覆盖动画导致 GUID 丢失、加权切线与循环设置丢失的问题。

## 测试

测试使用本地 Unity 6000.3.22f1。修改前确认中英文目标时长缩放有效，同时复现了加权信息和循环设置丢失问题。
回归检查覆盖目标时长、比例批处理、输出目录、同名输出、曲线形状、对象引用、事件、GUID、撤销、异常输入、真实 FBX 导入导出和两个窗口的创建。
FBX 夹具为项目自带的最小合成动画，不代表所有第三方 DCC 文件或 Humanoid 重定向素材。

不依赖额外测试包。关闭本工程的 Unity 窗口后，在工程根目录执行（其他工程可以保持打开）：

    unity run . --editor-path "D:/Program Files/UnityHub/6000.3.22f1/Editor/Unity.exe" --timeout 240 --log-file Logs/regression.log --no-tail -- -nographics -executeMethod AnimationBatchScalerTests.Run

结果：TestReports/regression.txt；详细日志：Logs/regression.log。
测试会创建并清理 Assets/__AnimationScalerRegression，若此目录已存在会拒绝覆盖。
批处理已验证处理逻辑及窗口实例化；未进行鼠标操作的界面交互验收。

---

# AnimationClipBatchScaler (English)

Batch retime standalone .anim clips and embedded FBX animations. Project version: Unity **6000.3.22f1**.

## Usage

1. Open this project in Unity, or import the tool scripts and their .meta files from Assets/Editor into another project.
2. Open Tools > Animation Batch Scaler (Chinese: Tools > 动画批量缩放器).
3. Select .anim or FBX assets in the Project window. A model selection processes all valid embedded clips; selecting an individual clip processes only that clip. Duplicates and preview clips are excluded.
4. Choose **Target duration** (each clip reaches the specified seconds) or **Duration ratio** (new duration = original × ratio). 0.5 doubles playback speed; 2 halves it. This retimes animation without scaling property values.
5. Drag an existing folder into Output folder or browse for a folder inside this project's Assets. Leave it empty to save beside each source.
6. Click Scale animations. Exports are standalone .anim files with unique filenames.

Overwrite updates writable standalone .anim assets in place, preserving GUIDs and references. Embedded FBX and read-only clips still export to the selected folder (or beside the source); the FBX is unchanged. Unity Undo supports the batch operation.

The tool preserves weighted float curves, wrap modes, object-reference curves, events and payloads, frame rate and clip settings. Times and tangents are retimed together. Nonpositive/nonfinite inputs are rejected; zero-length clips are skipped.

## Implemented in 0.0.1

- Output directory selection and duration-ratio input.
- Shared Chinese/English implementation.
- Fixes for GUID, weighted tangent and loop-setting loss.

## Verification

Run the command above from the project root with this project closed. No additional test package is needed.
See TestReports/regression.txt and Logs/regression.log. Tests cover retiming, output collisions, metadata, Undo, invalid inputs, synthetic FBX import/export and window instantiation.
The included FBX fixture does not cover every DCC export or Humanoid retargeting setup. Mouse-driven UI interaction has not been validated.
