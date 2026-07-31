# Mobile Branch TODO

> `mobile` 分支目标：先保证手机、Pad、平板等移动端设备可运行、不卡死、不因 Windows x86 原生依赖崩溃；精度和完整物理一致性后续再逐步补齐。

## P0 - 移动端跑通阻塞

- [x] **隔离 `CrystalPhysicsCore.dll` 的 Windows x86/x86_64 依赖**
  - 当前 `NativeInterface.cs` 通过 `DllImport("CrystalPhysicsCore")` 调用 Windows 原生 DLL。
  - Android / iOS 不能加载该 DLL，移动端构建必须避免直接进入 DLL 调用。
  - 建议：在 `NativeInterface.SafeCalculate()` 中按平台分流：
    - Windows Editor / Windows Standalone：继续调用 DLL。
    - Android / iOS / 非 Windows：走 managed fallback，返回保守近似结果，保证上层链路继续运行。

- [x] **为 `NativeInterface` 增加 managed fallback**
  - fallback 输出内容：
    - `n_prime` 使用 `CrystalProfile` 静态折射率加一阶电光近似扰动。
    - `rotation_matrix` 使用 identity。
    - `sensitivity` 使用简化估算值，避免 Vπ / 示波器链路拿到非法值。
  - 目标不是高精度，而是移动端不崩溃、锥光/示波器/功率链路能继续显示。

- [x] **处理 DLL 缺失时的 Windows 兜底**
  - Windows 上如果 DLL 缺失或入口函数缺失，目前会报错并返回失败。
  - 建议改为：`DllNotFoundException` / `EntryPointNotFoundException` 时记录 warning，并进入 managed fallback。
  - 其他未知异常仍可返回失败，避免掩盖真实 native 崩溃。

- [x] **锥光干涉 RenderTexture 格式降级**
  - `ConoscopicTextureRenderer.cs` 当前中间纹理优先使用 `ARGBFloat`，否则使用 `ARGBHalf`。
  - 部分移动 GPU 可能不支持 float/half RenderTexture。
  - 建议增加格式选择：
    - 支持 `ARGBFloat` 用 `ARGBFloat`
    - 否则支持 `ARGBHalf` 用 `ARGBHalf`
    - 否则回退 `ARGB32`

## P1 - 锥光干涉移动端方案

- [ ] **移动端优先使用 C# / Shader 锥光管线**
  - 可复用现有模块：
    - `ConoscopicTextureRenderer`
    - `ConoscopicJonesGpuCore`
    - `ConoscopicIntensityCalculator`
  - 移动端不应强依赖 native DLL 输出。

- [ ] **抽象晶体物理后端**
  - 后续可引入接口，例如：
    - `NativeCrystalPhysicsBackend`
    - `ManagedCrystalPhysicsBackend`
    - `GpuCrystalPhysicsBackend`
  - 上层 `CrystalPhysicalCore`、锥光、示波器、Vπ 计算只依赖统一后端接口。

- [ ] **移动端降低默认锥光分辨率**
  - Scene2 预览可从 512 降到 256 或按设备性能动态选择。
  - 避免移动端实时 `Graphics.Blit` 和高精度 RenderTexture 带来过高开销。

- [ ] **为低端设备准备静态/低频刷新模式**
  - 锥光纹理不必每帧重算。
  - 仅在晶体旋转、晶体选择、参数变化时刷新。

## P2 - 触控与 UI 适配

- [ ] **替换键鼠依赖**
  - 当前多个交互依赖键盘：
    - `A/D` 移动光学元件
    - `WASD` 微调激光/探测器
    - `Space` 放置
    - `Enter` 锁定
  - 移动端需要对应虚拟按钮、拖拽、滑杆或手势方案。

- [x] **第一版移动端虚拟按键**
  - 新增 `MobileVirtualInput` 作为共享输入桥，保留桌面键盘输入，同时支持移动端虚拟按键注入。
  - 新增 `MobileVirtualControls`，Android/iOS 运行时自动生成 `W/A/S/D`、`Drop(Space)`、`Enter` 屏幕按键。
  - 虚拟按键仅在 `Scene2.The Lab` 和 `Scene4` 场景显示，避免遮挡主菜单和晶体选择流程。
  - D-pad 位于右下角，避免遮挡左下角光屏/锥光显示面板。
  - 已接入：光学元件移动/放置、激光微调/锁定、探测器微调、旋转台、晶体旋转面板、Scene4 示波器调压。
  - Scene4 只需要 A/D 调压，移动端在 Scene4 只显示 A/D 两个按钮，隐藏 W/S/Drop/Enter，并移动到底部左侧空位，避免压住“当前状态”等文字。

- [x] **Scene4 移动端波形区域修正**
  - Scene4 的波形区域是一组联动布局。当前只重排右侧波形组：`WavePanel`、`WaveContentArea`、`CH1Block`、`CH2Block`、`ResultPanel`；不再改 `MainArea`、左侧控制面板或右侧整体面板。
  - 移动端/Editor Android Target 下，波形图挂到各自 `CH1Block` / `CH2Block`，并配合上面的波形组重排，避免只改波形或只改面板造成错位。
  - Scene4 A/D 虚拟键改为上/下箭头，放到电压数值左侧并垂直排列，用于升/降直流偏置电压；当前位置约为 `↑(118,763)`、`↓(118,678)`，尺寸 `92×72`。
  - 新增 `Scene4MobileTextAdapter`，移动端隐藏桌面提示“按 A / D 键调节电压”。

- [x] **第二版移动端虚拟按键位置微调**
  - 虚拟按键从右下角移动到底部中间，避开原本靠右的场景按钮。
  - 在 16:9 安全区域内计算底部偏移，Pad 上出现上下黑边时按键会上移，避免落入黑边。
  - 光学元件移动不再依赖 Unity Axis 映射，改为显式读取 W/S/A/D 和方向键，保持桌面键盘与移动虚拟按键方向一致。

- [x] **第三版移动端虚拟按键位置微调**
  - 按键按用户反馈移到左下角：`W/A/S/D` 在左下，`Drop` 和 `Enter` 在左下底部同排。
  - `Enter` 对应键盘回车，目前主要用于激光微调完成后的锁定/确认（`LaserEmitterMover.isCalibrationDone`）。

- [ ] **适配手机和平板屏幕比例**
  - 检查固定位置 UI，例如底部/左下角面板。
  - 使用 safe area，避免刘海屏、圆角屏、系统导航栏遮挡。

- [x] **移动端 16:9 视口约束**
  - 新增 `MobileAspectRatioEnforcer`，Android/iOS 运行时自动将所有非 RenderTexture 摄像机约束到 16:9 视口。
  - 在 Pad 等非 16:9 屏幕上添加黑边遮罩，避免画面被拉伸或构图变化过大。
  - 黑边低于虚拟按键排序，虚拟按键仍可显示和触控。
  - 第三版曾尝试运行时重包 Canvas 子节点，但会导致开始菜单按钮跑出屏幕，已回滚；后续 UI 比例适配需要逐场景/逐 Canvas 做安全处理，不能全局移动已有 UI 层级。

- [x] **移动端 CanvasScaler 比例适配**
  - 新增 `MobileCanvasScalerAdapter`，在正式移动端页面统一运行：主菜单、介绍、晶体选择、主实验、极值法、示波器、历史、测验、报告、附加实验。
  - 不移动任何已有 UI 层级，只调整根 Canvas 的 `CanvasScaler`。
  - Pad / 4:3 等窄于 16:9 的屏幕使用 `matchWidthOrHeight = 0`，优先保住 1920 设计宽度，减少光屏窗口、极值法界面和面板横向挤压。
  - 宽于 16:9 的手机屏幕使用 `matchWidthOrHeight = 1`，优先保住 1080 设计高度。
  - 第四版补充：每帧持续统一实验场景中运行时新创建的根 Canvas（例如 `WindowsCanvas`、光屏窗口），并为缺少 `CanvasScaler` 的根屏幕 Canvas 自动补齐，避免底层图和上层面板使用不同缩放标准。
  - 第五版补充：适配范围扩展到所有正式页面；测试/可视化场景暂不处理，避免影响开发验证场景。

- [x] **Android 横屏与安全区约束**
  - 禁止 Android 自动旋转到竖屏，只保留横屏方向，避免 Pad/手机切到非实验设计方向后 UI 全面错位。
  - 关闭 `androidRenderOutsideSafeArea`，避免画面最外圈渲染到系统安全区/圆角/导航区域之外。

- [x] **Scene3 极值法移动端专用布局**
  - 新增 `Scene3MobileLayoutAdapter`，仅在移动端 Scene3 运行。
  - 曾尝试把整个 `DataCanvas` 移入 `Scene3MobileViewportRoot`，实测会导致整页压扁并露出 3D 背景，已回滚。
  - 当前保留更安全的左右分栏适配：恢复顶部标题安全区，使用原设计 800/1120 左右比例，并对左右面板做裁剪，避免重挂复杂 UI 层级。
  - 基于 2560×1600 Pad 继续微调：移动端适配可在 Editor Android Build Target 下生效；Scene3 主区域会按 16:10 计算 60px 逻辑上下安全边距，顶部额外保留 100px 标题区；左侧固定 800px，右侧从 830px 开始。
  - 分析结果面板应覆盖左侧仪器区，而不是覆盖右侧表格/图表区；`analysisPanel` 已改为使用左侧布局。

- [x] **Editor 内移动端布局测试**
  - 新增 `MobileRuntime`：真机移动端启用移动端适配；Unity Editor 中如果当前 Build Target 是 Android，也启用移动端适配。
  - 可以在电脑上打开 Unity Game 视图，添加/选择自定义分辨率 `2560×1600`，直接 Play 测试 Pad 布局，不必每次 Build APK。

- [x] **Scene2-preview 返回按钮移动端微调**
  - 新增 `Scene2PreviewMobileLayoutAdapter`，仅在移动端/Editor Android Target 的 Scene2-preview 生效。
  - 自动查找文本为“返回”的按钮，仅调整其位置到左上安全区域，不修改原按钮尺寸和字号；当前位置为 `(48, -22)`。

- [ ] **增大可点击区域**
  - 移动端按钮、旋钮、卡片、关闭按钮等需要满足触控尺寸。
  - 双击交互在移动端可能不稳定，必要时改为长按或显式按钮。

## P3 - 构建与验证

- [ ] **当前阶段验收口径：先验证能运行，不验证完整触控**
  - 触控交互改造完成前，真机端不要求完整完成实验流程。
  - 当前只验证 App 能启动、能切到核心场景、不会因 Windows DLL 或锥光渲染链路崩溃。
  - 实验逻辑和完整流程仍可先在 Unity Editor / Windows 上验证。

- [ ] **配置 Unity 插件平台导入**
  - Windows DLL 只勾选 Windows 平台。
  - Android / iOS 不应尝试导入 `CrystalPhysicsCore.dll`。

- [ ] **建立移动端最低验证清单**
  - Android/iOS 能进入主菜单。
  - 能进入 `Scene2-preview` 并选择晶体。
  - 能进入 `Scene2.The Lab`。
  - 放置光屏后红点显示正常。
  - 放置晶体 + 扩束器后锥光面板不崩溃，能显示可接受图案或降级图案。
  - 示波器场景可进入，波形不因 Vπ / sensitivity 异常中断。

- [x] **Android 导出测试步骤**
  - Unity Hub 左侧进入 `Installs / 安装`，找到 Unity 2022.3.62f2c1，点击该版本右侧齿轮或 `...`，选择 `Add modules`，安装 Android Build Support、Android SDK & NDK Tools、OpenJDK。
  - 如果 Manage 菜单里只有 `Show in Explorer`、`Release notes`、`Remove from Hub`，说明该 Unity 版本多半是手动/离线安装或 Hub 无法追加模块；此时需要重新通过 Hub 安装同版本并勾选 Android 模块，或从 Unity Download Archive 下载完全匹配 `2022.3.62f2c1` 的 Android Build Support 离线模块安装包。
  - 中文版界面对照：`Installs` = `安装`，`Install Editor` = `安装编辑器`，`Add modules` = `添加模块`，`Manage` = `管理`，`Show in Explorer` = `在资源管理器中显示`，`Remove from Hub` = `从 Hub 中移除`。
  - 当前机器已手动补装：
    - Android Build Support：`D:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Data\PlaybackEngines\AndroidPlayer`
    - Android SDK：`...\AndroidPlayer\SDK`
    - Android SDK Build Tools：`34.0.0`
    - Android Platform：`android-35`
    - Android SDK Command-line Tools：`6.0`
    - Android NDK：`23.1.7779620`
    - OpenJDK：`11.0.32`
  - 如果 Unity 报错 `Android SDK command-line tools component is not found. Make sure "Command-line Tools (6.0)" is installed`，说明缺少 `cmdline-tools;6.0`；当前机器已补装到 `...\AndroidPlayer\SDK\cmdline-tools\6.0`，重启 Unity 后再检查。
  - 补装完成后需要关闭并重新打开 Unity Editor，再进入 `File > Build Settings` 检查 Android 是否可切换。
  - 用 Unity 打开 `ElectroOptic-Lab/` 项目目录。
  - `File > Build Settings` 选择 Android，并点击 `Switch Platform`。
  - 在 `Assets/Plugins/x86_64/CrystalPhysicsCore.dll` 的 Inspector 中只保留 Windows/Standalone，取消 Android/iOS 或 Any Platform 对移动端的包含。
  - `Scenes In Build` 至少包含主菜单、晶体选择、主实验场景和示波器场景。
  - `Player Settings` 中设置 Package Name，例如 `com.electrooptics.labmobile`；Minimum API Level 建议 API 26+；Target Architectures 勾选 ARM64；Scripting Backend 建议 IL2CPP。
  - 先执行 `Build` 产出 APK；构建通过后再用真机 `Build And Run` 或手动安装 APK。
  - 当前只验证 App 启动、切场景、核心渲染和不崩溃，不验证完整触控实验流程。

- [ ] **Unity Editor 内当前操作**
  - `File > Build Settings` 选择 Android，点击 `Switch Platform`，等待资源重新导入完成。
  - 检查 `Assets/Plugins/x86_64/CrystalPhysicsCore.dll`，确保 Android/iOS 未勾选。
  - 检查 `Scenes In Build` 至少包含 `Scene0.Open Menu`、`Scene2-preview`、`Scene2.The Lab`、`Scene4_UIRebuild 1`。
  - `Player Settings` 设置 `Company Name = ElectroOptics`、`Product Name = ElectroOpticLabMobile`、`Package Name = com.electrooptics.labmobile`。
  - 如果没看到 `Package Name`，在 Android `Other Settings / 其他设置 > Identification / 标识` 中找 `Application Identifier / 应用程序标识符`；如果有 `Override Default Package Name`，先勾选它再填写。
  - Android `Other Settings` 不在 Build Settings 主窗口里：进入 `File > Build Settings`，左侧选 Android，点左下 `Player Settings...`，在 `Project Settings > Player` 顶部选择 Android 小机器人图标，再展开 `Other Settings / 其他设置`。
  - Android `Other Settings / 其他设置` 设置 `Scripting Backend = IL2CPP`、`Target Architectures` 只勾 `ARM64`（取消 `ARMv7`）、`Minimum API Level >= Android 8.0/API 26`、`Target API Level = Automatic` 或 Android 35。
  - 如果 `ARM64` 是灰色不可勾，先把 `Configuration > Scripting Backend` 从 `Mono` 改为 `IL2CPP`；Unity Android 的 ARM64 需要 IL2CPP。
  - 先执行 `Build` 输出 APK，不急着 `Build And Run`；如果失败，优先看 Console 第一条红色错误。
  - 点击 `Build` 后建议输出到 `D:\Projects\Github\MyRepo\ElectroOptic-Lab-Unity\Builds\Android\ElectroOpticLabMobile.apk`；如果 Unity 只让选择文件夹，则选择 `Builds\Android`。
  - 第一次 Android/IL2CPP 构建可能需要数分钟到十几分钟；构建成功后再安装真机，只验证启动、切场景、核心渲染和不崩溃。
  - APK 可直接通过微信/QQ/网盘/数据线传到安卓设备安装；首次安装需要允许对应 App 或文件管理器“安装未知来源应用”。
  - 安装后只检查 App 能启动、能进主菜单、能进入晶体选择、能进入实验场景、不闪退、不黑屏。

- [ ] **记录移动端精度差异**
  - managed fallback 只用于跑通，不代表和 DLL 完全一致。
  - 后续如果需要教学/论文级一致性，再考虑编译 Android `.so` 和 iOS `.a` / `.framework`。
