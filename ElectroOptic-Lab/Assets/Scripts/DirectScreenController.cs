using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
//ZYX
/// <summary>
/// 光屏控制（双击）+ 窗口设置
/// </summary>


// 1. 继承 IOpticalReceiver 接口
public class DirectScreenController : MonoBehaviour, IOpticalReceiver
{
    [Header("配置")]
    public GameObject screenCube;
    public Vector2 windowSize = new Vector2(600, 600);
    [Tooltip("双击判定时间")]
    public float doubleClickInterval = 0.3f;

    // --- 内部变量 ---
    private Texture2D sharedTexture;    // 核心数据源
    private GameObject displayWindow;
    private RawImage uiDisplayImage;
    private Renderer objRenderer;

    // 交互用的计时器
    private float lastClickTime = 0f;

    // 光学物理变量
    private float currentIntensity = 0f;      // 当前亮度
    private bool receivedLightThisFrame = false; // 这一帧有没有光打过来？
    private Color[] colorBuffer;

    // 🔥 修复死机：记录上一次画的亮度，如果没变就不画
    private float lastDrawnIntensity = -1f;

    void Start()
    {
        // 1. 初始化物体引用
        if (screenCube == null) screenCube = gameObject;
        objRenderer = screenCube.GetComponent<Renderer>();

        if (objRenderer == null)
        {
            Debug.LogError("❌ 光屏物体上缺少 MeshRenderer 组件！");
            return;
        }

        // 2. 初始化纹理 (512x512)
        sharedTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        objRenderer.material.mainTexture = sharedTexture;

        // 3. 初始化缓存数组
        colorBuffer = new Color[512 * 512];

        // 4. 先画一次
        DrawPattern(0f);
    }

    void Update()
    {
        // --- 逻辑 A: 光强衰减 ---
        if (!receivedLightThisFrame)
        {
            currentIntensity = Mathf.Lerp(currentIntensity, 0f, Time.deltaTime * 10f);
        }

        // --- 逻辑 B: 绘图触发器 (修复死机版) ---
        // 只有当“亮度发生实质变化”时，才进行重绘！
        // 之前只要窗口打开就狂画，导致死机。现在只在数据变了才画。
        if (Mathf.Abs(currentIntensity - lastDrawnIntensity) > 0.001f)
        {
            DrawPattern(currentIntensity);
            lastDrawnIntensity = currentIntensity; // 记住这次画的亮度
        }

        // 重置标志位
        receivedLightThisFrame = false;
    }

    // 接口实现
    public void ReceiveLight(LightData lightIn, Vector3 hitPoint, Vector3 dir)
    {
        currentIntensity = lightIn.intensity;
        receivedLightThisFrame = true;
    }

    // 物理绘图核心
    void DrawPattern(float brightness)
    {
        float centerX = 256;
        float centerY = 256;
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

    // 交互：双击检测
    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        float currentTime = Time.time;
        if (currentTime - lastClickTime <= doubleClickInterval)
        {
            Debug.Log("🎯 光屏被双击，打开数据窗口");
            OpenDisplayWindow();
            lastClickTime = 0f;
        }
        else
        {
            lastClickTime = currentTime;
        }
    }

    // UI 窗口管理
    public void OpenDisplayWindow()
    {
        if (displayWindow == null) CreateDisplayWindow();
        displayWindow.SetActive(true);
        displayWindow.transform.SetAsLastSibling();
    }

    private void CreateDisplayWindow()
    {
        GameObject canvasObj = GameObject.Find("WindowsCanvas");
        Canvas canvas;

        if (canvasObj == null)
        {
            canvasObj = new GameObject("WindowsCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 确保显示在最前面
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

        // --- 修复叉叉显示 ---
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

        // 🔥 修复重点：强制从系统获取 Arial 字体，防止 Null
        txt.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
        // 如果系统里连 Arial 都没有（极少见），再试一次 Legacy
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        txt.fontSize = 24;
    }
}

// 拖拽辅助脚本
public class SimpleDrag : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData eventData)
    {
        // 简单的拖拽逻辑，如果你发现 Canvas Scale 很大导致拖得太快，可以除以 scaleFactor
        // transform.position += (Vector3)eventData.delta; 

        // 优化版拖拽：适应 Canvas 缩放，手感更跟手
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvas.transform,
                eventData.position,
                canvas.worldCamera,
                out pos);
            // 这里为了简单，保持你原来的逻辑，如果不跟手再改
            transform.position += (Vector3)eventData.delta;
        }
    }
}
