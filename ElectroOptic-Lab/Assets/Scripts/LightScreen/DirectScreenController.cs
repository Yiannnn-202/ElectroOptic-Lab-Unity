using UnityEngine;

// ZYX - 光屏控制器 (精准红点追踪)
public class DirectScreenController : MonoBehaviour, IOpticalReceiver
{
    private const float IntensityRedrawThreshold = 0.005f;
    private const float ExtinctionRedrawThreshold = 0.001f;

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

    [Header("显示调节")]
    [Tooltip("红点亮度倍率，1=物理原始亮度，>1 更亮，<1 更暗")]
    [Range(0.5f, 3f)]
    public float dotBrightness = 1f;

    // --- 内部变量 ---
    private RenderTexture _renderTexture;
    private Material _dotMaterial;
    private Renderer objRenderer;

    private float currentIntensity = 0f;
    private bool receivedLightThisFrame = false;
    private float lastOpticalSignalTime = float.NegativeInfinity;

    private float lastDrawnIntensity = -1f;

    // 用于记录红点在画布上的目标坐标和上次绘制坐标
    private float targetCenterX = 256f;
    private float targetCenterY = 256f;
    private float lastDrawnX = -1f;
    private float lastDrawnY = -1f;

    /// <summary>
    /// 公开纹理供 UnifiedScreenPanel 读取
    /// </summary>
    public Texture SharedTexture => _renderTexture;
    public float CurrentIntensity => currentIntensity;
    public Vector2 CurrentSpotPosition => new Vector2(targetCenterX, targetCenterY);
    public bool HasRecentOpticalSignal => Time.unscaledTime - lastOpticalSignalTime <= 0.15f;

    void Start()
    {
        if (screenCube == null) screenCube = gameObject;
        objRenderer = screenCube.GetComponent<Renderer>();

        if (objRenderer == null)
        {
            Debug.LogError("[DirectScreenController] 光屏物体上缺少 MeshRenderer 组件");
            return;
        }

        _renderTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        _renderTexture.Create();

        Shader shader = Shader.Find("ElectroOptics/DotTracking");
        if (shader != null)
        {
            _dotMaterial = new Material(shader);
        }
        else
        {
            Debug.LogError("[DirectScreenController] Shader 'ElectroOptics/DotTracking' not found, falling back to default.");
            _dotMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        DrawPattern(0f, 256f, 256f);
    }

    void Update()
    {
        if (!receivedLightThisFrame)
        {
            currentIntensity = Mathf.Lerp(currentIntensity, 0f, Time.deltaTime * 10f);
        }

        bool extinctionChanged = currentIntensity <= ExtinctionRedrawThreshold && lastDrawnIntensity > ExtinctionRedrawThreshold;

        // 亮度变化、位置变化或进入消光状态时重画
        if (Mathf.Abs(currentIntensity - lastDrawnIntensity) > IntensityRedrawThreshold ||
            extinctionChanged ||
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
        lastOpticalSignalTime = Time.unscaledTime;
        Debug.Log($"[DirectScreen] ReceiveLight I={lightIn.intensity:F6} S1={lightIn.stokesQ:F6} S2={lightIn.stokesU:F6} S3={lightIn.stokesV:F6}");

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
        float u = centerX / 512f;
        float v = centerY / 512f;

        _dotMaterial.SetVector("_HitUV", new Vector4(u, v, 0f, 0f));
        _dotMaterial.SetFloat("_DotIntensity", brightness);
        _dotMaterial.SetFloat("_DotBrightness", dotBrightness);

        Graphics.Blit(null, _renderTexture, _dotMaterial);
    }

    private void OnDestroy()
    {
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        if (_dotMaterial != null)
        {
            Destroy(_dotMaterial);
        }
    }
}
