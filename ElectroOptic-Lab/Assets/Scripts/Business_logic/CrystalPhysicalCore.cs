using System;
using ElectroOptics;
using UnityEngine;

public class CrystalPhysicalCore : MonoBehaviour
{
    // ========================================================================
    // 1. 公共接口 (Public API)
    // ========================================================================

    // --- A. 物理真值层 (Physical Truth) ---
    // 用于数据分析、调制深度计算、琼斯矩阵构建等
    // 所有输出均已转换为 Unity 左手坐标系

    /// <summary>
    /// 施加电压后的新主折射率 (nx', ny', nz')
    /// </summary>
    public Vector3 NewPrincipalIndices => _newIndices;

    /// <summary>
    /// 局部扰动矩阵 (Local Perturbation Matrix)
    /// 描述折射率椭球相对于“晶体几何坐标系”的旋转 (R_EO)
    /// </summary>
    public Matrix4x4 LocalPerturbationMatrix => _localPerturbationMatrix;

    /// <summary>
    /// 有效灵敏度 S_eff (1/V)
    /// </summary>
    public float Sensitivity => _sensitivity;

    // --- B. 渲染合成层 (Render Composite) ---
    // 专供 Shader 使用，已包含 World -> Geo -> Principal 的完整变换

    /// <summary>
    /// 视口空间到主轴空间的变换矩阵
    /// Matrix = (R_EO_LHS)^T * (R_Geo_to_World)^T
    /// </summary>
    public Matrix4x4 ShaderWorldToPrincipalMatrix => _shaderCompositeMatrix;

    // --- C. 配置访问 ---

    /// <summary>
    /// 获取当前生效的配置快照 (用于读取波长、长度等)
    /// </summary>
    public CrystalConfig CurrentConfig => _lastConfig;

    // ========================================================================
    // 2. 内部状态
    // ========================================================================

    [Header("Debug View")]
    [SerializeField] private float _sensitivity;
    [SerializeField] private Vector3 _newIndices;
    [SerializeField] private Matrix4x4 _localPerturbationMatrix; // R_EO (Left-Handed)
    [SerializeField] private Matrix4x4 _shaderCompositeMatrix;   // Final Shader Matrix

    private CrystalConfig _lastConfig;
    private SimInputData _inputData;
    private CrystalOutputData _outputData;
    private bool _isInitialized = false;

    // 缓存中间变量
    private double[] _waveVectorLocal;

    void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _inputData = new SimInputData();
        _inputData.Initialize();
        _outputData = new CrystalOutputData();
        _outputData.Initialize();
        _waveVectorLocal = new double[3];
        _localPerturbationMatrix = Matrix4x4.identity;
        _shaderCompositeMatrix = Matrix4x4.identity;
        _isInitialized = true;
    }

    /// <summary>
    /// 应用新的物理配置
    /// </summary>
    public void ApplyConfig(CrystalConfig config)
    {
        EnsureInitialized();
        if (config.profile == null) return;

        // 1. 探测通道 (Probe Pass): 当几何/探测轴变化时运行
        if (config.IsGeometryDifferent(_lastConfig))
        {
            RunProbePass(config);
        }

        // 2. 渲染通道 (Render Pass): 总是运行
        RunRenderPass(config);

        _lastConfig = config;
    }

    // ========================================================================
    // 3. 计算通道
    // ========================================================================

    private void RunProbePass(CrystalConfig config)
    {
        // 准备静态数据
        _inputData.static_n = config.profile.GetStaticIndices();
        _inputData.r_tensor = config.profile.GetTensorArray();

        // 计算 Local 波矢 (World +Z -> Local)
        // Inverse Rotation maps World Vector to Local Vector
        Vector3 k_world = config.worldLightDirection.normalized;
        Vector3 k_local = Quaternion.Inverse(config.crystalRotation) * k_world;

        _waveVectorLocal[0] = k_local.x;
        _waveVectorLocal[1] = k_local.y;
        _waveVectorLocal[2] = k_local.z;
        Array.Copy(_waveVectorLocal, _inputData.wave_vector, 3);

        // 设置探测电场
        Vector3 probeE = config.probeFieldDirection.normalized;
        _inputData.e_field_local[0] = probeE.x;
        _inputData.e_field_local[1] = probeE.y;
        _inputData.e_field_local[2] = probeE.z;

        // DLL 计算
        if (NativeInterface.SafeCalculate(ref _inputData, ref _outputData))
        {
            _sensitivity = (float)_outputData.sensitivity;
        }
        else
        {
            _sensitivity = 0f;
        }
    }

    private void RunRenderPass(CrystalConfig config)
    {
        // 设置真实电场
        _inputData.e_field_local[0] = config.localEField.x;
        _inputData.e_field_local[1] = config.localEField.y;
        _inputData.e_field_local[2] = config.localEField.z;

        // 复用波矢
        Array.Copy(_waveVectorLocal, _inputData.wave_vector, 3);

        // DLL 计算
        if (NativeInterface.SafeCalculate(ref _inputData, ref _outputData))
        {
            // 1. 提取折射率 (标量，无需坐标转换)
            _newIndices = new Vector3(
                (float)_outputData.n_prime[0],
                (float)_outputData.n_prime[1],
                (float)_outputData.n_prime[2]
            );

            // 2. 提取原始旋转矩阵 (DLL Right-Handed)
            Matrix4x4 matEO_RHS = Matrix4x4.identity;
            var rm = _outputData.rotation_matrix;
            matEO_RHS.m00 = (float)rm[0]; matEO_RHS.m01 = (float)rm[1]; matEO_RHS.m02 = (float)rm[2];
            matEO_RHS.m10 = (float)rm[3]; matEO_RHS.m11 = (float)rm[4]; matEO_RHS.m12 = (float)rm[5];
            matEO_RHS.m20 = (float)rm[6]; matEO_RHS.m21 = (float)rm[7]; matEO_RHS.m22 = (float)rm[8];

            // 3. 坐标系转换 (Right-Handed -> Left-Handed)
            //    Standard Z-Flip for Rotation Matrix: M_lhs = F * M_rhs * F
            //    Where F = Scale(1, 1, -1)
            _localPerturbationMatrix = ConvertRHStoLHS(matEO_RHS);

            // 4. 合成 Shader 矩阵 (World -> Principal)
            //    M_Shader = (R_EO_LHS)^T * (R_Geo_to_World)^T

            // A. 获取几何旋转 (Geo -> World)
            Matrix4x4 matGeo = Matrix4x4.Rotate(config.crystalRotation);

            // B. 计算逆变换 (World -> Geo)
            Matrix4x4 matWorldToGeo = matGeo.transpose; // Inverse

            // C. 计算扰动逆变换 (Geo -> Principal)
            //    Rotation Matrix 的逆 = 转置
            Matrix4x4 matGeoToPrincipal = _localPerturbationMatrix.transpose;

            // D. 最终合成: 先 World->Geo，再 Geo->Principal
            //    Unity 矩阵乘法: Parent * Child (右乘向量时为 v * M)
            //    Total = WorldToGeo * GeoToPrincipal
            _shaderCompositeMatrix = matWorldToGeo * matGeoToPrincipal;
        }
    }

    /// <summary>
    /// 辅助：将右手系旋转矩阵转换为左手系 (Z-Flip)
    /// </summary>
    private Matrix4x4 ConvertRHStoLHS(Matrix4x4 rhs)
    {
        Matrix4x4 lhs = rhs;
        // 翻转涉及 Z 的非对角项
        lhs.m02 *= -1f; // xz
        lhs.m12 *= -1f; // yz
        lhs.m20 *= -1f; // zx
        lhs.m21 *= -1f; // zy
        // m22 (zz) 保持不变 (-1 * -1)
        return lhs;
    }
}