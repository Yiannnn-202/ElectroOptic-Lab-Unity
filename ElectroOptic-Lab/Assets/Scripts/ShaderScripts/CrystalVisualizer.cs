using UnityEngine;
using UnityEngine.UI; // 用于显示 RawImage

public class CrystalVisualizer : MonoBehaviour
{
    [Header("References")]
    public CrystalPhysicalCore physicalCore;
    public Material targetMaterial; // 赋值上面创建的 Shader 的材质

    [Header("Visualization Settings")]
    [Range(1, 120)] public float fov = 10.0f;
    public Color laserColor = Color.red;

    // 为了演示方便，这里允许覆盖长度和波长
    // 实际项目中应从 LabController/CrystalConfig 获取
    [Header("Sync Settings (Auto-read if 0)")]
    public float overrideLength_mm = 0f;
    public float overrideWavelength_nm = 0f;

    void Update()
    {
        if (physicalCore == null || targetMaterial == null) return;

        UpdateShaderProperties();
    }

    void UpdateShaderProperties()
    {
        // 1. 获取物理数据
        Vector3 indices = physicalCore.NewIndices; // (nx', ny', nz')
        Matrix4x4 rotMatrix = physicalCore.EllipsoidRotation;

        // 2. 坐标系修正 (The Z-Flip)
        // DLL (右手系) -> Unity (左手系)
        // 规则：翻转所有涉及 Z 轴的非对角项 (m02, m12, m20, m21)
        Matrix4x4 unityMat = rotMatrix;
        unityMat.m02 *= -1f;
        unityMat.m12 *= -1f;
        unityMat.m20 *= -1f;
        unityMat.m21 *= -1f;
        // m22 (zz) 保持不变，因为 -1 * -1 = 1

        // 3. 处理尺寸参数 (优先使用 Override，否则用默认值)
        // 注意：Shader 需要米 (m)，这里输入是毫米 (mm) 和 纳米 (nm)
        float len_m = (overrideLength_mm > 0 ? overrideLength_mm : 20.0f) * 1e-3f;
        float wave_m = (overrideWavelength_nm > 0 ? overrideWavelength_nm : 633.0f) * 1e-9f;

        // 4. 发送给 GPU
        targetMaterial.SetVector("_RefractiveIndices", indices);
        targetMaterial.SetMatrix("_RotationMatrix", unityMat);
        targetMaterial.SetFloat("_CrystalLength", len_m);
        targetMaterial.SetFloat("_Wavelength", wave_m);
        targetMaterial.SetFloat("_FOV", fov);
        targetMaterial.SetColor("_BaseColor", laserColor);
    }
}