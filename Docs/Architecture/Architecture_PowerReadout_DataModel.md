# 光功率计读数与实验数据模型技术规范

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档名称 | 光功率计读数与实验数据模型技术规范 |
| 版本 | v0.1 待确认 |
| 创建日期 | 2026-05-22 |
| 对应 PRD | `Docs/PRD/PRD_PowerReadout_DataModel.md` |
| 范围 | 技术设计与实现规范，不包含本次代码实现 |

---

## 1. 设计目标

本规范定义一个统一的光功率读数计算模型，用于：

- `PowerReadoutController` 的光功率计面板显示
- `RecordManager` 或后续填表场景的数据记录
- 后续实验数据处理模块的输入数据

核心目标：

1. 统一面板显示值和填表记录值。
2. 将理论电光调制曲线、接收器对准效率、仪器零点/残余透过分层。
3. 分离“稳定记录值”和“面板显示噪声”。
4. 保持实现可测试、可配置、可逐步接入现有场景。

---

## 2. 建议模块划分

### 2.1 新增纯计算模块

建议新增：

```text
ElectroOptic-Lab/Assets/Scripts/Power/PowerReadoutCalculator.cs
```

或如果保持现有目录风格，也可放在：

```text
ElectroOptic-Lab/Assets/Scripts/PowerReadoutCalculator.cs
```

职责：

- 只做数值计算，不依赖 Unity 场景对象。
- 不读取输入、不更新 UI、不写表格。
- 所有方法应为确定性计算，除非显式传入噪声。

### 2.2 参数结构

建议定义可序列化参数结构：

```csharp
[System.Serializable]
public struct PowerReadoutParameters
{
    public float darkPower;
    public float powerScale;
    public float leakage;
    public float visibility;
    public float phaseOffset;
    public float halfWaveVoltage;
    public float beamFocus;
}
```

可选：

```csharp
[System.Serializable]
public struct PowerNoiseParameters
{
    public bool enabled;
    public float amplitude;
    public float frequency;
}
```

### 2.3 计算结果结构

建议输出：

```csharp
public struct PowerReadoutResult
{
    public float stablePower;
    public float displayPower;
    public float transmission;
    public float alignmentEfficiency;
}
```

字段含义：

| 字段 | 说明 |
|------|------|
| `stablePower` | 稳定读数，用于填表和数据处理 |
| `displayPower` | 面板显示读数，可叠加噪声 |
| `transmission` | 电光调制透过率 |
| `alignmentEfficiency` | 接收器对准效率 |

---

## 3. 公式定义

### 3.1 电光调制透过率

```text
phase = phaseOffset + pi * voltage / (2 * halfWaveVoltage)
transmission = leakage + visibility * sin²(phase)
```

约束：

```text
transmission = clamp01(transmission)
```

说明：

- `phaseOffset = 0` 表示零电压时接近消光。
- 若需要平行偏振片模式，后续可扩展为 `cos²` 或模式枚举，本阶段不强制。

### 3.2 接收器对准效率

```text
rSquared = receiverDevX² + receiverDevY²
alignmentEfficiency = exp(-beamFocus * rSquared)
```

约束：

```text
alignmentEfficiency = clamp01(alignmentEfficiency)
```

说明：

- 该项只影响整体接收功率。
- 不参与半波电压和相位周期计算。

### 3.3 稳定光功率读数

```text
stablePower = darkPower + alignmentEfficiency * powerScale * transmission
```

约束：

```text
stablePower = max(0, stablePower)
```

### 3.4 面板显示光功率

```text
displayPower = stablePower + displayNoise
```

其中 `displayNoise` 仅用于 UI 面板，不进入填表和数据处理。

建议噪声：

```text
displayNoise = (PerlinNoise(t * frequency, 0) - 0.5) * 2 * amplitude
```

最终：

```text
displayPower = max(0, displayPower)
```

---

## 4. API 规范

### 4.1 稳定值计算

```csharp
public static float CalculateStablePower(
    float voltage,
    float receiverDevX,
    float receiverDevY,
    PowerReadoutParameters parameters)
```

用途：

- 填表记录
- 后续数据处理
- 单元测试

要求：

- 不使用 `Time.time`
- 不使用随机数
- 相同输入必须得到相同输出

### 4.2 透过率计算

```csharp
public static float CalculateTransmission(
    float voltage,
    float halfWaveVoltage,
    float leakage,
    float visibility,
    float phaseOffset)
```

用途：

- 单独测试电光调制曲线
- 后续可给图表或调试面板显示

边界处理：

- `halfWaveVoltage <= 0` 时返回安全值，建议为 `0`
- 输入 NaN/Infinity 时返回安全值

### 4.3 对准效率计算

```csharp
public static float CalculateAlignmentEfficiency(
    float receiverDevX,
    float receiverDevY,
    float beamFocus)
```

用途：

- 接收器微调反馈
- 光功率计读数衰减

边界处理：

- `beamFocus < 0` 时按 `0` 处理或钳制
- 输出范围固定为 `[0, 1]`

### 4.4 面板显示值计算

```csharp
public static float AddDisplayNoise(
    float stablePower,
    float time,
    PowerNoiseParameters noise)
```

用途：

- 仅供面板显示

要求：

- 不用于填表。
- 可通过参数完全关闭。

---

## 5. 现有模块接入规范

### 5.1 `PowerReadoutController`

应改为：

- 保留接收器状态控制和 WASD 微调逻辑。
- 使用 `PowerReadoutCalculator` 计算 `stablePower` 和 `displayPower`。
- 面板显示 `displayPower`。
- 暴露当前 `stablePower` 给记录或调试模块使用。

建议新增只读属性：

```csharp
public float CurrentStablePower { get; private set; }
public float CurrentDisplayPower { get; private set; }
public float CurrentAlignmentEfficiency { get; private set; }
```

行为规范：

| 接收器状态 | 行为 |
|------------|------|
| `0` 关闭 | 面板隐藏或读数为 0 |
| `1` 监控 | 显示当前稳定读数，可叠加显示噪声 |
| `2` 微调 | WASD 改变 `receiverDevX/Y`，读数随 `eta_align` 变化 |

### 5.2 `RecordManager`

应改为：

- 不再内部维护独立的理想 `CalculateReceiverValue` 作为唯一数据源。
- 记录表写入 `stablePower`。
- 若没有可用 `PowerReadoutController`，可直接调用 `PowerReadoutCalculator` 计算稳定值。

推荐优先级：

1. 若场景中存在已启用的 `PowerReadoutController`，使用其 `CurrentStablePower`。
2. 否则使用 `RecordManager.currentVoltage` 和共享参数直接计算。

### 5.3 半波电压来源

半波电压 `Vpi` 保持现有来源：

- Scene3/填表场景可继续由 `Scene3CrystalBridge` 注入 `RecordManager.halfWaveVoltage`。
- 若未来统一配置，应将 `halfWaveVoltage` 放入共享参数，再由桥接模块写入。

### 5.4 电压来源

`PowerReadoutController` 若需要参与填表场景，应能获取当前实验电压。

建议方案：

- 短期：在 Inspector 中引用 `RecordManager`，读取 `currentVoltage` 和 `halfWaveVoltage`。
- 中期：定义只读接口 `IVoltageSource`，由填表/电压控制模块实现。

接口草案：

```csharp
public interface IVoltageSource
{
    float CurrentVoltage { get; }
    float HalfWaveVoltage { get; }
}
```

---

## 6. 参数默认值与 Inspector 映射

### 6.1 建议 Inspector 字段

| 字段 | 默认值 | 所属模块 |
|------|--------|----------|
| `darkPower` | `0.2f` | 计算参数 |
| `powerScale` | `198.5f` | 计算参数 |
| `leakage` | `0.01f` | 计算参数 |
| `visibility` | `0.99f` | 计算参数 |
| `phaseOffset` | `0f` | 计算参数 |
| `noiseAmplitude` | `0.2f` | 面板显示 |
| `noiseEnabled` | `true` | 面板显示 |
| `beamFocus` | 沿用现值 | 对准效率 |

### 6.2 兼容现有字段

现有 `PowerReadoutController.maxPowerReceiver` 可映射为 `powerScale`。

现有 `beamFocus` 保留语义不变。

现有 `receiverDevX/Y` 保留为内部运行状态。

---

## 7. 数据处理规范

后续实验数据处理只能使用稳定读数：

```text
stablePower
```

不得使用：

- `displayPower`
- `displayNoise`
- `receiverDevX/Y`
- 归一化理论强度，除非明确进入“理论演示模式”

极值分析要求：

- `P_min` 允许大于 0。
- 半波电压计算应主要使用极值位置的电压差，而不是假设最小值为 0。
- 消光比可使用：

```text
extinctionRatio = P_max / P_min
```

若 `P_min <= 0`，应提示无法可靠计算消光比。

调制度可使用：

```text
modulationDepth = (P_max - P_min) / (P_max + P_min)
```

---

## 8. 测试规范

### 8.1 纯计算测试

建议新增编辑器测试或菜单测试，覆盖：

| 测试项 | 输入 | 预期 |
|--------|------|------|
| 零电压消光 | `V=0, phi0=0, leakage=0` | `transmission=0` |
| 半波电压最大 | `V=Vpi, phi0=0, leakage=0` | `transmission=1` |
| 四分之一波中值 | `V=Vpi/2, phi0=0, leakage=0` | `transmission=0.5` |
| 消光残余 | `V=0, leakage=0.01` | `transmission=0.01` |
| 对准中心 | `receiverDevX=0, receiverDevY=0` | `eta_align=1` |
| 对准偏离 | `r² > 0` | `0 < eta_align < 1` |
| 无效 Vpi | `halfWaveVoltage<=0` | 不 NaN，不 Infinity |

### 8.2 场景行为测试

| 场景 | 操作 | 预期 |
|------|------|------|
| 光功率计关闭 | 接收器状态为 `0` | 面板隐藏或 0 |
| 监控模式 | 接收器状态为 `1` | 显示当前电压下读数 |
| 微调模式 | 接收器状态为 `2` 并按 WASD | 读数幅度变化 |
| 填表记录 | 点击记录 | 表格值等于稳定读数 |
| 噪声启用 | 面板观察 | 面板轻微抖动，表格不抖动 |

---

## 9. 迁移步骤建议

1. 新增纯计算模块和参数结构。
2. 为计算模块添加基础测试。
3. 将 `PowerReadoutController.CalculatePower` 替换为新模型。
4. 暴露 `CurrentStablePower`。
5. 将 `RecordManager` 的记录数据源改为稳定读数。
6. 手动验证 Scene2 光功率计面板。
7. 手动验证填表场景记录值和面板值一致。
8. 更新用户文档中的“光强/光功率”命名说明。

---

## 10. 风险与对策

| 风险 | 影响 | 对策 |
|------|------|------|
| 现有场景未给 PowerReadoutController 配电压来源 | 面板只显示默认电压读数 | 提供 fallback，默认 `V=0`，并输出 warning |
| 噪声进入记录表 | 数据处理不稳定 | 严格区分 `stablePower` 和 `displayPower` |
| UI 文案仍叫光强 | 学生混淆物理量 | 表格建议改为“光功率/uW”或补充说明 |
| `P_min > 0` 影响旧算法 | 后续处理假设最小值为 0 会出错 | 数据处理按极值位置求 Vpi，不依赖绝对零点 |
| 参数过多增加教学负担 | Inspector 配置复杂 | 默认折叠高级真实度参数，保留教学模式 |

---

## 11. 待确认技术决策

1. 是否新增 `Power` 子目录，还是保持脚本在 `Assets/Scripts` 根目录？
2. `RecordManager` 是否直接依赖 `PowerReadoutController`，还是通过接口解耦？
3. 默认模式采用“接近真实读数”还是“理想教学曲线”？
4. 表格是否记录 `stablePower` 的绝对单位 `uW`，还是归一化到 `0-100`？
5. 是否需要在 UI 中显示当前对准效率 `eta_align` 供调试使用？
