# 晶体锥光干涉实验 - P0-P1 阶段开发文档

## 文档信息

| 项目 | 内容 |
|------|------|
| **项目名称** | ElectroOptic Lab Unity - 晶体锥光干涉实验 |
| **文档版本** | 1.1 |
| **创建日期** | 2026-02-17 |
| **Unity版本** | 2022.3.62f2c1 |
| **开发原则** | 解耦设计，不修改原代码 |
| **当前阶段** | P0-P1-P2 已完成 |

---

## 一、需求列表

### 1.1 功能需求

| ID | 需求描述 | 优先级 | 状态 | 实现文件 |
|----|----------|--------|------|----------|
| FR-001 | 从场景2-preview选择晶体进入Scene2 | P0 | **已完成** | CrystalCardSelector.cs, CrystalSelectionData.cs |
| FR-002 | 偏振片消光观察 | P0 | 已完成（原有） | - |
| FR-003 | 晶体建模放置到导轨 | P1 | **已完成** | CrystalComponentInitializer.cs |
| FR-004 | 双击光屏弹出锥光干涉图 | P2 | **已完成** | ScreenPopupManager.cs, ConoscopicWindowView.cs |
| FR-005 | 晶体XY轴旋转控制面板 | P2 | **已完成** | CrystalRotationPanel.cs, RotationKnob.cs, AngleDisplay.cs |
| FR-006 | 锥光干涉图随旋转实时变化 | P2 | **已完成** | ConoscopicWindowView.cs (每帧更新) |
| FR-007 | 调零操作（人工观察判别） | P3 | 已完成（无需代码） | - |

### 1.2 非功能需求

| ID | 需求描述 | 实现方式 |
|----|----------|----------|
| NFR-001 | 新代码不修改原代码，保持向后兼容 | 所有新代码在独立目录 |
| NFR-002 | 模块间解耦，通过接口通信 | 定义 ICrystalSelectable, ICrystalConfigurable 接口 |
| NFR-003 | 代码结构清晰，易于维护和扩展 | 按职责划分目录结构 |
| NFR-004 | 支持后续新增晶体类型 | CrystalProfile 作为 ScriptableObject |
| NFR-005 | UI样式与现有弹窗保持一致 | 复用 WindowsCanvas 和 SimpleDrag |

### 1.3 技术约束

| 约束项 | 内容 |
|--------|------|
| Unity版本 | 2022.3.62f2c1 |
| 开发语言 | C# |
| 不修改原代码 | CrystalPhysicalCore.cs, DirectScreenController.cs 等保持不变 |
| 晶体模型 | Scene2中名为"晶体"的 GameObject |
| 角度范围 | -15° ~ +15°（XY轴微调） |
| 弹窗内容 | 有晶体时显示锥光干涉图，无晶体时显示红点 |
| 干涉图显示 | 仅在弹窗中显示，不在3D场景中显示 |

---

## 二、数据结构

### 2.1 场景间数据传递

#### CrystalSelectionData（静态数据类）

```csharp
// 文件：Scripts/DataTransfer/CrystalSelectionData.cs
// 命名空间：ElectroOptics.DataTransfer
// 职责：场景间传递选择的晶体 Profile

public static class CrystalSelectionData
{
    // 选中的晶体 Profile
    public static CrystalProfile SelectedProfile { get; set; }

    // 是否有选中的晶体
    public static bool HasSelection => SelectedProfile != null;

    // 清除选择数据
    public static void Clear() => SelectedProfile = null;
}
```

#### CrystalRuntime（运行时引用类）

```csharp
// 文件：Scripts/DataTransfer/CrystalRuntime.cs
// 命名空间：ElectroOptics.DataTransfer
// 职责：Scene2 中晶体组件的静态访问入口

public static class CrystalRuntime
{
    // 晶体 GameObject
    public static GameObject CrystalObject { get; set; }

    // 晶体控制器
    public static CrystalControllerWrapper Controller { get; set; }

    // 锥光干涉纹理渲染器
    public static ConoscopicTextureRenderer TextureRenderer { get; set; }

    // 晶体物理核心
    public static CrystalPhysicalCore PhysicalCore { get; set; }

    // 是否已初始化
    public static bool IsInitialized => CrystalObject != null && Controller != null;

    // 清理所有引用
    public static void Clear() { ... }

    // 检查晶体是否在导轨上
    public static bool IsCrystalOnRail(Vector3 railPosition, float detectionRange) { ... }
}
```

### 2.2 复用的现有数据结构

#### CrystalConfig（已存在）

```csharp
// 文件：Scripts/Business_logic/CrystalConfig.cs
// 命名空间：ElectroOptics
// 用途：封装晶体运行时配置

public struct CrystalConfig
{
    public CrystalProfile profile;           // 晶体 Profile
    public Quaternion crystalRotation;       // 晶体旋转
    public Vector3 localEField;              // 局部电场矢量
    public Vector3 probeFieldDirection;      // 探测电场方向
    public Vector3 worldLightDirection;      // 世界空间光传播方向
}
```

#### CrystalProfile（已存在）

```csharp
// 文件：Scripts/Business_logic/CrystalProfile.cs
// 命名空间：ElectroOptics
// 用途：ScriptableObject 定义晶体属性

public class CrystalProfile : ScriptableObject
{
    public string crystalName;               // 晶体名称
    public double defaultWavelength_nm;      // 默认波长 (nm)
    public double n_x, n_y, n_z;             // 折射率
    public double r11...r63;                 // 电光系数 (pm/V)
    public double defaultLength_mm;          // 默认长度 (mm)
    public double defaultThickness_mm;       // 默认厚度 (mm)
}
```

---

## 三、接口定义

### 3.1 ICrystalSelectable（晶体选择接口）

```csharp
// 文件：Scripts/Experiment/Interfaces/ICrystalSelectable.cs
// 命名空间：ElectroOptics.Experiment.Interfaces
// 职责：定义可被选择进入场景的晶体卡片接口

public interface ICrystalSelectable
{
    /// <summary>
    /// 获取绑定的晶体 Profile
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

### 3.2 ICrystalConfigurable（晶体配置接口）

```csharp
// 文件：Scripts/Experiment/Interfaces/ICrystalConfigurable.cs
// 命名空间：ElectroOptics.Experiment.Interfaces
// 职责：定义可配置的晶体控制器接口

public interface ICrystalConfigurable
{
    /// <summary>
    /// 设置晶体 Profile
    /// </summary>
    void SetProfile(CrystalProfile profile);

    /// <summary>
    /// 获取当前 Profile
    /// </summary>
    CrystalProfile GetProfile();

    /// <summary>
    /// 设置旋转角度（X=俯仰, Y=偏航）
    /// </summary>
    void SetRotation(Vector2 rotation);

    /// <summary>
    /// 获取当前旋转角度
    /// </summary>
    Vector2 GetRotation();

    /// <summary>
    /// 增量旋转
    /// </summary>
    void AddRotation(Vector2 delta);

    /// <summary>
    /// 重置旋转到零
    /// </summary>
    void ResetRotation();

    /// <summary>
    /// 是否已初始化
    /// </summary>
    bool IsInitialized();
}
```

---

## 四、文件架构

### 4.1 目录结构

```
Assets/Scripts/
├── DataTransfer/                                    # 数据传递模块 [P0 新增]
│   ├── CrystalSelectionData.cs                      # 场景间选择数据
│   └── CrystalRuntime.cs                            # 运行时引用管理
│
├── Experiment/                                      # 实验模块 [P0-P1 新增]
│   ├── Interfaces/                                  # 接口定义
│   │   ├── ICrystalSelectable.cs                    # 晶体选择接口
│   │   └── ICrystalConfigurable.cs                  # 晶体配置接口
│   │
│   ├── Controller/                                  # 控制器
│   │   └── CrystalControllerWrapper.cs              # 晶体控制包装器
│   │
│   ├── Initializer/                                 # 初始化器
│   │   └── CrystalComponentInitializer.cs           # 晶体组件初始化器
│   │
│   └── Renderer/                                    # 渲染器
│       └── ConoscopicTextureRenderer.cs             # 锥光干涉纹理渲染器
│
├── UI/                                              # UI模块 [P0 新增]
│   └── CrystalSelector/                             # 晶体选择
│       └── CrystalCardSelector.cs                   # 晶体卡片选择器
│
├── [P2 待新增目录]
│   ├── UI/Common/                                   # 通用UI组件
│   ├── UI/ScreenPopup/                              # 光屏弹窗
│   └── UI/ControlPanel/                             # 控制面板
│
└── [原代码保持不变]
    ├── Business_logic/                              # 业务逻辑
    ├── DataContract/                                # 数据契约
    ├── ShaderScripts/                               # Shader脚本
    ├── Buttons/                                     # 按钮控制
    └── *.cs                                         # 其他脚本
```

### 4.2 P0-P1 新增文件清单

| 文件路径 | 命名空间 | 职责 |
|---------|----------|------|
| `Scripts/DataTransfer/CrystalSelectionData.cs` | ElectroOptics.DataTransfer | 场景间数据传递 |
| `Scripts/DataTransfer/CrystalRuntime.cs` | ElectroOptics.DataTransfer | 运行时引用管理 |
| `Scripts/Experiment/Interfaces/ICrystalSelectable.cs` | ElectroOptics.Experiment.Interfaces | 晶体选择接口 |
| `Scripts/Experiment/Interfaces/ICrystalConfigurable.cs` | ElectroOptics.Experiment.Interfaces | 晶体配置接口 |
| `Scripts/Experiment/Controller/CrystalControllerWrapper.cs` | ElectroOptics.Experiment.Controller | 晶体控制包装器 |
| `Scripts/Experiment/Initializer/CrystalComponentInitializer.cs` | ElectroOptics.Experiment.Initializer | 晶体组件初始化器 |
| `Scripts/Experiment/Renderer/ConoscopicTextureRenderer.cs` | ElectroOptics.Experiment.Renderer | 锥光干涉纹理渲染器 |
| `Scripts/UI/CrystalSelector/CrystalCardSelector.cs` | ElectroOptics.UI.CrystalSelector | 晶体卡片选择器 |

---

## 五、实现步骤（P0-P1 已完成）

### 阶段 P0：基础数据传递

#### 步骤 P0.1：创建数据传递模块

**文件**：`Scripts/DataTransfer/CrystalSelectionData.cs`

- 创建静态类 `CrystalSelectionData`
- 实现 `SelectedProfile` 属性
- 实现 `HasSelection` 和 `Clear()` 方法

#### 步骤 P0.2：创建晶体选择功能

**文件**：
- `Scripts/Experiment/Interfaces/ICrystalSelectable.cs`
- `Scripts/UI/CrystalSelector/CrystalCardSelector.cs`

- 定义 `ICrystalSelectable` 接口
- 实现 `CrystalCardSelector` 组件
- Inspector 配置：`crystalProfile`, `displayName`, `targetSceneName`
- 点击事件：保存选择 → 切换场景

### 阶段 P1：晶体初始化

#### 步骤 P1.1：创建晶体配置接口

**文件**：`Scripts/Experiment/Interfaces/ICrystalConfigurable.cs`

- 定义所有配置和旋转控制方法

#### 步骤 P1.2：实现晶体控制器包装器

**文件**：`Scripts/Experiment/Controller/CrystalControllerWrapper.cs`

- 实现 `ICrystalConfigurable` 接口
- 封装 `CrystalPhysicalCore` 的控制
- 旋转范围限制：-15° ~ +15°
- 调用 `PhysicalCore.ApplyConfig()` 更新物理状态

#### 步骤 P1.3：实现晶体组件初始化器

**文件**：`Scripts/Experiment/Initializer/CrystalComponentInitializer.cs`

- 在场景启动时自动执行
- 查找晶体模型（名称为"晶体"）
- 添加必要的组件（PhysicalCore, ControllerWrapper, Collider）
- 加载 `CrystalSelectionData` 中保存的 Profile
- 注册到 `CrystalRuntime`

#### 步骤 P1.4：实现锥光干涉纹理渲染器

**文件**：`Scripts/Experiment/Renderer/ConoscopicTextureRenderer.cs`

- 创建 `RenderTexture` 用于弹窗显示
- 创建隐藏的预览摄像机和 Quad
- 从 `CrystalPhysicalCore` 获取计算结果
- 设置 Shader 参数并渲染
- 提供 `UpdateAndRender()` 方法供 P2 弹窗调用

---

## 六、数据流设计

### 6.1 晶体选择流程（P0）

```
[用户操作]
    │
    ↓ 点击晶体卡片 (Scene2-preview)
    │
CrystalCardSelector.OnCardClick()
    │
    ├──→ 验证 crystalProfile != null
    │
    ├──→ CrystalSelectionData.SelectedProfile = profile
    │
    └──→ SceneManager.LoadScene("Scene2.The Lab")
```

### 6.2 晶体初始化流程（P1）

```
[Scene2.The Lab 启动]
    │
    ↓
CrystalComponentInitializer.Start()
    │
    ├──→ FindCrystalModel()
    │       └──→ GameObject.Find("晶体")
    │
    ├──→ SetupCrystalComponents()
    │       ├──→ 添加 CrystalPhysicalCore
    │       ├──→ 添加 Collider
    │       ├──→ 添加 CrystalControllerWrapper
    │       └──→ controller.Initialize(physicalCore)
    │
    ├──→ SetupTextureRenderer()
    │       └──→ 添加 ConoscopicTextureRenderer
    │
    ├──→ RegisterToRuntime()
    │       └──→ 设置 CrystalRuntime 静态属性
    │
    └──→ LoadSelectedProfile()
            │
            ├──→ 检查 CrystalSelectionData.HasSelection
            │
            └──→ controller.SetProfile(selectedProfile)
                    │
                    └──→ UpdatePhysicsConfig()
                            │
                            └──→ physicalCore.ApplyConfig(config)
```

### 6.3 P2 弹窗渲染流程（待实现）

```
[用户双击光屏]
    │
    ↓
ScreenPopupManager.HandleDoubleClick()
    │
    ├──→ HasCrystalOnRail() → 检测晶体
    │
    ├──→ [有晶体]
    │       │
    │       ↓
    │   ShowConoscopicWindow()
    │       │
    │       ├──→ 获取 CrystalRuntime.TextureRenderer.RenderTexture
    │       │
    │       └──→ ConoscopicWindowView.Show()
    │               │
    │               └──→ RawImage.texture = renderTexture
    │
    └──→ [无晶体]
            │
            ↓
        DirectScreenController.OpenDisplayWindow()
```

### 6.4 P2 旋转控制流程（待实现）

```
[用户双击晶体]
    │
    ↓
CrystalRotationPanel.TogglePanel()
    │
    ↓
[用户调节滑块]
    │
    ↓
OnRotationXChanged/YChanged(value)
    │
    ↓
CrystalRuntime.Controller.SetRotation()
    │
    ├──→ 限制范围 [-15, 15]
    │
    ├──→ transform.localRotation = Quaternion.Euler(x, y, 0)
    │
    └──→ UpdatePhysicsConfig()
            │
            └──→ physicalCore.ApplyConfig(config)
                    │
                    ↓
            [弹窗 Update()]
                    │
                    ↓
            ConoscopicTextureRenderer.UpdateAndRender()
                    │
                    ↓
            干涉图实时更新
```

---

## 七、Unity 配置清单

### 7.1 Scene2-preview 配置

| 对象 | 组件 | 配置项 | 配置值 |
|------|------|--------|--------|
| Card1 (KDP) | CrystalCardSelector | crystalProfile | KDP.asset |
| | | displayName | "KDP 晶体" |
| | | targetSceneName | "Scene2.The Lab" |
| | Button | OnClick | CrystalCardSelector.OnCardClick() |
| Card2 (LiNbO3) | CrystalCardSelector | crystalProfile | LiNbO3_Profile.asset |
| | | displayName | "LiNbO3 晶体" |
| | | targetSceneName | "Scene2.The Lab" |
| | Button | OnClick | CrystalCardSelector.OnCardClick() |

### 7.2 Scene2.The Lab 配置

| 对象 | 组件 | 配置项 | 配置值 |
|------|------|--------|--------|
| CrystalInitializer (新建空对象) | CrystalComponentInitializer | renderTextureSize | 512 |
| | | conoscopicFOV | 10 |
| | | laserColor | Color.red |
| 晶体 (已存在) | (自动添加组件) | - | - |

### 7.3 层级设置（可选）

| 层级名称 | 用途 |
|---------|------|
| ConoscopicPreview | 隐藏的预览 Quad，仅被预览摄像机渲染 |

---

## 八、P2 阶段规划

### 8.1 待创建文件

| 文件路径 | 职责 |
|---------|------|
| `Scripts/UI/Common/WindowFactory.cs` | 窗口工厂，统一创建弹窗 UI |
| `Scripts/UI/ScreenPopup/ScreenPopupManager.cs` | 光屏双击弹窗管理 |
| `Scripts/UI/ScreenPopup/ConoscopicWindowView.cs` | 锥光干涉弹窗视图 |
| `Scripts/UI/ControlPanel/CrystalRotationPanel.cs` | 晶体旋转控制面板 |

### 8.2 P2 实现要点

1. **ScreenPopupManager**
   - 挂载在光屏对象上
   - 双击检测（复用 `doubleClickInterval` 模式）
   - 调用 `CrystalRuntime.IsCrystalOnRail()` 检测晶体
   - 有晶体 → 显示锥光干涉弹窗
   - 无晶体 → 调用 `DirectScreenController.OpenDisplayWindow()`

2. **ConoscopicWindowView**
   - 创建弹窗 UI（复用 WindowsCanvas）
   - 获取 `CrystalRuntime.TextureRenderer.RenderTexture`
   - 设置到 `RawImage.texture`
   - 实时更新：在 `Update()` 中调用 `TextureRenderer.UpdateAndRender()`

3. **CrystalRotationPanel**
   - 挂载在晶体对象上
   - 双击晶体显示/隐藏面板
   - Slider 控制 X/Y 旋转
   - 调用 `CrystalRuntime.Controller.SetRotation()`

### 8.3 依赖关系

```
P2 组件依赖 P0-P1 提供的接口：

ScreenPopupManager
    └──→ CrystalRuntime.TextureRenderer (获取 RenderTexture)
    └──→ CrystalRuntime.IsCrystalOnRail() (检测晶体)

ConoscopicWindowView
    └──→ CrystalRuntime.TextureRenderer.RenderTexture

CrystalRotationPanel
    └──→ CrystalRuntime.Controller.SetRotation()
    └──→ CrystalRuntime.Controller.GetRotation()
```

---

## 九、错误处理与日志

### 9.1 关键检查点

| 检查点 | 错误条件 | 处理方式 |
|--------|----------|----------|
| 卡片选择 | crystalProfile == null | Warning 日志，不执行场景切换 |
| 晶体初始化 | 晶体模型未找到 | Error 日志，组件禁用 |
| Profile加载 | CrystalSelectionData无选择 | Warning 日志，正常继续 |
| 控制器初始化 | PhysicalCore == null | Warning 日志，不更新物理配置 |

### 9.2 日志格式规范

```csharp
// 信息日志 - 使用方括号标识模块
Debug.Log("[CrystalCardSelector] 已选择晶体: KDP");

// 警告日志
Debug.LogWarning("[CrystalComponentInitializer] 无晶体选择数据");

// 错误日志
Debug.LogError("[CrystalComponentInitializer] 未找到晶体模型！");
```

---

## 十、命名空间总览

| 命名空间 | 包含类 |
|---------|--------|
| `ElectroOptics` | CrystalProfile, CrystalConfig, CrystalPhysicalCore (原有) |
| `ElectroOptics.DataTransfer` | CrystalSelectionData, CrystalRuntime |
| `ElectroOptics.Experiment.Interfaces` | ICrystalSelectable, ICrystalConfigurable |
| `ElectroOptics.Experiment.Controller` | CrystalControllerWrapper |
| `ElectroOptics.Experiment.Initializer` | CrystalComponentInitializer |
| `ElectroOptics.Experiment.Renderer` | ConoscopicTextureRenderer |
| `ElectroOptics.UI.CrystalSelector` | CrystalCardSelector |

---

## 十一、参考资源

### 11.1 现有资源

| 资源类型 | 路径 |
|---------|------|
| KDP Profile | Assets/KDP.asset |
| LiNbO3 Profile | Assets/LiNbO3_Profile.asset |
| 锥光干涉 Shader | Assets/Shaders/ConoscopicInterference.shader |
| 锥光干涉材质 | Assets/Shaders/Mat_Conoscopic.mat |

### 11.2 参考文档

| 文档 | 说明 |
|------|------|
| CLAUDE.md | 代码库开发指南 |
| implementation_plan.md | 原始实现计划 |
| P0_P1_Development_Documentation.md | 本文档 |

---

**文档结束**

*本文档记录了 P0-P1 阶段的开发成果，作为 P2 阶段开发的重要依据。*
