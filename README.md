# ElectroOptic-Lab-Unity

一个基于Unity的电光实验室模拟项目，用于模拟晶体光学实验，包括锥光干涉图样、偏振光传播和电光调制。

## 项目概述

这是一个Unity 2022.3.62f2c1项目，模拟电光实验室中的晶体光学实验。项目实现了晶体物理计算、光学组件链式传播、实时干涉图样可视化等功能。

## 主要功能

- **晶体物理模拟**：通过原生C++ DLL计算电光系数、电场下的折射率和旋转矩阵
- **光学组件系统**：链式责任模式的偏振光传播，支持激光发射器、偏振片、屏幕等组件
- **干涉图样可视化**：GPU着色器实时渲染锥光干涉图样
- **实验模块**：可扩展的实验系统，支持晶体选择、旋转配置和结果查看
- **用户界面**：动态弹窗、旋钮控制面板、双点击交互等

## 技术架构

### 核心组件

1. **晶体物理核心** (`CrystalPhysicalCore.cs`)
   - 两阶段配置系统（探测阶段和渲染阶段）
   - 原生DLL集成 (`CrystalPhysicsCore.dll`)
   - 右手坐标系到左手坐标系的转换

2. **光学组件链**
   - `IOpticalReceiver`接口定义
   - `LightData`结构体传递光强和偏振信息
   - 基于射线检测的链式传播

3. **实验模块**
   - 解耦设计原则，不修改原始代码
   - 包装器和适配器模式扩展功能
   - 独立目录结构：`Scripts/DataTransfer/`、`Scripts/Experiment/`、`Scripts/UI/`

### 数据流

1. UI控制 → `LabController` → `CrystalConfig`
2. `CrystalPhysicalCore` → 原生DLL计算 → 物理数据
3. 物理数据 → 着色器属性 → 实时可视化

## 构建和运行

### 环境要求
- Unity 2022.3.62f2c1 或兼容版本
- Windows平台（原生DLL为x86_64架构）

### 打开项目
1. 使用Unity Hub打开项目根目录
2. 主要场景：`Assets/Scenes/Scene2.The Lab.unity`
3. 构建通过Unity标准构建系统

### 项目结构
```
ElectroOptic-Lab-Unity/
├── ElectroOptic-Lab/          # 主Unity项目
│   ├── Assets/Scenes/        # 场景文件
│   ├── Assets/Scripts/       # C#脚本
│   │   ├── Business_logic/   # 业务逻辑（晶体物理）
│   │   ├── DataTransfer/     # 数据传递（实验模块）
│   │   ├── Experiment/       # 实验控制器
│   │   └── UI/               # 用户界面
│   └── Assets/Plugins/       # 原生插件（C++ DLL）
├── CLAUDE.md                 # 项目开发指南
└── README.md                 # 项目说明（本文件）
```

## 开发原则

### 解耦设计
新代码不应修改原始代码，使用包装器/适配器模式扩展功能。实验模块（P0-P2）遵循此原则，所有新代码位于独立目录中。

### 坐标系统
原生DLL使用右手坐标系，Unity使用左手坐标系。转换在`CrystalPhysicalCore.cs`中通过Z轴翻转旋转矩阵处理。

### 材质安全
运行时修改材质时使用`.material`（创建实例）而非`.sharedMaterial`（永久修改资源）。

## 许可证

本项目采用MIT许可证。详见LICENSE文件。

## 更多信息

详细开发指南请参阅[CLAUDE.md](./CLAUDE.md)文件。