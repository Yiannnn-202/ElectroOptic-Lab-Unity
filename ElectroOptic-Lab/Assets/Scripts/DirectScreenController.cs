using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// ZYX - 光屏控制器 (带精准红点追踪与双击窗口)
public class DirectScreenController : MonoBehaviour, IOpticalReceiver
{
    [Header("基础配置")]
    public GameObject screenCube;
    public Vector2 windowSize = new Vector2(600, 600);
    [Tooltip("双击判定时间")]
    public float doubleClickInterval = 0.3f;

    [Header("🎯 坐标映射配置 (重要!)")]
    [Tooltip("红点移动范围比例（对应你模型控制台输出的 LocalPos 大小，大概是 0.05）")]
    public float screenLocalWidth = 0.05f;
    public float screenLocalHeight = 0.05f;

    [Tooltip("【核心修复】如果发现左右移动光屏，红点却上下跑，请勾选这个对调XY轴")]
    public bool swapXY = false;

    [Tooltip("如果发现激光往左，红点往右，就勾选这个反转X轴")]
    public bool invertX = false;
    [Tooltip("如果发现激光往上，红点往下，就勾选这个反转Y轴")]
    public bool invertY = false;

    // --- 内部变量 ---
    private Texture2D sharedTexture;
    private GameObject displayWindow;
    private RawImage uiDisplayImage;
    private Renderer objRenderer;

    private float lastClickTime = 0f;

    private float currentIntensity = 0f;
    private bool receivedLightThisFrame = false;
    private Color[] colorBuffer;

    private float lastDrawnIntensity = -1f;

    // 用于记录红点在画布上的目标坐标和上次绘制坐标
    private float targetCenterX = 256f;
    private float targetCenterY = 256f;
    private float lastDrawnX = -1f;
    private float lastDrawnY = -1f;

    void Start()
    {
        if (screenCube == null) screenCube = gameObject;
        objRenderer = screenCube.GetComponent<Renderer>();

        if (objRenderer == null)
        {
            Debug.LogError("❌ 光屏物体上缺少 MeshRenderer 组件！");
            return;
        }

        sharedTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        objRenderer.material.mainTexture = sharedTexture;

        colorBuffer = new Color[512 * 512];

        DrawPattern(0f, 256f, 256f);
    }

    void Update()
    {
        if (!receivedLightThisFrame)
        {
            currentIntensity = Mathf.Lerp(currentIntensity, 0f, Time.deltaTime * 10f);
        }

        // 降低敏感度，位置变化超过 2 个像素或亮度有变化才重画，保证流畅不卡顿
        if (Mathf.Abs(currentIntensity - lastDrawnIntensity) > 0.05f ||
            Mathf.Abs(targetCenterX - lastDrawnX) > 2f ||
            Mathf.Abs(targetCenterY - lastDrawnY) > 2f)
        {
            DrawPattern(currentIntensity, targetCenterX, targetCenterY);
            lastDrawnIntensity = currentIntensity;
            lastDrawnX = targetCenterX;
            lastDrawnY = targetCenterY;
        }

        receivedLightThisFrame = false;
    }

    public void ReceiveLight(LightData lightIn, Vector3 hitPoint, Vector3 dir)
    {
        currentIntensity = lightIn.intensity;
        receivedLightThisFrame = true;

        // 1. 获取激光打在光屏上的局部坐标
        Vector3 localPos = screenCube.transform.InverseTransformPoint(hitPoint);

        // 2. 根据面板开关，决定谁是 X 轴，谁是 Y 轴 (修复上下左右错乱)
        float rawX = swapXY ? localPos.y : localPos.x;
        float rawY = swapXY ? localPos.x : localPos.y;

        // 3. 计算 0~1 的比例 (将微小的局部坐标放大)
        float normalizedX = (rawX / screenLocalWidth) + 0.5f;
        float normalizedY = (rawY / screenLocalHeight) + 0.5f;

        // 4. 处理可能的反向问题
        if (invertX) normalizedX = 1f - normalizedX;
        if (invertY) normalizedY = 1f - normalizedY;

        // 5. 映射到 512x512 画布，并限制在 0-512 范围内防止报错
        targetCenterX = Mathf.Clamp(normalizedX * 512f, 0f, 512f);
        targetCenterY = Mathf.Clamp(normalizedY * 512f, 0f, 512f);
    }

    void DrawPattern(float brightness, float centerX = 256f, float centerY = 256f)
    {
        float radius = 25f;
        float radiusSq = radius * radius;

        if (brightness < 0.005f)
        {
            System.Array.Fill(colorBuffer, Color.white);
        }
        else
        {
            for (int i = 0; i < colorBuffer.Length; i++)
            {
                int x = i % 512;
                int y = i / 512;

                float dx = x - centerX;
                float dy = y - centerY;
                float distSq = dx * dx + dy * dy;

                if (distSq < radiusSq)
                {
                    float normalizedDistSq = distSq / radiusSq;
                    float softFactor = 1.0f - normalizedDistSq;
                    float finalPixelIntensity = brightness * softFactor;
                    colorBuffer[i] = Color.Lerp(Color.white, Color.red, finalPixelIntensity);
                }
                else
                {
                    colorBuffer[i] = Color.white;
                }
            }
        }

        sharedTexture.SetPixels(colorBuffer);
        sharedTexture.Apply();
    }

    // --- 以下是双击交互与窗口 UI 逻辑 ---
    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        float currentTime = Time.time;
        if (currentTime - lastClickTime <= doubleClickInterval)
        {

            OpenDisplayWindow();
            lastClickTime = 0f;
        }
        else
        {
            lastClickTime = currentTime;
        }
    }

    public void OpenDisplayWindow()
    {
        if (displayWindow == null) CreateDisplayWindow();
        displayWindow.SetActive(true);
        displayWindow.transform.SetAsLastSibling();
    }

    private void CreateDisplayWindow()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }

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

        displayWindow = new GameObject("DataWindow_" + gameObject.name);
        displayWindow.transform.SetParent(canvas.transform, false);

        RectTransform rect = displayWindow.AddComponent<RectTransform>();
        rect.sizeDelta = windowSize;

        Image bg = displayWindow.AddComponent<Image>();
        bg.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        GameObject imgObj = new GameObject("PatternView");
        imgObj.transform.SetParent(displayWindow.transform, false);

        RectTransform imgRect = imgObj.AddComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0.05f, 0.05f);
        imgRect.anchorMax = new Vector2(0.95f, 0.9f);
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;

        uiDisplayImage = imgObj.AddComponent<RawImage>();
        uiDisplayImage.texture = sharedTexture;

        CreateCloseBtn();
        displayWindow.AddComponent<SimpleDrag>();
    }

    private void CreateCloseBtn()
    {
        GameObject btnObj = new GameObject("CloseBtn");
        btnObj.transform.SetParent(displayWindow.transform, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.92f, 0.92f);
        rect.anchorMax = new Vector2(0.98f, 0.98f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.8f, 0.3f, 0.3f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => displayWindow.SetActive(false));

        GameObject txtObj = new GameObject("BtnText");
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        Text txt = txtObj.AddComponent<Text>();
        txt.text = "X";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        txt.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        txt.fontSize = 24;
    }
}

// 拖拽辅助脚本
public class SimpleDrag : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvas.transform,
                eventData.position,
                canvas.worldCamera,
                out pos);
            transform.position += (Vector3)eventData.delta;
        }
    }
}
