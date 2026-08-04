# Scene2 实时实验指引卡片技术开发文档

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档版本 | v1.0 |
| 创建日期 | 2026-08-04 |
| 适用工程 | `ElectroOptic-Lab`（Unity 2022.3.62f2c1） |
| 适用场景 | `Assets/Scenes/Scene2.The Lab.unity` |
| 需求依据 | `Docs/PRD/PRD_Scene2_RealtimeGuide_Redesign.md` v1.0 |
| 目标读者 | Unity 开发、UI 开发、测试、后续维护人员 |
| 文档状态 | 待开发 |

---

## 1. 文档目的

本文将 Scene2 实时实验指引卡片 PRD 转换为可执行的 Unity 技术方案，明确：

- 六阶段状态采集、判定、稳定计时和单向推进的实现边界；
- 卡片展开/收起、相机可见性、完成反馈的 UI 状态机；
- 激光校准 Enter 锁定的输入接管方案；
- 消光亮态基准的建立与使用规则；
- GIF 读取、解码、循环播放、异常处理和资源释放；
- 模态弹窗对 uGUI、3D 点击和键盘实验操作的完整锁定；
- 现有 Scene2 UI 的原位升级、第三方依赖和测试落地方式。

本文只规定开发方案，不修改旧四页 PNG 手册的入口与行为，不包含工时或排期。

---

## 2. 当前实现基线

### 2.1 现有实时指引

当前实时指引主要由以下文件组成：

- `Assets/Scripts/UI/ExperimentGuide/Scene2CardGuide.cs`
- `Assets/Scripts/UI/ExperimentGuide/Scene2CardGuideView.cs`
- `Assets/Scripts/UI/ExperimentGuide/Editor/Scene2CardGuideSceneBuilder.cs`

`Scene2CardGuide` 同时负责动态步骤生成、对象查找、每帧状态轮询、UI 更新和目标指示图形，职责过于集中。步骤会因场景对象缺失而被省略，无法使用数组下标稳定绑定阶段或 GIF。当前 F1 的行为是隐藏/重新开始，也不等同于本次要求的展开/收起。

场景中的 `Scene2CardGuideRoot` 和 `Scene2CardGuideView` 已有稳定资源引用。本次保留脚本文件及其 `.meta`，在原对象上升级结构，避免 Missing Script 或新旧卡片并存。

### 2.2 可复用状态来源

| 状态 | 当前来源 | 接入方式 |
|------|----------|----------|
| 器件是否在轨 | `OpticalComponent.isOnRail` | 直接读取 |
| 激光传播方向 | `LaserEmitter.transform` | 使用 `-transform.right` |
| 偏振器角度 | `RotateStandController.GetCurrentRotateAngle()` | 直接读取 |
| 光屏显示模式 | `UnifiedScreenPanel.CurrentMode` | 直接读取 |
| 特写状态 | `ExperimentCameraController.IsInCloseUpView` | 作为相机可见性的一项输入 |
| 激光锁定状态 | `LaserEmitterMover.isCalibrationDone` | 由新校准接管器控制 |
| 光屏强度与红点坐标 | `DirectScreenController` 私有字段 | 由只读遥测适配器读取 |

### 2.3 必须规避的现有行为

1. `LaserEmitterMover` 在激光被选中时会接受任意 Enter 并直接锁定，无法满足“居中稳定后 Enter 才有效”。
2. `DirectScreenController` 只公开纹理，实际强度和红点坐标仍为私有字段。
3. `CameraSwitch` 未公开当前目标视角和过渡完成状态，仅依赖 `IsInCloseUpView` 无法识别正视、俯视和普通过渡。
4. 全屏 uGUI 遮罩只能拦截 UI 射线，不能保证阻止所有 `OnMouseDown` 和直接读取 `Input` 的脚本。
5. 现有 Editor Builder 遇到已有 View 会提前返回，不能完成旧结构升级。

---

## 3. 设计原则

1. **固定阶段身份**：所有业务、GIF 和测试均以 `Scene2GuideStageId` 关联，不使用运行时数组下标作为业务 ID。
2. **判定与显示分离**：状态采集、纯判定、流程推进、UI、GIF、交互锁分别实现。
3. **适配旧系统**：不修改旧光学器件业务逻辑；通过新脚本和序列化引用读取或接管必要状态。
4. **失败关闭**：关键引用或遥测不可用时，相关阶段保持等待并记录一次明确错误，不通过省略阶段或放宽条件继续。
5. **进度单向**：已完成阶段永不回退；当前阶段只按自己的完整条件判断。
6. **精确恢复**：模态关闭后恢复打开前的启用状态、相机事件掩码和卡片状态，不把所有组件无条件设为启用。
7. **生命周期对称**：每个注册、锁定、异步任务和运行时纹理都必须有对应的注销、解锁、取消和销毁路径。
8. **可测试优先**：光路顺序、角度、阈值和阶段判定使用无 MonoBehaviour 依赖的纯逻辑类。

---

## 4. 总体架构

```text
Scene2 实验组件
  └─ Scene2GuideStateProvider
      ├─ Scene2GuideScreenTelemetryReader
      ├─ Scene2GuideRailOrderCalculator
      └─ Scene2GuideStateSnapshot
             │
             ▼
      Scene2GuideStageEvaluator（纯逻辑）
             │
             ▼
      Scene2CardGuide（流程编排）
      ├─ Scene2GuideLaserCalibrationGate
      ├─ Scene2GuideCameraVisibility
      ├─ Scene2CardGuideView
      └─ Scene2GuideGifPopup
          ├─ IGuideGifPlayer / UnityGifGuidePlayer
          └─ Scene2GuideInteractionLock
```

数据流为单向：场景组件只被状态适配器读取；纯判定器输出判定结果；控制器维护当前阶段和计时；View 只接收展示模型；弹窗通过独立接口暂停推进并锁定实验交互。

---

## 5. 目录与文件规划

### 5.1 保留并重构

| 文件 | 调整内容 |
|------|----------|
| `Assets/Scripts/UI/ExperimentGuide/Scene2CardGuide.cs` | 保留 GUID，改为阶段流程编排器，移除动态步骤、目标圈、箭头和模拟光标职责 |
| `Assets/Scripts/UI/ExperimentGuide/Scene2CardGuideView.cs` | 保留 GUID，改为卡片展开/收起和文本渲染 View |
| `Assets/Scripts/UI/ExperimentGuide/Editor/Scene2CardGuideSceneBuilder.cs` | 改为可重复执行的创建/升级/修复工具 |

### 5.2 新增运行时代码

建议放置于 `Assets/Scripts/UI/ExperimentGuide/Realtime/`：

```text
Scene2GuideStageId.cs
Scene2GuideStageDefinition.cs
Scene2GuideStateSnapshot.cs
IScene2GuideStateProvider.cs
Scene2GuideStateProvider.cs
Scene2GuideScreenTelemetryReader.cs
Scene2GuideRailOrderCalculator.cs
Scene2GuideStageEvaluator.cs
Scene2GuideLaserCalibrationGate.cs
Scene2GuideCameraVisibility.cs
Scene2GuideGifPopup.cs
IGuideGifPlayer.cs
UnityGifGuidePlayer.cs
IScene2GuideInteractionLock.cs
Scene2GuideInteractionLock.cs
Scene2GuideInputTargetRegistry.cs
```

继续使用现有命名空间 `ElectroOptics.UI.ExperimentGuide`，本期不新增 `.asmdef`。

### 5.3 新增 Editor 测试

```text
Assets/Scripts/UI/ExperimentGuide/Editor/Scene2RealtimeGuideTests.cs
```

菜单入口：

```text
ElectroOptics/Tests/Run Scene2 Realtime Guide Tests
```

### 5.4 第三方和素材目录

```text
Assets/ThirdParty/Unity-GifDecoder/
├─ Runtime source...
└─ LICENSE

Assets/StreamingAssets/ExperimentGuide/
├─ PlaceScreen.gif
├─ CalibrateLaser.gif
├─ VerifyExtinction.gif
├─ ObserveConoscopic.gif
├─ InstallPowerMeterProbe.gif
└─ InstallPhotodiodeProbe.gif
```

GIF 素材本期允许为空目录。第三方源码引入时必须固定具体 commit SHA，并在 `Assets/ThirdParty/Unity-GifDecoder/README_Codex.md` 或项目第三方说明中记录来源、SHA 和 MIT 许可证。依赖来源：[3DI70R/Unity-GifDecoder](https://github.com/3DI70R/Unity-GifDecoder)。

---

## 6. 核心数据契约

### 6.1 固定阶段 ID

```csharp
public enum Scene2GuideStageId
{
    PlaceScreen,
    CalibrateLaser,
    VerifyExtinction,
    ObserveConoscopic,
    InstallPowerMeterProbe,
    InstallPhotodiodeProbe
}
```

枚举值不可按照场景对象是否存在动态增删。所有六阶段定义必须完整存在，缺失场景引用时由状态提供器报告无效。

### 6.2 阶段定义

```csharp
[Serializable]
public sealed class Scene2GuideStageDefinition
{
    public Scene2GuideStageId Id;
    public string Title;
    [TextArea] public string Body;
    public string GifFileName;
}
```

六条定义由 `Scene2CardGuide` 的序列化列表保存，但初始化时必须校验：

- 每个 `Scene2GuideStageId` 恰好出现一次；
- GIF 文件名与 PRD 固定映射一致；
- 标题和正文非空；
- 运行顺序由代码中的只读 StageId 序列决定，不依赖 Inspector 拖动顺序。

校验失败时输出明确错误并禁用自动推进，避免显示阶段与判定阶段错位。

### 6.3 状态快照

```csharp
public readonly struct Scene2GuideStateSnapshot
{
    public readonly double CapturedAt;
    public readonly bool ReferencesValid;
    public readonly bool ScreenTelemetryValid;

    public readonly bool ScreenOnRail;
    public readonly bool PolarizerOnRail;
    public readonly bool AnalyzerOnRail;
    public readonly bool BeamExpanderOnRail;
    public readonly bool CrystalOnRail;
    public readonly bool PowerMeterProbeOnRail;
    public readonly bool PhotodiodeProbeOnRail;

    public readonly float ScreenIntensity;
    public readonly Vector2 ScreenSpotPosition;
    public readonly bool LaserCalibrationCommitted;
    public readonly float PolarizerAngle;
    public readonly float AnalyzerAngle;
    public readonly ScreenMode ScreenMode;

    public readonly Vector3 LaserOrigin;
    public readonly Vector3 LaserDirection;
    public readonly Scene2GuideRailProjection RailProjection;
}
```

实际实现可以拆分子结构，但必须保留以下语义：遥测是否有效、器件身份、在轨状态、实际强度、红点坐标、角度、Conoscopic 模式、校准提交状态和沿激光方向的投影位置。

快照本身不保存稳定计时和亮态基准；这些是引导会话状态，分别由流程控制器和状态提供器中的基准跟踪器维护。

### 6.4 判定结果

```csharp
public readonly struct Scene2GuideStageEvaluation
{
    public readonly bool PreconditionsMet;
    public readonly bool CompletionConditionMet;
    public readonly string StatusMessage;
}
```

`StatusMessage` 只用于必要的状态说明。首期明确需要的动态提示是消光阶段无亮态基准时的“请先将检偏器调至亮态以建立基准”。普通条件未满足不显示错误。

---

## 7. 场景状态提供器

### 7.1 接口职责

```csharp
public interface IScene2GuideStateProvider
{
    bool IsReady { get; }
    Scene2GuideStateSnapshot Capture();
    Scene2GuideStageEvaluation Evaluate(
        Scene2GuideStageId stageId,
        in Scene2GuideStateSnapshot snapshot);
    void ResetSessionState();
}
```

`Capture()` 每帧最多执行一次。UI、控制器和判定器共享同一快照，禁止各自重复查找场景对象或读取不同时间点的数据。

### 7.2 序列化引用

状态提供器需要显式区分并序列化：

- 激光器 Transform、`LaserEmitter`、`LaserEmitterMover`、`LaserStateController`；
- 光屏 `OpticalComponent` 和 `DirectScreenController`；
- 起偏器、检偏器各自的 `OpticalComponent` 与 `RotateStandController`；
- 扩束镜、晶体各自的 `OpticalComponent`；
- `接收器.fbx` 对应的光功率计探头 `OpticalComponent`；
- `光电二极管.fbx` 对应的光电二极管探头 `OpticalComponent`；
- `UnifiedScreenPanel`、主相机和相机控制器。

Inspector 引用是主路径。仅当引用为空时允许按名称或组件类型回退查找；每次回退必须输出一次警告，包含字段名、匹配对象完整层级和匹配规则。出现零个或多个歧义匹配时视为无效，不选择“第一个”。

### 7.3 光屏遥测适配

`DirectScreenController` 当前未公开强度和红点坐标。为遵守“不修改旧业务脚本”，新增 `Scene2GuideScreenTelemetryReader`，在初始化时一次性缓存以下 `FieldInfo`：

```text
currentIntensity
targetCenterX
targetCenterY
receivedLightThisFrame（仅诊断，不作为唯一收光依据）
```

读取规则：

- 强度必须不是 NaN 或 Infinity，且大于 0 才视为有效亮度；
- 红点坐标必须为有限数值；
- `receivedLightThisFrame` 会在 `DirectScreenController.Update()` 末尾清零，受脚本执行顺序影响，因此不能单独用于阶段判定；
- `PlaceScreen` 主要使用“光屏在轨 + 有效实际强度”持续 0.5 秒；
- 反射字段不存在、类型变化或读取异常时，记录一次错误并令 `ScreenTelemetryValid=false`，相关阶段不得完成。

该反射适配集中在单一类中，禁止在 UI 或阶段判定器中散布字段名。后续若旧组件新增正式公开接口，只需替换此适配器内部实现。

### 7.4 光路顺序计算

`Scene2GuideRailOrderCalculator` 是纯逻辑工具。对每个器件计算：

```csharp
direction = (-laserTransform.right).normalized;
projection = Vector3.Dot(component.position - laser.position, direction);
```

顺序条件要求：

```text
p[0] + epsilon < p[1]
p[1] + epsilon < p[2]
...
```

`epsilon` 为序列化容差，建议默认 `0.01m`。所有参与顺序判断的器件必须同时满足 `isOnRail=true`。不得使用世界 X 坐标，也不得仅按与激光器的欧氏距离排序。

若激光方向长度近似为零或任一引用失效，顺序判定失败并输出一次诊断。

### 7.5 偏振角归一化

两偏振器夹角按 180° 周期计算：

```csharp
delta = Mathf.Abs(Mathf.DeltaAngle(angleA, angleB));
delta = Mathf.Min(delta, 180f - delta);
```

- 近似平行：`delta <= 8°`；
- 近似正交：`abs(delta - 90°) <= 8°`。

阈值放在统一配置中，不在多处硬编码。

---

## 8. 六阶段判定实现

### 8.1 通用稳定规则

除激光校准的“就绪后确认”特例外，当前阶段的 `CompletionConditionMet` 必须连续为真 0.5 秒。任一帧为假立即将当前稳定累计清零。

稳定计时使用 `Time.unscaledDeltaTime`。打开 GIF 弹窗时不执行计时更新，关闭后从冻结值继续；不把弹窗持续时间计入 0.5 秒或 0.6 秒。

### 8.2 Stage 1：PlaceScreen

```text
ScreenOnRail
AND ScreenTelemetryValid
AND ScreenIntensity > 0
```

条件连续 0.5 秒后完成。只有纹理存在、屏幕在轨但没有实际有效强度时不得完成。

### 8.3 Stage 2：CalibrateLaser

中心距离：

```csharp
distance = Vector2.Distance(snapshot.ScreenSpotPosition, new Vector2(256f, 256f));
centered = snapshot.ScreenTelemetryValid && distance <= 16f;
```

`centered` 连续 0.5 秒后进入“可确认”状态；此时按 Return 或 KeypadEnter 才提交锁定并完成阶段。范围外 Enter 不提交、不锁定、不显示额外错误。

这里不再对提交后的布尔值追加第二轮 0.5 秒稳定等待。0.5 秒用于建立 `centeredReady`，有效 Enter 直接触发 0.6 秒完成反馈。

### 8.4 Stage 3：VerifyExtinction

前置光路必须严格为：

```text
激光 → 起偏器 → 检偏器 → 光屏
```

亮态基准状态机：

1. 进入本阶段时 `baselineReady=false`、累计时间为 0。
2. 光路正确、遥测有效、实际强度大于 0 且夹角 `<=8°` 时累计亮态稳定时间。
3. 连续 0.5 秒后，以当前实际强度建立基准。
4. 在光路仍正确、遥测有效且夹角 `<=8°` 的有效亮态期间，基准持续取 `max(baseline, currentIntensity)`。
5. 没有基准时，正交和暗强度都不得完成，并显示“请先将检偏器调至亮态以建立基准”。
6. 基准建立后，当夹角处于 `90°±8°` 且 `currentIntensity <= baseline * 0.05`，完成条件为真。
7. 第 6 条仍需连续稳定 0.5 秒。

基准只在本次 Scene2 引导会话、本阶段中有效；重新加载 Scene2 或调用会话重置时清空。阶段推进后不再重算。

### 8.5 Stage 4：ObserveConoscopic

必须同时满足：

```text
激光 → 起偏器 → 扩束镜 → 晶体 → 检偏器 → 光屏
所有上述器件在轨
UnifiedScreenPanel.CurrentMode == ScreenMode.Conoscopic
```

不读取晶体旋转量，不要求进入特写。条件连续 0.5 秒后完成。

### 8.6 Stage 5：InstallPowerMeterProbe

必须同时满足：

- 光屏离轨；
- 扩束镜离轨；
- 光电二极管探头离轨；
- 光功率计探头在轨；
- 光路严格为“激光 → 起偏器 → 晶体 → 检偏器 → 光功率计探头”。

条件连续 0.5 秒后完成。

### 8.7 Stage 6：InstallPhotodiodeProbe

必须同时满足：

- 光屏离轨；
- 扩束镜离轨；
- 光功率计探头离轨；
- 光电二极管探头在轨；
- 光路严格为“激光 → 起偏器 → 晶体 → 检偏器 → 光电二极管探头”。

条件连续 0.5 秒后完成，随后显示 0.6 秒完成反馈并进入永久完成页。

---

## 9. 激光校准输入接管

### 9.1 原因

旧 `LaserEmitterMover.Update()` 在激光被选中时接受任意 Enter。若新指引只是在 UI 层拒绝推进，旧脚本仍会把 `isCalibrationDone` 设为 true 并停止移动，用户将无法继续校准。因此必须在校准阶段接管移动和 Enter，而不是事后回滚。

### 9.2 `Scene2GuideLaserCalibrationGate`

初始化时：

1. 缓存 `LaserEmitterMover`、`LaserStateController` 和激光 Transform；
2. 保存 Scene2 加载后的激光初始位置；
3. 保存旧 mover 原始 `enabled` 状态；
4. 当引导进入 `CalibrateLaser` 且尚未提交时，禁用旧 mover，由 Gate 接管输入。

接管期间：

- 仅当 `LaserStateController.IsSelected` 时读取 Horizontal/Vertical；
- 使用旧 mover 的公开 `moveSpeed`、`moveRange`；
- 复现原来的 XY 移动和相对初始位置 `±moveRange` 限制，Z 保持初始值；
- 仅在控制器告知 `centeredReady=true` 时接受 Return/KeypadEnter；
- 有效确认时设置 `isCalibrationDone=true`，调用 `Deselect()`，向控制器发出一次提交事件；
- 未就绪时的 Enter 完全忽略。

退出校准阶段、组件禁用、销毁或场景卸载时恢复旧 mover 的原始 `enabled` 状态，但不清除已合法提交的 `isCalibrationDone`。如果初始化时旧 mover 本来就是禁用状态，关闭接管后仍保持禁用。

### 9.3 与模态锁的关系

弹窗打开时 Gate 也属于交互锁目标，必须暂停移动和 Enter。关闭后恢复弹窗打开前的 Gate 启用状态。调用 `Input.ResetInputAxes()`，防止关闭瞬间消费弹窗期间按下的 Enter 或方向键。

---

## 10. 流程控制器状态机

`Scene2CardGuide` 重构后只维护以下状态：

```text
Initializing
→ Evaluating(stage 1..6)
→ CompletionFeedback(0.6s)
→ Evaluating(next stage)
→ Finished
```

与主流程正交的状态：

```text
CardPresentation = Expanded | Collapsed
CameraVisibility = Visible | Hidden
ModalState = Closed | Open
```

### 10.1 初始化

1. Scene2 启动后等待现有 `autoStartDelay` 或直到必要依赖已注册。
2. 解析并校验六阶段定义和场景引用。
3. 重置本次会话进度、稳定计时、反馈计时和消光基准。
4. 卡片展示状态固定为 Expanded，不读取 PlayerPrefs 或静态缓存。
5. 从 `PlaceScreen` 开始判定，不因场景中某些后续装置已提前完成而跳过当前阶段。

### 10.2 每帧顺序

```text
处理弹窗 Esc
→ 更新相机可见性
→ 若 Modal Open：停止引导输入、计时和推进，结束本帧
→ 处理 F1/标题栏展开收起
→ 捕获一次状态快照
→ 更新当前阶段的辅助状态（校准就绪、消光基准）
→ 纯判定器评估当前阶段
→ 更新 0.5 秒稳定计时或校准提交
→ 必要时进入 0.6 秒完成反馈
→ 反馈结束后推进到下一固定 StageId
→ 第六阶段反馈结束后进入 Finished
→ 向 View 提交最终展示模型
```

相机隐藏只影响 `View` 可见性，不停止状态捕获和阶段推进。卡片收起只影响布局，不停止判定。只有 GIF 模态打开会冻结稳定计时、反馈计时和阶段推进。

### 10.3 单向推进

控制器只保存当前阶段索引，不扫描并回退已完成阶段。到达某阶段时，如果用户提前构建的状态仍满足完整条件，则正常累计 0.5 秒后自动完成。

### 10.4 完成反馈

进入反馈时锁定本次阶段完成结果，反馈计时期间不因器件变化取消。View 使用青蓝/绿色高亮约 0.6 秒：

- 展开状态：卡片边框和标题区域高亮；
- 收起状态：仅标题长条高亮；
- 反馈结束后更新到下一阶段；
- 不自动展开已收起的卡片。

---

## 11. 相机可见性

### 11.1 判定方案

新增 `Scene2GuideCameraVisibility`，不修改 `CameraSwitch` 和 `ExperimentCameraController`。

在 Scene2 初始化完成、相机仍处于默认全局视角时捕获：

```text
defaultPosition
defaultRotation
```

卡片可见必须同时满足：

- `ExperimentCameraController.IsInCloseUpView == false`；
- 主相机到默认位置的距离 `<= 0.02m`；
- 主相机到默认旋转的夹角 `<= 0.5°`；
- 位姿连续稳定 `>= 0.1s`。

三个数值均为序列化配置。只要相机移动、进入特写或超出容差，卡片立即隐藏；返回默认位姿后必须重新稳定 0.1 秒才显示。该方案覆盖正视、俯视、任意器件特写和相机过渡，不依赖 `CameraSwitch` 私有目标字段。

### 11.2 状态恢复

相机隐藏不修改 `Expanded/Collapsed`。恢复显示时直接使用隐藏前一直保留的展示状态，并渲染隐藏期间已经推进到的最新阶段。

GIF 弹窗打开时，卡片仍处于原逻辑状态，但被全屏遮罩覆盖；弹窗关闭后再按当前相机状态决定是否显示。

---

## 12. 卡片 View 与 UI 层级

### 12.1 场景层级

```text
Scene2CardGuideRoot
└─ GuideCard
   ├─ Header (Button)
   │  ├─ HeaderText
   │  └─ ToggleIcon
   └─ ExpandedContent
      ├─ StageTitle
      ├─ BodyText
      ├─ StatusText
      └─ VideoButton
```

`Scene2CardGuideRoot` 只覆盖卡片需要的点击区域，不再承载全屏目标圈、箭头、连线或模拟光标。

### 12.2 View 序列化引用

`Scene2CardGuideView` 新结构至少包含：

- `CanvasGroup rootGroup`
- `RectTransform cardRect`
- `Button headerButton`
- `RectTransform headerRect`
- `TextMeshProUGUI headerText`
- `RectTransform toggleIcon`
- `CanvasGroup expandedContentGroup`
- `RectTransform expandedContentRect`
- `TextMeshProUGUI stageTitleText`
- `TextMeshProUGUI bodyText`
- `TextMeshProUGUI statusText`
- `Button videoButton`

旧的 close/restart/pointer 引用不再参与运行时逻辑。升级工具应移除或停用旧子对象，不能保留不可见但仍接收射线的遗留控件。

### 12.3 布局和动画

- 左上锚点，参考边距 32px；
- 展开约 `460×240`，收起约 `460×52`；
- 使用项目现有 `CanvasScaler` 的 1920×1080 参考分辨率；
- 中文统一使用 `SIMHEI SDF` 或项目中明确支持 CJK 的 TMP Font Asset；
- Header 整条和三角都通过同一个 Button 触发展开/收起；
- 展开/收起约 0.2 秒，使用 `Time.unscaledDeltaTime`；
- 动画同时插值卡片高度和 ExpandedContent 的 alpha；
- 收起时 ExpandedContent 必须 `blocksRaycasts=false`、`interactable=false`；
- 隐藏时 Root `alpha=0`、`blocksRaycasts=false`、`interactable=false`；
- 动画被反向触发时从当前插值状态继续，不能瞬移或并行启动多个协程。

### 12.4 展示文本

进行中收起标题：

```text
实时操作指引｜阶段 n/6｜当前阶段标题
```

完成后收起标题：

```text
实时操作指引｜已完成
```

完成页隐藏 `VideoButton`，不生成退出、重启、上一步或下一步按钮。

---

## 13. GIF 弹窗

### 13.1 独立实现

新增 `Scene2GuideGifPopup`，首次打开时在 `WindowsCanvas` 下构建或绑定自身 UI。不得直接复用旧 `ExperimentGuidePopup`：旧组件负责四页 PNG 手册且包含翻页输入，职责和生命周期不同。

弹窗建议层级：

```text
Scene2GuideGifModalRoot
├─ Dimmer（全屏 Image，raycastTarget=true，无关闭事件）
└─ ModalPanel
   ├─ TitleText
   ├─ MediaViewport
   │  ├─ RawImage（AspectRatioFitter / FitInParent）
   │  ├─ LoadingIndicator
   │  └─ MessageText
   └─ CloseButton
```

Root 位于 `WindowsCanvas` 最后一个 sibling，并使用足够高的排序层级覆盖 Scene2 其他 UI。遮罩使用约 60%～70% 黑色透明度。`MediaViewport` 完整显示原比例画面，允许留边，不裁切。

### 13.2 打开流程

```text
确认当前为六个教学阶段之一
→ 按 StageId 获取固定文件名
→ 获取交互锁 token
→ 通知 Scene2CardGuide ModalState=Open
→ 显示遮罩与加载状态
→ 使用 StreamingAssets 路径读取字节
→ 文件缺失：显示占位文案
→ 正常：启动解码，从第一帧循环播放
→ 失败：显示文件名和用户可理解的失败信息
```

标题格式：`当前阶段标题｜视频指引`。

缺失素材显示：`该阶段视频指引待补充`。损坏素材显示：`视频加载失败：<文件名>`，完整异常写入 Console。无论加载、缺失或失败，Close 和 Esc 始终有效。

### 13.3 关闭流程

```text
使本次播放 generation 失效
→ 取消读取/解码任务
→ 停止播放计时
→ 清空有界帧队列
→ 销毁运行时 Texture2D
→ 隐藏弹窗
→ 释放交互锁 token
→ 通知 Scene2CardGuide ModalState=Closed
→ Input.ResetInputAxes()
```

`OnDisable`、`OnDestroy`、场景卸载和解码异常都必须进入同一幂等清理路径。

### 13.4 GIF 播放接口

```csharp
public interface IGuideGifPlayer : IDisposable
{
    bool IsLoading { get; }
    bool IsPlaying { get; }
    Texture CurrentTexture { get; }
    event Action<Texture> FrameChanged;
    event Action<string> Failed;

    void Open(Scene2GuideStageId stageId, string streamingAssetPath);
    void Close();
}
```

具体签名可以按第三方库适配，但 UI 只依赖本项目接口，不能直接引用第三方解码类型。

### 13.5 读取、解码和帧缓冲

- 使用 `Application.streamingAssetsPath` 组合路径；
- 通过 `UnityWebRequest` 读取字节，兼容 StreamingAssets 的不同部署形式；
- 第三方解析与 Unity 对象创建分离；工作线程不得创建或修改 `Texture2D`、`RawImage` 等 Unity 对象；
- 解码输出为像素数据和帧延时，放入容量建议为 3 的有界队列；
- 以 960×540 RGBA32 估算，三帧像素缓存约 6MB，禁止无界缓存整段 GIF 的全部纹理；
- 主线程维护一个可复用 `Texture2D` 并上传当前帧像素；
- 使用 `Time.unscaledDeltaTime` 按 GIF 帧延时播放；
- 播放到末尾后从压缩字节重新开始解码或重置解码器，形成无缝循环；
- 每次 `Open()` 增加 generation id，旧任务回调必须检查 id，防止关闭后写回已销毁的 UI；
- 关闭时取消任务、清空队列并 `Destroy()` 运行时纹理。

如果选定版本的解码器内部直接创建 Unity 纹理，适配层必须把这部分调度到主线程并分帧执行，且通过 20MB 上限素材进行卡顿验证；不得在线程池调用 Unity API。

### 13.6 资产映射

使用显式字典或阶段定义映射：

| StageId | 文件名 |
|---------|--------|
| `PlaceScreen` | `PlaceScreen.gif` |
| `CalibrateLaser` | `CalibrateLaser.gif` |
| `VerifyExtinction` | `VerifyExtinction.gif` |
| `ObserveConoscopic` | `ObserveConoscopic.gif` |
| `InstallPowerMeterProbe` | `InstallPowerMeterProbe.gif` |
| `InstallPhotodiodeProbe` | `InstallPhotodiodeProbe.gif` |

禁止通过 `(int)stageId` 拼数组索引或依赖 Inspector 列表顺序。

---

## 14. 模态交互锁

### 14.1 锁定目标

`Scene2GuideInteractionLock` 使用 token/引用计数模型，`Acquire()` 和 `Release()` 幂等。首次获取锁时保存原状态，最后一个 token 释放时恢复。

锁定分三层：

1. **uGUI**：全屏 Dimmer 拦截普通 UI 射线，除 ModalPanel 内控件外不允许点击。
2. **3D 鼠标事件**：保存主相机 `eventMask` 后设为 0，阻止 `OnMouseDown` 等相机事件，释放时精确恢复。
3. **直接输入脚本**：保存并禁用已登记的实验输入 MonoBehaviour，释放时按保存值恢复。

### 14.2 默认登记类型

至少覆盖：

- `LaserStateController`
- `Scene2GuideLaserCalibrationGate`
- `OpticalComponent`
- `RailObjectMover`
- `RotateStandController`
- `ReceiverStateController`
- `PowerReadoutController`
- `CameraSwitch`
- `ExperimentCameraController`
- `ClickAreaFocus`

对于场景中通过 EventTrigger 驱动的虚拟旋钮和按钮，由全屏遮罩拦截。升级工具应扫描已知输入类型并自动填入 registry；运行时发现未登记的已知类型时输出警告。

不要禁用：

- `EventSystem`；
- `LaserEmitter`；
- `DirectScreenController`；
- `UnifiedScreenPanel`；
- 光学传播、渲染和非交互视觉更新组件。

这样关闭按钮、GIF 和背景视觉仍能运行，同时实验状态不会因用户输入改变。

### 14.3 输入边界

- 获取锁和释放锁时调用 `Input.ResetInputAxes()`；
- 弹窗打开时 `Scene2CardGuide` 不响应 F1；
- 弹窗自身只处理 Esc 和 Close；
- 不设置 `Time.timeScale`；
- 锁定期间状态快照可停止捕获或仅作诊断，但阶段稳定计时、反馈计时和推进必须冻结。

### 14.4 异常安全

所有关闭路径最终调用同一个 `ReleaseModalResources()`。该方法可重复调用，并按以下顺序保护恢复：

1. 标记 popup 已关闭，阻止新帧回调；
2. 取消异步任务；
3. 释放播放器资源；
4. 恢复相机事件掩码和脚本启用状态；
5. 恢复引导推进；
6. 清理 token 和缓存。

任何单项恢复失败都要记录错误并继续执行后续恢复，不能因一个组件已销毁而遗留全局交互锁。

---

## 15. Scene2 集成与升级工具

### 15.1 场景对象关系

- `GameManager` 上保留单一 `Scene2CardGuide`；
- `Scene2CardGuideRoot` 上保留单一 `Scene2CardGuideView`；
- 状态提供器、校准 Gate、相机可见性和交互锁可挂在 `GameManager`，引用由 Inspector 固定；
- GIF Popup 可在场景中预建，也可首次打开时在 `WindowsCanvas` 下创建；无论哪种方式只能存在一个实例；
- 原 `Btn_Guide`、`ExperimentGuidePopup` 和四页 PNG 资源不修改。

### 15.2 Editor Builder 行为

新增或改名菜单：

```text
ElectroOptics/Experiment Guide/Upgrade Realtime Guide UI In Scene2
```

工具必须可重复执行：

1. 打开或确认当前为 `Scene2.The Lab`；
2. 查找现有 `Scene2CardGuideRoot` 和 View；
3. 若为旧结构，在原 Root 下创建新层级并绑定新引用；
4. 删除或停用旧 close/restart/pointer 子对象；
5. 给 GameManager 添加缺失的新组件并解析场景引用；
6. 校验六阶段定义和输入锁 registry；
7. 检查全场景只存在一个 controller、一个 view、一个 popup；
8. 标记场景 dirty，由开发者显式保存；
9. 输出升级摘要和所有未解决引用。

工具不能因为发现旧 View 就直接返回，也不能在已有新结构旁再创建第二套 UI。

### 15.3 运行时 Bootstrap

保留现有 Bootstrap 作为兜底，但仅在 Scene2 确实不存在 `Scene2CardGuide` 时创建。运行时构建层级必须与 Editor Builder 一致，并进行相同的六阶段和引用校验。

正式场景验收以场景内可编辑 UI 为主；运行时兜底不应掩盖场景引用错误。

---

## 16. 配置参数

所有阈值集中到 `Scene2CardGuide` 或独立可序列化设置结构，默认值如下：

| 参数 | 默认值 | 用途 |
|------|--------|------|
| `conditionStableSeconds` | `0.5` | 普通阶段完成稳定时间 |
| `completionFeedbackSeconds` | `0.6` | 阶段完成高亮时间 |
| `foldAnimationSeconds` | `0.2` | 展开/收起动画 |
| `calibrationRadiusPixels` | `16` | 红点居中半径 |
| `screenCenterPixels` | `(256,256)` | 512×512 屏幕中心 |
| `parallelToleranceDegrees` | `8` | 建立亮态基准 |
| `orthogonalToleranceDegrees` | `8` | 消光角度范围 |
| `extinctionRatio` | `0.05` | 实际强度/亮态基准上限 |
| `railOrderEpsilonMeters` | `0.01` | 严格光路顺序最小间隔 |
| `defaultPosePositionTolerance` | `0.02` | 默认相机位置容差（m） |
| `defaultPoseAngleTolerance` | `0.5` | 默认相机旋转容差（°） |
| `defaultPoseStableSeconds` | `0.1` | 回位后恢复显示稳定时间 |
| `gifFrameBufferCapacity` | `3` | 有界像素帧队列容量 |

这些默认值来自已确认 PRD或现有场景工程容差。调整时必须同步测试，不允许在判定函数中出现第二份魔法数。

---

## 17. 日志与诊断

统一日志前缀建议为：

```text
[Scene2RealtimeGuide]
```

日志分级：

- `Log`：初始化成功、阶段完成、弹窗打开/关闭；
- `LogWarning`：使用名称回退、GIF 超出推荐规格、输入目标未登记；
- `LogError`：关键引用缺失、光屏遥测字段失效、阶段定义重复/缺失、GIF 解码失败、交互锁恢复异常。

每帧条件不满足不得刷日志。引用和反射错误按实例与错误类型只记录一次。GIF 用户界面显示简短信息，Console 保留文件路径、异常类型和堆栈。

建议在 `Scene2CardGuide` Inspector 增加只读调试区或条件编译日志，显示当前 StageId、稳定累计、相机可见性、模态状态、当前强度、亮态基准和各投影值，正式 UI 不显示这些开发信息。

---

## 18. 测试方案

### 18.1 Editor 纯逻辑测试

沿用项目已有静态菜单测试风格，不引入 CI 或新的测试框架假设。`Scene2RealtimeGuideTests.cs` 至少覆盖：

1. `Scene2GuideRailOrderCalculator`
   - 正确顺序；
   - 逆序；
   - 间距小于 epsilon；
   - 激光方向旋转后仍正确；
   - 不依赖世界 X；
   - 引用或方向无效。
2. 偏振角归一化
   - 0/180/360 周期；
   - 平行边界 8°；
   - 正交边界 82°、98°；
   - 边界外失败。
3. 六阶段判定
   - 每阶段完整成功和逐项失败；
   - 错误顺序；
   - 必须离轨的器件仍在轨；
   - Conoscopic 模式缺失；
   - 提前完成后续状态；
   - 已完成阶段不回退。
4. 稳定计时
   - 恰好 0.5 秒；
   - 中途一帧失败归零；
   - 模态暂停不累计；
   - 反馈 0.6 秒后推进。
5. 消光基准
   - 正常建立基准；
   - 进入时已暗且无基准；
   - 基准取有效亮态最大值；
   - 5% 边界通过；
   - 高于 5% 失败；
   - 角度正确但顺序错误失败。
6. 激光校准
   - 半径 16 像素边界；
   - 未稳定时 Enter 无效；
   - 稳定后 Enter 提交；
   - KeypadEnter 等价；
   - 模态期间不提交。

纯逻辑测试使用人工构造快照，不加载 Scene2，不依赖物理帧顺序。

### 18.2 Scene2 Play Mode 手工测试

按 PRD 完整执行：

- 默认展开、整栏点击、三角、F1、收起后继续推进；
- 0.6 秒反馈、完成页、无退出/重启/视频按钮；
- 默认/正视/俯视/特写/过渡中隐藏和回位恢复；
- 校准范围外 Enter 不锁定，范围内稳定后可锁定；
- 六条严格光路顺序；
- 无亮态基准的消光提示；
- 弹窗期间鼠标、WASD、A/D、Space、Enter、相机切换和 F1 均无效；
- Close/Esc 精确恢复；遮罩点击不关闭；
- GIF 缺失、损坏、正常、循环、重开第一帧、资源释放；
- 1366×768、1600×900、1920×1080、2560×1440 四种分辨率；
- 旧四页 PNG 手册、光屏显示、偏振器窗口和实验操作回归。

### 18.3 验收映射

| PRD 验收项 | 技术验证 |
|------------|----------|
| `ST-01`～`ST-02` | PlaceScreen 纯逻辑测试 + Scene2 实际收光测试 |
| `ST-03`～`ST-04` | CalibrationGate 输入测试 + 16px 边界手工测试 |
| `ST-05`～`ST-07` | 消光基准、角度和 5% 阈值测试 |
| `ST-08`～`ST-13` | 旋转传播方向下的严格顺序测试 + Scene2 六阶段通关 |
| `ST-14`～`ST-15` | 提前完成、单向推进状态机测试 |
| `UI-01`～`UI-03` | View 默认状态、动画和收起反馈测试 |
| `UI-04`～`UI-06` | 相机位姿容差与稳定时间测试 |
| `UI-07` | Finished 展示模型测试 |
| `UI-08` | 四分辨率人工截图验收 |
| `GIF-01`～`GIF-04` | 播放器缺失/正常/重开/损坏测试 |
| `GIF-05`～`GIF-08` | 交互锁、关闭路径、场景卸载和清理测试 |
| `NF-01`～`NF-08` | Unity 版本、无 asmdef、资源/字体/回归检查清单 |

---

## 19. 实施顺序

本节仅规定依赖顺序，不作工时估算。

### Phase 1：纯数据与判定核心

- 新增 StageId、阶段定义、快照、判定结果；
- 实现角度工具、光路投影和六阶段纯判定；
- 实现稳定计时与单向推进状态机；
- 先完成对应 Editor 菜单测试。

### Phase 2：场景状态适配

- 新增状态提供器和序列化引用；
- 实现名称回退与一次性诊断；
- 实现光屏遥测适配和消光基准跟踪；
- 在 Scene2 中验证六阶段快照数据。

### Phase 3：激光校准与相机可见性

- 实现 CalibrationGate，接管旧 mover 的移动和 Enter；
- 验证非法 Enter 不会锁死激光；
- 捕获默认相机位姿并实现隐藏/恢复判定。

### Phase 4：卡片 UI

- 重构 controller/view；
- 构建展开/收起、F1、完成反馈和完成页；
- 删除目标圈、箭头、连线和模拟光标运行逻辑；
- 完成 Editor 升级工具和运行时 fallback 同步。

### Phase 5：模态弹窗与交互锁

- 先使用缺失素材占位完成弹窗；
- 实现三层交互锁和异常安全释放；
- 完成鼠标、键盘、相机、F1 的锁定矩阵。

### Phase 6：GIF 解码集成

- 固定第三方 commit 并保留 LICENSE；
- 实现 `IGuideGifPlayer` 适配、有界缓冲和生命周期；
- 验证缺失、损坏、循环、重开第一帧和场景卸载。

### Phase 7：全量回归

- 执行 Editor 菜单测试；
- 完成四分辨率 Scene2 Play Mode 验收；
- 验证旧 PNG 手册和现有实验功能不受影响；
- 保存 Scene2，并检查无重复 UI、Missing Script 或遗留锁。

---

## 20. 风险与处理

| 风险 | 影响 | 处理方式 |
|------|------|----------|
| `DirectScreenController` 私有字段更名 | 光屏、校准、消光无法判定 | 单点反射适配、启动校验、失败关闭；后续可替换为正式公开接口 |
| 旧 mover 与新校准输入同时运行 | 范围外 Enter 仍可能锁定 | 校准阶段禁用旧 mover，由 Gate 独占输入 |
| 相机默认位姿捕获过早 | 默认视角识别错误 | 在 Scene2 初始化完成且相机稳定后捕获；Inspector 允许重新指定参考 Transform |
| 仅用 UI 遮罩无法阻止 3D 输入 | 弹窗期间实验状态变化 | 相机 eventMask + 输入脚本 registry + 遮罩三层锁 |
| GIF 解码造成主线程卡顿 | 弹窗无响应 | 异步读取、像素级有界缓冲、主线程只上传当前帧、素材规格限制 |
| 异步回调写入已销毁 UI | 切场景报错或泄漏 | cancellation + generation id + 幂等 Close |
| Builder 生成重复 UI | 显示和引用冲突 | 原位升级、唯一性检查、工具可重复执行 |
| 名称回退误匹配两个探头 | 阶段判定错误 | 歧义即失败，要求 Inspector 显式绑定，不选第一个 |
| 模态恢复时启用了原本禁用组件 | 改变场景行为 | 逐组件保存原 `enabled`，最后 token 释放时精确恢复 |
| 20MB GIF 内存峰值过高 | GC 或掉帧 | 压缩字节 + 三帧像素队列 + 单一复用纹理，不缓存全段纹理 |

---

## 21. 完成定义

开发完成必须同时满足：

- 六个固定 StageId、正文、GIF 文件映射与 PRD 一致；
- 所有阶段按真实激光传播方向验证严格光路顺序；
- 校准范围外 Enter 不锁定，范围内稳定 0.5 秒后 Enter 才提交；
- 消光必须先建立真实亮态基准，再满足角度和 5% 强度阈值；
- 卡片默认展开、可整栏/F1 收起展开，收起后继续判定且不自动展开；
- 非默认视角及过渡期间隐藏，回到默认稳定位置后恢复原展示状态和最新阶段；
- 完成反馈、完成页和六阶段单向推进符合 PRD；
- GIF 弹窗具备加载、缺失、错误、循环、第一帧重开和完整释放行为；
- 弹窗期间实验鼠标、键盘、相机与指引推进均被锁定，关闭后精确恢复；
- 旧四页 PNG 手册入口和所有现有实验操作通过回归；
- Editor 升级工具可重复执行，Scene2 中无重复新旧卡片、Missing Script 或未绑定关键引用；
- Editor 菜单测试全部通过，并完成四种目标 16:9 分辨率的人工验收；
- 第三方依赖固定 commit、保留 LICENSE，Console 无持续报错，场景卸载后无残留交互锁、解码任务或运行时纹理。

