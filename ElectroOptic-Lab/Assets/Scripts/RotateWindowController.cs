using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RotateWindowController : MonoBehaviour
{
    [Header("窗口配置")]
    public Vector2 defaultWindowSize = new Vector2(500, 500);
    public string texturePath = "UI/mine";

    [Header("窗口标题")]
    public string windowTitle = "刻度盘窗口";

    // 私有组件
    private RectTransform windowRect;
    private Image dialImage;
    private Button closeButton;
    private RectTransform titleBarRect;
    private Text titleText;

    // 拖动相关
    private bool isDragging = false;
    private Vector2 dragStartPosition;
    private Vector2 windowStartPosition;

    // 旋转座引用
    private RotateStandController rotateStand;

    /// <summary>
    /// 创建窗口
    /// </summary>
    public static RotateWindowController CreateRotateWindow(RotateStandController stand)
    {
        // 1. 创建/获取专用 Canvas
        Canvas canvas = GetOrCreateCanvas();

        // 2. 确保EventSystem
        EnsureEventSystem();

        // 3. 创建窗口对象（唯一名称）
        string windowName = $"DialWindow_{stand.GetRotateStandName()}_{System.DateTime.Now.Ticks}";
        GameObject windowObj = new GameObject(windowName);
        windowObj.transform.SetParent(canvas.transform, false);

        // 4. 添加控制器
        RotateWindowController controller = windowObj.AddComponent<RotateWindowController>();
        controller.rotateStand = stand;
        controller.windowTitle = $"刻度盘 - {stand.GetRotateStandName()}";
        controller.InitWindow();

        return controller;
    }

    private static Canvas GetOrCreateCanvas()
    {
        // 寻找专用的 WindowsCanvas，防止被按钮的 Canvas 干扰
        GameObject canvasObj = GameObject.Find("WindowsCanvas");
        Canvas canvas;

        if (canvasObj == null)
        {
            canvasObj = new GameObject("WindowsCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // 强制正确缩放
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

    private static void EnsureEventSystem()
    {
        // 🚨🚨🚨 【修复警告的地方】 🚨🚨🚨
        // 旧写法：Object.FindObjectOfType<EventSystem>()
        // 新写法：Object.FindFirstObjectByType<EventSystem>()
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }
    }

    private void InitWindow()
    {
        // ===== 1. 窗口根节点 =====
        windowRect = gameObject.AddComponent<RectTransform>();
        windowRect.sizeDelta = defaultWindowSize;
        // 恢复随机位置逻辑
        windowRect.anchoredPosition = new Vector2(Random.Range(-200, 200), Random.Range(-100, 100));
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);

        // 窗口背景
        Image windowBg = gameObject.AddComponent<Image>();
        windowBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        windowBg.raycastTarget = true;

        // ===== 2. 标题栏 =====
        CreateTitleBar();

        // ===== 3. 刻度盘图片 =====
        CreateDialImage();
    }

    private void CreateTitleBar()
    {
        GameObject titleBarObj = new GameObject("TitleBar");
        titleBarObj.transform.SetParent(transform, false);
        titleBarRect = titleBarObj.AddComponent<RectTransform>();

        titleBarRect.anchorMin = new Vector2(0, 1);
        titleBarRect.anchorMax = new Vector2(1, 1);
        titleBarRect.pivot = new Vector2(0.5f, 1);
        titleBarRect.anchoredPosition = new Vector2(0, 0);
        titleBarRect.sizeDelta = new Vector2(0, 40);

        Image titleBarBg = titleBarObj.AddComponent<Image>();
        titleBarBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        titleBarBg.raycastTarget = true;

        // 标题文本
        GameObject titleTextObj = new GameObject("TitleText");
        titleTextObj.transform.SetParent(titleBarObj.transform, false);

        RectTransform titleTextRect = titleTextObj.AddComponent<RectTransform>();
        titleTextRect.anchorMin = new Vector2(0, 0);
        titleTextRect.anchorMax = new Vector2(1, 1);
        titleTextRect.offsetMin = new Vector2(10, 5);
        titleTextRect.offsetMax = new Vector2(-40, -5);

        titleText = titleTextObj.AddComponent<Text>();
        titleText.text = windowTitle;
        titleText.color = Color.white;
        // 恢复使用 LegacyRuntime.ttf
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 16;
        titleText.alignment = TextAnchor.MiddleLeft;

        // 关闭按钮
        CreateCloseButton(titleBarObj);

        // 绑定拖动事件
        BindDragEvent(titleBarObj);
    }

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
        closeBg.color = new Color(1, 0.3f, 0.3f, 1);
        closeBg.raycastTarget = true;

        closeButton = closeBtnObj.AddComponent<Button>();
        closeButton.targetGraphic = closeBg;

        ColorBlock colors = closeButton.colors;
        colors.normalColor = new Color(1, 0.3f, 0.3f, 1);
        colors.highlightedColor = new Color(1, 0.5f, 0.5f, 1);
        colors.pressedColor = new Color(0.8f, 0.2f, 0.2f, 1);
        closeButton.colors = colors;

        GameObject closeTextObj = new GameObject("CloseText");
        closeTextObj.transform.SetParent(closeBtnObj.transform, false);

        RectTransform closeTextRect = closeTextObj.AddComponent<RectTransform>();
        closeTextRect.anchorMin = new Vector2(0, 0);
        closeTextRect.anchorMax = new Vector2(1, 1);
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;

        Text closeText = closeTextObj.AddComponent<Text>();
        closeText.text = "✕";
        closeText.color = Color.white;
        closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeText.fontSize = 18;
        closeText.alignment = TextAnchor.MiddleCenter;

        // 点击事件
        closeButton.onClick.AddListener(() =>
        {
            // 关闭窗口时解锁底座
            if (rotateStand != null)
            {
                RotateStandController.DeselectAll();
            }

            Debug.Log($"🪟 关闭窗口: {gameObject.name}");
            gameObject.SetActive(false);
        });
    }

    private void CreateDialImage()
    {
        GameObject dialObj = new GameObject("DialImage");
        dialObj.transform.SetParent(transform, false);
        dialObj.transform.SetSiblingIndex(0);

        RectTransform dialRect = dialObj.AddComponent<RectTransform>();
        dialRect.anchorMin = new Vector2(0.1f, 0.1f);
        dialRect.anchorMax = new Vector2(0.9f, 0.9f);
        dialRect.offsetMin = Vector2.zero;
        dialRect.offsetMax = Vector2.zero;
        dialRect.pivot = new Vector2(0.5f, 0.5f);

        dialImage = dialObj.AddComponent<Image>();

        // 恢复原来的加载逻辑
        Texture2D dialTex = Resources.Load<Texture2D>(texturePath);
        if (dialTex != null)
        {
            dialImage.sprite = Sprite.Create(dialTex,
                new Rect(0, 0, dialTex.width, dialTex.height),
                new Vector2(0.5f, 0.5f));
            dialImage.preserveAspect = true;
        }
        else
        {
            dialImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }
        dialImage.raycastTarget = false;
    }

    private void BindDragEvent(GameObject targetObj)
    {
        EventTrigger trigger = targetObj.AddComponent<EventTrigger>();

        EventTrigger.Entry pointerDown = new EventTrigger.Entry();
        pointerDown.eventID = EventTriggerType.PointerDown;
        pointerDown.callback.AddListener((data) =>
        {
            PointerEventData evtData = (PointerEventData)data;
            isDragging = true;
            dragStartPosition = evtData.position;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)windowRect.parent,
                windowRect.position,
                evtData.pressEventCamera,
                out windowStartPosition);

            transform.SetAsLastSibling();
        });
        trigger.triggers.Add(pointerDown);

        EventTrigger.Entry drag = new EventTrigger.Entry();
        drag.eventID = EventTriggerType.Drag;
        drag.callback.AddListener((data) =>
        {
            if (!isDragging) return;

            PointerEventData evtData = (PointerEventData)data;
            Vector2 mouseDelta = evtData.position - dragStartPosition;

            // 恢复原来的拖动计算逻辑
            windowRect.anchoredPosition = windowStartPosition + (mouseDelta / GetComponentInParent<Canvas>().scaleFactor);
        });
        trigger.triggers.Add(drag);

        EventTrigger.Entry pointerUp = new EventTrigger.Entry();
        pointerUp.eventID = EventTriggerType.PointerUp;
        pointerUp.callback.AddListener((data) =>
        {
            isDragging = false;
        });
        trigger.triggers.Add(pointerUp);
    }

    private void Update()
    {
        if (rotateStand != null && dialImage != null)
        {
            float angle = rotateStand.GetCurrentRotateAngle();
            dialImage.rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);

            if (titleText != null)
            {
                titleText.text = $"{windowTitle} ({angle:F1}°)";
            }
        }
    }

    public void ShowWindow()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }
}
