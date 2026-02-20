using UnityEngine;

namespace ElectroOptics.Experiment.Renderer
{
    /// <summary>
    /// 锥光干涉纹理渲染器
    /// 使用 RenderTexture 和专用摄像机将锥光干涉图渲染到纹理
    /// 用于在弹窗中显示干涉图案
    /// </summary>
    public class ConoscopicTextureRenderer : MonoBehaviour
    {
        #region 常量

        /// <summary>
        /// 预览层名称
        /// </summary>
        private const string PREVIEW_LAYER_NAME = "ConoscopicPreview";

        /// <summary>
        /// 默认 Shader 名称
        /// </summary>
        private const string SHADER_NAME = "ElectroOptics/ConoscopicInterference";

        #endregion

        #region 私有字段

        private RenderTexture _renderTexture;
        private Camera _previewCamera;
        private GameObject _previewQuad;
        private Material _previewMaterial;
        private CrystalPhysicalCore _sourcePhysicalCore;

        private int _textureSize = 512;
        private float _fov = 10f;
        private Color _laserColor = Color.red;

        private bool _isInitialized;

        #endregion

        #region 公共属性

        /// <summary>
        /// 渲染纹理
        /// </summary>
        public RenderTexture RenderTexture => _renderTexture;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化渲染器
        /// </summary>
        /// <param name="sourcePhysicalCore">晶体物理核心引用</param>
        /// <param name="textureSize">纹理尺寸</param>
        /// <param name="fov">视场角</param>
        /// <param name="laserColor">激光颜色</param>
        public void Initialize(CrystalPhysicalCore sourcePhysicalCore, int textureSize = 512, float fov = 10f, Color laserColor = default)
        {
            _sourcePhysicalCore = sourcePhysicalCore;
            _textureSize = textureSize;
            _fov = fov;
            _laserColor = laserColor == default ? Color.red : laserColor;

            // 确保有预览层
            EnsurePreviewLayer();

            // 创建渲染资源
            CreateRenderTexture();
            CreatePreviewCamera();
            CreatePreviewQuad();

            _isInitialized = true;
            Debug.Log($"[ConoscopicTextureRenderer] 初始化完成，纹理尺寸: {_textureSize}x{_textureSize}");
        }

        /// <summary>
        /// 确保预览层存在
        /// </summary>
        private void EnsurePreviewLayer()
        {
            int layerIndex = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
            if (layerIndex == -1)
            {
                Debug.LogWarning($"[ConoscopicTextureRenderer] 层 '{PREVIEW_LAYER_NAME}' 不存在，请手动添加。\n" +
                                 "路径: Edit -> Project Settings -> Tags and Layers -> Layers");
            }
        }

        /// <summary>
        /// 创建渲染纹理
        /// </summary>
        private void CreateRenderTexture()
        {
            _renderTexture = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.ARGB32);
            _renderTexture.antiAliasing = 4;
            _renderTexture.Create();
        }

        /// <summary>
        /// 创建预览摄像机
        /// </summary>
        private void CreatePreviewCamera()
        {
            var camObj = new GameObject("ConoscopicPreviewCamera");
            camObj.transform.position = Vector3.back * 2f;
            camObj.transform.SetParent(transform);  // 作为子对象
            camObj.hideFlags = HideFlags.HideInHierarchy;  // 隐藏在 Hierarchy

            _previewCamera = camObj.AddComponent<Camera>();
            _previewCamera.orthographic = false;
            _previewCamera.fieldOfView = _fov;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 10f;
            _previewCamera.targetTexture = _renderTexture;
            _previewCamera.backgroundColor = Color.black;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;

            // 只渲染预览层
            int layerIndex = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
            if (layerIndex != -1)
            {
                _previewCamera.cullingMask = 1 << layerIndex;
            }
            else
            {
                // 如果层不存在，渲染默认层
                _previewCamera.cullingMask = 1 << 0;
            }

            _previewCamera.enabled = false;  // 手动渲染
        }

        /// <summary>
        /// 创建预览 Quad
        /// </summary>
        private void CreatePreviewQuad()
        {
            _previewQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _previewQuad.name = "ConoscopicPreviewQuad";

            // 设置层级
            int layerIndex = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
            if (layerIndex != -1)
            {
                _previewQuad.layer = layerIndex;
            }

            _previewQuad.transform.position = Vector3.forward;
            _previewQuad.transform.SetParent(transform);  // 作为子对象
            _previewQuad.hideFlags = HideFlags.HideInHierarchy;  // 隐藏在 Hierarchy

            // 移除碰撞器（不需要）
            var collider = _previewQuad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // 创建材质
            var renderer = _previewQuad.GetComponent<UnityEngine.Renderer>();
            Shader shader = Shader.Find(SHADER_NAME);
            if (shader != null)
            {
                _previewMaterial = new Material(shader);
                renderer.material = _previewMaterial;
            }
            else
            {
                Debug.LogError($"[ConoscopicTextureRenderer] 找不到 Shader: {SHADER_NAME}");
                // 使用默认材质
                _previewMaterial = renderer.material;
            }
        }

        #endregion

        #region 渲染方法

        /// <summary>
        /// 更新参数并渲染一帧
        /// </summary>
        public void UpdateAndRender()
        {
            if (!_isInitialized || _sourcePhysicalCore == null || _previewMaterial == null || _previewCamera == null)
                return;

            // 从 PhysicalCore 获取计算结果
            UpdateShaderProperties();

            // 渲染一帧
            _previewCamera.Render();
        }

        /// <summary>
        /// 更新 Shader 属性
        /// </summary>
        private void UpdateShaderProperties()
        {
            // 调试：打印材质和颜色信息
            if (_previewMaterial != null)
            {
                Debug.Log($"[ConoscopicTextureRenderer] Shader名称: {_previewMaterial.shader.name}");
                Debug.Log($"[ConoscopicTextureRenderer] 设置前 _BaseColor: {_previewMaterial.GetColor("_BaseColor")}");
            }
            Debug.Log($"[ConoscopicTextureRenderer] LaserColor: {_laserColor}");

            // 获取物理核心的数据
            Vector3 indices = _sourcePhysicalCore.NewPrincipalIndices;
            Matrix4x4 matrix = _sourcePhysicalCore.ShaderWorldToPrincipalMatrix;

            // 获取尺寸和波长（从 Profile 或使用默认值）
            float len_m = 0.02f;  // 默认 20mm
            float wave_m = 633e-9f;  // 默认 633nm

            var config = _sourcePhysicalCore.CurrentConfig;
            if (config.profile != null)
            {
                len_m = (float)(config.profile.defaultLength_mm * 1e-3);
                wave_m = (float)(config.profile.defaultWavelength_nm * 1e-9);
            }

            // 安全检查：防止除零
            if (wave_m < 1e-9f) wave_m = 633e-9f;

            // 设置 Shader 参数
            _previewMaterial.SetVector("_RefractiveIndices", indices);
            _previewMaterial.SetMatrix("_RotationMatrix", matrix);
            _previewMaterial.SetFloat("_CrystalLength", len_m);
            _previewMaterial.SetFloat("_Wavelength", wave_m);
            _previewMaterial.SetFloat("_FOV", _fov);
            _previewMaterial.SetColor("_BaseColor", _laserColor);

            // 调试：确认颜色已设置
            Debug.Log($"[ConoscopicTextureRenderer] 设置后 _BaseColor: {_previewMaterial.GetColor("_BaseColor")}");
        }

        #endregion

        #region 公共设置方法

        /// <summary>
        /// 设置视场角
        /// </summary>
        /// <param name="fov">视场角度数</param>
        public void SetFOV(float fov)
        {
            _fov = Mathf.Clamp(fov, 1f, 60f);
            if (_previewCamera != null)
            {
                _previewCamera.fieldOfView = _fov;
            }
            if (_previewMaterial != null)
            {
                _previewMaterial.SetFloat("_FOV", _fov);
            }
        }

        /// <summary>
        /// 设置激光颜色
        /// </summary>
        /// <param name="color">激光颜色</param>
        public void SetLaserColor(Color color)
        {
            _laserColor = color;
            if (_previewMaterial != null)
            {
                _previewMaterial.SetColor("_BaseColor", _laserColor);
            }
        }

        #endregion

        #region 清理

        private void OnDestroy()
        {
            // 清理渲染纹理
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            // 清理摄像机
            if (_previewCamera != null)
            {
                Destroy(_previewCamera.gameObject);
            }

            // 清理 Quad
            if (_previewQuad != null)
            {
                Destroy(_previewQuad);
            }

            // 清理材质
            if (_previewMaterial != null)
            {
                Destroy(_previewMaterial);
            }
        }

        #endregion

        #region 编辑器调试

#if UNITY_EDITOR
        [ContextMenu("手动渲染一帧")]
        private void ContextMenuRender()
        {
            UpdateAndRender();
            Debug.Log("[ConoscopicTextureRenderer] 已手动渲染一帧");
        }

        [ContextMenu("打印状态")]
        private void ContextMenuPrintStatus()
        {
            Debug.Log($"[ConoscopicTextureRenderer] 状态报告:\n" +
                      $"  - 初始化: {_isInitialized}\n" +
                      $"  - 纹理尺寸: {_textureSize}x{_textureSize}\n" +
                      $"  - 视场角: {_fov}°\n" +
                      $"  - RenderTexture: {(_renderTexture != null ? "已创建" : "null")}\n" +
                      $"  - PreviewCamera: {(_previewCamera != null ? "已创建" : "null")}\n" +
                      $"  - PreviewQuad: {(_previewQuad != null ? "已创建" : "null")}\n" +
                      $"  - Material: {(_previewMaterial != null ? "已创建" : "null")}\n" +
                      $"  - PhysicalCore: {(_sourcePhysicalCore != null ? "已设置" : "null")}");
        }
#endif

        #endregion
    }
}
