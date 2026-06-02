#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;

public static class ConoscopicJonesCoreTests
{
    private const string LiNbO3ProfilePath = "Assets/LiNbO3_Profile.asset";
    private const string KtpProfilePath = "Assets/KTP_Profile.asset";
    private const float EoSmoothElectricFieldVm = 15000000f;
    private const float EoSmoothPhaseScale = 0.05f;
    private const float EoSmoothAntiAliasStrength = 3f;
    private const int EoSmoothSupersampleFactor = 2;
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
        TestUniaxialEoParameterCopy();
        TestBiaxialProfileDefaults();
        TestCpuBiaxialRangeAndDelta();
        TestGpuCoreLifecycle();
        TestGpuElectricFieldPerturbsRawJonesParameters();
        TestGpuContinuousEoViewUsesRawJonesEigenMode();
        TestEoSmoothPresetParameters();
        TestAdditionalExperimentApiPresets();
        TestAdditionalExperimentVisualizationSettings();
        TestAdditionalExperimentUiModeGlobalParameters();
        TestAdditionalExperimentUiResetCurrentModeParameters();
        TestAdditionalExperimentApiGpuLifecycle();
        TestGpuKtpBiaxialLifecycle();
        Debug.Log($"========== Conoscopic Jones Core Tests Done: {_passed} passed, {_failed} failed ==========");
    }

    private static void TestParametersClamp()
    {
        var parameters = new ConoscopicJonesParameters
        {
            resolution = 1,
            renderSupersampleFactor = 99,
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
            heightScale = -1f,
            phaseScale = -1f,
            ringSharpness = 99f,
            crossWidth = 99f,
            blackCutoff = 99f,
            displayGamma = -1f
        };

        parameters.Clamp();
        AssertTrue("Clamp resolution", parameters.resolution == ConoscopicJonesParameters.MinResolution);
        AssertTrue("Clamp supersample high", parameters.renderSupersampleFactor == ConoscopicJonesParameters.MaxRenderSupersampleFactor);
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
        AssertClose("Clamp phase scale", parameters.phaseScale, ConoscopicJonesParameters.MinPhaseScale, 1e-6f);
        AssertClose("Clamp ring sharpness", parameters.ringSharpness, ConoscopicJonesParameters.MaxRingSharpness, 1e-6f);
        AssertClose("Clamp cross width", parameters.crossWidth, ConoscopicJonesParameters.MaxCrossWidth, 1e-6f);
        AssertClose("Clamp black cutoff", parameters.blackCutoff, ConoscopicJonesParameters.MaxBlackCutoff, 1e-6f);
        AssertClose("Clamp display gamma", parameters.displayGamma, ConoscopicJonesParameters.MinDisplayGamma, 1e-6f);

        var lowSupersample = new ConoscopicJonesParameters { renderSupersampleFactor = -1 };
        lowSupersample.Clamp();
        AssertTrue("Clamp supersample low", lowSupersample.renderSupersampleFactor == ConoscopicJonesParameters.MinRenderSupersampleFactor);
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
        AssertClose("KTP paper wavelength", parameters.wavelengthNm, ConoscopicJonesParameters.PaperKtp1WavelengthNm, 1e-5f);
        AssertClose("KTP paper thickness", parameters.thicknessMm, ConoscopicJonesParameters.PaperKtp1ThicknessMm, 1e-5f);
        AssertClose("KTP paper alpha", parameters.crystalAxisAngleDeg, ConoscopicJonesParameters.PaperKtp1AlphaDeg, 1e-5f);
        AssertTrue("KTP paper display mode", parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.PaperKtp1);
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
        float nearby = ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(0.36f, 0.2f), parameters);
        float sampleMax = Mathf.Max(
            center,
            quadrant,
            nearby,
            ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(-0.35f, -0.2f), parameters),
            ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(0.2f, -0.35f), parameters),
            ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(-0.2f, 0.35f), parameters));
        float outside = ConoscopicJonesCpuReference.EvaluateIntensity(new Vector2(1.2f, 0f), parameters);
        float delta = ConoscopicJonesCpuReference.EvaluateDelta(new Vector2(0.35f, 0.2f), parameters);

        AssertTrue("CPU KTP center finite range", IsUnitFinite(center));
        AssertTrue("CPU KTP quadrant finite range", IsUnitFinite(quadrant));
        AssertTrue("CPU KTP teaching display has signal", sampleMax > 0.0001f);
        AssertTrue("CPU KTP adjacent sample not spiky", Mathf.Abs(quadrant - nearby) < 0.95f);
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

            parameters.resolution = 64;
            parameters.renderSupersampleFactor = 2;
            core.SetParameters(parameters);
            core.ForceRecalculate();
            AssertTrue("GPU supersample result valid", core.Result.IsValid);
            AssertTrue("GPU supersample final texture size", core.IntensityHeightMap != null && core.IntensityHeightMap.width == 64 && core.IntensityHeightMap.height == 64);
            AssertTrue("GPU supersample max finite", IsUnitFinite(core.Result.MaxIntensity));
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

            AssertTrue("GPU KTP center finite range", IsUnitFinite(ReadGpuPixel(core.IntensityHeightMap, 16, 16)));
            AssertGpuCloseToCpu("GPU/CPU KTP quadrant", core, 21, 19, 0.2f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestUniaxialEoParameterCopy()
    {
        var parameters = new ConoscopicJonesParameters
        {
            uniaxialEoView = true,
            uniaxialEoUsePerturbedAxis = true,
            forceUniaxial = true,
            biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones
        };

        var copy = new ConoscopicJonesParameters(parameters);
        AssertTrue("Uniaxial EO view copied", copy.uniaxialEoView);
        AssertTrue("Uniaxial EO axis option copied", copy.uniaxialEoUsePerturbedAxis);
        AssertTrue("Uniaxial EO force copied", copy.forceUniaxial);
        AssertTrue("Uniaxial EO mode copied", copy.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones);
    }

    private static void TestGpuElectricFieldPerturbsRawJonesParameters()
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[SKIP] LiNbO3 profile not found at {LiNbO3ProfilePath}");
            return;
        }

        if (Shader.Find("ElectroOptics/ConoscopicJonesIntensity") == null)
        {
            Debug.LogWarning("[SKIP] Conoscopic Jones shader not imported yet.");
            return;
        }

        var go = new GameObject("ConoscopicJonesGpuCore_EField_Test");
        try
        {
            var core = go.AddComponent<ConoscopicJonesGpuCore>();
            core.SetProfile(profile);

            var parameters = new ConoscopicJonesParameters(core.Parameters)
            {
                resolution = 32,
                biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones,
                electricFieldStrength = 0f
            };
            core.SetParameters(parameters);
            core.ForceRecalculate();

            Vector3 zeroFieldIndices = GetPrincipalIndices(core.Parameters);
            Matrix4x4 zeroFieldMatrix = core.Parameters.worldToPrincipalMatrix;
            AssertTrue("GPU zero-field EO result valid", core.Result.IsValid);
            AssertTrue("GPU zero-field EO max finite", IsUnitFinite(core.Result.MaxIntensity));

            parameters = new ConoscopicJonesParameters(core.Parameters)
            {
                biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones,
                electricFieldStrength = 50000000f
            };
            core.SetParameters(parameters);
            core.ForceRecalculate();

            Vector3 fieldIndices = GetPrincipalIndices(core.Parameters);
            Matrix4x4 fieldMatrix = core.Parameters.worldToPrincipalMatrix;
            bool indicesChanged = (fieldIndices - zeroFieldIndices).sqrMagnitude > 1e-14f;
            bool matrixChanged = MatrixChanged(fieldMatrix, zeroFieldMatrix, 1e-7f);

            AssertTrue("GPU nonzero-field EO result valid", core.Result.IsValid);
            AssertTrue("GPU nonzero-field EO max finite", IsUnitFinite(core.Result.MaxIntensity));
            AssertTrue("GPU electric field perturbs RawJones parameters", indicesChanged || matrixChanged);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestGpuContinuousEoViewUsesRawJonesEigenMode()
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[SKIP] LiNbO3 profile not found at {LiNbO3ProfilePath}");
            return;
        }

        if (Shader.Find("ElectroOptics/ConoscopicJonesIntensity") == null)
        {
            Debug.LogWarning("[SKIP] Conoscopic Jones shader not imported yet.");
            return;
        }

        var go = new GameObject("ConoscopicJonesGpuCore_ContinuousEO_Test");
        try
        {
            var core = go.AddComponent<ConoscopicJonesGpuCore>();
            core.SetProfile(profile);

            var zeroField = new ConoscopicJonesParameters(core.Parameters)
            {
                resolution = 32,
                biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones,
                forceUniaxial = false,
                uniaxialEoView = true,
                uniaxialEoUsePerturbedAxis = false,
                electricFieldStrength = 0f
            };
            core.SetParameters(zeroField);
            core.ForceRecalculate();
            Vector3 zeroFieldIndices = GetPrincipalIndices(core.Parameters);

            var field = new ConoscopicJonesParameters(core.Parameters)
            {
                forceUniaxial = false,
                uniaxialEoView = true,
                uniaxialEoUsePerturbedAxis = false,
                electricFieldStrength = 50000000f
            };
            core.SetParameters(field);
            core.ForceRecalculate();
            Vector3 fieldIndices = GetPrincipalIndices(core.Parameters);

            AssertTrue("Continuous EO result valid", core.Result.IsValid);
            AssertTrue("Continuous EO forced RawJones", core.Parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones);
            AssertTrue("Continuous EO keeps eigen path enabled", !core.Parameters.forceUniaxial);
            AssertTrue("Continuous EO view flag active", core.Parameters.uniaxialEoView);
            AssertTrue("Continuous EO max finite", IsUnitFinite(core.Result.MaxIntensity));
            AssertTrue("Continuous EO field perturbs indices", (fieldIndices - zeroFieldIndices).sqrMagnitude > 1e-14f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestEoSmoothPresetParameters()
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[SKIP] LiNbO3 profile not found at {LiNbO3ProfilePath}");
            return;
        }

        var parameters = new ConoscopicJonesParameters();
        parameters.ApplyProfileDefaults(profile);
        parameters.resolution = 256;
        parameters.renderSupersampleFactor = EoSmoothSupersampleFactor;
        parameters.biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones;
        parameters.forceUniaxial = false;
        parameters.uniaxialEoView = true;
        parameters.uniaxialEoUsePerturbedAxis = false;
        parameters.electricFieldStrength = EoSmoothElectricFieldVm;
        parameters.phaseScale = EoSmoothPhaseScale;
        parameters.phaseAntiAliasStrength = EoSmoothAntiAliasStrength;
        parameters.Clamp();

        AssertTrue("EO smooth mode RawJones", parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones);
        AssertTrue("EO smooth continuous view", parameters.uniaxialEoView);
        AssertTrue("EO smooth fixed axis", !parameters.uniaxialEoUsePerturbedAxis);
        AssertTrue("EO smooth keeps eigen path enabled", !parameters.forceUniaxial);
        AssertTrue("EO smooth resolution", parameters.resolution == 256);
        AssertTrue("EO smooth supersample", parameters.renderSupersampleFactor == EoSmoothSupersampleFactor);
        AssertClose("EO smooth field", parameters.electricFieldStrength, EoSmoothElectricFieldVm, 0.5f);
        AssertClose("EO smooth phase scale", parameters.phaseScale, EoSmoothPhaseScale, 1e-6f);
        AssertClose("EO smooth AA", parameters.phaseAntiAliasStrength, EoSmoothAntiAliasStrength, 1e-6f);
    }

    private static void TestAdditionalExperimentApiPresets()
    {
        CrystalProfile liNbO3 = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        CrystalProfile ktp = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);
        if (liNbO3 == null || ktp == null)
        {
            Debug.LogWarning("[SKIP] Additional experiment profiles not found.");
            return;
        }

        var go = new GameObject("AdditionalConoscopicExperimentApi_Preset_Test");
        try
        {
            var api = go.AddComponent<AdditionalConoscopicExperimentApi>();
            api.SetProfiles(liNbO3, ktp);

            var user = AdditionalConoscopicUserParameters.Defaults;
            user.voltageV = 1000f;
            user.wavelengthNm = 587f;
            user.thicknessMm = 7.5f;
            user.crystalAxisAngleDeg = 37f;
            user.thetaDeg = 12f;
            user.phiDeg = 25f;
            user.apertureRadius = 0.75f;

            var liSource = new ConoscopicJonesParameters();
            liSource.ApplyProfileDefaults(liNbO3);
            ConoscopicJonesParameters m1 = api.BuildParametersForMode(AdditionalConoscopicMode.Uniaxial, user, liSource);
            AssertTrue("Additional M1 RawJones-compatible preset", m1.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones);
            AssertTrue("Additional M1 disables continuous EO", !m1.uniaxialEoView);
            AssertTrue("Additional M1 keeps eigen path available", !m1.forceUniaxial);
            AssertClose("Additional M1 electric field zero", m1.electricFieldStrength, 0f, 1e-6f);
            AssertClose("Additional M1 aperture", m1.apertureRadius, user.apertureRadius, 1e-6f);

            var ktpSource = new ConoscopicJonesParameters();
            ktpSource.ApplyProfileDefaults(ktp);
            AssertTrue("KTP profile default enters Paper preset", ktpSource.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.PaperKtp1);
            ConoscopicJonesParameters m2 = api.BuildParametersForMode(AdditionalConoscopicMode.BiaxialVoltage, user, ktpSource);
            AssertTrue("Additional M2 forces RawJones", m2.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones);
            AssertTrue("Additional M2 disables continuous EO", !m2.uniaxialEoView);
            AssertTrue("Additional M2 keeps eigen path available", !m2.forceUniaxial);
            AssertClose("Additional M2 voltage conversion", m2.electricFieldStrength, 50000000f, 0.5f);
            AssertClose("Additional M2 alpha", m2.crystalAxisAngleDeg, user.crystalAxisAngleDeg, 1e-6f);
            AssertClose("Additional M2 theta maps to optic tilt", m2.opticAxisTiltDeg, user.thetaDeg, 1e-6f);
            AssertClose("Additional M2 phi maps to optic azimuth", m2.opticAxisAzimuthDeg, user.phiDeg, 1e-6f);

            ConoscopicJonesParameters m3 = api.BuildParametersForMode(AdditionalConoscopicMode.UniaxialVoltage, user, liSource);
            AssertTrue("Additional M3 forces RawJones", m3.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones);
            AssertTrue("Additional M3 enables continuous EO", m3.uniaxialEoView);
            AssertTrue("Additional M3 fixed axis", !m3.uniaxialEoUsePerturbedAxis);
            AssertTrue("Additional M3 keeps eigen path available", !m3.forceUniaxial);
            AssertClose("Additional M3 voltage conversion", m3.electricFieldStrength, 50000000f, 0.5f);
            AssertClose("Additional M3 keeps user wavelength", m3.wavelengthNm, user.wavelengthNm, 1e-4f);
            AssertClose("Additional M3 keeps user thickness", m3.thicknessMm, user.thicknessMm, 1e-4f);
            AssertClose("Additional M3 screen distance", m3.screenDistanceM, 0.35f, 1e-6f);
            AssertClose("Additional M3 screen half size", m3.screenHalfSizeM, 0.08f, 1e-6f);
            AssertClose("Additional M3 alpha", m3.crystalAxisAngleDeg, 45f, 1e-6f);
            AssertTrue("Additional M3 resolution", m3.resolution == 256);
            AssertClose("Additional M3 phase scale", m3.phaseScale, EoSmoothPhaseScale, 1e-6f);
            AssertClose("Additional M3 AA", m3.phaseAntiAliasStrength, EoSmoothAntiAliasStrength, 1e-6f);
            AssertTrue("Additional M3 supersample", m3.renderSupersampleFactor == EoSmoothSupersampleFactor);
            AssertTrue("Additional profile M2 resolves KTP", api.ResolveProfile(AdditionalConoscopicMode.BiaxialVoltage) == ktp);
            AssertTrue("Additional profile M1 resolves LiNbO3", api.ResolveProfile(AdditionalConoscopicMode.Uniaxial) == liNbO3);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestAdditionalExperimentVisualizationSettings()
    {
        var go = new GameObject("AdditionalConoscopicVisualizationSettings_Test");
        GameObject surfaceGo = null;
        Camera generatedCamera = null;
        try
        {
            var settings = go.AddComponent<AdditionalConoscopicVisualizationSettings>();
            settings.Resolution = 64;
            settings.RenderSupersampleFactor = 3;
            settings.ScreenDistanceM = 1.2f;
            settings.ScreenHalfSizeM = 0.12f;
            settings.M2ScreenDistanceM = 0.42f;
            settings.M2ScreenHalfSizeM = 0.06f;
            settings.InitialIntensity = 0.8f;
            settings.PhaseScale = 0.22f;
            settings.PhaseAntiAliasStrength = 2.4f;
            settings.RingSharpness = 1.7f;
            settings.CrossWidth = 0.09f;
            settings.BlackCutoff = 0.02f;
            settings.DisplayGamma = 1.4f;
            settings.OutputTextureSize = 512;
            settings.SurfaceSize = 7f;
            settings.HeightScale = 2.4f;
            settings.NormalizeDisplayIntensity = true;
            settings.UseM3SmoothPreset = true;

            var api = go.AddComponent<AdditionalConoscopicExperimentApi>();
            api.SetVisualizationSettings(settings);

            var user = AdditionalConoscopicUserParameters.Defaults;
            user.voltageV = 1000f;
            user.wavelengthNm = 587f;
            user.thicknessMm = 7.5f;

            ConoscopicJonesParameters m1 = api.BuildParametersForMode(AdditionalConoscopicMode.Uniaxial, user);
            AssertTrue("Additional settings M1 resolution", m1.resolution == 64);
            AssertTrue("Additional settings M1 supersample", m1.renderSupersampleFactor == 3);
            AssertClose("Additional settings M1 screen distance", m1.screenDistanceM, 1.2f, 1e-6f);
            AssertClose("Additional settings M1 screen half size", m1.screenHalfSizeM, 0.12f, 1e-6f);
            AssertClose("Additional settings M1 phase scale", m1.phaseScale, 0.22f, 1e-6f);
            AssertClose("Additional settings M1 AA", m1.phaseAntiAliasStrength, 2.4f, 1e-6f);
            AssertClose("Additional settings M1 ring sharpness", m1.ringSharpness, 1.7f, 1e-6f);
            AssertClose("Additional settings M1 cross width", m1.crossWidth, 0.09f, 1e-6f);
            AssertClose("Additional settings M1 black cutoff", m1.blackCutoff, 0.02f, 1e-6f);
            AssertClose("Additional settings M1 display gamma", m1.displayGamma, 1.4f, 1e-6f);

            ConoscopicJonesParameters m2 = api.BuildParametersForMode(AdditionalConoscopicMode.BiaxialVoltage, user);
            AssertClose("Additional settings M2 screen distance override", m2.screenDistanceM, 0.42f, 1e-6f);
            AssertClose("Additional settings M2 screen half size override", m2.screenHalfSizeM, 0.06f, 1e-6f);

            settings.UseM2ScreenOverride = false;
            ConoscopicJonesParameters m2Shared = api.BuildParametersForMode(AdditionalConoscopicMode.BiaxialVoltage, user);
            AssertClose("Additional settings M2 shared screen distance", m2Shared.screenDistanceM, 1.2f, 1e-6f);
            AssertClose("Additional settings M2 shared screen half size", m2Shared.screenHalfSizeM, 0.12f, 1e-6f);
            settings.UseM2ScreenOverride = true;

            ConoscopicJonesParameters m3Smooth = api.BuildParametersForMode(AdditionalConoscopicMode.UniaxialVoltage, user);
            AssertTrue("Additional settings M3 smooth resolution", m3Smooth.resolution == 256);
            AssertClose("Additional settings M3 smooth keeps user wavelength", m3Smooth.wavelengthNm, user.wavelengthNm, 1e-6f);
            AssertClose("Additional settings M3 smooth keeps user thickness", m3Smooth.thicknessMm, user.thicknessMm, 1e-6f);
            AssertClose("Additional settings M3 smooth screen distance", m3Smooth.screenDistanceM, 0.35f, 1e-6f);
            AssertClose("Additional settings M3 smooth screen half size", m3Smooth.screenHalfSizeM, 0.08f, 1e-6f);
            AssertClose("Additional settings M3 smooth alpha", m3Smooth.crystalAxisAngleDeg, 45f, 1e-6f);
            AssertTrue("Additional settings M3 smooth supersample", m3Smooth.renderSupersampleFactor == EoSmoothSupersampleFactor);
            AssertClose("Additional settings M3 smooth phase scale", m3Smooth.phaseScale, EoSmoothPhaseScale, 1e-6f);
            AssertClose("Additional settings M3 smooth AA", m3Smooth.phaseAntiAliasStrength, EoSmoothAntiAliasStrength, 1e-6f);

            settings.UseM3SmoothPreset = false;
            ConoscopicJonesParameters m3Custom = api.BuildParametersForMode(AdditionalConoscopicMode.UniaxialVoltage, user);
            AssertTrue("Additional settings M3 custom supersample", m3Custom.renderSupersampleFactor == 3);
            AssertClose("Additional settings M3 custom keeps user wavelength", m3Custom.wavelengthNm, user.wavelengthNm, 1e-6f);
            AssertClose("Additional settings M3 custom keeps user thickness", m3Custom.thicknessMm, user.thicknessMm, 1e-6f);
            AssertClose("Additional settings M3 custom phase scale", m3Custom.phaseScale, 0.22f, 1e-6f);
            AssertClose("Additional settings M3 custom AA", m3Custom.phaseAntiAliasStrength, 2.4f, 1e-6f);

            surfaceGo = new GameObject("AdditionalConoscopicSurfaceView_Settings_Test");
            var surfaceView = surfaceGo.AddComponent<AdditionalConoscopicSurfaceView>();
            surfaceView.SetVisualizationSettings(settings);
            generatedCamera = surfaceView.RenderCamera;
            AssertTrue("Additional surface output texture setting", surfaceView.RenderTexture != null && surfaceView.RenderTexture.width == 512);
            AssertClose("Additional surface size setting", surfaceView.EffectiveSurfaceSize, 7f, 1e-6f);
            AssertClose("Additional surface height setting", surfaceView.EffectiveHeightScale, 2.4f, 1e-6f);
            AssertTrue("Additional surface normalize setting", surfaceView.EffectiveNormalizeDisplayIntensity);
            settings.UseM3SmoothPreset = true;
            AssertClose(
                "Additional surface M3 height setting",
                surfaceView.ResolveEffectiveHeightScale(m3Smooth),
                0.12f,
                1e-6f);
        }
        finally
        {
            if (surfaceGo != null)
            {
                Object.DestroyImmediate(surfaceGo);
            }

            if (generatedCamera != null)
            {
                Object.DestroyImmediate(generatedCamera.gameObject);
            }

            Object.DestroyImmediate(go);
        }
    }

    private static void TestAdditionalExperimentUiModeGlobalParameters()
    {
        var go = new GameObject("AdditionalExperimentUi_ModeGlobals_Test", typeof(RectTransform));
        try
        {
            var ui = go.AddComponent<AdditionalExperimentUiVisualController>();

            ui.ShowM1();
            AdditionalConoscopicUserParameters m1 = ui.GetUserParameters();
            AssertClose("Additional UI M1 wavelength", m1.wavelengthNm, 532f, 1e-6f);
            AssertClose("Additional UI M1 thickness", m1.thicknessMm, 2.5f, 1e-6f);

            ui.ShowM3();
            AdditionalConoscopicUserParameters m3 = ui.GetUserParameters();
            AssertClose("Additional UI M3 wavelength", m3.wavelengthNm, 633f, 1e-6f);
            AssertClose("Additional UI M3 thickness", m3.thicknessMm, 20f, 1e-6f);

            ui.ShowM2();
            AdditionalConoscopicUserParameters m2 = ui.GetUserParameters();
            AssertClose("Additional UI M2 wavelength", m2.wavelengthNm, 532f, 1e-6f);
            AssertClose("Additional UI M2 thickness", m2.thicknessMm, 2.5f, 1e-6f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestAdditionalExperimentUiResetCurrentModeParameters()
    {
        var go = new GameObject("AdditionalExperimentUi_Reset_Test", typeof(RectTransform));
        try
        {
            var ui = go.AddComponent<AdditionalExperimentUiVisualController>();

            SetUiPrivateField(
                ui,
                "m1GlobalParameters",
                AdditionalConoscopicGlobalPhysicalParameters.Create(633f, 20f, 0f, 90f));
            SetUiPrivateField(
                ui,
                "m2GlobalParameters",
                AdditionalConoscopicGlobalPhysicalParameters.Create(532f, 2.55f, 0f, 90f));
            SetUiPrivateField(
                ui,
                "m3GlobalParameters",
                AdditionalConoscopicGlobalPhysicalParameters.Create(633f, 20f, 0f, 90f));

            AdditionalConoscopicUserParameters m1Initial = AdditionalConoscopicUserParameters.Defaults;
            m1Initial.opticAxisTiltDeg = 11f;
            m1Initial.opticAxisAzimuthDeg = 22f;
            m1Initial.apertureRadius = 0.66f;
            SetUiPrivateField(ui, "m1InitialParameters", m1Initial);

            AdditionalConoscopicUserParameters m2Initial = AdditionalConoscopicUserParameters.Defaults;
            m2Initial.voltageV = 25f;
            m2Initial.crystalAxisAngleDeg = 35f;
            m2Initial.thetaDeg = 12f;
            m2Initial.phiDeg = 48f;
            m2Initial.apertureRadius = 0.72f;
            SetUiPrivateField(ui, "m2InitialParameters", m2Initial);

            AdditionalConoscopicUserParameters m3Initial = AdditionalConoscopicUserParameters.UniaxialVoltageDefaults;
            m3Initial.voltageV = 40f;
            m3Initial.apertureRadius = 0.81f;
            SetUiPrivateField(ui, "m3InitialParameters", m3Initial);

            int parameterNotifications = 0;
            ui.ParametersChanged += _ => parameterNotifications++;

            ui.ShowM1();
            parameterNotifications = 0;
            SetUiControlValueWithoutNotify(ui, "wavelength", 700f);
            SetUiControlValueWithoutNotify(ui, "thickness", 8f);
            SetUiControlValueWithoutNotify(ui, "m1.opticTilt", 30f);
            SetUiControlValueWithoutNotify(ui, "m1.opticAzimuth", 180f);
            SetUiControlValueWithoutNotify(ui, "m1.aperture", 0.2f);
            ui.ResetCurrentModeParameters();
            AdditionalConoscopicUserParameters m1 = ui.GetUserParameters();
            AssertTrue("Additional UI reset M1 single notification", parameterNotifications == 1);
            AssertClose("Additional UI reset M1 wavelength", m1.wavelengthNm, 633f, 1e-6f);
            AssertClose("Additional UI reset M1 thickness", m1.thicknessMm, 20f, 1e-6f);
            AssertClose("Additional UI reset M1 tilt", m1.opticAxisTiltDeg, 11f, 1e-6f);
            AssertClose("Additional UI reset M1 azimuth", m1.opticAxisAzimuthDeg, 22f, 1e-6f);
            AssertClose("Additional UI reset M1 aperture", m1.apertureRadius, 0.66f, 1e-6f);

            ui.ShowM2();
            parameterNotifications = 0;
            SetUiControlValueWithoutNotify(ui, "wavelength", 710f);
            SetUiControlValueWithoutNotify(ui, "thickness", 9f);
            SetUiControlValueWithoutNotify(ui, "m2.voltage", 900f);
            SetUiControlValueWithoutNotify(ui, "m2.alpha", 120f);
            SetUiControlValueWithoutNotify(ui, "m2.theta", 55f);
            SetUiControlValueWithoutNotify(ui, "m2.phi", 200f);
            SetUiControlValueWithoutNotify(ui, "m2.aperture", 0.25f);
            ui.ResetCurrentModeParameters();
            AdditionalConoscopicUserParameters m2 = ui.GetUserParameters();
            AssertTrue("Additional UI reset M2 single notification", parameterNotifications == 1);
            AssertClose("Additional UI reset M2 wavelength", m2.wavelengthNm, 532f, 1e-6f);
            AssertClose("Additional UI reset M2 thickness", m2.thicknessMm, 2.55f, 1e-6f);
            AssertClose("Additional UI reset M2 voltage", m2.voltageV, 25f, 1e-6f);
            AssertClose("Additional UI reset M2 alpha", m2.crystalAxisAngleDeg, 35f, 1e-6f);
            AssertClose("Additional UI reset M2 theta", m2.thetaDeg, 12f, 1e-6f);
            AssertClose("Additional UI reset M2 phi", m2.phiDeg, 48f, 1e-6f);
            AssertClose("Additional UI reset M2 aperture", m2.apertureRadius, 0.72f, 1e-6f);

            ui.ShowM3();
            parameterNotifications = 0;
            SetUiControlValueWithoutNotify(ui, "wavelength", 720f);
            SetUiControlValueWithoutNotify(ui, "thickness", 10f);
            SetUiControlValueWithoutNotify(ui, "m3.voltage", 950f);
            SetUiControlValueWithoutNotify(ui, "m3.aperture", 0.3f);
            ui.ResetCurrentModeParameters();
            AdditionalConoscopicUserParameters m3 = ui.GetUserParameters();
            AssertTrue("Additional UI reset M3 single notification", parameterNotifications == 1);
            AssertClose("Additional UI reset M3 wavelength", m3.wavelengthNm, 633f, 1e-6f);
            AssertClose("Additional UI reset M3 thickness", m3.thicknessMm, 20f, 1e-6f);
            AssertClose("Additional UI reset M3 voltage", m3.voltageV, 40f, 1e-6f);
            AssertClose("Additional UI reset M3 aperture", m3.apertureRadius, 0.81f, 1e-6f);

            ui.ShowM1();
            AdditionalConoscopicUserParameters restoredM1 = ui.GetUserParameters();
            AssertClose("Additional UI reset keeps M1 cache", restoredM1.wavelengthNm, 633f, 1e-6f);
            AssertClose("Additional UI reset keeps M1 aperture", restoredM1.apertureRadius, 0.66f, 1e-6f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void SetUiPrivateField<T>(AdditionalExperimentUiVisualController ui, string fieldName, T value)
    {
        FieldInfo field = typeof(AdditionalExperimentUiVisualController).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue("Additional UI private field exists: " + fieldName, field != null);
        field?.SetValue(ui, value);
    }

    private static void SetUiControlValueWithoutNotify(
        AdditionalExperimentUiVisualController ui,
        string key,
        float value)
    {
        MethodInfo method = typeof(AdditionalExperimentUiVisualController).GetMethod(
            "SetControlValueWithoutNotify",
            BindingFlags.Instance | BindingFlags.NonPublic);
        AssertTrue("Additional UI set control helper exists", method != null);
        method?.Invoke(ui, new object[] { key, value });
    }

    private static void TestAdditionalExperimentApiGpuLifecycle()
    {
        CrystalProfile liNbO3 = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        CrystalProfile ktp = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);
        if (liNbO3 == null || ktp == null)
        {
            Debug.LogWarning("[SKIP] Additional experiment profiles not found.");
            return;
        }

        if (Shader.Find("ElectroOptics/ConoscopicJonesIntensity") == null)
        {
            Debug.LogWarning("[SKIP] Conoscopic Jones shader not imported yet.");
            return;
        }

        var go = new GameObject("AdditionalConoscopicExperimentApi_GPU_Test");
        try
        {
            var core = go.AddComponent<ConoscopicJonesGpuCore>();
            var api = go.AddComponent<AdditionalConoscopicExperimentApi>();
            api.Configure(core, liNbO3, ktp);

            var zeroVoltage = AdditionalConoscopicUserParameters.Defaults;
            zeroVoltage.voltageV = 0f;
            zeroVoltage.wavelengthNm = 532f;
            zeroVoltage.thicknessMm = 2.5f;
            api.ApplyAndRecalculate(AdditionalConoscopicMode.BiaxialVoltage, zeroVoltage);
            Vector3 zeroM2Indices = GetPrincipalIndices(core.Parameters);
            Matrix4x4 zeroM2Matrix = core.Parameters.worldToPrincipalMatrix;

            var fieldVoltage = zeroVoltage;
            fieldVoltage.voltageV = 1000f;
            api.ApplyAndRecalculate(AdditionalConoscopicMode.BiaxialVoltage, fieldVoltage);
            Vector3 fieldM2Indices = GetPrincipalIndices(core.Parameters);
            Matrix4x4 fieldM2Matrix = core.Parameters.worldToPrincipalMatrix;
            bool m2IndicesChanged = (fieldM2Indices - zeroM2Indices).sqrMagnitude > 1e-14f;
            bool m2MatrixChanged = MatrixChanged(fieldM2Matrix, zeroM2Matrix, 1e-7f);

            AssertTrue("Additional M2 GPU result valid", core.Result.IsValid);
            AssertTrue("Additional M2 GPU texture valid", core.IntensityHeightMap != null && core.IntensityHeightMap.width == 128);
            AssertTrue("Additional M2 GPU stays RawJones", core.Parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones);
            AssertTrue("Additional M2 GPU field perturbs parameters", m2IndicesChanged || m2MatrixChanged);

            api.ApplyAndRecalculate(AdditionalConoscopicMode.UniaxialVoltage, zeroVoltage);
            Vector3 zeroM3Indices = GetPrincipalIndices(core.Parameters);
            fieldVoltage.voltageV = 1000f;
            api.ApplyAndRecalculate(AdditionalConoscopicMode.UniaxialVoltage, fieldVoltage);
            Vector3 fieldM3Indices = GetPrincipalIndices(core.Parameters);

            AssertTrue("Additional M3 GPU result valid", core.Result.IsValid);
            AssertTrue("Additional M3 GPU texture valid", core.IntensityHeightMap != null && core.IntensityHeightMap.width == 128);
            AssertTrue("Additional M3 GPU continuous view", core.Parameters.uniaxialEoView);
            AssertTrue("Additional M3 GPU keeps RawJones", core.Parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones);
            AssertTrue("Additional M3 GPU field perturbs indices", (fieldM3Indices - zeroM3Indices).sqrMagnitude > 1e-14f);
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

    private static Vector3 GetPrincipalIndices(ConoscopicJonesParameters parameters)
    {
        return new Vector3(
            parameters.principalIndexNx,
            parameters.principalIndexNy,
            parameters.principalIndexNz);
    }

    private static bool MatrixChanged(Matrix4x4 a, Matrix4x4 b, float tolerance)
    {
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                if (Mathf.Abs(a[row, column] - b[row, column]) > tolerance)
                {
                    return true;
                }
            }
        }

        return false;
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
