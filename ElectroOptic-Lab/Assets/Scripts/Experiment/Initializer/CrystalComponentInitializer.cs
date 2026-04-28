using UnityEngine;
using ElectroOptics;
using ElectroOptics.DataTransfer;
using ElectroOptics.Experiment.Controller;
using ElectroOptics.Experiment.Renderer;

namespace ElectroOptics.Experiment.Initializer
{
    /// <summary>
    /// 晶体组件初始化器
    /// 在 Scene2.The Lab 启动时初始化晶体组件并加载选择的 Profile
    /// 挂载在场景中的任意 GameObject 上（建议创建空对象 "CrystalInitializer"）
    /// </summary>
    public class CrystalComponentInitializer : MonoBehaviour
    {
        #region Inspector 配置

        [Header("晶体模型")]
        [Tooltip("晶体 GameObject，留空则自动查找名为'晶体'的对象")]
        [SerializeField] private GameObject crystalModel;

        [Header("渲染配置")]
        [Tooltip("RenderTexture 尺寸")]
        [SerializeField] private int renderTextureSize = 512;

        [Tooltip("锥光干涉视场角")]
        [SerializeField] [Range(1f, 60f)] private float conoscopicFOV = 10f;

        [Tooltip("激光颜色")]
        [SerializeField] private Color laserColor = Color.red;

        [Header("光路配置")]
        [Tooltip("激光发射器 Transform；留空时自动查找场景中的 LaserEmitter")]
        [SerializeField] private Transform lightDirectionSource;

        #endregion

        #region 私有字段

        private CrystalControllerWrapper _controller;
        private ConoscopicTextureRenderer _textureRenderer;
        private CrystalPhysicalCore _physicalCore;

        #endregion

        #region Unity 生命周期

        private void Start()
        {
            Debug.Log("[CrystalComponentInitializer] 开始初始化...");

            // 1. 查找晶体模型
            FindCrystalModel();

            // 2. 验证晶体存在
            if (crystalModel == null)
            {
                Debug.LogError("[CrystalComponentInitializer] 未找到晶体模型！请确保场景中有名为'晶体'的 GameObject");
                enabled = false;
                return;
            }

            Debug.Log($"[CrystalComponentInitializer] 找到晶体模型: {crystalModel.name}");

            // 3. 设置晶体组件
            SetupCrystalComponents();

            // 4. 设置真实光路方向
            SetupLightDirectionSource();

            // 5. 设置纹理渲染器
            SetupTextureRenderer();

            // 6. 注册到 CrystalRuntime
            RegisterToRuntime();

            // 7. 加载选择的 Profile
            LoadSelectedProfile();
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 查找晶体模型
        /// </summary>
        private void FindCrystalModel()
        {
            if (crystalModel != null)
                return;

            // 尝试按名称查找
            crystalModel = GameObject.Find("晶体");

            // 如果还找不到，尝试通过标签查找
            if (crystalModel == null)
            {
                crystalModel = GameObject.FindGameObjectWithTag("Crystal");
            }
        }

        /// <summary>
        /// 设置晶体组件
        /// </summary>
        private void SetupCrystalComponents()
        {
            // 1. 获取或添加 CrystalPhysicalCore
            _physicalCore = crystalModel.GetComponent<CrystalPhysicalCore>();
            if (_physicalCore == null)
            {
                _physicalCore = crystalModel.AddComponent<CrystalPhysicalCore>();
                Debug.Log("[CrystalComponentInitializer] 添加 CrystalPhysicalCore 组件");
            }

            // 2. 确保有 Collider（用于检测）
            EnsureCollider();

            // 3. 获取或添加 CrystalControllerWrapper
            _controller = crystalModel.GetComponent<CrystalControllerWrapper>();
            if (_controller == null)
            {
                _controller = crystalModel.AddComponent<CrystalControllerWrapper>();
                Debug.Log("[CrystalComponentInitializer] 添加 CrystalControllerWrapper 组件");
            }

            // 4. 初始化控制器
            _controller.Initialize(_physicalCore);
        }

        private void SetupLightDirectionSource()
        {
            if (lightDirectionSource == null)
            {
                global::LaserEmitter laserEmitter = Object.FindFirstObjectByType<global::LaserEmitter>();
                if (laserEmitter != null)
                {
                    lightDirectionSource = laserEmitter.transform;
                }
            }

            if (_controller != null)
            {
                _controller.SetLightDirectionSource(lightDirectionSource);
            }
        }

        /// <summary>
        /// 确保晶体有 Collider 组件
        /// </summary>
        private void EnsureCollider()
        {
            var collider = crystalModel.GetComponent<Collider>();
            if (collider == null)
            {
                // 尝试使用 MeshCollider
                var meshFilter = crystalModel.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    var meshCollider = crystalModel.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                    meshCollider.convex = true;  // 用于物理检测
                    Debug.Log("[CrystalComponentInitializer] 添加 MeshCollider 组件");
                }
                else
                {
                    // 没有网格，使用 BoxCollider
                    crystalModel.AddComponent<BoxCollider>();
                    Debug.Log("[CrystalComponentInitializer] 添加 BoxCollider 组件");
                }
            }
        }

        /// <summary>
        /// 设置纹理渲染器
        /// </summary>
        private void SetupTextureRenderer()
        {
            // 在当前 GameObject 上添加或获取 ConoscopicTextureRenderer
            _textureRenderer = GetComponent<ConoscopicTextureRenderer>();
            if (_textureRenderer == null)
            {
                _textureRenderer = gameObject.AddComponent<ConoscopicTextureRenderer>();
                Debug.Log("[CrystalComponentInitializer] 添加 ConoscopicTextureRenderer 组件");
            }

            // 初始化渲染器
            _textureRenderer.Initialize(_physicalCore, renderTextureSize, conoscopicFOV, laserColor);
        }

        /// <summary>
        /// 注册到 CrystalRuntime
        /// </summary>
        private void RegisterToRuntime()
        {
            CrystalRuntime.CrystalObject = crystalModel;
            CrystalRuntime.Controller = _controller;
            CrystalRuntime.TextureRenderer = _textureRenderer;
            CrystalRuntime.PhysicalCore = _physicalCore;

            Debug.Log("[CrystalComponentInitializer] 已注册到 CrystalRuntime");
        }

        /// <summary>
        /// 加载选择的 Profile
        /// </summary>
        private void LoadSelectedProfile()
        {
            // 1. 检查是否有选择数据
            if (!CrystalSelectionData.HasSelection)
            {
                Debug.LogWarning("[CrystalComponentInitializer] 无晶体选择数据，可能是直接进入场景");
                return;
            }

            var selectedProfile = CrystalSelectionData.SelectedProfile;

            // 2. 验证 Profile 有效
            if (selectedProfile == null)
            {
                Debug.LogWarning("[CrystalComponentInitializer] 选择的 Profile 为 null");
                return;
            }

            // 3. 应用到控制器
            if (_controller != null && _controller.IsInitialized())
            {
                _controller.SetProfile(selectedProfile);
                Debug.Log($"[CrystalComponentInitializer] 已加载晶体 Profile: {selectedProfile.crystalName}");
            }
            else
            {
                Debug.LogError("[CrystalComponentInitializer] 控制器未初始化，无法加载 Profile");
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取晶体控制器
        /// </summary>
        public CrystalControllerWrapper GetController() => _controller;

        /// <summary>
        /// 获取晶体模型
        /// </summary>
        public GameObject GetCrystalModel() => crystalModel;

        /// <summary>
        /// 获取纹理渲染器
        /// </summary>
        public ConoscopicTextureRenderer GetTextureRenderer() => _textureRenderer;

        #endregion

        #region 编辑器调试

#if UNITY_EDITOR
        [ContextMenu("重新初始化")]
        private void ContextMenuReinitialize()
        {
            Start();
        }

        [ContextMenu("打印状态")]
        private void ContextMenuPrintStatus()
        {
            Debug.Log($"[CrystalComponentInitializer] 状态报告:\n" +
                      $"  - 晶体模型: {(crystalModel != null ? crystalModel.name : "null")}\n" +
                      $"  - 控制器: {(_controller != null ? "已设置" : "null")}\n" +
                      $"  - 纹理渲染器: {(_textureRenderer != null ? "已设置" : "null")}\n" +
                      $"  - 物理核心: {(_physicalCore != null ? "已设置" : "null")}\n" +
                      $"  - CrystalSelectionData.HasSelection: {CrystalSelectionData.HasSelection}");
        }
#endif

        #endregion
    }
}
