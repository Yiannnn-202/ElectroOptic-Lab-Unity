# 锥光干涉图 Jones 单轴光强曲面开发日志

## 文档信息

| 项目 | 内容 |
|------|------|
| 日期 | 2026-05-13 |
| 分支 | `codex/conoscopic-additional-experiment` |
| 关联 PRD | `Docs/PRD/PRD_Conoscopic_IntensitySurface_CalcCore.md` |
| 关联计划 | `Docs/Plan/Conoscopic_Jones_IntensitySurface_GPU_Development_Plan.md` |
| 当前状态 | 单轴晶体 Jones 核心计算到可视化链路已完成验证 |
| 后续状态 | 双轴晶体核心计算与可视化仍需检查、修改和验收 |

---

## 1. 背景与算法变更

本阶段最初从“复刻当前 `ConoscopicInterference.shader` 的光强网格”开始，旧方案重点是让三维曲面和 Scene2 二维锥光图保持一致。开发和可视化验证后发现，该经验模型更适合作为教学显示复刻，不足以支撑参考图中基于实验参数调节的三维光强曲面。

因此本版本新增并行的 Jones 物理计算链路，算法核心已发生变化：

- 旧核心：`ConoscopicIntensityCore` + `ConoscopicIntensityCalculator`
  - 目标是 CPU 复刻现有 shader 的 Fresnel/消光/显示 shaping 逻辑。
  - 输出 `float[] Intensities` 规则网格。
  - 适合和旧二维图样对照。
- 新核心：`ConoscopicJonesGpuCore` + `ConoscopicJonesIntensity.shader`
  - 目标是用实验参数驱动单轴晶体 Jones 偏振传播。
  - 输出 GPU `RenderTexture` 高度纹理。
  - 适合作为新附加实验的物理曲面基础。

本次没有删除旧核心。两个模式并行保留，后续可在调度层按实验目标选择。

---

## 2. 单轴 Jones 计算链路

### 2.1 参数输入

单轴 Jones 链路使用 `ConoscopicJonesParameters` 作为参数副本。主要参数包括：

| 参数 | 说明 |
|------|------|
| `wavelengthNm` | 激光波长 |
| `thicknessMm` | 晶体厚度/有效光程厚度 |
| `ordinaryIndexNo` / `extraordinaryIndexNe` | 单轴晶体 o/e 折射率 |
| `screenDistanceM` | 晶体到观察屏距离 |
| `screenHalfSizeM` | 观察屏半宽，用于控制锥光视场尺度 |
| `initialIntensity` | 入射光强 |
| `phaseAntiAliasStrength` | 相位抗混叠强度 |
| `polarizerAngleDeg` / `analyzerAngleDeg` | 起偏器/检偏器角度，默认正交偏振 |
| `crystalAxisAngleDeg` | 晶体横向参考轴角度，用于控制消光结构方向 |
| `opticAxisTiltDeg` / `opticAxisAzimuthDeg` | 光轴相对观察方向的倾角和方位角，默认光轴沿 `z` |
| `electricFieldStrength` / `electroOpticCoefficientR22` | 电光效应接口预留，当前未实现真实扰动 |

### 2.2 GPU 计算流程

运行时主链路：

```text
CrystalProfile / 手动参数
  -> ConoscopicJonesParameters
  -> ConoscopicJonesGpuCore.UploadParameters()
  -> ConoscopicJonesIntensity.shader
  -> RenderTexture intensityHeightMap
  -> ConoscopicJonesSurfaceVisualizer 读回并生成测试曲面
```

Shader 每像素计算：

1. `uv` 映射到归一化观察坐标 `p = uv * 2 - 1`。
2. 圆孔外输出 `0`。
3. 使用实验尺度映射光线：

```text
screenPoint = (p.x * screenHalfSizeM, p.y * screenHalfSizeM)
rayDir = normalize(screenPoint.x, screenPoint.y, screenDistanceM)
```

4. 默认光轴沿观察方向：

```text
opticAxis = (0, 0, 1)  // opticAxisTiltDeg = 0
```

5. 计算单轴晶体有效 e 光折射率 `nEffective`。
6. 计算相位差：

```text
delta = 2*pi*thickness*(nEffective - no)*pathFactor / wavelength
```

7. 通过 Jones 投影计算正交偏振下的输出光强。
8. 使用 `fwidth(delta)` 做显示向相位抗混叠，避免 20mm 晶体下高频相位欠采样形成碎纹。
9. 输出 `[0, 1]` 的归一化强度纹理。

### 2.3 CPU 参考链路

`ConoscopicJonesCpuReference` 提供同源 CPU 单点计算，用于测试：

- `EvaluateIntensity()`：单点 Jones 光强。
- `EvaluateDelta()`：单点相位差。
- `EvaluateIntensityWithFiniteDifference()`：使用相邻像素有限差分近似 GPU `fwidth(delta)`，用于 GPU/CPU 抽样对照。
- `GetPixelCenterCoordinate()`：匹配 GPU 像素中心采样，避免 CPU 按网格顶点、GPU 按像素中心导致对照误差。

---

## 3. 可视化链路

### 3.1 测试场景

新增测试场景：

```text
ElectroOptic-Lab/Assets/Scenes/ConoscopicJonesIntensitySurface_Test.unity
```

对应 Editor 菜单：

```text
ElectroOptics/Tests/Create Conoscopic Jones Visualization Scene
ElectroOptics/Tests/Repair Conoscopic Jones Visualization Scene
```

### 3.2 可视化组件

`ConoscopicJonesSurfaceVisualizer` 负责临时测试显示：

- 监听 `ConoscopicJonesGpuCore.OnJonesIntensityUpdated`。
- 从 `IntensityHeightMap` 读回强度。
- 根据强度生成 Mesh 顶点高度。
- 使用蓝-青-绿-黄-红伪彩色顶点色显示曲面。
- 运行时 IMGUI 面板提供调参入口。

当前可视化仅用于验证算法，不代表最终 UI 方案。

### 3.3 验证后的关键修正

开发过程中发现并修复了三个影响图样正确性的关键问题：

1. GPU/CPU 对照坐标不一致  
   CPU 原先按网格顶点坐标对照，GPU 按像素中心采样。已新增 `GetPixelCenterCoordinate()`。

2. 视场尺度过大  
   原先直接把归一化 `[-1, 1]` 当作横向物理长度，导致视场过大、相位过高频。已新增 `screenHalfSizeM`。

3. 光轴几何语义错误  
   原先 `crystalAxisAngleDeg` 被用作屏幕平面内光轴方向，破坏了单轴锥光图样的径向对称。已将光轴改为独立的 `opticAxisTiltDeg` / `opticAxisAzimuthDeg`，默认光轴沿观察方向 `z`；`crystalAxisAngleDeg` 只保留为横向参考轴，影响消光结构方向。

---

## 4. 测试过程

### 4.1 菜单测试

新增菜单测试：

```text
ElectroOptics/Tests/Run Conoscopic Jones Core Tests
```

覆盖项包括：

- 参数 clamp：分辨率、波长、厚度、折射率、屏距、屏幕半宽、抗混叠、孔径、高度等。
- CPU 参考输出范围：中心、四象限、圆孔外。
- Profile 默认值映射：LiNbO3 的波长、厚度、折射率。
- GPU Core 生命周期：RenderTexture 创建、事件触发、结果有效、强度范围。
- GPU/CPU 抽样对照：使用像素中心坐标和有限差分参考。
- `screenHalfSizeM` 改变后的 dirty 重算。
- `phaseAntiAliasStrength` 原始/平滑两种状态输出有限。

用户侧已反馈：单轴晶体从核心计算到可视化链路已完成验证。

### 4.2 可视化验收

通过 Jones 可视化场景验证：

- LiNbO3 默认参数可输出有效三维曲面。
- 修正光轴几何后，图样从非预期的四瓣拉伸形态转向单轴锥光应有的黑十字与同心圆趋势。
- 调整波长、厚度、折射率差、屏幕半宽和抗混叠参数时，图样密度和显示平滑度可观察变化。

---

## 5. 单轴与双轴的差异

### 5.1 单轴当前链路

单轴晶体只需要一个光轴：

```text
opticAxis
rayDir 与 opticAxis 的夹角 -> nEffective
Jones o/e 两个本征方向 -> phase delta -> intensity
```

默认沿光轴观察时，相位主要随半径变化，因此自然形成同心环；正交偏振下叠加黑十字/消光刷。

### 5.2 双轴后续链路

双轴晶体不能继续使用单个 `opticAxis` 与 `no/ne` 模型。KTP 等双轴晶体至少需要：

- 三个主折射率 `nx/ny/nz`。
- 两条光轴或等价的双轴 Fresnel 本征传播求解。
- 每条光线方向下的两个本征折射率和本征偏振方向。
- 双 melatope/双消光结构的可视化验收。

双轴核心和可视化预计差异：

| 项目 | 单轴 | 双轴 |
|------|------|------|
| 折射率输入 | `no/ne` | `nx/ny/nz` |
| 光轴 | 1 条 | 2 条或从 Fresnel 椭球推导 |
| 相位差 | `nEffective - no` | 两个本征解 `n1 - n2` |
| 消光结构 | 单黑十字/单 melatope | 双 melatope/双消光刷 |
| 可视化验收 | 黑十字 + 同心圆 | 双轴特征图样，需要和 KTP 现有二维显示对照 |
| 风险 | 参数尺度和抗混叠 | 本征方向连续性、双轴奇点、图样翻转和对称性 |

### 5.3 后续待办

下一阶段需要专门检查、修改并验证：

1. 双轴晶体核心计算。
2. 双轴 GPU Shader 或 CPU/GPU 混合参考算法。
3. KTP 参数映射与双轴工作几何。
4. 双 melatope 可视化是否与当前 `ConoscopicInterference.shader` 的教学图样一致。
5. 双轴测试场景中的参数面板和验收截图。

---

## 6. 当前版本说明

本版本提交目标：

- 记录单轴 Jones 计算链路、可视化链路、测试过程和关键修正。
- 明确算法核心已从旧 shader-equivalent 复刻扩展为新的 Jones 物理计算链路。
- 明确双轴晶体尚未完成最终检查与修改。
- 将当前单轴验证版本推送到远程分支，便于后续继续双轴开发。
