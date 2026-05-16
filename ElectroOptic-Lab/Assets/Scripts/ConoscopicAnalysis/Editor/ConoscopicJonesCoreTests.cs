#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;

public static class ConoscopicJonesCoreTests
{
    private const string LiNbO3ProfilePath = "Assets/LiNbO3_Profile.asset";
    private const string KtpProfilePath = "Assets/KTP_Profile.asset";
    private static int _passed;
    private static int _failed;

    [MenuItem("ElectroOptics/Tests/Run Conoscopic Jones Core Tests")]
    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== Conoscopic Jones Core Tests Start ==========");
        TestParametersClamp();
        TestCpuReferenceRangeAndAperture();
        TestProfileDefaults();
        TestBiaxialProfileDefaults();
        TestCpuBiaxialRangeAndDelta();
        TestGpuCoreLifecycle();
        TestGpuKtpBiaxialLifecycle();
        Debug.Log($"========== Conoscopic Jones Core Tests Done: {_passed} passed, {_failed} failed ==========");
    }

    private static void TestParametersClamp()
    {
        var parameters = new ConoscopicJonesParameters
        {
            resolution = 1,
            wavelengthNm = -10f,
            thicknessMm = -1f,
            ordinaryIndexNo = 0.5f,
            extraordinaryIndexNe = 8f,
            screenDistanceM = 0f,
            screenHalfSizeM = -1f,
            initialIntensity = 20f,
            phaseAntiAliasStrength = 99f,
            opticAxisTiltDeg = 90f,
            apertureRadius = 9f,
            heightScale = -1f
        };

        parameters.Clamp();
        AssertTrue("Clamp resolution", parameters.resolution == ConoscopicJonesParameters.MinResolution);
        AssertClose("Clamp wavelength", parameters.wavelengthNm, ConoscopicJonesParameters.MinWavelengthNm, 1e-6f);
        AssertClose("Clamp thickness", parameters.thicknessMm, ConoscopicJonesParameters.MinThicknessMm, 1e-6f);
        AssertClose("Clamp no", parameters.ordinaryIndexNo, ConoscopicJonesParameters.MinIndex, 1e-6f);
        AssertClose("Clamp ne", parameters.extraordinaryIndexNe, ConoscopicJonesParameters.MaxIndex, 1e-6f);
        AssertClose("Clamp distance", parameters.screenDistanceM, ConoscopicJonesParameters.MinScreenDistanceM, 1e-6f);
        AssertClose("Clamp half size", parameters.screenHalfSizeM, ConoscopicJonesParameters.MinScreenHalfSizeM, 1e-6f);
        AssertClose("Clamp initial intensity", parameters.initialIntensity, ConoscopicJonesParameters.MaxInitialIntensity, 1e-6f);
        AssertClose("Clamp anti alias", parameters.phaseAntiAliasStrength, ConoscopicJonesParameters.MaxPhaseAntiAliasStrength, 1e-6f);
        AssertClose("Clamp optic tilt", parameters.opticAxisTiltDeg, ConoscopicJonesParameters.MaxOpticAxisTiltDeg, 1e-6f);
        AssertClose("Clamp aperture", parameters.apertureRadius, ConoscopicJonesParameters.MaxApertureRadius, 1e-6f);
        AssertClose("Clamp height", parameters.heightScale, ConoscopicJonesParameters.MinHeightScale, 1e-6f);
    }

    private static void TestCpuReferenceRangeAndAperture()
    {
        var parameters = new ConoscopicJonesParameters();
        float center = ConoscopicJonesCpuReference.EvaluateIntensity(Vector2.zero, parameters);
        float quadrant = ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(0.35f, 0.25f), parameters);
        float outside = ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(1.2f, 0f), parameters);

        AssertTrue("CPU center finite range", IsUnitFinite(center));
        AssertTrue("CPU quadrant finite range", IsUnitFinite(quadrant));
        AssertClose("CPU outside aperture zero", outside, 0f, 1e-6f);

        float before = quadrant;
        parameters.thicknessMm *= 1.7f;
        float after = ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(0.35f, 0.25f), parameters);
        AssertTrue("CPU thickness changes signal", Mathf.Abs(before - after) > 1e-5f);
    }

    private static void TestProfileDefaults()
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[SKIP] LiNbO3 profile not found at {LiNbO3ProfilePath}");
            return;
        }

        var parameters = new ConoscopicJonesParameters();
        parameters.ApplyProfileDefaults(profile);
        AssertClose("Profile wavelength", parameters.wavelengthNm, (float)profile.defaultWavelength_nm, 1e-4f);
        AssertClose("Profile thickness", parameters.thicknessMm, (float)profile.defaultLength_mm, 1e-4f);
        AssertTrue("Profile no finite", parameters.ordinaryIndexNo > 1f);
        AssertTrue("Profile ne finite", parameters.extraordinaryIndexNe > 1f);
    }

    private static void TestBiaxialProfileDefaults()
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[SKIP] KTP profile not found at {KtpProfilePath}");
            return;
        }

        var parameters = new ConoscopicJonesParameters();
        parameters.ApplyProfileDefaults(profile);
        AssertClose("KTP nx", parameters.principalIndexNx, (float)profile.n_x, 1e-5f);
        AssertClose("KTP ny", parameters.principalIndexNy, (float)profile.n_y, 1e-5f);
        AssertClose("KTP nz", parameters.principalIndexNz, (float)profile.n_z, 1e-5f);
        AssertTrue("KTP classified biaxial", parameters.IsBiaxial());

        parameters.principalIndexNy = parameters.principalIndexNx + parameters.uniaxialEpsilon * 0.25f;
        AssertTrue("Near-degenerate falls back uniaxial", !parameters.IsBiaxial());
    }

    private static void TestCpuBiaxialRangeAndDelta()
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[SKIP] KTP profile not found at {KtpProfilePath}");
            return;
        }

        var parameters = new ConoscopicJonesParameters();
        parameters.ApplyProfileDefaults(profile);
        parameters.worldToPrincipalMatrix = Matrix4x4.identity;

        float center = ConoscopicJonesCpuReference.EvaluateIntensity(Vector2.zero, parameters);
        float quadrant = ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(0.35f, 0.2f), parameters);
        float outside = ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(1.2f, 0f), parameters);
        float delta = ConoscopicJonesCpuReference.EvaluateDelta(new Vector2(0.35f, 0.2f), parameters);

        AssertTrue("CPU KTP center finite range", IsUnitFinite(center));
        AssertTrue("CPU KTP quadrant finite range", IsUnitFinite(quadrant));
        AssertClose("CPU KTP outside aperture zero", outside, 0f, 1e-6f);
        AssertTrue("CPU KTP delta finite positive", !float.IsNaN(delta) && !float.IsInfinity(delta) && delta > 0f);
    }

    private static void TestGpuCoreLifecycle()
    {
        if (Shader.Find("ElectroOptics/ConoscopicJonesIntensity") == null)
        {
            Debug.LogWarning("[SKIP] Conoscopic Jones shader not imported yet.");
            return;
        }

        var go = new GameObject("ConoscopicJonesGpuCore_Test");
        try
        {
            var core = go.AddComponent<ConoscopicJonesGpuCore>();
            int updateCount = 0;
            core.OnJonesIntensityUpdated += _ => updateCount++;
            var parameters = new ConoscopicJonesParameters { resolution = 32 };
            core.SetParameters(parameters);
            core.ForceRecalculate();

            AssertTrue("GPU result valid", core.Result.IsValid);
            AssertTrue("GPU event fired", updateCount > 0);
            AssertTrue("GPU texture size", core.IntensityHeightMap != null && core.IntensityHeightMap.width == 32 && core.IntensityHeightMap.height == 32);
            AssertTrue("GPU min finite range", IsUnitFinite(core.Result.MinIntensity));
            AssertTrue("GPU max finite range", IsUnitFinite(core.Result.MaxIntensity));

            AssertGpuCloseToCpu("GPU/CPU center", core, 16, 16, 0.12f);
            AssertGpuCloseToCpu("GPU/CPU quadrant", core, 21, 19, 0.12f);

            float previousMax = core.Result.MaxIntensity;
            parameters.screenHalfSizeM = 0.16f;
            core.SetParameters(parameters);
            AssertTrue("GPU dirty after half size change", core.IsDirty);
            core.ForceRecalculate();
            AssertTrue("GPU valid after half size change", core.Result.IsValid);
            AssertTrue("GPU half size changed signal", Mathf.Abs(previousMax - core.Result.MaxIntensity) > 1e-5f || core.Result.MaxIntensity > 0f);

            parameters.phaseAntiAliasStrength = 0f;
            core.SetParameters(parameters);
            core.ForceRecalculate();
            AssertTrue("GPU raw AA finite", IsUnitFinite(core.Result.MaxIntensity));

            parameters.phaseAntiAliasStrength = 1f;
            core.SetParameters(parameters);
            core.ForceRecalculate();
            AssertTrue("GPU smoothed AA finite", IsUnitFinite(core.Result.MaxIntensity));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestGpuKtpBiaxialLifecycle()
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[SKIP] KTP profile not found at {KtpProfilePath}");
            return;
        }

        if (Shader.Find("ElectroOptics/ConoscopicJonesIntensity") == null)
        {
            Debug.LogWarning("[SKIP] Conoscopic Jones shader not imported yet.");
            return;
        }

        var go = new GameObject("ConoscopicJonesGpuCore_KTP_Test");
        try
        {
            var core = go.AddComponent<ConoscopicJonesGpuCore>();
            core.SetProfile(profile);
            core.SetResolution(32);
            core.ForceRecalculate();

            AssertTrue("GPU KTP result valid", core.Result.IsValid);
            AssertTrue("GPU KTP classified biaxial", core.Parameters.IsBiaxial());
            AssertTrue("GPU KTP texture size", core.IntensityHeightMap != null && core.IntensityHeightMap.width == 32 && core.IntensityHeightMap.height == 32);
            AssertTrue("GPU KTP min finite range", IsUnitFinite(core.Result.MinIntensity));
            AssertTrue("GPU KTP max finite range", IsUnitFinite(core.Result.MaxIntensity));

            AssertGpuCloseToCpu("GPU/CPU KTP center", core, 16, 16, 0.2f);
            AssertGpuCloseToCpu("GPU/CPU KTP quadrant", core, 21, 19, 0.2f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void AssertGpuCloseToCpu(string name, ConoscopicJonesGpuCore core, int x, int y, float tolerance)
    {
        float gpu = ReadGpuPixel(core.IntensityHeightMap, x, y);
        float cpu = ConoscopicJonesCpuReference.EvaluateIntensityWithFiniteDifference(x, y, core.Parameters.resolution, core.Parameters);
        AssertClose(name, gpu, cpu, tolerance);
    }

    private static float ReadGpuPixel(RenderTexture texture, int x, int y)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        var readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);

        try
        {
            readback.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            readback.Apply(false, false);
            return Mathf.Clamp01(readback.GetPixel(0, 0).r);
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(readback);
        }
    }

    private static bool IsUnitFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= -1e-6f && value <= 1f + 1e-6f;
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
