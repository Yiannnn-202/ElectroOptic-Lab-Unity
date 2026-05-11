# ElectroOptic-Lab-Unity

基于 Unity 2022.3 的电光实验室仿真项目，模拟晶体光学实验，涵盖锥光干涉图样、偏振光传播和电光调制等功能。

## 项目概述

本项目使用 Unity 引擎构建交互式电光实验室环境。用户可在虚拟光学平台上操作激光器、偏振片、晶体等光学元件，实时观察锥光干涉图样、测量半波电压，并通过示波器分析电光调制波形。

### 技术栈

| 层面 | 技术 |
|------|------|
| 引擎 | Unity 2022.3.62f2c1 |
| 语言 | C# (脚本), C++ (物理引擎 DLL) |
| 渲染 | HLSL Shader, UGUI, LineRenderer |
| 原生接口 | P/Invoke 调用 `CrystalPhysicsCore.dll` |
| 包依赖 | TextMesh Pro, QuickOutline, Postprocessing |

## 多项目结构

本仓库包含多个独立 Unity 项目：

| 目录 | 说明 |
|------|------|
| `ElectroOptic-Lab/` | **主仿真项目**（核心工作目录） |
| `Docs/` | 项目文档（PRD、架构、API、开发日志等） |

其余目录（`Screen/`、`3DAssets/`、`TestRepo/`）为历史遗留或独立子项目。

## 快速开始

### 环境要求

- **Unity 2022.3.62f2c1** 或兼容版本
- **Windows x86_64**（原生 DLL 仅支持此平台）
- 建议 IDE：Visual Studio / JetBrains Rider

### 打开项目

1. 使用 Unity Hub → "Open" → 选择 `ElectroOptic-Lab/` 目录
2. 定位主场景：`Assets/Scenes/Scene2.The Lab.unity`
3. 通过 Unity 标准构建系统编译运行

### 场景导航

| 场景文件 | 功能 |
|----------|------|
| `Scene0.Open Menu` | 主菜单，提供各场景入口 |
| `Scene1.intro` | 入门介绍 / 教程 |
| `Scene2-preview` | 晶体选择预览（晶体卡片展示与选择） |
| `Scene2.The Lab` | **主实验场景**（光轨、激光、晶体、屏幕、示波器） |
| `Scene3_UIRebuild` | 实验 UI 原型（重建版） |
| `Scene4_UIRebuild 1` | 示波器重建场景（波形渲染与按键记录） |
| `Scene5.History Records` | 历史数据记录查看器 |
| `Scene6_Quiz` | 测验 / 练习场景（34 题题库） |
| `Scene7_Report` | 实验报告生成与导出 |

> **注意**：`SceneTest.unity`、`SceneTest2.unity` 为开发调试场景，非生产用途。

## 功能模块

### 1. 晶体物理引擎

核心物理计算由原生 C++ DLL 提供，C# 层通过 P/Invoke 调用。

**关键文件**：
- `CrystalPhysicsCore.dll` (`Assets/Plugins/x86_64/`) — 原生 DLL，计算电光系数、折射率、旋转矩阵
- `NativeInterface.cs` — DLL 安全调用封装与验证
- `DataContracts.cs` — 与 C++ 结构体内存布局一致的 C# 结构体
- `CrystalProfile.cs` — ScriptableObject，定义晶体属性参数
- `CrystalConfig.cs` — 运行时晶体状态配置（旋转、电场、光方向）
- `CrystalPhysicalCore.cs` — 核心组件：调用 DLL、坐标系转换、传递数据到 Shader
- `EOEnums.cs` — 枚举：传播轴、电场轴、调制模式

**支持晶体**：KDP (`KDP.asset`)、KTP (`KTP_Profile.asset`)、LiNbO₃ (`LiNbO3_Profile.asset`)

### 2. 光学组件链

采用**责任链模式**，通过 `IOpticalReceiver` 接口串联光学元件：

```
LaserEmitter → PolarizerPhysics → Crystal → Screen Controller
```

**关键文件**：
- `OpticalDef.cs` — `IOpticalReceiver` 接口、`LightData` 结构体
- `LaserEmitter.cs` — 激光发射器，LineRenderer + Raycast 模拟光束
- `PolarizerPhysics.cs` — 偏振片，应用马吕斯定律，输出完全偏振光 (DOP=1)
- `DirectScreenController.cs` — 屏幕控制器，渲染红点追踪到 512×512 Texture2D

### 3. 光学导轨与组件放置

- `OpticalRail.cs` — 导轨定义（方向、长度、吸附位置计算）
- `OpticalComponent_Keyboard.cs` — 光学组件：点击拾取、A/D 沿轨移动、Space 放置/吸附、双击移除
- `RailObjectMover.cs` — 通用导轨移动器（轴向限位、轮廓高亮、放大镜头锚点）

### 4. 激光调节系统

- `LaserStateController.cs` — 激光选择（单击高亮 Outline 切换）
- `LaserEmitterMover.cs` — 物理微调（WASD ±0.035m），Enter 锁定校准
- `LaserKnobBridge.cs` — UI 旋钮控制激光组装体旋转

### 5. 旋转台系统

- `RotateStandController.cs` — 偏振片旋转台：双击打开刻度盘、单击选中（Outline 高亮）、A/D 旋转
- `RotateWindowController.cs` — 动态刻度盘窗口，支持拖拽旋转
- `RotateVirtualKeys.cs` — 虚拟按钮驱动旋转（替代键盘 A/D）

全局互斥：同时只能选择一个旋转台。

### 6. 实验模块 (P0-P2)

遵循**解耦设计**原则 —— 通过包装器/适配器扩展功能，不修改原有代码。所有新代码位于独立目录。

```
Scripts/Experiment/
├── Interfaces/          ICrystalSelectable, ICrystalConfigurable
├── Controller/          CrystalControllerWrapper, CrystalKnobBridge
├── Initializer/         CrystalComponentInitializer, Scene3CrystalBridge
└── Renderer/            ConoscopicTextureRenderer
```

**数据传递** (`Scripts/DataTransfer/`)：
- `CrystalSelectionData` — 跨场景晶体选择传递
- `CrystalRuntime` — 运行时全局访问晶体组件（Controller、TextureRenderer、PhysicalCore）

**关键功能**：
- 晶体选择预览 → 主场景加载 → 组件自动初始化
- 晶体旋转控制（XY 轴 ±15°）
- 锥光干涉离线渲染到 RenderTexture

### 7. 屏幕显示模块

- `UnifiedScreenPanel.cs` — 左下角固定面板，双层显示（红点追踪 / 锥光干涉），根据 `isOnRail` 自动切换模式并带淡入淡出动画
- `IScreenDataProvider.cs` — 接口解耦数据源与显示面板
- `ConoscopicScreenDataProvider.cs` — 封装锥光 RenderTexture
- `DirectScreenDataProvider.cs` — 封装红点追踪 Texture2D
- `CanvasGroupTweener.cs` — CanvasGroup 淡入/淡出/交叉淡入淡出动画

### 8. 示波器模块

- `OscilloscopeCore.cs` — 顶层编排器：接收参数 → 调用 Bridge 获取灵敏度 → Vπ 计算 → 波形生成 → 事件通知 UI
- `OscilloscopeCrystalBridge.cs` — 封装 CrystalPhysicalCore 用于示波器场景，含状态缓存优化
- `OscilloscopeParameters.cs` — 输入参数（VDC、Vm、频率、调制模式、电场轴、补偿相位）
- `VpiCalculator.cs` — 纯数学计算：从波长、尺寸、灵敏度计算半波电压 Vπ
- `WaveformCalculator.cs` — 纯数学引擎：生成 CH1（AC 电压）和 CH2（透射光强）波形数组
- `WaveformResult.cs` — 输出容器（CH1/CH2 数组、Vπ、γ₀）
- `OscilloscopeWaveformGraphic.cs` — 自定义 uGUI Graphic，无需纹理/材质直接渲染波形折线
- `Scene4OscilloscopeDispatcher.cs` — Scene4 UI 编排器，绑定参数到 UI 控件
- `OscilloscopeCalcTests.cs` — 编辑器测试（消光、倍频、同频调制、补偿相位、数组复用验证）

### 9. 功率计

- `ReceiverStateController.cs` — 接收器状态机（0=关闭, 1=监测/蓝色, 2=调节选中/绿色）。双击开关电源，单击切换监测/选中
- `PowerReadoutController.cs` — 读数窗口，WASD 微调，基于光束聚焦模型计算功率

### 10. 测验系统

- `QuizManager.cs` — 从 34 题题库随机出题，管理提交、评分和导航
- `QuestionData.cs` — 题目数据结构（题干、4 个选项、正确索引、解析）
- `QuestionItemUI.cs` — 题目 UI 项（选项选择、解析展示）

### 11. Shader 可视化

| Shader | 文件 | 功能 |
|--------|------|------|
| 锥光干涉 | `ConoscopicInterference.shader` | GPU 菲涅尔方程计算干涉图样 |
| 红点追踪 | `DotTracking.shader` | 光斑渲染 |
| 轮廓高亮填充 | `OutlineFill.shader` | QuickOutline 填充层 |
| 轮廓高亮遮罩 | `OutlineMask.shader` | QuickOutline 遮罩层 |

`CrystalVisualizer.cs` 负责将晶体物理数据同步到 Shader 属性（折射率、旋转矩阵、晶体长度、波长、FOV）。

### 12. 其他系统

- **相机系统**：`CameraSwitch.cs`（三视图切换），`ExperimentCameraController.cs`（放大镜头），`CameraFocusController.cs`（平滑对焦过渡）
- **数据记录**：`RecordManager.cs`（电压-功率数据表，支持删除和清除）
- **旋钮控制**：`KnobAdjuster.cs`（按住旋转 3D 旋钮模型），`RotationKnob.cs`（控制面板旋钮），`KnobToggleController.cs`
- **场景加载**：`SceneLoad.cs`、`ExperimentNavigator.cs`（场景导航按钮）
- **报告**：`OpenReportButton.cs`（实验报告入口）

## 项目架构

### 目录结构

```
ElectroOptic-Lab/
├── Assets/
│   ├── Scenes/                    场景文件（8 个主场景 + 测试场景）
│   ├── Scripts/
│   │   ├── Business_logic/        晶体物理（Profile, Config, PhysicalCore, LabController, Enums）
│   │   ├── DataContract/          原生 DLL 接口（NativeInterface, DataContracts）
│   │   ├── DataTransfer/          跨场景数据传递（CrystalSelectionData, CrystalRuntime）
│   │   ├── Experiment/            实验模块
│   │   │   ├── Controller/        CrystalControllerWrapper, CrystalKnobBridge
│   │   │   ├── Initializer/       CrystalComponentInitializer, Scene3CrystalBridge
│   │   │   ├── Interfaces/        ICrystalSelectable, ICrystalConfigurable
│   │   │   └── Renderer/          ConoscopicTextureRenderer
│   │   ├── Laser/                 激光系统（LaserEmitter, LaserStateController, LaserEmitterMover, LaserKnobBridge）
│   │   ├── Oscilloscope/          示波器模块（Core, Bridge, Parameters, Calculators, WaveformGraphic, Dispatcher, Tests）
│   │   ├── UI/
│   │   │   ├── ControlPanel/      旋转控制面板（RotationPanel, RotationKnob, AngleDisplay）
│   │   │   ├── CrystalSelector/   晶体卡片选择器
│   │   │   ├── ScreenDisplay/     统一显示面板（UnifiedPanel, DataProviders, CanvasGroupTweener）
│   │   │   └── VoltageSwitch/     相机对焦与点击区域
│   │   ├── ShaderScripts/         Shader 参数同步（CrystalVisualizer）
│   │   ├── exercise/              测验系统（QuizManager, QuestionData, QuestionItemUI）
│   │   ├── Receiver/              功率计接收器状态
│   │   ├── LightScreen/           光屏（DirectScreenController, SimpleDrag）
│   │   ├── Buttons/               按钮导航（SceneLoad, ExperimentNavigator）
│   │   ├── ViewButton/            旋钮调节器（KnobAdjuster）
│   │   └── Report/                报告（OpenReportButton）
│   ├── Shaders/                   自定义 Shader（ConoscopicInterference, DotTracking）
│   ├── Plugins/x86_64/            原生 C++ DLL（CrystalPhysicsCore.dll）
│   ├── QuickOutline/              轮廓高亮资源包
│   ├── *.asset                     晶体配置文件（KDP, KTP, LiNbO3）
│   └── Resources/                 运行时资源
└── ProjectSettings/               Unity 项目配置
```

### 命名空间

| 命名空间 | 包含 |
|----------|------|
| `ElectroOptics` | CrystalProfile, CrystalConfig, CrystalPhysicalCore (原始核心) |
| `ElectroOptics.DataTransfer` | CrystalSelectionData, CrystalRuntime |
| `ElectroOptics.Experiment.Interfaces` | ICrystalSelectable, ICrystalConfigurable |
| `ElectroOptics.Experiment.Controller` | CrystalControllerWrapper, CrystalKnobBridge |
| `ElectroOptics.Experiment.Initializer` | CrystalComponentInitializer |
| `ElectroOptics.Experiment.Renderer` | ConoscopicTextureRenderer |
| `ElectroOptics.UI.ScreenDisplay` | UnifiedScreenPanel, IScreenDataProvider, CanvasGroupTweener |
| `ElectroOptics.UI.ControlPanel` | CrystalRotationPanel, RotationKnob, AngleDisplay |
| `ElectroOptics.UI.CrystalSelector` | CrystalCardSelector |
| `ElectroOptics.Oscilloscope` | OscilloscopeCore, OscilloscopeCrystalBridge, WaveformCalculator 等 |

### 核心数据流

```
UI 控件 → LabController
            ↓
        CrystalConfig（晶体旋转、电场、光方向、尺寸）
            ↓
        CrystalPhysicalCore
            ↓           ↑
        NativeInterface → CrystalPhysicsCore.dll
            ↓
        物理数据（折射率矩阵、电光系数、旋转矩阵）
            ↓
        CrystalVisualizer → Shader Properties
            ↓
        GPU 实时渲染（锥光干涉图样 / 红点追踪）
```

## 开发指南

### 核心原则

1. **解耦设计**：新功能通过包装器/适配器扩展，不修改原有代码。所有实验模块代码位于 `Scripts/Experiment/`、`Scripts/DataTransfer/`、`Scripts/UI/` 独立目录。

2. **坐标系转换**：原生 DLL 使用右手坐标系，Unity 使用左手坐标系。转换通过 `CrystalPhysicalCore.cs` 中 Z 轴翻转旋转矩阵实现，对上层代码透明。

3. **材质安全**：运行时修改材质必须使用 `.material`（创建运行时实例），禁止使用 `.sharedMaterial`（会永久修改磁盘资源）。

### 常见开发任务

**修改晶体行为**：
1. 修改 `CrystalProfile` 资产（如 `KDP.asset`）添加新晶体类型
2. 物理计算变更需修改 C++ DLL 源码并替换 `Plugins/x86_64/CrystalPhysicsCore.dll`
3. `DataContracts.cs` 结构体必须与 C++ 内存布局完全匹配

**添加光学组件**：
1. 实现 `IOpticalReceiver` 接口
2. 在 `ReceiveLight()` 中处理光数据
3. 通过 Raycast 传递到下游光学元件（可选）
4. 使用 LineRenderer 可视化出射光
5. 确保正确的 Layer / Collider 配置

**创建 UI 窗口**：
- Canvas 使用 Screen Space Overlay，命名为 "WindowsCanvas"
- 拖拽通过 `SimpleDrag` 或 EventTrigger 实现
- Canvas Scaler 参考分辨率 1920×1080
- 确保场景中存在 EventSystem
- 双击检测统一使用 0.3s 间隔

**编辑器测试**：
- 菜单 **ElectroOptics > Tests > Run Oscilloscope Calc Tests** 运行示波器计算测试

### 已弃用代码

以下文件在场景中已无引用，仅保留作参考：

- `CrystalInteract.cs` — 旧版晶体交互（双击选择 + WASD 旋转），已被 `CrystalControllerWrapper` + `CrystalRotationPanel` 取代
- `ScreenInteract.cs` — 旧版屏幕交互，已被 `UnifiedScreenPanel` 取代

## 更多信息

- 详细开发指南：参阅 [CLAUDE.md](./CLAUDE.md)
- 项目文档：参阅 [Docs/](./Docs/) 目录（PRD、架构设计、API 文档、开发日志）
