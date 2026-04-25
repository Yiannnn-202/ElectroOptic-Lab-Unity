# 代码库审计：已启用逻辑 vs 废弃逻辑

> 审计日期: 2026-04-24

---

## 一、当前启用的核心架构

### 场景入口链

```
Scene0.Open Menu → Scene2-preview (CrystalCardSelector 选晶体) → Scene2.The Lab (主实验)
```

### Scene2.The Lab 中经 GUID 确认挂载的脚本

| 脚本 | GUID (prefix) | 状态 |
|------|-------------|------|
| `LaserEmitter` | `59278614...` | 已启用 — 激光发射 |
| `LaserStateController` | `f060c438...` | 已启用 — 激光器选中高亮 |
| `LaserEmitterMover` | `76bd02ae...` | 已启用 — 激光器物理微调 |
| `ReceiverStateController` | `e6a629e7...` | 已启用 — 接收器状态机 (关机/监控/选中) |
| `OpticalComponent` (OpticalComponent_Keyboard.cs) | `b0353043...` | 已启用 — 光学元件拾取/吸附导轨 |
| `DirectScreenController` | `d3753eb9...` | 已启用 — 光屏红点追踪 |
| `CrystalKnobBridge` | `08a657b0...` | 已启用 — 晶体盒旋钮 |
| `PowerReadoutController` | `59278614...` | 已启用 — 光功率计读数 |

---

## 二、已启用的业务模块

### 实验模块 (Scripts/Experiment/)

全部启用，这是 P0-P2 开发的成果：

| 类 | 职责 |
|---|------|
| `CrystalComponentInitializer` | 场景启动时自动挂载 `CrystalPhysicalCore`、`CrystalControllerWrapper`、`ConoscopicTextureRenderer`，注册到 `CrystalRuntime` |
| `CrystalControllerWrapper` | 封装晶体旋转控制 (±15°)，通过 `CrystalConfig` 调用 `CrystalPhysicalCore.ApplyConfig()` |
| `CrystalRuntime` (静态) | 全局运行时引用注册表 |
| `CrystalSelectionData` (静态) | 跨场景传递选中的 CrystalProfile |
| `ConoscopicTextureRenderer` | 独立 Camera + RenderTexture 渲染锥光干涉图到纹理 |
| `CrystalKnobBridge` | 晶体盒旋钮按钮 → `CrystalControllerWrapper.AddRotation()` |
| `ICrystalConfigurable` / `ICrystalSelectable` | 接口 |

依赖链: `CrystalCardSelector` → `CrystalSelectionData` → `CrystalComponentInitializer` → `CrystalControllerWrapper` → `CrystalPhysicalCore` → `NativeInterface` → DLL

### 光屏显示模块 (Scripts/UI/ScreenDisplay/)

新重构成果 (2026-03)，替代旧的弹窗方案：

| 类 | 职责 |
|---|------|
| `UnifiedScreenPanel` | 左下角统一面板，根据 `OpticalComponent.isOnRail` 自动切换 Direct / Conoscopic 两层 |
| `DirectScreenDataProvider` | 从 `DirectScreenController.SharedTexture` 获取红点纹理 |
| `ConoscopicScreenDataProvider` | 从 `CrystalRuntime.TextureRenderer.RenderTexture` 获取锥光纹理 |
| `IScreenDataProvider` | 数据提供者接口 |
| `ScreenMode` | 显示模式枚举 (Direct / Conoscopic) |
| `CanvasGroupTweener` | 淡入淡出过渡动画 |

### 示波器模块 (Scripts/Oscilloscope/)

架构完整，遵循解耦模式：

| 类 | 职责 |
|---|------|
| `OscilloscopeCore` | 顶层编排器：DLL交互 → Vπ计算 → 波形生成，dirty flag 优化 |
| `OscilloscopeCrystalBridge` | 封装 `CrystalPhysicalCore`（与 `CrystalControllerWrapper` 模式一致） |
| `OscilloscopeParameters` | 输入参数容器 |
| `VpiCalculator` | Vπ 计算（纯数学，无 Unity 依赖） |
| `WaveformCalculator` | 波形计算引擎（纯数学） |
| `WaveformResult` | 输出容器 (ch1/ch2 波形数组, Vπ, Γ₀) |

### 光学链路 (Lasers/LightScreen/)

| 类 | 职责 |
|---|------|
| `LaserEmitter` | 发射激光 (LineRenderer + Physics.Raycast)，调用 `IOpticalReceiver.ReceiveLight()` |
| `PolarizerPhysics` | 实现 `IOpticalReceiver`，应用马吕斯定律，链式传递 |
| `DirectScreenController` | 实现 `IOpticalReceiver`，红点追踪渲染到 512x512 Texture2D |
| `OpticalRail` | 光学导轨吸附位置计算 |
| `OpticalComponent` | 光学元件：单击拾取/A/D移动/空格放下/双击取下，吸附导轨 |

### 晶体物理核心 (Scripts/Business_logic/)

| 类 | 职责 |
|---|------|
| `CrystalPhysicalCore` | 核心引擎：两遍系统 (Probe Pass 几何敏感度 + Render Pass 实际场)，调用 DLL，坐标系转换 (RHS → LHS) |
| `LabController` | 主 UI 编排器：电压/调制模式/电场轴/晶体尺寸 |
| `CrystalConfig` | 运行时配置结构体 |
| `CrystalProfile` | ScriptableObject 定义晶体属性 |
| `EOEnums` | `PropagationAxis` / `ElectricFieldAxis` / `ModulationMode` |

### 其他已启用模块

| 模块 | 文件 | 场景 |
|------|------|------|
| 答题系统 | `QuizManager` + `QuestionData` + `QuestionItemUI` | Scene6_Quiz |
| 偏光镜旋转座 | `RotateStandController` + `RotateWindowController` + `RotateVirtualKeys` | Scene2 |
| 导轨物体移动器 | `RailObjectMover.cs` (Assets/ 根目录) | Scene2 |
| 特写镜头 | `ExperimentCameraController` | Scene2 |
| 相机视角切换 | `CameraSwitch` | Scene2 |
| 数据记录表 | `RecordManager` | Scene2 |
| 晶体可视化 | `CrystalVisualizer` | Scene2 |
| 旋钮调节 | `KnobAdjuster` | Scene2 |
| DLL 安全接口 | `NativeInterface` + `DataContracts` | 全局 |
| 场景加载 | `Cardclick` / `SceneLoad` | Scene0/Scene2-preview |
| 相机聚焦 | `CameraFocusController` + `ClickAreaFocus` | 待确认 |

---

## 三、废弃/孤立代码

### 明确废弃 (未出现在任何 .unity 场景中)

| 脚本 | 原因 | 删除风险 |
|------|------|----------|
| **`CrystalInteract.cs`** | 旧版晶体交互：双击选中 + WASD 旋转 + 随机初始偏转。已被 `CrystalControllerWrapper` + `CrystalRotationPanel` 替代。不在任何场景中。 | 低 — 但如果之前打包的 Prefab 上残留此组件，在场景中动态 AddComponent 会导致与 `CrystalComponentInitializer` 冲突 |
| **`ScreenInteract.cs`** | 旧版光屏交互：双击切换 `conoscopeUIPanel` GameObject。已被 `UnifiedScreenPanel` 替代。不在任何场景中。 | 低 — 但引用 `CrystalInteract.Instance`，而后者也是废弃代码 |

### 疑似废弃/孤立

| 脚本 | 分析 | 建议 |
|------|------|------|
| **`CrystalStateController.cs`** | 单击变色版晶体选中（功能比 CrystalInteract 简洁）。场景引用未找到。 | 可能是废弃开发过程中的另一个中间版本。确认后删除。 |
| **`CoreDebugger.cs`** | 开发调试工具 — 绕开 LabController 直接操作 CrystalPhysicalCore。 | 保留作为开发工具，但不应出现在生产场景中 |

### 测试代码

| 脚本 | 分析 |
|------|------|
| **`BridgeLayerTest.cs`** (DataContract/) | 明确的测试脚本：Happy Path + Sabotage 测试 NativeInterface 安全性 |
| **`OscilloscopeCalcTests.cs`** (Oscilloscope/Editor/) | Editor 目录下，仅编辑器运行 |

---

## 四、CLAUDE.md 与实际代码不符的条目

| CLAUDE.md 中的描述 | 实际情况 |
|-------------------|---------|
| `ScreenPopupManager` — 双击检测、调度弹窗 | 文件不存在，功能已合并到 `UnifiedScreenPanel` |
| `ConoscopicWindowView` — 锥光干涉弹窗 | 文件不存在，功能已合并到 `UnifiedScreenPanel` + `ConoscopicScreenDataProvider` |
| `Scripts/UI/` 下有 `ScreenPopupManager` | 实际目录为 `UI/ScreenDisplay/`，无此文件 |

建议更新 CLAUDE.md 以反映这些变化。

---

## 五、架构冲突/冗余

### 1. 晶体控制双轨制

| 维度 | CrystalInteract (废弃) | CrystalControllerWrapper (启用) |
|------|----------------------|-------------------------------|
| 选中方式 | OnMouseDown 双击 | 通过 CrystalComponentInitializer 自动设置 |
| 旋转方式 | `crystalRotation *= Quaternion.Euler(delta)` 增量式 | `SetRotation(Vector2)` 绝对值式 |
| 旋转范围 | 无限制 | ±15° |
| 键盘控制 | 自身 Update 中检测 WASD | 委托给 CrystalRotationPanel |
| Profile | 不涉及，使用 Core 当前 Config | 通过 SetProfile() 显式设置 |

两者不能同时存在于同一晶体上 — 都会调用 `CrystalPhysicalCore.ApplyConfig()`。

### 2. 光屏显示双轨制

| 维度 | ScreenInteract (废弃) | UnifiedScreenPanel (启用) |
|------|---------------------|------------------------|
| 触发方式 | OnMouseDown 双击光屏 | 自动检测 `OpticalComponent.isOnRail` |
| UI 呈现 | 切换预制 `conoscopeUIPanel` | 动态创建双图层 (Direct/Conoscopic) |
| 数据源 | 未知 (依赖外部面板) | IScreenDataProvider 接口 |

### 3. 场景文件冗余

存在多个测试/中间版本场景，建议清理：
- `SceneTest.unity` / `SceneTest2.unity` / `test.unity`
- `Scene3.Exp1.unity` / `Scene4.Exp1 1.unity`
- `Scene3_UIRebuild.unity` / `Scene4_UIRebuild 1.unity`

### 4. 目录结构不规范

- `RailObjectMover.cs` 放在 `Assets/` 根目录而非 `Scripts/`
- `Shaders/` 目录不存在（Shader 实际位于何处需确认）
- `ConoscopicInterference.shader` 引用位置需确认

---

## 六、建议行动计划

### 可立即执行
- [ ] 删除 `CrystalInteract.cs` + `.meta`（无场景引用）
- [ ] 删除 `ScreenInteract.cs` + `.meta`（无场景引用）
- [ ] 删除 `CrystalStateController.cs` + `.meta`（确认无引用后）
- [ ] 将 `RailObjectMover.cs` 移入 `Scripts/` 合适子目录

### 需确认后执行
- [ ] 确认 `CoreDebugger.cs` 是否在生产场景中被挂载
- [ ] 审视是否需要保留旧版 Scene3/Scene4 unity 文件
- [ ] 将 `SceneTest.unity` / `test.unity` 移至开发分支或删除

### 文档更新
- [ ] 更新 CLAUDE.md: 移除 `ScreenPopupManager`/`ConoscopicWindowView`，补充 `UnifiedScreenPanel`/`IScreenDataProvider`
- [ ] 补充示波器模块的 CLAUDE.md 条目
