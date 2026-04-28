# Scene2 锥光干涉图角度与 FOV 修复开发归档

## 基本信息

- **分支**: `codex/fix-scene2-conoscopic-angle-reset`
- **PR**: https://github.com/Yiannnn-202/ElectroOptic-Lab-Unity/pull/13
- **日期**: 2026-04-28
- **相关提交**:
  - `006384c` Fix scene2 conoscopic rotation reset
  - `a6f4fbd` Align scene2 conoscopic optics inputs
  - `2a443fa` Fix conoscopic FOV render framing

## 问题背景

Scene2 中锥光干涉图存在三类问题：

1. 调整晶体角度后，锥光干涉图变化过大，且难以恢复到初始状态。
2. 光屏面板的锥光图输入引用与场景实际光学元件不完全一致，扩束镜与晶体引用存在混用风险。
3. 增大 `conoscopicFOV` 后，光屏面板周围出现大面积黑屏，表现像 RenderTexture 没有正常渲染。

进一步分析后确认：当前锥光图算法仍是近似可视化模型，不是完整实验光路模型。它主要通过折射率、相位差和简化黑十字生成干涉图，没有完整模拟起偏器、检偏器、物镜 NA、晶体表面折射、Jones 矩阵和多波长色散。因此本轮修复优先解决工程层面的角度复位、场景引用和 FOV 黑屏问题。

## 关键发现

### 角度复位问题

`CrystalControllerWrapper` 之前会从晶体模型 `localEulerAngles` 推导初始物理旋转。Scene2 中晶体模型本身存在摆放旋转，这会把模型姿态误当成物理角度初值，导致调节后无法回到预期的锥光图初始状态。

修复后，物理旋转初值固定为 `Vector2.zero`，模型摆放只作为场景表现，不再污染锥光计算中的晶体角度。

### 光路方向与元件引用问题

Scene2 中锥光图应基于实际激光传播方向，而不是固定世界方向。修复后由 `CrystalComponentInitializer` 查找或绑定 `LaserEmitter`，并将其作为光线方向源传给 `CrystalControllerWrapper`。

同时修正了 `ScreenDisplayPanel` 的场景引用：

- `beamExpanderOpticalComponent` 指向扩束镜对应的 `OpticalComponent`
- `crystalOpticalComponent` 指向晶体盒对应的 `OpticalComponent`

这样 `UnifiedScreenPanel` 判断锥光模式时，依赖的是实际应该上导轨的扩束镜、晶体和光屏。

### FOV 黑屏问题

`ConoscopicTextureRenderer` 原本把同一个 `_fov` 同时用于两处：

- 离屏预览相机的 `Camera.fieldOfView`
- shader 中锥光角度采样的 `_FOV`

当 FOV 增大时，相机视野变宽，但前方用于渲染 shader 的 Quad 仍是 1x1，导致 RenderTexture 里大部分区域只拍到黑色背景。这个黑色背景被 RawImage 显示到光屏面板上，就表现为“周围黑屏”或“无渲染”。

修复后，预览相机固定为正交相机，只负责把 Quad 填满 RenderTexture；锥光 FOV 只传给 shader，专门控制物理采样角度。

## 实现内容

### `CrystalControllerWrapper`

- 初始物理旋转改为 `Vector2.zero`。
- 新增光线方向来源 `Transform`。
- 新增 `SetLightDirectionSource(Transform)`。
- 通过实际激光方向更新 `CrystalConfig`。
- 在 `Update()` 中检测光线方向变化，必要时重新应用物理配置。

### `CrystalComponentInitializer`

- 新增 `lightDirectionSource` 配置项，留空时自动查找场景中的 `LaserEmitter`。
- 初始化控制器时绑定光线方向源。
- `conoscopicFOV` Inspector 范围从 `1..60` 扩展为 `1..120`。
- 新增 `conoscopicPhaseScale`，范围 `0.1..5`，用于在不继续扩大 FOV 的情况下增加同心环数量。

### `ConoscopicTextureRenderer`

- 离屏预览相机改为固定正交相机：
  - `orthographic = true`
  - `orthographicSize = 0.5`
- 新增隐藏的 `ConoscopicPreviewRoot`，使预览相机和 Quad 脱离晶体父物体的旋转与缩放影响。
- `SetFOV(float fov)` 不再修改相机 `fieldOfView`，只更新 shader `_FOV`。
- FOV clamp 上限扩展到 `120`。
- 新增 `SetPhaseScale(float phaseScale)`。
- 移除每帧 `_BaseColor`、`LaserColor` 等调试日志，避免 Play Mode Console 刷屏。
- FOV 超过 `90` 时仅打印一次 warning，提示边缘畸变会更明显。

### `ConoscopicInterference.shader`

- 新增 `_PhaseScale` 属性。
- 相位差计算从：

```hlsl
float gamma = (2.0 * PI * pathLength * delta_n) / _Wavelength;
```

改为：

```hlsl
float gamma = ((2.0 * PI * pathLength * delta_n) / _Wavelength) * _PhaseScale;
```

这允许通过相位缩放增加环数，避免单纯依赖超大 FOV。

### Scene2 参数

当前 Scene2 归档参数：

- `panelSize`: `{x: 300, y: 300}`
- `panelPosition`: `{x: 280, y: 180}`
- `renderTextureSize`: `1024`
- `conoscopicFOV`: `15`
- `conoscopicPhaseScale`: `5`
- `lightDirectionSource`: 绑定到场景中的激光发射器

## 使用建议

- 需要扩大可见锥光范围时，优先调 `conoscopicFOV`，建议测试 `15 / 30 / 60 / 90 / 120`。
- 需要看到更多黑色同心环时，优先调 `conoscopicPhaseScale`，建议 `1..5` 之间测试。
- FOV 超过 `90` 后，边缘角度采样会明显变形，这属于超广角采样的可视化副作用，不代表真实实验光路已被严格模拟。
- 若目标是更接近真实实验图，后续应引入完整偏振光路模型，包括起偏器、检偏器、Jones 矩阵、物镜 NA、晶体内折射方向和有效光程。

## 验证记录

- 已执行 `git diff --check`，未发现空白错误。
- 已确认当前分支推送到远程：`origin/codex/fix-scene2-conoscopic-angle-reset`。
- 已创建 PR：`#13 Fix scene2 conoscopic FOV rendering`。
- 当前环境中 `Unity`、`dotnet`、`msbuild` 不在 PATH，未能执行 Unity 编译或运行时验证。

## 后续待验证

- 在 Unity Play Mode 中测试 FOV 为 `15 / 30 / 60 / 90 / 120` 时，光屏面板是否始终被锥光图填满。
- 检查 `conoscopicPhaseScale = 5` 时环数是否满足实验教学展示需求。
- 晶体、扩束镜、光屏都在导轨上时，`UnifiedScreenPanel` 是否稳定切换到 `Conoscopic` 模式。
- 调整晶体角度后，确认锥光图变化连续，且回到零角度时图案可恢复。
- 晶体移出导轨时，确认面板正常切回 Direct 红点模式。
