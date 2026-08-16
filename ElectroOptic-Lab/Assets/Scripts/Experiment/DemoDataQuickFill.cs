using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一组用于 Scene3 演示的电压、光功率记录。
/// 功率单位为 mW，与 RecordManager 的表格和图表保持一致。
/// </summary>
public struct DemoRecordSample
{
    public float voltage;
    public float powerMilliwatts;

    public DemoRecordSample(float voltage, float powerMilliwatts)
    {
        this.voltage = voltage;
        this.powerMilliwatts = powerMilliwatts;
    }
}

/// <summary>
/// 生成稳定的理论演示数据，并可在填表时叠加随机电压扰动。
/// </summary>
public static class DemoRecordSampleGenerator
{
    public static List<DemoRecordSample> GenerateTheoretical(
        float startVoltage,
        float endVoltage,
        float voltageStep,
        float halfWaveVoltage,
        PowerReadoutParameters parameters)
    {
        var samples = new List<DemoRecordSample>();
        if (!IsFinite(startVoltage) || !IsFinite(endVoltage) || !IsFinite(voltageStep)
            || endVoltage < startVoltage || voltageStep <= 0f
            || !IsFinite(halfWaveVoltage) || halfWaveVoltage <= 0f)
        {
            return samples;
        }

        parameters.halfWaveVoltage = halfWaveVoltage;
        int sampleCount = Mathf.FloorToInt((endVoltage - startVoltage) / voltageStep + 0.0001f) + 1;
        samples.Capacity = sampleCount;

        for (int i = 0; i < sampleCount; i++)
        {
            float voltage = startVoltage + i * voltageStep;
            float stablePowerMicrowatts = PowerReadoutCalculator.CalculateStablePower(
                voltage,
                0f,
                0f,
                parameters);

            samples.Add(new DemoRecordSample(
                voltage,
                stablePowerMicrowatts / 1000f));
        }

        return samples;
    }

    public static List<DemoRecordSample> AddRandomVoltageJitter(
        IList<DemoRecordSample> theoreticalSamples,
        float jitterAmplitudeVolts,
        float minimumVoltage,
        float maximumVoltage,
        float halfWaveVoltage,
        PowerReadoutParameters parameters)
    {
        var jitteredSamples = new List<DemoRecordSample>(
            theoreticalSamples != null ? theoreticalSamples.Count : 0);
        if (theoreticalSamples == null || !IsFinite(minimumVoltage) || !IsFinite(maximumVoltage)
            || maximumVoltage < minimumVoltage || !IsFinite(halfWaveVoltage) || halfWaveVoltage <= 0f)
        {
            return jitteredSamples;
        }

        float safeAmplitude = IsFinite(jitterAmplitudeVolts)
            ? Mathf.Max(0f, jitterAmplitudeVolts)
            : 0f;
        parameters.halfWaveVoltage = halfWaveVoltage;

        for (int i = 0; i < theoreticalSamples.Count; i++)
        {
            DemoRecordSample sample = theoreticalSamples[i];
            float jitter = safeAmplitude > 0f
                ? Random.Range(-safeAmplitude, safeAmplitude)
                : 0f;
            float voltage = Mathf.Clamp(sample.voltage + jitter, minimumVoltage, maximumVoltage);
            float stablePowerMicrowatts = PowerReadoutCalculator.CalculateStablePower(
                voltage,
                0f,
                0f,
                parameters);
            jitteredSamples.Add(new DemoRecordSample(
                voltage,
                stablePowerMicrowatts / 1000f));
        }

        return jitteredSamples;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

/// <summary>
/// Scene3 演示用的快捷数据填充器。
/// Ctrl+Shift+D 会使用当前晶体的 Vπ 生成固定电压点的数据，不改变图表或报告页面状态。
/// </summary>
[RequireComponent(typeof(RecordManager))]
public class DemoDataQuickFill : MonoBehaviour
{
    private const float FallbackHalfWaveVoltage = 150f;
    private const float FillStartVoltage = 0f;
    private const float FillEndVoltage = 380f;
    private const float FillVoltageStep = 20f;

    [Header("Scene3 演示快捷键")]
    [SerializeField] private RecordManager recordManager;
    [SerializeField] private KeyCode shortcutKey = KeyCode.D;
    [Tooltip("每个固定电压点叠加的最大随机扰动（V）。")]
    [SerializeField, Min(0f)] private float voltageJitterVolts = 2f;

    private void Awake()
    {
        if (recordManager == null)
        {
            recordManager = GetComponent<RecordManager>();
        }
    }

    private void Update()
    {
        // 附加场景仍可能在后台存活，避免其响应当前场景的全局快捷键。
        if (gameObject.scene != SceneManager.GetActiveScene() || !IsShortcutPressed())
        {
            return;
        }

        FillDemoData();
    }

    /// <summary>
    /// 清空当前表格并填入 0 至 380V、20V 步长的数据，电压附加随机扰动。
    /// 可由快捷键或 Inspector 的组件菜单调用。
    /// </summary>
    [ContextMenu("Fill Scene3 Demo Data")]
    public void FillDemoData()
    {
        if (recordManager == null)
        {
            Debug.LogWarning("[DemoDataQuickFill] RecordManager is missing; demo data was not filled.", this);
            return;
        }

        int tableCapacity = Mathf.Min(recordManager.voltageCells.Count, recordManager.powerCells.Count);
        if (tableCapacity <= 0)
        {
            Debug.LogWarning("[DemoDataQuickFill] No paired table cells were found; demo data was not filled.", this);
            return;
        }

        float halfWaveVoltage = ResolveHalfWaveVoltage(recordManager.halfWaveVoltage);
        PowerReadoutParameters powerParameters = BuildPowerParameters(halfWaveVoltage);
        List<DemoRecordSample> theoreticalSamples = DemoRecordSampleGenerator.GenerateTheoretical(
            FillStartVoltage,
            FillEndVoltage,
            FillVoltageStep,
            halfWaveVoltage,
            powerParameters);
        int sampleCount = Mathf.Min(tableCapacity, theoreticalSamples.Count);
        if (sampleCount <= 0)
        {
            Debug.LogWarning("[DemoDataQuickFill] No demo samples were generated; demo data was not filled.", this);
            return;
        }

        List<DemoRecordSample> samples = DemoRecordSampleGenerator.AddRandomVoltageJitter(
            theoreticalSamples,
            NonNegativeFinite(voltageJitterVolts),
            FillStartVoltage,
            FillEndVoltage,
            halfWaveVoltage,
            powerParameters);

        recordManager.ClearTable();

        for (int i = 0; i < sampleCount; i++)
        {
            DemoRecordSample sample = samples[i];

            // 复用既有记录流程推进私有索引，保留清空、删除和满表保护语义。
            recordManager.currentVoltage = sample.voltage;
            recordManager.RecordData();

            recordManager.voltageCells[i].color = recordManager.normalTextColor;
            recordManager.powerCells[i].color = recordManager.normalTextColor;
            recordManager.voltageCells[i].text = sample.voltage.ToString("F1");
            recordManager.powerCells[i].text = sample.powerMilliwatts.ToString("F2");
        }

        DemoRecordSample lastSample = samples[sampleCount - 1];
        recordManager.currentVoltage = lastSample.voltage;
        if (recordManager.voltageText != null)
        {
            recordManager.voltageText.text = lastSample.voltage.ToString("F1");
        }

        if (recordManager.receiverText != null)
        {
            recordManager.receiverText.text = lastSample.powerMilliwatts.ToString("F2");
        }

        Debug.Log($"[DemoDataQuickFill] Filled {sampleCount} samples from {FillStartVoltage:F1} V to {lastSample.voltage:F1} V " +
                  $"with ±{NonNegativeFinite(voltageJitterVolts):F1} V jitter (Vπ = {halfWaveVoltage:F1} V).", this);
    }

    private bool IsShortcutPressed()
    {
        bool controlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        return controlHeld && shiftHeld && Input.GetKeyDown(shortcutKey);
    }

    private PowerReadoutParameters BuildPowerParameters(float halfWaveVoltage)
    {
        return new PowerReadoutParameters
        {
            darkPower = NonNegativeFinite(recordManager.darkPower),
            powerScale = NonNegativeFinite(recordManager.powerScale),
            leakage = NonNegativeFinite(recordManager.leakage),
            visibility = NonNegativeFinite(recordManager.visibility),
            phaseOffset = IsFinite(recordManager.phaseOffset) ? recordManager.phaseOffset : 0f,
            halfWaveVoltage = halfWaveVoltage,
            beamFocus = NonNegativeFinite(recordManager.beamFocus)
        };
    }

    private static float ResolveHalfWaveVoltage(float value)
    {
        return IsFinite(value) && value > 0f ? value : FallbackHalfWaveVoltage;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float NonNegativeFinite(float value)
    {
        return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
    }
}
