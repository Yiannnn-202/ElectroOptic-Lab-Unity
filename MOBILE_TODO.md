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

- [ ] **适配手机和平板屏幕比例**
  - 检查固定位置 UI，例如底部/左下角面板。
  - 使用 safe area，避免刘海屏、圆角屏、系统导航栏遮挡。

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
    - Android NDK：`23.1.7779620`
    - OpenJDK：`11.0.32`
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
  - Android `Other Settings` 设置 `Scripting Backend = IL2CPP`、`Target Architectures = ARM64`、`Minimum API Level >= Android 8.0/API 26`、`Target API Level = Automatic` 或 Android 35。
  - 先执行 `Build` 输出 APK，不急着 `Build And Run`；如果失败，优先看 Console 第一条红色错误。

- [ ] **记录移动端精度差异**
  - managed fallback 只用于跑通，不代表和 DLL 完全一致。
  - 后续如果需要教学/论文级一致性，再考虑编译 Android `.so` 和 iOS `.a` / `.framework`。
