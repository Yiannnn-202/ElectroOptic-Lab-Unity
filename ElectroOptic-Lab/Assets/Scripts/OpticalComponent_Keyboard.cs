using UnityEngine;

// 键盘控制：拾取移动，点击放下/吸附导轨，双击再次拾起
public class OpticalComponent : MonoBehaviour
{
    [Header("基础")]
    public LayerMask railLayer;
    public float hoverHeight = 0.5f;
    public float moveSpeed = 2.0f;

    [Header("吸附配置")]
    // 吸附后物体朝向，需要手动填角度，如 -90, 0, 90, 180 等（根据物体朝向自行调整）
    public float snapRotationY = -90f;
    public float detectionRadius = 0.5f;
    [Tooltip("手动 Y 偏移：正值上抬，负值下压，修正碰撞体与模型底部不一致")]
    public float snapYOffset = 0f;

    [Header("状态")]
    public bool isSelected = false;
    public bool isOnRail = false;

    private Vector3 originalPos;
    private OpticalRail currentRail;

    // 用于检测双击的变量
    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f; // 0.3秒内点击两次算双击

    // --- 高亮组件（QuickOutline 插件） ---
    private Outline outline;

    void Start()
    {
        originalPos = transform.position;

        // 初始化 Outline 组件
        outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }
        outline.enabled = false; // 默认关闭高光
    }

    // 1. 点击物体：切换"拾起"或"放下"
    void OnMouseDown()
    {
        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time;

        // 处理双击逻辑
        // 如果已经吸附在导轨上，单击直接忽略，只有双击才会拾起
        if (isOnRail)
        {
            if (timeSinceLastClick < DOUBLE_CLICK_TIME)
            {
                Debug.Log("双击检测：从导轨取下");
                isOnRail = false;
                PickUp();
            }
            return;
        }

        if (!isSelected)
        {
            PickUp();
        }
        else
        {
            TryDrop();
        }
    }

    // 2. 每一帧：键盘移动
    void Update()
    {
        if (isSelected)
        {
            HandleKeyboardMove();

            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryDrop();
            }
        }
    }

    void PickUp()
    {
        isSelected = true;
        isOnRail = false;
        currentRail = null;

        // --- 打开高光 ---
        if (outline != null) outline.enabled = true;

        Vector3 currentPos = transform.position;
        currentPos.y = originalPos.y + hoverHeight;
        transform.position = currentPos;

        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;

        Debug.Log("已选中：" + gameObject.name);
    }

    void HandleKeyboardMove()
    {
        float h = Input.GetAxis("Vertical"); // A/D
        float v = Input.GetAxis("Horizontal");   // W/S

        Vector3 movement = new Vector3(-h, 0, v) * moveSpeed * Time.deltaTime;

        transform.Translate(movement, Space.World);
    }

    void TryDrop()
    {
        if (CheckDropTarget())
        {
            isSelected = false;
        }
        else
        {
            isSelected = false;
            // 没有对准导轨，回到原始高度
            Vector3 landPos = transform.position;
            landPos.y = originalPos.y;
            transform.position = landPos;
            Debug.Log("放下，未吸附导轨");
        }

        // --- 关闭高光 ---
        if (outline != null) outline.enabled = false;
    }

    private bool CheckDropTarget()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, railLayer);

        foreach (var col in hitColliders)
        {
            OpticalRail railScript = col.GetComponent<OpticalRail>();

            if (railScript != null)
            {
                SnapToRail(railScript);
                return true;
            }
        }
        return false;
    }

    private void SnapToRail(OpticalRail rail)
    {
        isOnRail = true;
        currentRail = rail;

        // 先应用旋转，使 bounds 反映最终朝向
        transform.rotation = Quaternion.Euler(0, snapRotationY, 0);

        // 获取吸附位置
        Vector3 finalPos = rail.GetSnapPosition(transform.position);

        // 自动修正：计算 pivot 到碰撞体底部的距离，向上偏移使底部贴合导轨
        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col != null)
        {
            float pivotToBottom = transform.position.y - col.bounds.min.y;
            finalPos.y += pivotToBottom;
        }

        // 手动修正：补偿碰撞体与模型视觉底部的差异
        finalPos.y += snapYOffset;

        transform.position = finalPos;

        Debug.Log("已吸附到导轨");
    }

    void OnDrawGizmos()
    {
        if (isSelected)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
