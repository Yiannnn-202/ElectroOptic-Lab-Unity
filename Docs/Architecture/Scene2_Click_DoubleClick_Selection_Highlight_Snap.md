# Scene2.The Lab 物体交互脚本逻辑整理

本文档整理 Scene2.The Lab 场景中物体上挂载的关于单击、双击、选中、高亮和吸附的脚本逻辑。

## 概述

在 Scene2.The Lab 场景中，多个物体通过挂载不同的 MonoBehavior 脚本实现了丰富的交互功能：
- **单击 (Click)**：聚焦相机、拾取物体、切换状态
- **双击 (Double Click)**：选择晶体、打开UI、切换场景、取下物体
- **选中 (Selection)**：状态管理、高亮显示、唯一选中逻辑
- **高亮 (Highlight)**：轮廓线 (Outline) 或材质变色
- **吸附 (Snap)**：将光学组件吸附到导轨 (OpticalRail) 上

## 脚本列表

### 1. FocusableItem.cs (`Assets/Scripts/FocusableItem.cs`)
**职责**：提供基础的选中/取消选中接口，预留相机锚点。

**关键字段**：
- `closeUpCameraAnchor`：特写相机的位置/旋转参考。

**关键方法**：
- `OnSelected()`：选中时调用（当前仅打印日志）。
- `OnDeselected()`：取消选中时调用（当前为空）。

**备注**：
- 新增加的脚本，尚未被其他组件广泛使用。
- 可扩展为通用的“可聚焦物体”基类。

### 2. CrystalInteract.cs (`Assets/Scripts/CrystalInteract.cs`)
**职责**：处理晶体的双击选中、高亮显示，以及在选中且UI打开时通过 WASD 微调晶体角度。

**关键字段**：
- `isSelected`：晶体是否被选中。
- `isUIOpen`：UI 是否打开（与 ScreenInteract 同步）。
- `highlightColor`：选中时的颜色。
- `rotationSpeed`：WASD 调整角度的速度。

**关键方法**：
- `OnMouseDown()`：检测双击，选中晶体并高亮。
- `SetHighlight(bool active)`：切换材质颜色实现高亮。
- `UpdateConfigAndSendToDLL()`：根据 WASD 输入实时更新晶体角度并提交给底层 DLL。

**依赖**：
- 需要 `Collider` 和 `CrystalPhysicalCore` 组件。
- 与 `ScreenInteract` 通过 `isUIOpen` 状态联动。

### 3. ScreenInteract.cs (`Assets/Scripts/ScreenInteract.cs`)
**职责**：处理屏幕的双击交互，用于打开/关闭锥光干涉图 UI。

**关键字段**：
- `conoscopeUIPanel`：锥光干涉图 UI 面板。

**关键方法**：
- `OnMouseDown()`：检测双击，当晶体被选中时切换 UI 面板的激活状态，并同步 `CrystalInteract.Instance.isUIOpen`。

**依赖**：
- 需要 `Collider` 组件。
- 依赖 `CrystalInteract.Instance` 判断晶体选中状态。

### 4. ClickAreaFocus.cs (`Assets/Scripts/UI/VoltageSwitch/ClickAreaFocus.cs`)
**职责**：处理点击区域的单击/双击逻辑。单击时调用相机聚焦，双击时跳转场景。

**关键字段**：
- `focusPoint`：相机聚焦的目标点。
- `targetSceneName`：双击时要跳转的场景名。
- `doubleClickInterval`：双击间隔时间（默认 0.35s）。

**关键方法**：
- `OnMouseDown()`：计算点击间隔，双击则跳转场景，单击则调用 `CameraFocusController.FocusOn(focusPoint)`。

**依赖**：
- 依赖 `CameraFocusController` 挂载在主相机上。

### 5. CameraFocusController.cs (`Assets/Scripts/UI/VoltageSwitch/CameraFocusController.cs`)
**职责**：平滑移动相机到指定目标点。

**关键字段**：
- `moveSpeed`、`rotateSpeed`：移动和旋转的插值速度。

**关键方法**：
- `FocusOn(Transform focusPoint)`：开始向目标点移动。
- `Update()`：每帧插值更新相机位置和旋转，到达阈值后停止。

### 6. OpticalComponent.cs (`Assets/Scripts/OpticalComponent_Keyboard.cs`)
**职责**：光学组件的键盘控制、拾取、移动、吸附到导轨。支持单击拾取/放下，双击从导轨上取下。

**关键字段**：
- `railLayer`：导轨所在的层。
- `snapRotationY`：吸附时的 Y 轴旋转角度。
- `snapYOffset`：吸附时的手动 Y 轴偏移。
- `isSelected`：该组件是否被选中。
- `isOnRail`：是否已吸附在导轨上。
- `currentSelectedComponent`：静态变量，记录全场唯一选中的物体。

**关键方法**：
- `OnMouseDown()`：处理单击/双击逻辑。
  - 若物体在导轨上，双击则取下并拾起。
  - 若物体未选中，单击则拾起（并确保唯一选中）。
  - 若物体已选中，单击则放下。
- `PickUp()`：拾起物体，升高高度，启用轮廓线。
- `TryDrop()`：尝试放下物体，检测附近导轨并吸附，否则落回原位。
- `SnapToRail(OpticalRail rail)`：将物体吸附到指定导轨，调用 `rail.GetSnapPosition` 计算位置。
- `HandleKeyboardMove()`：使用键盘方向键移动选中的物体。

**依赖**：
- 需要 `Outline` 组件（运行时自动添加）。
- 需要 `Collider` 或 `Rigidbody` 组件。
- 依赖 `OpticalRail` 提供的吸附位置计算。

### 7. OpticalRail.cs (`Assets/Scripts/OpticalRail.cs`)
**职责**：定义导轨的几何属性，提供吸附位置计算。

**关键字段**：
- `railDirection`：导轨方向（默认为 X 轴）。
- `railLength`：导轨长度。
- `railHeightOffset`：导轨高度偏移。

**关键方法**：
- `GetSnapPosition(Vector3 worldPosition)`：将世界坐标点吸附到导轨上，限制 X 轴范围，固定 Y 为轨道顶面，Z 为 0。

**备注**：
- 在编辑器中用绿色 Gizmo 绘制导轨。

### 8. CrystalCardSelector.cs (`Assets/Scripts/UI/CrystalSelector/CrystalCardSelector.cs`)
**职责**：在预览场景（Scene2-preview）中处理晶体卡片的点击选择，保存选择的晶体 Profile 并跳转到实验场景。

**关键字段**：
- `crystalProfile`：绑定的晶体配置资源。
- `targetSceneName`：目标场景名（默认为 Scene2.The Lab）。

**关键方法**：
- `OnSelected()`：实现 `ICrystalSelectable` 接口，调用 `OnCardClick()`。
- `OnCardClick()`：保存 `CrystalSelectionData.SelectedProfile`，加载目标场景。

**依赖**：
- 实现 `ICrystalSelectable` 接口。
- 使用 `CrystalSelectionData` 静态类跨场景传递数据。

### 9. ICrystalSelectable.cs (`Assets/Scripts/Experiment/Interfaces/ICrystalSelectable.cs`)
**职责**：定义晶体卡片选择接口。

**关键方法**：
- `GetCrystalProfile()`：返回晶体 Profile。
- `OnSelected()`：选中时调用。
- `GetDisplayName()`、`GetDescription()`：提供显示信息。

## 交互逻辑详解

### 单击/双击检测机制
所有脚本均采用相似的单击/双击检测模式：
```csharp
private float lastClickTime = 0f;
private const float DOUBLE_CLICK_THRESHOLD = 0.3f;

void OnMouseDown()
{
    float timeSinceLastClick = Time.time - lastClickTime;
    if (timeSinceLastClick <= DOUBLE_CLICK_THRESHOLD)
    {
        // 双击处理
    }
    // 单击处理（若有）
    lastClickTime = Time.time;
}
```
- `CrystalInteract`：双击选中晶体，单击无单独作用。
- `ScreenInteract`：双击打开/关闭 UI（需晶体已选中）。
- `ClickAreaFocus`：双击跳转场景，单击聚焦相机。
- `OpticalComponent`：双击从导轨取下，单击拾取/放下。

### 选中状态管理
- **唯一选中**：`OpticalComponent` 使用静态变量 `currentSelectedComponent` 确保全场只有一个物体被拾起。拾取新物体前会强制放下旧物体。
- **晶体选中**：`CrystalInteract` 的 `isSelected` 标记晶体是否被双击选中，并与 `ScreenInteract` 共享 `isUIOpen` 状态。
- **卡片选中**：`CrystalCardSelector` 在预览场景中处理卡片选中，数据通过静态类传递。

### 高亮显示方式
1. **材质颜色**：`CrystalInteract.SetHighlight()` 切换材质的 `_Color` 属性。
2. **轮廓线**：`OpticalComponent` 使用 `Outline` 组件（运行时自动添加），拾取时启用。
3. **预留接口**：`FocusableItem` 预留了高亮接口，可扩展轮廓线或发光效果。

### 相机聚焦流程
1. `ClickAreaFocus.OnMouseDown()` 单击事件。
2. 获取主相机的 `CameraFocusController` 组件。
3. 调用 `FocusOn(focusPoint)`，设置目标点并开始移动。
4. `CameraFocusController.Update()` 中通过 `Vector3.Lerp` 和 `Quaternion.Slerp` 平滑插值，直到位置和旋转误差小于阈值。

### 吸附到导轨流程
1. `OpticalComponent.TryDrop()` 放下物体时，调用 `CheckDropTarget()`。
2. 以 `detectionRadius` 为半径检测 `railLayer` 层上的碰撞体。
3. 找到 `OpticalRail` 组件后，调用 `SnapToRail(railScript)`。
4. `SnapToRail` 内：
   - 设置 `isOnRail = true`，记录 `currentRail`。
   - 应用固定的 Y 轴旋转 (`snapRotationY`)。
   - 调用 `rail.GetSnapPosition(transform.position)` 计算吸附位置。
   - 调整 Y 坐标考虑碰撞体底部偏移和手动 `snapYOffset`。
   - 更新物体位置。

## 脚本依赖关系图

```
┌─────────────────┐      ┌──────────────────┐
│  ClickAreaFocus │ ───> │CameraFocusController│
└─────────────────┘      └──────────────────┘
        │
        ▼ (双击)
┌─────────────────┐
│  SceneManager   │ (跳转场景)
└─────────────────┘

┌─────────────────┐      ┌─────────────────┐
│ CrystalInteract │ <──> │  ScreenInteract │
└─────────────────┘      └─────────────────┘
        │  (双击选中)            │ (双击开/关 UI)
        ▼                       ▼
┌─────────────────┐      ┌─────────────────┐
│CrystalPhysicalCore│    │  Conoscope UI   │
└─────────────────┘      └─────────────────┘

┌─────────────────┐      ┌─────────────────┐
│OpticalComponent │ ───> │   OpticalRail   │
└─────────────────┘      └─────────────────┘
        │  (吸附)
        ▼
┌─────────────────┐
│DirectScreenController│ (检测吸附状态)
└─────────────────┘

┌─────────────────┐      ┌─────────────────┐
│CrystalCardSelector│ ──>│CrystalSelectionData│
└─────────────────┘      └─────────────────┘
        │
        ▼ (加载场景)
┌─────────────────┐
│  SceneManager   │
└─────────────────┘
```

## 使用注意事项

1. **双击间隔**：所有脚本的双击间隔阈值均为 0.3 秒左右，保持一致。
2. **唯一选中**：`OpticalComponent` 确保同时只能有一个物体被拾起，避免冲突。
3. **组件依赖**：
   - `CrystalInteract` 和 `ScreenInteract` 需要 `Collider`。
   - `OpticalComponent` 需要 `Collider` 或 `Rigidbody`，会自动添加 `Outline`。
   - `CameraFocusController` 应挂载在主相机上。
4. **层设置**：`OpticalComponent.railLayer` 需正确设置导轨所在的层，否则吸附检测失效。
5. **坐标系统**：`OpticalRail` 吸附计算在局部空间进行，确保导轨旋转后吸附位置正确。
6. **高亮性能**：若大量物体需要高亮，建议使用 GPU Instancing 或替换为轮廓线 Shader。

## 扩展建议

- **FocusableItem** 可作为其他可交互物体的基类，统一实现选中/取消选中接口。
- **吸附系统** 可扩展为通用接口 `ISnapTarget`，支持多种吸附目标（如插座、卡槽）。
- **双击检测** 可抽象为通用工具类，减少代码重复。
- **高亮系统** 可统一使用 `Outline` 组件，通过管理器控制显示/隐藏。

---

*文档生成日期：2026-04-01*
*基于代码分析，实际挂载情况请以 Unity Editor 为准。*