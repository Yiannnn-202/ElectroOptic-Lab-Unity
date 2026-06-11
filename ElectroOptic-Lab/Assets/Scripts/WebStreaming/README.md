# Web 流式传输部署指南

## 概述

本项目通过 Unity Render Streaming 实现 Windows 端的画面串流到浏览器，
使远端用户可以在网页中操作电光实验室的全部功能。
适用于无法将 CrystalPhysicsCore.dll 编译为 WebGL 的场景。

## 架构

```
浏览器 (WebRTC) ←→ 信令服务器 (Node.js) ←→ Unity Standalone (Windows)
                          ↓
                   Render Streaming
                   (CameraStreamer + InputReceiver)
```

- **Unity 端**：Windows 独立程序，运行全部物理计算和渲染
- **信令服务器**：Node.js 程序，负责 WebRTC 连接协商
- **浏览器端**：Web 页面，接收视频流 + 发送键盘/鼠标输入

## 前置条件

1. Unity 2022.3.62f2c1（已完成）
2. `manifest.json` 中已添加 `com.unity.renderstreaming@3.1.0-exp.7`
3. Player Settings → Input System 已设为 "Both" 模式（已完成）
4. Node.js 18+ 或 Unity Render Streaming webserver

## 场景配置

**一键挂载（推荐）：**

Unity 顶部菜单 → **ElectroOptics → WebStreaming → Add to Active Scene**

这会自动在场景中创建 `WebStreaming` GameObject 并挂载 `WebStreamingSetup` 组件，
自动关联 MainCamera，默认信令地址 `http://localhost`。

**手动配置（备选）：**

在主场景中新建空 GameObject 命名为 `WebStreaming`，挂载 `WebStreamingSetup` 脚本。

Inspector 中设置：
- **Signaling URL**：`http://localhost` (本地) 或 `http://192.168.x.x` (局域网)
- **Stream Camera**：Main Camera
- **Stream Size**：1920×1080

## 四步启动流程

### 步骤一：启动信令服务器

**方式 A：使用 Unity 自带的 WebApp（推荐）**

```bash
# 1. 找到 Render Streaming 包中的 WebApp
cd Library/PackageCache/com.unity.renderstreaming@3.1.0-exp.7/WebApp

# 2. 安装依赖（仅第一次）
npm install

# 3. 启动服务器（监听 80 端口）
node server.js -p 80
```

**方式 B：下载预编译 webserver.exe**

从 https://github.com/Unity-Technologies/UnityRenderStreaming/releases 下载对应版本。

```bash
.\webserver.exe -p 80
```

### 步骤二：打包 Unity 项目

1. File → Build Settings
2. Platform: **PC, Mac & Linux Standalone**
3. Target Platform: **Windows**
4. Architecture: **Intel 64-bit** (x86_64 — CrystalPhysicsCore.dll 需要)
5. ✅ 勾选 **Run In Background**（已自动配置）
6. ✅ 勾选 **Copy PDB files**（可选，调试用）
7. Build → 导出 `ElectroOptic-Lab.exe`

### 步骤三：运行

1. 先启动信令服务器：`node server.js -p 80`
2. 再启动打包好的 `ElectroOptic-Lab.exe`
3. 打开浏览器，访问 `http://localhost`
4. 点击页面上的 **Play** 按钮，开始串流

### 步骤四：局域网 / 公网访问

**局域网内：**
```bash
# 查看本机 IP
ipconfig
# 假设 IP 是 192.168.1.105
# 其他人访问 http://192.168.1.105 即可
```

**公网访问（内网穿透）：**
使用 cpolar / frp 等工具将本地 80 端口映射到公网。

## 远程输入说明

所有原有的键盘和鼠标操作均可在浏览器中使用：

| 原有操作 | 浏览器端操作 |
|----------|-------------|
| 点击光学元件 | 鼠标点击 |
| A/D 移动元件 | 键盘 A/D |
| Space 放下元件 | 空格键 |
| WASD 微调激光 | 键盘 WASD |
| Enter 锁定校准 | 回车键 |
| 点击晶体/激光器/接收器 | 鼠标点击 |
| 旋转旋钮 | 点击 + A/D |
| R 记录数据 | 键盘 R |
| Backspace 删除记录 | 退格键 |

## 技术细节

### 修改的文件

- `Packages/manifest.json` — 添加 Render Streaming + Input System 依赖
- `ProjectSettings/ProjectSettings.asset` — Input System 设为 Both 模式
- 11 个脚本文件中的 `Input.X` 替换为 `RemoteInputRelay.X`

### 新增文件

| 文件 | 功能 |
|------|------|
| `Scripts/WebStreaming/RemoteInputRelay.cs` | 静态桥接层，统一本地/远程输入 API |
| `Scripts/WebStreaming/RemoteInputUpdater.cs` | 每帧驱动，同步远程输入 + OnMouseDown 转发 |
| `Scripts/WebStreaming/WebStreamingSetup.cs` | 自动装配 Render Streaming 组件 |

### 回滚

如需恢复纯本地模式，将 `WebStreamingSetup` GameObject 禁用或删除即可。
所有修改过的脚本在 `RemoteInputRelay.IsRemote` 为 false 时自动回退到原始 `Input` API。
