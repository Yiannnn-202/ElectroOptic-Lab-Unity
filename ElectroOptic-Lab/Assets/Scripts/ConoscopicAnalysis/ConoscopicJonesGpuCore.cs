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
        [SerializeField] private Shader _shader;

        private Material _material;
        private RenderTexture _intensityHeightMap;
        private readonly ConoscopicJonesResult _result = new ConoscopicJonesResult();
        private bool _isDirty = true;

        public ConoscopicJonesParameters Parameters => _parameters;
        public ConoscopicJonesResult Result => _result;
        public RenderTexture IntensityHeightMap => _intensityHeightMap;
        public CrystalProfile Profile => _profile;
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
            if (!EnsureMaterial())
            {
                _result.MarkInvalid(_profile, _parameters, _intensityHeightMap);
                _isDirty = false;
                OnJonesIntensityUpdated?.Invoke(_result);
                return;
            }

            EnsureRenderTexture();
            UploadParameters();
            Graphics.Blit(null, _intensityHeightMap, _material, 0);

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
            if (_intensityHeightMap != null
                && _intensityHeightMap.width == resolution
                && _intensityHeightMap.height == resolution)
            {
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
        }

        private void UploadParameters()
        {
            _material.SetFloat("_WavelengthM", _parameters.wavelengthNm * 1e-9f);
            _material.SetFloat("_ThicknessM", _parameters.thicknessMm * 1e-3f);
            _material.SetFloat("_OrdinaryIndexNo", _parameters.ordinaryIndexNo);
            _material.SetFloat("_ExtraordinaryIndexNe", _parameters.extraordinaryIndexNe);
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
