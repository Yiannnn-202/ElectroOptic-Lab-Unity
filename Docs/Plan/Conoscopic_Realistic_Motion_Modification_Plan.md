# 真实锥光干涉图运动规律与修改计划

## 1. 目标与边界

本文档用于记录下一轮锥光干涉图可视化改造方案：在不改变 Scene2 现有调节管道的前提下，让锥光图在调节晶体角度时更符合真实实验中的运动规律。

必须保留的现有管道：

```text
CrystalKnobBridge
  -> CrystalControllerWrapper
  -> CrystalPhysicalCore
  -> ConoscopicTextureRenderer
  -> ConoscopicInterference.shader
  -> UnifiedScreenPanel
```

本计划只增强 `ConoscopicTextureRenderer` 到 shader 的派生可视化参数，以及 shader 内部的光轴投影、偏振消光和环纹显示模型。调节输入、角度 clamp、物理核心、RenderTexture 显示层均不替换。

## 2. 真实锥光干涉图运动规律

在正交偏振下，单轴晶体的锥光干涉图主要由两类结构组成：

- 等色线 `isochromates`：由相位延迟相同的方向形成，通常表现为围绕光轴出射点的同心环。
- 等倾线/消光线 `isogyres`：由偏振消光形成，典型表现为黑十字。

当晶体光轴正对观察轴时，黑十字中心 `melatope` 位于视场中心，同心环也以该点为中心。此时如果只是绕光轴自转，理想居中单轴图样应基本不移动。

当晶体光轴发生俯仰或偏航时，`melatope` 会偏离视场中心；黑十字交点和同心环中心应一起随光轴投影平滑移动。继续旋转晶体时，黑十字和环心会围绕视场进动，而不是固定在屏幕中心。

在单轴晶体且无外电场的近似下，环纹应主要保持圆形，只是中心偏移。若引入横向电场、双轴化或更完整的电光效应模型，黑十字可能变成双曲形，环纹也可能轻微椭圆化。

## 3. 当前实现差异

当前 `ConoscopicInterference.shader` 的黑十字主要由屏幕坐标生成：

```hlsl
float phi = atan2(p.y, p.x);
```

这意味着黑十字天然锁定在 RenderTexture 中心。调节晶体俯仰/偏航时，相位 `gamma` 会随 `_RotationMatrix` 和折射率解变化，但黑十字中心和 `melatope` 没有真实跟随光轴投影移动。

因此当前效果更接近“居中单轴锥光图”的视觉近似，不符合真实追光/调角时应出现的光轴投影偏移。用户看到的主要问题包括：

- 黑十字像固定 UI 图形，而不是随晶体姿态运动的消光结构。
- 环心与黑十字交点没有被统一的光轴投影驱动。
- 调节角度时图案变化方式不符合“melatope 偏移、环心跟随、归零复位”的真实规律。

## 4. 修改原则

1. 不改变 Scene2 调节管道。
2. 不改变 `CrystalControllerWrapper` 的角度输入、clamp 范围和旋钮桥接逻辑。
3. 不改变 `CrystalPhysicalCore` 的 DLL 计算职责和现有公开接口。
4. 不替换 `UnifiedScreenPanel`、`ConoscopicScreenDataProvider` 和 RenderTexture 显示方式。
5. 只在 renderer 中派生“光轴在观察坐标中的方向”，并传给 shader。
6. shader 内部用光轴投影生成 `melatope`、黑十字和环心，而不是继续使用固定屏幕中心。

## 5. 实现计划

### 5.1 Renderer 派生光轴方向

在 `ConoscopicTextureRenderer` 中，从 `CrystalPhysicalCore.ShaderWorldToPrincipalMatrix` 派生当前光轴在观察坐标中的方向，并传给 shader，例如：

```hlsl
_OpticAxisView
```

当前 KDP 与 LiNbO3 都可按单轴晶体处理，且 `nx ~= ny != nz`，默认唯一折射率轴为 z 主轴。实现时可先固定使用主轴 z 作为光轴；如果未来需要支持更通用晶体，再根据 `NewPrincipalIndices` 自动识别唯一轴。

### 5.2 Shader 使用光轴投影

shader 中使用 `_OpticAxisView` 计算光轴在视场中的投影点：

```hlsl
melatopeOffset = opticAxisView.xy / (opticAxisView.z * tan(FOV / 2))
```

后续十字与环纹中心都应以 `p - melatopeOffset` 为局部坐标，而不是固定使用 `p`。

### 5.3 黑十字由偏振消光生成

黑十字不再用固定屏幕坐标 `atan2(p.y, p.x)` 生成。改为对每个视场 ray：

1. 将光轴投影到垂直于 ray 的平面。
2. 得到该 ray 对应的局部偏振方向。
3. 用该偏振方向与起偏器/检偏器方向计算消光强度。

这样黑十字会自然随光轴投影移动。居中时仍表现为中心黑十字；倾斜时十字交点和暗带随 `melatope` 偏移。

### 5.4 环纹继续使用现有相位模型

保留现有 Fresnel 解与相位差计算：

```hlsl
gamma = 2 * PI * pathLength * delta_n / wavelength
```

继续保留显示调参：

- `_PhaseScale`
- `_DisplayGamma`
- `_BlackCutoff`
- `_RingSharpness`
- `_CrossWidth`

但环纹视觉中心应随 `melatopeOffset` 偏移。第一阶段优先实现“偏心圆环 + 随动黑十字”；不在本轮实现完整 Jones 矩阵、物镜 NA、晶体表面折射、多波长色散或横向电场双轴化。

## 6. 测试计划

1. 零角度：黑十字中心与视场中心重合，环纹居中。
2. LU/RD 调节：`melatope`、黑十字交点、环心沿对应方向平滑偏移，松开后停止。
3. 角度归零：图样回到居中状态。
4. 绕光轴自转：居中单轴图不应出现大幅漂移。
5. KDP 与 LiNbO3：均能显示清晰黑十字和同心环，环数可通过 `conoscopicPhaseScale` 微调。
6. 画面质量：不出现硬切黑条、随机黑噪点或 alpha 二值化红黑图。

## 7. 后续高保真阶段

本轮计划只修复“调角时的真实运动趋势”。后续若需要进一步提升物理真实性，可分阶段加入：

- 完整起偏器/检偏器 Jones 矩阵。
- 横向电场导致的双轴化和双曲形 isogyres。
- 物镜数值孔径与视场采样模型。
- 晶体表面折射和有效光程修正。
- 多波长色散与彩色干涉环。

## 8. 参考资料

- Scientific Reports, “Isogyres - Manifestation of Spin-orbit interaction in uniaxial crystal”：https://www.nature.com/articles/srep33141
- LibreTexts, “Uniaxial Interference Figures”：https://geo.libretexts.org/Bookshelves/Geology/Mineralogy_(Perkins_et_al.)/05%3A_Optical_Mineralogy/5.06%3A_Interference_Figures/5.6.01%3A_Uniaxial_Interference_Figures
