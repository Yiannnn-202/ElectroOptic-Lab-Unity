using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;
using UnityEngine;

[System.Serializable]
public struct AdditionalConoscopicUserParameters
{
    public float wavelengthNm;
    public float thicknessMm;
    public float voltageV;
    public float crystalAxisAngleDeg;
    public float polarizerAngleDeg;
    public float analyzerAngleDeg;
    public float opticAxisTiltDeg;
    public float opticAxisAzimuthDeg;
    public float thetaDeg;
    public float phiDeg;
    public float apertureRadius;

    public static AdditionalConoscopicUserParameters Defaults => new AdditionalConoscopicUserParameters
    {
        wavelengthNm = 532f,
        thicknessMm = 2.5f,
        voltageV = 0f,
        crystalAxisAngleDeg = 45f,
        polarizerAngleDeg = 0f,
        analyzerAngleDeg = 90f,
        opticAxisTiltDeg = 0f,
        opticAxisAzimuthDeg = 0f,
        thetaDeg = 0f,
        phiDeg = 0f,
        apertureRadius = 1f
    };

    public static AdditionalConoscopicUserParameters UniaxialVoltageDefaults => new AdditionalConoscopicUserParameters
    {
        wavelengthNm = 633f,
        thicknessMm = 20f,
        voltageV = 0f,
        crystalAxisAngleDeg = 45f,
        polarizerAngleDeg = 0f,
        analyzerAngleDeg = 90f,
        opticAxisTiltDeg = 0f,
        opticAxisAzimuthDeg = 0f,
        thetaDeg = 0f,
        phiDeg = 0f,
        apertureRadius = 1f
    };
}

[DisallowMultipleComponent]
public sealed class AdditionalConoscopicExperimentApi : MonoBehaviour
{
    public const float DefaultFieldVmPerVolt = 50000f;
    private const int DefaultResolution = 128;
    private const float DefaultPhaseScale = 0.1f;
    private const float DefaultPhaseAntiAliasStrength = 1f;
    private const float EoSmoothPhaseScale = 0.05f;
    private const float EoSmoothPhaseAntiAliasStrength = 3f;
    private const int EoSmoothSupersampleFactor = 2;
    private const int EoSmoothResolution = 256;
    private const float EoSmoothScreenDistanceM = 0.35f;
    private const float EoSmoothScreenHalfSizeM = 0.08f;
    private const float EoSmoothCrystalAxisAngleDeg = 45f;

    [Header("Core")]
    [SerializeField] private ConoscopicJonesGpuCore core;
    [SerializeField] private CrystalProfile liNbO3Profile;
    [SerializeField] private CrystalProfile ktpProfile;

    [Header("Preset")]
    [SerializeField] private float fieldVmPerVolt = DefaultFieldVmPerVolt;
    [SerializeField] [Range(16, 256)] private int resolution = DefaultResolution;
    [SerializeField] private AdditionalConoscopicVisualizationSettings visualizationSettings;

    private AdditionalConoscopicMode _mode = AdditionalConoscopicMode.Uniaxial;
    private AdditionalConoscopicUserParameters _userParameters = AdditionalConoscopicUserParameters.Defaults;

    public ConoscopicJonesGpuCore Core => EnsureCore();
    public ConoscopicJonesResult Result => Core != null ? Core.Result : null;
    public RenderTexture IntensityTexture => Core != null ? Core.IntensityHeightMap : null;
    public AdditionalConoscopicMode Mode => _mode;
    public float FieldVmPerVolt => fieldVmPerVolt;
    public AdditionalConoscopicVisualizationSettings VisualizationSettings => visualizationSettings;

    public void Configure(ConoscopicJonesGpuCore targetCore, CrystalProfile liNbO3, CrystalProfile ktp)
    {
        Configure(targetCore, liNbO3, ktp, visualizationSettings);
    }

    public void Configure(
        ConoscopicJonesGpuCore targetCore,
        CrystalProfile liNbO3,
        CrystalProfile ktp,
        AdditionalConoscopicVisualizationSettings settings)
    {
        core = targetCore;
        liNbO3Profile = liNbO3;
        ktpProfile = ktp;
        if (settings != null)
        {
            visualizationSettings = settings;
        }
    }

    public void SetProfiles(CrystalProfile liNbO3, CrystalProfile ktp)
    {
        liNbO3Profile = liNbO3;
        ktpProfile = ktp;
    }

    public void SetVisualizationSettings(AdditionalConoscopicVisualizationSettings settings)
    {
        visualizationSettings = settings;
    }

    public void SetMode(AdditionalConoscopicMode mode)
    {
        _mode = mode;
        ApplyCoreState(false);
    }

    public void ApplyUserParameters(AdditionalConoscopicUserParameters userParameters)
    {
        _userParameters = userParameters;
        ApplyCoreState(false);
    }

    public void ApplyAndRecalculate(AdditionalConoscopicMode mode, AdditionalConoscopicUserParameters userParameters)
    {
        _mode = mode;
        _userParameters = userParameters;
        ApplyCoreState(true);
    }

    public void Recalculate()
    {
        ApplyCoreState(true);
    }

    public float VoltageToField(float voltageV)
    {
        return Mathf.Max(0f, voltageV) * Mathf.Max(0f, fieldVmPerVolt);
    }

    public ConoscopicJonesParameters BuildParametersForMode(
        AdditionalConoscopicMode mode,
        AdditionalConoscopicUserParameters userParameters,
        ConoscopicJonesParameters sourceParameters = null)
    {
        var parameters = sourceParameters != null
            ? new ConoscopicJonesParameters(sourceParameters)
            : new ConoscopicJonesParameters();

        ApplySharedDisplayPreset(parameters);
        ApplySharedUserParameters(parameters, userParameters);

        switch (mode)
        {
            case AdditionalConoscopicMode.BiaxialVoltage:
                ApplyBiaxialVoltagePreset(parameters, userParameters);
                break;
            case AdditionalConoscopicMode.UniaxialVoltage:
                ApplyUniaxialVoltagePreset(parameters, userParameters);
                break;
            default:
                ApplyUniaxialPreset(parameters, userParameters);
                break;
        }

        parameters.Clamp();
        return parameters;
    }

    public CrystalProfile ResolveProfile(AdditionalConoscopicMode mode)
    {
        return mode == AdditionalConoscopicMode.BiaxialVoltage
            ? ktpProfile
            : liNbO3Profile;
    }

    private void ApplyCoreState(bool forceRecalculate)
    {
        ConoscopicJonesGpuCore targetCore = EnsureCore();
        if (targetCore == null)
        {
            return;
        }

        CrystalProfile profile = ResolveProfile(_mode);
        if (profile == null)
        {
            Debug.LogError($"[AdditionalConoscopicExperimentApi] Missing CrystalProfile for mode {_mode}.");
            return;
        }

        targetCore.SetProfile(profile);
        targetCore.SetParameters(BuildParametersForMode(_mode, _userParameters, targetCore.Parameters));

        if (forceRecalculate)
        {
            targetCore.ForceRecalculate();
        }
    }

    private ConoscopicJonesGpuCore EnsureCore()
    {
        if (core != null)
        {
            return core;
        }

        core = GetComponent<ConoscopicJonesGpuCore>();
        if (core == null)
        {
            core = gameObject.AddComponent<ConoscopicJonesGpuCore>();
        }

        return core;
    }

    private void ApplySharedDisplayPreset(ConoscopicJonesParameters parameters)
    {
        if (visualizationSettings != null)
        {
            visualizationSettings.ApplyTo(parameters);
            return;
        }

        ApplyFallbackDisplayPreset(parameters);
    }

    private void ApplyFallbackDisplayPreset(ConoscopicJonesParameters parameters)
    {
        parameters.resolution = Mathf.Clamp(resolution, 16, 256);
        parameters.renderSupersampleFactor = 1;
        parameters.screenDistanceM = 0.7f;
        parameters.screenHalfSizeM = 0.08f;
        parameters.initialIntensity = 1f;
        parameters.phaseAntiAliasStrength = DefaultPhaseAntiAliasStrength;
        parameters.phaseScale = DefaultPhaseScale;
        parameters.ringSharpness = 1f;
        parameters.crossWidth = 0.16f;
        parameters.blackCutoff = 0.012f;
        parameters.displayGamma = 1.25f;
        parameters.initialMelatopeOffset = Vector2.zero;
    }

    private void ApplySharedUserParameters(
        ConoscopicJonesParameters parameters,
        AdditionalConoscopicUserParameters userParameters)
    {
        parameters.wavelengthNm = userParameters.wavelengthNm;
        parameters.thicknessMm = userParameters.thicknessMm;
        parameters.polarizerAngleDeg = userParameters.polarizerAngleDeg;
        parameters.analyzerAngleDeg = userParameters.analyzerAngleDeg;
        parameters.apertureRadius = userParameters.apertureRadius;
    }

    private void ApplyUniaxialPreset(
        ConoscopicJonesParameters parameters,
        AdditionalConoscopicUserParameters userParameters)
    {
        parameters.biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones;
        parameters.uniaxialEoView = false;
        parameters.uniaxialEoUsePerturbedAxis = false;
        parameters.forceUniaxial = false;
        parameters.electricFieldStrength = 0f;
        parameters.crystalAxisAngleDeg = 0f;
        parameters.opticAxisTiltDeg = userParameters.opticAxisTiltDeg;
        parameters.opticAxisAzimuthDeg = userParameters.opticAxisAzimuthDeg;
        parameters.paperThetaDeg = 0f;
        parameters.paperPhiDeg = 0f;
    }

    private void ApplyBiaxialVoltagePreset(
        ConoscopicJonesParameters parameters,
        AdditionalConoscopicUserParameters userParameters)
    {
        parameters.biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones;
        parameters.uniaxialEoView = false;
        parameters.uniaxialEoUsePerturbedAxis = false;
        parameters.forceUniaxial = false;
        parameters.electricFieldStrength = VoltageToField(userParameters.voltageV);
        parameters.crystalAxisAngleDeg = userParameters.crystalAxisAngleDeg;
        parameters.opticAxisTiltDeg = userParameters.thetaDeg;
        parameters.opticAxisAzimuthDeg = userParameters.phiDeg;
        parameters.paperThetaDeg = userParameters.thetaDeg;
        parameters.paperPhiDeg = userParameters.phiDeg;
        if (visualizationSettings != null)
        {
            visualizationSettings.ApplyM2ScreenOverrideTo(parameters);
        }
    }

    private void ApplyUniaxialVoltagePreset(
        ConoscopicJonesParameters parameters,
        AdditionalConoscopicUserParameters userParameters)
    {
        parameters.biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones;
        parameters.uniaxialEoView = true;
        parameters.uniaxialEoUsePerturbedAxis = false;
        parameters.forceUniaxial = false;
        parameters.electricFieldStrength = VoltageToField(userParameters.voltageV);
        parameters.crystalAxisAngleDeg = EoSmoothCrystalAxisAngleDeg;
        parameters.opticAxisTiltDeg = 0f;
        parameters.opticAxisAzimuthDeg = 0f;
        parameters.paperThetaDeg = 0f;
        parameters.paperPhiDeg = 0f;

        if (visualizationSettings != null)
        {
            if (visualizationSettings.UseM3SmoothPreset)
            {
                visualizationSettings.ApplyM3SmoothPresetTo(parameters);
            }

            return;
        }

        ApplyFallbackM3SmoothPreset(parameters);
    }

    private void ApplyFallbackM3SmoothPreset(ConoscopicJonesParameters parameters)
    {
        parameters.resolution = EoSmoothResolution;
        parameters.renderSupersampleFactor = EoSmoothSupersampleFactor;
        parameters.screenDistanceM = EoSmoothScreenDistanceM;
        parameters.screenHalfSizeM = EoSmoothScreenHalfSizeM;
        parameters.crystalAxisAngleDeg = EoSmoothCrystalAxisAngleDeg;
        parameters.phaseScale = EoSmoothPhaseScale;
        parameters.phaseAntiAliasStrength = EoSmoothPhaseAntiAliasStrength;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fieldVmPerVolt = Mathf.Max(0f, fieldVmPerVolt);
        resolution = Mathf.Clamp(resolution, 16, 256);
    }
#endif
}
