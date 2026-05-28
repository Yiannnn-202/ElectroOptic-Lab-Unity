# Architecture: 附加实验 Jones 锥光三模式技术设计

## 目的

本文档把测试场景中的 Jones 锥光计算链路整理为实际附加实验场景可接入的技术方案。实际场景需要提供三种模式：

1. 单轴模式：走测试场景中的单轴计算链路。
2. 双轴模式：走测试场景中的双轴 Raw Jones 计算链路，并支持电压调节。
3. 单轴电压调整模式：走测试场景中的连续 EO 计算链路。

## 参考组件

| 组件 | 职责 |
| --- | --- |
| `ConoscopicJonesParameters` | 保存计算参数、模式标志、Clamp 规则和物理量。 |
| `ConoscopicJonesGpuCore` | 计算入口。管理 profile、应用物理配置、上传 shader 参数、输出 `IntensityHeightMap`。 |
| `CrystalPhysicalCore` | 现有电光物理核心。根据电场输出扰动后的主折射率和 `ShaderWorldToPrincipalMatrix`。 |
| `ConoscopicJonesIntensity.shader` | GPU Jones/Fresnel 强度计算。包含单轴 fallback、双轴本征分支和连续 EO 行为。 |
| `ConoscopicJonesSurfaceVisualizer` | 测试场景调试面板和 3D mesh readback，可参考但不建议直接作为正式 UI。 |
| `ConoscopicJonesCpuReference` | CPU reference，用于测试和抽样对照。 |

## 推荐接入形态

正式场景应新增 facade，避免 UI 直接操作 `ConoscopicJonesParameters`。

```csharp
public enum AdditionalConoscopicMode
{
    Uniaxial,
    BiaxialVoltage,
    UniaxialVoltage
}

public sealed class AdditionalConoscopicExperimentApi : MonoBehaviour
{
    public ConoscopicJonesGpuCore Core { get; }
    public ConoscopicJonesResult Result => Core.Result;
    public RenderTexture IntensityTexture => Core.IntensityHeightMap;

    public void SetMode(AdditionalConoscopicMode mode);
    public void ApplyUserParameters(AdditionalConoscopicUserParameters userParameters);
    public void Recalculate();
}
```

用户参数结构建议：

```csharp
public struct AdditionalConoscopicUserParameters
{
    public float wavelengthNm;
    public float thicknessMm;
    public float voltageV;
    public float crystalAxisAngleDeg;
    public float polarizerAngleDeg;
    public float analyzerAngleDeg;
    public float opticAxisTiltDeg;
    public float opticAxisAzimuthDeg;
    public float thetaDeg;
    public float phiDeg;
    public float apertureRadius;
}
```

电压换算必须由 facade 或 mode preset 负责：

```csharp
electricFieldStrengthVm = VoltageToField(voltageV);
```

待确认的物理换算通常有两种：

```text
E(V/m) = voltage(V) / electrodeGap(m)
```

或：

```text
E(V/m) = voltage(V) * fieldVmPerVolt
```

如果实际场景不建模电极间距，建议把 `fieldVmPerVolt` 做成明确命名的 preset 字段，并在实验说明中写清楚这是场景标定比例。

## 共享核心流程

三种模式最终都走同一条核心调用链：

```text
UI 参数
  -> AdditionalConoscopicExperimentApi
  -> 克隆 ConoscopicJonesParameters
  -> 应用模式 preset
  -> 应用用户物理参数
  -> core.SetProfile(profile)
  -> core.SetParameters(parameters)
  -> core.ForceRecalculate()
  -> ConoscopicJonesGpuCore.ApplyPhysicalConfig()
  -> ConoscopicJonesGpuCore.UploadParameters()
  -> ConoscopicJonesIntensity.shader pass 0
  -> core.Result + core.IntensityHeightMap
  -> 2D/3D 显示层
```

`ForceRecalculate()` 内部关键步骤：

```text
EnsureDefaults()
ApplyPhysicalConfig()
EnsureMaterial()
EnsureRenderTexture()
UploadParameters()
Graphics.Blit(...)
EstimateMinMaxFromReadback()
Result.SetValid(...)
OnJonesIntensityUpdated
```

## 模式 1：单轴计算链路

### 意图

M1 对应测试场景的普通单轴行为，是无电压基线模式。

### 必要参数状态

| 字段 | 必要值 |
| --- | --- |
| Profile | 单轴 profile，默认 LiNbO3 |
| `electricFieldStrength` | `0` |
| `uniaxialEoView` | `false` |
| `uniaxialEoUsePerturbedAxis` | `false` |
| `forceUniaxial` | `false`，除非明确要强制分类 |
| `biaxialDisplayMode` | `RawJones` 或 `ConoscopicTeaching` 均可；主折射率退化时都会落入单轴分支 |
| `principalIndexNx/Ny/Nz` | 从 profile 加载 |
| `ordinaryIndexNo/extraordinaryIndexNe` | 从 profile 推导 |

### 计算链路

```text
SetProfile(LiNbO3)
  -> parameters.ApplyProfileDefaults(profile)
  -> nx, ny, nz 呈单轴近简并
  -> ordinary/extraordinary indices 被推导

SetParameters(...)
ForceRecalculate()
  -> ApplyPhysicalConfig()
     -> CrystalPhysicalCore.ApplyConfig(...)
     -> NewPrincipalIndices 回写到 parameters
     -> worldToPrincipalMatrix 回写到 parameters
  -> UploadParameters()
     -> _UseBiaxial = parameters.IsBiaxial() || uniaxialEoView = false
     -> _ContinuousEigenMode = 0
     -> _CrystalAxisAngleRad 被上传，但主要作为 fallback 参考方向
  -> shader
     -> TryEvaluateBiaxialTeaching 返回 false
     -> TryGetBiaxialEigenSystem 返回 false
     -> 执行单轴 fallback 分支
```

单轴 shader 分支主要使用：

```text
opticAxis = OpticAxis(_OpticAxisTiltRad, _OpticAxisAzimuthRad)
eDirection = opticAxis 投影到波前
oDirection = cross(rayDir, eDirection)
cosTheta = dot(rayDir, opticAxis)
nEffective = EffectiveExtraordinaryIndex(no, ne, cosTheta)
delta = 2*pi*thickness*(nEffective - no)*pathFactor/wavelength*phaseScale
intensity = 偏振片/检偏器 Jones 投影
```

### 参数暴露建议

默认暴露：

| UI | 内部字段 | 说明 |
| --- | --- | --- |
| 波长 | `wavelengthNm` | 物理参数。 |
| 厚度 | `thicknessMm` | 物理参数。 |
| 起偏器角度 | `polarizerAngleDeg` | 物理参数。 |
| 检偏器角度 | `analyzerAngleDeg` | 物理参数。 |
| 光轴倾角 | `opticAxisTiltDeg` | 单轴图像响应的关键取向参数。 |
| 光轴方位角 | `opticAxisAzimuthDeg` | 高级参数。 |
| 通光孔径 | `apertureRadius` | 高级参数。 |

默认隐藏：

| 字段 | 原因 |
| --- | --- |
| `crystalAxisAngleDeg` / alpha | 单轴 fallback 中不是主计算变量，调节后可能无明显图像变化。 |
| `electricFieldStrength` | M1 是无电压模式。 |
| 折射率字段 | 材料数据，不是普通控件。 |

## 模式 2：双轴电压计算链路

### 意图

M2 是支持电压调节的双轴晶体模式，默认建议使用 KTP。该模式必须让电压进入 `CrystalPhysicalCore`，再由物理核心扰动主折射率和主轴矩阵。

### 必要参数状态

| 字段 | 必要值 |
| --- | --- |
| Profile | 双轴 profile，默认 KTP |
| `biaxialDisplayMode` | `RawJones` |
| `uniaxialEoView` | `false` |
| `uniaxialEoUsePerturbedAxis` | `false` |
| `forceUniaxial` | `false` |
| `electricFieldStrength` | 由 UI 电压换算 |
| `worldToPrincipalMatrix` | 由 `CrystalPhysicalCore` 输出，必要时由 facade 组合用户取向 |

### 为什么不能用 `PaperKtp1`

`ConoscopicJonesGpuCore.ApplyPhysicalConfig()` 对 KTP Paper 模式有特殊早返回：

```text
if !uniaxialEoView
   && biaxialDisplayMode == PaperKtp1
   && profile is KTP:
       使用 profile 折射率
       创建 Paper KTP 矩阵
       return
```

这个分支不会调用 `CrystalPhysicalCore`，因此不会处理电压扰动。它适合论文图样参考 preset，不适合作为 M2 的电压运行链路。

### 计算链路

```text
SetProfile(KTP)
  -> parameters.ApplyProfileDefaults(profile)
  -> profile 加载 nx, ny, nz

应用 M2 preset
  -> biaxialDisplayMode = RawJones
  -> uniaxialEoView = false
  -> forceUniaxial = false
  -> electricFieldStrength = VoltageToField(voltageV)

ForceRecalculate()
  -> ApplyPhysicalConfig()
     -> EnsurePhysicalCore()
     -> config.crystalRotation = ResolveCrystalRotation()
        - crystalAxisAngleDeg 形成面内旋转
        - opticAxisTiltDeg/opticAxisAzimuthDeg 可形成额外倾角
     -> config.localEField = Vector3.forward * electricFieldStrength
     -> _physicalCore.ApplyConfig(config)
     -> parameters.ApplyPrincipalIndices(_physicalCore.NewPrincipalIndices)
     -> parameters.worldToPrincipalMatrix = _physicalCore.ShaderWorldToPrincipalMatrix
  -> UploadParameters()
     -> _UseBiaxial = true，前提是主折射率非退化
     -> _ContinuousEigenMode = 0
     -> 上传 _WorldToPrincipalMatrix
     -> 上传 _PrincipalIndices
  -> shader
     -> TryGetBiaxialEigenSystem 执行
     -> SolveBiaxialFresnel 求 n1/n2
     -> 本征方向转换回 view space
     -> Jones 投影输出强度
```

双轴 shader 分支核心计算：

```text
rayPrincipal = mul(rayDir, _WorldToPrincipalMatrix)
SolveBiaxialFresnel(rayPrincipal, nx, ny, nz) -> n1, n2
eigenPrincipal = 切平面上的解析本征向量
eigenA = principalToView * eigenPrincipal
eigenB = cross(rayDir, eigenA)
delta = 2*pi*thickness*abs(n1 - n2)*pathFactor/wavelength*phaseScale
intensity = 偏振片/检偏器 Jones 投影
```

### 电压处理

UI 只显示电压，核心只接收 `electricFieldStrength`。

推荐 facade 规则：

```text
electricFieldStrength = voltageV * fieldVmPerVolt
```

或：

```text
electricFieldStrength = voltageV / electrodeGapM
```

`fieldVmPerVolt` 或 `electrodeGapM` 必须是 preset 字段，并在实现前由实验设计确认。

### 参数暴露建议

默认暴露：

| UI | 内部字段 | 说明 |
| --- | --- | --- |
| 电压 | 换算到 `electricFieldStrength` | M2 核心控制。 |
| 波长 | `wavelengthNm` | 物理参数。 |
| 厚度 | `thicknessMm` | 物理参数。 |
| 晶片旋转角 alpha | `crystalAxisAngleDeg` | 双轴主轴面内旋转，不能叫入射角。 |
| 起偏器角度 | `polarizerAngleDeg` | 物理参数。 |
| 检偏器角度 | `analyzerAngleDeg` | 物理参数。 |
| theta | `paperThetaDeg` 或 facade 取向字段 | 高级参数；若暴露，Raw Jones 路径必须真的组合进矩阵。 |
| phi | `paperPhiDeg` 或 facade 取向字段 | 高级参数；若暴露，Raw Jones 路径必须真的组合进矩阵。 |
| 通光孔径 | `apertureRadius` | 高级参数。 |

默认隐藏：

| 字段 | 原因 |
| --- | --- |
| `biaxialDisplayMode` | 模式 preset 固定为 `RawJones`。 |
| `phaseScale`、`ringSharpness`、`crossWidth`、`blackCutoff`、`displayGamma` | 显示 preset。 |
| `nx/ny/nz`、`r22` | 材料数据，只读诊断即可。 |

## 模式 3：单轴电压连续 EO 计算链路

### 意图

M3 对应测试场景的 `EO Smooth` / `uniaxialEoView`。它以单轴晶体为基础，但允许电压让主折射率发生微小分裂，从而进入连续本征计算。

M3 与 M1 的区别：M3 有电压，且 `_ContinuousEigenMode` 打开。

M3 与 M2 的区别：M3 的 base profile 是单轴，`uniaxialEoView` 强制允许连续本征路径。

### 必要参数状态

| 字段 | 必要值 |
| --- | --- |
| Profile | 单轴 profile，默认 LiNbO3 |
| `biaxialDisplayMode` | `RawJones` |
| `uniaxialEoView` | `true` |
| `uniaxialEoUsePerturbedAxis` | 测试场景 smooth 默认 `false` |
| `forceUniaxial` | `false` |
| `electricFieldStrength` | 由 UI 电压换算 |
| `phaseScale` | 测试场景 smooth preset 为 `0.05` |
| `phaseAntiAliasStrength` | 测试场景 smooth preset 为 `3` |
| `renderSupersampleFactor` | 测试场景 smooth preset 为 `2` |

### 计算链路

```text
SetProfile(LiNbO3)
  -> parameters.ApplyProfileDefaults(profile)
  -> base nx, ny, nz 是单轴/近退化

应用 M3 continuous EO preset
  -> biaxialDisplayMode = RawJones
  -> uniaxialEoView = true
  -> forceUniaxial = false
  -> uniaxialEoUsePerturbedAxis = false
  -> electricFieldStrength = VoltageToField(voltageV)
  -> phaseScale = 0.05
  -> phaseAntiAliasStrength = 3

ForceRecalculate()
  -> ApplyPhysicalConfig()
     -> CrystalPhysicalCore 接收非零 localEField
     -> 扰动后的 principal indices 回写
     -> 扰动后的 worldToPrincipalMatrix 回写
     -> ApplyContinuousEoDisplayMode()
        - display mode 强制为 RawJones
        - forceUniaxial 强制为 false
        - 仅当 uniaxialEoUsePerturbedAxis 为 true 时更新 optic axis
  -> UploadParameters()
     -> _UseBiaxial = parameters.IsBiaxial() || uniaxialEoView = true
     -> _ContinuousEigenMode = 1
  -> shader
     -> TryGetBiaxialEigenSystem 使用 continuous mode 阈值
     -> 电压造成 split 足够大时进入 Raw Jones 本征路径
     -> split 接近 0 时回退到单轴 fallback
```

连续 EO shader 判定逻辑：

```text
continuousEigen = _ContinuousEigenMode > 0.5
if continuousEigen:
    max principal-index split >= 1e-7 时允许本征路径
else:
    要求三组主折射率差值都超过 uniaxialEpsilon
```

因此 M3 必须保持 `forceUniaxial = false`，否则会破坏连续本征链路。

### 参数暴露建议

默认暴露：

| UI | 内部字段 | 说明 |
| --- | --- | --- |
| 电压 | 换算到 `electricFieldStrength` | M3 核心控制。 |
| 波长 | `wavelengthNm` | 物理参数。 |
| 厚度 | `thicknessMm` | 物理参数。 |
| 起偏器角度 | `polarizerAngleDeg` | 物理参数。 |
| 检偏器角度 | `analyzerAngleDeg` | 物理参数。 |
| 通光孔径 | `apertureRadius` | 高级参数。 |

隐藏或开发者专用：

| 字段 | 原因 |
| --- | --- |
| `crystalAxisAngleDeg` / alpha | 不是 M3 的核心教学参数。 |
| `uniaxialEoUsePerturbedAxis` | 诊断“固定轴/扰动轴”差异，不是普通学生流程。 |
| `phaseScale`、`phaseAntiAliasStrength`、`renderSupersampleFactor` | smooth preset。 |
| `forceUniaxial` | 必须保持 `false`。 |

## 模式切换规则

facade 在切换模式时必须重置互斥状态：

| 切换目标 | 重置要求 |
| --- | --- |
| 任意 -> M1 | 电压/电场清零，`uniaxialEoView=false`，隐藏 alpha。 |
| 任意 -> M2 | `biaxialDisplayMode=RawJones`，`uniaxialEoView=false`，`forceUniaxial=false`，加载双轴 profile。 |
| 任意 -> M3 | `biaxialDisplayMode=RawJones`，`uniaxialEoView=true`，`forceUniaxial=false`，加载单轴 profile，应用 smooth EO preset。 |
| M2 -> M3 | 先清除 KTP/Paper 相关假设，再加载单轴 profile 和电压 preset。 |
| M3 -> M2 | 先关闭 continuous mode，再应用双轴电压参数。 |

## Preset 建议

### M1 单轴基线 preset

```text
profile = LiNbO3
biaxialDisplayMode = RawJones
uniaxialEoView = false
uniaxialEoUsePerturbedAxis = false
forceUniaxial = false
electricFieldStrength = 0
resolution = 256
renderSupersampleFactor = 1
phaseAntiAliasStrength = 1
phaseScale = 0.1 或沿用测试场景默认
```

### M2 双轴电压 preset

```text
profile = KTP
biaxialDisplayMode = RawJones
uniaxialEoView = false
uniaxialEoUsePerturbedAxis = false
forceUniaxial = false
electricFieldStrength = VoltageToField(voltageV)
resolution = 256
renderSupersampleFactor = 1 或 2
phaseAntiAliasStrength = 1
phaseScale = preset 管理
```

注意：M2 电压运行链路不要调用 `ApplyPaperKtp1Preset()`，除非后续改造 Paper 分支使其也经过物理核心电场扰动。

### M3 单轴电压 continuous preset

```text
profile = LiNbO3
biaxialDisplayMode = RawJones
uniaxialEoView = true
uniaxialEoUsePerturbedAxis = false
forceUniaxial = false
electricFieldStrength = VoltageToField(voltageV)
resolution = 256
renderSupersampleFactor = 2
phaseScale = 0.05
phaseAntiAliasStrength = 3
normalizeDisplayIntensity = false
```

## 显示层接入

实际场景可参考 `ConoscopicJonesSurfaceVisualizer` 的 mesh readback 和 rebuild 逻辑，但不应复用其 `OnGUI` 调试面板作为正式 UI。

推荐显示更新流程：

```text
core.OnJonesIntensityUpdated
  -> 读取 Result.IntensityHeightMap
  -> 更新 2D 屏幕纹理或 3D mesh
  -> 更新状态文字：模式、晶体、电压、最大强度
```

3D mesh 显示建议：

| 关注点 | 建议 |
| --- | --- |
| Mesh readback | 以测试场景 `RebuildMesh()` 为起点抽出正式组件。 |
| 高度倍率 | 每个模式 preset 管理。 |
| 颜色映射 | 场景固定。 |
| Normalize display | 默认 `false`，只在开发 preset 中开启。 |
| 平滑处理 | preset 管理，不作为用户控件。 |

## 验证计划

### Editor / 单元测试

建议复用或扩展 `ConoscopicJonesCoreTests`：

| 测试 | 期望 |
| --- | --- |
| M1 单轴模式创建有效纹理 | Result valid，纹理分辨率正确，输出有限。 |
| M2 KTP RawJones 电压 0 vs 非零 | 非零电压改变主折射率或矩阵。 |
| M2 不走 Paper 早返回 | 能观察到 `CrystalPhysicalCore` 扰动结果。 |
| M3 continuous EO 电压 0 vs 非零 | 非零电压改变 indices，`uniaxialEoView` 保持 true。 |
| 模式切换清理 | 每个模式能重置互斥 flags。 |

### 手动场景检查

| 检查 | 期望 |
| --- | --- |
| 反复切换 M1 -> M2 -> M3 | 无旧图残留、无 null texture、无黑屏。 |
| 调节 M2 电压 | 图样变化，或诊断中 indices/matrix 变化。 |
| 调节 M3 电压 | 连续 EO 图样在目标电压范围内平滑变化。 |
| 调节 M2 alpha | 双轴图样取向变化。 |
| M1/M3 默认隐藏 alpha | 用户不会被误导去调一个不响应或含义不清的参数。 |

## 实现待确认

| 项 | 需要确认 |
| --- | --- |
| 电压换算 | M2/M3 使用电极间距还是 `fieldVmPerVolt`。 |
| 电压范围 | 最小值、最大值、默认值、是否允许负电压。 |
| Profile 列表 | v1 是否固定 M1/M3 为 LiNbO3、M2 为 KTP。 |
| 重新计算策略 | 手动按钮、防抖实时刷新，还是两者都有。 |
| Raw Jones 的 theta/phi | 如果 M2 暴露 theta/phi，需要确认如何在非 Paper 路径组合进矩阵。 |

