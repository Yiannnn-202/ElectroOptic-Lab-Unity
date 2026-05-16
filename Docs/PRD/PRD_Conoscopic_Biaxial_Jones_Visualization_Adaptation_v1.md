# PRD: 附加实验双轴晶体 Jones 计算核心到可视化适配 v1.0

## 文档信息

| 项目 | 内容 |
|------|------|
| 项目名称 | 锥光干涉附加实验: 双轴晶体 Jones 计算核心到可视化适配 |
| 版本 | v1.0 |
| 创建日期 | 2026-05-14 |
| 目标模块 | `ElectroOptics.ConoscopicAnalysis` |
| 首要验收对象 | KTP 双轴晶体 |
| 回归对象 | LiNbO3 等单轴晶体 |

---

## 1. 背景与目标

现有附加实验 Jones/GPU 链路已经支持单轴晶体光强曲面:

```text
CrystalProfile
  -> ConoscopicJonesParameters
  -> ConoscopicJonesGpuCore
  -> ConoscopicJonesIntensity.shader
  -> RenderTexture intensityHeightMap
  -> ConoscopicJonesSurfaceVisualizer
```

旧实现只使用 `ordinaryIndexNo` / `extraordinaryIndexNe` 和单光轴模型。KTP 等双轴晶体的 `n_x/n_y/n_z` 会被压缩为单轴近似，无法表现双轴晶体的双光轴、双 melatope 和双消光刷。

v1.0 目标是在不新建并行核心的前提下扩展现有 `ConoscopicJones*` 链路，使其可通过 `CrystalPhysicalCore.NewPrincipalIndices` 与 `ShaderWorldToPrincipalMatrix` 使用三主折射率和主轴变换，生成 KTP 双轴可视特征，同时保持单轴晶体当前表现。

---

## 2. 单轴与双轴链路差异

| 项目 | 单轴链路 | 双轴链路 |
|------|----------|----------|
| 折射率输入 | `no/ne` | `nx/ny/nz` |
| 光轴 | 1 条 optic axis | 由三主折射率和主轴姿态推导的双轴行为 |
| 相位差 | `nEffective - no` | Fresnel 本征解 `abs(n1 - n2)` |
| 偏振方向 | o/e 投影 | 解析近似本征偏振方向，并做方向符号稳定 |
| 可视特征 | 同心环 + 单消光十字 | 双轴相位结构 + KTP 双轴消光特征 |
| 退化策略 | 原路径 | 接近单轴或数值失败时自动回退单轴并输出日志 |

---

## 3. 功能要求

- 保留现有 `ConoscopicJones*` API 和单轴行为。
- `ConoscopicJonesParameters` 保留 `no/ne`，并新增或维护 `nx/ny/nz`、双轴判定阈值、主轴变换矩阵。
- `ConoscopicJonesGpuCore` 在存在 Profile 时使用现有 `CrystalPhysicalCore` 获取电光扰动后的 `NewPrincipalIndices` 和 `ShaderWorldToPrincipalMatrix`。
- `ConoscopicJonesIntensity.shader` 增加单轴/双轴分支:
  - 单轴继续使用当前 `EffectiveExtraordinaryIndex(no, ne)`。
  - 双轴使用 Fresnel 方程求两条本征传播折射率，并使用解析近似本征偏振方向进行 Jones 投影。
- 双轴失败、接近退化点或本征方向不稳定时自动回退单轴路径，并通过日志提示。
- `ConoscopicJonesSurfaceVisualizer` 首版以物理参数为主，显示晶体类型、`nx/ny/nz`、波长、厚度、偏振/检偏角、屏幕距离、屏幕半宽和分辨率。
- 分辨率默认 `256`，有效范围 `16-1024`，运行时可调。

---

## 4. 非目标

- 不实现完整 Berreman 4x4 模型。
- 不在 Jones 核心内重复实现完整电光张量扰动；电光扰动结果来自现有 `CrystalPhysicalCore`。
- 不实现新的最终 UI 或双击进入附加实验的调度链路。
- 不要求与旧二维锥光 shader 像素级一致，只要求双轴特征和主要趋势一致。

---

## 5. 验收标准

1. KTP Profile 进入双轴路径，并使用三主折射率参与 GPU/CPU 计算。
2. KTP 默认参数下，三维曲面可观察到双轴晶体可视特征。
3. LiNbO3 等单轴 Profile 继续显示当前单轴 Jones 图样。
4. 主折射率接近退化点时自动回退单轴路径，不黑屏、不 NaN。
5. GPU 输出强度保持在 `[0, 1]`，圆孔外强度为 `0`。
6. CPU reference 与 GPU 关键点抽样趋势一致，允许合理容差。
7. 截图验收包含 KTP 默认、LiNbO3 默认、KTP 调整厚度/波长/屏幕半宽后的对照。

---

## 6. 测试计划

- 参数映射测试: `CrystalProfile.n_x/n_y/n_z` 正确进入 Jones 参数副本。
- 晶体类型测试: KTP 判定为双轴，近退化主折射率判定为单轴。
- CPU 双轴测试: KTP 强度有限、圆孔外为零、相位差有限正值。
- GPU 生命周期测试: KTP 可创建有效 RenderTexture，输出范围合法。
- GPU/CPU 抽样测试: KTP 中心和象限点与 CPU reference 趋势一致。
- 单轴回归测试: 现有 LiNbO3/默认单轴 GPU/CPU 测试继续通过。

