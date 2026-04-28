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

        private RenderTexture _renderTexture;
        private GameObject _previewRoot;
        private Camera _previewCamera;
        private GameObject _previewQuad;
        private Material _previewMaterial;
        private CrystalPhysicalCore _sourcePhysicalCore;

        private int _textureSize = 512;
        private float _fov = 10f;
        private float _phaseScale = 1f;
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
            float phaseScale = 1f)
        {
            _sourcePhysicalCore = sourcePhysicalCore;
            _textureSize = textureSize;
            _fov = Mathf.Clamp(fov, MIN_FOV, MAX_FOV);
            _phaseScale = Mathf.Max(MIN_PHASE_SCALE, phaseScale);
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

            _previewMaterial.SetVector("_RefractiveIndices", indices);
            _previewMaterial.SetMatrix("_RotationMatrix", matrix);
            _previewMaterial.SetFloat("_CrystalLength", lengthMeters);
            _previewMaterial.SetFloat("_Wavelength", wavelengthMeters);
            _previewMaterial.SetFloat("_FOV", _fov);
            _previewMaterial.SetFloat("_PhaseScale", _phaseScale);
            _previewMaterial.SetColor("_BaseColor", _laserColor);

            WarnIfWideFov();
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
