# 双轴晶体锥光可视化修复计划

## Summary

当前 KTP 曲面不符合双轴锥光预期，根因不是 mesh 没显示，而是双轴分支直接显示高频 Jones 相位结果：20mm 晶体相位过大导致 aliasing，且没有显式构造双轴锥光图应有的双 melatope / isogyre / isochrome 结构。修复方向应从“原始双轴 Jones 强度面”改为“面向教学可视化的双轴锥光强度面”，对齐现有二维锥光 shader 的成熟模型。

参考预期特征：双轴图应有两个 melatope、弯曲或双曲线状 isogyres、围绕 melatope 的 isochromes，而不是满屏尖刺噪声。

参考资料：

- [Conoscopic interference pattern](https://en.wikipedia.org/wiki/Conoscopic_interference_pattern)
- [LibreTexts Biaxial Interference Figures](https://geo.libretexts.org/Bookshelves/Geology/Mineralogy_%28Perkins_et_al.%29/05%3A_Optical_Mineralogy/5.06%3A_Interference_Figures/5.6.2%3A_Biaxial_Interference_Figures)

---

## Key Changes

- 双轴路径改为教学锥光模型：
  - 使用三主折射率 Fresnel 方程计算 `n1/n2` 和 `deltaN`。
  - 增加 `phaseScale`，默认沿用旧锥光链路 `0.1`，避免 20mm KTP 直接产生不可采样高频相位。
  - 使用 `fwidth(gamma)` + `ringSharpness` 抑制过密环纹 aliasing。
  - 显式生成双 melatope / isogyre 消光 mask，而不是只依赖近似本征偏振方向自然形成图样。
- 复用旧二维锥光逻辑：
  - 从 `ConoscopicInterference.shader` / `ConoscopicIntensityCalculator` 迁移 `SolveFresnel`、`DeriveBiaxialAxesView`、`GetBiaxialExtinctionPattern` 的等价逻辑。
  - C# 侧上传 `_BiaxialAxesView`，shader 侧只负责重建双光轴投影和渲染强度。
- 分离“教学显示”和“原始 Jones”：
  - 单轴仍保留当前 Jones 分支。
  - 双轴默认使用 `BiaxialConoscopicDisplay` 分支。
  - 原始双轴 Jones 近似可保留为后续调试/研究模式，但不作为默认可视化输出。
- 修正显示层：
  - 关闭默认 `Normalize Display`，避免把 aliasing 或微小噪声放大成尖刺。
  - 由 shader 输出已平滑、已 shaping 的 `[0,1]` 教学强度图，曲面直接使用该强度做高度和颜色。
  - UI 暴露 `phaseScale`、`ringSharpness`、`crossWidth`、`blackCutoff/displayGamma`，并保留物理参数 `nx/ny/nz`、波长、厚度、屏幕距离/半宽。

---

## Implementation Details

### `ConoscopicJonesParameters`

- 新增双轴显示参数：
  - `phaseScale = 0.1`
  - `ringSharpness = 1.0`
  - `crossWidth = 0.16`
  - `blackCutoff = 0.012`
  - `displayGamma = 1.25`
  - `initialMelatopeOffset = (0.035, -0.025)`
- 新增模式字段：
  - `biaxialDisplayMode = ConoscopicTeaching`
  - 默认仅双轴启用。

### `ConoscopicJonesGpuCore`

- 保留 `PhysicalCore.NewPrincipalIndices` 和 `ShaderWorldToPrincipalMatrix` 上传。
- 增加 `DeriveBiaxialAxesView(indices, worldToPrincipalMatrix)`，逻辑与旧链路一致，近退化时上传 `Vector4.zero` 并回退单轴。
- 上传 `_BiaxialAxesView` 和新增显示 shaping 参数。

### `ConoscopicJonesIntensity.shader`

单轴分支保持现状。双轴分支改为：

```text
rayView -> rayPrincipal
SolveFresnel(rayPrincipal, nx/ny/nz) -> deltaN
gamma = 2*pi*thickness*deltaN/wavelength*phaseScale
ringPattern = 0.5 - 0.5*cos(gamma)*visibility
crossPattern = min(patternA, patternB)
intensity = shaped(ringPattern * crossPattern * aperture)
```

双轴失败时回退单轴消光。

### `ConoscopicJonesSurfaceVisualizer`

- 默认不归一化显示。
- 面板加入 `PhaseScale`、`RingSharpness`、`CrossWidth`、`BlackCutoff`、`DisplayGamma` 控件。
- KTP 默认参数应先显示平滑的双轴教学图样，再用高度面呈现。

---

## Test Plan

### 自动测试

- KTP 判定为双轴，`_BiaxialAxesView` 非零。
- 双轴 Fresnel 解有限，圆孔外强度为 `0`，输出范围在 `[0,1]`。
- KTP 默认输出有有效动态范围，但相邻像素差异不过度尖刺化。
- LiNbO3 单轴 GPU/CPU 抽样测试继续通过。

### 视觉验收

- KTP 默认参数下，应出现双轴图样趋势：两个暗点/双消光刷或双曲线状 isogyres，而不是满屏噪声尖峰。
- 调整 `phaseScale` 应改变环纹密度。
- 调整 `crossWidth` 应改变消光刷宽度。
- 关闭/打开 `Normalize Display` 不应改变底层图样，只影响调试显示。

---

## Assumptions

- 本次修复目标是“符合双轴锥光教学图和光强面预期”，不追求完整 Berreman 物理模型。
- KTP 首版以可见双轴特征为验收核心；不要求和网上/教材图片像素级一致。
- 现有旧二维锥光模型是项目内可信参考，实现时优先复用它的双轴显示逻辑。

