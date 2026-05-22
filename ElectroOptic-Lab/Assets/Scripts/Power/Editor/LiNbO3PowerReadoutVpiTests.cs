#if UNITY_EDITOR
using ElectroOptics;
using ElectroOptics.Oscilloscope;
using UnityEditor;
using UnityEngine;

public static class LiNbO3PowerReadoutVpiTests
{
    private const string ProfilePath = "Assets/LiNbO3_Profile.asset";
    private const int SampleCount = 1201;
    private const float RelativeTolerance = 0.01f;

    [MenuItem("ElectroOptics/Tests/Run LiNbO3 Power Readout Vpi Test")]
    public static void Run()
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogError($"[LiNbO3PowerReadoutVpiTests] Missing profile at {ProfilePath}");
            return;
        }

        GameObject probeObject = new GameObject("LiNbO3 Vpi Probe");
        try
        {
            CrystalPhysicalCore core = probeObject.AddComponent<CrystalPhysicalCore>();
            float sensitivity = ComputeSensitivity(core, profile);
            double expectedVpi = VpiCalculator.Calculate(
                profile.defaultWavelength_nm,
                profile.defaultLength_mm,
                profile.defaultThickness_mm,
                sensitivity,
                ModulationMode.Transverse);

            if (double.IsNaN(expectedVpi) || double.IsInfinity(expectedVpi) || expectedVpi <= 0.0)
            {
                Debug.LogError($"[LiNbO3PowerReadoutVpiTests] Invalid expected Vpi. sensitivity={sensitivity:E6}, Vpi={expectedVpi}");
                return;
            }

            PowerReadoutParameters parameters = PowerReadoutParameters.Default;
            parameters.halfWaveVoltage = (float)expectedVpi;
            parameters.beamFocus = 20f;

            float estimatedVpi = EstimateVpiFromPowerReadings(parameters);
            float tolerance = Mathf.Max(0.5f, (float)expectedVpi * RelativeTolerance);
            float error = Mathf.Abs(estimatedVpi - (float)expectedVpi);

            if (error <= tolerance)
            {
                Debug.Log(
                    "[PASS] LiNbO3 power readout can recover Vpi. " +
                    $"profile={profile.crystalName}, sensitivity={sensitivity:E6}, " +
                    $"expected={expectedVpi:F3} V, estimated={estimatedVpi:F3} V, error={error:F3} V");
            }
            else
            {
                Debug.LogError(
                    "[FAIL] LiNbO3 power readout Vpi mismatch. " +
                    $"profile={profile.crystalName}, sensitivity={sensitivity:E6}, " +
                    $"expected={expectedVpi:F3} V, estimated={estimatedVpi:F3} V, " +
                    $"error={error:F3} V, tolerance={tolerance:F3} V");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probeObject);
        }
    }

    private static float ComputeSensitivity(CrystalPhysicalCore core, CrystalProfile profile)
    {
        var config = new CrystalConfig
        {
            profile = profile,
            crystalRotation = Quaternion.identity,
            localEField = Vector3.forward,
            probeFieldDirection = Vector3.forward,
            worldLightDirection = Vector3.forward
        };

        core.ApplyConfig(config);
        return core.Sensitivity;
    }

    private static float EstimateVpiFromPowerReadings(PowerReadoutParameters parameters)
    {
        float scanMaxVoltage = parameters.halfWaveVoltage * 2f;
        float bestVoltage = 0f;
        float bestPower = float.NegativeInfinity;

        for (int i = 0; i < SampleCount; i++)
        {
            float t = i / (float)(SampleCount - 1);
            float voltage = Mathf.Lerp(0f, scanMaxVoltage, t);
            float power = PowerReadoutCalculator.CalculateStablePower(voltage, 0f, 0f, parameters);

            if (power > bestPower)
            {
                bestPower = power;
                bestVoltage = voltage;
            }
        }

        return bestVoltage;
    }
}
#endif
