# 开发日志：锥光干涉图旋钮调整功能

## 基本信息

- **分支**: `feature/conoscopic-interference-adjustment`
- **日期**: 2026-04-07
- **关联文档**: [PRD](PRD_Conoscopic_KnobAdjust.md) | [配置教程](Guide_Conoscopic_KnobAdjust_Setup.md)

## 背景与动机

Scene2 实验室场景中，晶体盒模型上已有两组旋钮按钮（LU/RD 各顺时针、逆时针），但它们的 onClick 事件为空。KnobAdjuster 脚本只有视觉旋转功能且 targetKnob 未赋值。整条数据通路处于断开状态，用户按下按钮无任何效果。

目标是接通按钮到物理计算管线，实现模拟实验中通过旋钮校准晶体角度、观察锥光干涉图变化的交互。

## 数据流分析

在开发前完整梳理了现有的锥光干涉图数据流：

```
旋转输入 → CrystalControllerWrapper.AddRotation(Vector2)
  → SetRotation (clamp ±15°) → UpdatePhysicsConfig()
    → CrystalPhysicalCore.ApplyConfig(CrystalConfig)
      → NativeInterface.SafeCalculate() [C++ DLL]
        → 输出 n_prime[3], rotation_matrix[9]
          → ConoscopicTextureRenderer.UpdateShaderProperties()
            → ConoscopicInterference.shader (GPU 计算)
              → UnifiedScreenPanel 显示
```

关键发现：
1. 数据通路本身完整，只缺少输入端（按钮→Controller 的连接）
2. 场景中存在两套独立的旋转机制：`CrystalControllerWrapper`（Experiment模块）和 `CrystalInteract`（原始代码，Scene2 未挂载）
3. `CrystalComponentInitializer` 在 Start() 中动态添加 Controller，存在时序问题

## 技术决策

### 选择 CrystalControllerWrapper 而非 CrystalInteract

| 方案 | 优点 | 缺点 |
|------|------|------|
| CrystalInteract | 代码已存在 | 未挂载；直接操作 Quaternion 无角度限制；需修改原有代码 |
| CrystalControllerWrapper | 有 ±15° clamp；有 AddRotation API；符合解耦原则 | 需处理延迟初始化 |

**决策**：复用 CrystalControllerWrapper，通过 CrystalRuntime.Controller 静态访问。

### 使用 EventTrigger 而非修改 KnobAdjuster

KnobAdjuster 已实现 IPointerDownHandler/IPointerUpHandler，但它只做视觉旋转。为避免修改原有代码，选择通过 EventTrigger 组件添加 PointerDown/PointerUp 回调。EventTrigger 回调与 IPointerHandler 接口互不干扰，可以共存。

### 延迟获取 Controller

CloseUpUI 默认不激活，只有用户进入近景视角时才启用。此时 CrystalComponentInitializer.Start() 通常已执行完毕。但为安全起见，OnEnable 中使用协程等待 CrystalRuntime.Controller 可用。

## 实现内容

### 新增文件

| 文件 | 路径 | 说明 |
|------|------|------|
| CrystalKnobBridge.cs | `Scripts/Experiment/Controller/` | 桥接脚本 |
| PRD_Conoscopic_KnobAdjust.md | 项目根目录 | 需求文档 |
| Guide_Conoscopic_KnobAdjust_Setup.md | 项目根目录 | 配置教程 |
| DevLog_Conoscopic_KnobAdjust.md | 项目根目录 | 本文件 |

### 未修改的文件

遵循解耦原则，未修改任何现有脚本：
- CrystalControllerWrapper.cs
- KnobAdjuster.cs
- CrystalPhysicalCore.cs
- CrystalComponentInitializer.cs
- CrystalRuntime.cs

### Scene2 中需手动配置（见配置教程）

- CloseUpUI 添加 CrystalKnobBridge 组件
- 4 个按钮字段赋值
- 轴映射和旋转速度配置
- （可选）KnobAdjuster 的 targetKnob 赋值

## Scene2 按钮结构记录

```
晶体盒物体 (Prefab 实例)
├── CloseUpUI (Canvas, World Space, 默认不激活)
│   ├── Btn_LU_ClockWise     [Button + Image + KnobAdjuster(targetKnob=null)]
│   ├── Btn_RD_ClockWise     [Button + Image]
│   ├── Btn_LU_AntiClockWise [Button + Image]
│   └── Btn_RD_AntiClockWise [Button + Image]
├── CameraAnchor (近景摄像机锚点)
└── (其他子物体...)

扩束镜物体 (独立，不在本次范围)
├── CloseUpUI
│   ├── Btn_Up_ClockWise
│   ├── Btn_Up_AntiClockWise
│   ├── Btn_Right_ClockWise
│   └── Btn_Right_AntiClockWise
```

## 待验证事项

- [ ] 按住按钮时干涉图是否实时变化
- [ ] LU/RD 两组按钮分别控制不同旋转轴
- [ ] 角度到达 ±15° 边界时是否正确停止
- [ ] CloseUpUI 关闭时按压状态是否正确清除
- [ ] KnobAdjuster 与 EventTrigger 是否正常共存（无事件冲突）
- [ ] 轴映射方向是否需要互换（需运行时确认）
