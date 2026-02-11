using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 1. 继承 IOpticalReceiver 接口，这样激光器才能找到它
public class DirectScreenController : MonoBehaviour, IOpticalReceiver
{
    [Header("配置")]
    public GameObject screenCube;
    public Vector2 windowSize = new Vector2(600, 600);
    [Tooltip("双击判定时间")]
    public float doubleClickInterval = 0.3f;

    // --- 内部变量 ---
    private Texture2D sharedTexture;    // 核心数据源：3D和2D共用这张图
    private GameObject displayWindow;
    private RawImage uiDisplayImage;
    private Renderer objRenderer;

    // 交互用的计时器
    private float lastClickTime = 0f;

    // 光学物理变量
    private float currentIntensity = 0f;      // 当前亮度
    private bool receivedLightThisFrame = false; // 这一帧有没有光打过来？
    private Color[] colorBuffer; // 缓存颜色数组，防止每帧new产生垃圾

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

        // 4. 先画一次（初始化为白屏）
        DrawPattern(0f);
    }

    void Update()
    {
        // --- 逻辑 A: 光强衰减 (模拟光被挡住时的熄灭效果) ---
        if (!receivedLightThisFrame)
        {
            // 如果这一帧没收到光，亮度快速衰减到 0
            currentIntensity = Mathf.Lerp(currentIntensity, 0f, Time.deltaTime * 10f);
        }

        // --- 逻辑 B: 绘图触发器 ---
        // 为了节省性能，只有在“有亮度”或者“窗口打开”的时候才计算像素
        // 0.005f 是一个阈值，太暗就不算了
        if (currentIntensity > 0.005f || (displayWindow != null && displayWindow.activeSelf))
        {
            DrawPattern(currentIntensity);
        }
        else if (currentIntensity <= 0.005f && receivedLightThisFrame == false)
        {
            // 如果彻底没光了，且没新光进来，就不再 Update 画图了，省电
            // 确保最后画一次全白
            if (colorBuffer[0] != Color.white) DrawPattern(0f);
        }

        // 重置标志位，等待下一帧的 ReceiveLight
        receivedLightThisFrame = false;
    }

    // ==========================================
    // 💡 接口实现：当激光打中屏幕时自动调用
    // ==========================================
    public void ReceiveLight(LightData lightIn, Vector3 hitPoint, Vector3 dir)
    {
        // 1. 记录光强
        currentIntensity = lightIn.intensity;
        // 2. 标记这一帧收到了光
        receivedLightThisFrame = true;
    }

    // ==========================================
    // 🎨 物理绘图核心 (白屏红光斑版)
    // ==========================================
    void DrawPattern(float brightness)
    {
        float centerX = 256;
        float centerY = 256;

        // 尺寸缩小：模拟约1cm的激光点
        float radius = 25f;
        float radiusSq = radius * radius;

        // 如果亮度极低，直接全白屏
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
                    // 边缘柔化
                    float normalizedDistSq = distSq / radiusSq;
                    float softFactor = 1.0f - normalizedDistSq;

                    // 最终该点的亮度贡献值
                    float finalPixelIntensity = brightness * softFactor;

                    // 红色光斑在白色背景上
                    colorBuffer[i] = Color.Lerp(Color.white, Color.red, finalPixelIntensity);
                }
                else
                {
                    // 圆圈外面是白屏
                    colorBuffer[i] = Color.white;
                }
            }
        }

        // 应用像素到纹理
        sharedTexture.SetPixels(colorBuffer);
        sharedTexture.Apply();
    }

    // ==========================================
    // 🖱️ 交互：双击检测
    // ==========================================
    private void OnMouseDown()
    {
        // 防止点穿 UI
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

    // ==========================================
    // 🪟 UI 窗口管理 (代码生成 UI)
    // ==========================================
    public void OpenDisplayWindow()
    {
        if (displayWindow == null) CreateDisplayWindow();
        displayWindow.SetActive(true);
        displayWindow.transform.SetAsLastSibling(); // 置顶
    }

    private void CreateDisplayWindow()
    {
        // 🚨🚨🚨 【核心修改点】 开始 🚨🚨🚨
        // 不再随便找 Canvas，而是找名为 "WindowsCanvas" 的专用画布
        // 这样就不会被你放按钮的那个 Canvas 干扰了
        GameObject canvasObj = GameObject.Find("WindowsCanvas");
        Canvas canvas;

        if (canvasObj == null)
        {
            // 如果没找到，就新建一个，并强制设置正确的缩放模式
            canvasObj = new GameObject("WindowsCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // 确保是屏幕自适应
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas = canvasObj.GetComponent<Canvas>();
        }
        // 🚨🚨🚨 【核心修改点】 结束 🚨🚨🚨

        // 2. 创建窗口背景
        displayWindow = new GameObject("DataWindow_" + gameObject.name);
        displayWindow.transform.SetParent(canvas.transform, false);

        RectTransform rect = displayWindow.AddComponent<RectTransform>();
        rect.sizeDelta = windowSize;

        Image bg = displayWindow.AddComponent<Image>();
        bg.color = new Color(0.9f, 0.9f, 0.9f, 1f); // 浅灰背景

        // 3. 创建显示图片 RawImage
        GameObject imgObj = new GameObject("PatternView");
        imgObj.transform.SetParent(displayWindow.transform, false);

        RectTransform imgRect = imgObj.AddComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0.05f, 0.05f);
        imgRect.anchorMax = new Vector2(0.95f, 0.9f); // 留出上面放关闭按钮
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;

        uiDisplayImage = imgObj.AddComponent<RawImage>();
        uiDisplayImage.texture = sharedTexture;

        // 4. 关闭按钮
        CreateCloseBtn();

        // 5. 拖拽脚本
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
        img.color = new Color(0.8f, 0.3f, 0.3f, 1f); // 红色按钮

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => displayWindow.SetActive(false));
    }
}

// ==========================================
// ✋ 拖拽辅助脚本
// ==========================================
public class SimpleDrag : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData eventData)
    {
        transform.position += (Vector3)eventData.delta;
    }
}
