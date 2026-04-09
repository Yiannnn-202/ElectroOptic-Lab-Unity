# PRD: 锥光干涉图旋钮调整功能

## 1. 概述

### 1.1 背景
Scene2（实验室场景）中，晶体盒模型上已有两组旋钮（LU/RD），每组包含顺时针和逆时针按钮，用于模拟真实实验中旋转晶体的操作。但当前这些按钮未接通物理计算系统，按下后不产生任何效果。

### 1.2 目标
将晶体盒上的旋钮按钮连接到晶体物理计算管线，实现：按住按钮 → 持续调整晶体旋转角度 → 锥光干涉图实时变化，模拟真实实验中通过旋钮校准光路的过程。

### 1.3 范围
- **包含**：按钮→物理的桥接逻辑、数据通路接通
- **不包含**：新增UI元素、修改现有脚本、扩束镜按钮(Btn_Up_*/Btn_Right_*)

## 2. 用户故事

作为实验操作者，我希望在进入晶体近景视角后，按住晶体盒上的旋钮按钮来调整晶体角度，观察底部屏幕面板中锥光干涉图的实时变化，以便模拟真实实验中的光路校准过程。

## 3. 功能需求

### 3.1 按钮交互
| 需求 | 描述 |
|------|------|
| 按住触发 | 按住按钮时持续旋转，松开停止 |
| 两组旋钮 | LU组控制一个旋转轴，RD组控制另一个旋转轴 |
| 双向控制 | 每组有顺时针（角度+）和逆时针（角度-）两个按钮 |
| 旋转范围 | ±15°（复用现有 CrystalControllerWrapper 的限制） |
| 旋转速度 | 默认 5°/s，可在 Inspector 中配置 |

### 3.2 干涉图响应
| 需求 | 描述 |
|------|------|
| 实时更新 | 按钮按住期间干涉图持续变化 |
| 显示位置 | 底部 UnifiedScreenPanel 的锥光干涉层 |
| 数据通路 | 复用现有 CrystalControllerWrapper → CrystalPhysicalCore → Shader 管线 |

### 3.3 视觉反馈
| 需求 | 描述 |
|------|------|
| 3D旋钮旋转 | 按住按钮时对应的3D旋钮模型同步旋转（复用 KnobAdjuster） |

## 4. 技术设计

### 4.1 数据流
```
用户按住按钮
  → EventTrigger.PointerDown → pressed 标志 = true
    → Update(): 计算 delta[axisIndex] = ±speed × deltaTime
      → CrystalRuntime.Controller.AddRotation(delta)
        → CrystalControllerWrapper.SetRotation (clamp ±15°)
          → CrystalPhysicalCore.ApplyConfig()
            → NativeInterface.SafeCalculate() [C++ DLL]
              → ConoscopicTextureRenderer → Shader → 干涉图更新
```

### 4.2 新增文件
| 文件 | 路径 | 说明 |
|------|------|------|
| CrystalKnobBridge.cs | Scripts/Experiment/Controller/ | 桥接脚本，连接按钮与物理系统 |

### 4.3 设计原则
- **解耦设计**：不修改任何现有脚本，新代码在独立文件中
- **复用现有管线**：通过 CrystalControllerWrapper.AddRotation() 接入
- **与 KnobAdjuster 共存**：使用 EventTrigger（不替换 IPointerHandler），两者独立运行

### 4.4 关键依赖
| 组件 | 作用 |
|------|------|
| CrystalControllerWrapper | 提供 AddRotation(Vector2) API |
| CrystalRuntime | 静态访问 Controller 实例 |
| CrystalComponentInitializer | 运行时动态创建 Controller（需延迟获取） |
| KnobAdjuster | 视觉旋钮旋转（独立运行） |

### 4.5 Edge Cases
- **Controller 延迟初始化**：OnEnable 使用协程等待 CrystalRuntime.Controller 可用
- **UI 关闭时按住按钮**：OnDisable 重置所有 pressed 标志
- **角度边界**：由 CrystalControllerWrapper 内部 clamp

## 5. Scene2 按钮清单

### 晶体盒 CloseUpUI（本次范围）
| 按钮 | 功能 |
|------|------|
| Btn_LU_ClockWise | LU组顺时针（角度+） |
| Btn_LU_AntiClockWise | LU组逆时针（角度-） |
| Btn_RD_ClockWise | RD组顺时针（角度+） |
| Btn_RD_AntiClockWise | RD组逆时针（角度-） |

### 不在本次范围
| 按钮 | 说明 |
|------|------|
| Btn_Up_ClockWise / AntiClockWise | 扩束镜调整 |
| Btn_Right_ClockWise / AntiClockWise | 扩束镜调整 |
| Btn_FrontView / CloseUp / Overview | 摄像机视角切换 |

## 6. 验收标准
1. 按住 LU 顺时针按钮，干涉图沿一个轴方向持续变化
2. 按住 RD 顺时针按钮，干涉图沿另一个轴方向持续变化
3. 逆时针按钮效果与顺时针相反
4. 松开按钮后干涉图停止变化
5. 旋转角度不超过 ±15°
6. 3D旋钮模型同步旋转（需 Editor 中配置 KnobAdjuster.targetKnob）
7. 不影响现有功能（WASD键盘旋转、RailObjectMover 移动等）
