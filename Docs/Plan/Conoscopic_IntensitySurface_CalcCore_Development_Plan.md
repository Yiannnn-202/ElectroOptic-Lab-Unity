# 锥光干涉图光强曲面计算核心开发计划

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档日期 | 2026-05-13 |
| 关联 PRD | `Docs/PRD/PRD_Conoscopic_IntensitySurface_CalcCore.md` |
| 目标模块 | `ElectroOptics.ConoscopicAnalysis` |
| 范围 | 计算核心、参数/结果契约、测试与验收计划 |
| 不包含 | UI 设计、场景搭建、双击跳转、三维 Mesh 曲面渲染 |

---

## 1. 开发目标

本计划用于指导锥光干涉图附加实验的第一阶段开发：先完成独立的光强曲面计算核心。核心负责接收晶体 Profile、锥光显示参数和网格配置，输出规则 `x/y/intensity` 网格，供后续 UI 与三维曲面渲染消费。

完成后应具备：

- 不依赖 Scene2 运行时对象或 `CrystalRuntime` 的独立计算能力。
- 默认使用进入新场景时传入的晶体 Profile，并允许本地切换晶体。
- 参数变化后 dirty 重算，并通过事件通知外部刷新。
- CPU 计算结果在主要图样上复刻当前 `ConoscopicInterference.shader`。
- 为后续 UI、调度脚本和曲面渲染保留清晰接入点。

---

## 2. 模块设计

### 2.1 建议新增目录与文件

建议新增目录：

```text
ElectroOptic-Lab/Assets/Scripts/ConoscopicAnalysis/
```

建议新增文件：

| 文件 | 职责 |
|------|------|
| `ConoscopicIntensityCore.cs` | Unity 组件入口，管理 Profile、参数、dirty 状态、独立物理核心和事件通知 |
| `ConoscopicIntensityParameters.cs` | 可序列化参数容器，保存 FOV、phaseScale、display mapping、网格分辨率等输入 |
| `ConoscopicIntensityResult.cs` | 输出数据容器，保存规则网格、参数快照和计算状态 |
| `ConoscopicIntensityCalculator.cs` | 可选纯算法辅助类，承载 shader 等价 CPU 采样逻辑 |

命名空间统一使用：

```csharp
namespace ElectroOptics.ConoscopicAnalysis
```

### 2.2 依赖关系

计算核心允许依赖：

- `CrystalProfile`
- `CrystalConfig`
- `CrystalWorkingGeometry.ResolveConoscopic`
- 独立 `CrystalPhysicalCore`
- Unity 数学类型：`Vector2`、`Vector3`、`Vector4`、`Matrix4x4`、`Color`

计算核心不得依赖：

- `CrystalRuntime`
- Scene2 中的 `CrystalControllerWrapper`
- Scene2 的 `ConoscopicTextureRenderer` 实例
- 任何未定 UI 控件、场景对象路径或 Mesh 渲染组件

---

## 3. 接口契约

### 3.1 参数类型

`ConoscopicIntensityParameters` 应至少包含：

| 参数 | 默认值 | 约束 |
|------|--------|------|
| `resolution` | 实现阶段评估 `128` 或 `256` | 最小建议 `16`，运行时 clamp 到有效范围 |
| `fov` | `10f` | 与现有 renderer 一致，clamp 到 `[1, 120]` |
| `phaseScale` | `0.1f` | 最小 `0.01f` |
| `displayGamma` | `1.25f` | 最小 `0.1f` |
| `blackCutoff` | `0.012f` | clamp 到 `[0, 0.25]` |
| `ringSharpness` | `1f` | 最小 `0.01f` |
| `crossWidth` | `0.16f` | clamp 到 `[0.001, 0.9]` |
| `initialMelatopeOffset` | `(0.035, -0.025)` | 每轴 clamp 到 `[-0.25, 0.25]` |
| `laserColor` | `Color.red` | 作为显示元数据保留，第一版不参与强度输出 |
| `crystalRotation` | `(0, 0)` | 新场景本地独立旋转 |

### 3.2 结果类型

`ConoscopicIntensityResult` 应提供：

- `int Resolution`
- `float[] Intensities`，长度为 `resolution * resolution`
- 可选 `Vector2[] Coordinates`，或通过索引和分辨率推导 `x/y`
- `CrystalProfile Profile`
- `ConoscopicIntensityParameters ParametersSnapshot`
- `bool IsValid`

网格坐标规则：

```text
x = lerp(-1, 1, i / (resolution - 1))
y = lerp(-1, 1, j / (resolution - 1))
z = Intensities[j * resolution + i]
```

圆孔外点保留在矩阵中，`z = 0`。

### 3.3 核心组件 API

`ConoscopicIntensityCore` 建议提供：

```csharp
public ConoscopicIntensityResult Result { get; }
public ConoscopicIntensityParameters Parameters { get; }
public bool IsReady { get; }
public event Action<ConoscopicIntensityResult> OnIntensityUpdated;

public void Initialize(CrystalPhysicalCore physicalCore = null);
public void SetProfile(CrystalProfile profile);
public void SetParameters(ConoscopicIntensityParameters parameters);
public void SetResolution(int resolution);
public void SetCrystalRotation(Vector2 rotation);
public void SetDisplayMapping(float displayGamma, float blackCutoff, float ringSharpness, float crossWidth);
public void MarkDirty();
public void RecalculateIfDirty();
public void ForceRecalculate();
```

---

## 4. 计算管线

### 4.1 初始化

1. 调度脚本创建或引用 `ConoscopicIntensityCore`。
2. Core 初始化独立 `CrystalPhysicalCore`：
   - 如果外部注入实例，则使用注入实例。
   - 如果未注入，则在自身 GameObject 上添加或获取一个独立实例。
3. 外部注入默认 `CrystalProfile`。
4. Core 使用本地参数生成 `CrystalConfig`。

### 4.2 物理配置

Core 每次 Profile、旋转或光路相关参数变化时，按以下规则配置物理核心：

```text
CrystalWorkingGeometry.ResolveConoscopic(profile, Vector3.forward)
  -> CrystalConfig {
       profile = profile,
       crystalRotation = Quaternion.Euler(rotation.x, rotation.y, 0),
       localEField = geometry.LocalEFieldDirection,
       probeFieldDirection = geometry.ProbeFieldDirection,
       worldLightDirection = geometry.WorldLightDirection
     }
  -> CrystalPhysicalCore.ApplyConfig(config)
```

KTP 等特殊工作几何继续由 `CrystalWorkingGeometry.ResolveConoscopic` 统一处理。

### 4.3 CPU 采样

每个网格点执行：

1. 将 `x/y` 视场坐标映射到 `p = (x, y)`。
2. 计算 `radius = length(p)` 和 aperture；圆孔外返回 `0`。
3. 使用 `fov` 得到 `halfSize = tan(radians(fov) * 0.5)`。
4. 得到视图空间光线 `rayView = normalize(float3(p.x * halfSize, p.y * halfSize, 1))`。
5. 使用 `ShaderWorldToPrincipalMatrix` 得到主轴空间方向。
6. 复刻 shader 的 Fresnel 求解，得到 `delta_n`。
7. 计算 `gamma = ((2*pi*pathLength*delta_n)/wavelength) * phaseScale`。
8. 计算单轴或双轴消光图样。
9. 使用有限差分估算 `gammaWidth`，复刻 `ringPattern`。
10. 叠加 aperture、display mapping，输出 `0-1` 归一化强度。

### 4.4 shader 复刻要点

CPU 逻辑必须对齐以下 shader 行为：

- `SolveFresnel` 的二次方程形式。
- `DeriveOpticAxisView` 与 `DeriveBiaxialAxesView` 的轴向推导。
- 单轴 fallback：`_BiaxialAxesView` 近零时使用 `_OpticAxisView`。
- 双轴路径：两个 axis 分别投影 melatope，取两个 extinction pattern 的 `min`。
- `ClampBiaxialMelatopeOffset` 的最大半径 `0.42`。
- aperture 使用 `smoothstep(0.96, 1.0, radius)`。
- display shaping 顺序为 `saturate -> smoothstep(blackCutoff, 1) -> pow(1/displayGamma)`。

### 4.5 `fwidth(gamma)` 有限差分

CPU 没有 GPU 屏幕导数，建议分两步计算：

1. 第一遍为所有有效网格点计算并缓存 `gamma`、`crossPattern`、`aperture`。
2. 第二遍用相邻点估算：

```text
gammaDx = abs(gamma[x + 1, y] - gamma[x - 1, y]) * 0.5
gammaDy = abs(gamma[x, y + 1] - gamma[x, y - 1]) * 0.5
gammaWidth = max(gammaDx + gammaDy, 0.0001)
```

边界点使用最近有效邻居或单边差分。圆孔外点参与强度输出为 `0`，但不应污染圆内边界的 `gammaWidth`；边界差分优先取圆内有效邻居。

---

## 5. Dirty 更新策略

- Profile、分辨率、FOV、phaseScale、display mapping、melatopeOffset、晶体旋转变化时标记 dirty。
- `Update()` 中可调用 `RecalculateIfDirty()`，也允许调度脚本手动调用。
- 参数未变化时不得重复分配网格数组。
- 分辨率不变时复用 `float[]` 缓冲；分辨率变化时重新分配。
- 重算成功后更新 `Result` 快照，并触发 `OnIntensityUpdated(Result)`。
- Profile 为空或物理核心未就绪时，`Result.IsValid = false`，不抛异常。

---

## 6. 分阶段开发任务

### 6.1 阶段一：数据契约与 Core 骨架

- 新增参数、结果、核心组件类。
- 实现参数默认值、clamp、dirty 标记、结果数组复用。
- 实现 Profile 注入、本地晶体旋转和独立 `CrystalPhysicalCore` 初始化。

### 6.2 阶段二：shader 等价 CPU 计算

- 移植 Fresnel、optic axis、biaxial axes、melatope、aperture 和 display shaping 逻辑。
- 实现 `gamma` 缓存与有限差分 `gammaWidth`。
- 保证输出强度范围稳定在 `[0, 1]`。

### 6.3 阶段三：编辑器验证与调试入口

- 增加 ContextMenu 调试入口，例如 `Force Recalculate`、`Print Status`。
- 输出核心状态：Profile、resolution、参数快照、强度 min/max。
- 如后续需要，可增加临时 Texture2D 预览方法，但不能成为核心依赖。

### 6.4 阶段四：测试与文档回填

- 增加 Editor 测试或菜单测试，覆盖参数、网格、圆孔外强度、dirty 行为。
- 与当前 shader 做少量抽样对照，确认主要图样一致。
- 实现完成后新增 DevLog，记录实际取舍、性能数据和偏差说明。

---

## 7. 测试计划

### 7.1 数据契约测试

| 测试项 | 预期 |
|--------|------|
| 默认参数初始化 | Core 可创建有效参数对象，未注入 Profile 时 Result 无效但不报错 |
| 注入 Profile 后计算 | Result 有效，数组长度为 `resolution * resolution` |
| 坐标范围 | 推导出的 `x/y` 范围为 `[-1, 1]` |
| 强度范围 | 所有 intensity 位于 `[0, 1]` |
| 圆孔外点 | `radius >= 1` 区域输出 `0` |

### 7.2 参数更新测试

验证以下 setter 均会标记 dirty 并在重算后触发 `OnIntensityUpdated`：

- `SetResolution`
- `SetProfile`
- `SetCrystalRotation`
- `SetDisplayMapping`
- `SetParameters`
- `SetFOV` / `SetPhaseScale` 若实现为单独 setter

### 7.3 图样行为测试

- 修改 `fov` 后，图样空间映射发生变化。
- 修改 `phaseScale` 后，环纹密度或相位表现发生变化。
- 修改 `displayGamma`、`blackCutoff`、`ringSharpness`、`crossWidth` 后，强度 shaping 发生变化。
- LiNbO3/KDP 等单轴晶体走单 melatope 路径。
- KTP 等双轴晶体走双 melatope 路径。

### 7.4 独立性测试

- 不设置 `CrystalRuntime`，仅手动注入 Profile 和独立物理核心，计算仍可执行。
- 新核心切换 Profile 不改变 `CrystalSelectionData.SelectedProfile`。
- 不引用 Scene2 中任何具体 GameObject 或 UI 路径。

### 7.5 shader 对照测试

对同一 Profile、FOV、phaseScale 和 display 参数：

- 从 CPU 结果抽样中心、四象限、环纹区域、圆孔边缘点。
- 与当前 shader 渲染结果做视觉或数值趋势对照。
- 高频环纹允许存在有限差分与 GPU `fwidth` 的细节偏差。

---

## 8. 验收标准

1. 新增计算核心可在空场景中通过注入 `CrystalProfile` 独立输出光强网格。
2. 输出网格尺寸、坐标推导、强度范围满足 PRD 契约。
3. 参数变化触发 dirty 重算和更新事件。
4. 单轴/双轴晶体分别表现出对应 melatope 和消光刷路径。
5. 圆孔外强度恒为 `0`。
6. 不依赖 Scene2 运行时对象、UI、Mesh 或 `CrystalRuntime`。
7. 与现有 `ConoscopicInterference.shader` 在主要图样上保持一致。

---

## 9. 后续接入边界

后续 UI/调度脚本只应通过公开 API 使用核心：

- 进入新场景时注入默认晶体 Profile。
- UI 参数控件调用核心 setter。
- 曲面渲染监听 `OnIntensityUpdated` 并消费 `ConoscopicIntensityResult`。
- 双击跳转、相机控制、曲面 Mesh 构建和颜色映射均独立于本核心开发。

如果后续需要 RGB 输出或热力图，应新增显示映射层，不改变本阶段 `intensity` 作为基础 z 值的契约。
