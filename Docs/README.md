# 开发文档索引

> 最后更新：2026-05-12

---

## PRD（产品需求文档）

| 文档 | 说明 |
|------|------|
| [PRD_Conoscopic_IntensitySurface_CalcCore](PRD/PRD_Conoscopic_IntensitySurface_CalcCore.md) | 锥光干涉图附加实验：光强曲面计算核心需求 |
| [PRD_Conoscopic_KnobAdjust](PRD/PRD_Conoscopic_KnobAdjust.md) | 锥光干涉旋钮调节功能需求 |
| [PRD_Oscilloscope_CalcCore](PRD/PRD_Oscilloscope_CalcCore.md) | 示波器计算核心需求 |
| [PRD_ScreenDisplay_Refactor](PRD/PRD_ScreenDisplay_Refactor.md) | 光屏显示重构 — 统一面板架构（替代旧弹窗方案） |

## Architecture（架构设计）

| 文档 | 说明 |
|------|------|
| [Architecture_Codebase_Audit](Architecture/Architecture_Codebase_Audit.md) | **代码库审计** — 已启用逻辑 vs 废弃逻辑、架构冲突、建议行动计划（2026-04-24） |
| [Architecture_Oscilloscope_CalcCore](Architecture/Architecture_Oscilloscope_CalcCore.md) | 示波器计算核心架构设计 |
| [Architecture_Biaxial_Conoscopic_Display](Architecture/Architecture_Biaxial_Conoscopic_Display.md) | 双轴晶体锥光干涉显示设计（KTP 双光轴/双 melatope） |
| [Scene2_Click_DoubleClick_Selection_Highlight_Snap](Architecture/Scene2_Click_DoubleClick_Selection_Highlight_Snap.md) | Scene2 单击/双击/选中/高亮/吸附交互架构 |

## API（接口文档）

| 文档 | 说明 |
|------|------|
| [ScreenDisplay_API](API/ScreenDisplay_API.md) | 光屏显示与红点消光系统 API |

## DevLog（开发日志）

| 文档 | 说明 | 状态 |
|------|------|------|
| [P0_P1_Development_Documentation](DevLog/P0_P1_Development_Documentation.md) | P0-P1 阶段：数据传递 + 晶体初始化 | 已落地 |
| [P2_Development_Documentation](DevLog/P2_Development_Documentation.md) | P2 阶段：弹窗 + 旋转控制面板 | ⚠️ 弹窗方案已被 UnifiedScreenPanel 替代 |
| [DevLog_Conoscopic_KnobAdjust](DevLog/DevLog_Conoscopic_KnobAdjust.md) | 锥光干涉旋钮调节开发日志 |
| [DevLog_Oscilloscope_CalcCore](DevLog/DevLog_Oscilloscope_CalcCore.md) | 示波器计算核心开发日志 |
| [DevLog_Scene2_Conoscopic_FOV_And_Angle_Fix](DevLog/DevLog_Scene2_Conoscopic_FOV_And_Angle_Fix.md) | Scene2 锥光干涉 FOV 与角度修复日志 |

## Guide（操作指南）

| 文档 | 说明 |
|------|------|
| [Guide_Conoscopic_KnobAdjust_Setup](Guide/Guide_Conoscopic_KnobAdjust_Setup.md) | 锥光干涉旋钮调节配置指南 |
| [UnifiedScreenPanel_EditorSetup](Guide/UnifiedScreenPanel_EditorSetup.md) | 统一显示面板编辑器配置指南 |

## Plan（实施计划）

| 文档 | 说明 | 状态 |
|------|------|------|
| [implementation_plan](Plan/implementation_plan.md) | 原始总体实现计划 | 已完成 |
| [Conoscopic_Realistic_Motion_Modification_Plan](Plan/Conoscopic_Realistic_Motion_Modification_Plan.md) | 锥光干涉真实运动修改计划 |
| [Conoscopic_IntensitySurface_CalcCore_Development_Plan](Plan/Conoscopic_IntensitySurface_CalcCore_Development_Plan.md) | 锥光干涉图附加实验：光强曲面计算核心开发计划 |
| [Scene4_Oscilloscope_Dispatcher_Plan](Plan/Scene4_Oscilloscope_Dispatcher_Plan.md) | Scene4 示波器调度与波形显示开发/测试计划 |

---

## 架构演进说明

### 光屏显示：弹窗 → 统一面板

P0-P2 阶段最初采用 `ScreenPopupManager` + `ConoscopicWindowView` 弹窗方案。该方案后来被 [PRD_ScreenDisplay_Refactor](PRD/PRD_ScreenDisplay_Refactor.md) 中的统一面板架构（`UnifiedScreenPanel` + `IScreenDataProvider`）替代。

- **已删除**：`ScreenPopupManager.cs`、`ConoscopicWindowView.cs`（从未挂载到场景）
- **当前方案**：`UnifiedScreenPanel` + `DirectScreenDataProvider` / `ConoscopicScreenDataProvider` + `CanvasGroupTweener`
- **相关 PRD**：[PRD_ScreenDisplay_Refactor](PRD/PRD_ScreenDisplay_Refactor.md)

### 晶体交互：CrystalInteract → CrystalControllerWrapper

旧版 `CrystalInteract.cs`（双击选中 + WASD 旋转 + 随机初始偏转）已被 `CrystalControllerWrapper` + `CrystalRotationPanel` 替代。

- **已废弃**：`CrystalInteract.cs`（无场景引用）
- **当前方案**：`CrystalControllerWrapper`（±15° 范围限制，通过 `CrystalConfig` 调用 `ApplyConfig()`）

### 新增模块（P0-P2 之后）

- **示波器模块**（`Scripts/Oscilloscope/`）：`OscilloscopeCore`、`VpiCalculator`、`WaveformCalculator`、`OscilloscopeWaveformGraphic`
- **统一面板**（`Scripts/UI/ScreenDisplay/`）：`UnifiedScreenPanel`、`IScreenDataProvider`、数据提供者
- **激光调节**（`Scripts/Laser/`）：`LaserStateController`、`LaserEmitterMover`、`LaserKnobBridge`

---

## 文档分类说明

| 目录 | 用途 |
|------|------|
| `PRD/` | 产品需求文档 — 定义"要做什么"和验收标准 |
| `Architecture/` | 架构设计 — 定义"怎么做"的技术方案和模块设计 |
| `API/` | 接口文档 — 公共 API 和接口契约 |
| `DevLog/` | 开发日志 — 实现过程、决策记录、版本记录 |
| `Guide/` | 操作指南 — Unity Editor 配置步骤和使用说明 |
| `Plan/` | 实施计划 — 分阶段开发计划和任务拆解 |
