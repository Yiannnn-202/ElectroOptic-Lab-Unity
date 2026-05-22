#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PowerReadoutCalculatorTests
{
    private static int _passed;
    private static int _failed;

    [MenuItem("ElectroOptics/Tests/Run Power Readout Calc Tests")]
    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== Power Readout Calc Tests Start ==========");

        Test_ZeroVoltageExtinction();
        Test_HalfWaveMaximum();
        Test_QuarterWaveMidValue();
        Test_LeakageMinimum();
        Test_AlignmentCenter();
        Test_AlignmentOffset();
        Test_InvalidHalfWaveVoltage();

        Debug.Log($"========== Power Readout Calc Tests Done: {_passed} passed, {_failed} failed ==========");
    }

    private static void Test_ZeroVoltageExtinction()
    {
        float transmission = PowerReadoutCalculator.CalculateTransmission(0f, 150f, 0f, 1f, 0f);
        AssertClose("ZeroVoltageExtinction", transmission, 0f, 1e-6f);
    }

    private static void Test_HalfWaveMaximum()
    {
        float transmission = PowerReadoutCalculator.CalculateTransmission(150f, 150f, 0f, 1f, 0f);
        AssertClose("HalfWaveMaximum", transmission, 1f, 1e-6f);
    }

    private static void Test_QuarterWaveMidValue()
    {
        float transmission = PowerReadoutCalculator.CalculateTransmission(75f, 150f, 0f, 1f, 0f);
        AssertClose("QuarterWaveMidValue", transmission, 0.5f, 1e-5f);
    }

    private static void Test_LeakageMinimum()
    {
        float transmission = PowerReadoutCalculator.CalculateTransmission(0f, 150f, 0.01f, 0.99f, 0f);
        AssertClose("LeakageMinimum", transmission, 0.01f, 1e-6f);
    }

    private static void Test_AlignmentCenter()
    {
        float efficiency = PowerReadoutCalculator.CalculateAlignmentEfficiency(0f, 0f, 20f);
        AssertClose("AlignmentCenter", efficiency, 1f, 1e-6f);
    }

    private static void Test_AlignmentOffset()
    {
        float efficiency = PowerReadoutCalculator.CalculateAlignmentEfficiency(0.1f, 0.1f, 20f);
        AssertTrue("AlignmentOffset", efficiency > 0f && efficiency < 1f);
    }

    private static void Test_InvalidHalfWaveVoltage()
    {
        float transmission = PowerReadoutCalculator.CalculateTransmission(10f, 0f, 0f, 1f, 0f);
        AssertTrue("InvalidHalfWaveVoltage finite",
            !float.IsNaN(transmission) && !float.IsInfinity(transmission));
        AssertClose("InvalidHalfWaveVoltage safe zero", transmission, 0f, 1e-6f);
    }

    private static void AssertClose(string label, float actual, float expected, float tolerance)
    {
        if (Mathf.Abs(actual - expected) <= tolerance)
        {
            _passed++;
            Debug.Log($"[PASS] {label}: {actual}");
            return;
        }

        _failed++;
        Debug.LogError($"[FAIL] {label}: expected={expected}, actual={actual}, tolerance={tolerance}");
    }

    private static void AssertTrue(string label, bool condition)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"[PASS] {label}");
            return;
        }

        _failed++;
        Debug.LogError($"[FAIL] {label}");
    }
}
#endif
