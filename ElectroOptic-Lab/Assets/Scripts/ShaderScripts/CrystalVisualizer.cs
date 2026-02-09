using UnityEngine;

public class CrystalVisualizer : MonoBehaviour
{
    [Header("References")]
    public CrystalPhysicalCore physicalCore;

    // 【修改点 1】不再引用 Material，改为引用 Renderer
    // 这样能确保我们拿到的是物体上“正在用”的那个
    public Renderer targetScreenRenderer;

    [Header("Visualization Settings")]
    [Range(1, 120)] public float fov = 10.0f;
    public Color laserColor = Color.red;

    [Header("Sync Settings")]
    public float overrideLength_mm = 0f;
    public float overrideWavelength_nm = 0f;

    // 内部缓存实例
    private Material _runtimeMaterial;

    void Start()
    {
        if (targetScreenRenderer == null)
        {
            Debug.LogError("[CrystalVisualizer] 屏幕 Renderer 未赋值！请将 Quad 拖入 Target Screen Renderer。");
            return;
        }

        // 【修改点 2】关键修复！获取运行时材质实例 (Instance)
        // 访问 .material 会自动实例化，确保修改能即时生效
        _runtimeMaterial = targetScreenRenderer.material;
    }

    void Update()
    {
        if (physicalCore == null || _runtimeMaterial == null) return;

        UpdateShaderProperties();
    }

    void UpdateShaderProperties()
    {
        // 1. 获取物理数据 (读取 Core 算好的最终矩阵)
        Vector3 indices = physicalCore.NewPrincipalIndices;
        Matrix4x4 finalMatrix = physicalCore.ShaderWorldToPrincipalMatrix;

        // 2. 获取尺寸参数
        float len_m = (overrideLength_mm > 0 ? overrideLength_mm : 20.0f) * 1e-3f;
        float wave_m = (overrideWavelength_nm > 0 ? overrideWavelength_nm : 633.0f) * 1e-9f;

        // 尝试从 Core 的 Config 读取波长作为备选
        if (physicalCore.CurrentConfig.profile != null && overrideWavelength_nm <= 0)
        {
            wave_m = (float)(physicalCore.CurrentConfig.profile.defaultWavelength_nm * 1e-9);
        }

        // 安全检查：防止波长为 0 导致黑屏
        if (wave_m < 1e-9f) wave_m = 633e-9f;

        // 3. 发送给 GPU (使用 _runtimeMaterial)
        _runtimeMaterial.SetVector("_RefractiveIndices", indices);
        _runtimeMaterial.SetMatrix("_RotationMatrix", finalMatrix);
        _runtimeMaterial.SetFloat("_CrystalLength", len_m);
        _runtimeMaterial.SetFloat("_Wavelength", wave_m);
        _runtimeMaterial.SetFloat("_FOV", fov);
        _runtimeMaterial.SetColor("_BaseColor", laserColor);
    }
}