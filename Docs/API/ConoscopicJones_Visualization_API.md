# Conoscopic Jones 3D Visualization API

> 用于后续“附加实验”新场景迁移。目标是把 Jones/KTP 会聚偏光 3D 可视化作为一个计算与显示服务接入新 UI：用户只调物理实验参数，可视化参数由面板或场景预设统一管理。

## 1. 设计边界

当前实现分为三层：

| 层级 | 当前脚本 | 职责 | 新场景建议 |
| --- | --- | --- | --- |
| 参数模型 | `ConoscopicJonesParameters` | 保存 Jones 计算参数、KTP 论文预设、Clamp 规则 | 继续使用，但新 UI 不直接暴露全部字段 |
| 计算核心 | `ConoscopicJonesGpuCore` | 接收 profile/parameters，上传 shader，输出 `RenderTexture` 和 min/max | 作为新场景的主 API 入口 |
| 演示 3D 面 | `ConoscopicJonesSurfaceVisualizer` | 调试面板、读取纹理、生成彩色高度网格 | 新场景可复用网格生成逻辑，但调试面板应替换 |

新附加实验 UI 的原则：

- 面向学生/用户：只展示“实验物理参数”。
- 面向开发/面板预设：锁定“可视化表现参数”，不让用户误以为这些是晶体物理量。
- `KTP_Profile.asset` 不作为论文 1# 厚度/波长的全局修改来源；论文预设应通过运行时参数覆盖。
- 计算输出保持单一入口：`ConoscopicJonesGpuCore.SetParameters(...)` + `ForceRecalculate()`。

## 2. 推荐对外 API

建议新场景不要直接让 UI 修改 `ConoscopicJonesParameters` 的任意字段，而是加一个轻量 facade，例如：

```csharp
public sealed class ConoscopicPaperKtpVisualizationApi : MonoBehaviour
{
    public ConoscopicJonesGpuCore Core { get; }
    public ConoscopicJonesResult Result { get; }

    public void LoadKtpPaper1Preset();
    public void ApplyPhysicalParameters(ConoscopicPhysicalUiParameters physical);
    public void ApplyVisualizationPreset(ConoscopicVisualizationPreset preset);
    public void Recalculate();
}
```

其中 UI 只绑定：

```csharp
public struct ConoscopicPhysicalUiParameters
{
    public float wavelengthNm;
    public float thicknessMm;
    public float alphaDeg;
    public float polarizerDeg;
    public float analyzerDeg;
    public float thetaDeg;
    public float phiDeg;
    public float apertureRadius;
}
```

可视化预设由场景或面板内部持有：

```csharp
public struct ConoscopicVisualizationPreset
{
    public ConoscopicBiaxialDisplayMode displayMode;
    public int resolution;
    public float screenDistanceM;
    public float screenHalfSizeM;
    public float initialIntensity;
    public float phaseAntiAliasStrength;
    public float phaseScale;
    public float ringSharpness;
    public float crossWidth;
    public float blackCutoff;
    public float displayGamma;
    public float surfaceSize;
    public float surfaceHeightScale;
    public bool normalizeDisplayIntensity;
}
```

## 3. 用户可调物理参数

这些参数应出现在新附加实验 UI 中，属于实验条件或可被解释为真实光学设置。

| UI 名称 | 字段 | 推荐默认 | 推荐范围 | 说明 |
| --- | --- | ---: | ---: | --- |
| 晶体样品 | `CrystalProfile` | KTP | 固定/下拉 | 附加实验若只做论文 1#，可固定 KTP；如保留扩展，可提供 KTP/LiNbO3 切换 |
| 波长 λ | `wavelengthNm` | `589.3` | `400-800 nm` | 论文 1# 条件为钠光 589.3 nm |
| 厚度 d | `thicknessMm` | `0.915` | `0.1-5 mm` | 论文 1# KTP 样品厚度；不建议沿用调试默认 20 mm |
| 晶片旋转角 α | `crystalAxisAngleDeg` | `45` | `0-180 deg` | 论文对比度较好的展示条件；UI 可提供 `α=0`、`α=45` 快捷按钮 |
| 起偏器角 | `polarizerAngleDeg` | `0` | `0-180 deg` | 对应偏振片方向 |
| 检偏器角 | `analyzerAngleDeg` | `90` | `0-180 deg` | 默认正交偏振；可提供“一键正交” |
| 样品倾角 θ | `paperThetaDeg` | `0` | `0-90 deg` | 论文 1# 默认 `θ=0`；建议默认隐藏到“高级物理参数” |
| 方位角 φ | `paperPhiDeg` | `0` | `0-360 deg` | 论文 1# 默认 `φ=0`；建议默认隐藏到“高级物理参数” |
| 通光孔径 | `apertureRadius` | `1.0` | `0.05-1.0` | 表示观察视场圆孔范围；可作为物理视场控制 |

### 可选高级物理参数

以下字段是真实材料或电光相关参数，但普通实验界面不建议直接暴露，避免破坏论文预设：

| 字段 | 默认来源 | 建议 |
| --- | --- | --- |
| `principalIndexNx` / `principalIndexNy` / `principalIndexNz` | `KTP_Profile.asset` | 只读显示；需要材料教学时再开放 |
| `ordinaryIndexNo` / `extraordinaryIndexNe` | 由主折射率推导 | 双轴 KTP 场景中只读 |
| `electricFieldStrength` | `0` | 本附加实验若不讲电光效应，隐藏 |
| `electroOpticCoefficientR22` | Profile | 本附加实验若不讲电光效应，隐藏或只读 |
| `forceUniaxial` / `uniaxialEpsilon` | 内部分类 | 不暴露 |
| `opticAxisTiltDeg` / `opticAxisAzimuthDeg` | 普通物理核心使用 | Paper KTP 1# 模式下不作为主入口，避免和 `theta/phi` 混淆 |

## 4. 面板预设的可视化参数

这些参数主要控制“像不像论文图”和 3D 展示稳定性，不应在学生实验 UI 中作为可调项出现。建议由场景 preset、难度档位或隐藏调试面板管理。

| 参数 | 字段 | Paper KTP 1# 推荐值 | 原因 |
| --- | --- | ---: | --- |
| 显示模式 | `biaxialDisplayMode` | `PaperKtp1` | 使用论文特征模式，而不是 Raw Jones |
| 计算分辨率 | `resolution` | `256` | 兼顾条纹清晰度与性能 |
| 屏幕距离 | `screenDistanceM` | `0.70` | 当前论文模式调校基准 |
| 屏幕半宽 | `screenHalfSizeM` | `0.08` | 控制视场角和双光轴间距表现 |
| 初始光强 | `initialIntensity` | `1.0` | 固定强度基准 |
| 相位抗锯齿 | `phaseAntiAliasStrength` | `1.0` | 抑制高频条纹闪烁 |
| 论文相位比例 | `phaseScale` | `1.0` | 论文模式不使用 20 mm 调试补偿 |
| 环纹锐度 | `ringSharpness` | `1.6` | 控制等色线清晰度 |
| 暗带宽度 | `crossWidth` | `0.08` | 控制 isogyre 暗带宽度 |
| 黑位裁切 | `blackCutoff` | `0.01` | 保持暗纹低高度/深蓝 |
| 显示 Gamma | `displayGamma` | `1.15` | 稳定亮区对比度 |
| 初始光轴偏移 | `initialMelatopeOffset` | `(0, 0)` | 论文 1# 默认不人为偏移 |
| 3D 平面尺寸 | `_surfaceSize` | `5.0` | 纯显示尺度 |
| 3D 高度倍率 | `_heightScale` | `0.05-1.6` | 建议按场景镜头预设，不给用户随意调 |
| 强度归一化 | `_normalizeDisplayIntensity` | `false` | 避免弱噪声被放大成假山峰 |
| Paper 平滑 | `SmoothDisplayValues` | 开启 | Paper 模式内部固定使用，减少尖刺 |
| 热力颜色 | `EvaluateHeatColor` | 固定 | 保持蓝-青-绿-黄-红映射一致 |

## 5. 推荐默认预设

### Paper KTP 1# 默认物理 preset

调用：

```csharp
parameters.ApplyPaperKtp1Preset(resetIndices: false);
```

等价关键参数：

| 字段 | 值 |
| --- | ---: |
| `wavelengthNm` | `589.3` |
| `thicknessMm` | `0.915` |
| `crystalAxisAngleDeg` | `45` |
| `paperThetaDeg` | `0` |
| `paperPhiDeg` | `0` |
| `polarizerAngleDeg` | `0` |
| `analyzerAngleDeg` | `90` |
| `biaxialDisplayMode` | `PaperKtp1` |

### Paper KTP 1# 视觉 preset

```csharp
parameters.screenDistanceM = 0.7f;
parameters.screenHalfSizeM = 0.08f;
parameters.initialIntensity = 1f;
parameters.phaseAntiAliasStrength = 1f;
parameters.phaseScale = 1f;
parameters.ringSharpness = 1.6f;
parameters.crossWidth = 0.08f;
parameters.blackCutoff = 0.01f;
parameters.displayGamma = 1.15f;
parameters.initialMelatopeOffset = Vector2.zero;
normalizeDisplayIntensity = false;
```

## 6. 新 UI 到核心的调用流程

推荐流程：

1. 场景加载时取得 `CrystalProfile`，优先使用 KTP profile。
2. 调用 `core.SetProfile(ktpProfile)`，让核心加载 profile 折射率。
3. 创建 `ConoscopicJonesParameters p = new ConoscopicJonesParameters(core.Parameters)`。
4. 调用 `p.ApplyPaperKtp1Preset(false)`。
5. 用 UI 的物理参数覆盖 `p.wavelengthNm`、`p.thicknessMm`、`p.crystalAxisAngleDeg`、`p.polarizerAngleDeg`、`p.analyzerAngleDeg`、`p.paperThetaDeg`、`p.paperPhiDeg`、`p.apertureRadius`。
6. 用面板内部 preset 覆盖所有可视化参数。
7. 若处于 `PaperKtp1`，重新生成取向矩阵：

```csharp
p.worldToPrincipalMatrix = ConoscopicJonesParameters.CreatePaperKtp1WorldToPrincipalMatrix(
    p.crystalAxisAngleDeg,
    p.paperThetaDeg,
    p.paperPhiDeg);
```

8. 调用：

```csharp
p.Clamp();
core.SetParameters(p);
core.ForceRecalculate();
```

9. 读取：

```csharp
RenderTexture texture = core.IntensityHeightMap;
ConoscopicJonesResult result = core.Result;
```

## 7. 参数分层建议

新 UI 可以分成三组：

### 基础实验参数

- 波长 λ
- 厚度 d
- 晶片旋转角 α
- 起偏器/检偏器角
- `α=0`、`α=45` 快捷按钮
- 重算按钮

### 高级实验参数

- θ
- φ
- 通光孔径
- 折射率只读信息：`nx, ny, nz`

### 隐藏开发调试

- Raw Jones / Paper KTP 1# display mode
- screen distance / screen half size
- phase scale / ring sharpness / cross width / black cutoff / gamma
- normalize display
- resolution
- height scale / surface size / view yaw

## 8. 当前字段迁移表

| 当前调试面板字段 | 分类 | 新 UI 建议 |
| --- | --- | --- |
| `Wavelength nm` | 物理 | 显示并可调 |
| `Thickness mm` | 物理 | 显示并可调 |
| `no/ne` | 材料派生 | 隐藏或只读 |
| `nx/ny/nz` | 材料物理 | 默认只读 |
| `Screen m` | 可视化/观察几何 | 预设 |
| `Screen Half m` | 可视化/观察视场 | 预设，必要时高级隐藏 |
| `I0` | 可视化强度基准 | 预设 |
| `AA Strength` | 可视化抗锯齿 | 预设 |
| `Phase Scale` | 可视化/半物理补偿 | 预设 |
| `Ring Sharp` | 可视化条纹锐度 | 预设 |
| `Cross Width` | 可视化暗带宽度 | 预设 |
| `Black Cutoff` | 可视化黑位 | 预设 |
| `Display Gamma` | 可视化色调 | 预设 |
| `Polarizer` | 物理 | 显示并可调 |
| `Analyzer` | 物理 | 显示并可调 |
| `Alpha` | 物理 | 显示并可调 |
| `Theta` | 物理高级 | 默认隐藏，可高级展开 |
| `Phi` | 物理高级 | 默认隐藏，可高级展开 |
| `Optic Tilt/Azimuth` | 旧物理核心取向 | Paper 1# 中隐藏 |
| `E Field/r22` | 电光物理 | 本实验隐藏 |
| `Aperture` | 物理/视场 | 可调或高级 |
| `Resolution` | 可视化质量 | 预设 |
| `Height` | 3D 显示 | 预设 |
| `View Yaw` | 相机/展示 | 新场景相机控制处理 |
| `Normalize Display` | 可视化映射 | Paper 模式固定 false |

## 9. 注意事项

- `PaperKtp1` 当前仍是“论文特征相似”的半物理显示模式，不是论文完整边界条件和振幅求解。
- 新 UI 不应把 `phaseScale`、`ringSharpness`、`crossWidth` 这类参数命名成物理量，它们用于视觉匹配。
- 若后续实现论文完整模型，可以保持 `ConoscopicPhysicalUiParameters` 不变，只替换 facade 内部对 `ConoscopicJonesParameters` 和 shader 的映射。
- 若要让不同实验共享可视化，可把 `ConoscopicVisualizationPreset` 做成 `ScriptableObject`，新场景只引用 `PaperKtp1Preset.asset`。

