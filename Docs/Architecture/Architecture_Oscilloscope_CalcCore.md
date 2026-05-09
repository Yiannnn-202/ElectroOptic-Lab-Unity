# 示波器倍频实验计算核心 — 架构设计

## Context

电光调制实验需要一个示波器波形计算核心，根据输入的电压、晶体参数实时计算两条波形（AC电压 + 输出光强），用于展示倍频现象。该模块为新场景服务，遵循项目解耦原则，不修改任何现有代码。

---

## 模块结构

新增目录：`ElectroOptic-Lab/Assets/Scripts/Oscilloscope/`

```
Scripts/Oscilloscope/
├── OscilloscopeCore.cs            # 顶层编排器 (MonoBehaviour)
├── OscilloscopeCrystalBridge.cs   # CrystalPhysicalCore 包装器 (MonoBehaviour)
├── WaveformCalculator.cs          # 纯数学计算引擎 (Plain C#)
├── VpiCalculator.cs               # Vπ 计算静态工具 (Static Class)
├── OscilloscopeParameters.cs      # 输入参数数据容器
└── WaveformResult.cs              # 输出数据容器
```

命名空间：`ElectroOptics.Oscilloscope`

---

## 依赖关系

```
OscilloscopeCore (编排器, MonoBehaviour)
  ├── OscilloscopeParameters      (数据, Serializable)
  ├── WaveformResult              (数据)
  ├── WaveformCalculator          (纯数学, 无 Unity 依赖)
  ├── VpiCalculator               (静态方法)
  └── OscilloscopeCrystalBridge   (MonoBehaviour, 同 GameObject)
        └── CrystalPhysicalCore   (已有, 场景提供)
```

---

## 各类职责与 API

### 1. `OscilloscopeParameters` — 输入参数容器

`[Serializable] class`，可在 Inspector 中配置。

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `vDC` | float | 0 | DC 偏置电压 (V) |
| `vModulation` | float | 5 | AC 幅值 (V) |
| `frequency` | float | 1000 | AC 频率 (Hz) |
| `intensityMax` | float | 1 | 最大光强 I₀ |
| `compensatorPhase` | float | 0 | 补偿器相移 (rad)，预留 |
| `modulationMode` | ModulationMode | Transverse | 调制模式 |
| `fieldAxis` | ElectricFieldAxis | Z_Axis | 电场轴 |
| `sampleCount` | int | 1024 | 采样点数 |
| `displayPeriods` | float | 2 | 显示周期数 |

### 2. `WaveformResult` — 输出容器

```csharp
public class WaveformResult
{
    public float[] ch1;     // AC 电压波形
    public float[] ch2;     // 光强波形
    public double vPi;      // 当前 Vπ
    public double gamma0;   // 当前静态相位偏置

    public void EnsureSize(int count);  // 避免每帧 GC，仅尺寸变化时重新分配
}
```

### 3. `VpiCalculator` — 静态工具

复用 `LabController` 中的算法（第179-204行），提取为纯静态方法：

```csharp
public static class VpiCalculator
{
    public static double Calculate(
        double wavelength_nm, double length_mm, double thickness_mm,
        double sensitivity, ModulationMode mode);
    // Transverse: Vπ = (λ × d) / (2 × L × S_eff)
    // Longitudinal: Vπ = λ / (2 × S_eff)
    // sensitivity < 1e-20 → 返回 double.PositiveInfinity
}
```

### 4. `WaveformCalculator` — 纯数学引擎

无 Unity API 依赖，可独立单元测试。

```csharp
public class WaveformCalculator
{
    public void Compute(OscilloscopeParameters p, double vPi, WaveformResult result);
}
```

计算逻辑：
```
时间窗口 T = displayPeriods / frequency
dt = T / sampleCount
对每个采样点 i:
  t = i × dt
  ch1[i] = V_m × sin(2πf × t)
  V(t) = V_DC + ch1[i]
  Γ(t) = π × V(t) / Vπ + δ_compensator
  ch2[i] = I₀ × sin²(Γ(t) / 2)
```

- 中间计算使用 `double` 精度，仅输出时转 `float`
- 单次循环同时计算 Ch1 和 Ch2
- Vπ 无效 (Infinity/NaN) 时 ch2 全部置 0

### 5. `OscilloscopeCrystalBridge` — 物理核心包装器

遵循 `CrystalControllerWrapper` 的模式（见 `Scripts/Experiment/Controller/CrystalControllerWrapper.cs`）：

```csharp
public class OscilloscopeCrystalBridge : MonoBehaviour
{
    public void Initialize(CrystalPhysicalCore core);
    public void SetProfile(CrystalProfile profile);
    public float ConfigureAndGetSensitivity(ElectricFieldAxis axis, ModulationMode mode);
    public bool NeedsReconfigure(ElectricFieldAxis axis, ModulationMode mode);
    public float Sensitivity { get; }
    public CrystalProfile Profile { get; }
    public bool IsInitialized { get; }
}
```

构建 CrystalConfig 的方式：
- `crystalRotation` = `Quaternion.identity`（固定 0°）
- `worldLightDirection` = 默认 `Vector3.forward`（+Z）；KTP 由 `CrystalWorkingGeometry` 覆盖为 `Vector3.up`（+Y）
- `probeFieldDirection` = `fieldAxis` 对应单位向量（驱动 Probe Pass 计算 Sensitivity）
- `localEField` = 同上单位向量（Render Pass 需要，但输出我们不使用）

内部缓存 `_lastAxis` 和 `_lastMode`，避免参数未变时重复调用 DLL。

KTP 的 Vπ 无效问题不是 Profile 传递失败，而是几何不一致：Scene2 锥光图使用当前激光方向，而 Scene4 原先硬编码 `worldLightDirection = Vector3.forward` 且默认 `E = Z`。对 KTP 来说，`k=Z, E=Z` 会让有效灵敏度接近 0，`VpiCalculator` 因此返回无穷大并让 ch2 置 0。

修复后，Bridge 与 Scene2 共用 `CrystalWorkingGeometry`：

```text
普通晶体: k = 默认 +Z，E/probe = 用户请求轴，mode = 用户请求模式
KTP:      k = +Y，E/probe = +Z，mode = Transverse
```

`OscilloscopeCore` 使用 Bridge 暴露的实际调制模式计算 Vπ，因此 KTP 的示波器调制方向与锥光显示方向保持一致。这个规则只覆盖 KTP，不改变 LiNbO3 和 KDP 的既有行为，也不新增传播轴 UI。

### 6. `OscilloscopeCore` — 顶层编排器

```csharp
public class OscilloscopeCore : MonoBehaviour
{
    // 初始化
    public void Initialize(CrystalPhysicalCore core, CrystalProfile profile);

    // 参数设置（每个 setter 标记 dirty）
    public void SetVoltages(float vDC, float vModulation);
    public void SetFrequency(float frequency);
    public void SetModulationConfig(ModulationMode mode, ElectricFieldAxis axis);
    public void SetCompensatorPhase(float phase);
    public void SetIntensityMax(float i0);
    public void SetDisplayConfig(int sampleCount, float displayPeriods);
    public void MarkDirty();  // 强制下一帧重算

    // 输出
    public WaveformResult Result { get; }
    public OscilloscopeParameters Parameters { get; }
    public bool IsReady { get; }

    // 事件
    public event Action OnWaveformUpdated;
}
```

---

## 数据流（每帧 Update）

```
_isDirty == false → 跳过，不做任何计算
_isDirty == true →
  ┌─ Bridge.NeedsReconfigure(axis, mode)?
  │    ├─ yes → Bridge.ConfigureAndGetSensitivity()
  │    │          → 构建 CrystalConfig → ApplyConfig(DLL) → 读取 Sensitivity
  │    └─ no  → 使用缓存的 Bridge.Sensitivity
  │
  ├─ VpiCalculator.Calculate(λ, L, d, sensitivity, mode) → vPi
  │
  ├─ WaveformCalculator.Compute(params, vPi, result)
  │    → 填充 ch1[], ch2[], vPi, gamma0
  │
  └─ OnWaveformUpdated?.Invoke()
      → UI 读取 Result.ch1 / Result.ch2 渲染波形
```

**两级优化**：
1. **OscilloscopeCore 级**：dirty flag，参数未变时跳过全部计算
2. **Bridge 级**：axis/mode/profile 未变时跳过 DLL 调用，仅重算波形数学

---

## 初始化流程（由场景调度脚本负责）

```
1. 场景加载 → CrystalPhysicalCore 实例就绪
2. 读取 CrystalSelectionData.SelectedProfile → 获取 CrystalProfile
3. 调用 OscilloscopeCore.Initialize(core, profile)
4. 之后通过 setter 方法更新参数 → Update 自动重算
```

计算核心不负责查找 GameObject 或场景初始化。

---

## 关键复用

| 复用项 | 来源文件 | 复用方式 |
|--------|----------|----------|
| Vπ 计算公式 | `LabController.cs:179-204` | 提取为 `VpiCalculator.Calculate()` 静态方法 |
| Wrapper 模式 | `CrystalControllerWrapper.cs` | Bridge 同样 Initialize → BuildConfig → ApplyConfig |
| AxisToVector 映射 | `LabController.cs:206-215` | Bridge 内部实现相同的 switch |
| EOEnums | `EOEnums.cs` | 直接使用 `ModulationMode`, `ElectricFieldAxis` |
| CrystalSelectionData | `CrystalSelectionData.cs` | 场景调度脚本读取，传入 Initialize |

---

## 实现顺序

1. `OscilloscopeParameters.cs` + `WaveformResult.cs`（数据容器，无依赖）
2. `VpiCalculator.cs`（静态工具，无依赖）
3. `WaveformCalculator.cs`（纯数学，依赖 1+2）
4. `OscilloscopeCrystalBridge.cs`（依赖 CrystalPhysicalCore）
5. `OscilloscopeCore.cs`（组合所有模块）

---

## 验证方式

1. **编译验证**：创建完所有文件后在 Unity 中确认无编译错误
2. **倍频验证**：V_DC=0 时 ch2 频率应为 ch1 的 2 倍（对比波形零点数量）
3. **同频验证**：V_DC=Vπ/2 时 ch2 频率与 ch1 相同
4. **消光验证**：V_DC=0, V_m=0 时 ch2 全零
5. **Vπ 一致性**：相同参数下 VpiCalculator 输出应与 LabController 显示的 Vπ 一致
6. **GC 验证**：连续多帧调整参数，Profiler 中不应出现每帧 float[] 分配
