# 光屏显示重构 PRD 文档

## 文档信息

| 项目 | 内容 |
|------|------|
| 项目名称 | 光屏显示重构 - 统一面板架构 |
| 版本 | v1.1 |
| 创建日期 | 2026-03-15 |
| 更新日期 | 2026-03-16 |
| 技术方案 | A+C 综合方案（双图层 + 预渲染） |

---

## 1. 项目背景

### 1.1 现状分析

当前项目中存在两种光屏显示模式：

1. **红点追踪模式**（DirectScreenController）
   - 显示激光在光屏上的投射位置（红色光点）
   - 白色背景
   - 使用 Texture2D 动态绘制（SetPixels + Apply）
   - 双击弹出浮动窗口（旧逻辑，待移除）

2. **锥光干涉模式**（ConoscopicTextureRenderer）
   - 显示晶体锥光干涉图案
   - 黑色背景
   - 使用 RenderTexture + GPU Shader 渲染

### 1.2 场景现状（重要）

通过 GUID 核查，**Scene2.The Lab.unity** 中目前实际挂载的脚本为：

| 已挂载 | 未挂载（已写好但未部署） |
|--------|--------------------------|
| DirectScreenController | ScreenPopupManager |
| OpticalComponent (OpticalComponent_Keyboard.cs) | ConoscopicWindowView |
| LaserEmitter | CrystalComponentInitializer |
| PolarizerPhysics | ConoscopicTextureRenderer |
| OpticalRail | CrystalControllerWrapper |
| 其他基础组件 | CrystalRotationPanel |

结论：
- `ScreenPopupManager` / `ConoscopicWindowView` 从未进入场景，可以**直接删除** .cs 文件，无 Missing Script 风险
- 实验模块（`CrystalComponentInitializer` 等）已写好但未部署，本次重构需要将其加入场景作为前置依赖

### 1.3 问题与痛点

1. **用户体验不连贯**：两种模式使用独立的弹窗，切换时有明显的空白帧
2. **代码重复**：Canvas/EventSystem 创建逻辑在多处重复
3. **状态管理分散**：模式切换逻辑分散在多个组件中
4. **视觉跳跃**：直接切换背景色（白/黑）导致视觉闪烁
5. **旧实验模块未部署**：ConoscopicTextureRenderer 等组件写好但未挂载到场景

### 1.4 重构目标

- 统一显示面板（左下角固定位置，非弹窗）
- 实现平滑的淡入淡出过渡效果
- 预渲染机制避免空白帧
- 代码结构清晰、可维护
- 将实验模块正式部署到场景

---

## 2. 需求列表

### 2.1 功能需求

| ID | 需求描述 | 优先级 |
|----|----------|--------|
| F1 | 左下角固定面板显示，替代原有弹窗 | P0 |
| F2 | 红点追踪模式显示（白色背景） | P0 |
| F3 | 锥光干涉模式显示（黑色背景） | P0 |
| F4 | 根据晶体吸附状态自动切换模式（crystalOpticalComponent.isOnRail） | P0 |
| F5 | 切换时淡入淡出过渡动画（约 0.3s） | P0 |
| F6 | 切换前预渲染目标纹理，避免空白帧 | P0 |
| F7 | 面板可关闭/展开 | P1 |
| F8 | 手动切换模式按钮 | P2 |
| F9 | 面板尺寸可调整 | P2 |

> **注**：面板为固定位置，不支持拖拽。

### 2.2 非功能需求

| ID | 需求描述 |
|----|----------|
| NF1 | 过渡动画帧率 >= 30fps |
| NF2 | 切换响应时间 < 100ms |
| NF3 | 内存占用增加 < 50MB |
| NF4 | 兼容现有 Scene2 场景 |
| NF5 | 不影响现有光学系统链式调用 |

---

## 3. 技术方案

### 3.1 架构概述

采用 **双图层叠加 + CanvasGroup 淡入淡出** 方案：

```
UnifiedScreenPanel (Panel Container)
├── BackgroundImage (面板背景)
├── ConoscopicLayer (底层 - 锥光干涉)
│   ├── CanvasGroup (alpha 控制)
│   └── RawImage (RenderTexture)
├── DirectLayer (顶层 - 红点追踪)
│   ├── CanvasGroup (alpha 控制)
│   └── RawImage (Texture2D)
└── TitleBar (标题栏，无拖拽)
```

### 3.2 核心组件

#### 3.2.1 UnifiedScreenPanel（统一面板控制器）

- 职责：管理面板生命周期、模式切换、过渡动画、每帧驱动纹理更新
- 位置：左下角固定（anchoredPosition: (320, 200)，可在 Inspector 手动调整）
- 锚点/轴心：anchor = (0, 0)，pivot = (0, 0)（面板左下角对齐屏幕左下角）
- 尺寸：600x600（可配置）
- 模式检测：通过引用 `DirectScreenController`，读取 `crystalOpticalComponent.isOnRail`

#### 3.2.2 双图层系统

| 图层 | 纹理类型 | 背景色 | 显示条件 |
|------|----------|--------|----------|
| ConoscopicLayer | RenderTexture | 黑色 | 晶体吸附导轨（isOnRail == true） |
| DirectLayer | Texture2D | 白色 | 晶体未吸附（isOnRail == false） |

#### 3.2.3 纹理更新驱动

`UnifiedScreenPanel.Update()` 负责：
- 处于 **Conoscopic 模式**时：每帧调用 `CrystalRuntime.TextureRenderer.UpdateAndRender()`（接管原 `ConoscopicWindowView.Update()` 的职责）
- 处于 **Direct 模式**时：`DirectScreenController` 已在自身 `Update()` 中就地修改 `sharedTexture`，面板只需初始化时绑定一次 texture 引用，无需每帧重新赋值

#### 3.2.4 预渲染机制

```
切换流程：
1. 检测 isOnRail 状态变化
2. 预渲染目标纹理（调用 UpdateAndRender）
3. 等待 GPU 完成渲染（WaitForEndOfFrame）
4. 设置目标图层 alpha = 0，激活目标图层
5. 淡出当前图层（alpha 1 -> 0）
6. 淡入目标图层（alpha 0 -> 1）
7. 隐藏原图层
```

### 3.3 背景色过渡

通过两个图层的 CanvasGroup alpha 叠加实现：

| 状态 | ConoscopicLayer.alpha | DirectLayer.alpha | 视觉效果 |
|------|----------------------|-------------------|----------|
| 红点追踪 | 0 | 1 | 白色背景 |
| 过渡中 | 0.x | 0.x | 混合背景 |
| 锥光干涉 | 1 | 0 | 黑色背景 |

### 3.4 模式检测逻辑

```csharp
// 在 UnifiedScreenPanel.Update() 中
bool crystalOnRail = _directScreenController != null
    && _directScreenController.crystalOpticalComponent != null
    && _directScreenController.crystalOpticalComponent.isOnRail;

ScreenMode targetMode = crystalOnRail ? ScreenMode.Conoscopic : ScreenMode.Direct;
if (targetMode != _currentMode && !_isTransitioning)
{
    StartCoroutine(SwitchModeWithTransition(targetMode));
}
```

---

## 4. 需求拆分（优先级）

### 4.1 P0 核心功能

- 基础面板框架（场景 GameObject 挂载方式）
- 双图层架构
- 红点追踪集成
- 锥光干涉集成
- 自动模式切换（基于 isOnRail）
- 淡入淡出过渡
- 预渲染机制

### 4.2 P1 增强功能

- 关闭/展开功能

### 4.3 P2 可选功能

- 手动模式切换按钮
- 面板尺寸调整

---

## 5. 详细开发计划

### 前置步骤：场景组件部署

**目标**：将已写好但未挂载的实验模块正式加入 Scene2

| 操作 | 说明 |
|------|------|
| 在场景中创建 GameObject "ExperimentManager" | 挂载 `CrystalComponentInitializer` |
| 确认晶体 GameObject 上有 `CrystalPhysicalCore` | CrystalComponentInitializer 依赖此组件 |
| 验证 `CrystalRuntime.IsInitialized` 在运行时为 true | 通过 Debug.Log 确认 |

> **注**：此步骤在代码开发完成后，在 Unity Editor 中手动操作。

---

### Phase 1: 基础架构（预计 2-3 小时）

**目标**：搭建统一面板框架和双图层结构

#### 任务列表

1. 创建 `ScreenMode.cs` 枚举定义
2. 创建 `IScreenDataProvider.cs` 接口
3. 创建 `CanvasGroupTweener.cs` 工具类
4. 创建 `UnifiedScreenPanel.cs` 基础框架（UI 层级、双图层）

#### 涉及文件

| 操作 | 文件路径 |
|------|----------|
| 新建 | `Assets/Scripts/UI/ScreenDisplay/ScreenMode.cs` |
| 新建 | `Assets/Scripts/UI/ScreenDisplay/IScreenDataProvider.cs` |
| 新建 | `Assets/Scripts/UI/ScreenDisplay/CanvasGroupTweener.cs` |
| 新建 | `Assets/Scripts/UI/ScreenDisplay/UnifiedScreenPanel.cs` |

---

### Phase 2: 红点追踪集成（预计 1-2 小时）

**目标**：将 DirectScreenController 的 sharedTexture 显示到 DirectLayer

#### 任务列表

1. 修改 `DirectScreenController`，将 `sharedTexture` 改为公开属性 `SharedTexture`
2. 创建 `DirectScreenDataProvider` 实现数据提供者接口
3. 在 `UnifiedScreenPanel` 中绑定 DirectLayer 纹理
4. 测试红点位置映射正确性

#### 涉及文件

| 操作 | 文件路径 |
|------|----------|
| 修改 | `Assets/Scripts/LightScreen/DirectScreenController.cs` |
| 新建 | `Assets/Scripts/UI/ScreenDisplay/DirectScreenDataProvider.cs` |

#### DirectScreenController 修改内容

```csharp
// 将 private Texture2D sharedTexture 改为：
public Texture2D SharedTexture => sharedTexture;
private Texture2D sharedTexture;
```

---

### Phase 3: 锥光干涉集成（预计 1-2 小时）

**目标**：将 ConoscopicTextureRenderer 的 RenderTexture 显示到 ConoscopicLayer

#### 任务列表

1. 创建 `ConoscopicScreenDataProvider` 实现数据提供者接口
2. 在 `UnifiedScreenPanel` 中绑定 ConoscopicLayer 纹理
3. 实现预渲染调用逻辑
4. 测试干涉图显示正确性

#### 涉及文件

| 操作 | 文件路径 |
|------|----------|
| 新建 | `Assets/Scripts/UI/ScreenDisplay/ConoscopicScreenDataProvider.cs` |

---

### Phase 4: 切换逻辑与过渡动画（预计 2-3 小时）

**目标**：实现自动模式检测和平滑过渡

#### 任务列表

1. 实现 `isOnRail` 状态检测（每帧轮询，通过 `DirectScreenController` 引用）
2. 实现模式切换状态机（防止过渡期间重复触发）
3. 实现预渲染 + `WaitForEndOfFrame` 机制
4. 实现淡入淡出动画（CanvasGroupTweener.CrossFade）
5. **在 `Update()` 中驱动 `UpdateAndRender()`**（Conoscopic 模式时每帧调用，接管原 ConoscopicWindowView 的职责）
6. 测试切换流畅性

#### 核心切换逻辑

```csharp
private IEnumerator SwitchModeWithTransition(ScreenMode newMode)
{
    if (_isTransitioning) yield break;
    _isTransitioning = true;

    CanvasGroup targetLayer = (newMode == ScreenMode.Conoscopic) ? _conoscopicLayerGroup : _directLayerGroup;
    CanvasGroup currentLayer = (newMode == ScreenMode.Conoscopic) ? _directLayerGroup : _conoscopicLayerGroup;

    // 预渲染目标纹理
    if (newMode == ScreenMode.Conoscopic && CrystalRuntime.TextureRenderer != null)
    {
        CrystalRuntime.TextureRenderer.UpdateAndRender();
    }

    // 等待 GPU 完成
    yield return new WaitForEndOfFrame();

    // 显示目标图层（alpha=0）
    targetLayer.gameObject.SetActive(true);
    targetLayer.alpha = 0f;

    // 并行淡入淡出
    yield return StartCoroutine(CanvasGroupTweener.CrossFade(currentLayer, targetLayer, 0.3f));

    // 隐藏原图层
    currentLayer.gameObject.SetActive(false);

    _currentMode = newMode;
    _isTransitioning = false;
}
```

#### Update 驱动示意

```csharp
private void Update()
{
    // 检测模式切换
    bool crystalOnRail = _directScreenController != null
        && _directScreenController.crystalOpticalComponent != null
        && _directScreenController.crystalOpticalComponent.isOnRail;
    ScreenMode targetMode = crystalOnRail ? ScreenMode.Conoscopic : ScreenMode.Direct;
    if (targetMode != _currentMode && !_isTransitioning)
        StartCoroutine(SwitchModeWithTransition(targetMode));

    // 驱动锥光干涉每帧渲染
    if (_currentMode == ScreenMode.Conoscopic && !_isTransitioning
        && CrystalRuntime.IsInitialized && CrystalRuntime.TextureRenderer != null)
    {
        CrystalRuntime.TextureRenderer.UpdateAndRender();
    }
}
```

---

### Phase 5: 清理旧代码（预计 1 小时）

**目标**：删除旧弹窗系统，迁移 SimpleDrag，清理 DirectScreenController

#### 任务列表

1. **迁移 `SimpleDrag`**：将 `SimpleDrag` 类从 `DirectScreenController.cs` 底部独立为 `SimpleDrag.cs`（`CrystalRotationPanel` 仍依赖此类）
2. **清理 `DirectScreenController`**：移除弹窗相关方法和字段（见下方清单）
3. **删除 `ScreenPopupManager.cs`**：未挂载场景，可直接删除
4. **删除 `ConoscopicWindowView.cs`**：未挂载场景，可直接删除
5. 添加关闭/展开功能（P1）

#### DirectScreenController 清理内容

```csharp
// 移除以下字段：
// private GameObject displayWindow;
// private RawImage uiDisplayImage;
// private float lastClickTime;

// 移除以下方法：
// OnMouseDown()          —— 双击检测逻辑
// OpenDisplayWindow()
// CreateDisplayWindow()
// CreateCloseBtn()

// 保留以下核心逻辑：
// ReceiveLight()         —— 光学链接收
// DrawPattern()          —— 纹理绘制
// Update()               —— 纹理更新驱动
// SharedTexture          —— 公开纹理属性
// OnDestroy()            —— 纹理内存释放
```

#### 涉及文件

| 操作 | 文件路径 |
|------|----------|
| 新建（迁移） | `Assets/Scripts/LightScreen/SimpleDrag.cs` |
| 修改 | `Assets/Scripts/LightScreen/DirectScreenController.cs` |
| **删除** | `Assets/Scripts/UI/ScreenPopup/ScreenPopupManager.cs` |
| **删除** | `Assets/Scripts/UI/ScreenPopup/ConoscopicWindowView.cs` |

---

## 6. 数据流设计

### 6.1 数据流向图

```
┌─────────────────────────────────────────────────────────────────┐
│                         Scene2 场景                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────┐    LightData    ┌───────────────────────────┐ │
│  │ LaserEmitter │ ──────────────► │  DirectScreenController   │ │
│  └──────────────┘                 │   - ReceiveLight()        │ │
│                                   │   - SharedTexture (pub)   │ │
│                                   └────────────┬──────────────┘ │
│                                                │ Texture2D      │
│  ┌─────────────────────┐                       │                │
│  │ CrystalComponentInit│──► CrystalRuntime      │                │
│  │ (ExperimentManager) │    - TextureRenderer   │                │
│  └─────────────────────┘    - IsInitialized     │                │
│                                     │           │                │
│                              RenderTex│          │                │
│                                     ▼           ▼                │
│                          ┌───────────────────────────────────┐  │
│                          │       UnifiedScreenPanel          │  │
│                          │  - ConoscopicLayer (RenderTex)    │  │
│                          │  - DirectLayer (Texture2D)        │  │
│                          │  - SwitchModeWithTransition()     │  │
│                          │  - Update() 驱动 UpdateAndRender  │  │
│                          └───────────────────────────────────┘  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 6.2 模式切换数据流

```
每帧 Update() 轮询
        │
        ▼
┌───────────────────┐
│ 读取 isOnRail      │
│ DirectScreenCtrl  │
│ .crystalOptical   │
│ Component.isOnRail│
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ 状态变化？         │
│ (防抖：过渡中忽略)  │
└────────┬──────────┘
         │ 有变化
         ▼
┌───────────────────┐
│ 预渲染目标纹理     │
│ WaitForEndOfFrame │
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ 激活目标图层       │
│ alpha = 0         │
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ CrossFade 协程     │
│ 0.3s 淡入淡出      │
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ 隐藏原图层         │
│ 更新 _currentMode │
└───────────────────┘
```

---

## 7. 接口定义

### 7.1 ScreenMode 枚举

```csharp
namespace ElectroOptics.UI.ScreenDisplay
{
    public enum ScreenMode
    {
        /// <summary>红点追踪模式（晶体未上导轨）</summary>
        Direct = 0,
        /// <summary>锥光干涉模式（晶体已上导轨）</summary>
        Conoscopic = 1
    }
}
```

### 7.2 IScreenDataProvider 接口

```csharp
namespace ElectroOptics.UI.ScreenDisplay
{
    public interface IScreenDataProvider
    {
        Texture GetTexture();
        void PreRender();
        bool IsAvailable { get; }
        string ModeName { get; }
    }
}
```

### 7.3 UnifiedScreenPanel 公共接口

```csharp
namespace ElectroOptics.UI.ScreenDisplay
{
    public class UnifiedScreenPanel : MonoBehaviour
    {
        [Header("引用配置")]
        [SerializeField] private DirectScreenController directScreenController;

        public ScreenMode CurrentMode { get; private set; }
        public bool IsTransitioning { get; private set; }

        public void SwitchToMode(ScreenMode mode);           // 带过渡动画
        public void SwitchToModeImmediate(ScreenMode mode);  // 立即切换
        public void Show();
        public void Hide();
        public void SetPosition(Vector2 anchoredPosition);
        public void SetSize(Vector2 size);
    }
}
```

### 7.4 CanvasGroupTweener 接口

```csharp
namespace ElectroOptics.UI.ScreenDisplay
{
    public static class CanvasGroupTweener
    {
        public static IEnumerator FadeIn(CanvasGroup group, float duration);
        public static IEnumerator FadeOut(CanvasGroup group, float duration);
        public static IEnumerator CrossFade(CanvasGroup from, CanvasGroup to, float duration);
        public static void SetAlpha(CanvasGroup group, float alpha);
    }
}
```

---

## 8. 错误处理

### 8.1 错误场景与处理

| 错误场景 | 处理方式 | 用户反馈 |
|----------|----------|----------|
| CrystalRuntime 未初始化 | 保持 Direct 模式 | 无（静默处理） |
| ConoscopicTextureRenderer 为空 | 保持 Direct 模式 | Console 警告 |
| DirectScreenController 引用为空 | 默认 Direct 模式，禁用自动切换 | Console 错误 |
| DirectScreenController.SharedTexture 为空 | 显示占位图 | Console 警告 |
| 过渡动画被中断（快速反复切换） | _isTransitioning 锁防止重入，等待当前过渡完成 | 无 |
| 预渲染超时 | 跳过预渲染，直接切换 | Console 警告 |

### 8.2 日志规范

```csharp
private const string LOG_PREFIX = "[UnifiedScreenPanel]";

Debug.Log($"{LOG_PREFIX} 模式切换: {CurrentMode} -> {newMode}");
Debug.LogWarning($"{LOG_PREFIX} CrystalRuntime 未初始化，保持 Direct 模式");
Debug.LogError($"{LOG_PREFIX} DirectScreenController 引用为空，自动切换禁用");
```

---

## 9. 测试计划

### 9.1 单元测试

| 测试用例 | 预期结果 |
|----------|----------|
| CanvasGroupTweener.FadeIn | alpha 从 0 渐变到 1 |
| CanvasGroupTweener.FadeOut | alpha 从 1 渐变到 0 |
| CanvasGroupTweener.CrossFade | 两个图层同时变化，总时长正确 |
| ScreenMode 枚举值 | Direct=0, Conoscopic=1 |

### 9.2 集成测试

| 测试场景 | 操作步骤 | 预期结果 |
|----------|----------|----------|
| 场景启动 | 加载 Scene2 | 面板自动显示在左下角，Direct 模式（白底） |
| 激光照射 | 开启激光照射光屏 | 红点正确显示位置 |
| 放置晶体 | 将晶体吸附到导轨上 | 自动切换锥光干涉（黑底），过渡平滑 |
| 移除晶体 | 将晶体从导轨移开 | 自动切换回红点追踪（白底），过渡平滑 |
| 快速反复切换 | 快速拖拽晶体进出导轨 | 不崩溃，过渡有序完成 |
| 电压调节 | 调节晶体电压 | 干涉图案实时变化 |
| CrystalRuntime 未初始化 | 不挂载 CrystalComponentInitializer | 保持 Direct 模式，Console 警告 |

### 9.3 性能测试

| 测试项目 | 通过条件 |
|----------|----------|
| 过渡动画帧率 | >= 30fps |
| 切换响应时间 | < 200ms |
| 内存增量 | < 100MB |
| GPU 渲染时间 | < 16ms/frame |

---

## 10. 文件清单汇总

### 10.1 新建文件

| 文件路径 | 说明 |
|----------|------|
| `Assets/Scripts/UI/ScreenDisplay/UnifiedScreenPanel.cs` | 统一面板主控制器（MonoBehaviour，挂载到场景 GameObject） |
| `Assets/Scripts/UI/ScreenDisplay/ScreenMode.cs` | 显示模式枚举 |
| `Assets/Scripts/UI/ScreenDisplay/CanvasGroupTweener.cs` | CanvasGroup 动画工具类（静态类） |
| `Assets/Scripts/UI/ScreenDisplay/IScreenDataProvider.cs` | 数据提供者接口 |
| `Assets/Scripts/UI/ScreenDisplay/DirectScreenDataProvider.cs` | 红点追踪数据提供者 |
| `Assets/Scripts/UI/ScreenDisplay/ConoscopicScreenDataProvider.cs` | 锥光干涉数据提供者 |

### 10.2 迁移文件

| 操作 | 原位置 | 新位置 | 原因 |
|------|--------|--------|------|
| 迁移 SimpleDrag 类 | `DirectScreenController.cs` 底部 | `Assets/Scripts/LightScreen/SimpleDrag.cs` | CrystalRotationPanel 仍依赖此类；原文件要移除弹窗代码块 |

### 10.3 修改文件

| 文件路径 | 修改内容 |
|----------|----------|
| `Assets/Scripts/LightScreen/DirectScreenController.cs` | 暴露 SharedTexture 属性；移除 OnMouseDown/OpenDisplayWindow/CreateDisplayWindow/CreateCloseBtn 及相关字段；移除 SimpleDrag 类定义（已迁移） |

### 10.4 删除文件

| 文件路径 | 原因 |
|----------|------|
| `Assets/Scripts/UI/ScreenPopup/ScreenPopupManager.cs` | 从未挂载到任何场景，功能由 UnifiedScreenPanel 替代 |
| `Assets/Scripts/UI/ScreenPopup/ConoscopicWindowView.cs` | 从未挂载到任何场景，功能由 UnifiedScreenPanel 替代 |

### 10.5 无需修改（接口已兼容）

| 文件路径 | 说明 |
|----------|------|
| `Assets/Scripts/DataTransfer/CrystalRuntime.cs` | 接口兼容，UnifiedScreenPanel 直接读取 TextureRenderer |
| `Assets/Scripts/Experiment/Renderer/ConoscopicTextureRenderer.cs` | UpdateAndRender() / RenderTexture 接口不变 |
| `Assets/Scripts/Experiment/Initializer/CrystalComponentInitializer.cs` | 需部署到场景，代码本身无需修改 |
| `Assets/Scripts/UI/ControlPanel/CrystalRotationPanel.cs` | 依赖 SimpleDrag（迁移后自动解析，无需修改） |

---

## 11. 场景配置指引（Unity Editor 操作）

代码开发完成后，在 Unity Editor 中执行：

1. **部署实验模块**：创建空 GameObject "ExperimentManager"，挂载 `CrystalComponentInitializer`
2. **部署统一面板**：创建空 GameObject "ScreenDisplayPanel"，挂载 `UnifiedScreenPanel`，在 Inspector 中将光屏上的 `DirectScreenController` 拖入引用槽
3. **移除旧组件**：确认光屏 GameObject 上没有 `ScreenPopupManager`（已确认未挂载，无需操作）
4. **验证**：运行场景，确认 Console 无报错，`CrystalRuntime.IsInitialized` 输出 true

---

## 12. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| WaitForEndOfFrame 在某些情况下可能不生效 | 空白帧 | 添加超时机制，超时后直接切换 |
| Canvas 重复创建 | UI 异常 | UnifiedScreenPanel 直接挂载到场景 GameObject，不动态创建 Canvas |
| isOnRail 检测误判（如晶体动画过渡中抖动） | 反复切换 | _isTransitioning 锁 + 过渡期间忽略状态变化 |
| CrystalComponentInitializer 未部署 | Conoscopic 模式无法显示 | 降级到 Direct 模式，Console 警告提示 |
| 内存泄漏（纹理未释放） | 内存增长 | OnDestroy 中正确释放 RenderTexture 和 Texture2D |
| SimpleDrag 迁移后 CrystalRotationPanel 找不到类 | 编译错误 | SimpleDrag 迁移到同目录或全局命名空间，保证可见性 |

---

## 13. 变更历史

| 版本 | 日期 | 变更内容 |
|------|------|----------|
| v1.0 | 2026-03-15 | 初始版本 |
| v1.1 | 2026-03-16 | 确认设计决策：固定面板（移除拖拽需求）；模式检测改为 isOnRail 直接读取；补充场景现状分析（GUID 核查）；ScreenPopupManager/ConoscopicWindowView 改为直接删除；新增 SimpleDrag 迁移条目；新增 Update() 驱动 UpdateAndRender() 机制；新增场景配置指引；更新文件清单 |
