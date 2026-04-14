# 开发日志：示波器倍频实验计算核心

## 2026-03-29

### 完成内容

#### 1. 需求分析与核对

与用户逐步核对并确认了完整需求：

- **物理模型**：标准电光调制传递函数 `I(t) = I₀ × sin²(Γ(t)/2)`
- **电压模型**：`V(t) = V_DC + V_m × sin(2πft)`
- **相位延迟**：`Γ(t) = π × V(t) / Vπ + δ_compensator`
- **静态偏置 Γ₀**：仅通过 DC 电压控制，与场景2完全解耦，不考虑自然双折射
- **补偿器**：预留 `compensatorPhase` 接口，默认为 0
- **输出**：Ch1 = AC 电压波形（不含 DC），Ch2 = 光强波形
- **晶体角度**：XY 旋转固定 0°
- **晶体类型**：通过 `CrystalSelectionData.SelectedProfile` 获取
- **Vπ**：复用 LabController 算法，依赖新场景提供的 `CrystalPhysicalCore` 实例
- **更新策略**：参数变化时每帧全量重算，无滚动

#### 2. 文档输出

| 文档 | 路径 |
|------|------|
| PRD 需求文档 | `PRD_Oscilloscope_CalcCore.md` |
| 架构设计文档 | `Architecture_Oscilloscope_CalcCore.md` |

#### 3. 代码实现

新增目录 `ElectroOptic-Lab/Assets/Scripts/Oscilloscope/`，命名空间 `ElectroOptics.Oscilloscope`。

| 文件 | 类 | 职责 |
|------|-----|------|
| `OscilloscopeParameters.cs` | `OscilloscopeParameters` | 输入参数容器（[Serializable]） |
| `WaveformResult.cs` | `WaveformResult` | 输出容器（ch1, ch2, vPi, gamma0 + EnsureSize 防 GC） |
| `VpiCalculator.cs` | `VpiCalculator` | 静态 Vπ 计算工具（算法复用自 LabController:179-204） |
| `WaveformCalculator.cs` | `WaveformCalculator` | 纯数学波形引擎（无 Unity 依赖，double 精度中间计算） |
| `OscilloscopeCrystalBridge.cs` | `OscilloscopeCrystalBridge` | CrystalPhysicalCore 包装器（遵循 CrystalControllerWrapper 模式） |
| `OscilloscopeCore.cs` | `OscilloscopeCore` | 顶层编排器（dirty flag 两级优化 + 事件通知） |

#### 4. 测试脚本

| 文件 | 路径 |
|------|------|
| Editor 测试 | `Scripts/Oscilloscope/Editor/OscilloscopeCalcTests.cs` |

运行方式：Unity 菜单 `ElectroOptics → Tests → Run Oscilloscope Calc Tests`

10 项测试覆盖：

| # | 测试名 | 验证内容 |
|---|--------|----------|
| 1 | Vpi_Transverse | 横向 Vπ 公式与手算对比 |
| 2 | Vpi_Longitudinal | 纵向 Vπ 公式与手算对比 |
| 3 | Vpi_ZeroSensitivity | sensitivity=0 → Infinity |
| 4 | Extinction | V_DC=0, V_m=0 → ch2 全零 |
| 5 | FrequencyDoubling | V_DC=0 → ch2 频率 = 2× ch1（倍频核心验证） |
| 6 | SameFrequency | V_DC=Vπ/2 → ch2 与 ch1 同频 |
| 7 | MaxIntensity | V_DC=Vπ → I=I₀, Γ₀=π |
| 8 | InvalidVpi | Vπ=∞ → ch2 全零, ch1 正常 |
| 9 | EnsureSize_NoGC | 相同尺寸不重新分配数组 |
| 10 | Compensator | δ=π/2, V=0 → I=0.5×I₀ |

### 设计要点

- **解耦原则**：未修改任何现有代码，全部为新增文件
- **两级优化**：OscilloscopeCore 级 dirty flag + Bridge 级 DLL 调用缓存
- **可测试性**：WaveformCalculator 和 VpiCalculator 为纯 C#，不依赖 Unity API
- **GC 友好**：WaveformResult.EnsureSize 仅在尺寸变化时分配新数组

### 待办

- [ ] Unity 中编译验证
- [ ] 运行 Editor 测试确认全部通过
- [ ] 新场景搭建时编写场景调度脚本调用 `OscilloscopeCore.Initialize()`
- [ ] UI 层订阅 `OnWaveformUpdated` 事件渲染波形
