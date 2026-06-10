#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;

public static class ConoscopicIntensityCoreTests
{
    private const string LiNbO3ProfilePath = "Assets/Resources/Profiles/LiNbO3_Profile.asset";
    private const string KtpProfilePath = "Assets/Resources/Profiles/KTP_Profile.asset";
    private static int _passed;
    private static int _failed;

    [MenuItem("ElectroOptics/Tests/Run Conoscopic Intensity Core Tests")]
    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== Conoscopic Intensity Core Tests Start ==========");
        TestParametersClamp();
        TestResultReuseAndCoordinates();
        TestCoreInvalidWithoutProfile();
        TestCoreComputesWithProfile(LiNbO3ProfilePath, "LiNbO3");
        TestCoreComputesWithProfile(KtpProfilePath, "KTP");
        Debug.Log($"========== Conoscopic Intensity Core Tests Done: {_passed} passed, {_failed} failed ==========");
    }

    private static void TestParametersClamp()
    {
        var parameters = new ConoscopicIntensityParameters
        {
            resolution = 1,
            fov = 999f,
            phaseScale = -1f,
            displayGamma = -1f,
            blackCutoff = 2f,
            ringSharpness = -1f,
            crossWidth = 10f,
            initialMelatopeOffset = new Vector2(9f, -9f)
        };

        parameters.Clamp();
        AssertTrue("Clamp resolution", parameters.resolution == ConoscopicIntensityParameters.MinResolution);
        AssertClose("Clamp fov", parameters.fov, ConoscopicIntensityParameters.MaxFov, 1e-6f);
        AssertClose("Clamp phaseScale", parameters.phaseScale, ConoscopicIntensityParameters.MinPhaseScale, 1e-6f);
        AssertClose("Clamp displayGamma", parameters.displayGamma, ConoscopicIntensityParameters.MinDisplayGamma, 1e-6f);
        AssertClose("Clamp blackCutoff", parameters.blackCutoff, ConoscopicIntensityParameters.MaxBlackCutoff, 1e-6f);
        AssertClose("Clamp ringSharpness", parameters.ringSharpness, ConoscopicIntensityParameters.MinRingSharpness, 1e-6f);
        AssertClose("Clamp crossWidth", parameters.crossWidth, ConoscopicIntensityParameters.MaxCrossWidth, 1e-6f);
        AssertClose("Clamp melatope x", parameters.initialMelatopeOffset.x, ConoscopicIntensityParameters.MaxInitialMelatopeOffset, 1e-6f);
        AssertClose("Clamp melatope y", parameters.initialMelatopeOffset.y, -ConoscopicIntensityParameters.MaxInitialMelatopeOffset, 1e-6f);
    }

    private static void TestResultReuseAndCoordinates()
    {
        var result = new ConoscopicIntensityResult();
        result.EnsureSize(32);
        float[] first = result.Intensities;
        result.EnsureSize(32);
        AssertTrue("Result reuses same resolution array", ReferenceEquals(first, result.Intensities));
        result.EnsureSize(64);
        AssertTrue("Result reallocates resized array", !ReferenceEquals(first, result.Intensities) && result.Intensities.Length == 4096);

        Vector2 lowerLeft = result.GetCoordinate(0, 0);
        Vector2 upperRight = result.GetCoordinate(63, 63);
        AssertClose("Coordinate lower x", lowerLeft.x, -1f, 1e-6f);
        AssertClose("Coordinate lower y", lowerLeft.y, -1f, 1e-6f);
        AssertClose("Coordinate upper x", upperRight.x, 1f, 1e-6f);
        AssertClose("Coordinate upper y", upperRight.y, 1f, 1e-6f);
    }

    private static void TestCoreInvalidWithoutProfile()
    {
        var go = new GameObject("ConoscopicIntensityCore_Invalid_Test");
        try
        {
            var core = go.AddComponent<ConoscopicIntensityCore>();
            core.SetResolution(32);
            core.ForceRecalculate();
            AssertTrue("Core invalid without profile", !core.Result.IsValid);
            AssertTrue("Invalid result has expected size", core.Result.Intensities != null && core.Result.Intensities.Length == 1024);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestCoreComputesWithProfile(string profilePath, string label)
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(profilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[SKIP] {label} profile not found at {profilePath}");
            return;
        }

        var go = new GameObject($"ConoscopicIntensityCore_{label}_Test");
        try
        {
            var core = go.AddComponent<ConoscopicIntensityCore>();
            int updateCount = 0;
            core.OnIntensityUpdated += _ => updateCount++;
            core.SetProfile(profile);
            core.SetResolution(32);
            core.ForceRecalculate();

            AssertTrue($"{label} result valid", core.Result.IsValid);
            AssertTrue($"{label} event fired", updateCount > 0);
            AssertTrue($"{label} result size", core.Result.Intensities != null && core.Result.Intensities.Length == 1024);
            AssertIntensityRange($"{label} intensity range", core.Result.Intensities);
            AssertTrue($"{label} has visible signal", core.Result.MaxIntensity > 1e-5f);
            AssertOuterApertureZero($"{label} aperture outside zero", core.Result);

            float previousMax = core.Result.MaxIntensity;
            core.SetPhaseScale(core.Parameters.phaseScale * 1.5f);
            AssertTrue($"{label} dirty after phase change", core.IsDirty);
            core.ForceRecalculate();
            AssertTrue($"{label} valid after phase change", core.Result.IsValid);
            AssertTrue($"{label} recompute finite", !float.IsNaN(core.Result.MaxIntensity) && !float.IsInfinity(core.Result.MaxIntensity));
            Debug.Log($"  [Details] {label} max before={previousMax:F4}, after={core.Result.MaxIntensity:F4}");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void AssertIntensityRange(string name, float[] values)
    {
        bool inRange = true;
        for (int i = 0; i < values.Length; i++)
        {
            float value = values[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < -1e-6f || value > 1f + 1e-6f)
            {
                inRange = false;
                break;
            }
        }

        AssertTrue(name, inRange);
    }

    private static void AssertOuterApertureZero(string name, ConoscopicIntensityResult result)
    {
        bool outsideZero = true;
        for (int y = 0; y < result.Resolution; y++)
        {
            for (int x = 0; x < result.Resolution; x++)
            {
                Vector2 p = result.GetCoordinate(x, y);
                if (p.magnitude >= 1f && Mathf.Abs(result.Intensities[y * result.Resolution + x]) > 1e-5f)
                {
                    outsideZero = false;
                    break;
                }
            }
        }

        AssertTrue(name, outsideZero);
    }

    private static void AssertClose(string name, float actual, float expected, float tolerance)
    {
        if (Mathf.Abs(actual - expected) <= tolerance)
        {
            _passed++;
            Debug.Log($"  <color=green>[PASS]</color> {name} (actual={actual:G6}, expected={expected:G6})");
        }
        else
        {
            _failed++;
            Debug.LogError($"  <color=red>[FAIL]</color> {name} (actual={actual:G6}, expected={expected:G6})");
        }
    }

    private static void AssertTrue(string name, bool condition)
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
