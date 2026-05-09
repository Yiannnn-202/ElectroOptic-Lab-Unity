# 双轴晶体锥光干涉显示设计

## 目标

本设计用于在现有锥光干涉屏幕中显示双轴晶体的教学特征。它不改变 `CrystalProfile` 的数据结构，也不改变场景间 Profile 传递方式，只在现有渲染链路中补充双轴光轴与双黑点显示。

## 当前支持边界

现有 shader 已经通过 Fresnel 方程使用三主折射率 `n_x, n_y, n_z` 计算两条传播本征折射率：

```text
A_i = 1 / n_i^2

X^2
- X * [s_x^2(A_y + A_z) + s_y^2(A_x + A_z) + s_z^2(A_x + A_y)]
+ [s_x^2 A_y A_z + s_y^2 A_x A_z + s_z^2 A_x A_y] = 0

n_1 = 1 / sqrt(X_1)
n_2 = 1 / sqrt(X_2)
Delta n = abs(n_1 - n_2)
Gamma = 2*pi*L*Delta n / lambda
```

因此环纹相位本身可以接受双轴晶体的三主折射率。原有不足在消光图样：旧逻辑只从 `_OpticAxisView` 推出单个 melatope 和单个黑十字，所以对双轴晶体只能显示为单轴近似。

## 双轴光轴近似公式

当三主折射率均不相等时，渲染器按双轴晶体处理。先将三主折射率排序为：

```text
n_alpha <= n_beta <= n_gamma
```

令主轴方向分别为 `e_alpha, e_beta, e_gamma`。在 `e_alpha-e_gamma` 平面内，两条光轴相对于 `e_gamma` 对称。近似角度为：

```text
cos(theta) = sqrt((n_beta^2 - n_alpha^2) / (n_gamma^2 - n_alpha^2))
sin(theta) = sqrt(1 - cos(theta)^2)

axis_a = normalize( e_alpha * sin(theta) + e_gamma * cos(theta))
axis_b = normalize(-e_alpha * sin(theta) + e_gamma * cos(theta))
```

随后用 `ShaderWorldToPrincipalMatrix` 的转置把两条光轴从主轴坐标变换到视图坐标，并保证 `z >= 0`，只传递投影所需的 `xy` 分量给 shader。

若任意两主折射率差值小于阈值，仍按单轴晶体处理，避免 LiNbO3 与 KDP 的显示行为变化。

## Shader 显示逻辑

shader 保留原有单轴路径：

```text
melatope = project(_OpticAxisView)
cross = extinction(ray, melatope)
```

新增双轴路径：

```text
axis_a = reconstruct(_BiaxialAxesView.xy)
axis_b = reconstruct(_BiaxialAxesView.zw)

offset_a = project(axis_a) + initial_offset
offset_b = project(axis_b) + initial_offset

pattern_a = extinction(ray, axis_a, p - offset_a)
pattern_b = extinction(ray, axis_b, p - offset_b)
cross_pattern = min(pattern_a, pattern_b)
```

当真实光轴投影落在当前锥光视场外时，双轴路径会把 melatope 的显示投影限制在画面内，用于保证教学演示中能看到双黑点。该限制只影响消光刷的屏幕位置，不改变 Fresnel 相位环纹中的 `Delta n` 计算。

最终强度仍沿用现有环纹模型：

```text
ring_pattern = 0.5 - 0.5 * cos(Gamma) * visibility
intensity = cross_pattern * ring_pattern * aperture
```

该实现的目标是教学可视化：让双轴晶体出现两个 melatope 和双消光刷。它不是完整的偏振本征矢、振幅传输和分析器投影模型。

## 数据流

新增晶体参数继续使用既有 Profile 链路：

```text
CrystalProfile
  -> CrystalSelectionData.SelectedProfile
  -> CrystalComponentInitializer
  -> CrystalControllerWrapper.SetProfile()
  -> CrystalPhysicalCore.ApplyConfig()
  -> ConoscopicTextureRenderer.UpdateShaderProperties()
  -> ConoscopicInterference.shader
```

因此 KDP、KTP、LiNbO3 在后续实验和示波器场景中都通过同一个 `CrystalProfile` 来源读取折射率、EO 系数、默认波长和几何尺寸，不需要为新晶体写特殊分支。

## KTP 工作几何一致性

KTP 是本轮唯一启用双轴锥光显示的经典双轴晶体。为避免 Scene2 锥光图和 Scene4 示波器调制计算使用不同传播方向，KTP 通过 `CrystalWorkingGeometry` 固定解析为同一套物理计算几何：

```text
k = +Y = Vector3.up
E = +Z = Vector3.forward
probe = +Z = Vector3.forward
modulationMode = Transverse
```

Scene2 中，`CrystalControllerWrapper.UpdatePhysicsConfig()` 仍然读取当前激光方向作为请求方向；普通晶体继续使用该方向。若当前 Profile 为 KTP，解析器会把 `CrystalConfig.worldLightDirection` 覆盖为 `Vector3.up`，锥光图按 KTP 的 Y 通光几何显示。锥光显示是无外场图，所以 `localEField` 保持 `Vector3.zero`，但 `probeFieldDirection` 记录为 Z，便于诊断与 Scene4 保持一致。

Scene4 中，`OscilloscopeCrystalBridge.ConfigureAndGetSensitivity()` 也调用同一个解析器。普通晶体仍使用默认 `worldLightDirection = Vector3.forward` 和用户请求的电场轴；KTP 覆盖为 `k=Y, E=Z, Transverse`，从而避免 KTP 在 `k=Z, E=Z` 几何下有效灵敏度接近 0 导致 Vπ 无效。

该覆盖只作用于物理计算几何，不强制移动 Scene2 的可视激光器、晶体模型或光路物体。
