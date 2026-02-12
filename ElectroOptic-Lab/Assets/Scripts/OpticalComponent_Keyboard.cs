using UnityEngine;

public class OpticalComponent : MonoBehaviour
{
    [Header("设置")]
    public LayerMask railLayer;     // 记得选 Rail 层
    public float hoverHeight = 0.5f; // 拿起时悬浮的高度
    public float moveSpeed = 2.0f;   // 键盘移动速度

    [Header("状态")]
    public bool isSelected = false; // 是否正在被拿着
    public bool isOnRail = false;

    private Vector3 originalPos; // 记录拿起前的位置，以便取消操作时复原
    private OpticalRail currentRail;

    void Start()
    {
        originalPos = transform.position;
    }

    // 1. 鼠标点击：触发“拿起”或“放下”
    void OnMouseDown()
    {
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

            // 也可以支持按空格键放下
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryDrop();
            }
        }
    }

    // --- 核心逻辑 ---

    void PickUp()
    {
        isSelected = true;
        isOnRail = false;
        currentRail = null;

        // 记录一下“老家”，万一不想动了可以撤销（可选）
        // originalPos = transform.position; 

        // 视觉反馈：抬高一点
        Vector3 currentPos = transform.position;
        currentPos.y = originalPos.y + hoverHeight;
        transform.position = currentPos;

        // 暂时关闭物理（如果有Rigidbody）
        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;

        Debug.Log("已选中：" + gameObject.name + "，请使用键盘 WASD 移动");
    }

    void HandleKeyboardMove()
    {
        float h = Input.GetAxis("Horizontal"); // A/D 或 左右箭头
        float v = Input.GetAxis("Vertical");   // W/S 或 上下箭头

        // 构造移动向量 (假设是在世界坐标系的水平面上移动)
        Vector3 movement = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;

        // 应用移动
        transform.Translate(movement, Space.World);
    }

    void TryDrop()
    {
        // 检测下方是否有导轨
        if (CheckDropTarget())
        {
            // 如果放下了，状态结束
            isSelected = false;
        }
        else
        {
            // 如果没对准导轨，是否要弹回原处？
            // 或者：允许放在桌面上任意位置（当前逻辑是允许放在任意位置，只是不吸附）
            isSelected = false;

            // 让它落回桌面高度 (假设桌面高度是 originalPos.y)
            Vector3 landPos = transform.position;
            landPos.y = originalPos.y;
            transform.position = landPos;

            Debug.Log("放置在桌面上");
        }
    }

    // --- 检测逻辑 (与之前类似，但适配新流程) ---

    private bool CheckDropTarget()
    {
        // 使用球体检测，容错率高
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.2f, railLayer);

        if (hitColliders.Length > 0)
        {
            Collider col = hitColliders[0];
            OpticalRail railScript = col.GetComponent<OpticalRail>();

            if (railScript != null)
            {
                SnapToRail(railScript);
                return true; // 放置成功
            }
        }
        return false; // 没找到导轨
    }

    private void SnapToRail(OpticalRail rail)
    {
        isOnRail = true;
        currentRail = rail;

        // 获取吸附位置
        // 注意：这里我们用 transform.position，因为现在就是物体在哪里就在哪里吸附
        Vector3 finalPos = rail.GetSnapPosition(transform.position);
        transform.position = finalPos;

        // 修正旋转
        transform.rotation = Quaternion.LookRotation(Vector3.forward);

        Debug.Log("已吸附到导轨！");
    }

    // 画个圈圈方便调试
    void OnDrawGizmos()
    {
        if (isSelected)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}