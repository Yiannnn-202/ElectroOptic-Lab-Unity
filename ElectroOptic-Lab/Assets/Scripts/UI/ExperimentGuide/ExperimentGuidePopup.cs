using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ElectroOptics.UI.ExperimentGuide
{
    /// <summary>
    /// Runtime-built experiment guide popup for Scene2.
    /// Displays pre-rendered PNG guide pages one page at a time for maximum readability.
    /// </summary>
    public class ExperimentGuidePopup : MonoBehaviour
    {
        private const string CanvasName = "WindowsCanvas";
        private const string PopupRootName = "ExperimentGuidePopupRoot";
        private const string SimheiResourcePath = "Fonts/SIMHEI SDF";

        [Header("Content")]
        [SerializeField] private string guideTitle = "实验操作指引";
        [SerializeField] private Sprite[] pages;
        [SerializeField] private bool resetToFirstPageOnShow = true;

        [Header("Panel Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(1500f, 920f);
        [SerializeField] private float titleBarHeight = 70f;
        [SerializeField] private float bottomBarHeight = 78f;
        [SerializeField] private Vector2 pageAreaPadding = new Vector2(26f, 20f);
        [SerializeField] private bool fitPageWidth = true;
        [SerializeField] [Range(0.5f, 2.5f)] private float pageZoom = 1f;

        [Header("Interaction")]
        [SerializeField] private bool closeOnOverlayClick = true;
        [SerializeField] private bool closeOnEscape = true;
        [SerializeField] private float fadeDuration = 0.18f;

        [Header("Colors")]
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color panelColor = new Color(0.96f, 0.93f, 0.86f, 0.98f);
        [SerializeField] private Color titleBarColor = new Color(0.18f, 0.21f, 0.26f, 1f);
        [SerializeField] private Color pageBackColor = new Color(0.91f, 0.86f, 0.76f, 1f);
        [SerializeField] private Color pageCardColor = new Color(1f, 0.985f, 0.94f, 1f);
        [SerializeField] private Color accentColor = new Color(0.24f, 0.49f, 0.68f, 1f);
        [SerializeField] private Color inactivePageButtonColor = new Color(0.48f, 0.54f, 0.60f, 1f);

        private Canvas _canvas;
        private GameObject _rootObject;
        private CanvasGroup _rootGroup;
        private RectTransform _panelRect;
        private RectTransform _pageCardRect;
        private ScrollRect _pageScrollRect;
        private RectTransform _pageImageRect;
        private Image _pageImage;
        private TextMeshProUGUI _pageIndicator;
        private TextMeshProUGUI _emptyText;
        private TMP_FontAsset _font;
        private Coroutine _fadeCoroutine;
        private readonly List<Button> _pageButtons = new List<Button>();
        private int _currentPageIndex;
        private bool _built;

        public bool IsOpen => _rootObject != null && _rootObject.activeSelf;
        public int PageCount => pages != null ? pages.Length : 0;

        private void OnDestroy()
        {
            if (_rootObject == null)
            {
                return;
            }

            if (Application.isPlaying)
                Destroy(_rootObject);
            else
                DestroyImmediate(_rootObject);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                PreviousPage();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                NextPage();
            }
        }

        public void SetTitle(string title)
        {
            guideTitle = string.IsNullOrWhiteSpace(title) ? "实验操作指引" : title;
            if (_built)
            {
                Rebuild();
            }
        }

        public void SetPages(Sprite[] guidePages)
        {
            pages = guidePages;
            _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, Mathf.Max(0, PageCount - 1));
            if (_built)
            {
                Rebuild();
            }
        }

        public void Show()
        {
            EnsureBuilt();
            if (_rootObject == null)
            {
                return;
            }

            _rootObject.SetActive(true);
            _rootObject.transform.SetAsLastSibling();

            if (resetToFirstPageOnShow)
            {
                SetPage(0);
            }
            else
            {
                RefreshPage();
            }

            FadeTo(1f, true);
        }

        public void Hide()
        {
            if (_rootObject == null || !_rootObject.activeSelf)
            {
                return;
            }

            FadeTo(0f, false);
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public void NextPage()
        {
            SetPage(_currentPageIndex + 1);
        }

        public void PreviousPage()
        {
            SetPage(_currentPageIndex - 1);
        }

        public void BackToTop()
        {
            SetPage(0);
        }

        public void SetPage(int pageIndex)
        {
            if (PageCount <= 0)
            {
                _currentPageIndex = 0;
            }
            else
            {
                _currentPageIndex = Mathf.Clamp(pageIndex, 0, PageCount - 1);
            }

            RefreshPage();
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Guide Popup")]
        private void EditorRebuild()
        {
            Rebuild();
        }
#endif

        private void EnsureBuilt()
        {
            if (_built && _rootObject != null)
            {
                return;
            }

            _canvas = GetOrCreateWindowsCanvas();
            EnsureEventSystem();
            Build();
        }

        private void Rebuild()
        {
            if (_rootObject != null)
            {
                if (Application.isPlaying)
                    Destroy(_rootObject);
                else
                    DestroyImmediate(_rootObject);
            }

            _rootObject = null;
            _rootGroup = null;
            _panelRect = null;
            _pageCardRect = null;
            _pageScrollRect = null;
            _pageImageRect = null;
            _pageImage = null;
            _pageIndicator = null;
            _emptyText = null;
            _pageButtons.Clear();
            _built = false;

            if (isActiveAndEnabled)
            {
                EnsureBuilt();
            }
        }

        private void Build()
        {
            _font = ResolveFont();

            _rootObject = CreateRectObject(PopupRootName, _canvas.transform);
            RectTransform rootRt = _rootObject.GetComponent<RectTransform>();
            Stretch(rootRt);
            _rootGroup = _rootObject.AddComponent<CanvasGroup>();
            _rootGroup.alpha = 0f;

            CreateOverlay(rootRt);
            CreatePanel(rootRt);
            CreateTitleBar();
            CreatePageArea();
            CreateBottomBar();

            _rootObject.SetActive(false);
            _built = true;
            RefreshPage();
        }

        private void CreateOverlay(RectTransform root)
        {
            GameObject overlay = CreateRectObject("Overlay", root);
            RectTransform overlayRt = overlay.GetComponent<RectTransform>();
            Stretch(overlayRt);
            Image image = overlay.AddComponent<Image>();
            image.color = overlayColor;
            image.raycastTarget = true;

            if (closeOnOverlayClick)
            {
                Button button = overlay.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(Hide);
            }
        }

        private void CreatePanel(RectTransform root)
        {
            GameObject panel = CreateRectObject("GuidePanel", root);
            _panelRect = panel.GetComponent<RectTransform>();
            _panelRect.anchorMin = _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.sizeDelta = panelSize;
            _panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = panelColor;
            panelImage.raycastTarget = true;

            UnityEngine.UI.Outline outline = panel.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.22f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private void CreateTitleBar()
        {
            GameObject titleBar = CreateRectObject("TitleBar", _panelRect);
            RectTransform titleRt = titleBar.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, titleBarHeight);
            titleRt.anchoredPosition = Vector2.zero;
            titleBar.AddComponent<Image>().color = titleBarColor;

            TextMeshProUGUI titleText = CreateText("Title", titleRt, guideTitle, 30, Color.white, TextAlignmentOptions.Center);
            RectTransform titleTextRt = titleText.GetComponent<RectTransform>();
            titleTextRt.anchorMin = titleTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleTextRt.pivot = new Vector2(0.5f, 0.5f);
            titleTextRt.sizeDelta = new Vector2(520f, 46f);
            titleTextRt.anchoredPosition = Vector2.zero;

            TextMeshProUGUI subtitle = CreateText("Subtitle", titleRt, "左右方向键 / A D 可翻页", 16, new Color(1f, 1f, 1f, 0.68f), TextAlignmentOptions.Left);
            RectTransform subtitleRt = subtitle.GetComponent<RectTransform>();
            subtitleRt.anchorMin = subtitleRt.anchorMax = new Vector2(0f, 0.5f);
            subtitleRt.pivot = new Vector2(0f, 0.5f);
            subtitleRt.sizeDelta = new Vector2(300f, 32f);
            subtitleRt.anchoredPosition = new Vector2(28f, -6f);

            Button closeButton = CreateButton("CloseButton", titleRt, "X", new Vector2(46f, 42f), 22, accentColor);
            RectTransform closeRt = closeButton.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-18f, 0f);
            closeButton.onClick.AddListener(Hide);
        }

        private void CreatePageArea()
        {
            GameObject pageBack = CreateRectObject("PageBack", _panelRect);
            RectTransform backRt = pageBack.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 0f);
            backRt.anchorMax = new Vector2(1f, 1f);
            backRt.offsetMin = new Vector2(26f, bottomBarHeight + 18f);
            backRt.offsetMax = new Vector2(-26f, -titleBarHeight - 18f);
            pageBack.AddComponent<Image>().color = pageBackColor;

            GameObject pageCard = CreateRectObject("PageCard", backRt);
            _pageCardRect = pageCard.GetComponent<RectTransform>();
            _pageCardRect.anchorMin = Vector2.zero;
            _pageCardRect.anchorMax = Vector2.one;
            _pageCardRect.offsetMin = new Vector2(pageAreaPadding.x, pageAreaPadding.y);
            _pageCardRect.offsetMax = new Vector2(-pageAreaPadding.x, -pageAreaPadding.y);

            Image cardImage = pageCard.AddComponent<Image>();
            cardImage.color = pageCardColor;
            pageCard.AddComponent<RectMask2D>();

            UnityEngine.UI.Outline shadow = pageCard.AddComponent<UnityEngine.UI.Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.15f);
            shadow.effectDistance = new Vector2(2.5f, -2.5f);

            _pageScrollRect = pageCard.AddComponent<ScrollRect>();
            _pageScrollRect.horizontal = false;
            _pageScrollRect.vertical = true;
            _pageScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _pageScrollRect.scrollSensitivity = 42f;

            GameObject pageImageObj = CreateRectObject("PageImage", _pageCardRect);
            _pageImageRect = pageImageObj.GetComponent<RectTransform>();
            _pageImageRect.anchorMin = new Vector2(0.5f, 1f);
            _pageImageRect.anchorMax = new Vector2(0.5f, 1f);
            _pageImageRect.pivot = new Vector2(0.5f, 1f);
            _pageImageRect.anchoredPosition = Vector2.zero;
            _pageScrollRect.content = _pageImageRect;

            _pageImage = pageImageObj.AddComponent<Image>();
            _pageImage.color = Color.white;
            _pageImage.preserveAspect = true;
            _pageImage.raycastTarget = false;

            _emptyText = CreateText("EmptyHint", _pageCardRect, "暂无实验指引页面\n请在 Inspector 中配置 PNG 页面 Sprite", 24, new Color(0.27f, 0.25f, 0.22f, 0.85f), TextAlignmentOptions.Center);
            RectTransform emptyRt = _emptyText.GetComponent<RectTransform>();
            Stretch(emptyRt);
        }

        private void CreateBottomBar()
        {
            GameObject bottomBar = CreateRectObject("BottomBar", _panelRect);
            RectTransform bottomRt = bottomBar.GetComponent<RectTransform>();
            bottomRt.anchorMin = new Vector2(0f, 0f);
            bottomRt.anchorMax = new Vector2(1f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0f);
            bottomRt.sizeDelta = new Vector2(0f, bottomBarHeight);
            bottomRt.anchoredPosition = Vector2.zero;
            bottomBar.AddComponent<Image>().color = new Color(0.2f, 0.18f, 0.14f, 0.16f);

            HorizontalLayoutGroup layout = bottomBar.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 18f;
            layout.padding = new RectOffset(28, 28, 12, 12);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Button previous = CreateButton("PreviousPage", bottomRt, "上一页", new Vector2(116f, 42f), 19, accentColor);
            previous.onClick.AddListener(PreviousPage);

            GameObject pageGroup = CreateRectObject("PageButtons", bottomRt);
            RectTransform pageGroupRt = pageGroup.GetComponent<RectTransform>();
            pageGroupRt.sizeDelta = new Vector2(260f, 42f);
            LayoutElement pageGroupLayout = pageGroup.AddComponent<LayoutElement>();
            pageGroupLayout.preferredWidth = 260f;
            pageGroupLayout.preferredHeight = 42f;

            HorizontalLayoutGroup pageGroupHlg = pageGroup.AddComponent<HorizontalLayoutGroup>();
            pageGroupHlg.childAlignment = TextAnchor.MiddleCenter;
            pageGroupHlg.spacing = 8f;
            pageGroupHlg.childControlWidth = false;
            pageGroupHlg.childControlHeight = false;
            pageGroupHlg.childForceExpandWidth = false;
            pageGroupHlg.childForceExpandHeight = false;

            CreatePageButtons(pageGroupRt);

            _pageIndicator = CreateText("PageIndicator", bottomRt, "", 19, new Color(0.18f, 0.16f, 0.13f, 1f), TextAlignmentOptions.Center);
            RectTransform indicatorRt = _pageIndicator.GetComponent<RectTransform>();
            indicatorRt.sizeDelta = new Vector2(150f, 42f);
            LayoutElement indicatorLayout = _pageIndicator.gameObject.AddComponent<LayoutElement>();
            indicatorLayout.preferredWidth = 150f;
            indicatorLayout.preferredHeight = 42f;

            Button next = CreateButton("NextPage", bottomRt, "下一页", new Vector2(116f, 42f), 19, accentColor);
            next.onClick.AddListener(NextPage);
        }

        private void CreatePageButtons(Transform parent)
        {
            _pageButtons.Clear();
            int buttonCount = Mathf.Max(1, PageCount);
            for (int i = 0; i < buttonCount; i++)
            {
                int pageIndex = i;
                Button button = CreateButton($"PageButton_{i + 1}", parent, (i + 1).ToString(), new Vector2(42f, 42f), 18, inactivePageButtonColor);
                button.onClick.AddListener(() => SetPage(pageIndex));
                _pageButtons.Add(button);
            }
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 size, int fontSize, Color normalColor)
        {
            GameObject buttonObj = CreateRectObject(name, parent);
            RectTransform rt = buttonObj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            LayoutElement layout = buttonObj.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            Image image = buttonObj.AddComponent<Image>();
            image.color = normalColor;

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            TextMeshProUGUI text = CreateText("Label", rt, label, fontSize, Color.white, TextAlignmentOptions.Center);
            Stretch(text.GetComponent<RectTransform>());
            return button;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string text, int fontSize, Color color, TextAlignmentOptions alignment)
        {
            GameObject textObj = CreateRectObject(name, parent);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            if (_font != null)
            {
                tmp.font = _font;
            }

            return tmp;
        }

        private void RefreshPage()
        {
            bool hasPages = PageCount > 0;
            _currentPageIndex = hasPages
                ? Mathf.Clamp(_currentPageIndex, 0, PageCount - 1)
                : 0;

            if (_pageImage != null)
            {
                _pageImage.gameObject.SetActive(hasPages);
                _pageImage.sprite = hasPages ? pages[_currentPageIndex] : null;
                FitPageImageToCard();
                if (_pageScrollRect != null)
                {
                    _pageScrollRect.verticalNormalizedPosition = 1f;
                }
            }

            if (_emptyText != null)
            {
                _emptyText.gameObject.SetActive(!hasPages);
            }

            if (_pageIndicator != null)
            {
                _pageIndicator.text = hasPages
                    ? $"第 {_currentPageIndex + 1} / {PageCount} 页"
                    : "0 / 0";
            }

            RefreshPageButtons();
        }

        private void FitPageImageToCard()
        {
            if (_pageImage == null || _pageCardRect == null)
            {
                return;
            }

            RectTransform imageRt = _pageImageRect != null ? _pageImageRect : _pageImage.GetComponent<RectTransform>();
            Sprite sprite = _pageImage.sprite;
            Rect cardRect = _pageCardRect.rect;
            float maxWidth = Mathf.Max(1f, cardRect.width - 42f);
            float maxHeight = Mathf.Max(1f, cardRect.height - 42f);

            if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
            {
                imageRt.sizeDelta = new Vector2(maxWidth, maxHeight);
                return;
            }

            float imageAspect = sprite.rect.width / sprite.rect.height;
            float safeZoom = Mathf.Max(0.1f, pageZoom);
            Vector2 size;
            if (fitPageWidth)
            {
                float width = maxWidth * safeZoom;
                size = new Vector2(width, width / imageAspect);
            }
            else
            {
                float areaAspect = maxWidth / maxHeight;
                size = imageAspect > areaAspect
                    ? new Vector2(maxWidth, maxWidth / imageAspect)
                    : new Vector2(maxHeight * imageAspect, maxHeight);
                size *= safeZoom;
            }

            imageRt.sizeDelta = size;
            imageRt.anchoredPosition = Vector2.zero;
        }

        private void RefreshPageButtons()
        {
            for (int i = 0; i < _pageButtons.Count; i++)
            {
                Button button = _pageButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool active = i == _currentPageIndex && PageCount > 0;
                Color color = active ? accentColor : inactivePageButtonColor;
                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = color;
                }

                ColorBlock colors = button.colors;
                colors.normalColor = color;
                colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
                colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
                button.interactable = i < PageCount;
            }
        }

        private void FadeTo(float targetAlpha, bool keepActive)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, keepActive));
        }

        private IEnumerator FadeRoutine(float targetAlpha, bool keepActive)
        {
            float duration = Mathf.Max(0.001f, fadeDuration);
            float startAlpha = _rootGroup != null ? _rootGroup.alpha : targetAlpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (_rootGroup != null)
                {
                    _rootGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                    _rootGroup.blocksRaycasts = true;
                    _rootGroup.interactable = true;
                }
                yield return null;
            }

            if (_rootGroup != null)
            {
                _rootGroup.alpha = targetAlpha;
                _rootGroup.blocksRaycasts = keepActive;
                _rootGroup.interactable = keepActive;
            }

            if (!keepActive && _rootObject != null)
            {
                _rootObject.SetActive(false);
            }

            _fadeCoroutine = null;
        }

        private TMP_FontAsset ResolveFont()
        {
            TMP_FontAsset resourceFont = Resources.Load<TMP_FontAsset>(SimheiResourcePath);
            if (resourceFont != null)
            {
                return resourceFont;
            }

            TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null && texts[i].font.name.Contains("SIMHEI"))
                {
                    return texts[i].font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static Canvas GetOrCreateWindowsCanvas()
        {
            GameObject canvasObj = GameObject.Find(CanvasName);
            if (canvasObj == null)
            {
                canvasObj = new GameObject(CanvasName);
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
                return canvas;
            }

            Canvas existingCanvas = canvasObj.GetComponent<Canvas>();
            if (existingCanvas == null)
            {
                existingCanvas = canvasObj.AddComponent<Canvas>();
                existingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                existingCanvas.sortingOrder = 100;
            }

            if (canvasObj.GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            return existingCanvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
