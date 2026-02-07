using UnityEngine;
using UnityEngine.UI; // 必须引用，用于 Dropdown 和 Slider
using ElectroOptics;  // 引用我们的命名空间

public class LabController : MonoBehaviour
{
    [Header("Core References")]
    public CrystalPhysicalCore core;           // 物理核心
    public CrystalProfile defaultProfile;      // 默认晶体数据

    [Header("UI Controls")]
    public Dropdown dropPropAxis;   // 通光方向
    public Dropdown dropFieldAxis;  // 电场方向
    public Dropdown dropModMode;    // 调制模式
    public Slider sliderVoltage;    // 电压滑条
    public Text txtVoltageValue;    // 电压数值显示
    public InputField inputLength;  // 长度输入
    public InputField inputThickness; // 厚度输入

    // 内部持有的当前配置 (Struct)
    private CrystalConfig _currentConfig;

    void Start()
    {
        // 1. 初始化配置数据
        // 这里的 profile 必须非空，否则物理核心不工作
        if (defaultProfile == null)
        {
            Debug.LogError("[LabController] Default Profile is missing!");
            return;
        }

        _currentConfig = new CrystalConfig();
        _currentConfig.profile = defaultProfile;

        // 读取 Profile 的默认尺寸作为初始值
        _currentConfig.length_mm = defaultProfile.length_mm;
        _currentConfig.thickness_mm = defaultProfile.thickness_mm;
        _currentConfig.wavelength_nm = 633.0; // 默认 633nm 红光

        // 2. 绑定 UI 事件监听
        // 注意：AddListener 会在值改变时触发对应的函数
        dropPropAxis.onValueChanged.AddListener(OnPropAxisChanged);
        dropFieldAxis.onValueChanged.AddListener(OnFieldAxisChanged);
        dropModMode.onValueChanged.AddListener(OnModModeChanged);
        sliderVoltage.onValueChanged.AddListener(OnVoltageChanged);

        // 监听输入框 (结束编辑时触发)
        inputLength.onEndEdit.AddListener((val) => { ParseDimensions(); DispatchConfig(); });
        inputThickness.onEndEdit.AddListener((val) => { ParseDimensions(); DispatchConfig(); });

        // 3. 初始化 UI 控件的显示值
        sliderVoltage.value = 0;
        inputLength.text = _currentConfig.length_mm.ToString();
        inputThickness.text = _currentConfig.thickness_mm.ToString();

        // 4. 应用默认的轴向策略 (确保初始状态也是合理的)
        // 默认设置为横向调制 (Transverse) -> Y轴通光，Z轴电场
        _currentConfig.mode = ModulationMode.Transverse;
        ApplyDefaultAxes(ModulationMode.Transverse);

        // 5. 首次发送完整配置
        DispatchConfig();
    }

    // --- UI 事件回调 (Event Callbacks) ---

    void OnVoltageChanged(float value)
    {
        _currentConfig.voltage = value;
        if (txtVoltageValue) txtVoltageValue.text = $"{value:F0} V";

        // 电压是高频热更新，直接发送
        DispatchConfig();
    }

    void OnPropAxisChanged(int value)
    {
        _currentConfig.propAxis = (PropagationAxis)value;

        // 如果当前是纵向模式，改变光路必须同步改变电场
        EnforceMutexLogic();
        DispatchConfig();
    }

    void OnFieldAxisChanged(int value)
    {
        _currentConfig.fieldAxis = (ElectricFieldAxis)value;
        DispatchConfig();
    }

    /// <summary>
    /// 当调制模式改变时触发
    /// 包含：预设策略 + 互斥逻辑
    /// </summary>
    void OnModModeChanged(int value)
    {
        ModulationMode newMode = (ModulationMode)value;
        _currentConfig.mode = newMode;

        // Step 1: 【预设策略】自动归位
        // 切换模式时，自动把轴向设置成最常用的物理配置
        ApplyDefaultAxes(newMode);

        // Step 2: 【互斥逻辑】强制锁定
        // 确保数据绝对合法 (主要是纵向模式下的平行约束)
        EnforceMutexLogic();

        // Step 3: 发送
        DispatchConfig();
    }

    // --- 核心逻辑方法 ---

    /// <summary>
    /// [预设策略] 根据调制模式，自动设置默认的通光和电场方向
    /// </summary>
    private void ApplyDefaultAxes(ModulationMode mode)
    {
        if (mode == ModulationMode.Longitudinal)
        {
            // 纵向默认场景 (如 KDP): 光沿 Z，电沿 Z
            _currentConfig.propAxis = PropagationAxis.Z_Axis;
            _currentConfig.fieldAxis = ElectricFieldAxis.Z_Axis;
        }
        else
        {
            // 横向默认场景 (如 LiNbO3): 光沿 Y，电沿 Z
            _currentConfig.propAxis = PropagationAxis.Y_Axis;
            _currentConfig.fieldAxis = ElectricFieldAxis.Z_Axis;
        }

        // 同步更新 UI 下拉菜单的显示 (Visual Sync)
        // 注意：这里修改 .value 可能会触发 OnValueChanged 回调，
        // 但由于我们已经设置了 _currentConfig，重复触发也是安全的（幂等操作）。
        if (dropPropAxis) dropPropAxis.value = (int)_currentConfig.propAxis;
        if (dropFieldAxis) dropFieldAxis.value = (int)_currentConfig.fieldAxis;
    }

    /// <summary>
    /// [互斥逻辑] 确保物理约束不被打破
    /// </summary>
    private void EnforceMutexLogic()
    {
        // 规则：纵向调制 (Longitudinal) 要求 电场 平行于 光路
        if (_currentConfig.mode == ModulationMode.Longitudinal)
        {
            // 1. 强制数据对齐 (以光路为准)
            _currentConfig.fieldAxis = (ElectricFieldAxis)_currentConfig.propAxis;

            // 2. UI 锁定与同步
            if (dropFieldAxis)
            {
                dropFieldAxis.value = (int)_currentConfig.propAxis;
                dropFieldAxis.interactable = false; // 禁用下拉，防止用户误操作
            }
        }
        else
        {
            // 横向调制：解锁下拉，允许用户自由选择
            if (dropFieldAxis) dropFieldAxis.interactable = true;
        }
    }

    private void ParseDimensions()
    {
        // 从输入框解析 float
        if (double.TryParse(inputLength.text, out double l)) _currentConfig.length_mm = l;
        if (double.TryParse(inputThickness.text, out double d)) _currentConfig.thickness_mm = d;
    }

    private void DispatchConfig()
    {
        if (core != null)
        {
            // 将合法的数据包发送给物理核心
            core.ApplyConfig(_currentConfig);
        }
    }
}