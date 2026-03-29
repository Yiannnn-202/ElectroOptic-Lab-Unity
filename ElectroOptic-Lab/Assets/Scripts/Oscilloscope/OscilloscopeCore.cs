using System;
using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// 示波器波形计算核心 — 顶层编排器
    /// 组合 Bridge(DLL交互)、VpiCalculator(Vπ计算)、WaveformCalculator(波形数学)
    /// 通过 dirty flag 优化，仅在参数变化时重算
    /// </summary>
    public class OscilloscopeCore : MonoBehaviour
    {
        #region Inspector 配置

        [SerializeField] private OscilloscopeParameters _parameters = new OscilloscopeParameters();

        #endregion

        #region 依赖

        private OscilloscopeCrystalBridge _bridge;

        #endregion

        #region 输出

        /// <summary>
        /// 波形计算结果（Ch1: AC电压, Ch2: 光强, Vπ, Γ₀）
        /// </summary>
        public WaveformResult Result { get; private set; }

        /// <summary>
        /// 当前参数（只读访问）
        /// </summary>
        public OscilloscopeParameters Parameters => _parameters;

        /// <summary>
        /// 计算核心是否就绪
        /// </summary>
        public bool IsReady => _bridge != null && _bridge.IsInitialized && _bridge.Profile != null;

        /// <summary>
        /// 波形更新事件，UI 订阅此事件刷新显示
        /// </summary>
        public event Action OnWaveformUpdated;

        #endregion

        #region 内部状态

        private WaveformCalculator _calculator = new WaveformCalculator();
        private bool _isDirty = true;

        #endregion

        #region 初始化

        /// <summary>
        /// 由场景调度脚本调用，注入物理核心和晶体 Profile
        /// </summary>
        public void Initialize(CrystalPhysicalCore core, CrystalProfile profile)
        {
            // 获取或添加 Bridge 组件
            _bridge = GetComponent<OscilloscopeCrystalBridge>();
            if (_bridge == null)
                _bridge = gameObject.AddComponent<OscilloscopeCrystalBridge>();

            _bridge.Initialize(core);
            _bridge.SetProfile(profile);

            Result = new WaveformResult();
            _isDirty = true;

            Debug.Log($"[OscilloscopeCore] 初始化完成, 晶体: {profile.crystalName}");
        }

        #endregion

        #region 参数设置（每个 setter 标记 dirty）

        public void SetVoltages(float vDC, float vModulation)
        {
            _parameters.vDC = vDC;
            _parameters.vModulation = vModulation;
            _isDirty = true;
        }

        public void SetFrequency(float frequency)
        {
            _parameters.frequency = frequency;
            _isDirty = true;
        }

        public void SetModulationConfig(ModulationMode mode, ElectricFieldAxis axis)
        {
            _parameters.modulationMode = mode;
            _parameters.fieldAxis = axis;
            _isDirty = true;
        }

        public void SetCompensatorPhase(float phase)
        {
            _parameters.compensatorPhase = phase;
            _isDirty = true;
        }

        public void SetIntensityMax(float i0)
        {
            _parameters.intensityMax = i0;
            _isDirty = true;
        }

        public void SetDisplayConfig(int sampleCount, float displayPeriods)
        {
            _parameters.sampleCount = sampleCount;
            _parameters.displayPeriods = displayPeriods;
            _isDirty = true;
        }

        /// <summary>
        /// 强制下一帧重算（Profile 变更等场景使用）
        /// </summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }

        #endregion

        #region Unity 生命周期

        private void Update()
        {
            if (!IsReady || !_isDirty) return;

            Recalculate();
            _isDirty = false;
        }

        #endregion

        #region 核心计算流水线

        private void Recalculate()
        {
            var p = _parameters;
            var profile = _bridge.Profile;

            // Step 1: 通过 Bridge 获取 Sensitivity（仅 axis/mode/profile 变化时触发 DLL）
            float sensitivity;
            if (_bridge.NeedsReconfigure(p.fieldAxis, p.modulationMode))
                sensitivity = _bridge.ConfigureAndGetSensitivity(p.fieldAxis, p.modulationMode);
            else
                sensitivity = _bridge.Sensitivity;

            // Step 2: 计算 Vπ
            double vPi = VpiCalculator.Calculate(
                profile.defaultWavelength_nm,
                profile.defaultLength_mm,
                profile.defaultThickness_mm,
                sensitivity,
                p.modulationMode);

            // Step 3: 计算波形
            _calculator.Compute(p, vPi, Result);

            // Step 4: 通知订阅者
            OnWaveformUpdated?.Invoke();
        }

        #endregion

        #region 编辑器调试

#if UNITY_EDITOR
        [ContextMenu("打印当前状态")]
        private void DebugPrintStatus()
        {
            Debug.Log($"[OscilloscopeCore] 状态报告:\n" +
                      $"  - IsReady: {IsReady}\n" +
                      $"  - IsDirty: {_isDirty}\n" +
                      $"  - V_DC: {_parameters.vDC:F2} V\n" +
                      $"  - V_m: {_parameters.vModulation:F2} V\n" +
                      $"  - Frequency: {_parameters.frequency:F1} Hz\n" +
                      $"  - Mode: {_parameters.modulationMode}\n" +
                      $"  - FieldAxis: {_parameters.fieldAxis}\n" +
                      $"  - Vπ: {(Result != null ? Result.vPi.ToString("F2") : "N/A")} V\n" +
                      $"  - Γ₀: {(Result != null ? Result.gamma0.ToString("F4") : "N/A")} rad");
        }

        [ContextMenu("强制重算")]
        private void DebugForceRecalculate()
        {
            MarkDirty();
        }
#endif

        #endregion
    }
}
