using UnityEngine;

// ZYX - 光屏控制器 (精准红点追踪)
public class DirectScreenController : MonoBehaviour, IOpticalReceiver
{
    [Header("状态联动 (必填)")]
    [Tooltip("把晶体的 OpticalComponent 拖到这里，用于检测晶体吸附状态")]
    public OpticalComponent crystalOpticalComponent;

    [Header("基础配置")]
    public GameObject screenCube;

    [Header("坐标映射配置")]
    [Tooltip("红点移动范围比例（对应模型控制台输出的 LocalPos 大小）")]
    public float screenLocalWidth = 0.05f;
    public float screenLocalHeight = 0.05f;

    [Header("偏移与反转微调")]
    [Tooltip("微调红点的左右位置")]
    public float offsetX = 0f;
    [Tooltip("微调红点的上下位置")]
    public float offsetY = 0f;

    [Tooltip("反转 X 轴")]
    public bool invertX = false;
    [Tooltip("反转 Y 轴")]
    public bool invertY = false;

    // --- 内部变量 ---
    private Texture2D sharedTexture;
    private Renderer objRenderer;

    private float currentIntensity = 0f;
    private bool receivedLightThisFrame = false;
    private Color[] colorBuffer;

    private float lastDrawnIntensity = -1f;

    // 用于记录红点在画布上的目标坐标和上次绘制坐标
    private float targetCenterX = 256f;
    private float targetCenterY = 256f;
    private float lastDrawnX = -1f;
    private float lastDrawnY = -1f;

    /// <summary>
    /// 公开纹理供 UnifiedScreenPanel 读取
    /// </summary>
    public Texture2D SharedTexture => sharedTexture;

    void Start()
    {
        if (screenCube == null) screenCube = gameObject;
        objRenderer = screenCube.GetComponent<Renderer>();

        if (objRenderer == null)
        {
            Debug.LogError("[DirectScreenController] 光屏物体上缺少 MeshRenderer 组件");
            return;
        }

        sharedTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        colorBuffer = new Color[512 * 512];

        DrawPattern(0f, 256f, 256f);
    }

    void Update()
    {
        if (!receivedLightThisFrame)
        {
            currentIntensity = Mathf.Lerp(currentIntensity, 0f, Time.deltaTime * 10f);
        }

        // 降低敏感度，位置变化超过 2 个像素或亮度有变化才重画
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

        // 获取激光打在光屏上的局部坐标
        Vector3 localPos = screenCube.transform.InverseTransformPoint(hitPoint);

        // 根据反馈：左右是绿色(Y轴)，上下是蓝色(Z轴)
        float rawX = localPos.y;
        float rawY = localPos.z;

        // 计算 0~1 的比例
        float normalizedX = ((rawX + offsetX) / screenLocalWidth) + 0.5f;
        float normalizedY = ((rawY + offsetY) / screenLocalHeight) + 0.5f;

        // 处理可能的反向问题
        if (invertX) normalizedX = 1f - normalizedX;
        if (invertY) normalizedY = 1f - normalizedY;

        // 映射到 512x512 画布
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

    private void OnDestroy()
    {
        if (sharedTexture != null)
        {
            Destroy(sharedTexture);
        }
    }
}
