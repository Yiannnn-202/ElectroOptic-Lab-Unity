using UnityEngine;
using ElectroOptics; // 引入你们的命名空间

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(CrystalPhysicalCore))]
public class CrystalInteract : MonoBehaviour
{
    public static CrystalInteract Instance { get; private set; }

    [Header("状态监控")]
    public bool isSelected = false;
    public bool isUIOpen = false; // 此状态由光屏脚本同步控制

    [Header("控制与难度设置")]
    [Tooltip("WASD 调节角度的速度")]
    public float rotationSpeed = 10f;
    [Tooltip("初始光轴偏离的最大角度范围 (度)")]
    public float maxRandomDeflection = 4f;

    [Header("高亮设置")]
    [Tooltip("选中时的自发光/高亮颜色")]
    public Color highlightColor = new Color(0.2f, 0.8f, 0.2f, 1f); // 浅绿色
    private Material _material;
    private Color _originalColor;

    private CrystalPhysicalCore _crystalCore;
    private float _lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;

    private void Awake()
    {
        Instance = this;
        _crystalCore = GetComponent<CrystalPhysicalCore>();

        // 获取材质以便实现高亮功能
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            _material = renderer.material;
            if (_material.HasProperty("_Color"))
                _originalColor = _material.color;
        }
    }

    private void Start()
    {
        // 游戏开始时：制造一个光路未对准的初始状态
        ApplyInitialRandomDeflection();
    }

    /// <summary>
    /// 核心：制造初始偏转并传给DLL
    /// </summary>
    private void ApplyInitialRandomDeflection()
    {
        CrystalConfig config = _crystalCore.CurrentConfig;

        // 绕 X 和 Y 轴随机偏转，使得光轴 (Z轴) 偏离完美中心
        float randPitch = Random.Range(-maxRandomDeflection, maxRandomDeflection);
        float randYaw = Random.Range(-maxRandomDeflection, maxRandomDeflection);

        config.crystalRotation *= Quaternion.Euler(randPitch, randYaw, 0);

        // 立即下发给 DLL，此时底层数据已处于“未校准”状态
        _crystalCore.ApplyConfig(config);
        Debug.Log($"【晶体】已应用初始随机偏转: Pitch={randPitch:F2}°, Yaw={randYaw:F2}°。光路目前未对准。");
    }

    // 鼠标双击检测
    private void OnMouseDown()
    {
        float timeSinceLastClick = Time.time - _lastClickTime;
        if (timeSinceLastClick <= DOUBLE_CLICK_THRESHOLD)
        {
            isSelected = true;
            SetHighlight(true); // 开启高亮
            Debug.Log("【晶体】已被双击选中（已高亮）！现在请双击光屏打开UI。");
        }
        _lastClickTime = Time.time;
    }

    private void Update()
    {
        // 只有在【被选中】且【UI面板打开】时，才允许通过 WASD 发送数据给底层
        if (!isSelected || !isUIOpen) return;

        float vInput = Input.GetAxis("Vertical");   // W/S
        float hInput = Input.GetAxis("Horizontal"); // A/D

        if (Mathf.Abs(vInput) > 0.001f || Mathf.Abs(hInput) > 0.001f)
        {
            UpdateConfigAndSendToDLL(vInput, hInput);
        }
    }

    /// <summary>
    /// 核心：实时捕获玩家输入，修改包裹，传给DLL
    /// </summary>
    private void UpdateConfigAndSendToDLL(float vInput, float hInput)
    {
        CrystalConfig currentConfig = _crystalCore.CurrentConfig;

        float deltaPitch = vInput * rotationSpeed * Time.deltaTime;
        float deltaYaw = -hInput * rotationSpeed * Time.deltaTime;

        currentConfig.crystalRotation *= Quaternion.Euler(deltaPitch, deltaYaw, 0);

        // 实时传输！调用 ApplyConfig 触发底层的 DLL 计算
        _crystalCore.ApplyConfig(currentConfig);
    }

    /// <summary>
    /// 切换高亮表现
    /// </summary>
    private void SetHighlight(bool active)
    {
        if (_material != null)
        {
            // 基础换色法
            if (_material.HasProperty("_Color"))
            {
                _material.color = active ? highlightColor : _originalColor;
            }

            // 如果你使用的是 Standard Shader，用下面这行可以实现自发光高亮（推荐取消注释）
            /*
            if (active) _material.EnableKeyword("_EMISSION"); else _material.DisableKeyword("_EMISSION");
            _material.SetColor("_EmissionColor", active ? highlightColor * 0.5f : Color.black);
            */
        }
    }
}