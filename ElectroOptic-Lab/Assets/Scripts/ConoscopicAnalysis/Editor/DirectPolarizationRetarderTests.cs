#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;

public static class DirectPolarizationRetarderTests
{
    private const string LiNbO3ProfilePath = "Assets/LiNbO3_Profile.asset";
    private const string KtpProfilePath = "Assets/KTP_Profile.asset";
    private static int _passed;
    private static int _failed;

    [MenuItem("ElectroOptics/Tests/Run Direct Polarization Retarder Tests")]
    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== Direct Polarization Retarder Tests Start ==========");
        TestPolarizerTransmission();
        TestRetarderBetweenCrossedPolarizers();
        TestEllipticalStokesThroughAnalyzer();
        TestDirectCenterRayPathFactor();
        TestUniaxialRetarderUsesRotationMatrix();
        TestProfileRetarderEigenSystems();
        Debug.Log($"========== Direct Polarization Retarder Tests Done: {_passed} passed, {_failed} failed ==========");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(_failed == 0 ? 0 : 1);
        }
    }

    private static void TestPolarizerTransmission()
    {
        LightData horizontal = LightData.FromLinear(1f, 0f);
        AssertClose("Parallel polarizer transmits", Analyze(horizontal, 0f), 1f, 1e-6f);
        AssertClose("Crossed polarizer extinguishes", Analyze(horizontal, 90f), 0f, 1e-6f);
    }

    private static void TestRetarderBetweenCrossedPolarizers()
    {
        LightData horizontal = LightData.FromLinear(1f, 0f);
        LightData noRetardance = CrystalRetarderPhysics.ApplyLinearRetarder(horizontal, 45f, 0f);
        LightData halfWave = CrystalRetarderPhysics.ApplyLinearRetarder(horizontal, 45f, Mathf.PI);
        LightData quarterWave = CrystalRetarderPhysics.ApplyLinearRetarder(horizontal, 45f, Mathf.PI * 0.5f);
        LightData alignedHalfWave = CrystalRetarderPhysics.ApplyLinearRetarder(horizontal, 0f, Mathf.PI);

        AssertClose("No retardance remains extinguished", Analyze(noRetardance, 90f), 0f, 1e-6f);
        AssertClose("Aligned half-wave remains extinguished", Analyze(alignedHalfWave, 90f), 0f, 1e-6f);
        AssertClose("45 degree half-wave restores crossed signal", Analyze(halfWave, 90f), 1f, 1e-5f);
        AssertClose("45 degree quarter-wave gives mid crossed signal", Analyze(quarterWave, 90f), 0.5f, 1e-5f);
    }

    private static void TestEllipticalStokesThroughAnalyzer()
    {
        LightData circular = LightData.FromStokes(1f, 0f, 0f, 1f);
        float horizontal = Analyze(circular, 0f);
        float vertical = Analyze(circular, 90f);

        AssertTrue("Circular horizontal analyzer finite", IsUnitFinite(horizontal));
        AssertTrue("Circular vertical analyzer finite", IsUnitFinite(vertical));
        AssertClose("Circular has equal linear analyzer power", horizontal, vertical, 1e-6f);
    }

    private static void TestDirectCenterRayPathFactor()
    {
        var parameters = new ConoscopicJonesParameters
        {
            wavelengthNm = 1000f,
            thicknessMm = 0.001f,
            worldToPrincipalMatrix = Matrix4x4.identity,
            biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones,
            phaseScale = 1f,
            uniaxialEpsilon = 0.000001f
        };
        parameters.ApplyPrincipalIndices(new Vector3(1.5f, 1.6f, 1.7f));

        bool ok = ConoscopicJonesCpuReference.TryGetRetarderEigenSystem(
            Vector3.right,
            parameters,
            out Vector3 _,
            out Vector3 _,
            out float delta);

        float expected = 2f * Mathf.PI * 0.001f * 1e-3f * 0.1f / (1000f * 1e-9f);
        AssertTrue("Direct center ray solves away from view Z", ok);
        AssertClose("Direct center ray uses unit path factor", delta, expected, 1e-3f);
    }

    private static void TestUniaxialRetarderUsesRotationMatrix()
    {
        var parameters = new ConoscopicJonesParameters
        {
            wavelengthNm = 1000f,
            thicknessMm = 0.001f,
            worldToPrincipalMatrix = Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)).transpose,
            forceUniaxial = true
        };
        parameters.ApplyPrincipalIndices(new Vector3(1.5f, 1.5f, 1.6f));

        bool ok = ConoscopicJonesCpuReference.TryGetRetarderEigenSystem(
            Vector3.forward,
            parameters,
            out Vector3 _,
            out Vector3 eigenB,
            out float _);

        AssertTrue("Uniaxial rotated retarder solve succeeds", ok);
        AssertTrue("Uniaxial retarder axis follows rotation matrix", Mathf.Abs(Vector3.Dot(eigenB.normalized, Vector3.up)) > 0.99f);
    }

    private static void TestProfileRetarderEigenSystems()
    {
        AssertProfileRetarderFinite(LiNbO3ProfilePath, "LiNbO3");
        AssertProfileRetarderFinite(KtpProfilePath, "KTP");
    }

    private static void AssertProfileRetarderFinite(string path, string label)
    {
        CrystalProfile profile = AssetDatabase.LoadAssetAtPath<CrystalProfile>(path);
        if (profile == null)
        {
            Debug.LogWarning($"[SKIP] {label} profile not found at {path}");
            return;
        }

        var parameters = new ConoscopicJonesParameters
        {
            wavelengthNm = (float)profile.defaultWavelength_nm,
            thicknessMm = (float)profile.defaultLength_mm,
            worldToPrincipalMatrix = Matrix4x4.identity,
            biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones,
            phaseScale = 1f
        };
        parameters.ApplyPrincipalIndices(new Vector3((float)profile.n_x, (float)profile.n_y, (float)profile.n_z));

        bool ok = ConoscopicJonesCpuReference.TryGetRetarderEigenSystem(
            Vector3.forward,
            parameters,
            out Vector3 eigenA,
            out Vector3 eigenB,
            out float delta);

        AssertTrue($"{label} retarder solve succeeds", ok);
        AssertTrue($"{label} retarder axis A finite", IsFiniteVector(eigenA));
        AssertTrue($"{label} retarder axis B finite", IsFiniteVector(eigenB));
        AssertTrue($"{label} retarder delta finite", !float.IsNaN(delta) && !float.IsInfinity(delta));
    }

    private static float Analyze(LightData light, float analyzerAngleDeg)
    {
        float angleRad = analyzerAngleDeg * Mathf.Deg2Rad;
        return Mathf.Max(0f, 0.5f * (
            light.intensity
            + light.stokesQ * Mathf.Cos(2f * angleRad)
            + light.stokesU * Mathf.Sin(2f * angleRad)));
    }

    private static bool IsUnitFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }

    private static bool IsFiniteVector(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
               && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
               && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
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
