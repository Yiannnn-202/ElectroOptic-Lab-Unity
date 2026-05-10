using UnityEngine;

namespace ElectroOptics.Experiment.Renderer
{
    /// <summary>
    /// Renders the conoscopic interference pattern into a RenderTexture.
    /// The preview camera framing is independent from the physical cone FOV.
    /// </summary>
    public class ConoscopicTextureRenderer : MonoBehaviour
    {
        private const string PREVIEW_LAYER_NAME = "ConoscopicPreview";
        private const string SHADER_NAME = "ElectroOptics/ConoscopicInterference";
        private const float MIN_FOV = 1f;
        private const float MAX_FOV = 120f;
        private const float WIDE_FOV_WARNING_THRESHOLD = 90f;
        private const float MIN_PHASE_SCALE = 0.01f;
        private const float MIN_DISPLAY_GAMMA = 0.1f;
        private const float MAX_BLACK_CUTOFF = 0.25f;
        private const float MIN_RING_SHARPNESS = 0.01f;
        private const float MIN_CROSS_WIDTH = 0.001f;
        private const float MAX_CROSS_WIDTH = 0.9f;
        private const float MIN_OPTIC_AXIS_SQR_MAGNITUDE = 0.000001f;
        private const float UNIAXIAL_EPSILON = 0.0005f;
        private const float MAX_INITIAL_MELATOPE_OFFSET = 0.25f;
        private static readonly Vector2 DEFAULT_INITIAL_MELATOPE_OFFSET = new Vector2(0.035f, -0.025f);

        private RenderTexture _renderTexture;
        private GameObject _previewRoot;
        private Camera _previewCamera;
        private GameObject _previewQuad;
        private Material _previewMaterial;
        private CrystalPhysicalCore _sourcePhysicalCore;

        private int _textureSize = 512;
        private float _fov = 10f;
        private float _phaseScale = 0.1f;
        private float _displayGamma = 1.25f;
        private float _blackCutoff = 0.012f;
        private float _ringSharpness = 1f;
        private float _crossWidth = 0.16f;
        private Vector2 _initialMelatopeOffset = DEFAULT_INITIAL_MELATOPE_OFFSET;
        private Color _laserColor = Color.red;

        private bool _isInitialized;
        private bool _hasWarnedWideFov;

        public RenderTexture RenderTexture => _renderTexture;
        public bool IsInitialized => _isInitialized;

        public void Initialize(
            CrystalPhysicalCore sourcePhysicalCore,
            int textureSize = 512,
            float fov = 10f,
            Color laserColor = default,
            float phaseScale = 0.1f,
            float displayGamma = 1.25f,
            float blackCutoff = 0.012f,
            float ringSharpness = 1f,
            float crossWidth = 0.16f)
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
            _sourcePhysicalCore = sourcePhysicalCore;
            _textureSize = textureSize;
            _fov = Mathf.Clamp(fov, MIN_FOV, MAX_FOV);
            _phaseScale = Mathf.Max(MIN_PHASE_SCALE, phaseScale);
            _displayGamma = Mathf.Max(MIN_DISPLAY_GAMMA, displayGamma);
            _blackCutoff = Mathf.Clamp(blackCutoff, 0f, MAX_BLACK_CUTOFF);
            _ringSharpness = Mathf.Max(MIN_RING_SHARPNESS, ringSharpness);
            _crossWidth = Mathf.Clamp(crossWidth, MIN_CROSS_WIDTH, MAX_CROSS_WIDTH);
            _initialMelatopeOffset = ClampInitialMelatopeOffset(initialMelatopeOffset);
            _laserColor = laserColor == default ? Color.red : laserColor;

            EnsurePreviewLayer();
            CreateRenderTexture();
            CreatePreviewRoot();
            CreatePreviewCamera();
            CreatePreviewQuad();

            _isInitialized = true;
            Debug.Log($"[ConoscopicTextureRenderer] Initialized, texture size: {_textureSize}x{_textureSize}");
        }

        private void EnsurePreviewLayer()
        {
            int layerIndex = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
            if (layerIndex == -1)
            {
                Debug.LogWarning($"[ConoscopicTextureRenderer] Layer '{PREVIEW_LAYER_NAME}' does not exist. Add it in Edit -> Project Settings -> Tags and Layers -> Layers.");
            }
        }

        private void CreateRenderTexture()
        {
            _renderTexture = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.ARGB32);
            _renderTexture.antiAliasing = 4;
            _renderTexture.Create();
        }

        private void CreatePreviewRoot()
        {
            _previewRoot = new GameObject("ConoscopicPreviewRoot");
            _previewRoot.transform.position = Vector3.zero;
            _previewRoot.transform.rotation = Quaternion.identity;
            _previewRoot.transform.localScale = Vector3.one;
            _previewRoot.hideFlags = HideFlags.HideInHierarchy;
        }

        private void CreatePreviewCamera()
        {
            var camObj = new GameObject("ConoscopicPreviewCamera");
            camObj.transform.SetParent(_previewRoot.transform, false);
            camObj.transform.localPosition = Vector3.back * 2f;
            camObj.transform.localRotation = Quaternion.identity;
            camObj.transform.localScale = Vector3.one;
            camObj.hideFlags = HideFlags.HideInHierarchy;

            _previewCamera = camObj.AddComponent<Camera>();
            _previewCamera.orthographic = true;
            _previewCamera.orthographicSize = 0.5f;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 10f;
            _previewCamera.targetTexture = _renderTexture;
            _previewCamera.backgroundColor = Color.black;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.cullingMask = GetPreviewCullingMask();
            _previewCamera.enabled = false;
        }

        private int GetPreviewCullingMask()
        {
            int layerIndex = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
            return layerIndex != -1 ? 1 << layerIndex : 1 << 0;
        }

        private void CreatePreviewQuad()
        {
            _previewQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _previewQuad.name = "ConoscopicPreviewQuad";
            _previewQuad.transform.SetParent(_previewRoot.transform, false);
            _previewQuad.transform.localPosition = Vector3.forward;
            _previewQuad.transform.localRotation = Quaternion.identity;
            _previewQuad.transform.localScale = Vector3.one;
            _previewQuad.hideFlags = HideFlags.HideInHierarchy;

            int layerIndex = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
            if (layerIndex != -1)
            {
                _previewQuad.layer = layerIndex;
            }

            var collider = _previewQuad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var quadRenderer = _previewQuad.GetComponent<UnityEngine.Renderer>();
            Shader shader = Shader.Find(SHADER_NAME);
            if (shader != null)
            {
                _previewMaterial = new Material(shader);
                quadRenderer.material = _previewMaterial;
            }
            else
            {
                Debug.LogError($"[ConoscopicTextureRenderer] Shader not found: {SHADER_NAME}");
                _previewMaterial = quadRenderer.material;
            }
        }

        public void UpdateAndRender()
        {
            if (!_isInitialized || _sourcePhysicalCore == null || _previewMaterial == null || _previewCamera == null)
            {
                return;
            }

            UpdateShaderProperties();
            _previewCamera.Render();
        }

        private void UpdateShaderProperties()
        {
            Vector3 indices = _sourcePhysicalCore.NewPrincipalIndices;
            Matrix4x4 matrix = _sourcePhysicalCore.ShaderWorldToPrincipalMatrix;

            float lengthMeters = 0.02f;
            float wavelengthMeters = 633e-9f;

            var config = _sourcePhysicalCore.CurrentConfig;
            if (config.profile != null)
            {
                lengthMeters = (float)(config.profile.defaultLength_mm * 1e-3);
                wavelengthMeters = (float)(config.profile.defaultWavelength_nm * 1e-9);
            }

            if (wavelengthMeters < 1e-9f)
            {
                wavelengthMeters = 633e-9f;
            }

            Vector3 opticAxisView = DeriveOpticAxisView(matrix);
            Vector4 biaxialAxesView = DeriveBiaxialAxesView(indices, matrix);
            _previewMaterial.SetVector("_RefractiveIndices", indices);
            _previewMaterial.SetMatrix("_RotationMatrix", matrix);
            _previewMaterial.SetVector("_OpticAxisView", new Vector4(opticAxisView.x, opticAxisView.y, opticAxisView.z, 0f));
            _previewMaterial.SetVector("_BiaxialAxesView", biaxialAxesView);
            _previewMaterial.SetFloat("_CrystalLength", lengthMeters);
            _previewMaterial.SetFloat("_Wavelength", wavelengthMeters);
            _previewMaterial.SetFloat("_FOV", _fov);
            _previewMaterial.SetFloat("_PhaseScale", _phaseScale);
            _previewMaterial.SetFloat("_DisplayGamma", _displayGamma);
            _previewMaterial.SetFloat("_BlackCutoff", _blackCutoff);
            _previewMaterial.SetFloat("_RingSharpness", _ringSharpness);
            _previewMaterial.SetFloat("_CrossWidth", _crossWidth);
            _previewMaterial.SetVector("_InitialMelatopeOffset", new Vector4(_initialMelatopeOffset.x, _initialMelatopeOffset.y, 0f, 0f));
            _previewMaterial.SetColor("_BaseColor", _laserColor);

            WarnIfWideFov();
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

        private static Vector4 DeriveBiaxialAxesView(Vector3 indices, Matrix4x4 viewToPrincipalMatrix)
        {
            float nx = indices.x;
            float ny = indices.y;
            float nz = indices.z;

            if (Mathf.Abs(nx - ny) < UNIAXIAL_EPSILON
                || Mathf.Abs(ny - nz) < UNIAXIAL_EPSILON
                || Mathf.Abs(nx - nz) < UNIAXIAL_EPSILON)
            {
                return Vector4.zero;
            }

            AxisIndex[] sorted =
            {
                new AxisIndex(nx, Vector3.right),
                new AxisIndex(ny, Vector3.up),
                new AxisIndex(nz, Vector3.forward)
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

        private static Vector2 ClampInitialMelatopeOffset(Vector2 offset)
        {
            return new Vector2(
                Mathf.Clamp(offset.x, -MAX_INITIAL_MELATOPE_OFFSET, MAX_INITIAL_MELATOPE_OFFSET),
                Mathf.Clamp(offset.y, -MAX_INITIAL_MELATOPE_OFFSET, MAX_INITIAL_MELATOPE_OFFSET));
        }

        public void SetFOV(float fov)
        {
            _fov = Mathf.Clamp(fov, MIN_FOV, MAX_FOV);
            if (_previewMaterial != null)
            {
                _previewMaterial.SetFloat("_FOV", _fov);
            }

            WarnIfWideFov();
        }

        public void SetPhaseScale(float phaseScale)
        {
            _phaseScale = Mathf.Max(MIN_PHASE_SCALE, phaseScale);
            if (_previewMaterial != null)
            {
                _previewMaterial.SetFloat("_PhaseScale", _phaseScale);
            }
        }

        public void SetDisplayGamma(float displayGamma)
        {
            _displayGamma = Mathf.Max(MIN_DISPLAY_GAMMA, displayGamma);
            if (_previewMaterial != null)
            {
                _previewMaterial.SetFloat("_DisplayGamma", _displayGamma);
            }
        }

        public void SetBlackCutoff(float blackCutoff)
        {
            _blackCutoff = Mathf.Clamp(blackCutoff, 0f, MAX_BLACK_CUTOFF);
            if (_previewMaterial != null)
            {
                _previewMaterial.SetFloat("_BlackCutoff", _blackCutoff);
            }
        }

        public void SetRingSharpness(float ringSharpness)
        {
            _ringSharpness = Mathf.Max(MIN_RING_SHARPNESS, ringSharpness);
            if (_previewMaterial != null)
            {
                _previewMaterial.SetFloat("_RingSharpness", _ringSharpness);
            }
        }

        public void SetCrossWidth(float crossWidth)
        {
            _crossWidth = Mathf.Clamp(crossWidth, MIN_CROSS_WIDTH, MAX_CROSS_WIDTH);
            if (_previewMaterial != null)
            {
                _previewMaterial.SetFloat("_CrossWidth", _crossWidth);
            }
        }

        public void SetInitialMelatopeOffset(Vector2 offset)
        {
            _initialMelatopeOffset = ClampInitialMelatopeOffset(offset);
            if (_previewMaterial != null)
            {
                _previewMaterial.SetVector("_InitialMelatopeOffset", new Vector4(_initialMelatopeOffset.x, _initialMelatopeOffset.y, 0f, 0f));
            }
        }

        public void SetDisplayMapping(float displayGamma, float blackCutoff, float ringSharpness, float crossWidth)
        {
            SetDisplayGamma(displayGamma);
            SetBlackCutoff(blackCutoff);
            SetRingSharpness(ringSharpness);
            SetCrossWidth(crossWidth);
        }

        public void SetLaserColor(Color color)
        {
            _laserColor = color;
            if (_previewMaterial != null)
            {
                _previewMaterial.SetColor("_BaseColor", _laserColor);
            }
        }

        private void WarnIfWideFov()
        {
            if (_hasWarnedWideFov || _fov <= WIDE_FOV_WARNING_THRESHOLD)
            {
                return;
            }

            Debug.LogWarning("[ConoscopicTextureRenderer] Conoscopic FOV is above 90 degrees. Rendering will continue, but edge distortion becomes pronounced.");
            _hasWarnedWideFov = true;
        }

        private void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            if (_previewRoot != null)
            {
                Destroy(_previewRoot);
            }
            else if (_previewCamera != null)
            {
                Destroy(_previewCamera.gameObject);
                if (_previewQuad != null)
                {
                    Destroy(_previewQuad);
                }
            }

            if (_previewMaterial != null)
            {
                Destroy(_previewMaterial);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Render One Frame")]
        private void ContextMenuRender()
        {
            UpdateAndRender();
            Debug.Log("[ConoscopicTextureRenderer] Rendered one frame.");
        }

        [ContextMenu("Print Status")]
        private void ContextMenuPrintStatus()
        {
            Debug.Log($"[ConoscopicTextureRenderer] Status:\n" +
                      $"  - Initialized: {_isInitialized}\n" +
                      $"  - TextureSize: {_textureSize}x{_textureSize}\n" +
                      $"  - FOV: {_fov} deg\n" +
                      $"  - PhaseScale: {_phaseScale}\n" +
                      $"  - DisplayGamma: {_displayGamma}\n" +
                      $"  - BlackCutoff: {_blackCutoff}\n" +
                      $"  - RingSharpness: {_ringSharpness}\n" +
                      $"  - CrossWidth: {_crossWidth}\n" +
                      $"  - InitialMelatopeOffset: {_initialMelatopeOffset}\n" +
                      $"  - RenderTexture: {(_renderTexture != null ? "created" : "null")}\n" +
                      $"  - PreviewRoot: {(_previewRoot != null ? "created" : "null")}\n" +
                      $"  - PreviewCamera: {(_previewCamera != null ? "created" : "null")}\n" +
                      $"  - PreviewQuad: {(_previewQuad != null ? "created" : "null")}\n" +
                      $"  - Material: {(_previewMaterial != null ? "created" : "null")}\n" +
                      $"  - PhysicalCore: {(_sourcePhysicalCore != null ? "set" : "null")}");
        }
#endif
    }
}
