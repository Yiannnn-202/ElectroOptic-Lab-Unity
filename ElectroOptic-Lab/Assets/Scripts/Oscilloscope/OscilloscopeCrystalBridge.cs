using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// CrystalPhysicalCore 包装器（示波器专用）
    /// 遵循 CrystalControllerWrapper 的解耦模式：
    /// 持有 Core 引用 → 构建 CrystalConfig → 调用 ApplyConfig → 读取 Sensitivity
    /// </summary>
    public class OscilloscopeCrystalBridge : MonoBehaviour
    {
        #region 私有字段

        private CrystalPhysicalCore _core;
        private CrystalProfile _profile;
        private bool _isInitialized;

        // 缓存上次配置状态，用于判断是否需要重新调用 DLL
        private ElectricFieldAxis _lastAxis;
        private ModulationMode _lastMode;
        private CrystalProfile _lastProfile;
        private bool _hasConfigured;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前缓存的有效电光灵敏度 (1/V)
        /// </summary>
        public float Sensitivity => _core != null ? _core.Sensitivity : 0f;

        /// <summary>
        /// 当前晶体 Profile
        /// </summary>
        public CrystalProfile Profile => _profile;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化 Bridge，绑定 CrystalPhysicalCore 引用
        /// </summary>
        public void Initialize(CrystalPhysicalCore core)
        {
            _core = core;
            _isInitialized = core != null;
            _hasConfigured = false;

            if (_isInitialized)
                Debug.Log("[OscilloscopeCrystalBridge] 初始化完成");
            else
                Debug.LogWarning("[OscilloscopeCrystalBridge] 初始化失败: core 为 null");
        }

        /// <summary>
        /// 设置晶体 Profile
        /// </summary>
        public void SetProfile(CrystalProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("[OscilloscopeCrystalBridge] SetProfile: profile 为 null");
                return;
            }

            _profile = profile;
            // Profile 变更后需要重新配置
            _hasConfigured = false;
            Debug.Log($"[OscilloscopeCrystalBridge] 设置 Profile: {profile.crystalName}");
        }

        #endregion

        #region 核心方法

        /// <summary>
        /// 判断是否需要重新调用 DLL（axis/mode/profile 是否变化）
        /// </summary>
        public bool NeedsReconfigure(ElectricFieldAxis axis, ModulationMode mode)
        {
            if (!_hasConfigured) return true;
            return axis != _lastAxis || mode != _lastMode || _profile != _lastProfile;
        }

        /// <summary>
        /// 配置 CrystalPhysicalCore 并返回计算得到的 Sensitivity
        /// 构建 CrystalConfig → 调用 ApplyConfig(触发 DLL) → 读取 Sensitivity
        /// </summary>
        public float ConfigureAndGetSensitivity(ElectricFieldAxis axis, ModulationMode mode)
        {
            if (!_isInitialized || _profile == null)
            {
                Debug.LogWarning("[OscilloscopeCrystalBridge] 未就绪，无法配置");
                return 0f;
            }

            Vector3 axisVec = AxisToVector(axis);

            var config = new CrystalConfig
            {
                profile = _profile,
                crystalRotation = Quaternion.identity,  // 固定 (0°, 0°, 0°)
                localEField = axisVec,                  // Render Pass 需要（输出不使用）
                probeFieldDirection = axisVec,           // 驱动 Probe Pass 计算 Sensitivity
                worldLightDirection = Vector3.forward    // 光沿 +Z 传播
            };

            _core.ApplyConfig(config);

            // 缓存状态
            _lastAxis = axis;
            _lastMode = mode;
            _lastProfile = _profile;
            _hasConfigured = true;

            return _core.Sensitivity;
        }

        #endregion

        #region 工具方法

        private static Vector3 AxisToVector(ElectricFieldAxis axis)
        {
            switch (axis)
            {
                case ElectricFieldAxis.X_Axis: return Vector3.right;
                case ElectricFieldAxis.Y_Axis: return Vector3.up;
                case ElectricFieldAxis.Z_Axis: return Vector3.forward;
                default: return Vector3.forward;
            }
        }

        #endregion
    }
}
