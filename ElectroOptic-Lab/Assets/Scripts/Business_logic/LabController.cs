using UnityEngine;
using UnityEngine.UI; // 用于 Slider
using TMPro;          // 用于 TMP 组件
using ElectroOptics;

public class LabController : MonoBehaviour
{
    [Header("Core References")]
    public CrystalPhysicalCore core;
    public CrystalProfile defaultProfile;

    [Header("UI Controls")]
    public TMP_Dropdown dropPropAxis;   // 通光方向
    public TMP_Dropdown dropFieldAxis;  // 电场方向
    public TMP_Dropdown dropModMode;    // 调制模式
    public Slider sliderVoltage;        // 电压滑条
    public TextMeshProUGUI txtVoltageValue; // 电压数值
    public TMP_InputField inputLength;  // 长度
    public TMP_InputField inputThickness; // 厚度

    // 内部持有的当前配置 (Struct)
    private CrystalConfig _currentConfig;

    // 防止回调死循环的标志位
    private bool _isUpdatingUI = false;

    void Start()
    {
        if (defaultProfile == null)
        {
            Debug.LogError("[LabController] Default Profile is missing!");
            return;
        }

        _currentConfig = new CrystalConfig();
        _currentConfig.profile = defaultProfile;

        // 读取 Profile 的默认尺寸
        _currentConfig.length_mm = defaultProfile.length_mm;
        _currentConfig.thickness_mm = defaultProfile.thickness_mm;
        _currentConfig.wavelength_nm = 633.0;

        // 绑定 UI 事件
        dropPropAxis.onValueChanged.AddListener(OnPropAxisChanged);
        dropFieldAxis.onValueChanged.AddListener(OnFieldAxisChanged);
        dropModMode.onValueChanged.AddListener(OnModModeChanged);
        sliderVoltage.onValueChanged.AddListener(OnVoltageChanged);

        inputLength.onEndEdit.AddListener((val) => { ParseDimensions(); DispatchConfig(); });
        inputThickness.onEndEdit.AddListener((val) => { ParseDimensions(); DispatchConfig(); });

        // 初始化 UI 值
        sliderVoltage.value = 0;
        inputLength.text = _currentConfig.length_mm.ToString();
        inputThickness.text = _currentConfig.thickness_mm.ToString();

        // 默认初始化为横向模式，并应用默认轴向
        _currentConfig.mode = ModulationMode.Transverse;
        ApplyDefaultAxes(ModulationMode.Transverse);

        DispatchConfig();
    }

    // --- UI 事件回调 ---

    void OnVoltageChanged(float value)
    {
        if (_isUpdatingUI) return;
        _currentConfig.voltage = value;
        if (txtVoltageValue) txtVoltageValue.text = $"{value:F0} V";
        DispatchConfig();
    }

    void OnPropAxisChanged(int value)
    {
        if (_isUpdatingUI) return;
        _currentConfig.propAxis = (PropagationAxis)value;

        // 光路改变是冲突的主要来源，必须检查互斥
        EnforceMutexLogic();
        DispatchConfig();
    }

    void OnFieldAxisChanged(int value)
    {
        if (_isUpdatingUI) return;
        _currentConfig.fieldAxis = (ElectricFieldAxis)value;

        // 通常用户改变电场时，我们尽量尊重用户选择
        // 但如果在纵向模式下用户强行改（理论上UI被锁了改不了），还是检查一下为好
        if (_currentConfig.mode == ModulationMode.Longitudinal)
        {
            EnforceMutexLogic();
        }
        DispatchConfig();
    }

    void OnModModeChanged(int value)
    {
        if (_isUpdatingUI) return;
        ModulationMode newMode = (ModulationMode)value;
        _currentConfig.mode = newMode;

        // 1. 预设策略：切模式时，先把轴向重置到最常用的状态
        ApplyDefaultAxes(newMode);

        // 2. 互斥检查：确保万无一失
        EnforceMutexLogic();

        DispatchConfig();
    }

    // --- 核心逻辑 ---

    /// <summary>
    /// [预设策略] 根据模式自动归位到经典配置
    /// </summary>
    private void ApplyDefaultAxes(ModulationMode mode)
    {
        _isUpdatingUI = true; // 暂停回调，防止改 Dropdown 时触发死循环

        if (mode == ModulationMode.Longitudinal)
        {
            _currentConfig.propAxis = PropagationAxis.Z_Axis;
            _currentConfig.fieldAxis = ElectricFieldAxis.Z_Axis;
        }
        else
        {
            _currentConfig.propAxis = PropagationAxis.Y_Axis;
            _currentConfig.fieldAxis = ElectricFieldAxis.Z_Axis;
        }

        // 同步 UI
        if (dropPropAxis) dropPropAxis.value = (int)_currentConfig.propAxis;
        if (dropFieldAxis) dropFieldAxis.value = (int)_currentConfig.fieldAxis;

        _isUpdatingUI = false; // 恢复回调
    }

    /// <summary>
    /// [互斥逻辑] 强制执行物理约束 (E平行k 或 E垂直k)
    /// </summary>
    private void EnforceMutexLogic()
    {
        int propIndex = (int)_currentConfig.propAxis;
        int fieldIndex = (int)_currentConfig.fieldAxis;

        if (_currentConfig.mode == ModulationMode.Longitudinal)
        {
            // === 纵向模式：必须平行 (E // k) ===

            // 1. 锁定电场下拉菜单
            if (dropFieldAxis) dropFieldAxis.interactable = false;

            // 2. 强制对齐
            if (fieldIndex != propIndex)
            {
                _isUpdatingUI = true; // 避免触发 OnFieldAxisChanged 导致二次 Dispatch

                _currentConfig.fieldAxis = (ElectricFieldAxis)propIndex;
                if (dropFieldAxis) dropFieldAxis.value = propIndex;

                _isUpdatingUI = false;
            }
        }
        else
        {
            // === 横向模式：必须垂直 (E ⊥ k) ===

            // 1. 解锁电场下拉菜单
            if (dropFieldAxis) dropFieldAxis.interactable = true;

            // 2. 冲突检测：如果重合了 (Prop == Field)
            if (fieldIndex == propIndex)
            {
                // 策略：自动躲避。顺延到下一个轴 (0->1, 1->2, 2->0)
                int newFieldIndex = (propIndex + 1) % 3;

                _isUpdatingUI = true;

                _currentConfig.fieldAxis = (ElectricFieldAxis)newFieldIndex;
                if (dropFieldAxis) dropFieldAxis.value = newFieldIndex;

                _isUpdatingUI = false;
            }
        }
    }

    private void ParseDimensions()
    {
        if (double.TryParse(inputLength.text, out double l)) _currentConfig.length_mm = l;
        if (double.TryParse(inputThickness.text, out double d)) _currentConfig.thickness_mm = d;
    }

    private void DispatchConfig()
    {
        if (core != null)
        {
            core.ApplyConfig(_currentConfig);
        }
    }
}