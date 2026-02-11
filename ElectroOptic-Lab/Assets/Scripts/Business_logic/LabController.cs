using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ElectroOptics;

public class LabController : MonoBehaviour
{
    [Header("Core References")]
    public CrystalPhysicalCore core;
    public CrystalProfile defaultProfile;

    [Header("Experiment Settings")]
    public PropagationAxis defaultPropAxis = PropagationAxis.Z_Axis;

    [Header("UI Controls")]
    public TMP_Dropdown dropPropAxis;   // 【修复】补回缺失的下拉菜单引用
    public TMP_Dropdown dropModMode;    // 横向/纵向
    public TMP_Dropdown dropFieldAxis;  // 电场方向 (X/Y/Z)
    public Slider sliderVoltage;
    public TextMeshProUGUI txtVoltageValue;
    public TextMeshProUGUI txtVpiValue;
    public TMP_InputField inputLength;
    public TMP_InputField inputThickness;

    [Header("Crystal Orientation")]
    public Vector3 crystalEulerAngles = Vector3.zero;

    [Header("Debug / Direct Control")]
    [SerializeField] private float _length_mm = 20.0f;
    [SerializeField] private float _thickness_mm = 1.0f;
    [SerializeField] private float _voltage = 0.0f;

    // 内部状态
    private CrystalConfig _config;
    private ModulationMode _modMode = ModulationMode.Transverse;
    private ElectricFieldAxis _fieldAxis = ElectricFieldAxis.Z_Axis;

    void Start()
    {
        // 1. 核心安全检查
        if (defaultProfile == null)
        {
            Debug.LogError("[LabController] Default Profile 未赋值！");
            return;
        }
        if (core == null)
        {
            Debug.LogError("[LabController] Core 未赋值！");
            return;
        }

        // 2. 初始化 Config
        _config = new CrystalConfig();
        _config.profile = defaultProfile;
        _config.worldLightDirection = new Vector3(0, 0, 1); // 默认光沿 Z 轴

        // 3. 读取默认尺寸
        _length_mm = (float)defaultProfile.defaultLength_mm;
        _thickness_mm = (float)defaultProfile.defaultThickness_mm;

        // 4. 安全地初始化 UI (仅当 UI 组件存在时才操作)
        if (inputLength != null) inputLength.text = _length_mm.ToString();
        if (inputThickness != null) inputThickness.text = _thickness_mm.ToString();
        if (sliderVoltage != null) sliderVoltage.value = _voltage;

        // 【修复】初始化通光轴下拉菜单
        if (dropPropAxis != null)
        {
            dropPropAxis.value = (int)defaultPropAxis; // 设置初始 UI 状态
            // 立即应用一次初始轴向的旋转逻辑
            OnPropAxisChanged((int)defaultPropAxis);
        }

        // 5. 绑定事件
        if (dropPropAxis != null) dropPropAxis.onValueChanged.AddListener(OnPropAxisChanged); // 【修复】绑定事件
        if (dropModMode != null) dropModMode.onValueChanged.AddListener(OnModModeChanged);
        if (dropFieldAxis != null) dropFieldAxis.onValueChanged.AddListener(OnFieldAxisChanged);
        if (sliderVoltage != null) sliderVoltage.onValueChanged.AddListener(OnVoltageChanged);
        if (inputLength != null) inputLength.onEndEdit.AddListener(OnDimChanged);
        if (inputThickness != null) inputThickness.onEndEdit.AddListener(OnDimChanged);

        // 6. 初始物理推送
        UpdateAndDispatch();
    }

    void Update()
    {
        if (core != null)
        {
            UpdateVpiDisplay(core.Sensitivity);
        }
    }

    // --- UI Callbacks ---

    // 【修复】新增：通光轴改变时的逻辑
    void OnPropAxisChanged(int val)
    {
        PropagationAxis axis = (PropagationAxis)val;

        // 简单的硬编码映射：根据选择的轴，旋转晶体使其对准世界 Z 轴
        switch (axis)
        {
            case PropagationAxis.X_Axis:
                // 将晶体 X 轴转到世界 Z 轴 (绕 Y 轴转 90 度)
                crystalEulerAngles = new Vector3(0, 90, 0);
                break;
            case PropagationAxis.Y_Axis:
                // 将晶体 Y 轴转到世界 Z 轴 (绕 X 轴转 90 度)
                crystalEulerAngles = new Vector3(90, 0, 0);
                break;
            case PropagationAxis.Z_Axis:
            default:
                // 默认状态 (无旋转)
                crystalEulerAngles = Vector3.zero;
                break;
        }

        // 立即更新物理状态
        UpdateAndDispatch();
    }

    void OnVoltageChanged(float val)
    {
        _voltage = val;
        if (txtVoltageValue) txtVoltageValue.text = $"{val:F0} V";
        UpdateAndDispatch();
    }

    void OnModModeChanged(int val)
    {
        _modMode = (ModulationMode)val;
        UpdateAndDispatch();
    }

    void OnFieldAxisChanged(int val)
    {
        _fieldAxis = (ElectricFieldAxis)val;
        UpdateAndDispatch();
    }

    void OnDimChanged(string val)
    {
        double tempL, tempD;
        if (inputLength != null && double.TryParse(inputLength.text, out tempL)) _length_mm = (float)tempL;
        if (inputThickness != null && double.TryParse(inputThickness.text, out tempD)) _thickness_mm = (float)tempD;
        UpdateAndDispatch();
    }

    // --- 核心逻辑：组装 Config ---

    private void UpdateAndDispatch()
    {
        if (core == null) return;

        // 1. 设置晶体姿态 (来自 UI 回调修改后的 crystalEulerAngles)
        _config.crystalRotation = Quaternion.Euler(crystalEulerAngles);

        // 2. 计算几何因子 (V -> E_scalar)
        double e_scalar = 0.0;
        if (_modMode == ModulationMode.Transverse)
            e_scalar = _voltage / (_thickness_mm * 1e-3);
        else
            e_scalar = _voltage / (_length_mm * 1e-3);

        // 3. 构造电场矢量
        Vector3 axisVec = GetAxisVector(_fieldAxis);
        _config.localEField = axisVec * (float)e_scalar;

        // 4. 设置探测方向
        _config.probeFieldDirection = axisVec;

        // 5. 发送
        core.ApplyConfig(_config);
    }

    // --- 辅助逻辑 ---

    private void UpdateVpiDisplay(float sensitivity)
    {
        if (txtVpiValue == null) return;

        if (sensitivity < 1e-20f)
        {
            txtVpiValue.text = "Vπ: N/A"; // 灵敏度为0时无法计算 V_pi
            return;
        }

        double lambda = _config.profile != null ? _config.profile.defaultWavelength_nm * 1e-9 : 633e-9;
        double L = _length_mm * 1e-3;
        double d = _thickness_mm * 1e-3;
        double v_pi = 0.0;

        if (_modMode == ModulationMode.Transverse)
        {
            v_pi = (lambda * d) / (2.0 * L * sensitivity);
        }
        else
        {
            v_pi = lambda / (2.0 * sensitivity);
        }

        txtVpiValue.text = $"Vπ: {v_pi:F0} V";
    }

    private Vector3 GetAxisVector(ElectricFieldAxis axis)
    {
        switch (axis)
        {
            case ElectricFieldAxis.X_Axis: return Vector3.right;
            case ElectricFieldAxis.Y_Axis: return Vector3.up;
            case ElectricFieldAxis.Z_Axis: return Vector3.forward;
            default: return Vector3.forward;
        }
    }
}