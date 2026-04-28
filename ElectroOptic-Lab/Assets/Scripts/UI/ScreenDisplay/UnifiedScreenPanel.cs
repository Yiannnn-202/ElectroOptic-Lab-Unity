using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ElectroOptics.DataTransfer;

namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 统一光屏显示面板
    /// 左下角固定面板，集成红点追踪和锥光干涉双图层显示，
    /// 根据晶体吸附状态自动切换模式，带淡入淡出过渡动画
    /// </summary>
    public class UnifiedScreenPanel : MonoBehaviour
    {
        private const string LOG_PREFIX = "[UnifiedScreenPanel]";

        #region Inspector 配置

        [Header("引用配置")]
        [Tooltip("光屏上的 DirectScreenController 组件")]
        [SerializeField] private DirectScreenController directScreenController;
        [SerializeField] private OpticalComponent screenOpticalComponent;
        [SerializeField] private OpticalComponent beamExpanderOpticalComponent;
        [SerializeField] private OpticalComponent crystalOpticalComponent;

        [Header("面板配置")]
        [Tooltip("面板尺寸")]
        [SerializeField] private Vector2 panelSize = new Vector2(600, 600);

        [Tooltip("面板位置（左下角偏移）")]
        [SerializeField] private Vector2 panelPosition = new Vector2(320, 200);

        [Header("过渡配置")]
        [Tooltip("淡入淡出时长（秒）")]
        [SerializeField] private float transitionDuration = 0.3f;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前显示模式
        /// </summary>
        public ScreenMode CurrentMode { get; private set; } = ScreenMode.Direct;

        /// <summary>
        /// 是否正在过渡中
        /// </summary>
        public bool IsTransitioning { get; private set; }

        #endregion

        #region 私有字段

        // UI 层级
        private Canvas _canvas;
        private GameObject _panelObject;
        private RectTransform _panelRect;

        // 双图层
        private GameObject _directLayerObj;
        private GameObject _conoscopicLayerObj;
        private CanvasGroup _directLayerGroup;
        private CanvasGroup _conoscopicLayerGroup;
        private RawImage _directLayerImage;
        private RawImage _conoscopicLayerImage;

        // 数据提供者
        private IScreenDataProvider _directDataProvider;
        private IScreenDataProvider _conoscopicDataProvider;

        // 状态
        private bool _isInitialized;
        private bool _isVisible = true;

        #endregion

        #region Unity 生命周期

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // 检测模式切换
            ScreenMode targetMode = DetectTargetMode();
            if (targetMode != CurrentMode && !IsTransitioning)
            {
                StartCoroutine(SwitchModeWithTransition(targetMode));
            }

            // 驱动锥光干涉每帧渲染
            if (CurrentMode == ScreenMode.Conoscopic && !IsTransitioning)
            {
                if (_conoscopicDataProvider != null && _conoscopicDataProvider.IsAvailable)
                {
                    _conoscopicDataProvider.PreRender();
                }
            }
        }

        private void OnDestroy()
        {
            if (_panelObject != null)
            {
                Destroy(_panelObject);
            }
        }

        #endregion

        #region 初始化

        private void Initialize()
        {
            if (directScreenController == null)
            {
                Debug.LogError($"{LOG_PREFIX} DirectScreenController 引用为空，自动切换禁用");
            }
            ResolvePlacementReferences();

            // 创建数据提供者
            if (directScreenController != null)
            {
                _directDataProvider = new DirectScreenDataProvider(directScreenController);
            }
            _conoscopicDataProvider = new ConoscopicScreenDataProvider();

            // 构建 UI
            EnsureEventSystem();
            CreateCanvas();
            CreatePanel();
            CreateLayers();

            // 初始状态：Direct 模式
            SetModeImmediate(ScreenMode.Direct);

            _isInitialized = true;
            Debug.Log($"{LOG_PREFIX} 初始化完成");
        }

        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }
        }

        private void CreateCanvas()
        {
            // 复用或创建 WindowsCanvas
            GameObject canvasObj = GameObject.Find("WindowsCanvas");

            if (canvasObj == null)
            {
                canvasObj = new GameObject("WindowsCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                _canvas = canvasObj.GetComponent<Canvas>();
            }
        }

        private void CreatePanel()
        {
            // 面板根对象
            _panelObject = new GameObject("UnifiedScreenPanel");
            _panelObject.transform.SetParent(_canvas.transform, false);

            _panelRect = _panelObject.AddComponent<RectTransform>();
            _panelRect.anchorMin = Vector2.zero; // 左下角
            _panelRect.anchorMax = Vector2.zero;
            _panelRect.pivot = Vector2.zero;     // 轴心在左下角
            _panelRect.anchoredPosition = panelPosition;
            _panelRect.sizeDelta = panelSize;

            // 面板背景
            Image bg = _panelObject.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            bg.raycastTarget = true;
        }

        private void CreateLayers()
        {
            // --- 锥光干涉图层（底层） ---
            _conoscopicLayerObj = new GameObject("ConoscopicLayer");
            _conoscopicLayerObj.transform.SetParent(_panelObject.transform, false);

            RectTransform conoscopicRect = _conoscopicLayerObj.AddComponent<RectTransform>();
            conoscopicRect.anchorMin = Vector2.zero;
            conoscopicRect.anchorMax = Vector2.one;
            conoscopicRect.offsetMin = Vector2.zero;
            conoscopicRect.offsetMax = Vector2.zero;

            _conoscopicLayerGroup = _conoscopicLayerObj.AddComponent<CanvasGroup>();
            _conoscopicLayerGroup.alpha = 0f;

            // 黑色背景 Image
            Image conoscopicBg = _conoscopicLayerObj.AddComponent<Image>();
            conoscopicBg.color = Color.black;
            conoscopicBg.raycastTarget = false;

            // 内容 RawImage（子对象，留边距）
            GameObject conoscopicContent = new GameObject("Content");
            conoscopicContent.transform.SetParent(_conoscopicLayerObj.transform, false);
            RectTransform ccRect = conoscopicContent.AddComponent<RectTransform>();
            ccRect.anchorMin = new Vector2(0.02f, 0.02f);
            ccRect.anchorMax = new Vector2(0.98f, 0.98f);
            ccRect.offsetMin = Vector2.zero;
            ccRect.offsetMax = Vector2.zero;

            _conoscopicLayerImage = conoscopicContent.AddComponent<RawImage>();
            _conoscopicLayerImage.color = Color.white;
            _conoscopicLayerImage.raycastTarget = false;

            // --- 红点追踪图层（顶层） ---
            _directLayerObj = new GameObject("DirectLayer");
            _directLayerObj.transform.SetParent(_panelObject.transform, false);

            RectTransform directRect = _directLayerObj.AddComponent<RectTransform>();
            directRect.anchorMin = Vector2.zero;
            directRect.anchorMax = Vector2.one;
            directRect.offsetMin = Vector2.zero;
            directRect.offsetMax = Vector2.zero;

            _directLayerGroup = _directLayerObj.AddComponent<CanvasGroup>();
            _directLayerGroup.alpha = 1f;

            // 白色背景 Image
            Image directBg = _directLayerObj.AddComponent<Image>();
            directBg.color = Color.white;
            directBg.raycastTarget = false;

            // 内容 RawImage（子对象，留边距）
            GameObject directContent = new GameObject("Content");
            directContent.transform.SetParent(_directLayerObj.transform, false);
            RectTransform dcRect = directContent.AddComponent<RectTransform>();
            dcRect.anchorMin = new Vector2(0.02f, 0.02f);
            dcRect.anchorMax = new Vector2(0.98f, 0.98f);
            dcRect.offsetMin = Vector2.zero;
            dcRect.offsetMax = Vector2.zero;

            _directLayerImage = directContent.AddComponent<RawImage>();
            _directLayerImage.color = Color.white;
            _directLayerImage.raycastTarget = false;

            // 绑定纹理
            BindTextures();
        }

        private void BindTextures()
        {
            // 绑定红点追踪纹理（Texture2D 就地修改，只需绑定一次）
            if (_directDataProvider != null && _directDataProvider.IsAvailable)
            {
                _directLayerImage.texture = _directDataProvider.GetTexture();
            }

            // 绑定锥光干涉纹理
            if (_conoscopicDataProvider != null && _conoscopicDataProvider.IsAvailable)
            {
                _conoscopicLayerImage.texture = _conoscopicDataProvider.GetTexture();
            }
        }

        #endregion

        #region 模式检测

        private ScreenMode DetectTargetMode()
        {
            if (AreConoscopicRequiredComponentsOnRail())
            {
                // 还需要确保锥光渲染器可用
                if (_conoscopicDataProvider != null && _conoscopicDataProvider.IsAvailable)
                {
                    return ScreenMode.Conoscopic;
                }
            }
            return ScreenMode.Direct;
        }

        private bool AreConoscopicRequiredComponentsOnRail()
        {
            ResolvePlacementReferences();

            return IsComponentOnRail(screenOpticalComponent)
                   && IsComponentOnRail(beamExpanderOpticalComponent)
                   && IsComponentOnRail(crystalOpticalComponent);
        }

        private static bool IsComponentOnRail(OpticalComponent component)
        {
            return component != null && component.gameObject.activeInHierarchy && component.isOnRail;
        }

        private void ResolvePlacementReferences()
        {
            if (screenOpticalComponent == null && directScreenController != null)
            {
                screenOpticalComponent = directScreenController.GetComponentInParent<OpticalComponent>();
            }

            if (screenOpticalComponent == null)
            {
                screenOpticalComponent = FindOpticalComponentByName("光屏");
            }

            if (beamExpanderOpticalComponent == null)
            {
                beamExpanderOpticalComponent = FindOpticalComponentByName("扩束镜", "晶体盒");
            }

            if (ShouldResolveCrystalReference())
            {
                crystalOpticalComponent = GetCurrentCrystalOpticalComponent();
            }

            if (crystalOpticalComponent == null)
            {
                crystalOpticalComponent = FindOpticalComponentByName("晶体");
            }
        }

        private bool ShouldResolveCrystalReference()
        {
            return crystalOpticalComponent == null
                   || !crystalOpticalComponent.gameObject.activeInHierarchy
                   || (CrystalRuntime.CrystalObject != null
                       && !crystalOpticalComponent.transform.IsChildOf(CrystalRuntime.CrystalObject.transform)
                       && crystalOpticalComponent.gameObject != CrystalRuntime.CrystalObject);
        }

        private OpticalComponent GetCurrentCrystalOpticalComponent()
        {
            if (CrystalRuntime.CrystalObject != null)
            {
                OpticalComponent runtimeCrystal = CrystalRuntime.CrystalObject.GetComponent<OpticalComponent>();
                if (runtimeCrystal != null) return runtimeCrystal;

                runtimeCrystal = CrystalRuntime.CrystalObject.GetComponentInChildren<OpticalComponent>();
                if (runtimeCrystal != null) return runtimeCrystal;

                runtimeCrystal = CrystalRuntime.CrystalObject.GetComponentInParent<OpticalComponent>();
                if (runtimeCrystal != null) return runtimeCrystal;
            }

            if (directScreenController != null && directScreenController.crystalOpticalComponent != null)
            {
                return directScreenController.crystalOpticalComponent;
            }

            return null;
        }

        private static OpticalComponent FindOpticalComponentByName(params string[] objectNames)
        {
            foreach (string objectName in objectNames)
            {
                GameObject found = GameObject.Find(objectName);
                if (found == null) continue;

                OpticalComponent component = found.GetComponent<OpticalComponent>();
                if (component != null) return component;

                component = found.GetComponentInChildren<OpticalComponent>();
                if (component != null) return component;

                component = found.GetComponentInParent<OpticalComponent>();
                if (component != null) return component;
            }

            return null;
        }

        #endregion

        #region 模式切换

        /// <summary>
        /// 切换到指定模式（带过渡动画）
        /// </summary>
        public void SwitchToMode(ScreenMode mode)
        {
            if (mode == CurrentMode || IsTransitioning) return;
            StartCoroutine(SwitchModeWithTransition(mode));
        }

        /// <summary>
        /// 切换到指定模式（立即切换，无动画）
        /// </summary>
        public void SwitchToModeImmediate(ScreenMode mode)
        {
            SetModeImmediate(mode);
        }

        private IEnumerator SwitchModeWithTransition(ScreenMode newMode)
        {
            if (IsTransitioning) yield break;
            IsTransitioning = true;

            Debug.Log($"{LOG_PREFIX} 模式切换: {CurrentMode} -> {newMode}");

            CanvasGroup targetLayer = (newMode == ScreenMode.Conoscopic) ? _conoscopicLayerGroup : _directLayerGroup;
            CanvasGroup currentLayer = (newMode == ScreenMode.Conoscopic) ? _directLayerGroup : _conoscopicLayerGroup;

            // 预渲染目标纹理
            if (newMode == ScreenMode.Conoscopic && _conoscopicDataProvider != null)
            {
                _conoscopicDataProvider.PreRender();
                // 确保纹理已绑定
                if (_conoscopicDataProvider.IsAvailable)
                {
                    _conoscopicLayerImage.texture = _conoscopicDataProvider.GetTexture();
                }
            }
            else if (newMode == ScreenMode.Direct && _directDataProvider != null)
            {
                _directDataProvider.PreRender();
                if (_directDataProvider.IsAvailable)
                {
                    _directLayerImage.texture = _directDataProvider.GetTexture();
                }
            }

            // 等待 GPU 完成渲染
            yield return new WaitForEndOfFrame();

            // 激活目标图层（alpha=0）
            targetLayer.gameObject.SetActive(true);
            targetLayer.alpha = 0f;

            // 并行淡入淡出
            yield return CanvasGroupTweener.CrossFade(currentLayer, targetLayer, transitionDuration);

            // 隐藏原图层
            currentLayer.gameObject.SetActive(false);

            CurrentMode = newMode;
            IsTransitioning = false;
        }

        private void SetModeImmediate(ScreenMode mode)
        {
            // 确保纹理已绑定
            BindTextures();

            if (mode == ScreenMode.Direct)
            {
                _directLayerObj.SetActive(true);
                _directLayerGroup.alpha = 1f;
                _conoscopicLayerObj.SetActive(false);
                _conoscopicLayerGroup.alpha = 0f;
            }
            else
            {
                _conoscopicLayerObj.SetActive(true);
                _conoscopicLayerGroup.alpha = 1f;
                _directLayerObj.SetActive(false);
                _directLayerGroup.alpha = 0f;
            }

            CurrentMode = mode;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 显示面板
        /// </summary>
        public void Show()
        {
            if (_panelObject != null)
            {
                _panelObject.SetActive(true);
                _isVisible = true;
            }
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
            if (_panelObject != null)
            {
                _panelObject.SetActive(false);
                _isVisible = false;
            }
        }

        /// <summary>
        /// 设置面板位置
        /// </summary>
        public void SetPosition(Vector2 anchoredPosition)
        {
            if (_panelRect != null)
            {
                _panelRect.anchoredPosition = anchoredPosition;
            }
        }

        /// <summary>
        /// 设置面板尺寸
        /// </summary>
        public void SetSize(Vector2 size)
        {
            if (_panelRect != null)
            {
                _panelRect.sizeDelta = size;
            }
        }

        #endregion

        #region 编辑器调试

#if UNITY_EDITOR
        [ContextMenu("切换到 Direct 模式")]
        private void DebugSwitchToDirect()
        {
            SwitchToMode(ScreenMode.Direct);
        }

        [ContextMenu("切换到 Conoscopic 模式")]
        private void DebugSwitchToConoscopic()
        {
            SwitchToMode(ScreenMode.Conoscopic);
        }

        [ContextMenu("打印状态")]
        private void DebugPrintStatus()
        {
            Debug.Log($"{LOG_PREFIX} 状态:\n" +
                      $"  - 已初始化: {_isInitialized}\n" +
                      $"  - 当前模式: {CurrentMode}\n" +
                      $"  - 过渡中: {IsTransitioning}\n" +
                      $"  - 可见: {_isVisible}\n" +
                      $"  - DirectProvider 可用: {_directDataProvider?.IsAvailable}\n" +
                      $"  - ConoscopicProvider 可用: {_conoscopicDataProvider?.IsAvailable}\n" +
                      $"  - DirectScreenController: {(directScreenController != null ? "已设置" : "null")}\n" +
                      $"  - screenOnRail: {screenOpticalComponent?.isOnRail}\n" +
                      $"  - beamExpanderOnRail: {beamExpanderOpticalComponent?.isOnRail}\n" +
                      $"  - crystalOnRail: {crystalOpticalComponent?.isOnRail}");
        }
#endif

        #endregion
    }
}
