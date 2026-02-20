using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ElectroOptics.DataTransfer;
using ElectroOptics.Experiment.Controller;

namespace ElectroOptics.UI.ControlPanel
{
    /// <summary>
    /// 晶体旋转控制面板
    /// 双击晶体显示，包含两个旋钮控件控制X/Y轴旋转
    /// </summary>
    public class CrystalRotationPanel : MonoBehaviour
    {
        #region Inspector 配置

        [Header("旋钮配置")]
        [Tooltip("旋钮刻度盘图片（复用现有的刻度盘图片）")]
        [SerializeField] private Texture2D knobTexture;

        [Header("旋转范围")]
        [Tooltip("最小旋转角度")]
        [SerializeField] private float minRotation = -15f;

        [Tooltip("最大旋转角度")]
        [SerializeField] private float maxRotation = 15f;

        [Tooltip("键盘旋转步进（度）")]
        [SerializeField] private float rotationStep = 0.5f;

        [Tooltip("键盘旋转速度（度/秒）")]
        [SerializeField] private float rotateSpeed = 90f;

        [Header("双击配置")]
        [Tooltip("双击判定时间间隔（秒）")]
        [SerializeField] private float doubleClickInterval = 0.3f;

        [Header("面板配置")]
        [Tooltip("面板尺寸")]
        [SerializeField] private Vector2 windowSize = new Vector2(500, 400);

        [Tooltip("面板标题")]
        [SerializeField] private string windowTitle = "晶体旋转控制";

        #endregion

        #region 私有字段

        private float _lastClickTime = 0f;
        private GameObject _panelObject;
        private RectTransform _panelRect;
        private bool _isPanelVisible = false;
        private bool _isProcessingClick = false;

        // UI 组件引用
        private RotationKnob _xAxisKnob;
        private RotationKnob _yAxisKnob;
        private AngleDisplay _xAxisDisplay;
        private AngleDisplay _yAxisDisplay;

        // 当前旋转状态
        private Vector2 _currentRotation;

        #endregion

        #region Unity 生命周期

        private void Start()
        {
            _currentRotation = Vector2.zero;
        }

        private void Update()
        {
            // 处理点击检测（协程方式避免冲突）
            if (!_isProcessingClick)
            {
                StartCoroutine(HandleClickDetection());
            }

            // 处理键盘输入
            if (_isPanelVisible)
            {
                HandleKeyboardInput();
            }
        }

        #endregion

        #region 点击检测

        private System.Collections.IEnumerator HandleClickDetection()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _isProcessingClick = true;
                yield return null;

                // 检查是否点击在 UI 上
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    _isProcessingClick = false;
                    yield break;
                }

                // 检查是否点击到晶体
                if (IsClickingThisCrystal())
                {
                    float currentTime = Time.time;
                    if (currentTime - _lastClickTime <= doubleClickInterval)
                    {
                        Debug.Log($"[CrystalRotationPanel] 检测到双击晶体: {gameObject.name}");
                        TogglePanel();
                        _lastClickTime = 0f;
                    }
                    else
                    {
                        _lastClickTime = currentTime;
                    }
                }

                _isProcessingClick = false;
            }
        }

        /// <summary>
        /// 检查是否点击到当前晶体
        /// </summary>
        private bool IsClickingThisCrystal()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray);

            foreach (RaycastHit hit in hits)
            {
                // 检查是否点击到自身或子物体
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    return true;
                }

                // 检查是否有 CrystalControllerWrapper 组件
                var wrapper = hit.collider.GetComponentInParent<CrystalControllerWrapper>();
                if (wrapper != null && wrapper.gameObject == gameObject)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 面板显示/隐藏

        /// <summary>
        /// 切换面板显示状态
        /// </summary>
        public void TogglePanel()
        {
            if (_isPanelVisible)
            {
                HidePanel();
            }
            else
            {
                ShowPanel();
            }
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        public void ShowPanel()
        {
            if (_panelObject == null)
            {
                CreatePanel();
            }

            // 同步当前旋转状态
            SyncCurrentRotation();

            _panelObject.SetActive(true);
            _panelObject.transform.SetAsLastSibling();
            _isPanelVisible = true;

            Debug.Log("[CrystalRotationPanel] 显示旋转控制面板");
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel()
        {
            if (_panelObject != null)
            {
                _panelObject.SetActive(false);
                _isPanelVisible = false;
                Debug.Log("[CrystalRotationPanel] 隐藏旋转控制面板");
            }
        }

        #endregion

        #region 面板创建

        /// <summary>
        /// 创建面板 UI
        /// </summary>
        private void CreatePanel()
        {
            Canvas canvas = GetOrCreateCanvas();
            EnsureEventSystem();

            // 创建面板对象
            _panelObject = new GameObject("CrystalRotationPanel");
            _panelObject.transform.SetParent(canvas.transform, false);

            // 设置 RectTransform
            _panelRect = _panelObject.AddComponent<RectTransform>();
            _panelRect.sizeDelta = windowSize;
            _panelRect.anchoredPosition = new Vector2(Random.Range(-100, 100), Random.Range(-50, 50));
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);

            // 面板背景
            Image panelBg = _panelObject.AddComponent<Image>();
            panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // 创建标题栏
            CreateTitleBar();

            // 创建内容区域
            CreateContentArea();

            // 添加拖拽功能
            _panelObject.AddComponent<SimpleDrag>();
        }

        /// <summary>
        /// 获取或创建 Canvas
        /// </summary>
        private Canvas GetOrCreateCanvas()
        {
            GameObject canvasObj = GameObject.Find("WindowsCanvas");
            Canvas canvas;

            if (canvasObj == null)
            {
                canvasObj = new GameObject("WindowsCanvas");
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
        private void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
            }
        }

        /// <summary>
        /// 创建标题栏
        /// </summary>
        private void CreateTitleBar()
        {
            GameObject titleBarObj = new GameObject("TitleBar");
            titleBarObj.transform.SetParent(_panelObject.transform, false);

            RectTransform titleBarRect = titleBarObj.AddComponent<RectTransform>();
            titleBarRect.anchorMin = new Vector2(0, 1);
            titleBarRect.anchorMax = new Vector2(1, 1);
            titleBarRect.pivot = new Vector2(0.5f, 1);
            titleBarRect.anchoredPosition = Vector2.zero;
            titleBarRect.sizeDelta = new Vector2(0, 40);

            Image titleBarBg = titleBarObj.AddComponent<Image>();
            titleBarBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // 标题文本
            GameObject titleTextObj = new GameObject("TitleText");
            titleTextObj.transform.SetParent(titleBarObj.transform, false);

            RectTransform titleTextRect = titleTextObj.AddComponent<RectTransform>();
            titleTextRect.anchorMin = Vector2.zero;
            titleTextRect.anchorMax = new Vector2(1, 1);
            titleTextRect.offsetMin = new Vector2(10, 5);
            titleTextRect.offsetMax = new Vector2(-40, -5);

            Text titleText = titleTextObj.AddComponent<Text>();
            titleText.text = windowTitle;
            titleText.color = Color.white;
            titleText.font = GetDefaultFont();
            titleText.fontSize = 16;
            titleText.alignment = TextAnchor.MiddleLeft;

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

            closeButton.onClick.AddListener(HidePanel);
        }

        /// <summary>
        /// 创建内容区域（两个旋钮和角度显示）
        /// </summary>
        private void CreateContentArea()
        {
            // 内容容器
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(_panelObject.transform, false);

            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.05f, 0.1f);
            contentRect.anchorMax = new Vector2(0.95f, 0.9f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // 使用水平布局
            HorizontalLayoutGroup layout = contentObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // X轴区域
            CreateAxisControl(contentObj, "X轴旋转", true);

            // Y轴区域
            CreateAxisControl(contentObj, "Y轴旋转", false);

            // 重置按钮
            CreateResetButton();
        }

        /// <summary>
        /// 创建单个轴的控制区域
        /// </summary>
        private void CreateAxisControl(GameObject parent, string label, bool isXAxis)
        {
            // 轴容器
            GameObject axisObj = new GameObject(isXAxis ? "XAxis" : "YAxis");
            axisObj.transform.SetParent(parent.transform, false);

            RectTransform axisRect = axisObj.AddComponent<RectTransform>();
            axisRect.pivot = new Vector2(0.5f, 0.5f);

            // 使用垂直布局
            VerticalLayoutGroup layout = axisObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // 标签
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(axisObj.transform, false);

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(150, 30);

            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = label;
            labelText.color = Color.white;
            labelText.font = GetDefaultFont();
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleCenter;

            // 旋钮
            GameObject knobObj = new GameObject("Knob");
            knobObj.transform.SetParent(axisObj.transform, false);

            RectTransform knobRect = knobObj.AddComponent<RectTransform>();
            knobRect.sizeDelta = new Vector2(150, 150);

            Image knobImage = knobObj.AddComponent<Image>();
            if (knobTexture != null)
            {
                knobImage.sprite = Sprite.Create(knobTexture,
                    new Rect(0, 0, knobTexture.width, knobTexture.height),
                    new Vector2(0.5f, 0.5f));
            }
            else
            {
                knobImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            // 添加旋钮组件
            RotationKnob knob = knobObj.AddComponent<RotationKnob>();
            knob.Initialize(minRotation, maxRotation, rotationStep, isXAxis ? 0 : 1);

            if (isXAxis)
            {
                _xAxisKnob = knob;
                knob.OnAngleChanged += OnXAxisKnobChanged;
            }
            else
            {
                _yAxisKnob = knob;
                knob.OnAngleChanged += OnYAxisKnobChanged;
            }

            // 角度显示
            GameObject displayObj = new GameObject("AngleDisplay");
            displayObj.transform.SetParent(axisObj.transform, false);

            RectTransform displayRect = displayObj.AddComponent<RectTransform>();
            displayRect.sizeDelta = new Vector2(150, 30);

            Text displayText = displayObj.AddComponent<Text>();
            displayText.text = isXAxis ? "X: +0.0°" : "Y: +0.0°";
            displayText.color = Color.green;
            displayText.font = GetDefaultFont();
            displayText.fontSize = 16;
            displayText.alignment = TextAnchor.MiddleCenter;

            AngleDisplay display = displayObj.AddComponent<AngleDisplay>();
            display.Initialize(isXAxis ? "X: " : "Y: ");

            if (isXAxis)
            {
                _xAxisDisplay = display;
            }
            else
            {
                _yAxisDisplay = display;
            }
        }

        /// <summary>
        /// 创建重置按钮
        /// </summary>
        private void CreateResetButton()
        {
            GameObject resetBtnObj = new GameObject("ResetButton");
            resetBtnObj.transform.SetParent(_panelObject.transform, false);

            RectTransform resetRect = resetBtnObj.AddComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(0.5f, 0.02f);
            resetRect.anchorMax = new Vector2(0.5f, 0.02f);
            resetRect.pivot = new Vector2(0.5f, 0);
            resetRect.anchoredPosition = Vector2.zero;
            resetRect.sizeDelta = new Vector2(100, 30);

            Image resetBg = resetBtnObj.AddComponent<Image>();
            resetBg.color = new Color(0.3f, 0.5f, 0.3f, 1f);

            Button resetButton = resetBtnObj.AddComponent<Button>();
            resetButton.targetGraphic = resetBg;

            // 按钮文本
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(resetBtnObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.text = "重置";
            text.color = Color.white;
            text.font = GetDefaultFont();
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;

            resetButton.onClick.AddListener(OnResetButtonClicked);
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

        #region 旋钮回调

        /// <summary>
        /// X轴旋钮角度变化回调
        /// </summary>
        private void OnXAxisKnobChanged(float angle)
        {
            _currentRotation.x = angle;
            ApplyRotation();
            UpdateDisplays();
        }

        /// <summary>
        /// Y轴旋钮角度变化回调
        /// </summary>
        private void OnYAxisKnobChanged(float angle)
        {
            _currentRotation.y = angle;
            ApplyRotation();
            UpdateDisplays();
        }

        #endregion

        #region 键盘输入

        /// <summary>
        /// 处理键盘输入
        /// </summary>
        private void HandleKeyboardInput()
        {
            float deltaX = 0f;
            float deltaY = 0f;

            // W/S 控制 X轴
            if (Input.GetKey(KeyCode.W))
            {
                deltaX = rotateSpeed * Time.deltaTime;
            }
            else if (Input.GetKey(KeyCode.S))
            {
                deltaX = -rotateSpeed * Time.deltaTime;
            }

            // A/D 控制 Y轴
            if (Input.GetKey(KeyCode.A))
            {
                deltaY = -rotateSpeed * Time.deltaTime;
            }
            else if (Input.GetKey(KeyCode.D))
            {
                deltaY = rotateSpeed * Time.deltaTime;
            }

            // 应用增量旋转
            if (deltaX != 0f || deltaY != 0f)
            {
                _currentRotation.x = Mathf.Clamp(_currentRotation.x + deltaX, minRotation, maxRotation);
                _currentRotation.y = Mathf.Clamp(_currentRotation.y + deltaY, minRotation, maxRotation);

                ApplyRotation();
                UpdateKnobs();
                UpdateDisplays();
            }
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置按钮点击回调
        /// </summary>
        private void OnResetButtonClicked()
        {
            _currentRotation = Vector2.zero;
            ApplyRotation();
            UpdateKnobs();
            UpdateDisplays();
            Debug.Log("[CrystalRotationPanel] 旋转已重置");
        }

        #endregion

        #region 同步方法

        /// <summary>
        /// 同步当前旋转状态（从 Controller 获取）
        /// </summary>
        private void SyncCurrentRotation()
        {
            if (CrystalRuntime.IsInitialized && CrystalRuntime.Controller != null)
            {
                _currentRotation = CrystalRuntime.Controller.GetRotation();
                UpdateKnobs();
                UpdateDisplays();
            }
        }

        /// <summary>
        /// 应用旋转到晶体
        /// </summary>
        private void ApplyRotation()
        {
            if (CrystalRuntime.IsInitialized && CrystalRuntime.Controller != null)
            {
                CrystalRuntime.Controller.SetRotation(_currentRotation);
            }
        }

        /// <summary>
        /// 更新旋钮显示
        /// </summary>
        private void UpdateKnobs()
        {
            if (_xAxisKnob != null)
            {
                _xAxisKnob.SetAngle(_currentRotation.x);
            }
            if (_yAxisKnob != null)
            {
                _yAxisKnob.SetAngle(_currentRotation.y);
            }
        }

        /// <summary>
        /// 更新角度显示
        /// </summary>
        private void UpdateDisplays()
        {
            if (_xAxisDisplay != null)
            {
                _xAxisDisplay.SetAngle(_currentRotation.x);
            }
            if (_yAxisDisplay != null)
            {
                _yAxisDisplay.SetAngle(_currentRotation.y);
            }
        }

        #endregion

        #region 编辑器调试

#if UNITY_EDITOR
        [ContextMenu("显示面板")]
        private void ContextMenuShowPanel()
        {
            ShowPanel();
        }

        [ContextMenu("隐藏面板")]
        private void ContextMenuHidePanel()
        {
            HidePanel();
        }

        [ContextMenu("重置旋转")]
        private void ContextMenuResetRotation()
        {
            OnResetButtonClicked();
        }

        [ContextMenu("打印状态")]
        private void ContextMenuPrintStatus()
        {
            Debug.Log($"[CrystalRotationPanel] 状态报告:\n" +
                      $"  - 面板可见: {_isPanelVisible}\n" +
                      $"  - 当前旋转: X={_currentRotation.x:F1}°, Y={_currentRotation.y:F1}°\n" +
                      $"  - CrystalRuntime.IsInitialized: {CrystalRuntime.IsInitialized}");
        }
#endif

        #endregion
    }
}
