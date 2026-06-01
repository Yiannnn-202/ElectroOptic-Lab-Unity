using UnityEngine;
using ElectroOptics;
using ElectroOptics.DataTransfer;
using ElectroOptics.Oscilloscope;

/// <summary>
/// Scene3_UIRebuild 晶体数据桥接
/// 挂载到 GameManager 上，从 CrystalSelectionData 读取选中的晶体 Profile，
/// 通过 CrystalPhysicalCore + DLL 计算 Vπ，注入到 RecordManager.halfWaveVoltage
/// </summary>
[RequireComponent(typeof(RecordManager))]
public class Scene3CrystalBridge : MonoBehaviour
{
    [Header("Fallback Profile")]
    [Tooltip("未在 Scene2-preview 选择晶体时使用的默认 Profile")]
    [SerializeField] private CrystalProfile fallbackProfile;

    [Header("调制配置")]
    [Tooltip("电场方向")]
    [SerializeField] private ElectricFieldAxis fieldAxis = ElectricFieldAxis.Z_Axis;

    [Tooltip("光传播方向（必须垂直于电场方向才能产生横向电光效应）")]
    [SerializeField] private Vector3 worldLightDirection = Vector3.up;

    [Tooltip("调制模式")]
    [SerializeField] private ModulationMode modulationMode = ModulationMode.Transverse;

    private CrystalPhysicalCore _core;
    private RecordManager _recordManager;

    private void Awake()
    {
        _recordManager = GetComponent<RecordManager>();
    }

    private void Start()
    {
        CrystalProfile profile = CrystalSelectionData.HasSelection
            ? CrystalSelectionData.SelectedProfile
            : fallbackProfile;

        if (profile == null)
        {
            Debug.LogWarning("[Bridge] No crystal profile found. halfWaveVoltage will keep its default value.");
            return;
        }

        // 创建物理核心
        _core = gameObject.AddComponent<CrystalPhysicalCore>();

        // ======== 晶体参数打印 ========
        Debug.Log("[Bridge] ======== Crystal Vpi Calculation ========");
        Debug.Log($"[Bridge] Crystal: {profile.crystalName}");
        Debug.Log($"[Bridge] Wavelength: {profile.defaultWavelength_nm} nm");
        Debug.Log($"[Bridge] Dimensions: L={profile.defaultLength_mm} mm, d={profile.defaultThickness_mm} mm");
        Debug.Log($"[Bridge] Refractive indices: nx={profile.n_x}, ny={profile.n_y}, nz={profile.n_z}");
        Debug.Log($"[Bridge] E-field axis: {fieldAxis}, Light direction: {worldLightDirection}, Mode: {modulationMode}");

        // 计算灵敏度
        float sensitivity = ComputeSensitivity(profile);
        Debug.Log($"[Bridge] DLL Sensitivity S_eff = {sensitivity:E6} (1/V)");

        if (Mathf.Approximately(sensitivity, 0f))
        {
            Debug.LogWarning("[Bridge] WARNING: S_eff ~= 0! E and k may be parallel, no transverse EO effect. Check light direction vs E-field.");
        }

        // 计算 Vpi
        double vPi = VpiCalculator.Calculate(
            profile.defaultWavelength_nm,
            profile.defaultLength_mm,
            profile.defaultThickness_mm,
            sensitivity,
            modulationMode);

        if (double.IsInfinity(vPi) || double.IsNaN(vPi) || vPi <= 0)
        {
            Debug.LogWarning($"[Bridge] Vpi invalid (S_eff={sensitivity:E6}). Keeping default: {_recordManager.halfWaveVoltage:F1} V");
            Debug.Log("[Bridge] ========================================");
            return;
        }

        _recordManager.halfWaveVoltage = (float)vPi;
        Debug.Log($"[Bridge] Calculated Vpi = {vPi:F2} V");
        Debug.Log($"[Bridge] Written to RecordManager.halfWaveVoltage = {vPi:F2} V");
        Debug.Log("[Bridge] ========================================");
    }

    private float ComputeSensitivity(CrystalProfile profile)
    {
        Vector3 axisVec = AxisToVector(fieldAxis);

        var config = new CrystalConfig
        {
            profile = profile,
            crystalRotation = Quaternion.identity,
            localEField = axisVec,
            probeFieldDirection = axisVec,
            worldLightDirection = worldLightDirection.normalized
        };

        _core.ApplyConfig(config);
        return _core.Sensitivity;
    }

    private static Vector3 AxisToVector(ElectricFieldAxis axis)
    {
        switch (axis)
        {
            case ElectricFieldAxis.X_Axis: return Vector3.right;
            case ElectricFieldAxis.Y_Axis: return Vector3.up;
            case ElectricFieldAxis.Z_Axis:
            default: return Vector3.forward;
        }
    }
}
