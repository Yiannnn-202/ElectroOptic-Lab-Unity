using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ElectroOptics.DataTransfer;

namespace ElectroOptics.UI.ScreenPopup
{
    /// <summary>
    /// 锥光干涉弹窗视图
    /// 显示锥光干涉 RenderTexture，每帧更新渲染
    /// </summary>
    public class ConoscopicWindowView : MonoBehaviour
    {
        #region 常量

        private const string WINDOW_CANVAS_NAME = "WindowsCanvas";

        #endregion

        #region 私有字段

        private RectTransform _windowRect;
        private RawImage _patternImage;
        private Text _titleText;
        private GameObject _windowObject;
        private Vector2 _windowSize;
        private string _windowTitle;
        private bool _isInitialized;

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 创建锥光干涉弹窗
        /// </summary>
        /// <param name="size">窗口尺寸</param>
        /// <param name="title">窗口标题</param>
        /// <returns>弹窗视图实例</returns>
        public static ConoscopicWindowView CreateWindow(Vector2 size, string title)
        {
            Canvas canvas = GetOrCreateCanvas();
            EnsureEventSystem();

            // 创建窗口对象
            GameObject windowObj = new GameObject("ConoscopicWindow");
            windowObj.transform.SetParent(canvas.transform, false);

            // 添加组件
            ConoscopicWindowView view = windowObj.AddComponent<ConoscopicWindowView>();
            view._windowSize = size;
            view._windowTitle = title;
            view._windowObject = windowObj;
            view.InitializeWindow();

            return view;
        }

        /// <summary>
        /// 获取或创建 WindowsCanvas
        /// </summary>
        private static Canvas GetOrCreateCanvas()
        {
            GameObject canvasObj = GameObject.Find(WINDOW_CANVAS_NAME);
            Canvas canvas;

            if (canvasObj == null)
            {
                canvasObj = new GameObject(WINDOW_CANVAS_NAME);
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                canvas = canvasObj.GetComponent<Canvas>();
            }

            return canvas;
        }

        /// <summary>
        /// 确保 EventSystem 存在
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化窗口 UI
        /// </summary>
        private void InitializeWindow()
        {
            // 确保有 RectTransform（UI 元素必须）
            _windowRect = GetComponent<RectTransform>();
            if (_windowRect == null)
            {
                _windowRect = gameObject.AddComponent<RectTransform>();
            }
            _windowRect.sizeDelta = _windowSize;
            _windowRect.anchoredPosition = new Vector2(Random.Range(-100, 100), Random.Range(-50, 50));
            _windowRect.pivot = new Vector2(0.5f, 0.5f);
            _windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            _windowRect.anchorMax = new Vector2(0.5f, 0.5f);

            // 窗口背景（浅色风格）
            Image windowBg = gameObject.AddComponent<Image>();
            windowBg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            windowBg.raycastTarget = true;

            // 创建标题栏
            CreateTitleBar();

            // 创建内容区域（显示干涉图）
            CreatePatternView();

            // 添加拖拽功能
            gameObject.AddComponent<SimpleDrag>();

            _isInitialized = true;
            Debug.Log($"[ConoscopicWindowView] 窗口初始化完成，尺寸: {_windowSize}");
        }

        /// <summary>
        /// 创建标题栏
        /// </summary>
        private void CreateTitleBar()
        {
            // 标题栏容器
            GameObject titleBarObj = new GameObject("TitleBar");
            titleBarObj.transform.SetParent(transform, false);

            RectTransform titleBarRect = titleBarObj.AddComponent<RectTransform>();
            titleBarRect.anchorMin = new Vector2(0, 1);
            titleBarRect.anchorMax = new Vector2(1, 1);
            titleBarRect.pivot = new Vector2(0.5f, 1);
            titleBarRect.anchoredPosition = Vector2.zero;
            titleBarRect.sizeDelta = new Vector2(0, 40);

            Image titleBarBg = titleBarObj.AddComponent<Image>();
            titleBarBg.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // 标题文本
            GameObject titleTextObj = new GameObject("TitleText");
            titleTextObj.transform.SetParent(titleBarObj.transform, false);

            RectTransform titleTextRect = titleTextObj.AddComponent<RectTransform>();
            titleTextRect.anchorMin = Vector2.zero;
            titleTextRect.anchorMax = new Vector2(1, 1);
            titleTextRect.offsetMin = new Vector2(10, 5);
            titleTextRect.offsetMax = new Vector2(-40, -5);

            _titleText = titleTextObj.AddComponent<Text>();
            _titleText.text = _windowTitle;
            _titleText.color = Color.black;
            _titleText.font = GetDefaultFont();
            _titleText.fontSize = 16;
            _titleText.alignment = TextAnchor.MiddleLeft;

            // 关闭按钮
            CreateCloseButton(titleBarObj);
        }

        /// <summary>
        /// 创建关闭按钮
        /// </summary>
        private void CreateCloseButton(GameObject parent)
        {
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(parent.transform, false);

            RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-20, 0);
            closeRect.sizeDelta = new Vector2(30, 30);

            Image closeBg = closeBtnObj.AddComponent<Image>();
            closeBg.color = new Color(1f, 0.3f, 0.3f, 1f);

            Button closeButton = closeBtnObj.AddComponent<Button>();
            closeButton.targetGraphic = closeBg;

            // 按钮颜色
            ColorBlock colors = closeButton.colors;
            colors.highlightedColor = new Color(1f, 0.5f, 0.5f, 1f);
            colors.pressedColor = new Color(0.8f, 0.2f, 0.2f, 1f);
            closeButton.colors = colors;

            // 关闭按钮文本
            GameObject closeTextObj = new GameObject("Text");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);

            RectTransform closeTextRect = closeTextObj.AddComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;

            Text closeText = closeTextObj.AddComponent<Text>();
            closeText.text = "X";
            closeText.color = Color.white;
            closeText.font = GetDefaultFont();
            closeText.fontSize = 18;
            closeText.alignment = TextAnchor.MiddleCenter;

            // 点击事件
            closeButton.onClick.AddListener(Hide);
        }

        /// <summary>
        /// 创建内容区域（干涉图显示）
        /// </summary>
        private void CreatePatternView()
        {
            GameObject patternObj = new GameObject("PatternView");
            patternObj.transform.SetParent(transform, false);

            RectTransform patternRect = patternObj.AddComponent<RectTransform>();
            patternRect.anchorMin = new Vector2(0.05f, 0.05f);
            patternRect.anchorMax = new Vector2(0.95f, 0.9f);
            patternRect.offsetMin = Vector2.zero;
            patternRect.offsetMax = Vector2.zero;
            patternRect.pivot = new Vector2(0.5f, 0.5f);

            _patternImage = patternObj.AddComponent<RawImage>();
            _patternImage.color = Color.black;

            // 设置 RenderTexture
            if (CrystalRuntime.IsInitialized && CrystalRuntime.TextureRenderer != null)
            {
                _patternImage.texture = CrystalRuntime.TextureRenderer.RenderTexture;
            }
        }

        /// <summary>
        /// 获取默认字体
        /// </summary>
        private Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
            return font;
        }

        #endregion

        #region 显示/隐藏

        /// <summary>
        /// 显示窗口
        /// </summary>
        public void Show()
        {
            if (_windowObject != null)
            {
                // 更新 RenderTexture 引用
                if (_patternImage != null && CrystalRuntime.IsInitialized && CrystalRuntime.TextureRenderer != null)
                {
                    _patternImage.texture = CrystalRuntime.TextureRenderer.RenderTexture;
                }

                _windowObject.SetActive(true);
                _windowObject.transform.SetAsLastSibling();
                Debug.Log("[ConoscopicWindowView] 显示锥光干涉弹窗");
            }
        }

        /// <summary>
        /// 隐藏窗口
        /// </summary>
        public void Hide()
        {
            if (_windowObject != null)
            {
                _windowObject.SetActive(false);
                Debug.Log("[ConoscopicWindowView] 隐藏锥光干涉弹窗");
            }
        }

        /// <summary>
        /// 检查窗口是否可见
        /// </summary>
        public bool IsVisible()
        {
            return _windowObject != null && _windowObject.activeSelf;
        }

        #endregion

        #region Unity 生命周期

        private void Update()
        {
            // 每帧更新渲染
            if (IsVisible() && CrystalRuntime.IsInitialized && CrystalRuntime.TextureRenderer != null)
            {
                CrystalRuntime.TextureRenderer.UpdateAndRender();
            }
        }

        #endregion
    }
}
