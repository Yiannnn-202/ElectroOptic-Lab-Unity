# 锥光干涉图 Jones 光强曲面 GPU 算法开发计划

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档日期 | 2026-05-13 |
| 关联 PRD | `Docs/PRD/PRD_Conoscopic_IntensitySurface_CalcCore.md` |
| 关联旧计划 | `Docs/Plan/Conoscopic_IntensitySurface_CalcCore_Development_Plan.md` |
| 目标模块 | `ElectroOptics.ConoscopicAnalysis` |
| 第一版范围 | 单轴晶体 Jones 光强计算、GPU 高度纹理输出、CPU 参考验证、测试场景要求 |
| 不包含 | UI 设计、双击跳转、新场景完整调度、最终 Mesh 曲面渲染、双轴晶体 Jones/Berreman 完整模型 |

---

## 1. 开发目标

当前光强曲面计算核心以复刻 `ConoscopicInterference.shader` 的经验显示逻辑为主，适合快速得到与 Scene2 二维图样一致的结果，但对于参考视频中更平滑、物理参数更明确的三维光强曲面仍显单薄。

本计划用于指导下一阶段算法升级：新增一套并行存在的物理模式，以单轴晶体 Jones 偏振传播模型为第一版核心。运行时主路径使用 GPU Shader 生成归一化光强高度纹理，后续三维曲面系统只需要采样该纹理即可得到 `z = intensity` 的高度。

完成后应具备：

- 可通过实验参数直接控制锥光干涉图样，而不是只调显示 shaping 参数。
- 默认正交偏振下生成稳定的同心等色环与黑十字/消光刷结构。
- 运行时输出 `RenderTexture` 形式的光强高度纹理，强度范围稳定在 `[0, 1]`。
- 保留当前 shader-equivalent 计算核心，与 Jones/Physical 模式并行，便于对照、回退和教学切换。
- 提供 CPU 小型参考计算器，用于单点和低分辨率抽样验证 GPU 结果趋势。

---

## 2. 模块设计

### 2.1 建议新增职责

建议继续放在现有命名空间：

```csharp
namespace ElectroOptics.ConoscopicAnalysis
```

建议新增或等价拆分以下职责：

| 模块 | 职责 |
|------|------|
| `ConoscopicJonesParameters` | 保存物理实验参数、分辨率、观察范围和参数 clamp 规则 |
| `ConoscopicJonesGpuCore` | Unity 运行时入口，管理 Shader/Material、RenderTexture、参数上传、dirty 更新和结果事件 |
| `ConoscopicJonesCpuReference` | 轻量 CPU Jones 参考实现，仅用于测试抽样和算法对照，不作为运行时主路径 |
| `ConoscopicJonesResult` | 保存输出纹理、参数快照、强度范围和有效状态 |

不要求第一版替换现有 `ConoscopicIntensityCore`。更稳妥的方式是新增 Jones/Physical 模式，并在未来调度层中提供模式选择：

```text
ShaderEquivalentMode  -> 当前 CPU/经验 shader 复刻逻辑
JonesPhysicalMode     -> 新增 GPU Jones 物理逻辑
```

### 2.2 依赖边界

允许依赖：

- Unity `Shader`、`Material`、`RenderTexture`、`Texture2D` 读回工具。
- `CrystalProfile` 作为进入新实验时的默认晶体来源。
- 现有晶体选择数据中的基础折射率、波长、长度字段，前提是进入新场景后参数副本独立维护。

不得依赖：

- Scene2 的具体 GameObject 路径、UI 控件或调度脚本。
- `CrystalRuntime` 的运行时实例状态。
- 当前测试场景里的临时可视化控制器。
- 最终三维 Mesh 曲面实现。

---

## 3. 参数契约

第一版参数以实验物理量为主，字段命名建议如下：

| 参数 | 默认值建议 | 说明 |
|------|------------|------|
| `resolution` | `256`，可评估 `128` | 输出高度纹理分辨率，运行时可 clamp 到有效范围 |
| `wavelengthNm` | 来自 Profile 或 `632.8` | 激光波长，单位 nm |
| `thicknessMm` | 来自 Profile 或实验默认值 | 晶片厚度，单位 mm |
| `ordinaryIndexNo` | 来自 Profile | o 光折射率 |
| `extraordinaryIndexNe` | 来自 Profile | e 光折射率 |
| `screenDistanceM` | `0.7` | 晶片出射面到观察屏距离，影响屏幕点到光线角度的映射 |
| `initialIntensity` | `1.0` | 入射光强归一化系数 |
| `polarizerAngleDeg` | `0` | 起偏器方向 |
| `analyzerAngleDeg` | `90` | 检偏器方向，默认与起偏器正交 |
| `crystalAxisAngleDeg` | `45` | 晶体主轴在观察平面内的角度 |
| `electricFieldStrength` | `0` | 外加电场强度，第一版默认不改变折射率 |
| `electroOpticCoefficientR22` | 来自 Profile 或 `0` | 电光系数接口预留 |
| `apertureRadius` | `1.0` | 归一化圆孔半径，圆孔外输出 `0` |
| `heightScale` | `1.0` | 仅作为后续曲面高度缩放元数据，不改变基础强度 |

参数规则：

- `polarizerAngleDeg` 和 `analyzerAngleDeg` 必须可调，但默认采用 `0° / 90°` 正交偏振。
- `electricFieldStrength != 0` 时，第一版只保证参数可传递、dirty 可触发、结果仍有限；完整电光扰动公式作为后续阶段补充。
- 从 `CrystalProfile` 读取的值进入 Jones 参数后应成为本地副本，后续新场景内调参不得回写 Scene2 或全局晶体选择。

---

## 4. GPU Jones 计算管线

### 4.1 数据流

```text
CrystalProfile / 默认实验参数
  -> ConoscopicJonesParameters
  -> ConoscopicJonesGpuCore.UploadParameters()
  -> Jones 光强 Shader
  -> RenderTexture intensityHeightMap
  -> 后续曲面/预览系统采样
```

### 4.2 每像素计算步骤

GPU Shader 对每个纹理像素执行：

1. 将 UV 映射到归一化观察屏坐标 `p = (x, y)`，范围建议为 `[-1, 1]`。
2. 计算 `radius = length(p)`；若在圆孔外，输出 `0`。
3. 根据 `screenDistanceM` 与屏幕坐标构造入射光线方向 `rayDir`。
4. 将晶体光轴方向按 `crystalAxisAngleDeg` 旋转到当前观察坐标系。
5. 对单轴晶体计算该光线方向下的有效 e 光折射率 `nExtraEffective`，o 光使用 `ordinaryIndexNo`。
6. 计算相位延迟：

```text
delta = 2 * pi * thickness / wavelength * (nExtraEffective - ordinaryIndexNo) * pathFactor
```

7. 构造本征偏振方向，将入射 Jones 向量投影到 o/e 两个本征方向。
8. 对 e 分量施加 `exp(i * delta)` 相位，o 分量作为参考相位。
9. 将晶体输出 Jones 向量投影到检偏器方向。
10. 输出归一化光强：

```text
intensity = initialIntensity * |dot(analyzerVector, outputJones)|^2
```

11. 对结果执行 `saturate`，保证输出范围为 `[0, 1]`。

### 4.3 平滑度与显示策略

- 第一版高度纹理应优先使用 `256 x 256` 或更高分辨率验证，低配设备可降到 `128 x 128`。
- Shader 输出的是物理光强高度，不在核心层强行套彩虹色；伪彩色、法线、光照和高度缩放留给曲面渲染层。
- 可在测试场景中使用双线性采样与曲面法线平滑来获得接近参考图的连续表面。

---

## 5. CPU 参考验证

`ConoscopicJonesCpuReference` 只实现与 GPU Shader 同源的单点计算逻辑，用于少量抽样：

```csharp
public static float EvaluateIntensity(Vector2 normalizedPoint, ConoscopicJonesParameters parameters);
```

使用要求：

- CPU 参考不创建完整高分辨率运行时曲面。
- 测试可对中心、四象限、圆孔边缘等少量点进行 GPU 读回对照。
- GPU 与 CPU 浮点误差、插值差异允许存在；验收重点是范围有效、参数趋势一致、图样结构正确。

---

## 6. Dirty 更新与结果事件

建议 `ConoscopicJonesGpuCore` 提供：

```csharp
public ConoscopicJonesParameters Parameters { get; }
public ConoscopicJonesResult Result { get; }
public RenderTexture IntensityHeightMap { get; }
public bool IsDirty { get; }
public event Action<ConoscopicJonesResult> OnJonesIntensityUpdated;

public void SetProfile(CrystalProfile profile);
public void SetParameters(ConoscopicJonesParameters parameters);
public void SetResolution(int resolution);
public void MarkDirty();
public void RecalculateIfDirty();
public void ForceRecalculate();
```

更新规则：

- 任意物理参数、分辨率、晶体 Profile 副本变化后标记 dirty。
- dirty 时重新上传 Shader 参数并刷新 `RenderTexture`。
- 分辨率不变时复用 `RenderTexture`；分辨率变化时释放并重建。
- 刷新完成后更新 `ConoscopicJonesResult` 快照，并触发结果事件。
- Profile 为空、Shader 缺失或 RenderTexture 创建失败时，结果置为无效并记录清晰错误，不抛出未处理异常。

---

## 7. 分阶段开发任务

### 阶段一：参数与结果骨架

- 新增 Jones 参数、结果和 GPU Core 骨架。
- 实现 Profile 到本地实验参数的默认映射。
- 实现参数 clamp、dirty 标记、RenderTexture 生命周期管理。

### 阶段二：GPU Shader 单轴 Jones 模型

- 编写单轴晶体 Jones 光强 Shader。
- 实现正交偏振默认图样、圆孔外清零、强度归一化。
- 验证 `wavelengthNm`、`thicknessMm`、`ordinaryIndexNo`、`extraordinaryIndexNe` 改变后环纹趋势变化。

### 阶段三：CPU 参考与抽样测试

- 实现 CPU 单点参考计算器。
- 增加 GPU 读回少量采样点的 Editor 测试或菜单测试。
- 对中心、四象限、圆孔内外、参数变化前后进行趋势验证。

### 阶段四：测试场景接入要求

- 测试场景只用于观察高度纹理或临时曲面，不写死最终 UI。
- 面板参数应覆盖本计划中的实验参数，便于观察图样变化。
- 测试场景允许显示伪彩色和高度缩放，但这些属于可视化层，不得反向污染 Jones 核心强度输出。

---

## 8. 测试计划

### 8.1 数据契约测试

| 测试项 | 预期 |
|--------|------|
| 默认参数初始化 | 参数合法，默认起偏器 `0°`、检偏器 `90°` |
| 输出纹理创建 | 分辨率符合参数，格式支持线性强度读写 |
| 圆孔外采样 | 强度为 `0` |
| 强度范围 | GPU 读回样本均位于 `[0, 1]` |
| Profile 副本 | 新场景调参不回写 Scene2 或全局选择 |

### 8.2 物理图样测试

- 默认单轴晶体应出现同心等色环趋势与黑十字/消光刷。
- 增大 `thicknessMm` 或增大 `|ne - no|` 后，环纹密度应增加。
- 改变 `wavelengthNm` 后，环纹间距应发生可观察变化。
- 改变 `polarizerAngleDeg` / `analyzerAngleDeg` 后，亮暗分布应变化。
- 改变 `crystalAxisAngleDeg` 后，消光刷方向应旋转。

### 8.3 GPU/CPU 对照测试

- 对固定参数下的少量点执行 GPU 读回，与 CPU 参考结果比较。
- 允许设置较宽容差，重点保护趋势和范围，不追求逐像素一致。
- 当 Shader 改动导致趋势测试失败时，应先检查 Jones 公式、角度单位、波长/厚度单位换算和坐标映射。

---

## 9. 验收标准

1. 新增 Jones/Physical 模式与当前 shader-equivalent 模式并行保留。
2. GPU 核心可在无 Scene2 运行时对象的测试场景中输出有效高度纹理。
3. 默认正交偏振下可观察到单轴晶体锥光干涉的主要物理图样。
4. 实验参数变化会触发 dirty 更新，并使图样按物理趋势变化。
5. 输出高度纹理强度稳定在 `[0, 1]`，圆孔外为 `0`。
6. CPU 参考抽样与 GPU 输出在主要采样点趋势一致。
7. 本阶段不要求完成最终 UI、双击跳转和 Mesh 曲面渲染。

---

## 10. 风险与后续扩展

- Jones 单轴模型优先服务 LiNbO3 等单轴晶体；KTP 等双轴晶体需要后续扩展为双轴 Jones 近似或更完整的 Berreman/光线追迹模型。
- 电光效应第一版只预留参数，非零电场的折射率扰动公式需结合具体晶体坐标、张量方向和实验配置单独核对。
- 参考图的“平滑彩色山峰”还依赖曲面网格密度、插值、伪彩色、法线、灯光和相机角度；算法核心只负责输出可信的强度高度。
- 若后续需要严格物理精度，应补充单位测试、论文公式来源记录和多晶体 Profile 的标定数据。
