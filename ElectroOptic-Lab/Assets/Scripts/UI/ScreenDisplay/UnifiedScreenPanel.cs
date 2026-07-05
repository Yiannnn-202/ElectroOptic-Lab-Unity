using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ElectroOptics.DataTransfer;

namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 统一光屏显示面板（ExecuteAlways：Edit 模式下也创建 UI 层级，方便 Inspector 调参）
    /// 左下角固定面板，集成红点追踪和锥光干涉双图层显示，
    /// 根据晶体吸附状态自动切换模式，带淡入淡出过渡动画
    /// </summary>
    [ExecuteAlways]
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

        [Header("点击交互")]
        [Tooltip("双击间隔阈值（秒）")]
        [SerializeField] private float doubleClickInterval = 0.3f;

        [Tooltip("双击后加载的目标场景名")]
        [SerializeField] private string targetSceneName = "Scene_additional_exp";

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
        private bool _isFading;

        // 面板整体 CanvasGroup（用于淡入淡出）
        private CanvasGroup _panelGroup;

        #endregion

        #region Unity 生命周期

        private void OnEnable()
        {
            // Edit 模式下自动构建 UI 层级，让 ScreenPanelInteraction 在 Inspector 中可见
            if (!Application.isPlaying)
            {
                BuildVisualHierarchy();
                Show();
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                // Play 模式：如果 Edit 模式还没构建，现在构建；否则复用已有层级
                BuildVisualHierarchy();
                InitializePlayMode();
            }
        }

        private void Update()
        {
            // Edit 模式不需要 play 逻辑
            if (!Application.isPlaying) return;
            if (!_isInitialized) return;

            // 管理面板可见性：光屏闲置在桌面上时隐藏，吸附导轨/被选中时显示
            bool screenOnDesktop = screenOpticalComponent != null
                                   && !screenOpticalComponent.isOnRail
                                   && !screenOpticalComponent.isSelected;
            bool shouldShow = !screenOnDesktop;

            if (shouldShow != _isVisible && !_isFading)
            {
                StartCoroutine(FadePanel(shouldShow));
            }

            // 面板隐藏时不检测模式切换和渲染
            if (!_isVisible && !_isFading) return;

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
                if (Application.isPlaying)
                    Destroy(_panelObject);
                else
                    DestroyImmediate(_panelObject);
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 构建 UI 可视化层级（Edit 和 Play 模式共用，带重复创建保护）
        /// </summary>
        private void BuildVisualHierarchy()
        {
            // 防止重复创建（Edit 模式下 OnEnable / OnValidate 可能多次触发）
            if (_panelObject != null)
            {
                SyncInspectorToComponents();
                return;
            }

            EnsureEventSystem();
            CreateCanvas();

            // 检查 Canvas 下是否已有 panel（防止重复）
            Transform existingPanel = _canvas.transform.Find("UnifiedScreenPanel");
            if (existingPanel != null)
            {
                _panelObject = existingPanel.gameObject;
                _panelRect = _panelObject.GetComponent<RectTransform>();
                _panelGroup = _panelObject.GetComponent<CanvasGroup>();
                // 恢复子对象引用
                RestoreChildReferences();
                // 同步 Inspector 参数到已有组件
                SyncInspectorToComponents();
                return;
            }

            CreatePanel();
            CreateLayers();
            CreateClickOverlay();

            // Edit 模式下标记场景已修改（但避免每次 OnEnable 都标记）
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        /// <summary>
        /// 恢复对已有子对象的引用（Edit 模式 undo/redo 或 reload 后）
        /// </summary>
        private void RestoreChildReferences()
        {
            if (_panelObject == null) return;

            _conoscopicLayerObj = _panelObject.transform.Find("ConoscopicLayer")?.gameObject;
            if (_conoscopicLayerObj != null)
            {
                _conoscopicLayerGroup = _conoscopicLayerObj.GetComponent<CanvasGroup>();
                Transform content = _conoscopicLayerObj.transform.Find("Content");
                if (content != null) _conoscopicLayerImage = content.GetComponent<RawImage>();
            }

            _directLayerObj = _panelObject.transform.Find("DirectLayer")?.gameObject;
            if (_directLayerObj != null)
            {
                _directLayerGroup = _directLayerObj.GetComponent<CanvasGroup>();
                Transform content = _directLayerObj.transform.Find("Content");
                if (content != null) _directLayerImage = content.GetComponent<RawImage>();
            }
        }

        /// <summary>
        /// 将 Inspector 上的参数同步到已创建的 runtime 组件
        /// </summary>
        private void SyncInspectorToComponents()
        {
            if (_panelObject == null) return;

            if (_panelRect == null)
            {
                _panelRect = _panelObject.GetComponent<RectTransform>();
            }

            if (_panelRect != null)
            {
                _panelRect.anchorMin = Vector2.zero;
                _panelRect.anchorMax = Vector2.zero;
                _panelRect.pivot = Vector2.zero;
                _panelRect.anchoredPosition = panelPosition;
                _panelRect.sizeDelta = panelSize;
            }

            // 同步 ScreenPanelInteraction（在 ClickOverlay 上）
            Transform clickOverlay = _panelObject.transform.Find("ClickOverlay");
            if (clickOverlay != null)
            {
                ConfigureClickOverlay(clickOverlay.gameObject);
            }
        }

        /// <summary>
        /// Play 模式专用初始化（数据提供者、纹理绑定、初始状态）
        /// </summary>
        private void InitializePlayMode()
        {
            if (_isInitialized) return;

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

            // 绑定纹理
            BindTextures();

            // 初始状态：Direct 模式，面板隐藏（等待光屏吸附到导轨）
            SetModeImmediate(ScreenMode.Direct);
            Hide();
            _isVisible = false;

            // 同步 Inspector 参数到组件
            SyncInspectorToComponents();

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
                // 确保 GraphicRaycaster 存在
                if (canvasObj.GetComponent<GraphicRaycaster>() == null)
                {
                    canvasObj.AddComponent<GraphicRaycaster>();
                }
            }
        }

        private void CreatePanel()
        {
            _panelObject = new GameObject("UnifiedScreenPanel");
            _panelObject.transform.SetParent(_canvas.transform, false);

            _panelRect = _panelObject.AddComponent<RectTransform>();
            _panelRect.anchorMin = Vector2.zero;
            _panelRect.anchorMax = Vector2.zero;
            _panelRect.pivot = Vector2.zero;
            _panelRect.anchoredPosition = panelPosition;
            _panelRect.sizeDelta = panelSize;

            // 面板 CanvasGroup（用于整体淡入淡出）
            _panelGroup = _panelObject.AddComponent<CanvasGroup>();

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

            Image conoscopicBg = _conoscopicLayerObj.AddComponent<Image>();
            conoscopicBg.color = Color.black;
            conoscopicBg.raycastTarget = false;

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

            Image directBg = _directLayerObj.AddComponent<Image>();
            directBg.color = Color.black;
            directBg.raycastTarget = false;

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
        }

        private void CreateClickOverlay()
        {
            // 透明点击覆盖层 — 最顶层子物体
            GameObject overlay = new GameObject("ClickOverlay");
            overlay.transform.SetParent(_panelObject.transform, false);

            RectTransform rect = overlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = overlay.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = true;

            ConfigureClickOverlay(overlay);

            Debug.Log($"{LOG_PREFIX} 点击覆盖层已创建");
        }

        private void ConfigureClickOverlay(GameObject overlay)
        {
            if (overlay == null) return;

            var interaction = overlay.GetComponent<ScreenPanelInteraction>();
            if (interaction == null)
            {
                interaction = overlay.AddComponent<ScreenPanelInteraction>();
            }
            interaction.doubleClickInterval = doubleClickInterval;
            interaction.targetSceneName = targetSceneName;

            var outline = overlay.GetComponent<UIOutline>();
            if (outline == null)
            {
                outline = overlay.AddComponent<UIOutline>();
            }
            outline.IsHighlighted = false;
        }

        private void BindTextures()
        {
            if (_directDataProvider != null && _directDataProvider.IsAvailable)
            {
                _directLayerImage.texture = _directDataProvider.GetTexture();
            }

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

        public void SwitchToMode(ScreenMode mode)
        {
            if (mode == CurrentMode || IsTransitioning) return;
            StartCoroutine(SwitchModeWithTransition(mode));
        }

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

            if (newMode == ScreenMode.Conoscopic && _conoscopicDataProvider != null)
            {
                _conoscopicDataProvider.PreRender();
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

            yield return new WaitForEndOfFrame();

            targetLayer.gameObject.SetActive(true);
            targetLayer.alpha = 0f;

            yield return CanvasGroupTweener.CrossFade(currentLayer, targetLayer, transitionDuration);

            currentLayer.gameObject.SetActive(false);

            CurrentMode = newMode;
            IsTransitioning = false;
        }

        private void SetModeImmediate(ScreenMode mode)
        {
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

        private IEnumerator FadePanel(bool show)
        {
            _isFading = true;
            _isVisible = show;

            if (show)
            {
                _panelObject.SetActive(true);
                yield return CanvasGroupTweener.FadeIn(_panelGroup, transitionDuration);
            }
            else
            {
                yield return CanvasGroupTweener.FadeOut(_panelGroup, transitionDuration);
                _panelObject.SetActive(false);
            }

            _isFading = false;
        }

        public void Show()
        {
            if (_panelObject != null)
            {
                _panelObject.SetActive(true);
                CanvasGroupTweener.SetAlpha(_panelGroup, 1f);
                _isVisible = true;
            }
        }

        public void Hide()
        {
            if (_panelObject != null)
            {
                _panelObject.SetActive(false);
                CanvasGroupTweener.SetAlpha(_panelGroup, 0f);
                _isVisible = false;
            }
        }

        public void SetPosition(Vector2 anchoredPosition)
        {
            if (_panelRect != null)
            {
                _panelRect.anchoredPosition = anchoredPosition;
            }
        }

        public void SetSize(Vector2 size)
        {
            if (_panelRect != null)
            {
                _panelRect.sizeDelta = size;
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            panelSize.x = Mathf.Max(1f, panelSize.x);
            panelSize.y = Mathf.Max(1f, panelSize.y);

            UnityEditor.EditorApplication.delayCall -= DelayedEditorSync;
            UnityEditor.EditorApplication.delayCall += DelayedEditorSync;
        }

        private void DelayedEditorSync()
        {
            if (this == null)
            {
                return;
            }

            if (!Application.isPlaying && isActiveAndEnabled)
            {
                BuildVisualHierarchy();
                SyncInspectorToComponents();
                UnityEditor.EditorUtility.SetDirty(this);
                if (_panelRect != null)
                {
                    UnityEditor.EditorUtility.SetDirty(_panelRect);
                }
            }
        }
#endif

        #region 编辑器调试

#if UNITY_EDITOR
        [ContextMenu("强制显示面板（测试点击）")]
        private void DebugForceShow()
        {
            if (!Application.isPlaying)
            {
                Debug.Log($"{LOG_PREFIX} 请在 Play 模式下使用此菜单");
                return;
            }
            if (_panelObject == null)
            {
                Debug.LogError($"{LOG_PREFIX} 面板尚未创建，请先运行 Initialize");
                return;
            }
            Show();
            _isVisible = true;
            Debug.Log($"{LOG_PREFIX} 面板已强制显示，现在可以测试点击交互");
        }

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
