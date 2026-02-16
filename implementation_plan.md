# 晶体锥光干涉实验功能实现计划

## 文档信息

| 项目 | 内容 |
|------|------|
| **项目名称** | ElectroOptic Lab Unity - 晶体锥光干涉实验 |
| **文档版本** | 1.0 |
| **创建日期** | 2026-02-16 |
| **Unity版本** | 2022.3.62f2c1 |
| **开发原则** | 解耦设计，不修改原代码 |

---

## 一、需求列表

### 1.1 功能需求

| ID | 需求描述 | 优先级 | 状态 |
|----|----------|--------|------|
| FR-001 | 从场景2-preview选择晶体进入Scene2 | P0 | 待实现 |
| FR-002 | 偏振片消光观察 | P0 | 已完成 |
| FR-003 | 晶体建模放置到导轨 | P1 | 待实现 |
| FR-004 | 双击光屏弹出锥光干涉图 | P2 | 待实现 |
| FR-005 | 晶体XY轴旋转控制面板 | P2 | 待实现 |
| FR-006 | 锥光干涉图随旋转实时变化 | P2 | 待实现 |
| FR-007 | 调零操作（人工观察判别） | P3 | 已完成（无需代码） |

### 1.2 非功能需求

| ID | 需求描述 |
|----|----------|
| NFR-001 | 新代码不修改原代码，保持向后兼容 |
| NFR-002 | 模块间解耦，通过接口通信 |
| NFR-003 | 代码结构清晰，易于维护和扩展 |
| NFR-004 | 支持后续新增晶体类型 |
| NFR-005 | UI样式与现有弹窗保持一致 |

### 1.3 技术约束

| 约束项 | 内容 |
|--------|------|
| Unity版本 | 2022.3.62f2c1 |
| 开发语言 | C# |
| 不修改原代码 | OpticalComponent_Keyboard.cs、DirectScreenController.cs等保持不变 |
| 晶体模型 | Assets/晶体.fbx (GUID: b4e9ab2a09e3e404a9f51fba902130c4) |
| 场景2晶体 | 使用Scene2中已有的晶体视觉模型（物理实验室的一部分） |
| 角度范围 | -15° ~ +15°（XY轴微调） |
| 弹窗内容 | 有晶体时只显示锥光干涉图，无晶体时显示红点 |

---

## 二、数据结构

### 2.1 数据传递类

#### CrystalSelectionData（静态数据类）

```csharp
// 文件：Scripts/DataTransfer/CrystalSelectionData.cs
// 职责：场景间传递选择的晶体Profile
public static class CrystalSelectionData
{
    // 选中的晶体Profile
    public static CrystalProfile SelectedProfile { get; set; }

    // 清除选择
    public static void Clear()
    {
        SelectedProfile = null;
    }

    // 检查是否有选择
    public static bool HasSelection => SelectedProfile != null;
}
```

### 2.2 配置数据类

#### CrystalConfig（已存在，复用）

```csharp
// 文件：Scripts/Business_logic/CrystalConfig.cs（已存在）
// 结构：见原代码
// 用途：封装晶体运行时配置
```

#### CrystalProfile（已存在，复用）

```csharp
// 文件：Scripts/Business_logic/CrystalProfile.cs（已存在）
// 已有资源：
//   - Assets/KDP.asset
//   - Assets/LiNbO3_Profile.asset
```

### 2.3 UI状态类

#### ConoscopicWindowState

```csharp
// 文件：Scripts/UI/ScreenPopup/ConoscopicWindowState.cs
// 职责：锥光干涉弹窗的状态管理
public class ConoscopicWindowState
{
    public bool IsWindowVisible { get; private set; }
    public Vector2 CurrentPosition { get; private set; }
    public CrystalControllerWrapper ActiveCrystal { get; set; }

    public void ShowWindow(Vector2 position)
    {
        CurrentPosition = position;
        IsWindowVisible = true;
    }

    public void HideWindow()
    {
        IsWindowVisible = false;
    }
}
```

#### CrystalControlPanelState

```csharp
// 文件：Scripts/UI/ControlPanel/CrystalControlPanelState.cs
// 职责：控制面板的状态管理
public class CrystalControlPanelState
{
    public bool IsPanelVisible { get; private set; }
    public Vector2 CurrentRotation { get; private set; }
    public CrystalControllerWrapper ActiveController { get; set; }

    public void ShowPanel()
    {
        IsPanelVisible = true;
    }

    public void HidePanel()
    {
        IsPanelVisible = false;
    }

    public void UpdateRotation(Vector2 rotation)
    {
        CurrentRotation = rotation;
    }
}
```

---

## 三、接口定义

### 3.1 核心接口

#### ICrystalSelectable

```csharp
// 文件：Scripts/Experiment/Interfaces/ICrystalSelectable.cs
// 职责：定义可被选择进入场景的晶体卡片接口
public interface ICrystalSelectable
{
    /// <summary>
    /// 获取绑定的晶体Profile
    /// </summary>
    CrystalProfile GetCrystalProfile();

    /// <summary>
    /// 处理选择事件
    /// </summary>
    void OnSelected();

    /// <summary>
    /// 获取卡片显示名称
    /// </summary>
    string GetDisplayName();

    /// <summary>
    /// 获取卡片描述
    /// </summary>
    string GetDescription();
}
```

#### ICrystalConfigurable

```csharp
// 文件：Scripts/Experiment/Interfaces/ICrystalConfigurable.cs
// 职责：定义可配置的晶体控制器接口
public interface ICrystalConfigurable
{
    /// <summary>
    /// 设置晶体Profile
    /// </summary>
    void SetProfile(CrystalProfile profile);

    /// <summary>
    /// 获取当前Profile
    /// </summary>
    CrystalProfile GetProfile();

    /// <summary>
    /// 设置旋转角度（XY轴）
    /// </summary>
    /// <param name="rotation">X轴和Y轴的旋转角度</param>
    void SetRotation(Vector2 rotation);

    /// <summary>
    /// 获取当前旋转角度
    /// </summary>
    Vector2 GetRotation();

    /// <summary>
    /// 增加旋转角度（增量）
    /// </summary>
    void AddRotation(Vector2 delta);

    /// <summary>
    /// 重置旋转角度到零
    /// </summary>
    void ResetRotation();

    /// <summary>
    /// 检查是否已初始化
    /// </summary>
    bool IsInitialized();
}
```

#### IScreenPopup

```csharp
// 文件：Scripts/Experiment/Interfaces/IScreenPopup.cs
// 职责：定义弹窗行为接口
public interface IScreenPopup
{
    /// <summary>
    /// 显示弹窗
    /// </summary>
    void Show();

    /// <summary>
    /// 隐藏弹窗
    /// </summary>
    void Hide();

    /// <summary>
    /// 判断弹窗是否可见
    /// </summary>
    bool IsVisible();

    /// <summary>
    /// 关闭回调
    /// </summary>
    void OnClose();

    /// <summary>
    /// 获取弹窗内容尺寸
    /// </summary>
    Vector2 GetContentSize();
}
```

#### IDraggable

```csharp
// 文件：Scripts/Experiment/Interfaces/IDraggable.cs
// 职责：定义可拖拽UI接口
public interface IDraggable
{
    /// <summary>
    /// 开始拖拽
    /// </summary>
    void OnDragStart(PointerEventData eventData);

    /// <summary>
    /// 拖拽中
    /// </summary>
    void OnDrag(PointerEventData eventData);

    /// <summary>
    /// 结束拖拽
    /// </summary>
    void OnDragEnd(PointerEventData eventData);

    /// <summary>
    /// 获取当前拖拽状态
    /// </summary>
    bool IsDragging();
}
```

---

## 四、文件架构

### 4.1 目录结构

```
Assets/Scripts/
├── DataTransfer/                                    # 数据传递模块
│   └── CrystalSelectionData.cs
│
├── UI/                                              # UI模块
│   ├── Common/                                         # 通用UI组件
│   │   ├── WindowFactory.cs
│   │   ├── DraggableWindow.cs
│   │   ├── CloseButton.cs
│   │   └── PanelBackground.cs
│   │
│   ├── CrystalSelector/                                # 晶体选择
│   │   └── CrystalCardSelector.cs
│   │
│   ├── ScreenPopup/                                    # 光屏弹窗
│   │   ├── ScreenPopupManager.cs
│   │   ├── ConoscopicWindowState.cs
│   │   ├── ConoscopicWindowView.cs
│   │   └── ConoscopicTextureRenderer.cs
│   │
│   └── ControlPanel/                                   # 控制面板
│       ├── CrystalRotationPanel.cs
│       ├── CrystalControlPanelState.cs
│       ├── RotationKnob.cs
│       └── AngleDisplay.cs
│
├── Experiment/                                      # 实验模块
│   ├── Interfaces/                                     # 接口定义
│   │   ├── ICrystalSelectable.cs
│   │   ├── ICrystalConfigurable.cs
│   │   ├── IScreenPopup.cs
│   │   └── IDraggable.cs
│   │
│   ├── Initializer/                                    # 初始化
│   │   └── CrystalComponentInitializer.cs
│   │
│   └── Controller/                                     # 控制器
│       └── CrystalControllerWrapper.cs
│
├── [原代码保持不变...]
└── [Business_logic/, DataContract/, ShaderScripts/ 等...]
```

### 4.2 新增文件清单

| 文件路径 | 模块 | 类名 | 说明 |
|---------|------|------|------|
| Scripts/DataTransfer/CrystalSelectionData.cs | DataTransfer | CrystalSelectionData | 静态数据类 |
| Scripts/Experiment/Interfaces/ICrystalSelectable.cs | Experiment | ICrystalSelectable | 晶体选择接口 |
| Scripts/Experiment/Interfaces/ICrystalConfigurable.cs | Experiment | ICrystalConfigurable | 晶体配置接口 |
| Scripts/Experiment/Interfaces/IScreenPopup.cs | Experiment | IScreenPopup | 弹窗接口 |
| Scripts/Experiment/Interfaces/IDraggable.cs | Experiment | IDraggable | 拖拽接口 |
| Scripts/UI/CrystalSelector/CrystalCardSelector.cs | UI | CrystalCardSelector | 晶体卡片选择器 |
| Scripts/UI/Common/WindowFactory.cs | UI | WindowFactory | 窗口工厂 |
| Scripts/UI/Common/DraggableWindow.cs | UI | DraggableWindow | 可拖拽窗口 |
| Scripts/UI/Common/CloseButton.cs | UI | CloseButton | 关闭按钮 |
| Scripts/UI/ScreenPopup/ScreenPopupManager.cs | UI | ScreenPopupManager | 光屏弹窗管理器 |
| Scripts/UI/ScreenPopup/ConoscopicWindowState.cs | UI | ConoscopicWindowState | 弹窗状态 |
| Scripts/UI/ScreenPopup/ConoscopicWindowView.cs | UI | ConoscopicWindowView | 锥光干涉视图 |
| Scripts/UI/ScreenPopup/ConoscopicTextureRenderer.cs | UI | ConoscopicTextureRenderer | 纹理渲染器 |
| Scripts/UI/ControlPanel/CrystalRotationPanel.cs | UI | CrystalRotationPanel | 旋转控制面板 |
| Scripts/UI/ControlPanel/CrystalControlPanelState.cs | UI | CrystalControlPanelState | 面板状态 |
| Scripts/Experiment/Initializer/CrystalComponentInitializer.cs | Experiment | CrystalComponentInitializer | 晶体组件初始化器 |
| Scripts/Experiment/Controller/CrystalControllerWrapper.cs | Experiment | CrystalControllerWrapper | 晶体控制包装器 |

---

## 五、实现步骤

### 阶段1：基础数据传递（P0）

#### 步骤1.1：创建数据传递模块

**文件**：`Scripts/DataTransfer/CrystalSelectionData.cs`

**任务**：
- 创建静态类 `CrystalSelectionData`
- 实现 `SelectedProfile` 属性
- 实现 `Clear()` 和 `HasSelection` 方法
- 添加错误处理（Profile为null检查）

**验收标准**：
- [ ] 类编译通过
- [ ] 静态属性可正常读写
- [ ] HasSelection返回正确的布尔值

---

### 阶段2：晶体选择功能（P0）

#### 步骤2.1：创建接口定义

**文件**：`Scripts/Experiment/Interfaces/ICrystalSelectable.cs`

**任务**：
- 定义 `ICrystalSelectable` 接口
- 定义方法签名：`GetCrystalProfile()`, `OnSelected()`, `GetDisplayName()`, `GetDescription()`

**验收标准**：
- [ ] 接口定义完整
- [ ] 方法签名清晰

---

#### 步骤2.2：实现晶体卡片选择器

**文件**：`Scripts/UI/CrystalSelector/CrystalCardSelector.cs`

**任务**：
- 创建类 `CrystalCardSelector` 实现 `ICrystalSelectable`
- 添加 Inspector 可配置属性：
  - `crystalProfile: CrystalProfile`
  - `displayName: string`
  - `description: string`
  - `cardImage: Sprite`（可选）
- 实现 `OnCardClick()` 方法：
  1. 检查 `crystalProfile` 不为null
  2. 调用 `CrystalSelectionData.SelectedProfile = crystalProfile`
  3. 调用 `SceneManager.LoadScene("Scene2.The Lab")`
- 添加日志输出用于调试

**验收标准**：
- [ ] 点击卡片后 `CrystalSelectionData.SelectedProfile` 正确设置
- [ ] 场景正确切换到 Scene2.The Lab
- [ ] 控制台有调试日志输出

**Unity配置**：
- 在 Scene2-preview 的每个卡片对象上添加此组件
- 在 Inspector 中绑定对应的 CrystalProfile 资源：
  - Card1 → KDP.asset
  - Card1 (1) → LiNbO3_Profile.asset
  - 其他卡片待定

---

### 阶段3：晶体初始化（P1）

#### 步骤3.1：创建晶体配置接口

**文件**：`Scripts/Experiment/Interfaces/ICrystalConfigurable.cs`

**任务**：
- 定义 `ICrystalConfigurable` 接口
- 定义所有必需方法签名

**验收标准**：
- [ ] 接口定义完整
- [ ] 方法包含注释说明

---

#### 步骤3.2：实现晶体控制器包装器

**文件**：`Scripts/Experiment/Controller/CrystalControllerWrapper.cs`

**任务**：
- 创建类 `CrystalControllerWrapper` 实现 `ICrystalConfigurable`
- 添加私有字段：
  - `_physicalCore: CrystalPhysicalCore`
  - `_visualizer: CrystalVisualizer`
  - `_profile: CrystalProfile`
  - `_rotation: Vector2`
- 实现 `Initialize(CrystalPhysicalCore, CrystalVisualizer)` 方法
- 实现 `SetProfile(CrystalProfile)` 方法：
  1. 保存引用
  2. 调用 `UpdatePhysicsConfig()`
- 实现 `SetRotation(Vector2)` 方法：
  1. 限制旋转范围到 -15~15°
  2. 更新 `transform.localRotation`
  3. 调用 `UpdatePhysicsConfig()`
- 实现 `GetRotation()` 方法
- 实现 `AddRotation(Vector2)` 方法
- 实现 `ResetRotation()` 方法
- 实现 `IsInitialized()` 方法
- 实现私有方法 `UpdatePhysicsConfig()`：
  1. 创建 `CrystalConfig`
  2. 设置所有必需属性
  3. 调用 `physicalCore.ApplyConfig(config)`

**验收标准**：
- [ ] 所有接口方法实现
- [ ] 旋转范围正确限制在 -15~15°
- [ ] 物理配置正确更新
- [ ] Visualizer 正确同步

---

#### 步骤3.3：实现晶体组件初始化器

**文件**：`Scripts/Experiment/Initializer/CrystalComponentInitializer.cs`

**任务**：
- 创建类 `CrystalComponentInitializer`
- 添加 Inspector 可配置属性：
  - `crystalModel: GameObject`（可选，为空时自动查找）
  - `conoscopicMaterial: Material`（引用 Mat_Conoscopic）
  - `crystalLayer: LayerMask`（用于检测）
- 实现 `Start()` 方法：
  1. 查找晶体模型：
     - 首先检查 `crystalModel` 是否已设置
     - 若未设置，通过 `GameObject.FindWithTag("Crystal")` 查找
     - 若仍未找到，通过 `GameObject.Find("晶体")` 查找
  2. 验证晶体模型存在
  3. 调用 `SetupCrystalComponents()`
  4. 调用 `LoadSelectedProfile()`
- 实现私有方法 `SetupCrystalComponents(GameObject crystal)`：
  1. 检查并添加 `CrystalPhysicalCore`
  2. 检查并添加 `CrystalVisualizer`
  3. 获取 `Renderer` 组件
  4. 设置 `Renderer.sharedMaterial = conoscopicMaterial`
  5. 配置 `visualizer.targetScreenRenderer = renderer`
  6. 检查并添加 `CrystalControllerWrapper`
  7. 调用 `controller.Initialize(physicalCore, visualizer)`
  8. 添加 `OpticalComponent` 脚本（如果不存在）
- 实现私有方法 `LoadSelectedProfile()`：
  1. 检查 `CrystalSelectionData.HasSelection`
  2. 若有选择，调用 `controller.SetProfile(selectedProfile)`
  3. 记录加载日志

**验收标准**：
- [ ] 启动时晶体组件正确初始化
- [ ] 材质正确设置为 Mat_Conoscopic
- [ ] 选中的 Profile 正确加载
- [ ] CrystalControllerWrapper 正确配置

**Unity配置**：
- 在 Scene2.The Lab 中创建空 GameObject，命名为 "CrystalInitializer"
- 添加 `CrystalComponentInitializer` 组件
- 配置 `conoscopicMaterial` 为 Assets/Shaders/Mat_Conoscopic
- 配置 `crystalLayer` 为晶体所在层级

---

### 阶段4：UI通用组件（P2）

#### 步骤4.1：创建弹窗接口

**文件**：`Scripts/Experiment/Interfaces/IScreenPopup.cs`

**任务**：
- 定义 `IScreenPopup` 接口
- 定义所有必需方法签名

**验收标准**：
- [ ] 接口定义完整

---

#### 步骤4.2：创建拖拽接口

**文件**：`Scripts/Experiment/Interfaces/IDraggable.cs`

**任务**：
- 定义 `IDraggable` 接口
- 定义所有必需方法签名

**验收标准**：
- [ ] 接口定义完整

---

#### 步骤4.3：实现窗口工厂

**文件**：`Scripts/UI/Common/WindowFactory.cs`

**任务**：
- 创建静态类 `WindowFactory`
- 实现方法 `GetOrCreateCanvas()`：
  1. 检查 `GameObject.Find("WindowsCanvas")`
  2. 若不存在，创建：
     - Canvas：ScreenSpaceOverlay
     - CanvasScaler：1920x1080 参考
     - GraphicRaycaster
- 实现方法 `CreateWindow(string title, Vector2 size, bool draggable)`：
  1. 创建窗口 GameObject
  2. 设置 RectTransform
  3. 添加 Image 背景
  4. 添加标题栏
  5. 添加关闭按钮
  6. 可选：添加 `DraggableWindow` 组件

**验收标准**：
- [ ] Canvas 正确创建或复用
- [ ] 窗口结构完整（背景、标题、关闭按钮）
- [ ] 窗口可拖拽（可选功能）

---

#### 步骤4.4：实现可拖拽窗口

**文件**：`Scripts/UI/Common/DraggableWindow.cs`

**任务**：
- 创建类 `DraggableWindow` 实现 `IDraggable`
- 实现 `IPointerDownHandler`, `IDragHandler`, `IPointerUpHandler`
- 实现 `OnPointerDown()`：
  1. 记录拖拽起始位置
  2. 设置拖拽状态
- 实现 `OnDrag()`：
  1. 计算鼠标位移
  2. 更新窗口位置
  3. 处理 Canvas 缩放
- 实现 `OnPointerUp()`：
  1. 清除拖拽状态
- 实现状态管理属性

**验收标准**：
- [ ] 窗口可正常拖拽
- [ ] 拖拽跟随鼠标
- [ ] 拖拽结束状态正确

---

#### 步骤4.5：实现关闭按钮

**文件**：`Scripts/UI/Common/CloseButton.cs`

**任务**：
- 创建类 `CloseButton`
- 添加 Inspector 可配置属性：
  - `onClick: Action`（Unity Inspector中通过事件设置）
- 创建 UI 结构：
  1. 关闭按钮 GameObject
  2. RectTransform 配置
  3. Image 组件
  4. Button 组件
  5. Text 组件（显示 "✕"）
- 实现 `CreateButton(GameObject parent, Action onClose)` 静态方法

**验收标准**：
- [ ] 关闭按钮样式正确
- [ ] 点击后触发关闭回调

---

### 阶段5：光屏弹窗功能（P2）

#### 步骤5.1：实现弹窗状态管理

**文件**：`Scripts/UI/ScreenPopup/ConoscopicWindowState.cs`

**任务**：
- 实现状态类（见数据结构部分）
- 添加线程安全考虑（单线程Unity环境，可选）

**验收标准**：
- [ ] 状态管理正确
- [ ] 状态可查询

---

#### 步骤5.2：实现锥光干涉视图

**文件**：`Scripts/UI/ScreenPopup/ConoscopicWindowView.cs`

**任务**：
- 创建类 `ConoscopicWindowView` 实现 `IScreenPopup`
- 添加 Inspector 可配置属性：
  - `windowSize: Vector2`
  - `borderWidth: float`
  - `backgroundColor: Color`
- 实现 `Show()` 方法：
  1. 调用 `WindowFactory.CreateWindow()`
  2. 创建内容区域
  3. 创建 RawImage 用于显示干涉图
  4. 配置 RawImage 的引用
- 实现 `Hide()` 方法：
  1. 设置窗口 GameObject.SetActive(false)
- 实现 `IsVisible()` 方法：
  1. 返回窗口 activeSelf
- 实现 `OnClose()` 方法：
  1. 隐藏窗口
- 实现方法 `SetTexture(Texture2D texture)`：
  1. 更新 RawImage 的 texture
- 实现方法 `UpdateFromCrystal(CrystalVisualizer visualizer)`：
  1. 获取 visualizer 的目标渲染器
  2. 获取渲染器的材质
  3. 创建临时摄像机渲染到 Texture2D
  4. 设置 RawImage 的 texture

**验收标准**：
- [ ] 弹窗正确显示
- [ ] 弹窗可关闭
- [ ] 锥光干涉图正确显示

---

#### 步骤5.3：实现光屏弹窗管理器

**文件**：`Scripts/UI/ScreenPopup/ScreenPopupManager.cs`

**任务**：
- 创建类 `ScreenPopupManager`
- 添加 Inspector 可配置属性：
  - `originalScreenController: DirectScreenController`（引用现有组件）
  - `crystalLayer: LayerMask`（用于检测晶体）
  - `detectionRange: float = 5f`（检测范围）
  - `doubleClickInterval: float = 0.3f`
  - `conoscopicWindowPrefab: GameObject`（可选）
- 添加私有字段：
  - `_lastClickTime: float`
  - `_conoscopicView: ConoscopicWindowView`
  - `_activeCrystal: CrystalControllerWrapper`
- 实现 `Start()` 方法：
  1. 获取或查找 `DirectScreenController`
  2. 初始化弹窗状态
- 实现 `OnMouseDown()` 方法：
  1. 检测双击
  2. 若双击，调用 `HandlePopupDecision()`
- 实现方法 `HandlePopupDecision()`：
  1. 调用 `HasCrystalOnRail()` 检测晶体
  2. 若有晶体：显示锥光干涉弹窗
  3. 若无晶体：显示红点弹窗（调用 `originalScreenController.OpenDisplayWindow()`）
- 实现私有方法 `HasCrystalOnRay()`（光路检测，可选扩展）：
  1. 从激光发射器到光屏进行 Raycast
  2. 检测 Raycast 路径上是否有晶体
- 实现私有方法 `HasCrystalOnRail()`：
  1. 使用 `Physics.OverlapSphere()` 检测范围
  2. 检查碰撞体是否有 `CrystalControllerWrapper` 组件
  3. 若有，返回 true
- 实现方法 `UpdateConoscopicPattern()`：
  1. 获取当前激活的晶体控制器
  2. 获取晶体的 `CrystalVisualizer`
  3. 更新弹窗的纹理显示

**验收标准**：
- [ ] 双击检测正确
- [ ] 晶体检测逻辑正确
- [ ] 无晶体时显示红点弹窗
- [ ] 有晶体时显示锥光干涉弹窗
- [ ] 锥光干涉图正确更新

**Unity配置**：
- 在 Scene2.The Lab 的光屏对象上添加此组件
- 引用现有的 `DirectScreenController` 组件
- 配置晶体检测层级

---

### 阶段6：晶体控制面板（P2）

#### 步骤6.1：实现面板状态管理

**文件**：`Scripts/UI/ControlPanel/CrystalControlPanelState.cs`

**任务**：
- 实现状态类（见数据结构部分）

**验收标准**：
- [ ] 状态管理正确

---

#### 步骤6.2：实现晶体旋转面板

**文件**：`Scripts/UI/ControlPanel/CrystalRotationPanel.cs`

**任务**：
- 创建类 `CrystalRotationPanel`
- 添加 Inspector 可配置属性：
  - `crystalController: CrystalControllerWrapper`（引用）
  - `windowSize: Vector2`
  - `minRotation: float = -15f`
  - `maxRotation: float = 15f`
  - `rotationStep: float = 0.1f`（滑块步进）
- 添加 UI 组件（动态创建或引用预制体）：
  - 窗口容器
  - 标题栏
  - 关闭按钮
  - X轴旋转区域：
    - 标签 "X轴旋转"
    - Slider 控件
    - 角度显示 Text
  - Y轴旋转区域：
    - 标签 "Y轴旋转"
    - Slider 控件
    - 角度显示 Text
  - 重置按钮（可选）
- 实现 `Start()` 方法：
  1. 查找或获取 `CrystalControllerWrapper`
  2. 初始化 UI 控件
  3. 绑定事件回调
- 实现 `OnCrystalDoubleClick()` 方法：
  1. 检查面板是否已显示
  2. 若未显示，调用 `ShowPanel()`
  3. 若已显示，不处理
- 实现 `ShowPanel()` 方法：
  1. 使用 `WindowFactory.CreateWindow()` 创建面板
  2. 显示面板
  3. 更新当前角度显示
- 实现 `HidePanel()` 方法：
  1. 隐藏面板
- 实现回调 `OnRotationXChanged(float value)`：
  1. 创建 Vector2 rotation = new Vector2(value, currentY)
  2. 调用 `crystalController.SetRotation(rotation)`
  3. 更新角度显示 Text
- 实现回调 `OnRotationYChanged(float value)`：
  1. 创建 Vector2 rotation = new Vector2(currentX, value)
  2. 调用 `crystalController.SetRotation(rotation)`
  3. 更新角度显示 Text
- 实现方法 `UpdateAngleDisplay()`：
  1. 获取当前旋转值
  2. 更新 X 和 Y 轴的 Text 显示
  3. 格式化为 "X: +XX.X°", "Y: +XX.X°"
- 实现重置按钮回调（可选）：
  1. 调用 `crystalController.ResetRotation()`
  2. 更新 UI 显示

**验收标准**：
- [ ] 双击晶体后面板正确显示
- [ ] X/Y滑块可正常操作
- [ ] 角度显示实时更新
- [ ] 角度限制在 -15~15° 范围内
- [ ] 面板可关闭
- [ ] 面板可拖拽

**Unity配置**：
- 方式1（推荐）：在 Scene2.The Lab 的晶体对象上添加此组件
- 方式2（备用）：创建独立的脚本管理器，在运行时查找晶体并显示面板

---

## 六、数据流设计

### 6.1 晶体选择流程

```
用户操作
    ↓
点击晶体卡片 (Scene2-preview)
    ↓
CrystalCardSelector.OnCardClick()
    ↓
验证 crystalProfile != null
    ↓
CrystalSelectionData.SelectedProfile = profile
    ↓
SceneManager.LoadScene("Scene2.The Lab")
    ↓
场景切换完成
```

### 6.2 晶体初始化流程

```
Scene2.The Lab 启动
    ↓
CrystalComponentInitializer.Start()
    ↓
查找/接收晶体模型
    ↓
SetupCrystalComponents()
    ├─→ 添加 CrystalPhysicalCore
    ├─→ 添加 CrystalVisualizer
    ├─→ 配置 Mat_Conoscopic 材质
    ├─→ 添加 CrystalControllerWrapper
    └─→ controller.Initialize(physicalCore, visualizer)
    ↓
LoadSelectedProfile()
    ↓
检查 CrystalSelectionData.HasSelection
    ↓
若有选择
    ↓
controller.SetProfile(selectedProfile)
    ↓
创建 CrystalConfig
    ↓
physicalCore.ApplyConfig(config)
    ↓
Shader 更新完成
```

### 6.3 光屏弹窗流程

```
用户双击光屏
    ↓
ScreenPopupManager.OnMouseDown()
    ↓
检测双击 (时间间隔 < 0.3s)
    ↓
HandlePopupDecision()
    ↓
HasCrystalOnRail() ? [检测晶体]
    ├─→ [有晶体]
    │       ↓
    │   ShowConoscopicWindow()
    │       ↓
    │   获取晶体的 CrystalVisualizer
    │       ↓
    │   渲染锥光干涉图到弹窗
    │
    └─→ [无晶体]
            ↓
        originalScreenController.OpenDisplayWindow()
            ↓
        显示红点弹窗
```

### 6.4 晶体控制流程

```
用户双击晶体
    ↓
CrystalRotationPanel.OnCrystalDoubleClick()
    ↓
显示控制面板
    ↓
用户调节 X/Y 滑块
    ↓
OnRotationXChanged/YChanged(value)
    ↓
crystalController.SetRotation(Vector2)
    ↓
限制旋转范围 (-15~15°)
    ↓
transform.localRotation = Quaternion.Euler(x, y, 0)
    ↓
UpdatePhysicsConfig()
    ↓
创建 CrystalConfig
    ↓
physicalCore.ApplyConfig(config)
    ↓
Shader 参数更新
    ↓
锥光干涉图实时变化
```

---

## 七、Unity配置清单

### 7.1 Scene2-preview 配置

| 对象 | 组件 | 配置项 | 配置值 |
|------|------|--------|--------|
| Card1 | CrystalCardSelector | crystalProfile | KDP.asset |
| Card1 (1) | CrystalCardSelector | crystalProfile | LiNbO3_Profile.asset |
| Card1 (2) | CrystalCardSelector | crystalProfile | (待新增) |
| "Btn返回" | Button | OnClick | SceneManager.LoadScene(0) |

### 7.2 Scene2.The Lab 配置

| 对象 | 组件 | 配置项 | 配置值 |
|------|------|--------|--------|
| 晶体模型 | CrystalComponentInitializer | conoscopicMaterial | Mat_Conoscopic |
| 晶体模型 | CrystalComponentInitializer | crystalLayer | 晶体所在层级 |
| 光屏 | ScreenPopupManager | originalScreenController | DirectScreenController引用 |
| 光屏 | ScreenPopupManager | crystalLayer | 晶体所在层级 |
| 光屏 | ScreenPopupManager | detectionRange | 5.0 |
| 晶体模型 | CrystalRotationPanel | crystalController | CrystalControllerWrapper引用 |

### 7.3 层级设置

| 层级名称 | 包含对象 |
|---------|----------|
| Default | 大部分对象 |
| Crystal | 晶体模型 |
| Rail | 导轨对象 |
| Screen | 光屏对象 |
| UI | UI 元素 |

### 7.4 标签设置

| 标签名称 | 用途 |
|---------|------|
| Crystal | 标识晶体对象（用于自动查找） |
| Screen | 标识光屏对象 |
| Rail | 标识导轨对象 |

---

## 八、测试计划

### 8.1 单元测试

| 测试类 | 测试方法 | 预期结果 |
|--------|----------|----------|
| CrystalSelectionDataTests | Test_SetAndGetProfile | 设置和获取的Profile一致 |
| CrystalSelectionDataTests | Test_Clear | Clear后SelectedProfile为null |
| CrystalControllerWrapperTests | Test_SetRotation_Clamping | 旋转值限制在-15~15° |
| CrystalControllerWrapperTests | Test_ResetRotation | Reset后旋转为(0,0) |
| CrystalControllerWrapperTests | Test_AddRotation | 增量旋转正确累加 |

### 8.2 集成测试

| 测试场景 | 测试步骤 | 预期结果 |
|----------|----------|----------|
| 晶体选择集成 | 1. 在Scene2-preview点击卡片<br>2. 进入Scene2<br>3. 检查Profile | 选择的Profile正确传递到Scene2 |
| 晶体初始化集成 | 1. 场景2启动<br>2. 检查晶体组件<br>3. 检查材质 | 所有组件正确添加，材质正确 |
| 光屏弹窗集成 | 1. 场景2无晶体<br>2. 双击光屏<br>3. 放置晶体<br>4. 双击光屏 | 无晶体显示红点，有晶体显示锥光干涉 |
| 控制面板集成 | 1. 双击晶体<br>2. 调节滑块<br>3. 检查旋转 | 面板显示，旋转正确更新，干涉图变化 |

### 8.3 端到端测试

| 测试场景 | 测试步骤 | 预期结果 |
|----------|----------|----------|
| 完整实验流程 | 1. Scene2-preview选择KDP<br>2. 进入Scene2<br>3. 将晶体拖到导轨<br>4. 双击晶体显示控制面板<br>5. 调节旋转进行调零<br>6. 双击光屏查看锥光干涉 | 整个流程无错误，锥光干涉图随旋转实时变化 |

---

## 九、错误处理与日志

### 9.1 关键检查点

| 检查点 | 错误条件 | 处理方式 |
|--------|----------|----------|
| 卡片选择 | crystalProfile == null | 警告日志，不执行场景切换 |
| 晶体初始化 | 晶体模型未找到 | 错误日志，组件禁用 |
| Profile加载 | CrystalSelectionData无选择 | 警告日志，使用默认Profile |
| 弹窗显示 | originalScreenController == null | 错误日志，弹窗功能禁用 |
| 控制面板 | crystalController == null | 错误日志，面板显示警告信息 |
| 物理配置 | physicalCore == null | 错误日志，功能禁用 |

### 9.2 日志输出规范

```csharp
// 信息日志
Debug.Log("[CrystalSelector] Selected profile: " + profile.crystalName);

// 警告日志
Debug.LogWarning("[CrystalInitializer] No crystal model found in scene.");

// 错误日志
Debug.LogError("[ScreenPopupManager] OriginalScreenController reference is missing!");
```

---

## 十、开发注意事项

### 10.1 坐标系处理

| 坐标系 | 说明 |
|--------|------|
| **Unity世界坐标系** | 左手系，X右Y上Z前 |
| **Unity局部坐标系** | 晶体自身的坐标系 |
| **DLL坐标系** | 右手系（通过Z-flip转换） |

**旋转映射：**
```csharp
// 用户界面输入 (XY轴旋转，-15~15°)
// 转换为 Unity 局部旋转
Vector2 userRotation = new Vector2(rotationX, rotationY);

// 应用到晶体 transform
transform.localRotation = Quaternion.Euler(userRotation.x, userRotation.y, 0);

// 传递给物理核心
config.crystalRotation = transform.localRotation;
```

### 10.2 线程安全

- 所有 UI 操作在主线程执行（Unity要求）
- CrystalSelectionData 为静态类，场景切换时自动清理

### 10.3 内存管理

- 弹窗窗口使用 `DontDestroyOnLoad` 保持跨场景（可选）
- 或者每个场景重新创建弹窗（推荐，避免状态残留）

### 10.4 性能优化

| 优化点 | 说明 |
|--------|------|
| 晶体检测缓存 | 使用标记避免每帧检测 |
| 弹窗更新频率 | 锥光干涉图按需更新，非每帧更新 |
| 材质实例化 | 使用 `.sharedMaterial` 避免重复创建 |

### 10.5 可扩展性考虑

| 扩展点 | 说明 |
|--------|------|
| 新增晶体类型 | 只需创建新的 CrystalProfile 资源 |
| 新增弹窗类型 | 实现 IScreenPopup 接口即可 |
| 新增控制参数 | 扩展 ICrystalConfigurable 接口 |

---

## 十一、里程碑

| 里程碑 | 完成标准 | 预计工作量 |
|--------|----------|------------|
| **M1: 数据传递** | CrystalSelectionData 实现并测试 | 0.5天 |
| **M2: 晶体选择** | CrystalCardSelector 实现并配置到Scene2-preview | 1天 |
| **M3: 晶体初始化** | CrystalComponentInitializer + CrystalControllerWrapper 实现 | 1.5天 |
| **M4: UI通用组件** | WindowFactory + DraggableWindow + CloseButton 实现 | 1天 |
| **M5: 光屏弹窗** | ScreenPopupManager + ConoscopicWindowView 实现 | 1.5天 |
| **M6: 控制面板** | CrystalRotationPanel 实现 | 1天 |
| **M7: 集成测试** | 端到端测试和Bug修复 | 1天 |

---

## 十二、附录

### 附录A：现有资源清单

| 资源类型 | 路径 | 说明 |
|---------|--------|------|
| 晶体模型 | Assets/晶体.fbx | 晶体3D模型（GUID: b4e9ab2a09e3e404a9f51fba902130c4） |
| KDP Profile | Assets/KDP.asset | KDP晶体配置 |
| LiNbO3 Profile | Assets/LiNbO3_Profile.asset | LiNbO3晶体配置 |
| 导轨模型 | Assets/导轨.fbx | 导轨3D模型 |
| 偏振镜模型 | Assets/偏振镜.fbx | 偏振镜3D模型 |
| 光屏模型 | Assets/光屏.fbx | 光屏3D模型 |
| 激光发射器 | Assets/激光发射器.fbx | 激光发射器3D模型 |
| 接收器 | Assets/接收器.fbx | 接收器3D模型 |
| 锥光干涉Shader | Assets/Shaders/ConoscopicInterference.shader | 锥光干涉着色器 |
| 锥光干涉材质 | Assets/Shaders/Mat_Conoscopic.mat | 锥光干涉材质 |

### 附录B：现有脚本清单

| 脚本文件 | 功能 | 是否修改 |
|---------|------|---------|
| Scripts/Business_logic/CrystalProfile.cs | 晶体Profile定义 | 否 |
| Scripts/Business_logic/CrystalPhysicalCore.cs | 晶体物理核心 | 否 |
| Scripts/Business_logic/CrystalConfig.cs | 晶体配置 | 否 |
| Scripts/Business_logic/LabController.cs | 实验室控制器 | 否 |
| Scripts/ShaderScripts/CrystalVisualizer.cs | 晶体可视化 | 否 |
| Scripts/DataContract/NativeInterface.cs | DLL接口 | 否 |
| Scripts/DataContract/DataContracts.cs | 数据契约 | 否 |
| Scripts/OpticalComponent_Keyboard.cs | 导轨组件交互 | 否 |
| Scripts/DirectScreenController.cs | 光屏控制器（红点） | 否 |
| Scripts/PolarizerPhysics.cs | 偏振片物理 | 否 |
| Scripts/RotateStandController.cs | 偏振片旋转控制 | 否 |
| Scripts/RotateWindowController.cs | 偏振片旋转窗口 | 否 |
| Scripts/Cardclick.cs | 卡片点击（仅跳转场景） | 否 |

### 附录C：参考文档

| 文档 | 说明 |
|------|------|
| CLAUDE.md | 代码库开发指南 |
| implementation_plan.md | 本实现计划 |

---

**文档结束**

*本计划作为后续开发的重要依据，任何变更请及时更新此文档。*
