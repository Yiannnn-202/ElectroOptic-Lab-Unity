# Scene4 示波器调度与波形显示开发计划

## 文档信息

| 项目 | 内容 |
|------|------|
| 文档日期 | 2026-04-25 |
| 所属场景 | `Scene4_UIRebuild 1.unity` |
| 相关模块 | `ElectroOptics.Oscilloscope` |
| 目标 | 将示波器计算核心接入 Scene4 UI，并提供最小可用波形绘制 |

---

## 1. 开发目标

Scene4 已具备“交流调制法测半波电压”的 UI 框架，包括左侧电压/状态面板、右侧 CH1/CH2 波形观察区和关键点记录卡片。本次开发目标是在不重写现有计算核心的前提下，新增场景调度层，将 `OscilloscopeCore` 的 `ch1/ch2` 输出实时显示到 UI 中。

完成后应支持：

- A/D 键调节直流偏置电压 `V_DC`
- 重置按钮恢复初始电压
- 记录/清空按钮管理关键点卡片
- CH1 显示交流输入电压波形
- CH2 显示输出光强波形
- UI 根据当前计算结果显示状态和 Vπ 信息

---

## 2. 开发计划

### 2.1 预检与修复

- 检查 `OscilloscopeCrystalBridge.cs` 是否存在损坏字符串或编译阻塞。
- 仅修复编译错误，不改动 `OscilloscopeCore`、`WaveformCalculator`、`VpiCalculator` 的计算逻辑。
- 保持 `OscilloscopeCrystalBridge` 原有 API：
  - `Initialize(CrystalPhysicalCore core)`
  - `SetProfile(CrystalProfile profile)`
  - `NeedsReconfigure(ElectricFieldAxis axis, ModulationMode mode)`
  - `ConfigureAndGetSensitivity(ElectricFieldAxis axis, ModulationMode mode)`

### 2.2 新增波形绘制组件

新增文件：

`ElectroOptic-Lab/Assets/Scripts/Oscilloscope/OscilloscopeWaveformGraphic.cs`

职责：

- 继承 Unity UI `Graphic`
- 使用 `VertexHelper` 绘制折线
- 不依赖 `LineRenderer`、临时贴图或额外资源
- 暴露接口：
  - `SetSamples(float[] samples, float minY, float maxY)`
  - `SetColor(Color color)`
  - `Clear()`

绘制规则：

- CH1 使用固定范围 `[-vModulation, +vModulation]`
- CH2 使用固定范围 `[0, intensityMax]`
- 在目标 UI 区域内保留少量 padding，避免折线贴边

### 2.3 新增 Scene4 调度脚本

新增文件：

`ElectroOptic-Lab/Assets/Scripts/Oscilloscope/Scene4OscilloscopeDispatcher.cs`

职责：

- 挂载在 `GameManager`
- 禁用旧 `RecordManager`，避免 A/D 和按钮监听冲突
- 初始化并持有 `OscilloscopeCore`
- 订阅 `OscilloscopeCore.OnWaveformUpdated`
- 自动查找 Scene4 UI 引用，Inspector 引用作为兜底
- 将计算结果推送到 CH1/CH2 波形绘制组件
- 管理当前电压、状态文本、调制状态文本和关键点卡片

默认参数：

| 参数 | 默认值 |
|------|--------|
| `initialVdc` | `120f` |
| `voltageStep` | `0.5f` |
| `modulationAmplitude` | `5f` |
| `frequency` | `1000f` |
| `sampleCount` | `1024` |
| `displayPeriods` | `2f` |
| `modulationMode` | `Transverse` |
| `fieldAxis` | `Z_Axis` |
| `intensityMax` | `1f` |

Profile 选择策略：

1. 优先使用 `CrystalSelectionData.SelectedProfile`
2. 为空时使用 Inspector 中的 `fallbackProfile`
3. 仍为空时输出错误日志并跳过核心初始化

物理核心选择策略：

1. 优先使用 Inspector 中的 `physicalCore`
2. 为空时查找场景内 `CrystalPhysicalCore`
3. 仍为空时在 `GameManager` 上补加 `CrystalPhysicalCore`

### 2.4 Scene4 集成

修改文件：

`ElectroOptic-Lab/Assets/Scenes/Scene4_UIRebuild 1.unity`

集成内容：

- 在 `GameManager` 上新增 `Scene4OscilloscopeDispatcher`
- 禁用原 `RecordManager`
- 为 Dispatcher 配置 `fallbackProfile`
- 对 Scene4 当前关键 UI 控件进行 Inspector/场景序列化直连绑定
- 保留运行时自动查找作为 UI 层级调整后的兜底

自动查找路径：

- `DataCanvas/MainArea/LeftControlPanel/当前电压面板/ValueText`
- `DataCanvas/MainArea/LeftControlPanel/当前状态面板/ValueText (1)`
- `DataCanvas/MainArea/LeftControlPanel/交流调制状态面板/ValueText (1)`
- `DataCanvas/MainArea/LeftControlPanel/ButtonPanel/RecordButton`
- `DataCanvas/MainArea/LeftControlPanel/ButtonPanel/Btn_Clear`
- `DataCanvas/MainArea/LeftControlPanel/ButtonPanel/重置电压`
- `DataCanvas/MainArea/RightDisplayPanel/WavePanel/WaveContentArea/CH1Block`
- `DataCanvas/MainArea/RightDisplayPanel/WavePanel/WaveContentArea/CH2Block`
- `DataCanvas/MainArea/RightDisplayPanel/ResultPanel/Group/card1..card4`

---

## 3. 测试计划

### 3.1 编译验证

目标：

- Unity 脚本无新增编译错误
- 新增 `.cs` 与 `.meta` 文件可被 Unity 正确识别
- Scene4 中新增组件不出现 Missing Script

验证方式：

- 使用 Unity Editor 打开项目，等待脚本刷新完成
- 检查 Console 中是否存在 `error CS` 或 Missing Script 报错
- 检查 `GameManager` 上是否存在并启用 `Scene4OscilloscopeDispatcher`
- 检查旧 `RecordManager` 是否禁用

### 3.2 计算核心回归

运行方式：

Unity 菜单：

`ElectroOptics -> Tests -> Run Oscilloscope Calc Tests`

预期：

- 现有 10 项计算核心测试全部通过
- `FrequencyDoubling` 与 `SameFrequency` 仍符合预期
- `EnsureSize_NoGC` 仍确认相同尺寸不重新分配数组

### 3.3 Scene4 运行测试

进入 `Scene4_UIRebuild 1.unity` 后验证：

| 测试项 | 操作 | 预期结果 |
|--------|------|----------|
| 初始状态 | 播放场景 | 当前电压显示 `120.0V`，调制状态显示“已接入” |
| CH1 波形 | 播放场景 | CH1 区域显示稳定正弦波 |
| CH2 波形 | 播放场景 | CH2 区域显示光强波形 |
| 降低电压 | 按 A | `V_DC` 按 `0.5V` 递减，CH2 波形更新 |
| 提高电压 | 按 D | `V_DC` 按 `0.5V` 递增，CH2 波形更新 |
| 重置电压 | 点击“重置电压” | 电压恢复 `120.0V`，波形刷新 |
| 记录关键点 | 点击“记录当前关键点” | 当前电压依次写入 card1..card4 |
| 清空记录 | 点击“清空关键点” | 关键点卡片恢复空白占位 |
| 记录上限 | 连续点击记录超过 4 次 | 不越界，Console 输出卡片已满提示 |

### 3.4 UI 自动绑定测试

验证：

- Inspector 中 UI 引用为空时，Dispatcher 能按路径自动找到控件
- 若 UI 层级改名，Console 输出明确 warning
- 手动在 Inspector 补引用后，功能仍可运行

### 3.5 波形绘制测试

验证：

- CH1 使用 `[-vModulation, +vModulation]` 范围，不因瞬时值改变而缩放跳动
- CH2 使用 `[0, intensityMax]` 范围，光强显示稳定
- 波形控件不拦截点击事件
- CH1/CH2 绘制对象铺满对应通道区域，并保留边距

---

## 4. 验证记录

已完成：

- 使用 Unity 自带 Roslyn 编译器对新增/改动脚本进行引用级编译烟测
- 修复 `Scene4OscilloscopeDispatcher` 中 `double`/`float` 类型不匹配问题
- 清理临时编译产物

环境限制：

- 当前命令行环境没有 `dotnet`
- Unity batchmode 命令返回成功，但未生成可读取日志；最终完整验证仍建议在 Unity Editor 内刷新脚本并运行菜单测试

---

## 5. 归档说明

本计划归档为 Scene4 示波器 UI 接入工作的实现与测试依据。若后续调整 UI 层级、记录规则或波形样式，应同步更新本文件。

后续提示 UI 待处理：

- A/D 键调整直流分量的步进长度需要在提示 UI 中同步显示，避免提示文案与 `voltageStep` 配置不一致。
