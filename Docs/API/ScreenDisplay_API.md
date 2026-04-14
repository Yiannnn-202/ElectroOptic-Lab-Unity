# 光屏显示与红点消光系统 API 文档

> 涵盖光屏（LightScreen）红点追踪/消光逻辑，以及统一光屏面板（UnifiedScreenPanel）的双图层显示与模式切换。

## 1. 架构总览

```
LaserEmitter ──(Raycast + LightData)──► DirectScreenController (IOpticalReceiver)
                                              │ 更新 SharedTexture (512x512)
                                              ▼
                                     DirectScreenDataProvider ──┐
                                                                ├─► UnifiedScreenPanel
  CrystalRuntime.TextureRenderer ──► ConoscopicScreenDataProvider ┘   (双图层 + 淡入淡出)
```

- 当**晶体未上导轨**时：面板显示 `Direct` 模式（红点追踪/消光）。
- 当**晶体已上导轨**（`OpticalComponent.isOnRail == true`）时：自动切换到 `Conoscopic` 模式（锥光干涉）。

---

## 2. 脚本清单

| 脚本 | 路径 | 职责 |
|------|------|------|
| `DirectScreenController` | `Scripts/LightScreen/DirectScreenController.cs` | 接收激光、计算命中位置，在 `Texture2D` 上绘制红点；强度消光时呈白屏 |
| `UnifiedScreenPanel` | `Scripts/UI/ScreenDisplay/UnifiedScreenPanel.cs` | 左下固定面板，集成红点与锥光图层，自动模式切换 |
| `IScreenDataProvider` | `Scripts/UI/ScreenDisplay/IScreenDataProvider.cs` | 数据源抽象接口 |
| `DirectScreenDataProvider` | `Scripts/UI/ScreenDisplay/DirectScreenDataProvider.cs` | 包装 `DirectScreenController.SharedTexture` |
| `ConoscopicScreenDataProvider` | `Scripts/UI/ScreenDisplay/ConoscopicScreenDataProvider.cs` | 包装 `CrystalRuntime.TextureRenderer.RenderTexture` |
| `ScreenMode` | `Scripts/UI/ScreenDisplay/ScreenMode.cs` | 模式枚举：`Direct` / `Conoscopic` |
| `CanvasGroupTweener` | `Scripts/UI/ScreenDisplay/CanvasGroupTweener.cs` | 面板淡入淡出协程工具 |
| `ScreenInteract` | `Scripts/ScreenInteract.cs` | 光屏双击打开锥光旧版弹窗（遗留） |

---

## 3. `DirectScreenController`

**命名空间**：全局（无 namespace）  
**基类**：`MonoBehaviour`，实现 `IOpticalReceiver`

### Inspector 字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `crystalOpticalComponent` | `OpticalComponent` | 晶体光学组件引用，用于判断 `isOnRail`（驱动面板模式切换） |
| `screenCube` | `GameObject` | 实际光屏 Cube；未设置时回退到自身 |
| `screenLocalWidth/Height` | `float` | 光屏在局部坐标系下的映射尺寸（默认 0.05） |
| `offsetX/Y` | `float` | 红点位置微调 |
| `invertX/Y` | `bool` | 坐标轴反向开关 |

### 公共成员

| 成员 | 说明 |
|------|------|
| `Texture2D SharedTexture { get; }` | 供 UI 层绑定的 512×512 RGBA 纹理 |
| `void ReceiveLight(LightData, Vector3 hitPoint, Vector3 dir)` | 实现 `IOpticalReceiver`；根据 `hitPoint` 的 local.y/z 映射到画布坐标，并记录光强 |

### 绘制与消光逻辑

- `Update()`：若本帧未收到光，将 `currentIntensity` 以 `10 * dt` 速率向 0 插值 → **消光**。
- 当 `|Δintensity| > 0.05` 或红点位移 > 2px 时调用 `DrawPattern()` 重画。
- `DrawPattern(brightness, cx, cy)`：
  - 若 `brightness < 0.005f`：整屏填白（完全消光）。
  - 否则以 `(cx, cy)` 为中心、半径 25px 的软边圆，按 `Lerp(white, red, brightness * softFactor)` 绘制。

---

## 4. `UnifiedScreenPanel`

**命名空间**：`ElectroOptics.UI.ScreenDisplay`

### Inspector 字段

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `directScreenController` | — | 必填；用于读取纹理 + 检测 `isOnRail` |
| `panelSize` | `(600, 600)` | 面板尺寸（像素） |
| `panelPosition` | `(320, 200)` | 相对左下角的 anchored 位置 |
| `transitionDuration` | `0.3s` | 淡入淡出时长 |

### 公共属性

| 属性 | 说明 |
|------|------|
| `ScreenMode CurrentMode` | 当前模式（只读） |
| `bool IsTransitioning` | 是否处于过渡动画中 |

### 公共方法

| 方法 | 说明 |
|------|------|
| `SwitchToMode(ScreenMode)` | 带淡入淡出切换；过渡中调用将被忽略 |
| `SwitchToModeImmediate(ScreenMode)` | 立即切换，无动画 |
| `Show() / Hide()` | 显隐整个面板 |
| `SetPosition(Vector2)` | 运行时调整位置 |
| `SetSize(Vector2)` | 运行时调整尺寸 |

### 行为要点

- 运行时在 `WindowsCanvas`（ScreenSpaceOverlay，1920×1080 基准）下构建：
  - **ConoscopicLayer**（底层，黑色背景 + RawImage 内容）
  - **DirectLayer**（顶层，白色背景 + RawImage 内容）
- `Update()` 每帧调用 `DetectTargetMode()`，依据 `crystalOpticalComponent.isOnRail` 与 `_conoscopicDataProvider.IsAvailable` 决定目标模式。
- `Conoscopic` 模式下每帧调用 `ConoscopicScreenDataProvider.PreRender()` 驱动 GPU 渲染。
- 切换前先 `PreRender` + `WaitForEndOfFrame`，再通过 `CanvasGroupTweener.CrossFade` 并行淡入/淡出，避免黑屏闪烁。

### 编辑器调试（`[ContextMenu]`）

- `切换到 Direct 模式`
- `切换到 Conoscopic 模式`
- `打印状态`：输出初始化、模式、Provider 可用性、`isOnRail` 等状态。

---

## 5. `IScreenDataProvider` 与实现

```csharp
public interface IScreenDataProvider
{
    Texture GetTexture();
    void PreRender();
    bool IsAvailable { get; }
    string ModeName { get; }
}
```

| 实现 | `GetTexture()` 数据源 | `PreRender()` | `ModeName` |
|------|----------------------|---------------|------------|
| `DirectScreenDataProvider` | `DirectScreenController.SharedTexture`（Texture2D，就地更新） | 空实现（控制器 `Update` 自行刷新） | `"RedDot"` |
| `ConoscopicScreenDataProvider` | `CrystalRuntime.TextureRenderer.RenderTexture` | 调用 `TextureRenderer.UpdateAndRender()` 驱动一次 GPU 渲染 | `"Conoscopic"` |

---

## 6. `ScreenMode`

```csharp
public enum ScreenMode
{
    Direct = 0,      // 红点追踪模式（晶体未上导轨）
    Conoscopic = 1   // 锥光干涉模式（晶体已上导轨）
}
```

---

## 7. 典型使用流程

1. 场景中光屏 Cube 挂 `DirectScreenController`，关联 `crystalOpticalComponent`。
2. 任意 GameObject 挂 `UnifiedScreenPanel`，在 Inspector 中把上一步控制器拖入 `directScreenController`。
3. 运行：
   - 起始为 `Direct` 模式，激光打到光屏 → 光屏上红点亮起，断光 → 红点消光回白。
   - 晶体吸附到导轨 → 面板淡出红点图层，淡入锥光干涉图层。
   - 晶体移出导轨 → 自动反向切回。

---

## 8. 遗留脚本（不推荐新用法）

- `ScreenInteract.cs`：旧版通过双击光屏手动开/关锥光 UI，与 `UnifiedScreenPanel` 的自动切换方案重复，新场景应使用 `UnifiedScreenPanel`。
