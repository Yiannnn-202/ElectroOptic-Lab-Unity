using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.Experiment.Renderer
{
    public enum Scene2BiaxialDisplayMode
    {
        TeachingMask = 0,
        RawJones = 1,
        PaperKtpPreset = 2
    }

    /// <summary>
    /// Renders the Scene2 conoscopic preview with the Jones intensity core, then maps intensity to a red/black texture.
    /// </summary>
    public class ConoscopicTextureRenderer : MonoBehaviour
    {
        private const string SHADER_NAME = "ElectroOptics/ConoscopicJonesIntensity";
        private const int JONES_INTENSITY_PASS = 0;
        private const int RED_BLACK_MAPPING_PASS = 1;
        private const float MIN_DISPLAY_GAMMA = 0.1f;
        private const float MAX_BLACK_CUTOFF = 0.25f;
        private const float MIN_RING_SHARPNESS = 0.01f;
        private const float MIN_CROSS_WIDTH = 0.001f;
        private const float MAX_CROSS_WIDTH = 0.9f;
        private const float MIN_PHASE_SCALE = 0.01f;
        private const float MIN_SCREEN_DISTANCE_M = 0.01f;
        private const float MIN_SCREEN_HALF_SIZE_M = 0.005f;
        private const float MIN_OUTPUT_GAMMA = 0.1f;
        private const float MAX_OUTPUT_BLACK_CUTOFF = 0.95f;
        private const float MIN_OPTIC_AXIS_SQR_MAGNITUDE = 0.000001f;
        private const float MAX_INITIAL_MELATOPE_OFFSET = 0.25f;
        private const float UNIAXIAL_EPSILON = 0.0005f;
        private const float PAPER_KTP_WAVELENGTH_NM = 589.3f;
        private const float PAPER_KTP_THICKNESS_MM = 0.915f;
        private const float PAPER_KTP_SCREEN_DISTANCE_M = 0.7f;
        private const float PAPER_KTP_SCREEN_HALF_SIZE_M = 0.08f;
        private const float PAPER_KTP_PHASE_SCALE = 1f;
        private const float PAPER_KTP_RING_SHARPNESS = 1.6f;
        private const float PAPER_KTP_CROSS_WIDTH = 0.08f;
        private const float PAPER_KTP_BLACK_CUTOFF = 0.01f;
        private const float PAPER_KTP_DISPLAY_GAMMA = 1.15f;
        private const float PAPER_KTP_ALPHA_DEG = 45f;
        private const float PAPER_KTP_THETA_DEG = 0f;
        private const float PAPER_KTP_PHI_DEG = 0f;
        private static readonly Vector2 DEFAULT_INITIAL_MELATOPE_OFFSET = new Vector2(0.035f, -0.025f);

        private RenderTexture _renderTexture;
        private RenderTexture _jonesIntensityTexture;
        private Material _jonesMaterial;
        private CrystalPhysicalCore _sourcePhysicalCore;

        private int _textureSize = 512;
        private float _phaseScale = 1f;
        private float _displayGamma = 1.15f;
        private float _blackCutoff = 0.01f;
        private float _ringSharpness = 1.6f;
        private float _crossWidth = 0.08f;
        private float _screenDistanceM = 1.26f;
        private float _screenHalfSizeM = 0.08f;
        private float _phaseAntiAliasStrength = 1f;
        private bool _overrideRawJonesPhysicalParameters;
        private float _rawJonesWavelengthNm = PAPER_KTP_WAVELENGTH_NM;
        private float _rawJonesThicknessMm = PAPER_KTP_THICKNESS_MM;
        private float _rawJonesDisplayGain = 1.6f;
        private float _rawJonesDisplayGamma = 1.2f;
        private float _rawJonesOutputBlackCutoff = 0f;
        private float _rawJonesOutputWhitePoint = 1f;
        private float _rawJonesCrystalAxisAngleDeg = 45f;
        private bool _enableReliefShading;
        private float _reliefStrength;
        private Scene2BiaxialDisplayMode _biaxialDisplayMode = Scene2BiaxialDisplayMode.PaperKtpPreset;
        private bool _usePaperKtpPresetForKtp = true;
        private Vector2 _initialMelatopeOffset = DEFAULT_INITIAL_MELATOPE_OFFSET;
        private Color _laserColor = Color.red;

        private float _polarizerAngleDeg = 0f;
        private float _analyzerAngleDeg = 90f;

        private bool _isInitialized;
        private Scene2BiaxialDisplayMode _lastActiveBiaxialDisplayMode = Scene2BiaxialDisplayMode.RawJones;

        public RenderTexture RenderTexture => _renderTexture;
        public bool IsInitialized => _isInitialized;

        public void Initialize(
            CrystalPhysicalCore sourcePhysicalCore,
            int textureSize = 512,
            float fov = 10f,
            Color laserColor = default,
            float phaseScale = 1f,
            float displayGamma = 1.15f,
            float blackCutoff = 0.01f,
            float ringSharpness = 1.6f,
            float crossWidth = 0.08f)
        {
            Initialize(
                sourcePhysicalCore,
                textureSize,
                fov,
                laserColor,
                phaseScale,
                displayGamma,
                blackCutoff,
                ringSharpness,
                crossWidth,
                DEFAULT_INITIAL_MELATOPE_OFFSET);
        }

        public void Initialize(
            CrystalPhysicalCore sourcePhysicalCore,
            int textureSize,
            float fov,
            Color laserColor,
            float phaseScale,
            float displayGamma,
            float blackCutoff,
            float ringSharpness,
            float crossWidth,
            Vector2 initialMelatopeOffset)
        {
            Initialize(
                sourcePhysicalCore,
                textureSize,
                fov,
                laserColor,
                phaseScale,
                displayGamma,
                blackCutoff,
                ringSharpness,
                crossWidth,
                initialMelatopeOffset,
                1.26f,
                0.08f,
                1f,
                false,
                0f,
                Scene2BiaxialDisplayMode.PaperKtpPreset,
                true);
        }

        public void Initialize(
            CrystalPhysicalCore sourcePhysicalCore,
            int textureSize,
            float fov,
            Color laserColor,
            float phaseScale,
            float displayGamma,
            float blackCutoff,
            float ringSharpness,
            float crossWidth,
            Vector2 initialMelatopeOffset,
            float screenDistanceM,
            float screenHalfSizeM,
            float phaseAntiAliasStrength,
            bool enableReliefShading,
            float reliefStrength)
        {
            Initialize(
                sourcePhysicalCore,
                textureSize,
                fov,
                laserColor,
                phaseScale,
                displayGamma,
                blackCutoff,
                ringSharpness,
                crossWidth,
                initialMelatopeOffset,
                screenDistanceM,
                screenHalfSizeM,
                phaseAntiAliasStrength,
                enableReliefShading,
                reliefStrength,
                Scene2BiaxialDisplayMode.PaperKtpPreset,
                true);
        }

        public void Initialize(
            CrystalPhysicalCore sourcePhysicalCore,
            int textureSize,
            float fov,
            Color laserColor,
            float phaseScale,
            float displayGamma,
            float blackCutoff,
            float ringSharpness,
            float crossWidth,
            Vector2 initialMelatopeOffset,
            float screenDistanceM,
            float screenHalfSizeM,
            float phaseAntiAliasStrength,
            bool enableReliefShading,
            float reliefStrength,
            Scene2BiaxialDisplayMode biaxialDisplayMode,
            bool usePaperKtpPresetForKtp)
        {
            _sourcePhysicalCore = sourcePhysicalCore;
            _textureSize = Mathf.Max(16, textureSize);
            _phaseScale = Mathf.Max(MIN_PHASE_SCALE, phaseScale);
            _displayGamma = Mathf.Max(MIN_DISPLAY_GAMMA, displayGamma);
            _blackCutoff = Mathf.Clamp(blackCutoff, 0f, MAX_BLACK_CUTOFF);
            _ringSharpness = Mathf.Max(MIN_RING_SHARPNESS, ringSharpness);
            _crossWidth = Mathf.Clamp(crossWidth, MIN_CROSS_WIDTH, MAX_CROSS_WIDTH);
            _initialMelatopeOffset = ClampInitialMelatopeOffset(initialMelatopeOffset);
            _screenDistanceM = Mathf.Max(MIN_SCREEN_DISTANCE_M, screenDistanceM);
            _screenHalfSizeM = Mathf.Max(MIN_SCREEN_HALF_SIZE_M, screenHalfSizeM);
            _phaseAntiAliasStrength = Mathf.Max(0f, phaseAntiAliasStrength);
            _enableReliefShading = enableReliefShading;
            _reliefStrength = Mathf.Max(0f, reliefStrength);
            _biaxialDisplayMode = biaxialDisplayMode;
            _usePaperKtpPresetForKtp = usePaperKtpPresetForKtp;
            _laserColor = laserColor == default ? Color.red : laserColor;

            CreateRenderTextures();
            EnsureMaterial();

            _isInitialized = _renderTexture != null && _jonesIntensityTexture != null && _jonesMaterial != null;
            Debug.Log($"[ConoscopicTextureRenderer] Initialized Jones red/black renderer, texture size: {_textureSize}x{_textureSize}");
        }

        private void CreateRenderTextures()
        {
            ReleaseRenderTexture(ref _renderTexture);
            ReleaseRenderTexture(ref _jonesIntensityTexture);

            _renderTexture = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.ARGB32)
            {
                name = "Conoscopic Jones RedBlack Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            _renderTexture.Create();
            ClearRenderTexture(_renderTexture);

            RenderTextureFormat intensityFormat = ResolveSupportedIntensityFormat();

            _jonesIntensityTexture = new RenderTexture(_textureSize, _textureSize, 0, intensityFormat)
            {
                name = "Conoscopic Jones Intensity Intermediate",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            _jonesIntensityTexture.Create();
            ClearRenderTexture(_jonesIntensityTexture);
        }

        private static RenderTextureFormat ResolveSupportedIntensityFormat()
        {
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
            {
                return RenderTextureFormat.ARGBFloat;
            }

            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                return RenderTextureFormat.ARGBHalf;
            }

            return RenderTextureFormat.ARGB32;
        }

        private bool EnsureMaterial()
        {
            Shader shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"[ConoscopicTextureRenderer] Shader not found: {SHADER_NAME}");
                return false;
            }

            if (_jonesMaterial == null || _jonesMaterial.shader != shader)
            {
                if (_jonesMaterial != null)
                {
                    Object.Destroy(_jonesMaterial);
                }

                _jonesMaterial = new Material(shader)
                {
                    name = "Scene2 Jones Conoscopic RedBlack Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return true;
        }

        public void UpdateAndRender()
        {
            if (!_isInitialized || _sourcePhysicalCore == null || !EnsureMaterial())
            {
                return;
            }

            UploadJonesShaderProperties();
            Graphics.Blit(null, _jonesIntensityTexture, _jonesMaterial, JONES_INTENSITY_PASS);

            UploadDisplayMappingProperties();
            Graphics.Blit(_jonesIntensityTexture, _renderTexture, _jonesMaterial, RED_BLACK_MAPPING_PASS);

#if UNITY_EDITOR
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[ConoscopicTextureRenderer] Shader angles — " +
                          $"polarizer={_polarizerAngleDeg:F1}°, analyzer={_analyzerAngleDeg:F1}°, " +
                          $"displayMode={_lastActiveBiaxialDisplayMode}");
            }
#endif
        }

        private void UploadJonesShaderProperties()
        {
            Vector3 indices = ResolvePrincipalIndices();
            bool isBiaxial = IsBiaxial(indices);
            var profile = _sourcePhysicalCore.CurrentConfig.profile;
            Scene2BiaxialDisplayMode activeMode = ResolveActiveBiaxialDisplayMode(profile, isBiaxial);
            _lastActiveBiaxialDisplayMode = activeMode;
            Matrix4x4 matrix = activeMode == Scene2BiaxialDisplayMode.PaperKtpPreset
                ? CreatePaperKtpWorldToPrincipalMatrix(PAPER_KTP_ALPHA_DEG, PAPER_KTP_THETA_DEG, PAPER_KTP_PHI_DEG)
                : _sourcePhysicalCore.ShaderWorldToPrincipalMatrix;
            if (activeMode == Scene2BiaxialDisplayMode.RawJones)
            {
                matrix = CreateInPlaneWorldToPrincipalMatrix(_rawJonesCrystalAxisAngleDeg) * matrix;
            }
            Vector3 opticAxisView = DeriveOpticAxisView(matrix);
            ResolveProfileDefaults(indices, out float wavelengthNm, out float thicknessMm, out float ordinaryIndex, out float extraordinaryIndex, out float electroOpticCoefficientR22);
            ResolveDisplayParameters(
                activeMode,
                ref wavelengthNm,
                ref thicknessMm,
                out float phaseScale,
                out float ringSharpness,
                out float crossWidth,
                out float blackCutoff,
                out float displayGamma,
                out float screenDistanceM,
                out float screenHalfSizeM);

            _jonesMaterial.SetFloat("_WavelengthM", wavelengthNm * 1e-9f);
            _jonesMaterial.SetFloat("_ThicknessM", thicknessMm * 1e-3f);
            _jonesMaterial.SetFloat("_OrdinaryIndexNo", ordinaryIndex);
            _jonesMaterial.SetFloat("_ExtraordinaryIndexNe", extraordinaryIndex);
            _jonesMaterial.SetVector("_PrincipalIndices", new Vector4(indices.x, indices.y, indices.z, 0f));
            _jonesMaterial.SetMatrix("_WorldToPrincipalMatrix", matrix);
            _jonesMaterial.SetFloat("_UseBiaxial", isBiaxial ? 1f : 0f);
            _jonesMaterial.SetFloat("_BiaxialDisplayMode", (float)activeMode);
            _jonesMaterial.SetVector("_BiaxialAxesView", DeriveBiaxialAxesView(indices, matrix));
            _jonesMaterial.SetVector("_InitialMelatopeOffset", new Vector4(_initialMelatopeOffset.x, _initialMelatopeOffset.y, 0f, 0f));
            _jonesMaterial.SetFloat("_PhaseScale", phaseScale);
            _jonesMaterial.SetFloat("_RingSharpness", ringSharpness);
            _jonesMaterial.SetFloat("_CrossWidth", crossWidth);
            _jonesMaterial.SetFloat("_BlackCutoff", blackCutoff);
            _jonesMaterial.SetFloat("_DisplayGamma", displayGamma);
            _jonesMaterial.SetFloat("_UniaxialEpsilon", UNIAXIAL_EPSILON);
            _jonesMaterial.SetFloat("_ScreenDistanceM", screenDistanceM);
            _jonesMaterial.SetFloat("_ScreenHalfSizeM", screenHalfSizeM);
            _jonesMaterial.SetFloat("_InitialIntensity", 1f);
            _jonesMaterial.SetFloat("_PhaseAntiAliasStrength", _phaseAntiAliasStrength);
            _jonesMaterial.SetFloat("_PolarizerAngleRad", _polarizerAngleDeg * Mathf.Deg2Rad);
            _jonesMaterial.SetFloat("_AnalyzerAngleRad", _analyzerAngleDeg * Mathf.Deg2Rad);
            _jonesMaterial.SetFloat("_CrystalAxisAngleRad", ResolveCrystalAxisAngleDeg(activeMode) * Mathf.Deg2Rad);
            _jonesMaterial.SetFloat("_OpticAxisTiltRad", Mathf.Acos(Mathf.Clamp(opticAxisView.z, -1f, 1f)));
            _jonesMaterial.SetFloat("_OpticAxisAzimuthRad", Mathf.Atan2(opticAxisView.y, opticAxisView.x));
            _jonesMaterial.SetFloat("_ElectricFieldStrength", 0f);
            _jonesMaterial.SetFloat("_ElectroOpticCoefficientR22", electroOpticCoefficientR22);
            _jonesMaterial.SetFloat("_ApertureRadius", 1f);
        }

        private void UploadDisplayMappingProperties()
        {
            _jonesMaterial.SetColor("_BaseColor", _laserColor);
            bool rawJonesOutput = _lastActiveBiaxialDisplayMode == Scene2BiaxialDisplayMode.RawJones;
            _jonesMaterial.SetFloat("_IntensityGain", rawJonesOutput ? _rawJonesDisplayGain : 1f);
            _jonesMaterial.SetFloat("_OutputGamma", rawJonesOutput ? _rawJonesDisplayGamma : 1f);
            _jonesMaterial.SetFloat("_OutputBlackCutoff", rawJonesOutput ? _rawJonesOutputBlackCutoff : 0f);
            _jonesMaterial.SetFloat("_OutputWhitePoint", rawJonesOutput ? _rawJonesOutputWhitePoint : 1f);
            _jonesMaterial.SetFloat("_EnableReliefShading", _enableReliefShading ? 1f : 0f);
            _jonesMaterial.SetFloat("_ReliefStrength", _reliefStrength);
        }

        private Vector3 ResolvePrincipalIndices()
        {
            Vector3 indices = _sourcePhysicalCore.NewPrincipalIndices;
            if (IsFinitePositive(indices.x) && IsFinitePositive(indices.y) && IsFinitePositive(indices.z))
            {
                return indices;
            }

            var profile = _sourcePhysicalCore.CurrentConfig.profile;
            if (profile != null)
            {
                return new Vector3((float)profile.n_x, (float)profile.n_y, (float)profile.n_z);
            }

            return new Vector3(2.286f, 2.286f, 2.2f);
        }

        private void ResolveProfileDefaults(
            Vector3 indices,
            out float wavelengthNm,
            out float thicknessMm,
            out float ordinaryIndex,
            out float extraordinaryIndex,
            out float electroOpticCoefficientR22)
        {
            wavelengthNm = 632.8f;
            thicknessMm = 20f;
            ordinaryIndex = ResolveOrdinaryIndex(indices);
            extraordinaryIndex = ResolveExtraordinaryIndex(indices);
            electroOpticCoefficientR22 = 0f;

            var profile = _sourcePhysicalCore.CurrentConfig.profile;
            if (profile == null)
            {
                return;
            }

            if (profile.defaultWavelength_nm > 0)
            {
                wavelengthNm = (float)profile.defaultWavelength_nm;
            }

            if (profile.defaultLength_mm > 0)
            {
                thicknessMm = (float)profile.defaultLength_mm;
            }

            electroOpticCoefficientR22 = (float)profile.r22;
        }

        private Scene2BiaxialDisplayMode ResolveActiveBiaxialDisplayMode(CrystalProfile profile, bool isBiaxial)
        {
            if (!isBiaxial)
            {
                return Scene2BiaxialDisplayMode.RawJones;
            }

            if (_biaxialDisplayMode == Scene2BiaxialDisplayMode.PaperKtpPreset)
            {
                return _usePaperKtpPresetForKtp && IsKtpProfile(profile)
                    ? Scene2BiaxialDisplayMode.PaperKtpPreset
                    : Scene2BiaxialDisplayMode.RawJones;
            }

            return _biaxialDisplayMode;
        }

        private void ResolveDisplayParameters(
            Scene2BiaxialDisplayMode activeMode,
            ref float wavelengthNm,
            ref float thicknessMm,
            out float phaseScale,
            out float ringSharpness,
            out float crossWidth,
            out float blackCutoff,
            out float displayGamma,
            out float screenDistanceM,
            out float screenHalfSizeM)
        {
            if (activeMode == Scene2BiaxialDisplayMode.PaperKtpPreset)
            {
                wavelengthNm = PAPER_KTP_WAVELENGTH_NM;
                thicknessMm = PAPER_KTP_THICKNESS_MM;
                phaseScale = PAPER_KTP_PHASE_SCALE;
                ringSharpness = PAPER_KTP_RING_SHARPNESS;
                crossWidth = PAPER_KTP_CROSS_WIDTH;
                blackCutoff = PAPER_KTP_BLACK_CUTOFF;
                displayGamma = PAPER_KTP_DISPLAY_GAMMA;
                screenDistanceM = PAPER_KTP_SCREEN_DISTANCE_M;
                screenHalfSizeM = PAPER_KTP_SCREEN_HALF_SIZE_M;
                return;
            }

            if (activeMode == Scene2BiaxialDisplayMode.RawJones && _overrideRawJonesPhysicalParameters)
            {
                wavelengthNm = _rawJonesWavelengthNm;
                thicknessMm = _rawJonesThicknessMm;
            }

            phaseScale = _phaseScale;
            ringSharpness = _ringSharpness;
            crossWidth = _crossWidth;
            blackCutoff = _blackCutoff;
            displayGamma = _displayGamma;
            screenDistanceM = _screenDistanceM;
            screenHalfSizeM = _screenHalfSizeM;
        }

        private static float ResolveOrdinaryIndex(Vector3 indices)
        {
            if (Mathf.Abs(indices.x - indices.y) < UNIAXIAL_EPSILON)
            {
                return Average(indices.x, indices.y);
            }

            if (Mathf.Abs(indices.x - indices.z) < UNIAXIAL_EPSILON)
            {
                return Average(indices.x, indices.z);
            }

            if (Mathf.Abs(indices.y - indices.z) < UNIAXIAL_EPSILON)
            {
                return Average(indices.y, indices.z);
            }

            return Mathf.Min(indices.x, Mathf.Min(indices.y, indices.z));
        }

        private static float ResolveExtraordinaryIndex(Vector3 indices)
        {
            if (Mathf.Abs(indices.x - indices.y) < UNIAXIAL_EPSILON)
            {
                return indices.z;
            }

            if (Mathf.Abs(indices.x - indices.z) < UNIAXIAL_EPSILON)
            {
                return indices.y;
            }

            if (Mathf.Abs(indices.y - indices.z) < UNIAXIAL_EPSILON)
            {
                return indices.x;
            }

            return Mathf.Max(indices.x, Mathf.Max(indices.y, indices.z));
        }

        private static Vector4 DeriveBiaxialAxesView(Vector3 indices, Matrix4x4 viewToPrincipalMatrix)
        {
            if (!IsBiaxial(indices))
            {
                return Vector4.zero;
            }

            AxisIndex[] sorted =
            {
                new AxisIndex(indices.x, Vector3.right),
                new AxisIndex(indices.y, Vector3.up),
                new AxisIndex(indices.z, Vector3.forward)
            };

            System.Array.Sort(sorted, (a, b) => a.Index.CompareTo(b.Index));

            float min2 = sorted[0].Index * sorted[0].Index;
            float mid2 = sorted[1].Index * sorted[1].Index;
            float max2 = sorted[2].Index * sorted[2].Index;
            float span = Mathf.Max(max2 - min2, 0.000001f);
            float cosFromMax = Mathf.Sqrt(Mathf.Clamp01((mid2 - min2) / span));
            float sinFromMax = Mathf.Sqrt(Mathf.Clamp01(1f - cosFromMax * cosFromMax));

            Vector3 minAxis = sorted[0].PrincipalAxis;
            Vector3 maxAxis = sorted[2].PrincipalAxis;
            Vector3 axisA = (minAxis * sinFromMax + maxAxis * cosFromMax).normalized;
            Vector3 axisB = (-minAxis * sinFromMax + maxAxis * cosFromMax).normalized;

            Matrix4x4 principalToView = viewToPrincipalMatrix.transpose;
            axisA = principalToView.MultiplyVector(axisA).normalized;
            axisB = principalToView.MultiplyVector(axisB).normalized;

            if (axisA.z < 0f) axisA = -axisA;
            if (axisB.z < 0f) axisB = -axisB;

            return new Vector4(axisA.x, axisA.y, axisB.x, axisB.y);
        }

        private static Vector3 DeriveOpticAxisView(Matrix4x4 worldToPrincipalMatrix)
        {
            Vector3 opticAxisView = new Vector3(
                worldToPrincipalMatrix.m02,
                worldToPrincipalMatrix.m12,
                worldToPrincipalMatrix.m22);

            if (opticAxisView.sqrMagnitude < MIN_OPTIC_AXIS_SQR_MAGNITUDE)
            {
                return Vector3.forward;
            }

            opticAxisView.Normalize();

            if (opticAxisView.z < 0f)
            {
                opticAxisView = -opticAxisView;
            }

            return opticAxisView;
        }

        private static Matrix4x4 CreatePaperKtpWorldToPrincipalMatrix(float alphaDeg, float thetaDeg, float phiDeg)
        {
            Quaternion inPlane = Quaternion.AngleAxis(-alphaDeg, Vector3.forward);
            Quaternion tilt = Quaternion.AngleAxis(
                -thetaDeg,
                new Vector3(Mathf.Cos(phiDeg * Mathf.Deg2Rad), Mathf.Sin(phiDeg * Mathf.Deg2Rad), 0f));
            return Matrix4x4.Rotate(inPlane * tilt);
        }

        private static Matrix4x4 CreateInPlaneWorldToPrincipalMatrix(float alphaDeg)
        {
            return Matrix4x4.Rotate(Quaternion.AngleAxis(-alphaDeg, Vector3.forward));
        }

        private float ResolveCrystalAxisAngleDeg(Scene2BiaxialDisplayMode activeMode)
        {
            return activeMode == Scene2BiaxialDisplayMode.PaperKtpPreset
                ? PAPER_KTP_ALPHA_DEG
                : _rawJonesCrystalAxisAngleDeg;
        }

        private static bool IsKtpProfile(CrystalProfile profile)
        {
            return profile != null
                   && !string.IsNullOrEmpty(profile.crystalName)
                   && profile.crystalName.ToLowerInvariant().Contains("ktp");
        }

        private static bool IsBiaxial(Vector3 indices)
        {
            return Mathf.Abs(indices.x - indices.y) >= UNIAXIAL_EPSILON
                   && Mathf.Abs(indices.y - indices.z) >= UNIAXIAL_EPSILON
                   && Mathf.Abs(indices.x - indices.z) >= UNIAXIAL_EPSILON;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Average(float a, float b)
        {
            return (a + b) * 0.5f;
        }

        private static Vector2 ClampInitialMelatopeOffset(Vector2 offset)
        {
            return new Vector2(
                Mathf.Clamp(offset.x, -MAX_INITIAL_MELATOPE_OFFSET, MAX_INITIAL_MELATOPE_OFFSET),
                Mathf.Clamp(offset.y, -MAX_INITIAL_MELATOPE_OFFSET, MAX_INITIAL_MELATOPE_OFFSET));
        }

        public void SetFOV(float fov)
        {
            float halfSize = Mathf.Tan(Mathf.Clamp(fov, 1f, 120f) * 0.5f * Mathf.Deg2Rad) * _screenDistanceM;
            _screenHalfSizeM = Mathf.Max(MIN_SCREEN_HALF_SIZE_M, halfSize);
        }

        public void SetPhaseScale(float phaseScale)
        {
            _phaseScale = Mathf.Max(MIN_PHASE_SCALE, phaseScale);
        }

        public void SetDisplayGamma(float displayGamma)
        {
            _displayGamma = Mathf.Max(MIN_DISPLAY_GAMMA, displayGamma);
        }

        public void SetBlackCutoff(float blackCutoff)
        {
            _blackCutoff = Mathf.Clamp(blackCutoff, 0f, MAX_BLACK_CUTOFF);
        }

        public void SetRingSharpness(float ringSharpness)
        {
            _ringSharpness = Mathf.Max(MIN_RING_SHARPNESS, ringSharpness);
        }

        public void SetCrossWidth(float crossWidth)
        {
            _crossWidth = Mathf.Clamp(crossWidth, MIN_CROSS_WIDTH, MAX_CROSS_WIDTH);
        }

        public void SetInitialMelatopeOffset(Vector2 offset)
        {
            _initialMelatopeOffset = ClampInitialMelatopeOffset(offset);
        }

        public void SetDisplayMapping(float displayGamma, float blackCutoff, float ringSharpness, float crossWidth)
        {
            SetDisplayGamma(displayGamma);
            SetBlackCutoff(blackCutoff);
            SetRingSharpness(ringSharpness);
            SetCrossWidth(crossWidth);
        }

        public void SetRawJonesDisplayMapping(float gain, float gamma, float blackCutoff, float whitePoint)
        {
            _rawJonesDisplayGain = Mathf.Max(0f, gain);
            _rawJonesDisplayGamma = Mathf.Max(MIN_OUTPUT_GAMMA, gamma);
            _rawJonesOutputBlackCutoff = Mathf.Clamp(blackCutoff, 0f, MAX_OUTPUT_BLACK_CUTOFF);
            _rawJonesOutputWhitePoint = Mathf.Max(_rawJonesOutputBlackCutoff + 0.0001f, whitePoint);
        }

        public void SetRawJonesPhysicalParameters(bool overridePhysicalParameters, float wavelengthNm, float thicknessMm)
        {
            _overrideRawJonesPhysicalParameters = overridePhysicalParameters;
            _rawJonesWavelengthNm = Mathf.Max(0.001f, wavelengthNm);
            _rawJonesThicknessMm = Mathf.Max(0.001f, thicknessMm);
        }

        public void SetRawJonesCrystalAxisAngle(float angleDeg)
        {
            _rawJonesCrystalAxisAngleDeg = Mathf.Repeat(angleDeg, 360f);
        }

        public void SetScene2DisplayParameters(
            float phaseScale,
            float displayGamma,
            float blackCutoff,
            float ringSharpness,
            float crossWidth,
            Vector2 initialMelatopeOffset,
            float screenDistanceM,
            float screenHalfSizeM,
            float phaseAntiAliasStrength,
            Color laserColor)
        {
            SetPhaseScale(phaseScale);
            SetDisplayMapping(displayGamma, blackCutoff, ringSharpness, crossWidth);
            SetInitialMelatopeOffset(initialMelatopeOffset);
            SetJonesViewParameters(screenDistanceM, screenHalfSizeM, phaseAntiAliasStrength);
            SetLaserColor(laserColor);
        }

        public void SetJonesViewParameters(float screenDistanceM, float screenHalfSizeM, float phaseAntiAliasStrength)
        {
            _screenDistanceM = Mathf.Max(MIN_SCREEN_DISTANCE_M, screenDistanceM);
            _screenHalfSizeM = Mathf.Max(MIN_SCREEN_HALF_SIZE_M, screenHalfSizeM);
            _phaseAntiAliasStrength = Mathf.Max(0f, phaseAntiAliasStrength);
        }

        public void SetReliefShading(bool enabled, float strength)
        {
            _enableReliefShading = enabled;
            _reliefStrength = Mathf.Max(0f, strength);
        }

        public void SetBiaxialDisplayMode(Scene2BiaxialDisplayMode mode, bool usePaperKtpPresetForKtp)
        {
            _biaxialDisplayMode = mode;
            _usePaperKtpPresetForKtp = usePaperKtpPresetForKtp;
        }

        public void SetLaserColor(Color color)
        {
            _laserColor = color == default ? Color.red : color;
        }

        public void SetPolarizerAngleDeg(float degrees)
        {
            _polarizerAngleDeg = Mathf.Repeat(degrees, 360f);
        }

        public void SetAnalyzerAngleDeg(float degrees)
        {
            _analyzerAngleDeg = Mathf.Repeat(degrees, 360f);
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture(ref _renderTexture);
            ReleaseRenderTexture(ref _jonesIntensityTexture);

            if (_jonesMaterial != null)
            {
                Object.Destroy(_jonesMaterial);
                _jonesMaterial = null;
            }
        }

        private static void ReleaseRenderTexture(ref RenderTexture renderTexture)
        {
            if (renderTexture == null)
            {
                return;
            }

            renderTexture.Release();
            Object.Destroy(renderTexture);
            renderTexture = null;
        }

        private static void ClearRenderTexture(RenderTexture renderTexture)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = previous;
        }

        private struct AxisIndex
        {
            public float Index;
            public Vector3 PrincipalAxis;

            public AxisIndex(float index, Vector3 principalAxis)
            {
                Index = index;
                PrincipalAxis = principalAxis;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Render One Frame")]
        private void ContextMenuRender()
        {
            UpdateAndRender();
            Debug.Log("[ConoscopicTextureRenderer] Rendered one Jones red/black frame.");
        }

        [ContextMenu("Print Status")]
        private void ContextMenuPrintStatus()
        {
            Debug.Log($"[ConoscopicTextureRenderer] Status:\n" +
                      $"  - Initialized: {_isInitialized}\n" +
                      $"  - TextureSize: {_textureSize}x{_textureSize}\n" +
                      $"  - PhaseScale: {_phaseScale}\n" +
                      $"  - DisplayGamma: {_displayGamma}\n" +
                      $"  - BlackCutoff: {_blackCutoff}\n" +
                      $"  - RingSharpness: {_ringSharpness}\n" +
                      $"  - CrossWidth: {_crossWidth}\n" +
                      $"  - ScreenDistanceM: {_screenDistanceM}\n" +
                      $"  - ScreenHalfSizeM: {_screenHalfSizeM}\n" +
                      $"  - PhaseAA: {_phaseAntiAliasStrength}\n" +
                      $"  - RawJonesPhysicalOverride: {_overrideRawJonesPhysicalParameters}\n" +
                      $"  - RawJonesWavelengthNm: {_rawJonesWavelengthNm}\n" +
                      $"  - RawJonesThicknessMm: {_rawJonesThicknessMm}\n" +
                      $"  - RawJonesGain: {_rawJonesDisplayGain}\n" +
                      $"  - RawJonesGamma: {_rawJonesDisplayGamma}\n" +
                      $"  - RawJonesCutoff: {_rawJonesOutputBlackCutoff}\n" +
                      $"  - RawJonesWhitePoint: {_rawJonesOutputWhitePoint}\n" +
                      $"  - RawJonesAxis: {_rawJonesCrystalAxisAngleDeg}\n" +
                      $"  - BiaxialDisplayMode: {_biaxialDisplayMode}\n" +
                      $"  - UsePaperKtpPresetForKtp: {_usePaperKtpPresetForKtp}\n" +
                      $"  - InitialMelatopeOffset: {_initialMelatopeOffset}\n" +
                      $"  - RenderTexture: {(_renderTexture != null ? "created" : "null")}\n" +
                      $"  - IntensityTexture: {(_jonesIntensityTexture != null ? "created" : "null")}\n" +
                      $"  - Material: {(_jonesMaterial != null ? "created" : "null")}\n" +
                      $"  - PhysicalCore: {(_sourcePhysicalCore != null ? "set" : "null")}");
        }
#endif
    }
}
