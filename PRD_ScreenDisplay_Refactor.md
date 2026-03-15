# 光屏显示重构 PRD 文档

## 文档信息

| 项目 | 内容 |
|------|------|
| 项目名称 | 光屏显示重构 - 统一面板架构 |
| 版本 | v1.0 |
| 创建日期 | 2026-03-15 |
| 技术方案 | A+C 综合方案（双图层 + 预渲染） |

---

## 1. 项目背景

### 1.1 现状分析

当前项目中存在两种光屏显示模式：

1. **红点追踪模式**（DirectScreenController）
   - 显示激光在光屏上的投射位置（红色光点）
   - 白色背景
   - 使用 Texture2D 动态绘制
   - 双击弹出浮动窗口

2. **锥光干涉模式**（ConoscopicWindowView）
   - 显示晶体锥光干涉图案
   - 黑色背景
   - 使用 RenderTexture + GPU Shader 渲染
   - 晶体在光屏附近时双击弹出

### 1.2 问题与痛点

1. **用户体验不连贯**：两种模式使用独立的弹窗，切换时有明显的空白帧
2. **代码重复**：Canvas/EventSystem 创建逻辑在多处重复
3. **状态管理分散**：模式切换逻辑分散在多个组件中
4. **视觉跳跃**：直接切换背景色（白/黑）导致视觉闪烁

### 1.3 重构目标

- 统一显示面板（左下角固定位置，非弹窗）
- 实现平滑的淡入淡出过渡效果
- 预渲染机制避免空白帧
- 代码结构清晰、可维护

---

## 2. 需求列表

### 2.1 功能需求

| ID | 需求描述 | 优先级 |
|----|----------|--------|
| F1 | 左下角固定面板显示，替代原有弹窗 | P0 |
| F2 | 红点追踪模式显示（白色背景） | P0 |
| F3 | 锥光干涉模式显示（黑色背景） | P0 |
| F4 | 根据晶体位置自动切换模式 | P0 |
| F5 | 切换时淡入淡出过渡动画（约 0.3s） | P0 |
| F6 | 切换前预渲染目标纹理，避免空白帧 | P0 |
| F7 | 面板可拖拽移动 | P1 |
| F8 | 面板可关闭/展开 | P1 |
| F9 | 手动切换模式按钮 | P2 |
| F10 | 面板尺寸可调整 | P2 |

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
├── TitleBar (标题栏 + 拖拽 + 关闭)
└── ModeIndicator (模式指示器)
```

### 3.2 核心组件

#### 3.2.1 UnifiedScreenPanel（统一面板控制器）

- 职责：管理面板生命周期、模式切换、过渡动画
- 位置：左下角固定（anchoredPosition: (320, 200)）
- 尺寸：600x600（可配置）

#### 3.2.2 双图层系统

| 图层 | 纹理类型 | 背景色 | 显示条件 |
|------|----------|--------|----------|
| ConoscopicLayer | RenderTexture | 黑色 | 晶体在光屏附近 |
| DirectLayer | Texture2D | 白色 | 无晶体或红点追踪模式 |

#### 3.2.3 预渲染机制

```
切换流程：
1. 检测模式变化
2. 准备目标纹理（调用 UpdateAndRender）
3. 等待 GPU 完成渲染（WaitForEndOfFrame）
4. 设置目标图层 alpha = 0，显示目标图层
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

---

## 4. 需求拆分（优先级）

### 4.1 P0 核心功能

- 基础面板框架
- 双图层架构
- 红点追踪集成
- 锥光干涉集成
- 自动模式切换
- 淡入淡出过渡
- 预渲染机制

### 4.2 P1 增强功能

- 面板拖拽
- 关闭/展开功能

### 4.3 P2 可选功能

- 手动模式切换按钮
- 面板尺寸调整

---

## 5. 详细开发计划

### Phase 1: 基础架构（预计 2-3 小时）

**目标**：搭建统一面板框架和双图层结构

#### 任务列表

1. 创建 UnifiedScreenPanel.cs 基础框架
2. 创建双图层 UI 结构
3. 实现 CanvasGroup 淡入淡出工具类
4. 创建面板 Prefab

#### 涉及文件

| 操作 | 文件路径 |
|------|----------|
| 新建 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/UnifiedScreenPanel.cs` |
| 新建 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/ScreenMode.cs` |
| 新建 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/CanvasGroupTweener.cs` |
| 新建 | `ElectroOptic-Lab/Assets/Prefabs/UI/UnifiedScreenPanel.prefab` |
| 新建 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/IScreenDataProvider.cs` |

---

### Phase 2: 红点追踪集成（预计 1-2 小时）

**目标**：将 DirectScreenController 的纹理显示到 DirectLayer

#### 任务列表

1. 修改 DirectScreenController，添加 SharedTexture 公共属性
2. 创建 DirectScreenDataProvider 实现数据提供者接口
3. 在 UnifiedScreenPanel 中集成红点追踪显示
4. 测试红点位置映射正确性

#### 涉及文件

| 操作 | 文件路径 |
|------|----------|
| 修改 | `ElectroOptic-Lab/Assets/Scripts/LightScreen/DirectScreenController.cs` |
| 新建 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/DirectScreenDataProvider.cs` |
| 修改 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/UnifiedScreenPanel.cs` |

#### DirectScreenController 修改点

```csharp
// 新增公共属性
public Texture2D SharedTexture => sharedTexture;

// 新增方法（可选，用于外部触发重绘）
public void ForceRedraw()
{
    DrawPattern(currentIntensity, targetCenterX, targetCenterY);
}
```

---

### Phase 3: 锥光干涉集成（预计 1-2 小时）

**目标**：将 ConoscopicTextureRenderer 的纹理显示到 ConoscopicLayer

#### 任务列表

1. 创建 ConoscopicScreenDataProvider 实现数据提供者接口
2. 在 UnifiedScreenPanel 中集成锥光干涉显示
3. 实现预渲染调用逻辑
4. 测试干涉图显示正确性

#### 涉及文件

| 操作 | 文件路径 |
|------|----------|
| 新建 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/ConoscopicScreenDataProvider.cs` |
| 修改 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/UnifiedScreenPanel.cs` |
| 参考 | `ElectroOptic-Lab/Assets/Scripts/Experiment/Renderer/ConoscopicTextureRenderer.cs` |

---

### Phase 4: 切换逻辑与过渡动画（预计 2-3 小时）

**目标**：实现自动模式检测和平滑过渡

#### 任务列表

1. 实现晶体位置检测（使用 CrystalRuntime）
2. 实现模式切换状态机
3. 实现预渲染 + WaitForEndOfFrame 机制
4. 实现淡入淡出动画
5. 添加背景色同步过渡
6. 测试切换流畅性

#### 涉及文件

| 操作 | 文件路径 |
|------|----------|
| 修改 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/UnifiedScreenPanel.cs` |
| 修改 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/CanvasGroupTweener.cs` |
| 参考 | `ElectroOptic-Lab/Assets/Scripts/DataTransfer/CrystalRuntime.cs` |

#### 核心切换逻辑

```csharp
private IEnumerator SwitchModeWithTransition(ScreenMode newMode)
{
    if (_isTransitioning) yield break;
    _isTransitioning = true;

    // 1. 确定目标图层
    CanvasGroup targetLayer = (newMode == ScreenMode.Conoscopic) ? _conoscopicLayerGroup : _directLayerGroup;
    CanvasGroup currentLayer = (newMode == ScreenMode.Conoscopic) ? _directLayerGroup : _conoscopicLayerGroup;

    // 2. 预渲染目标纹理
    if (newMode == ScreenMode.Conoscopic && CrystalRuntime.TextureRenderer != null)
    {
        CrystalRuntime.TextureRenderer.UpdateAndRender();
    }

    // 3. 等待 GPU 完成
    yield return new WaitForEndOfFrame();

    // 4. 显示目标图层（alpha=0）
    targetLayer.gameObject.SetActive(true);
    targetLayer.alpha = 0f;

    // 5. 并行淡入淡出
    float duration = 0.3f;
    yield return StartCoroutine(CanvasGroupTweener.CrossFade(currentLayer, targetLayer, duration));

    // 6. 隐藏原图层
    currentLayer.gameObject.SetActive(false);

    _currentMode = newMode;
    _isTransitioning = false;
}
```

---

### Phase 5: 清理与优化（预计 1-2 小时）

**目标**：移除旧代码，优化性能

#### 任务列表

1. 移除或标记过时的 ScreenPopupManager
2. 移除或标记过时的 ConoscopicWindowView
3. 移除 DirectScreenController 中的弹窗逻辑
4. 添加面板拖拽功能（P1）
5. 添加关闭/展开功能（P1）
6. 性能测试与优化
7. 文档更新

#### 涉及文件

| 操作 | 文件路径 |
|------|----------|
| 标记过时 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenPopup/ScreenPopupManager.cs` |
| 标记过时 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenPopup/ConoscopicWindowView.cs` |
| 修改 | `ElectroOptic-Lab/Assets/Scripts/LightScreen/DirectScreenController.cs` |
| 新建 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/PanelDragHandler.cs` |
| 修改 | `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/UnifiedScreenPanel.cs` |

#### DirectScreenController 清理点

```csharp
// 移除/注释以下方法：
// - OnMouseDown() 中的双击检测逻辑
// - OpenDisplayWindow()
// - CreateDisplayWindow()
// - CreateCloseBtn()
// - displayWindow 相关字段

// 保留以下核心逻辑：
// - ReceiveLight()
// - DrawPattern()
// - sharedTexture 管理
```

---

## 6. 数据流设计

### 6.1 数据流向图

```
┌─────────────────────────────────────────────────────────────────┐
│                         Scene2 场景                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────┐    LightData    ┌───────────────────────┐    │
│  │ LaserEmitter │ ──────────────► │ DirectScreenController │    │
│  └──────────────┘                 │  (IOpticalReceiver)    │    │
│                                   │  - ReceiveLight()      │    │
│                                   │  - SharedTexture       │    │
│                                   └───────────┬───────────┘    │
│                                               │                 │
│                                               │ Texture2D       │
│                                               ▼                 │
│  ┌──────────────────┐             ┌───────────────────────┐    │
│  │ CrystalRuntime   │             │ UnifiedScreenPanel    │    │
│  │  - TextureRenderer │─────────► │  - DirectLayer        │    │
│  │  - IsInitialized  │ RenderTex  │  - ConoscopicLayer    │    │
│  └──────────────────┘             │  - SwitchMode()       │    │
│         ▲                         └───────────────────────┘    │
│         │                                                       │
│         │ 检测晶体位置                                           │
│         │                                                       │
│  ┌──────┴───────────────────────────────────────────────────┐  │
│  │ CrystalPhysicalCore (晶体物理核心)                         │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 6.2 模式切换数据流

```
用户操作/晶体位置变化
        │
        ▼
┌───────────────────┐
│ 检测触发条件       │
│ - 晶体进入/离开    │
│ - 手动切换(可选)   │
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ 确定目标模式       │
│ ScreenMode 枚举    │
└────────┬──────────┘
         │
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
│ 并行淡入淡出       │
│ CrossFade 协程     │
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ 隐藏原图层         │
│ 更新当前模式       │
└───────────────────┘
```

---

## 7. 接口定义

### 7.1 ScreenMode 枚举

```csharp
namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 光屏显示模式
    /// </summary>
    public enum ScreenMode
    {
        /// <summary>
        /// 红点追踪模式（无晶体）
        /// </summary>
        Direct = 0,

        /// <summary>
        /// 锥光干涉模式（有晶体）
        /// </summary>
        Conoscopic = 1
    }
}
```

### 7.2 IScreenDataProvider 接口

```csharp
namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 光屏数据提供者接口
    /// 用于解耦数据源和显示面板
    /// </summary>
    public interface IScreenDataProvider
    {
        /// <summary>
        /// 获取显示纹理
        /// </summary>
        Texture GetTexture();

        /// <summary>
        /// 预渲染一帧（用于切换前准备）
        /// </summary>
        void PreRender();

        /// <summary>
        /// 数据提供者是否可用
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 模式名称（用于调试）
        /// </summary>
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
        /// <summary>
        /// 当前显示模式
        /// </summary>
        public ScreenMode CurrentMode { get; private set; }

        /// <summary>
        /// 是否正在过渡中
        /// </summary>
        public bool IsTransitioning { get; private set; }

        /// <summary>
        /// 切换到指定模式（带过渡动画）
        /// </summary>
        public void SwitchToMode(ScreenMode mode);

        /// <summary>
        /// 切换到指定模式（立即切换，无动画）
        /// </summary>
        public void SwitchToModeImmediate(ScreenMode mode);

        /// <summary>
        /// 显示面板
        /// </summary>
        public void Show();

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide();

        /// <summary>
        /// 设置面板位置
        /// </summary>
        public void SetPosition(Vector2 anchoredPosition);

        /// <summary>
        /// 设置面板尺寸
        /// </summary>
        public void SetSize(Vector2 size);

        /// <summary>
        /// 静态工厂方法：创建面板实例
        /// </summary>
        public static UnifiedScreenPanel Create(Transform parent = null);
    }
}
```

### 7.4 CanvasGroupTweener 接口

```csharp
namespace ElectroOptics.UI.ScreenDisplay
{
    public static class CanvasGroupTweener
    {
        /// <summary>
        /// 淡入
        /// </summary>
        public static IEnumerator FadeIn(CanvasGroup group, float duration);

        /// <summary>
        /// 淡出
        /// </summary>
        public static IEnumerator FadeOut(CanvasGroup group, float duration);

        /// <summary>
        /// 交叉淡入淡出（并行）
        /// </summary>
        public static IEnumerator CrossFade(CanvasGroup from, CanvasGroup to, float duration);

        /// <summary>
        /// 设置 alpha（立即）
        /// </summary>
        public static void SetAlpha(CanvasGroup group, float alpha);
    }
}
```

---

## 8. 错误处理

### 8.1 错误场景与处理

| 错误场景 | 处理方式 | 用户反馈 |
|----------|----------|----------|
| CrystalRuntime 未初始化 | 保持红点追踪模式 | 无（静默处理） |
| ConoscopicTextureRenderer 为空 | 保持红点追踪模式 | 无 |
| DirectScreenController.SharedTexture 为空 | 显示占位图 | Console 警告 |
| 过渡动画被中断 | 立即完成过渡到目标状态 | 无 |
| 预渲染超时 | 跳过预渲染，直接切换 | Console 警告 |

### 8.2 日志规范

```csharp
// 使用统一前缀
private const string LOG_PREFIX = "[UnifiedScreenPanel]";

// 日志级别
Debug.Log($"{LOG_PREFIX} 模式切换: {CurrentMode} -> {newMode}");
Debug.LogWarning($"{LOG_PREFIX} CrystalRuntime 未初始化，保持红点追踪模式");
Debug.LogError($"{LOG_PREFIX} 无法创建面板，Canvas 未找到");
```

### 8.3 空值安全检查

```csharp
// 所有外部引用在使用前进行空值检查
private void UpdateDisplay()
{
    if (_directDataProvider != null && _directDataProvider.IsAvailable)
    {
        _directLayerImage.texture = _directDataProvider.GetTexture();
    }

    if (_conoscopicDataProvider != null && _conoscopicDataProvider.IsAvailable)
    {
        _conoscopicLayerImage.texture = _conoscopicDataProvider.GetTexture();
    }
}
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
| 场景启动 | 加载 Scene2 | 面板自动创建并显示在左下角 |
| 无晶体时显示 | 不放置晶体 | 显示红点追踪（白底） |
| 放置晶体 | 将晶体放到导轨上 | 自动切换到锥光干涉（黑底），过渡平滑 |
| 移除晶体 | 将晶体从导轨移开 | 自动切换回红点追踪，过渡平滑 |
| 激光照射 | 开启激光照射光屏 | 红点正确显示位置 |
| 电压调节 | 调节晶体电压 | 干涉图案实时变化 |

### 9.3 性能测试

| 测试项目 | 基准值 | 通过条件 |
|----------|--------|----------|
| 过渡动画帧率 | 60fps | >= 30fps |
| 切换响应时间 | 100ms | < 200ms |
| 内存增量 | 50MB | < 100MB |
| GPU 渲染时间 | 5ms | < 16ms |

### 9.4 兼容性测试

| 测试项 | 说明 |
|--------|------|
| 现有光学链 | 确保激光发射-偏振片-晶体-光屏链路正常 |
| 场景切换 | 确保切换场景后面板正确销毁 |
| 多实例 | 确保不会创建重复的面板实例 |
| 分辨率缩放 | 确保 1920x1080 和其他分辨率下显示正常 |

---

## 10. 文件清单汇总

### 10.1 新建文件

| 文件路径 | 说明 |
|----------|------|
| `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/UnifiedScreenPanel.cs` | 统一面板主控制器 |
| `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/ScreenMode.cs` | 显示模式枚举 |
| `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/CanvasGroupTweener.cs` | CanvasGroup 动画工具类 |
| `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/IScreenDataProvider.cs` | 数据提供者接口 |
| `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/DirectScreenDataProvider.cs` | 红点追踪数据提供者 |
| `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/ConoscopicScreenDataProvider.cs` | 锥光干涉数据提供者 |
| `ElectroOptic-Lab/Assets/Scripts/UI/ScreenDisplay/PanelDragHandler.cs` | 面板拖拽处理 |
| `ElectroOptic-Lab/Assets/Prefabs/UI/UnifiedScreenPanel.prefab` | 面板 Prefab |

### 10.2 修改文件

| 文件路径 | 修改内容 |
|----------|----------|
| `ElectroOptic-Lab/Assets/Scripts/LightScreen/DirectScreenController.cs` | 添加 SharedTexture 属性，移除弹窗逻辑 |
| `ElectroOptic-Lab/Assets/Scripts/DataTransfer/CrystalRuntime.cs` | 确保接口兼容（无需修改） |
| `ElectroOptic-Lab/Assets/Scripts/Experiment/Renderer/ConoscopicTextureRenderer.cs` | 确保接口兼容（无需修改） |

### 10.3 标记过时文件

| 文件路径 | 处理方式 |
|----------|----------|
| `ElectroOptic-Lab/Assets/Scripts/UI/ScreenPopup/ScreenPopupManager.cs` | 添加 [Obsolete] 标记 |
| `ElectroOptic-Lab/Assets/Scripts/UI/ScreenPopup/ConoscopicWindowView.cs` | 添加 [Obsolete] 标记 |

---

## 11. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| WaitForEndOfFrame 在某些情况下可能不生效 | 空白帧 | 添加超时机制，超时后直接切换 |
| Canvas 重复创建 | UI 异常 | 使用单例模式或静态缓存 |
| 晶体检测不准确 | 错误切换 | 增加检测容错范围，添加手动切换按钮 |
| 内存泄漏（纹理未释放） | 内存增长 | 在 OnDestroy 中正确释放资源 |

---

## 12. 附录

### 12.1 相关代码引用

- DirectScreenController: `G:\ElectroOptic_Lab_Unity\v2\ElectroOptic-Lab-Unity\ElectroOptic-Lab\Assets\Scripts\LightScreen\DirectScreenController.cs`
- ConoscopicTextureRenderer: `G:\ElectroOptic_Lab_Unity\v2\ElectroOptic-Lab-Unity\ElectroOptic-Lab\Assets\Scripts\Experiment\Renderer\ConoscopicTextureRenderer.cs`
- CrystalRuntime: `G:\ElectroOptic_Lab_Unity\v2\ElectroOptic-Lab-Unity\ElectroOptic-Lab\Assets\Scripts\DataTransfer\CrystalRuntime.cs`
- ScreenPopupManager: `G:\ElectroOptic_Lab_Unity\v2\ElectroOptic-Lab-Unity\ElectroOptic-Lab\Assets\Scripts\UI\ScreenPopup\ScreenPopupManager.cs`
- ConoscopicWindowView: `G:\ElectroOptic_Lab_Unity\v2\ElectroOptic-Lab-Unity\ElectroOptic-Lab\Assets\Scripts\UI\ScreenPopup\ConoscopicWindowView.cs`

### 12.2 参考设计

- WindowsCanvas 配置: RenderMode.ScreenSpaceOverlay, ReferenceResolution 1920x1080
- 双图层叠加: ConoscopicLayer 在底层，DirectLayer 在顶层
- 淡入淡出: 使用 CanvasGroup.alpha 控制，DOTween 或协程实现

---

## 13. 变更历史

| 版本 | 日期 | 变更内容 |
|------|------|----------|
| v1.0 | 2026-03-15 | 初始版本 |
