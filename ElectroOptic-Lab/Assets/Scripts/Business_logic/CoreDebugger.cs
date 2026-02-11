using UnityEngine;
using ElectroOptics;

/// <summary>
/// 极简物理核心调试器
/// 替代 LabController，用于直接验证 "输入 -> 核心 -> 渲染" 链路
/// </summary>
public class CoreDebugger : MonoBehaviour
{
    [Header("基础连接 (Required)")]
    public CrystalPhysicalCore core;
    public CrystalProfile profile;

    [Header("几何控制 (Geometry)")]
    [Tooltip("晶体的欧拉角旋转 (X/Y/Z)")]
    public Vector3 crystalEulerAngles;

    [Tooltip("世界光照方向 (默认 0,0,1)")]
    public Vector3 lightDirection = new Vector3(0, 0, 1);

    [Header("物理控制 (Physics)")]
    [Tooltip("施加电压 (V)")]
    public float voltage = 0.0f;

    [Tooltip("晶体厚度/电极间距 (mm) - 用于计算电场强度 E=V/d")]
    public float thickness_mm = 1.0f;

    [Tooltip("电场方向 (单位向量) - 例如 (0,1,0) 代表 Y 方向")]
    public Vector3 eFieldDirection = new Vector3(0, 0, 1);

    void Start()
    {
        // 基础安全检查
        if (core == null) Debug.LogError("[CoreDebugger] Physical Core 未赋值！");
        if (profile == null) Debug.LogError("[CoreDebugger] Crystal Profile 未赋值！");
    }

    void Update()
    {
        // 持续驱动核心 (每帧更新，方便拖动数值实时观察)
        UpdateAndDispatch();
    }

    private void UpdateAndDispatch()
    {
        if (core == null || profile == null) return;

        // 1. 准备配置包
        CrystalConfig config = new CrystalConfig();
        config.profile = profile;

        // 2. 设置几何参数
        config.crystalRotation = Quaternion.Euler(crystalEulerAngles);
        config.worldLightDirection = lightDirection.normalized;

        // 3. 计算电场 (E = V / d)
        // 将 mm 转换为 m
        float d_meters = thickness_mm * 1e-3f;
        // 防除零保护
        if (d_meters < 1e-9f) d_meters = 1e-9f;

        float eMagnitude = voltage / d_meters; // V/m

        // 4. 设置物理场
        Vector3 dir = eFieldDirection.normalized;
        // 如果输入是零向量，默认防崩
        if (dir == Vector3.zero) dir = new Vector3(0, 0, 1);

        config.localEField = dir * eMagnitude;
        config.probeFieldDirection = dir; // 探测方向通常与施加场方向一致

        // 5. 推送给核心
        core.ApplyConfig(config);
    }
}