# ElectroOptic-Lab-Unity

基于 **Unity 2022.3.62f2c1** 的电光效应虚拟仿真实验项目。项目围绕晶体光学、电光调制、偏振光传播、锥光干涉、半波电压测量、示波器波形和实验报告生成构建，核心主工程位于 `ElectroOptic-Lab/`。

> 本 README 已按当前代码结构对齐。更细的设计文档见 `Docs/`，权威实验流程见 `memory/project_experiment_workflow.md`。

## 项目结构

| 路径 | 说明 |
|------|------|
| `ElectroOptic-Lab/` | 主 Unity 项目，当前所有运行代码、场景、Shader、插件都在这里 |
| `Docs/` | PRD、架构设计、API、开发日志、配置指南和开发计划 |
| `Experiment/` | 脱离 Unity 的 DLL/物理模型验证工具和理论资料 |
| `memory/` | 项目长期记忆，包含主实验流程等约定 |
| `outputs/` | 生成输出或临时结果 |
| `.claude/` | 本仓库的本地代理配置 |

## 环境要求

| 类别 | 要求 |
|------|------|
| Unity | Unity 2022.3.62f2c1 或兼容的 2022.3 LTS |
| 平台 | Windows x86_64；`CrystalPhysicsCore.dll` 位于 `Assets/Plugins/x86_64/` |
| 语言 | C#、HLSL Shader、原生 C++ DLL |
| 主要依赖 | TextMesh Pro、UGUI、Postprocessing、QuickOutline、XCharts、MathNet.Numerics |
| 包注册源 | `https://packages.unity.cn` |

打开方式：用 Unity Hub 打开 `ElectroOptic-Lab/`，主实验场景是 `Assets/Scenes/Scene2.The Lab.unity`。

## 主实验流程

Scene2 主实验按五步组织：

1. 放置光屏，`UnifiedScreenPanel` 显示红点追踪。
2. 微调激光器，使红点对准光屏中心。
3. 放置起偏器和检偏器，旋转检偏器实现消光，验证马吕斯定律。
4. 放入晶体盒和扩束镜，面板自动切换到锥光干涉模式，调节晶体俯仰/偏航观察图样变化。
5. 移除光屏和扩束镜，放置光电接收器，进入半波电压测量和示波器调制实验。

## 场景

| 场景 | 当前用途 |
|------|----------|
| `Scene0.Open Menu.unity` | 主菜单 |
| `Scene1.intro.unity` | 实验介绍/教程 |
| `Scene2-preview.unity` | 晶体选择预览；支持内置晶体和自定义晶体 |
| `Scene2.The Lab.unity` | 主实验场景：光轨、激光、偏振片、晶体、光屏、接收器、统一显示面板 |
| `Scene_additional_exp.unity` | 附加锥光干涉实验场景，使用 GPU Jones 管线和 3D 光强曲面 |
| `Scene3_UIRebuild.unity` | 极值法数据记录、拟合、残差分析和 Vπ 提取 |
| `Scene4_UIRebuild 1.unity` | 示波器场景，显示调制电压和透射光强波形 |
| `Scene5.History Records.unity` | 历史记录/数据查看 |
| `Scene6_Quiz.unity` | Unity 内置习题场景 |
| `Scene7_Report.unity` | 实验报告入口 |
| `ConoscopicIntensitySurface_Test.unity` | CPU 锥光强度曲面测试/可视化场景 |
| `ConoscopicJonesIntensitySurface_Test.unity` | GPU Jones 锥光强度曲面测试/可视化场景 |
| `SceneTest*.unity`、`test.unity` | 开发/调试遗留场景，不作为生产入口 |

## 核心架构

### 晶体物理核心

晶体物理计算由 `Assets/Plugins/x86_64/CrystalPhysicsCore.dll` 提供，C# 层通过 P/Invoke 访问：

| 文件 | 职责 |
|------|------|
| `Scripts/DataContract/DataContracts.cs` | 与 DLL 对齐的 `SimInputData`、`CrystalOutputData` 结构体 |
| `Scripts/DataContract/NativeInterface.cs` | DLL 安全调用、输入输出数组校验和异常日志 |
| `Scripts/Business_logic/CrystalProfile.cs` | 晶体参数 ScriptableObject；支持 `CreateAssetMenu` |
| `Scripts/Business_logic/CrystalConfig.cs` | 运行时晶体配置：Profile、旋转、电场、光方向 |
| `Scripts/Business_logic/CrystalPhysicalCore.cs` | 两遍计算：Probe Pass 获取灵敏度，Render Pass 获取折射率/旋转矩阵；完成右手到左手坐标转换 |
| `Scripts/Business_logic/CrystalWorkingGeometry.cs` | 统一解析锥光和示波器工作几何；KTP 有特殊几何覆盖 |
| `Scripts/Business_logic/LabController.cs` | 原始晶体参数 UI 编排 |

可用晶体资源包括 `Assets/KDP.asset`、`Assets/Resources/Profiles/LiNbO3_Profile.asset`、`Assets/Resources/Profiles/KTP_Profile.asset`。自定义晶体由运行时 `CrystalProfile` 承载并通过同一数据链路传递。

### 光学链路

直接光路采用 `IOpticalReceiver` 责任链：

```text
LaserEmitter
  -> PolarizerPhysics
  -> CrystalRetarderPhysics
  -> PolarizerPhysics / Analyzer
  -> DirectScreenController
```

`LightData` 已从早期“强度 + 线偏振角”扩展为 Stokes 表示，包含 `S0/Q/U/V`，因此可以描述线偏振、椭圆偏振和晶体延迟后的偏振态。`CrystalRetarderPhysics` 在红点模式下调用 Jones CPU 参考计算晶体本征轴和相位延迟，再把结果继续传给后级偏振片/屏幕。

### 光轨和交互

| 文件 | 职责 |
|------|------|
| `OpticalRail.cs` | 导轨方向、长度和吸附位置 |
| `OpticalComponent_Keyboard.cs` | 光学元件拾取、A/D 移动、Space 吸附、双击移除 |
| `LaserEmitterMover.cs` | 激光器 WASD 微调，Enter 锁定校准 |
| `LaserKnobBridge.cs` | UI 旋钮控制激光组件 |
| `RotateStandController.cs`、`RotateWindowController.cs`、`RotateVirtualKeys.cs` | 偏振片旋转座、动态刻度盘和虚拟按键 |
| `ExperimentCameraController.cs`、`FocusableItem.cs` | 双击特写视角 |

`Scene2AdditionalSceneNavigator` 在进入附加实验/预览场景时使用 additive scene flow，临时挂起 Scene2 的输入、相机、光路和 UI，返回后恢复，避免后台场景继续响应全局输入。

### 晶体选择和实验初始化

`ExperimentNavigator` 采用“两步导航”：先设置目标实验场景，再进入 `Scene2-preview` 选择晶体，选择完成后跳转到目标场景。若 Scene2 正在运行，会走 additive 加载路径以保留主实验状态。

| 模块 | 职责 |
|------|------|
| `CrystalSelectionData` | 跨场景保存选中 Profile 和目标场景 |
| `CrystalRuntime` | 运行时保存当前晶体控制器、渲染器和物理核心 |
| `CrystalCardSelector` | 普通晶体卡片选择 |
| `CustomCrystalCard`、`CustomCrystalPanel` | “自定义晶体”卡片和运行时参数输入面板 |
| `CrystalComponentInitializer` | Scene2 中自动挂载/注册晶体控制、物理核心和锥光渲染器 |
| `CrystalControllerWrapper`、`CrystalKnobBridge` | 晶体 XY 旋转控制，范围限制为 ±15° |

### 统一光屏显示

当前光屏显示方案是 `Scripts/UI/ScreenDisplay/` 下的统一面板，而不是旧弹窗：

| 文件 | 职责 |
|------|------|
| `UnifiedScreenPanel.cs` | 左下角固定面板，红点追踪/锥光干涉双图层，自动淡入淡出切换 |
| `DirectScreenDataProvider.cs` | 读取 `DirectScreenController.SharedTexture` |
| `ConoscopicScreenDataProvider.cs` | 读取 `CrystalRuntime.TextureRenderer.RenderTexture` |
| `ScreenPanelInteraction.cs` | 面板双击进入附加实验场景 |
| `Panel3DOutline.cs`、`UIOutline.cs` | 面板视觉描边 |
| `CanvasGroupTweener.cs` | UI 淡入淡出工具 |

切换条件以光屏、扩束镜、晶体盒的 `OpticalComponent.isOnRail` 状态为准。

### 实验操作指引弹窗

`Scripts/UI/ExperimentGuide/` 提供 Scene2 内嵌式操作指引阅读器：

| 文件 | 职责 |
|------|------|
| `ExperimentGuidePopup.cs` | 复用/创建 `WindowsCanvas`，动态构建书册风格弹窗、遮罩、标题栏、单页高清图片显示区和翻页导航 |
| `ExperimentGuideButton.cs` | 挂到 Scene2 UI Button 上，自动绑定点击事件并打开指引弹窗 |

指引内容由提前从 PDF 转好的 PNG 页面提供，推荐以 Sprite 数组通过 Inspector 配置；当前交互为上一页/下一页、页码按钮和键盘左右键翻页。

## 锥光干涉与附加实验

`Scripts/ConoscopicAnalysis/` 同时保留两套管线：

| 管线 | 文件 | 用途 |
|------|------|------|
| CPU 强度管线 | `ConoscopicIntensityCore`、`ConoscopicIntensityCalculator`、`ConoscopicIntensitySurfaceVisualizer` | 解析式强度计算、曲面可视化和测试场景 |
| GPU Jones 管线 | `ConoscopicJonesGpuCore`、`ConoscopicJonesParameters`、`ConoscopicJonesCpuReference`、`ConoscopicJonesSurfaceVisualizer` | Jones calculus GPU 渲染，支持单轴/双轴、KTP 论文预设、电场扰动、超采样和强度读回 |

`Scene_additional_exp` 使用 `Scripts/UI/AdditionalExperiment/`：

| 文件 | 职责 |
|------|------|
| `AdditionalExperimentUiVisualController` | 运行时构建参数 UI，提供 M1/M2/M3 三种模式 |
| `AdditionalConoscopicExperimentApi` | 将 UI 参数转换成 `ConoscopicJonesParameters` 并触发重算 |
| `AdditionalConoscopicSurfaceView` | 把 GPU 结果重建为 3D 光强曲面并渲染到 UI |
| `AdditionalConoscopicVisualizationSettings` | 分辨率、超采样、屏幕几何、显示映射和模式预设 |
| `AdditionalExperimentSceneDispatcher` | 自动绑定 UI、Profile、GPU Core、曲面视图和 RawImage 输出 |

## 示波器、功率计和数据分析

### 示波器

`Scripts/Oscilloscope/` 是解耦计算模块：

| 文件 | 职责 |
|------|------|
| `OscilloscopeCore.cs` | 顶层 dirty-flag 编排：DLL 灵敏度 -> Vπ -> 波形 |
| `OscilloscopeCrystalBridge.cs` | 包装 `CrystalPhysicalCore`，缓存几何配置 |
| `VpiCalculator.cs` | 半波电压计算 |
| `WaveformCalculator.cs` | CH1/CH2 波形计算 |
| `OscilloscopeWaveformGraphic.cs` | uGUI 自绘波形 |
| `Scene4OscilloscopeDispatcher.cs` | Scene4 UI、键盘 A/D 调压、AC 接入状态、关键点记录 |

### 功率计和极值法

| 文件 | 职责 |
|------|------|
| `ReceiverStateController.cs` | 光电接收器状态机：关机、监测、微调 |
| `PowerReadoutController.cs` | 功率计窗口、接收器 WASD 微调、显示噪声 |
| `PowerReadoutCalculator.cs` | 纯数学计算：`sin²(πV/2Vπ)` 透过率、对准效率和读数噪声 |
| `RecordManager.cs` | 电压/功率记录表、按钮/键盘调压、删除和清表 |
| `Scene3CrystalBridge.cs` | 将选中晶体的 DLL 计算 Vπ 注入 `RecordManager.halfWaveVoltage` |
| `UIStateManager.cs` | 使用 MathNet.Numerics 拟合数据，使用 XCharts 展示散点、拟合曲线和残差，提取 Vπ |

## 测验和报告

| 模块 | 文件 | 说明 |
|------|------|------|
| Unity 测验场景 | `QuizManager.cs`、`QuestionData.cs`、`QuestionItemUI.cs` | 34 题题库，默认抽题、交卷、得分和解析 |
| 外部网页测验 | `OpenQuizButton.cs`、`StreamingAssets/QuizWeb/quiz.html` | 用系统浏览器打开课后习题网页 |
| 报告入口 | `OpenReportButton.cs`、`StreamingAssets/ReportWeb/report_template.html` | 用系统浏览器打开报告模板 |
| 报告静态资源 | `report.html`、`report.css`、`report.js`、`report_template.js` | 报告页面和模板脚本 |

## Shader 和渲染资源

| Shader | 路径 | 用途 |
|--------|------|------|
| `ElectroOptics/DotTracking` | `Assets/Shaders/DotTracking.shader` | 光屏红点追踪 |
| `ElectroOptics/ConoscopicInterference` | `Assets/Shaders/ConoscopicInterference.shader` | 主实验锥光干涉图样 |
| `ElectroOptics/ConoscopicJonesIntensity` | `Assets/Shaders/ConoscopicJonesIntensity.shader` | GPU Jones 强度/高度图 |
| `ElectroOptics/ConoscopicIntensityVertexColor` | `Assets/Shaders/ConoscopicIntensityVertexColor.shader` | 3D 光强曲面顶点色 |
| `UI/WaveformLine` | `Assets/Shaders/WaveformLine.shader` | 示波器波形线 |
| QuickOutline shaders | `Assets/QuickOutline/Resources/Shaders/` | 物体选中高亮 |

`CrystalVisualizer.cs` 负责把晶体物理状态同步到渲染材质。

## 编辑器测试

项目没有独立 CI 或 headless 测试脚本。当前测试入口均为 Unity Editor 菜单：

| 菜单 | 文件 | 覆盖内容 |
|------|------|----------|
| `ElectroOptics/Tests/Run Oscilloscope Calc Tests` | `Oscilloscope/Editor/OscilloscopeCalcTests.cs` | Vπ 和波形计算 |
| `ElectroOptics/Tests/Run Direct Polarization Retarder Tests` | `ConoscopicAnalysis/Editor/DirectPolarizationRetarderTests.cs` | Stokes 光路、晶体延迟、偏振分析 |
| `ElectroOptics/Tests/Run Conoscopic Jones Core Tests` | `ConoscopicAnalysis/Editor/ConoscopicJonesCoreTests.cs` | GPU Jones 与 CPU 参考对照 |
| `ElectroOptics/Tests/Run Conoscopic Intensity Core Tests` | `ConoscopicAnalysis/Editor/ConoscopicIntensityCoreTests.cs` | CPU 强度计算 |
| `ElectroOptics/Tests/Run Power Readout Calc Tests` | `Power/Editor/PowerReadoutCalculatorTests.cs` | 功率透过率、对准和噪声数学 |
| `ElectroOptics/Tests/Run LiNbO3 Power Readout Vpi Test` | `Power/Editor/LiNbO3PowerReadoutVpiTests.cs` | LiNbO3 Vπ 计算 |
| `ElectroOptics/Tests/Create Conoscopic Intensity Visualization Scene` | `ConoscopicIntensityVisualizationSceneBuilder.cs` | CPU 可视化测试场景构建 |
| `ElectroOptics/Tests/Repair Conoscopic Intensity Visualization Scene` | `ConoscopicIntensityVisualizationSceneBuilder.cs` | CPU 可视化测试场景修复 |
| `ElectroOptics/Tests/Create Conoscopic Jones Visualization Scene` | `ConoscopicJonesVisualizationSceneBuilder.cs` | GPU Jones 可视化测试场景构建 |
| `ElectroOptics/Tests/Repair Conoscopic Jones Visualization Scene` | `ConoscopicJonesVisualizationSceneBuilder.cs` | GPU Jones 可视化测试场景修复 |

## 命名空间和程序集

项目业务脚本没有自定义 `.asmdef`，默认编译到 `Assembly-CSharp.dll`；Editor 脚本进入 `Assembly-CSharp-Editor.dll`。自定义程序集主要来自 XCharts：

- `XCharts.Runtime`
- `XCharts.Editor`
- `XCharts.Examples`

命名空间分层如下：

| 命名空间 | 主要内容 |
|----------|----------|
| 全局命名空间 | 原始核心脚本、光路、导轨、记录、功率计、报告/测验入口、部分桥接脚本 |
| `ElectroOptics` | `CrystalProfile`、`CrystalConfig`、`CrystalWorkingGeometry`、电光枚举 |
| `ElectroOptics.DataTransfer` | `CrystalSelectionData`、`CrystalRuntime` |
| `ElectroOptics.Experiment.*` | 晶体控制包装器、初始化器、渲染器、接口 |
| `ElectroOptics.UI.*` | 屏幕面板、晶体选择、控制面板、电压切换 UI |
| `ElectroOptics.UI.ExperimentGuide` | Scene2 实验操作指引弹窗 |
| `ElectroOptics.Oscilloscope` | 示波器核心、桥接、波形计算和 UI 调度 |
| `ElectroOptics.ConoscopicAnalysis` | CPU/GPU 锥光分析管线 |

## 已弃用或遗留代码

以下代码仍在仓库中，但当前设计不应继续扩展它们：

| 文件 | 状态 |
|------|------|
| `CrystalInteract.cs` | 旧晶体交互方案；由 `CrystalControllerWrapper`/`CrystalKnobBridge` 替代 |
| `ScreenInteract.cs` | 旧光屏弹窗方案；由 `UnifiedScreenPanel` 替代 |
| `Cardclick.cs` | 早期硬编码场景加载器；当前优先用 `ExperimentNavigator`/`CrystalCardSelector` |
| `CrystalStateController.cs` | 旧式晶体单击变色控制，疑似遗留 |
| `SceneTest*.unity`、`test.unity` | 调试/中间场景 |

## 开发注意事项

- 新功能优先通过包装器、桥接器和独立模块扩展，避免直接改动原始核心逻辑。
- 运行时修改材质应使用 `.material` 或创建运行时材质实例，避免写坏 `.sharedMaterial` 资源。
- DLL 结构体字段、数组长度和内存布局必须与 C++ 保持一致。
- Unity 和 DLL 坐标系不同：DLL 为右手系，Unity 为左手系，转换集中在 `CrystalPhysicalCore`。
- UI 动态构建优先复用 `CustomCrystalPanel`/`AdditionalExperimentUiVisualController` 的模式：确保 `RectTransform`、EventSystem、TMP 中文字体和 ScrollRect Mask 正确配置。
- 修改实验流程时以 `memory/project_experiment_workflow.md` 为准。

## 独立验证工具

`Experiment/generate_kdp_eo_data.py` 可在 Unity 外通过 Python `ctypes` 调用 `CrystalPhysicsCore.dll`，生成 KDP 电光响应数据并与一阶解析理论比较：

```powershell
python Experiment\generate_kdp_eo_data.py --fields 0 2e5 5e5 1e6 2e6 5e6 1e7
```

该工具适合在修改原生 DLL 或数据契约后做快速回归验证。
