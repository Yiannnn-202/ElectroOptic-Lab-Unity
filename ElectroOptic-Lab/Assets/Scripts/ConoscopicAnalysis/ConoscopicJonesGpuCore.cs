using System;
using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    [ExecuteAlways]
    public class ConoscopicJonesGpuCore : MonoBehaviour
    {
        private const string ShaderName = "ElectroOptics/ConoscopicJonesIntensity";
        private const int PreviewReadbackResolution = 16;

        [SerializeField] private ConoscopicJonesParameters _parameters = new ConoscopicJonesParameters();
        [SerializeField] private CrystalProfile _profile;
        [SerializeField] private CrystalPhysicalCore _physicalCore;
        [SerializeField] private Shader _shader;

        private Material _material;
        private RenderTexture _intensityHeightMap;
        private RenderTexture _supersampledIntensityMap;
        private readonly ConoscopicJonesResult _result = new ConoscopicJonesResult();
        private bool _isDirty = true;
        private bool _warnedBiaxialFallback;

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

        public ConoscopicJonesParameters Parameters => _parameters;
        public ConoscopicJonesResult Result => _result;
        public RenderTexture IntensityHeightMap => _intensityHeightMap;
        public CrystalProfile Profile => _profile;
        public CrystalPhysicalCore PhysicalCore => _physicalCore;
        public bool IsDirty => _isDirty;

        public event Action<ConoscopicJonesResult> OnJonesIntensityUpdated;

        private void OnEnable()
        {
            EnsureDefaults();
            MarkDirty();
        }

        private void Update()
        {
            RecalculateIfDirty();
        }

        private void OnDisable()
        {
            ReleaseRuntimeResources();
        }

        public void SetProfile(CrystalProfile profile)
        {
            _profile = profile;
            _warnedBiaxialFallback = false;
            if (_parameters == null)
            {
                _parameters = new ConoscopicJonesParameters();
            }

            _parameters.ApplyProfileDefaults(profile);
            MarkDirty();
        }

        public void SetParameters(ConoscopicJonesParameters parameters)
        {
            _parameters = parameters != null ? parameters.Clone() : new ConoscopicJonesParameters();
            _parameters.Clamp();
            MarkDirty();
        }

        public void SetResolution(int resolution)
        {
            EnsureDefaults();
            _parameters.resolution = resolution;
            _parameters.Clamp();
            MarkDirty();
        }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        public void RecalculateIfDirty()
        {
            if (!_isDirty)
            {
                return;
            }

            ForceRecalculate();
        }

        public void ForceRecalculate()
        {
            EnsureDefaults();
            ApplyPhysicalConfig();
            if (!EnsureMaterial())
            {
                _result.MarkInvalid(_profile, _parameters, _intensityHeightMap);
                _isDirty = false;
                OnJonesIntensityUpdated?.Invoke(_result);
                return;
            }

            EnsureRenderTexture();
            UploadParameters();
            RenderTexture renderTarget = GetRenderTarget();
            Graphics.Blit(null, renderTarget, _material, 0);
            if (renderTarget != _intensityHeightMap)
            {
                Graphics.Blit(renderTarget, _intensityHeightMap);
            }

            Vector2 minMax = EstimateMinMaxFromReadback();
            _result.SetValid(_profile, _parameters, _intensityHeightMap, minMax.x, minMax.y);
            _isDirty = false;
            OnJonesIntensityUpdated?.Invoke(_result);
        }

        private void EnsureDefaults()
        {
            if (_parameters == null)
            {
                _parameters = new ConoscopicJonesParameters();
            }

            _parameters.Clamp();
            if (_shader == null)
            {
                _shader = Shader.Find(ShaderName);
            }
        }

        private bool EnsureMaterial()
        {
            if (_shader == null)
            {
                Debug.LogError($"[ConoscopicJonesGpuCore] Shader '{ShaderName}' not found.");
                return false;
            }

            if (_material == null || _material.shader != _shader)
            {
                if (_material != null)
                {
                    DestroyImmediateSafe(_material);
                }

                _material = new Material(_shader)
                {
                    name = "Conoscopic Jones Intensity Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return true;
        }

        private void EnsureRenderTexture()
        {
            int resolution = _parameters.resolution;
            int supersampledResolution = GetSupersampledResolution();
            if (_intensityHeightMap != null
                && _intensityHeightMap.width == resolution
                && _intensityHeightMap.height == resolution)
            {
                EnsureSupersampledRenderTexture(supersampledResolution);
                return;
            }

            if (_intensityHeightMap != null)
            {
                _intensityHeightMap.Release();
                DestroyImmediateSafe(_intensityHeightMap);
            }

            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)
                ? RenderTextureFormat.ARGBFloat
                : RenderTextureFormat.ARGBHalf;

            _intensityHeightMap = new RenderTexture(resolution, resolution, 0, format)
            {
                name = "Conoscopic Jones Intensity HeightMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            _intensityHeightMap.Create();
            EnsureSupersampledRenderTexture(supersampledResolution);
        }

        private void EnsureSupersampledRenderTexture(int resolution)
        {
            if (resolution <= _parameters.resolution)
            {
                ReleaseSupersampledRenderTexture();
                return;
            }

            if (_supersampledIntensityMap != null
                && _supersampledIntensityMap.width == resolution
                && _supersampledIntensityMap.height == resolution
                && _supersampledIntensityMap.format == _intensityHeightMap.format)
            {
                return;
            }

            ReleaseSupersampledRenderTexture();
            _supersampledIntensityMap = new RenderTexture(resolution, resolution, 0, _intensityHeightMap.format)
            {
                name = "Conoscopic Jones Supersampled Intensity",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            _supersampledIntensityMap.Create();
        }

        private RenderTexture GetRenderTarget()
        {
            return _supersampledIntensityMap != null ? _supersampledIntensityMap : _intensityHeightMap;
        }

        private int GetSupersampledResolution()
        {
            int factor = Mathf.Clamp(
                _parameters.renderSupersampleFactor,
                ConoscopicJonesParameters.MinRenderSupersampleFactor,
                ConoscopicJonesParameters.MaxRenderSupersampleFactor);
            return Mathf.Min(_parameters.resolution * factor, ConoscopicJonesParameters.MaxResolution);
        }

        private void UploadParameters()
        {
            _material.SetFloat("_WavelengthM", _parameters.wavelengthNm * 1e-9f);
            _material.SetFloat("_ThicknessM", _parameters.thicknessMm * 1e-3f);
            _material.SetFloat("_OrdinaryIndexNo", _parameters.ordinaryIndexNo);
            _material.SetFloat("_ExtraordinaryIndexNe", _parameters.extraordinaryIndexNe);
            _material.SetVector("_PrincipalIndices", new Vector4(
                _parameters.principalIndexNx,
                _parameters.principalIndexNy,
                _parameters.principalIndexNz,
                0f));
            _material.SetMatrix("_WorldToPrincipalMatrix", _parameters.worldToPrincipalMatrix);
            _material.SetFloat("_UseBiaxial", _parameters.IsBiaxial() || _parameters.uniaxialEoView ? 1f : 0f);
            _material.SetFloat("_ContinuousEigenMode", _parameters.uniaxialEoView ? 1f : 0f);
            _material.SetFloat("_BiaxialDisplayMode", (float)_parameters.biaxialDisplayMode);
            _material.SetVector("_BiaxialAxesView", DeriveBiaxialAxesView(
                new Vector3(
                    _parameters.principalIndexNx,
                    _parameters.principalIndexNy,
                    _parameters.principalIndexNz),
                _parameters.worldToPrincipalMatrix));
            _material.SetVector("_InitialMelatopeOffset", new Vector4(
                _parameters.initialMelatopeOffset.x,
                _parameters.initialMelatopeOffset.y,
                0f,
                0f));
            _material.SetFloat("_PhaseScale", _parameters.phaseScale);
            _material.SetFloat("_RingSharpness", _parameters.ringSharpness);
            _material.SetFloat("_CrossWidth", _parameters.crossWidth);
            _material.SetFloat("_BlackCutoff", _parameters.blackCutoff);
            _material.SetFloat("_DisplayGamma", _parameters.displayGamma);
            _material.SetFloat("_UniaxialEpsilon", _parameters.uniaxialEpsilon);
            _material.SetFloat("_ScreenDistanceM", _parameters.screenDistanceM);
            _material.SetFloat("_ScreenHalfSizeM", _parameters.screenHalfSizeM);
            _material.SetFloat("_InitialIntensity", _parameters.initialIntensity);
            _material.SetFloat("_PhaseAntiAliasStrength", _parameters.phaseAntiAliasStrength);
            _material.SetFloat("_PolarizerAngleRad", _parameters.polarizerAngleDeg * Mathf.Deg2Rad);
            _material.SetFloat("_AnalyzerAngleRad", _parameters.analyzerAngleDeg * Mathf.Deg2Rad);
            _material.SetFloat("_CrystalAxisAngleRad", _parameters.crystalAxisAngleDeg * Mathf.Deg2Rad);
            _material.SetFloat("_OpticAxisTiltRad", _parameters.opticAxisTiltDeg * Mathf.Deg2Rad);
            _material.SetFloat("_OpticAxisAzimuthRad", _parameters.opticAxisAzimuthDeg * Mathf.Deg2Rad);
            _material.SetFloat("_ElectricFieldStrength", _parameters.electricFieldStrength);
            _material.SetFloat("_ElectroOpticCoefficientR22", _parameters.electroOpticCoefficientR22);
            _material.SetFloat("_ApertureRadius", _parameters.apertureRadius);
        }

        private void ApplyPhysicalConfig()
        {
            if (_profile == null)
            {
                ApplyContinuousEoDisplayMode();
                return;
            }

            if (!_parameters.uniaxialEoView
                && _parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.PaperKtp1
                && ConoscopicJonesParameters.IsKtpProfile(_profile))
            {
                _parameters.ApplyPrincipalIndices(new Vector3((float)_profile.n_x, (float)_profile.n_y, (float)_profile.n_z));
                _parameters.worldToPrincipalMatrix = ConoscopicJonesParameters.CreatePaperKtp1WorldToPrincipalMatrix(
                    _parameters.crystalAxisAngleDeg,
                    _parameters.paperThetaDeg,
                    _parameters.paperPhiDeg);
                return;
            }

            EnsurePhysicalCore();
            if (_physicalCore == null)
            {
                Debug.LogWarning("[ConoscopicJonesGpuCore] PhysicalCore is unavailable; profile defaults remain active.");
                ApplyContinuousEoDisplayMode();
                return;
            }

            Vector3 requestedWorldLightDirection = Vector3.forward;
            CrystalWorkingGeometry geometry = CrystalWorkingGeometry.ResolveConoscopic(_profile, requestedWorldLightDirection);
            var config = new CrystalConfig
            {
                profile = _profile,
                crystalRotation = ResolveCrystalRotation(),
                localEField = Vector3.forward * _parameters.electricFieldStrength,
                probeFieldDirection = Vector3.forward,
                worldLightDirection = geometry.WorldLightDirection
            };

            _physicalCore.ApplyConfig(config);
            _parameters.ApplyPrincipalIndices(_physicalCore.NewPrincipalIndices);
            _parameters.worldToPrincipalMatrix = ResolveMatrix(_physicalCore.ShaderWorldToPrincipalMatrix);
            ApplyContinuousEoDisplayMode();

            if (HasBiaxialProfile(_profile) && !_parameters.IsBiaxial() && !_warnedBiaxialFallback)
            {
                Debug.LogWarning("[ConoscopicJonesGpuCore] Biaxial profile fell back to uniaxial mode because principal indices are near-degenerate or invalid.");
                _warnedBiaxialFallback = true;
            }
        }

        private void ApplyContinuousEoDisplayMode()
        {
            if (!_parameters.uniaxialEoView)
            {
                return;
            }

            _parameters.biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones;
            _parameters.forceUniaxial = false;
            if (_parameters.uniaxialEoUsePerturbedAxis)
            {
                Vector3 opticAxis = DerivePerturbedOpticAxisView(_parameters.worldToPrincipalMatrix);
                _parameters.opticAxisTiltDeg = Mathf.Acos(Mathf.Clamp(opticAxis.z, -1f, 1f)) * Mathf.Rad2Deg;
                _parameters.opticAxisAzimuthDeg = Mathf.Repeat(Mathf.Atan2(opticAxis.y, opticAxis.x) * Mathf.Rad2Deg, 360f);
                _parameters.Clamp();
            }
        }

        private Quaternion ResolveCrystalRotation()
        {
            Quaternion inPlaneRotation = Quaternion.AngleAxis(_parameters.crystalAxisAngleDeg, Vector3.forward);
            if (_parameters.opticAxisTiltDeg <= 0.0001f)
            {
                return inPlaneRotation;
            }

            Quaternion tilt = Quaternion.Euler(_parameters.opticAxisTiltDeg, _parameters.opticAxisAzimuthDeg, 0f);
            return tilt * inPlaneRotation;
        }

        private void EnsurePhysicalCore()
        {
            if (_physicalCore != null)
            {
                return;
            }

            _physicalCore = GetComponent<CrystalPhysicalCore>();
            if (_physicalCore == null)
            {
                _physicalCore = gameObject.AddComponent<CrystalPhysicalCore>();
            }
        }

        private static Matrix4x4 ResolveMatrix(Matrix4x4 matrix)
        {
            Vector3 basisX = new Vector3(matrix.m00, matrix.m01, matrix.m02);
            Vector3 basisY = new Vector3(matrix.m10, matrix.m11, matrix.m12);
            Vector3 basisZ = new Vector3(matrix.m20, matrix.m21, matrix.m22);
            float magnitude = basisX.sqrMagnitude + basisY.sqrMagnitude + basisZ.sqrMagnitude;
            return magnitude > 0.000001f ? matrix : Matrix4x4.identity;
        }

        private static Vector3 DerivePerturbedOpticAxisView(Matrix4x4 viewToPrincipalMatrix)
        {
            Matrix4x4 principalToView = viewToPrincipalMatrix.transpose;
            Vector3 axis = principalToView.MultiplyVector(Vector3.forward);
            axis = axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector3.forward;
            return axis.z < 0f ? -axis : axis;
        }

        private static bool HasBiaxialProfile(CrystalProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            float nx = (float)profile.n_x;
            float ny = (float)profile.n_y;
            float nz = (float)profile.n_z;
            const float epsilon = ConoscopicJonesParameters.DefaultUniaxialEpsilon;
            return Mathf.Abs(nx - ny) >= epsilon
                   && Mathf.Abs(ny - nz) >= epsilon
                   && Mathf.Abs(nx - nz) >= epsilon;
        }

        private static Vector4 DeriveBiaxialAxesView(Vector3 indices, Matrix4x4 viewToPrincipalMatrix)
        {
            float nx = indices.x;
            float ny = indices.y;
            float nz = indices.z;
            const float epsilon = ConoscopicJonesParameters.DefaultUniaxialEpsilon;
            if (Mathf.Abs(nx - ny) < epsilon
                || Mathf.Abs(ny - nz) < epsilon
                || Mathf.Abs(nx - nz) < epsilon)
            {
                return Vector4.zero;
            }

            AxisIndex[] sorted =
            {
                new AxisIndex(nx, Vector3.right),
                new AxisIndex(ny, Vector3.up),
                new AxisIndex(nz, Vector3.forward)
            };
            Array.Sort(sorted, (a, b) => a.Index.CompareTo(b.Index));

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

        private Vector2 EstimateMinMaxFromReadback()
        {
            if (_intensityHeightMap == null)
            {
                return Vector2.zero;
            }

            int sampleResolution = Mathf.Min(PreviewReadbackResolution, _parameters.resolution);
            RenderTexture previous = RenderTexture.active;
            var downsample = RenderTexture.GetTemporary(sampleResolution, sampleResolution, 0, _intensityHeightMap.format);
            var texture = new Texture2D(sampleResolution, sampleResolution, TextureFormat.RGBAFloat, false, true);

            try
            {
                Graphics.Blit(_intensityHeightMap, downsample);
                RenderTexture.active = downsample;
                texture.ReadPixels(new Rect(0, 0, sampleResolution, sampleResolution), 0, 0);
                texture.Apply(false, false);
                Color[] pixels = texture.GetPixels();
                float min = float.PositiveInfinity;
                float max = float.NegativeInfinity;

                for (int i = 0; i < pixels.Length; i++)
                {
                    float value = pixels[i].r;
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        continue;
                    }

                    min = Mathf.Min(min, value);
                    max = Mathf.Max(max, value);
                }

                if (float.IsInfinity(min) || float.IsInfinity(max))
                {
                    return Vector2.zero;
                }

                return new Vector2(Mathf.Clamp01(min), Mathf.Clamp01(max));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(downsample);
                DestroyImmediateSafe(texture);
            }
        }

        private void ReleaseRuntimeResources()
        {
            ReleaseSupersampledRenderTexture();
            if (_material != null)
            {
                DestroyImmediateSafe(_material);
                _material = null;
            }

            if (_intensityHeightMap != null)
            {
                _intensityHeightMap.Release();
                DestroyImmediateSafe(_intensityHeightMap);
                _intensityHeightMap = null;
            }
        }

        private void ReleaseSupersampledRenderTexture()
        {
            if (_supersampledIntensityMap == null)
            {
                return;
            }

            _supersampledIntensityMap.Release();
            DestroyImmediateSafe(_supersampledIntensityMap);
            _supersampledIntensityMap = null;
        }

        private static void DestroyImmediateSafe(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureDefaults();
            MarkDirty();
        }

        [ContextMenu("Force Jones Recalculate")]
        private void ContextMenuForceRecalculate()
        {
            ForceRecalculate();
            Debug.Log($"[ConoscopicJonesGpuCore] Recalculated. valid={_result.IsValid}, " +
                      $"resolution={_result.Resolution}, min={_result.MinIntensity:F4}, max={_result.MaxIntensity:F4}");
        }
#endif
    }
}
