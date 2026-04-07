# 配置教程：锥光干涉图旋钮调整功能

## 前置条件

- Unity 2022.3.62f2c1 已打开项目
- 已切换到 `feature/conoscopic-interference-adjustment` 分支
- `CrystalKnobBridge.cs` 已存在于 `Scripts/Experiment/Controller/` 目录

## 步骤 1：找到 CloseUpUI 物体

1. 打开场景 `Assets/Scenes/Scene2.The Lab.unity`
2. 在 Hierarchy 中找到晶体物体（从 Prefab 实例化的晶体盒模型）
3. 展开晶体物体的子级，找到 **CloseUpUI**（默认处于未激活状态，Inspector 中勾选框为空）

> CloseUpUI 是一个带有 Canvas 组件的 World Space UI，它在进入近景视角时由 `RailObjectMover` 激活。

## 步骤 2：添加 CrystalKnobBridge 组件

1. 选中 **CloseUpUI** 物体
2. 在 Inspector 面板底部点击 **Add Component**
3. 搜索 `CrystalKnobBridge`，点击添加

## 步骤 3：配置按钮引用

展开 CloseUpUI 的子物体，可以看到 4 个按钮：

```
CloseUpUI
├── Btn_LU_ClockWise        ← 左上旋钮 顺时针
├── Btn_RD_ClockWise        ← 右下旋钮 顺时针
├── Btn_LU_AntiClockWise    ← 左上旋钮 逆时针
└── Btn_RD_AntiClockWise    ← 右下旋钮 逆时针
```

在 CrystalKnobBridge 组件的 Inspector 中，将对应按钮拖入字段：

| Inspector 字段 | 拖入的物体 |
|---------------|-----------|
| Btn LU Clockwise | `Btn_LU_ClockWise` |
| Btn LU Anti Clockwise | `Btn_LU_AntiClockWise` |
| Btn RD Clockwise | `Btn_RD_ClockWise` |
| Btn RD Anti Clockwise | `Btn_RD_AntiClockWise` |

## 步骤 4：配置轴映射

在 **轴映射** 部分设置每组旋钮控制的旋转轴：

| 字段 | 值 | 含义 |
|------|---|------|
| Lu Axis Index | 0 | LU 旋钮控制 X 轴（俯仰） |
| Rd Axis Index | 1 | RD 旋钮控制 Y 轴（偏航） |

> 如果实际旋钮朝向与预期不符，可以互换这两个值。在运行时测试后根据干涉图变化方向调整。

## 步骤 5：配置旋转速度

| 字段 | 默认值 | 说明 |
|------|-------|------|
| Degrees Per Second | 5.0 | 按住按钮每秒旋转的角度 |

> 建议范围：2.0 ~ 10.0。值越大旋转越快，值越小控制越精细。

## 步骤 6（可选）：配置 3D 旋钮视觉旋转

当前只有 `Btn_LU_ClockWise` 上挂载了 `KnobAdjuster` 组件。如需所有按钮按下时对应的 3D 旋钮模型也旋转：

### 6.1 给其余 3 个按钮添加 KnobAdjuster

1. 分别选中 `Btn_LU_AntiClockWise`、`Btn_RD_ClockWise`、`Btn_RD_AntiClockWise`
2. 点击 **Add Component** → 搜索 `KnobAdjuster` → 添加

### 6.2 配置 KnobAdjuster 参数

需要在晶体盒 Prefab 中找到两个 3D 旋钮模型的 Transform：

| 按钮 | Target Knob | Rotation Axis | Rotate Speed |
|------|------------|---------------|-------------|
| Btn_LU_ClockWise | LU 旋钮 Transform | 根据旋钮朝向设置 | 50（正值=顺时针） |
| Btn_LU_AntiClockWise | LU 旋钮 Transform | 同上 | -50（负值=逆时针） |
| Btn_RD_ClockWise | RD 旋钮 Transform | 根据旋钮朝向设置 | 50 |
| Btn_RD_AntiClockWise | RD 旋钮 Transform | 同上 | -50 |

> 同一组的两个按钮（顺时针/逆时针）应指向同一个 3D 旋钮 Transform，仅 rotateSpeed 正负不同。

### 6.3 确定旋钮 Transform

1. 展开晶体盒 Prefab 的子物体层级
2. 找到两个旋钮的 3D 模型节点
3. 将它们分别拖入对应按钮的 KnobAdjuster → Target Knob 字段

## 步骤 7：保存场景

完成所有配置后：
1. `Ctrl+S` 保存场景
2. 确认 Scene 文件有修改标记

## 验证测试

1. 点击 Unity 的 **Play** 按钮运行场景
2. 在场景中点击晶体，进入近景视角（CloseUpUI 会自动显示）
3. 用鼠标按住 `Btn_LU_ClockWise`，观察：
   - Console 中无报错
   - 底部 UnifiedScreenPanel 的锥光干涉图是否发生变化
4. 松开按钮，干涉图应停止变化
5. 按住 `Btn_LU_AntiClockWise`，干涉图应向反方向变化
6. 测试 RD 组的两个按钮，应控制另一个轴的旋转
7. 持续按住直到角度达到 ±15°，应自动停止变化（角度被 clamp）

## 故障排查

| 问题 | 可能原因 | 解决方法 |
|------|---------|---------|
| 按钮按下无反应 | CrystalKnobBridge 未正确引用按钮 | 检查 Inspector 中 4 个按钮字段是否都已赋值 |
| Console 报 NullReferenceException | CrystalRuntime.Controller 未初始化 | 确认场景中存在 `CrystalInitializer` 物体并已激活 |
| 干涉图不变化 | ConoscopicTextureRenderer 未刷新 | 确认 UnifiedScreenPanel 处于 Conoscopic 模式（晶体需放在导轨上） |
| 轴方向反了 | luAxisIndex/rdAxisIndex 设置不对 | 互换两个值（0↔1） |
| 旋转太快/太慢 | degreesPerSecond 值不合适 | 调整到 2.0~10.0 范围 |
