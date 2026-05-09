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
            Debug.LogWarning("[Scene3CrystalBridge] 无晶体 Profile：未在预览中选择，也未配置 fallbackProfile。使用 RecordManager 默认 halfWaveVoltage。");
            return;
        }

        // 创建物理核心
        _core = gameObject.AddComponent<CrystalPhysicalCore>();

        // 计算灵敏度
        float sensitivity = ComputeSensitivity(profile);
        Debug.Log($"[Scene3CrystalBridge] Profile={profile.crystalName}, Sensitivity={sensitivity:E6}");

        // 计算 Vπ
        double vPi = VpiCalculator.Calculate(
            profile.defaultWavelength_nm,
            profile.defaultLength_mm,
            profile.defaultThickness_mm,
            sensitivity,
            modulationMode);

        if (double.IsInfinity(vPi) || double.IsNaN(vPi) || vPi <= 0)
        {
            Debug.LogWarning("[Scene3CrystalBridge] Vπ 计算无效，保留 RecordManager 默认值。");
            return;
        }

        _recordManager.halfWaveVoltage = (float)vPi;
        Debug.Log($"[Scene3CrystalBridge] 已设置 halfWaveVoltage = {vPi:F1} V (原默认值 150V 已覆盖)");
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
            worldLightDirection = Vector3.forward
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
