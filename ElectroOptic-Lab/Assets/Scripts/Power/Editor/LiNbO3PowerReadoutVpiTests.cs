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
            CrystalWorkingGeometry scene3Geometry = CrystalWorkingGeometry.ResolveOscilloscope(
                profile,
                ModulationMode.Transverse,
                ElectricFieldAxis.Z_Axis,
                Vector3.up);
            CrystalWorkingGeometry scene4Geometry = CrystalWorkingGeometry.ResolveOscilloscope(
                profile,
                ModulationMode.Transverse,
                ElectricFieldAxis.Z_Axis,
                Vector3.forward);

            float sensitivity = ComputeSensitivity(core, profile, scene3Geometry);
            float scene4Sensitivity = ComputeSensitivity(core, profile, scene4Geometry);
            double expectedVpi = VpiCalculator.Calculate(
                profile.defaultWavelength_nm,
                profile.defaultLength_mm,
                profile.defaultThickness_mm,
                sensitivity,
                scene3Geometry.ModulationMode);
            double scene4Vpi = VpiCalculator.Calculate(
                profile.defaultWavelength_nm,
                profile.defaultLength_mm,
                profile.defaultThickness_mm,
                scene4Sensitivity,
                scene4Geometry.ModulationMode);

            if (double.IsNaN(expectedVpi) || double.IsInfinity(expectedVpi) || expectedVpi <= 0.0)
            {
                Debug.LogError($"[LiNbO3PowerReadoutVpiTests] Invalid expected Vpi. sensitivity={sensitivity:E6}, Vpi={expectedVpi}");
                return;
            }

            double geometryVpiDiff = System.Math.Abs(expectedVpi - scene4Vpi);
            if (double.IsNaN(scene4Vpi) || double.IsInfinity(scene4Vpi) || geometryVpiDiff > 1e-6)
            {
                Debug.LogError(
                    "[LiNbO3PowerReadoutVpiTests] Scene3/Scene4 geometry Vpi mismatch. " +
                    $"scene3Sensitivity={sensitivity:E6}, scene4Sensitivity={scene4Sensitivity:E6}, " +
                    $"scene3Vpi={expectedVpi:F6}, scene4Vpi={scene4Vpi:F6}, diff={geometryVpiDiff:E6}");
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

    private static float ComputeSensitivity(CrystalPhysicalCore core, CrystalProfile profile, CrystalWorkingGeometry geometry)
    {
        var config = new CrystalConfig
        {
            profile = profile,
            crystalRotation = Quaternion.identity,
            localEField = geometry.LocalEFieldDirection,
            probeFieldDirection = geometry.ProbeFieldDirection,
            worldLightDirection = geometry.WorldLightDirection
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
