using System;
using ElectroOptics; // 引用命名空间
using UnityEngine;

public class CrystalPhysicalCore : MonoBehaviour
{
    // --- 状态输出 (供 Shader 或 UI 读取) ---
    [Header("Read-Only Status")]
    [SerializeField] private float _halfWaveVoltage; // V_pi
    [SerializeField] private Vector3 _newIndices;    // n'
    [SerializeField] private Matrix4x4 _rotationMatrix;

    // 公开属性
    public float HalfWaveVoltage => _halfWaveVoltage;
    public Vector3 NewIndices => _newIndices;
    public Matrix4x4 EllipsoidRotation => _rotationMatrix;

    // --- 内部缓存 ---
    private CrystalConfig _lastConfig;
    private SimInputData _inputData;
    private CrystalOutputData _outputData;
    private bool _isInitialized = false;

    // --- 缓存的中间变量 (避免每帧重复计算) ---
    private double _geometryFactor; // 1/d 或 1/L
    private double[] _eUnitVector;  // 单位电场向量
    private double[] _kUnitVector;  // 单位波矢向量

    void Awake()
    {
        // 初始化内存
        _inputData = new SimInputData();
        _inputData.Initialize();

        _outputData = new CrystalOutputData();
        _outputData.Initialize();

        _eUnitVector = new double[3];
        _kUnitVector = new double[3];

        _isInitialized = true;
    }

    /// <summary>
    /// 核心驱动接口：应用新的配置
    /// </summary>
    public void ApplyConfig(CrystalConfig config)
    {
        if (!_isInitialized || config.profile == null) return;

        // 1. 脏检查：几何/模式是否改变？
        bool isGeometryDirty = config.IsGeometryDifferent(_lastConfig);

        // 如果是全新的配置(第一次运行) 或 几何变了 -> 运行探测通道 (Heavy Update)
        if (isGeometryDirty)
        {
            RunProbePass(config);
        }

        // 2. 总是运行渲染通道 (Light Update)
        // 只要调用了 ApplyConfig，就说明电压可能变了，或者刚刚重算了几何
        RunRenderPass(config);

        // 3. 更新缓存
        _lastConfig = config;
    }

    // ========================================================================
    // 阶段一：探测通道 (Probe Pass)
    // 职责：更新几何因子、单位向量，并计算 V_pi
    // ========================================================================
    private void RunProbePass(CrystalConfig config)
    {
        // A. 更新静态参数
        _inputData.static_n = config.profile.GetStaticIndices();
        _inputData.r_tensor = config.profile.GetTensorArray();

        // B. 构造单位向量 (根据枚举)
        SetVectorFromAxis(_eUnitVector, (int)config.fieldAxis);
        SetVectorFromAxis(_kUnitVector, (int)config.propAxis);

        // 填入 input (用于 Probe 计算)
        Array.Copy(_kUnitVector, _inputData.wave_vector, 3);
        Array.Copy(_eUnitVector, _inputData.e_field_local, 3); // 探测电场 E=1

        // C. 计算几何因子 (E = V * G)
        // Transverse: E = V / d
        // Longitudinal: E = V / L
        if (config.mode == ModulationMode.Transverse)
        {
            _geometryFactor = 1.0 / (config.thickness_mm * 1e-3); // mm -> m
        }
        else
        {
            _geometryFactor = 1.0 / (config.length_mm * 1e-3); // mm -> m
        }

        // D. 调用 DLL 计算灵敏度
        if (NativeInterface.SafeCalculate(ref _inputData, ref _outputData))
        {
            double s_eff = _outputData.sensitivity;

            // E. 计算半波电压 V_pi
            // V_pi = lambda / (2 * s_eff * CorrectionFactor)
            // CorrectionFactor: Transverse = L/d; Longitudinal = 1;

            double lambda = config.wavelength_nm * 1e-9;
            double L = config.length_mm * 1e-3;
            double d = config.thickness_mm * 1e-3;

            if (s_eff > 1e-20)
            {
                if (config.mode == ModulationMode.Transverse)
                {
                    // Transverse: Phase = (2pi/lambda) * L * (s * V/d)
                    // Pi = (2pi/lambda) * L * s * V_pi / d
                    // V_pi = lambda * d / (2 * L * s)
                    _halfWaveVoltage = (float)((lambda * d) / (2.0 * L * s_eff));
                }
                else
                {
                    // Longitudinal: Phase = (2pi/lambda) * L * (s * V/L) = (2pi/lambda) * s * V
                    // V_pi = lambda / (2 * s)
                    _halfWaveVoltage = (float)(lambda / (2.0 * s_eff));
                }
            }
            else
            {
                _halfWaveVoltage = float.MaxValue; // 灵敏度为0，V_pi 无穷大
            }
        }
    }

    // ========================================================================
    // 阶段二：渲染通道 (Render Pass)
    // 职责：根据电压计算最终折射率
    // ========================================================================
    private void RunRenderPass(CrystalConfig config)
    {
        // 1. 计算标量电场
        double E_scalar = config.voltage * _geometryFactor;

        // 2. 构造真实电场向量 E_vec = E_scalar * e_unit
        _inputData.e_field_local[0] = _eUnitVector[0] * E_scalar;
        _inputData.e_field_local[1] = _eUnitVector[1] * E_scalar;
        _inputData.e_field_local[2] = _eUnitVector[2] * E_scalar;

        // 波矢不需要变，延用 Probe Pass 的结果

        // 3. 调用 DLL
        if (NativeInterface.SafeCalculate(ref _inputData, ref _outputData))
        {
            // 4. 更新输出
            _newIndices = new Vector3(
                (float)_outputData.n_prime[0],
                (float)_outputData.n_prime[1],
                (float)_outputData.n_prime[2]
            );

            // 转换旋转矩阵 (3x3 -> 4x4)
            var rm = _outputData.rotation_matrix;
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = (float)rm[0]; m.m01 = (float)rm[1]; m.m02 = (float)rm[2];
            m.m10 = (float)rm[3]; m.m11 = (float)rm[4]; m.m12 = (float)rm[5];
            m.m20 = (float)rm[6]; m.m21 = (float)rm[7]; m.m22 = (float)rm[8];
            _rotationMatrix = m;
        }
    }

    // 辅助：根据枚举设置单位向量 [1,0,0], [0,1,0], [0,0,1]
    private void SetVectorFromAxis(double[] vec, int axisIndex)
    {
        vec[0] = (axisIndex == 0) ? 1.0 : 0.0;
        vec[1] = (axisIndex == 1) ? 1.0 : 0.0;
        vec[2] = (axisIndex == 2) ? 1.0 : 0.0;
    }
}