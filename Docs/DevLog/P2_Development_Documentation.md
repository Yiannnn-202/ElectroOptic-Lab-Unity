# 晶体锥光干涉实验 - P2 阶段开发文档

## 文档信息

| 项目 | 内容 |
|------|------|
| **项目名称** | ElectroOptic Lab Unity - 晶体锥光干涉实验 |
| **文档版本** | 1.0 |
| **创建日期** | 2026-02-20 |
| **Unity版本** | 2022.3.62f2c1 |
| **开发原则** | 解耦设计，不修改原代码 |
| **当前阶段** | P0-P1-P2 已完成 |

---

## 一、P2 阶段需求

### 1.1 功能需求

| ID | 需求描述 | 优先级 | 状态 | 实现文件 |
|----|----------|--------|------|----------|
| FR-004 | 双击光屏弹出锥光干涉图 | P2 | **已完成** | ScreenPopupManager.cs, ConoscopicWindowView.cs |
| FR-005 | 晶体XY轴旋转控制面板（旋钮UI） | P2 | **已完成** | CrystalRotationPanel.cs, RotationKnob.cs, AngleDisplay.cs |
| FR-006 | 锥光干涉图随旋转实时变化 | P2 | **已完成** | ConoscopicWindowView.cs (每帧更新) |

### 1.2 技术决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 晶体检测方式 | **位置距离检测** | 已有实现，与 `OpticalComponent` 一致 |
| 更新频率 | **每帧更新** | 与 `CrystalVisualizer` 和 `RotateWindowController` 一致 |
| ScreenPopupManager | **包装 DirectScreenController** | 复用原有代码，不修改 |
| 旋钮UI | **复用刻度盘图片风格** | 视觉一致性 |
| 旋钮键盘支持 | **支持 W/S/A/D 键** | W/S 控制 X轴，A/D 控制 Y轴 |
| 旋钮精细度 | **每格 0.5°** | 用户指定 |

---

## 二、文件架构

### 2.1 P2 新增文件

```
Assets/Scripts/UI/
├── ScreenPopup/
│   ├── ScreenPopupManager.cs      # 光屏弹窗管理器
│   └── ConoscopicWindowView.cs    # 锥光干涉弹窗视图
│
└── ControlPanel/
    ├── CrystalRotationPanel.cs    # 晶体旋转控制面板
    ├── RotationKnob.cs            # 旋钮控件
    └── AngleDisplay.cs            # 角度显示组件
```

### 2.2 文件清单

| 文件路径 | 命名空间 | 职责 |
|---------|----------|------|
| `Scripts/UI/ScreenPopup/ScreenPopupManager.cs` | ElectroOptics.UI.ScreenPopup | 光屏双击弹窗管理 |
| `Scripts/UI/ScreenPopup/ConoscopicWindowView.cs` | ElectroOptics.UI.ScreenPopup | 锥光干涉弹窗视图 |
| `Scripts/UI/ControlPanel/CrystalRotationPanel.cs` | ElectroOptics.UI.ControlPanel | 晶体旋转控制面板 |
| `Scripts/UI/ControlPanel/RotationKnob.cs` | ElectroOptics.UI.ControlPanel | 旋钮控件（拖拽旋转） |
| `Scripts/UI/ControlPanel/AngleDisplay.cs` | ElectroOptics.UI.ControlPanel | 角度显示（格式化） |

---

## 三、类设计

### 3.1 ScreenPopupManager

**挂载位置**：光屏 GameObject（与 DirectScreenController 同对象）

**职责**：
- 检测双击光屏
- 检测晶体是否在导轨上（位置距离检测）
- 调度显示锥光干涉弹窗或红点弹窗

**Inspector 配置**：
| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `directScreenController` | DirectScreenController | - | 引用现有组件 |
| `detectionRange` | float | 2.0f | 晶体检测范围 |
| `doubleClickInterval` | float | 0.3f | 双击间隔 |
| `windowSize` | Vector2 | (600, 600) | 弹窗尺寸 |
| `windowTitle` | string | "锥光干涉图" | 弹窗标题 |

### 3.2 ConoscopicWindowView

**职责**：
- 创建可拖拽弹窗
- 显示锥光干涉 RenderTexture
- 每帧调用 `UpdateAndRender()` 更新渲染

**UI 结构**：
```
ConoscopicWindow
├── TitleBar
│   ├── TitleText "锥光干涉图"
│   └── CloseButton [X]
└── PatternView (RawImage)
    └── texture = CrystalRuntime.TextureRenderer.RenderTexture
```

### 3.3 CrystalRotationPanel

**挂载位置**：晶体 GameObject（与 CrystalControllerWrapper 同对象）

**职责**：
- 检测双击晶体
- 创建/显示控制面板
- 管理两个旋钮控件
- 响应键盘输入（W/S/A/D）

**Inspector 配置**：
| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `knobTexture` | Texture2D | - | 旋钮刻度盘图片 |
| `minRotation` | float | -15f | 最小旋转角度 |
| `maxRotation` | float | 15f | 最大旋转角度 |
| `rotationStep` | float | 0.5f | 键盘旋转步进 |
| `rotateSpeed` | float | 90f | 键盘旋转速度（度/秒） |
| `doubleClickInterval` | float | 0.3f | 双击间隔 |
| `windowSize` | Vector2 | (500, 400) | 面板尺寸 |
| `windowTitle` | string | "晶体旋转控制" | 面板标题 |

**UI 结构**：
```
CrystalRotationPanel
├── TitleBar
│   ├── TitleText "晶体旋转控制"
│   └── CloseButton [X]
├── Content (HorizontalLayout)
│   ├── XAxis (VerticalLayout)
│   │   ├── Label "X轴旋转"
│   │   ├── Knob (RotationKnob)
│   │   └── AngleDisplay "X: +0.0°"
│   └── YAxis (VerticalLayout)
│       ├── Label "Y轴旋转"
│       ├── Knob (RotationKnob)
│       └── AngleDisplay "Y: +0.0°"
└── ResetButton "重置"
```

### 3.4 RotationKnob

**职责**：
- 可拖拽旋转的旋钮UI
- 角度范围限制
- 触发角度变化事件

**事件**：
```csharp
public event Action<float> OnAngleChanged;
```

**交互**：
- 鼠标拖拽：计算鼠标绕旋钮中心的角度变化
- 角度量化：按 `step` 步进量化
- 范围限制：`minAngle` ~ `maxAngle`

### 3.5 AngleDisplay

**职责**：
- 格式化显示角度值
- 正负号显示

**显示格式**：`X: +5.0°` 或 `Y: -3.5°`

---

## 四、数据流设计

### 4.1 光屏双击弹窗流程

```
[用户双击光屏]
    │
    ↓ ScreenPopupManager.OnMouseDown()
    │
    ├── 检测双击间隔 <= 0.3s
    │
    ↓ HandleDoubleClick()
    │
    ├──→ CrystalRuntime.IsCrystalOnRail(screenPosition, detectionRange)
    │       │
    │       └──→ Vector3.Distance(crystalPos, screenPos) <= detectionRange
    │
    ├──→ [有晶体] ConoscopicWindowView.Show()
    │       │
    │       ├── 创建弹窗（复用 WindowsCanvas）
    │       ├── RawImage.texture = CrystalRuntime.TextureRenderer.RenderTexture
    │       └── 每帧 Update() 调用 UpdateAndRender()
    │
    └──→ [无晶体] DirectScreenController.OpenDisplayWindow()
```

### 4.2 晶体旋转控制流程

```
[用户双击晶体]
    │
    ↓ CrystalRotationPanel.OnMouseDown()
    │
    ├── 检测双击间隔 <= 0.3s
    │
    ↓ TogglePanel() → ShowPanel()
    │
    ├── 创建面板 UI
    ├── 同步当前角度：Controller.GetRotation()
    └── 显示面板

[用户拖拽旋钮]
    │
    ↓ RotationKnob.OnDrag()
    │
    ├── 计算角度变化
    ├── 量化到步进值（0.5°）
    ├── 限制范围 [-15°, +15°]
    └── 触发 OnAngleChanged 事件
    │
    ↓ CrystalRotationPanel.OnXAxisKnobChanged/YChanged()
    │
    ↓ CrystalRuntime.Controller.SetRotation()
    │
    ↓ CrystalControllerWrapper.UpdatePhysicsConfig()
    │
    ↓ CrystalPhysicalCore.ApplyConfig()

[键盘控制]
    │
    ↓ CrystalRotationPanel.Update()
    │
    ├── W/S 键 → X轴 ±0.5°
    ├── A/D 键 → Y轴 ±0.5°
    │
    ↓ Controller.SetRotation()
    │
    ↓ 更新旋钮显示 + 角度显示
```

---

## 五、与 P0-P1 的接口衔接

| P2 组件 | 依赖的 P0-P1 接口 | 用途 |
|---------|------------------|------|
| ScreenPopupManager | `CrystalRuntime.IsCrystalOnRail()` | 检测晶体 |
| ScreenPopupManager | `CrystalRuntime.IsInitialized` | 检查初始化状态 |
| ConoscopicWindowView | `CrystalRuntime.TextureRenderer.RenderTexture` | 获取渲染纹理 |
| ConoscopicWindowView | `CrystalRuntime.TextureRenderer.UpdateAndRender()` | 每帧更新 |
| CrystalRotationPanel | `CrystalRuntime.Controller.SetRotation()` | 设置旋转 |
| CrystalRotationPanel | `CrystalRuntime.Controller.GetRotation()` | 获取当前旋转 |

---

## 六、Unity 配置步骤

### 6.1 光屏配置

1. 在 **Hierarchy** 中找到光屏对象
2. 在 **Inspector** 中点击 **Add Component**
3. 搜索并添加 `ScreenPopupManager`
4. 配置组件：
   - `Direct Screen Controller`: 拖拽同对象上的 `DirectScreenController` 组件
   - `Detection Range`: 2.0
   - `Double Click Interval`: 0.3
   - `Window Size`: (600, 600)
   - `Window Title`: "锥光干涉图"

### 6.2 晶体配置

1. 在 **Hierarchy** 中找到晶体对象（名为"晶体"）
2. 在 **Inspector** 中点击 **Add Component**
3. 搜索并添加 `CrystalRotationPanel`
4. 配置组件：
   - `Knob Texture`: 拖拽 `Assets/UI/mine.png`（或自定义刻度盘图片）
   - `Min Rotation`: -15
   - `Max Rotation`: 15
   - `Rotation Step`: 0.5
   - `Rotate Speed`: 90
   - `Double Click Interval`: 0.3
   - `Window Size`: (500, 400)
   - `Window Title`: "晶体旋转控制"

### 6.3 验证配置

1. 运行场景
2. **测试光屏弹窗**：
   - 无晶体时双击光屏 → 应显示红点弹窗
   - 有晶体时双击光屏 → 应显示锥光干涉图弹窗
3. **测试旋转面板**：
   - 双击晶体 → 应显示旋转控制面板
   - 拖拽旋钮 → 晶体应旋转，干涉图应变化
   - 按 W/S/A/D 键 → 晶体应旋转
   - 点击重置按钮 → 旋转应归零

---

## 七、命名空间总览

| 命名空间 | 包含类 |
|---------|--------|
| `ElectroOptics.DataTransfer` | CrystalSelectionData, CrystalRuntime |
| `ElectroOptics.Experiment.Interfaces` | ICrystalSelectable, ICrystalConfigurable |
| `ElectroOptics.Experiment.Controller` | CrystalControllerWrapper |
| `ElectroOptics.Experiment.Initializer` | CrystalComponentInitializer |
| `ElectroOptics.Experiment.Renderer` | ConoscopicTextureRenderer |
| `ElectroOptics.UI.CrystalSelector` | CrystalCardSelector |
| `ElectroOptics.UI.ScreenPopup` | ScreenPopupManager, ConoscopicWindowView |
| `ElectroOptics.UI.ControlPanel` | CrystalRotationPanel, RotationKnob, AngleDisplay |

---

## 八、错误处理

| 检查点 | 错误条件 | 处理方式 |
|--------|----------|----------|
| 弹窗显示 | CrystalRuntime 未初始化 | 调用红点弹窗，Warning 日志 |
| 弹窗显示 | TextureRenderer 未初始化 | 调用红点弹窗，Warning 日志 |
| 旋钮操作 | Controller 为 null | Warning 日志，不执行操作 |
| 键盘输入 | 面板未显示 | 不处理键盘输入 |

---

**文档结束**

*本文档记录了 P2 阶段的开发成果。*
