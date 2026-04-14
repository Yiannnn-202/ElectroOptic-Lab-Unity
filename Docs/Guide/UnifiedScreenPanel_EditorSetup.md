# Unity Editor 配置指南 - 统一光屏面板部署

## 背景

代码已全部生成完毕，需要在 Unity Editor 中完成场景配置，将新组件挂载到 Scene2 并连接引用。

---

## 步骤 1：打开场景

在 Unity Editor 中打开 `Assets/Scenes/Scene2.The Lab.unity`

---

## 步骤 2：部署 CrystalComponentInitializer（实验模块前置依赖）

> 这是锥光干涉模式能正常工作的前提。如果跳过此步，面板将始终停留在红点追踪模式（不会报错，但 Conoscopic 功能不可用）。

1. 在 Hierarchy 窗口空白处 **右键 → Create Empty**
2. 命名为 `CrystalInitializer`
3. 选中该对象，在 Inspector 中点击 **Add Component**
4. 搜索并添加 `CrystalComponentInitializer`
5. 配置 Inspector 参数：

| 参数 | 值 | 说明 |
|------|-----|------|
| Crystal Model | **留空** | 会自动查找名为"晶体"的 GameObject |
| Render Texture Size | `512` | 默认即可 |
| Conoscopic FOV | `10` | 视场角，默认即可 |
| Laser Color | `Red (255, 0, 0)` | 默认即可 |

6. 确认场景中有名为 **"晶体"** 的 GameObject（该对象应已存在于场景中）

---

## 步骤 3：部署 UnifiedScreenPanel

1. 在 Hierarchy 窗口空白处 **右键 → Create Empty**
2. 命名为 `ScreenDisplayPanel`
3. 选中该对象，在 Inspector 中点击 **Add Component**
4. 搜索并添加 `UnifiedScreenPanel`（命名空间：`ElectroOptics.UI.ScreenDisplay`）
5. 配置 Inspector 参数：

| 参数 | 值 | 说明 |
|------|-----|------|
| Direct Screen Controller | **拖入光屏对象** | Hierarchy 中找到 **"光屏"** GameObject，拖到此槽位 |
| Panel Size | `(600, 600)` | 默认即可，可按需调整 |
| Panel Position | `(320, 200)` | 面板左下角距屏幕左下角的偏移，可按需调整 |
| Transition Duration | `0.3` | 淡入淡出时长（秒） |

### 如何找到"光屏"对象

在 Hierarchy 中搜索 **"光屏"**，该对象上已挂载 `DirectScreenController` 组件，Inspector 中可以看到：
- `Crystal Optical Component` 已关联到晶体的 OpticalComponent
- `Screen Local Width` = 0.01
- `Screen Local Height` = 0.025

直接将这个 GameObject 拖到 `UnifiedScreenPanel` 的 `Direct Screen Controller` 槽位即可。

---

## 步骤 4：确认 ConoscopicPreview Layer（如未创建过）

1. 菜单栏 **Edit → Project Settings → Tags and Layers**
2. 展开 **Layers** 列表
3. 在空的 User Layer 中添加一层名为 `ConoscopicPreview`
4. 如果此 Layer 已存在，跳过此步

> 此 Layer 用于隔离锥光干涉渲染，避免干扰主摄像机。

---

## 步骤 5：运行验证

1. 点击 **Play** 进入运行模式
2. Console 中应看到以下日志（无红色 Error）：

```
[CrystalComponentInitializer] 开始初始化...
[CrystalComponentInitializer] 找到晶体模型: 晶体
[CrystalComponentInitializer] 已注册到 CrystalRuntime
[UnifiedScreenPanel] 初始化完成
```

3. 屏幕左下角应出现白色背景面板（Direct 模式 / 红点追踪）
4. 将晶体拖拽吸附到导轨上 → 面板应平滑过渡到黑底干涉图
5. 将晶体从导轨移开 → 面板应平滑切回白底红点

---

## 故障排查

| 现象 | 原因 | 解决方案 |
|------|------|----------|
| 面板不显示 | `UnifiedScreenPanel` 未挂载或未启用 | 检查 ScreenDisplayPanel 对象是否 active |
| 面板始终白底（不切换） | `CrystalComponentInitializer` 未部署，或晶体未命名为"晶体" | 检查 CrystalInitializer 对象；Console 查看是否有初始化日志 |
| Console 报 `DirectScreenController 引用为空` | 未拖入光屏引用 | 将"光屏"拖到 UnifiedScreenPanel 的 Inspector 槽位 |
| 干涉图全黑 / 无图案 | ConoscopicPreview Layer 未创建 | 按步骤 4 添加 Layer |
| Console 报 `无晶体选择数据` | 直接从 Scene2 启动（未经过 Scene2-preview 选择晶体） | 正常警告，不影响功能。若需测试干涉图，先从 Scene0 走完整流程 |
| `Missing Script` 警告 | 旧组件残留 | 确认光屏上没有 `ScreenPopupManager` 组件（已确认未挂载，通常不会出现） |

---

## 场景层级结构参考

完成配置后，Hierarchy 应包含（新增部分标注 ★）：

```
Scene2.The Lab
├── ... (原有场景对象)
├── 光屏                          ← DirectScreenController（已有，不动）
├── 晶体                          ← OpticalComponent（已有，不动）
├── CrystalInitializer   ★       ← CrystalComponentInitializer
└── ScreenDisplayPanel   ★       ← UnifiedScreenPanel
```

运行后自动生成的 UI 层级（不需要手动创建）：

```
WindowsCanvas (自动创建)
└── UnifiedScreenPanel
    ├── ConoscopicLayer (CanvasGroup + 黑底 + RawImage)
    └── DirectLayer (CanvasGroup + 白底 + RawImage)
```

---

## 涉及的代码文件

| 文件 | 路径 | 说明 |
|------|------|------|
| UnifiedScreenPanel.cs | `Scripts/UI/ScreenDisplay/` | 统一面板主控 |
| IScreenDataProvider.cs | `Scripts/UI/ScreenDisplay/` | 数据源接口 |
| ScreenMode.cs | `Scripts/UI/ScreenDisplay/` | 模式枚举 |
| DirectScreenDataProvider.cs | `Scripts/UI/ScreenDisplay/` | 红点追踪数据源 |
| ConoscopicScreenDataProvider.cs | `Scripts/UI/ScreenDisplay/` | 锥光干涉数据源 |
| CanvasGroupTweener.cs | `Scripts/UI/ScreenDisplay/` | 淡入淡出动画 |
| CrystalComponentInitializer.cs | `Scripts/Experiment/Initializer/` | 晶体组件初始化器 |
| DirectScreenController.cs | `Scripts/LightScreen/` | 光屏控制器（已有） |
