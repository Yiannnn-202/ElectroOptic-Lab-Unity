using UnityEngine;

// 单次点击拿起，移动到导轨上方空格键放下， 双击再次拿起
public class OpticalComponent : MonoBehaviour
{
    [Header("设置")]
    public LayerMask railLayer;
    public float hoverHeight = 0.5f;
    public float moveSpeed = 2.0f;

    [Header("吸附设置")]
    // 在这里填入你想要的吸附角度，比如 -90, 0, 90, 180 等（解决光屏侧身的问题）
    public float snapRotationY = -90f;
    public float detectionRadius = 0.5f;

    // 【新增】吸附高度补偿！用于解决模型中心点在中间导致陷进导轨的问题
    [Tooltip("如果模型吸附后陷进导轨，请增大这个值；如果悬空，请减小")]
    public float snapYOffset = 0f;

    [Header("状态")]
    public bool isSelected = false;
    public bool isOnRail = false;

    private Vector3 originalPos;
    private OpticalRail currentRail;

    // 用于检测双击的变量
    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f; // 0.3秒内点击两次算双击

    void Start()
    {
        originalPos = transform.position;
    }

    // 1. 鼠标点击：触发“拿起”或“放下”
    void OnMouseDown()
    {
        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time;

        // 新增双击检测
        // 如果已经吸附在导轨上了，点击它不会直接拿起，只有双击才会
        if (isOnRail)
        {
            if (timeSinceLastClick < DOUBLE_CLICK_TIME)
            {
                Debug.Log("双击检测：尝试拿起");
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

    // 2. 每一帧处理键盘移动
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
        // 拿起时，确保 isOnRail 为 false，否则逻辑会打架
        isOnRail = false;
        currentRail = null;

        Vector3 currentPos = transform.position;
        currentPos.y = originalPos.y + hoverHeight;
        transform.position = currentPos;

        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;

        Debug.Log("已选中：" + gameObject.name);
    }

    void HandleKeyboardMove()
    {
        // 这里保留了你之前修改过的移动逻辑（W/S与A/D控制可能反转或调换过轴向）
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
            // 没对准，放回原处（桌面）
            Vector3 landPos = transform.position;
            landPos.y = originalPos.y;
            transform.position = landPos;
            Debug.Log("放置在桌面上");
        }
    }

    private bool CheckDropTarget()
    {
        // 获取范围内所有碰撞体
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, railLayer);

        // 遍历数组寻找合法的导轨
        foreach (var col in hitColliders)
        {
            OpticalRail railScript = col.GetComponent<OpticalRail>();

            if (railScript != null)
            {
                SnapToRail(railScript);
                return true; // 找到了就立刻返回成功
            }
        }

        return false;
    }

    private void SnapToRail(OpticalRail rail)
    {
        isOnRail = true;
        currentRail = rail;

        // 1. 获取导轨给出的表面吸附位置
        Vector3 finalPos = rail.GetSnapPosition(transform.position);

        // 2. 【核心修改】加上物体自身的 Y 轴高度补偿，把陷进去的部分“拔”出来！
        finalPos.y += snapYOffset;

        // 3. 应用最终坐标
        transform.position = finalPos;

        // 4. 设置吸附后的朝向
        transform.rotation = Quaternion.Euler(0, snapRotationY, 0);

        Debug.Log("已吸附并锁定！");
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
