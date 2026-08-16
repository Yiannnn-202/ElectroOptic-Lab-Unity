#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DemoDataQuickFillTests
{
    private static int _passed;
    private static int _failed;

    [MenuItem("ElectroOptics/Tests/Run Scene3 Demo Data Tests")]
    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== Scene3 Demo Data Tests Start ==========");

        Test_FixedVoltageRangeAndStep();
        Test_TheoreticalPowerCurve();
        Test_ZeroVoltageJitterKeepsTheoreticalData();
        Test_RandomVoltageJitterStaysWithinBounds();

        Debug.Log($"========== Scene3 Demo Data Tests Done: {_passed} passed, {_failed} failed ==========");
    }

    private static void Test_FixedVoltageRangeAndStep()
    {
        List<DemoRecordSample> samples = CreateTheoreticalSamples();

        AssertTrue("SampleCount", samples.Count == 20);
        AssertClose("FirstVoltage", samples[0].voltage, 0f, 1e-6f);
        AssertClose("LastVoltage", samples[samples.Count - 1].voltage, 380f, 1e-5f);

        bool usesTwentyVoltSteps = true;
        for (int i = 1; i < samples.Count; i++)
        {
            usesTwentyVoltSteps &= Mathf.Approximately(samples[i].voltage - samples[i - 1].voltage, 20f);
        }
        AssertTrue("TwentyVoltStep", usesTwentyVoltSteps);
    }

    private static void Test_TheoreticalPowerCurve()
    {
        List<DemoRecordSample> samples = CreateTheoreticalSamples();

        AssertClose("PowerAtZero", samples[0].powerMilliwatts, 0f, 1e-6f);
        AssertClose("PowerAtVpi", samples[5].powerMilliwatts, 1f, 1e-6f);
        AssertClose("PowerAt2Vpi", samples[10].powerMilliwatts, 0f, 1e-6f);
    }

    private static void Test_ZeroVoltageJitterKeepsTheoreticalData()
    {
        List<DemoRecordSample> theoretical = CreateTheoreticalSamples();
        List<DemoRecordSample> withoutJitter = DemoRecordSampleGenerator.AddRandomVoltageJitter(
            theoretical,
            0f,
            0f,
            380f,
            100f,
            CreateIdealParameters());

        bool identical = theoretical.Count == withoutJitter.Count;
        for (int i = 0; identical && i < theoretical.Count; i++)
        {
            identical = Mathf.Approximately(theoretical[i].voltage, withoutJitter[i].voltage)
                        && Mathf.Approximately(theoretical[i].powerMilliwatts, withoutJitter[i].powerMilliwatts);
        }

        AssertTrue("ZeroVoltageJitterKeepsTheory", identical);
    }

    private static void Test_RandomVoltageJitterStaysWithinBounds()
    {
        const float jitterAmplitude = 2f;
        List<DemoRecordSample> theoretical = CreateTheoreticalSamples();
        Random.State previousRandomState = Random.state;
        Random.InitState(20260816);
        List<DemoRecordSample> jittered = DemoRecordSampleGenerator.AddRandomVoltageJitter(
            theoretical,
            jitterAmplitude,
            0f,
            380f,
            100f,
            CreateIdealParameters());
        Random.state = previousRandomState;
        PowerReadoutParameters expectedPowerParameters = CreateIdealParameters();
        expectedPowerParameters.halfWaveVoltage = 100f;

        bool withinBounds = jittered.Count == theoretical.Count;
        bool hasPerturbation = false;
        for (int i = 0; withinBounds && i < theoretical.Count; i++)
        {
            float delta = jittered[i].voltage - theoretical[i].voltage;
            float expectedPower = PowerReadoutCalculator.CalculateStablePower(
                jittered[i].voltage,
                0f,
                0f,
                expectedPowerParameters) / 1000f;
            withinBounds = Mathf.Abs(delta) <= jitterAmplitude + 1e-6f
                           && jittered[i].voltage >= 0f
                           && jittered[i].voltage <= 380f
                           && Mathf.Approximately(jittered[i].powerMilliwatts, expectedPower);
            hasPerturbation |= Mathf.Abs(delta) > 1e-6f;
        }

        AssertTrue("RandomVoltageJitterWithinBounds", withinBounds);
        AssertTrue("RandomVoltageJitterChangesSamples", hasPerturbation);
    }

    private static List<DemoRecordSample> CreateTheoreticalSamples()
    {
        return DemoRecordSampleGenerator.GenerateTheoretical(
            0f,
            380f,
            20f,
            100f,
            CreateIdealParameters());
    }

    private static PowerReadoutParameters CreateIdealParameters()
    {
        return new PowerReadoutParameters
        {
            darkPower = 0f,
            powerScale = 1000f,
            leakage = 0f,
            visibility = 1f,
            phaseOffset = 0f,
            halfWaveVoltage = 150f,
            beamFocus = 20f
        };
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
