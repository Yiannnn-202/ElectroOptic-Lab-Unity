# PRD: 附加实验 Jones 锥光三模式接入

## 文档信息

| 项目 | 内容 |
| --- | --- |
| 目标场景 | 实际接入的附加实验场景 |
| 参考场景 | `ConoscopicJonesIntensitySurface_Test.unity` |
| 目标核心 | `ConoscopicJonesGpuCore` |
| 文档日期 | 2026-05-28 |
| 文档用途 | 指导产品交互、模式划分、参数暴露和验收 |

## 背景

测试场景已经验证了 Jones/GPU 锥光图的三类计算行为：

1. 单轴晶体的普通 Jones 单轴分支。
2. 双轴晶体的 Raw Jones / Fresnel 本征计算分支。
3. 单轴晶体在电场下的连续 EO 计算分支，也就是 `uniaxialEoView` 链路。

实际附加实验不应直接暴露测试场景的完整调试面板，而应把这些计算能力包装成面向学生的三个清晰模式。学生只调节能解释为实验物理条件的参数；显示效果、数值稳定和视觉调校参数由场景 preset 管理。

## 产品目标

实际接入场景提供三个模式：

| 模式 | 用户名称 | 参考计算链路 | 学习目标 |
| --- | --- | --- | --- |
| M1 | 单轴晶体 | 测试场景单轴 Jones 分支 | 展示无电压单轴锥光图、偏振片/检偏器影响。 |
| M2 | 双轴晶体 | 测试场景双轴 Raw Jones 分支，并支持电压调节 | 展示双轴晶体锥光图及电压改变后的响应。 |
| M3 | 单轴电压调整 | 测试场景连续 EO 链路，即 `uniaxialEoView` | 展示单轴晶体在电场扰动下的连续本征模式变化。 |

## 非目标

- 不在正式 UI 中暴露 `ConoscopicJonesParameters` 的所有字段。
- 不把 `crystalAxisAngleDeg` 命名为“入射角”。它当前表示晶体主轴坐标系的面内旋转角 alpha。
- M2 的电压模式不走 `PaperKtp1` 早返回分支，因为该分支不会调用 `CrystalPhysicalCore`，电压不会生效。
- 不让学生直接编辑 `nx/ny/nz`、`no/ne`、`r22` 等材料参数。
- 不把 `phaseScale`、`ringSharpness`、`crossWidth`、`blackCutoff`、`displayGamma`、分辨率、曲面高度、视角旋转等显示参数作为学生主流程控件。

## 用户角色

| 角色 | 需求 |
| --- | --- |
| 学生 | 通过少量物理参数看到锥光图变化，避免被调试参数干扰。 |
| 教师 | 能清楚演示单轴、双轴、单轴电压扰动三种现象的差异。 |
| 开发者 | 用稳定 facade 接入测试场景核心，不复制 shader 或计算逻辑。 |

## 模式设计

### M1: 单轴晶体

M1 是无电压单轴基线模式，默认建议使用 LiNbO3。该模式应走测试场景中的单轴 fallback 计算链路。

建议暴露参数：

| UI 名称 | 内部字段 | 默认值 | 建议范围 | 说明 |
| --- | --- | ---: | ---: | --- |
| 晶体 | `CrystalProfile` | LiNbO3 | 单轴 profile | 如果 v1 只教学 LiNbO3，可固定不显示选择。 |
| 波长 | `wavelengthNm` | profile 默认 | 400-800 nm | 真实物理参数。 |
| 厚度 | `thicknessMm` | profile 默认 | 0.1-60 mm | 真实物理参数。 |
| 起偏器角度 | `polarizerAngleDeg` | 0 deg | 0-180 deg | 建议提供“正交偏振”快捷按钮。 |
| 检偏器角度 | `analyzerAngleDeg` | 90 deg | 0-180 deg | 建议提供“正交偏振”快捷按钮。 |
| 光轴倾角 | `opticAxisTiltDeg` | 0 deg | 0-45 deg | 单轴模式下真正影响图像的取向参数。 |
| 光轴方位角 | `opticAxisAzimuthDeg` | 0 deg | 0-360 deg | 建议放入高级参数。 |
| 通光孔径 | `apertureRadius` | 1.0 | 0.05-1.0 | 可作为高级参数。 |

建议隐藏参数：

| 字段 | 原因 |
| --- | --- |
| `crystalAxisAngleDeg` / alpha | 单轴 fallback 中它主要是 fallback 参考轴，不是主计算变量。普通用户调节后图像可能不动，容易误解。 |
| `electricFieldStrength` | M1 是无电压基线。 |
| `forceUniaxial` / `uniaxialEpsilon` | 内部分支控制。 |
| `nx/ny/nz`、`no/ne` | 材料数据，最多只读展示。 |

### M2: 双轴晶体

M2 是带电压调节的双轴晶体模式，默认建议使用 KTP。该模式必须走测试场景中的 Raw Jones 双轴链路，并通过 `CrystalPhysicalCore` 让电压扰动进入主折射率和主轴矩阵。

建议暴露参数：

| UI 名称 | 内部字段 | 默认值 | 建议范围 | 说明 |
| --- | --- | ---: | ---: | --- |
| 晶体 | `CrystalProfile` | KTP | 双轴 profile | v1 可固定 KTP。 |
| 电压 | 换算到 `electricFieldStrength` | 0 V | 待确认 | UI 显示伏特，内部使用 V/m。 |
| 波长 | `wavelengthNm` | profile 或 preset | 400-800 nm | 真实物理参数。 |
| 厚度 | `thicknessMm` | profile 或 preset | 0.1-60 mm | 真实物理参数。 |
| 晶片旋转角 alpha | `crystalAxisAngleDeg` | 45 deg | 0-180 deg | 双轴模式下有意义，表示主轴坐标系面内旋转角，不是入射角。 |
| 起偏器角度 | `polarizerAngleDeg` | 0 deg | 0-180 deg | 真实物理参数。 |
| 检偏器角度 | `analyzerAngleDeg` | 90 deg | 0-180 deg | 真实物理参数。 |
| 样品倾角 theta | `paperThetaDeg` 或 facade 取向字段 | 0 deg | 0-90 deg | 建议默认折叠到高级参数。 |
| 样品方位角 phi | `paperPhiDeg` 或 facade 取向字段 | 0 deg | 0-360 deg | 建议默认折叠到高级参数。 |
| 通光孔径 | `apertureRadius` | 1.0 | 0.05-1.0 | 高级参数。 |

M2 的重要产品约束：

| 约束 | 说明 |
| --- | --- |
| 使用 `RawJones` | 电压模式必须让 `ApplyPhysicalConfig()` 调用 `CrystalPhysicalCore`。 |
| 不使用 `PaperKtp1` 作为电压运行链路 | `PaperKtp1` 会提前返回，只设置 profile 折射率和论文矩阵，不处理电场扰动。 |
| 电压不直接写 shader | UI 电压应先换算为 `electricFieldStrength`，再由物理核心计算扰动后的主折射率和矩阵。 |

建议隐藏参数：

| 字段 | 原因 |
| --- | --- |
| `biaxialDisplayMode` | 模式 preset 固定为 `RawJones`。 |
| `phaseScale`、`ringSharpness`、`crossWidth`、`blackCutoff`、`displayGamma` | 显示调校参数。 |
| `nx/ny/nz`、`r22` | 材料参数，建议只读诊断。 |

### M3: 单轴电压调整

M3 是单轴晶体在电压下的连续 EO 显示模式，对应测试场景 `EO Smooth` / `uniaxialEoView` 链路。默认建议使用 LiNbO3。

建议暴露参数：

| UI 名称 | 内部字段 | 默认值 | 建议范围 | 说明 |
| --- | --- | ---: | ---: | --- |
| 晶体 | `CrystalProfile` | LiNbO3 | 单轴 profile | v1 可固定 LiNbO3。 |
| 电压 | 换算到 `electricFieldStrength` | 0 V 或 preset 值 | 待确认 | UI 显示伏特，内部使用 V/m。 |
| 波长 | `wavelengthNm` | profile 默认 | 400-800 nm | 真实物理参数。 |
| 厚度 | `thicknessMm` | profile 默认 | 0.1-60 mm | 真实物理参数。 |
| 起偏器角度 | `polarizerAngleDeg` | 0 deg | 0-180 deg | 真实物理参数。 |
| 检偏器角度 | `analyzerAngleDeg` | 90 deg | 0-180 deg | 真实物理参数。 |
| 通光孔径 | `apertureRadius` | 1.0 | 0.05-1.0 | 高级参数。 |

建议开发/高级参数：

| 字段 | 测试场景默认 | 原因 |
| --- | ---: | --- |
| `uniaxialEoUsePerturbedAxis` | `false` | 可用于诊断“固定轴/扰动轴”显示差异，但不建议放到学生主流程。 |
| `phaseScale` | 0.05 | `EO Smooth` 显示 preset，不应解释为实验物理量。 |
| `phaseAntiAliasStrength` | 3.0 | 显示稳定参数。 |
| `renderSupersampleFactor` | 2 | 显示质量参数。 |

建议隐藏参数：

| 字段 | 原因 |
| --- | --- |
| `crystalAxisAngleDeg` / alpha | 不是 M3 的核心教学控制。 |
| `forceUniaxial` | 必须保持 `false`，否则连续本征链路无法按预期工作。 |

## 通用交互流程

1. 用户选择 M1/M2/M3。
2. 场景加载对应 crystal profile 和模式 preset。
3. UI 只写入允许暴露的物理参数。
4. facade 把 UI 电压换算成 `electricFieldStrength`。
5. facade 在晶体变化时调用 `core.SetProfile(...)`。
6. facade 克隆 `ConoscopicJonesParameters`，应用 mode preset 和用户参数，然后调用 `core.SetParameters(p)`。
7. 用户点击“重新计算”，或参数变化后触发防抖自动计算。
8. 场景读取 `core.IntensityHeightMap` 和 `core.Result` 更新 2D/3D 显示。

## Preset 管理原则

以下参数由场景 preset 管理，不进入普通学生 UI：

| 参数 | 所属 |
| --- | --- |
| `resolution` | 计算质量 preset |
| `renderSupersampleFactor` | 计算质量 preset |
| `screenDistanceM` | 观察几何 preset |
| `screenHalfSizeM` | 观察几何 preset |
| `initialIntensity` | 显示基准 preset |
| `phaseAntiAliasStrength` | 数值稳定 preset |
| `phaseScale` | 显示/相位缩放 preset |
| `ringSharpness` | 显示 preset |
| `crossWidth` | 显示 preset |
| `blackCutoff` | 显示 preset |
| `displayGamma` | 显示 preset |
| 曲面尺寸、高度倍率、颜色映射 | 3D 可视化 preset |

## 验收标准

1. 实际附加实验场景有且只有三个主模式：单轴、双轴、单轴电压调整。
2. M1 使用单轴 profile，并产生与测试场景单轴路径一致类型的锥光图。
3. M2 使用双轴 profile、`RawJones` 模式，非零电压能通过 `CrystalPhysicalCore` 改变主折射率或主轴矩阵。
4. M2 不使用 `PaperKtp1` 作为电压运行链路。
5. M3 设置 `uniaxialEoView = true`、`forceUniaxial = false`，走连续 EO 链路。
6. UI 中电压以伏特显示，`electricFieldStrength` 作为内部派生值。
7. alpha 只在 M2 中默认暴露；M1/M3 默认隐藏或高级折叠。
8. 显示调校参数不出现在普通学生 UI。
9. 模式切换时会重置互斥字段，避免 M3 残留 Paper KTP 或 M2 残留 continuous EO。
10. 三个模式均能输出有效 `RenderTexture`，无 NaN、黑屏、纹理未刷新或旧图残留。

## 实施前待确认

| 问题 | 影响 |
| --- | --- |
| M2/M3 的电压到电场换算使用电极间距还是固定比例 `fieldVmPerVolt`？ | 决定 `electricFieldStrength` 的物理含义。 |
| 学生 UI 的电压范围、默认值、是否允许负电压？ | 决定 slider 范围和安全说明。 |
| M2 v1 是否固定 KTP，还是允许多个双轴 profile？ | 决定晶体选择器复杂度。 |
| M1/M3 是否固定 LiNbO3，还是允许多个单轴 profile？ | 决定晶体选择器和验证逻辑。 |
| 计算触发是手动按钮、防抖自动刷新，还是两者都有？ | 决定 UI 交互和性能策略。 |

