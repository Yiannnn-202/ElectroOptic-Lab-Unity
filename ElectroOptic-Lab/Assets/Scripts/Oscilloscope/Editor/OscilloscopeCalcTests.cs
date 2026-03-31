#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using ElectroOptics;
using ElectroOptics.Oscilloscope;

/// <summary>
/// 示波器计算核心验证测试
/// 通过 Unity 菜单 ElectroOptics/Tests/Run Oscilloscope Calc Tests 运行
/// </summary>
public static class OscilloscopeCalcTests
{
    private static int _passed;
    private static int _failed;

    [MenuItem("ElectroOptics/Tests/Run Oscilloscope Calc Tests")]
    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== 示波器计算核心测试开始 ==========");

        // VpiCalculator 测试
        Test_Vpi_Transverse();
        Test_Vpi_Longitudinal();
        Test_Vpi_ZeroSensitivity();

        // WaveformCalculator 测试
        Test_Extinction();
        Test_FrequencyDoubling();
        Test_SameFrequency();
        Test_MaxIntensity();
        Test_InvalidVpi();
        Test_EnsureSize_NoGC();
        Test_Compensator();

        Debug.Log($"========== 测试完成: {_passed} 通过, {_failed} 失败 ==========");
    }

    // =====================================================================
    // VpiCalculator 测试
    // =====================================================================

    static void Test_Vpi_Transverse()
    {
        // KDP 典型参数: λ=633nm, L=20mm, d=1mm
        // 使用已知 sensitivity 值反算验证公式
        double sensitivity = 1e-10; // 假设值
        double lambda_nm = 633.0;
        double L_mm = 20.0;
        double d_mm = 1.0;

        double vPi = VpiCalculator.Calculate(lambda_nm, L_mm, d_mm, sensitivity, ModulationMode.Transverse);

        // 手算: Vπ = (633e-9 × 1e-3) / (2 × 20e-3 × 1e-10) = 633e-12 / 4e-12 = 158.25
        double expected = (633e-9 * 1e-3) / (2.0 * 20e-3 * sensitivity);
        AssertClose("Vpi_Transverse", vPi, expected, 1e-6);
    }

    static void Test_Vpi_Longitudinal()
    {
        double sensitivity = 1e-10;
        double lambda_nm = 633.0;

        double vPi = VpiCalculator.Calculate(lambda_nm, 20.0, 1.0, sensitivity, ModulationMode.Longitudinal);

        // 手算: Vπ = 633e-9 / (2 × 1e-10) = 3165
        double expected = 633e-9 / (2.0 * sensitivity);
        AssertClose("Vpi_Longitudinal", vPi, expected, 1e-6);
    }

    static void Test_Vpi_ZeroSensitivity()
    {
        double vPi = VpiCalculator.Calculate(633.0, 20.0, 1.0, 0.0, ModulationMode.Transverse);
        AssertTrue("Vpi_ZeroSensitivity → Infinity", double.IsPositiveInfinity(vPi));
    }

    // =====================================================================
    // WaveformCalculator 测试
    // =====================================================================

    static void Test_Extinction()
    {
        // 消光验证: V_DC=0, V_m=0 → Γ(t)=0 → sin²(0)=0 → ch2 全零
        var p = MakeParams(vDC: 0, vMod: 0, freq: 1000);
        var result = Compute(p, vPi: 100.0);

        bool allZero = true;
        for (int i = 0; i < result.ch2.Length; i++)
        {
            if (Mathf.Abs(result.ch2[i]) > 1e-6f)
            {
                allZero = false;
                break;
            }
        }
        AssertTrue("Extinction: V_DC=0, V_m=0 → ch2 全零", allZero);
    }

    static void Test_FrequencyDoubling()
    {
        // 倍频验证: V_DC=0 → Γ₀=0（极值点）
        // ch2 频率应为 ch1 的 2 倍
        // 检测方法：统计零交叉（ch2 从上方穿过均值的次数）
        float vPi = 100f;
        var p = MakeParams(vDC: 0, vMod: vPi * 0.3f, freq: 100, periods: 4, samples: 4096);
        var result = Compute(p, vPi);

        int ch1Crossings = CountZeroCrossings(result.ch1);
        int ch2Crossings = CountZeroCrossings(result.ch2, mean: true);

        // ch1 显示 4 个周期，应有 ~8 次零交叉
        // ch2 倍频应有 ~16 次零交叉（2倍）
        float ratio = (float)ch2Crossings / ch1Crossings;
        AssertClose("FrequencyDoubling: ch2零交叉/ch1零交叉 ≈ 2.0", ratio, 2.0, 0.15);
        Debug.Log($"  [详情] ch1 零交叉={ch1Crossings}, ch2 零交叉={ch2Crossings}, ratio={ratio:F2}");
    }

    static void Test_SameFrequency()
    {
        // 同频验证: V_DC = Vπ/2 → Γ₀ = π/2（线性工作点）
        // ch2 频率应与 ch1 相同
        float vPi = 100f;
        var p = MakeParams(vDC: vPi / 2f, vMod: vPi * 0.1f, freq: 100, periods: 4, samples: 4096);
        var result = Compute(p, vPi);

        int ch1Crossings = CountZeroCrossings(result.ch1);
        int ch2Crossings = CountZeroCrossings(result.ch2, mean: true);

        float ratio = (float)ch2Crossings / ch1Crossings;
        AssertClose("SameFrequency: V_DC=Vπ/2 → ch2/ch1 零交叉比 ≈ 1.0", ratio, 1.0, 0.15);
        Debug.Log($"  [详情] ch1 零交叉={ch1Crossings}, ch2 零交叉={ch2Crossings}, ratio={ratio:F2}");
    }

    static void Test_MaxIntensity()
    {
        // V(t) = Vπ 时，Γ = π → sin²(π/2) = 1 → I = I₀
        float vPi = 100f;
        var p = MakeParams(vDC: vPi, vMod: 0, freq: 1000);
        p.intensityMax = 3.5f;
        var result = Compute(p, vPi);

        // 所有点应等于 I₀ × sin²(π/2) = I₀ × 1 = 3.5
        AssertClose("MaxIntensity: V_DC=Vπ → I=I₀", result.ch2[0], 3.5, 1e-4);
        AssertClose("MaxIntensity: Γ₀ = π", result.gamma0, Math.PI, 1e-6);
    }

    static void Test_InvalidVpi()
    {
        // Vπ = Infinity → ch2 全零
        var p = MakeParams(vDC: 50, vMod: 10, freq: 1000);
        var result = Compute(p, double.PositiveInfinity);

        bool allZero = true;
        for (int i = 0; i < result.ch2.Length; i++)
        {
            if (Mathf.Abs(result.ch2[i]) > 1e-6f) { allZero = false; break; }
        }
        AssertTrue("InvalidVpi: Vπ=∞ → ch2 全零", allZero);

        // ch1 仍应正常输出
        bool ch1HasSignal = false;
        for (int i = 0; i < result.ch1.Length; i++)
        {
            if (Mathf.Abs(result.ch1[i]) > 0.1f) { ch1HasSignal = true; break; }
        }
        AssertTrue("InvalidVpi: Vπ=∞ 时 ch1 仍有信号", ch1HasSignal);
    }

    static void Test_EnsureSize_NoGC()
    {
        // 连续调用相同 sampleCount，数组引用不应改变
        var result = new WaveformResult();
        result.EnsureSize(512);
        var ref1 = result.ch1;
        var ref2 = result.ch2;

        result.EnsureSize(512);
        AssertTrue("EnsureSize_NoGC: 相同尺寸不重新分配 ch1", ReferenceEquals(ref1, result.ch1));
        AssertTrue("EnsureSize_NoGC: 相同尺寸不重新分配 ch2", ReferenceEquals(ref2, result.ch2));

        // 尺寸变化时应重新分配
        result.EnsureSize(1024);
        AssertTrue("EnsureSize_Resize: 尺寸变化时重新分配", result.ch1.Length == 1024);
    }

    static void Test_Compensator()
    {
        // 补偿器验证: V_DC=0, V_m=0, compensatorPhase=π/2
        // Γ = δ = π/2 → I = I₀ × sin²(π/4) = I₀ × 0.5
        float vPi = 100f;
        var p = MakeParams(vDC: 0, vMod: 0, freq: 1000);
        p.compensatorPhase = (float)(Math.PI / 2.0);
        var result = Compute(p, vPi);

        double expected = 1.0 * Math.Sin(Math.PI / 4.0) * Math.Sin(Math.PI / 4.0); // 0.5
        AssertClose("Compensator: δ=π/2, V=0 → I=0.5×I₀", result.ch2[0], expected, 1e-4);
    }

    // =====================================================================
    // 辅助方法
    // =====================================================================

    static OscilloscopeParameters MakeParams(
        float vDC = 0, float vMod = 5, float freq = 1000,
        float periods = 2, int samples = 1024)
    {
        return new OscilloscopeParameters
        {
            vDC = vDC,
            vModulation = vMod,
            frequency = freq,
            intensityMax = 1f,
            compensatorPhase = 0f,
            modulationMode = ModulationMode.Transverse,
            fieldAxis = ElectricFieldAxis.Z_Axis,
            sampleCount = samples,
            displayPeriods = periods
        };
    }

    static WaveformResult Compute(OscilloscopeParameters p, double vPi)
    {
        var calc = new WaveformCalculator();
        var result = new WaveformResult();
        calc.Compute(p, vPi, result);
        return result;
    }

    /// <summary>
    /// 统计数组的零交叉次数（信号穿过零点或均值的次数）
    /// </summary>
    static int CountZeroCrossings(float[] data, bool mean = false)
    {
        if (data == null || data.Length < 2) return 0;

        float baseline = 0f;
        if (mean)
        {
            double sum = 0;
            for (int i = 0; i < data.Length; i++) sum += data[i];
            baseline = (float)(sum / data.Length);
        }

        int crossings = 0;
        bool wasAbove = data[0] > baseline;
        for (int i = 1; i < data.Length; i++)
        {
            bool isAbove = data[i] > baseline;
            if (isAbove != wasAbove)
            {
                crossings++;
                wasAbove = isAbove;
            }
        }
        return crossings;
    }

    static void AssertClose(string name, double actual, double expected, double tolerance)
    {
        double diff = Math.Abs(actual - expected);
        if (diff <= tolerance)
        {
            _passed++;
            Debug.Log($"  <color=green>[PASS]</color> {name} (actual={actual:G6}, expected={expected:G6})");
        }
        else
        {
            _failed++;
            Debug.LogError($"  <color=red>[FAIL]</color> {name} (actual={actual:G6}, expected={expected:G6}, diff={diff:G4})");
        }
    }

    static void AssertTrue(string name, bool condition)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  <color=green>[PASS]</color> {name}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  <color=red>[FAIL]</color> {name}");
        }
    }
}
#endif
