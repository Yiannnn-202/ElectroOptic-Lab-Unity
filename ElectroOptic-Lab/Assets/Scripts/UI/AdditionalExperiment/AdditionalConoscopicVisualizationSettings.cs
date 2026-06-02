using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AdditionalConoscopicVisualizationSettings : MonoBehaviour
{
    private const int MinPresetResolution = 16;
    private const int MaxPresetResolution = 256;
    private const int MinOutputTextureSize = 256;
    private const int MaxOutputTextureSize = 2048;

    [Header("Compute Screen")]
    [SerializeField] [Range(MinPresetResolution, MaxPresetResolution)] private int resolution = 128;
    [SerializeField] [Range(ConoscopicJonesParameters.MinRenderSupersampleFactor, ConoscopicJonesParameters.MaxRenderSupersampleFactor)] private int renderSupersampleFactor = 1;
    [SerializeField] private float screenDistanceM = 0.7f;
    [SerializeField] private float screenHalfSizeM = 0.08f;

    [Header("Intensity Shape")]
    [SerializeField] private float initialIntensity = 1f;
    [SerializeField] private float phaseScale = 0.1f;
    [SerializeField] private float phaseAntiAliasStrength = 1f;
    [SerializeField] private float ringSharpness = 1f;
    [SerializeField] private float crossWidth = 0.16f;
    [SerializeField] private float blackCutoff = 0.012f;
    [SerializeField] private float displayGamma = 1.25f;

    [Header("Surface View")]
    [SerializeField] private int outputTextureSize = 1024;
    [SerializeField] private float surfaceSize = 5f;
    [SerializeField] private float heightScale = 1.6f;
    [SerializeField] private bool normalizeDisplayIntensity = false;

    [Header("Mode Overrides")]
    [SerializeField] private bool useM3SmoothPreset = true;
    [SerializeField] private bool useM3ProfilePhysicalDefaults = true;
    [SerializeField] [Range(MinPresetResolution, MaxPresetResolution)] private int m3Resolution = 256;
    [SerializeField] [Range(ConoscopicJonesParameters.MinRenderSupersampleFactor, ConoscopicJonesParameters.MaxRenderSupersampleFactor)] private int m3RenderSupersampleFactor = 2;
    [SerializeField] private float m3WavelengthNm = 633f;
    [SerializeField] private float m3ThicknessMm = 20f;
    [SerializeField] private float m3ScreenDistanceM = 0.35f;
    [SerializeField] private float m3ScreenHalfSizeM = 0.08f;
    [SerializeField] private float m3CrystalAxisAngleDeg = 45f;
    [SerializeField] private float m3PhaseScale = 0.05f;
    [SerializeField] private float m3PhaseAntiAliasStrength = 3f;
    [SerializeField] private float m3HeightScale = 0.12f;

    public int Resolution
    {
        get => resolution;
        set
        {
            resolution = value;
            ClampValues();
        }
    }

    public int RenderSupersampleFactor
    {
        get => renderSupersampleFactor;
        set
        {
            renderSupersampleFactor = value;
            ClampValues();
        }
    }

    public float ScreenDistanceM
    {
        get => screenDistanceM;
        set
        {
            screenDistanceM = value;
            ClampValues();
        }
    }

    public float ScreenHalfSizeM
    {
        get => screenHalfSizeM;
        set
        {
            screenHalfSizeM = value;
            ClampValues();
        }
    }

    public float InitialIntensity
    {
        get => initialIntensity;
        set
        {
            initialIntensity = value;
            ClampValues();
        }
    }

    public float PhaseScale
    {
        get => phaseScale;
        set
        {
            phaseScale = value;
            ClampValues();
        }
    }

    public float PhaseAntiAliasStrength
    {
        get => phaseAntiAliasStrength;
        set
        {
            phaseAntiAliasStrength = value;
            ClampValues();
        }
    }

    public float RingSharpness
    {
        get => ringSharpness;
        set
        {
            ringSharpness = value;
            ClampValues();
        }
    }

    public float CrossWidth
    {
        get => crossWidth;
        set
        {
            crossWidth = value;
            ClampValues();
        }
    }

    public float BlackCutoff
    {
        get => blackCutoff;
        set
        {
            blackCutoff = value;
            ClampValues();
        }
    }

    public float DisplayGamma
    {
        get => displayGamma;
        set
        {
            displayGamma = value;
            ClampValues();
        }
    }

    public int OutputTextureSize
    {
        get => outputTextureSize;
        set
        {
            outputTextureSize = value;
            ClampValues();
        }
    }

    public float SurfaceSize
    {
        get => surfaceSize;
        set
        {
            surfaceSize = value;
            ClampValues();
        }
    }

    public float HeightScale
    {
        get => heightScale;
        set
        {
            heightScale = value;
            ClampValues();
        }
    }

    public bool NormalizeDisplayIntensity
    {
        get => normalizeDisplayIntensity;
        set => normalizeDisplayIntensity = value;
    }

    public bool UseM3SmoothPreset
    {
        get => useM3SmoothPreset;
        set => useM3SmoothPreset = value;
    }

    public bool UseM3ProfilePhysicalDefaults
    {
        get => useM3ProfilePhysicalDefaults;
        set => useM3ProfilePhysicalDefaults = value;
    }

    public float M3HeightScale => m3HeightScale;

    public void ApplyTo(ConoscopicJonesParameters parameters)
    {
        if (parameters == null)
        {
            return;
        }

        ClampValues();
        parameters.resolution = resolution;
        parameters.renderSupersampleFactor = renderSupersampleFactor;
        parameters.screenDistanceM = screenDistanceM;
        parameters.screenHalfSizeM = screenHalfSizeM;
        parameters.initialIntensity = initialIntensity;
        parameters.phaseScale = phaseScale;
        parameters.phaseAntiAliasStrength = phaseAntiAliasStrength;
        parameters.ringSharpness = ringSharpness;
        parameters.crossWidth = crossWidth;
        parameters.blackCutoff = blackCutoff;
        parameters.displayGamma = displayGamma;
        parameters.initialMelatopeOffset = Vector2.zero;
    }

    public void ApplyM3SmoothPresetTo(ConoscopicJonesParameters parameters, CrystalProfile profile)
    {
        if (parameters == null)
        {
            return;
        }

        ClampValues();
        parameters.resolution = m3Resolution;
        parameters.renderSupersampleFactor = m3RenderSupersampleFactor;
        parameters.wavelengthNm = ResolveProfileFloat(
            profile != null ? profile.defaultWavelength_nm : double.NaN,
            m3WavelengthNm,
            useM3ProfilePhysicalDefaults);
        parameters.thicknessMm = ResolveProfileFloat(
            profile != null ? profile.defaultLength_mm : double.NaN,
            m3ThicknessMm,
            useM3ProfilePhysicalDefaults);
        parameters.screenDistanceM = m3ScreenDistanceM;
        parameters.screenHalfSizeM = m3ScreenHalfSizeM;
        parameters.crystalAxisAngleDeg = m3CrystalAxisAngleDeg;
        parameters.phaseScale = m3PhaseScale;
        parameters.phaseAntiAliasStrength = m3PhaseAntiAliasStrength;
    }

    public float ResolveHeightScale(ConoscopicJonesParameters parameters)
    {
        ClampValues();
        if (useM3SmoothPreset
            && parameters != null
            && parameters.uniaxialEoView
            && parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.RawJones)
        {
            return m3HeightScale;
        }

        return heightScale;
    }

    private void ClampValues()
    {
        resolution = Mathf.Clamp(resolution, MinPresetResolution, MaxPresetResolution);
        renderSupersampleFactor = Mathf.Clamp(
            renderSupersampleFactor,
            ConoscopicJonesParameters.MinRenderSupersampleFactor,
            ConoscopicJonesParameters.MaxRenderSupersampleFactor);
        screenDistanceM = Mathf.Clamp(
            screenDistanceM,
            ConoscopicJonesParameters.MinScreenDistanceM,
            ConoscopicJonesParameters.MaxScreenDistanceM);
        screenHalfSizeM = Mathf.Clamp(
            screenHalfSizeM,
            ConoscopicJonesParameters.MinScreenHalfSizeM,
            ConoscopicJonesParameters.MaxScreenHalfSizeM);
        initialIntensity = Mathf.Clamp(
            initialIntensity,
            ConoscopicJonesParameters.MinInitialIntensity,
            ConoscopicJonesParameters.MaxInitialIntensity);
        phaseScale = Mathf.Clamp(
            phaseScale,
            ConoscopicJonesParameters.MinPhaseScale,
            ConoscopicJonesParameters.MaxPhaseScale);
        phaseAntiAliasStrength = Mathf.Clamp(
            phaseAntiAliasStrength,
            ConoscopicJonesParameters.MinPhaseAntiAliasStrength,
            ConoscopicJonesParameters.MaxPhaseAntiAliasStrength);
        ringSharpness = Mathf.Clamp(
            ringSharpness,
            ConoscopicJonesParameters.MinRingSharpness,
            ConoscopicJonesParameters.MaxRingSharpness);
        crossWidth = Mathf.Clamp(
            crossWidth,
            ConoscopicJonesParameters.MinCrossWidth,
            ConoscopicJonesParameters.MaxCrossWidth);
        blackCutoff = Mathf.Clamp(
            blackCutoff,
            ConoscopicJonesParameters.MinBlackCutoff,
            ConoscopicJonesParameters.MaxBlackCutoff);
        displayGamma = Mathf.Clamp(
            displayGamma,
            ConoscopicJonesParameters.MinDisplayGamma,
            ConoscopicJonesParameters.MaxDisplayGamma);
        outputTextureSize = Mathf.Clamp(outputTextureSize, MinOutputTextureSize, MaxOutputTextureSize);
        surfaceSize = Mathf.Max(0.1f, surfaceSize);
        heightScale = Mathf.Max(0.05f, heightScale);
        m3Resolution = Mathf.Clamp(m3Resolution, MinPresetResolution, MaxPresetResolution);
        m3RenderSupersampleFactor = Mathf.Clamp(
            m3RenderSupersampleFactor,
            ConoscopicJonesParameters.MinRenderSupersampleFactor,
            ConoscopicJonesParameters.MaxRenderSupersampleFactor);
        m3WavelengthNm = Mathf.Clamp(
            m3WavelengthNm,
            ConoscopicJonesParameters.MinWavelengthNm,
            ConoscopicJonesParameters.MaxWavelengthNm);
        m3ThicknessMm = Mathf.Clamp(
            m3ThicknessMm,
            ConoscopicJonesParameters.MinThicknessMm,
            ConoscopicJonesParameters.MaxThicknessMm);
        m3ScreenDistanceM = Mathf.Clamp(
            m3ScreenDistanceM,
            ConoscopicJonesParameters.MinScreenDistanceM,
            ConoscopicJonesParameters.MaxScreenDistanceM);
        m3ScreenHalfSizeM = Mathf.Clamp(
            m3ScreenHalfSizeM,
            ConoscopicJonesParameters.MinScreenHalfSizeM,
            ConoscopicJonesParameters.MaxScreenHalfSizeM);
        m3CrystalAxisAngleDeg = Mathf.Clamp(m3CrystalAxisAngleDeg, 0f, 180f);
        m3PhaseScale = Mathf.Clamp(
            m3PhaseScale,
            ConoscopicJonesParameters.MinPhaseScale,
            ConoscopicJonesParameters.MaxPhaseScale);
        m3PhaseAntiAliasStrength = Mathf.Clamp(
            m3PhaseAntiAliasStrength,
            ConoscopicJonesParameters.MinPhaseAntiAliasStrength,
            ConoscopicJonesParameters.MaxPhaseAntiAliasStrength);
        m3HeightScale = Mathf.Max(0.05f, m3HeightScale);
    }

    private static float ResolveProfileFloat(double profileValue, float fallback, bool preferProfile)
    {
        if (preferProfile
            && !double.IsNaN(profileValue)
            && !double.IsInfinity(profileValue)
            && profileValue > 0.0)
        {
            return (float)profileValue;
        }

        return fallback;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampValues();
    }
#endif
}
