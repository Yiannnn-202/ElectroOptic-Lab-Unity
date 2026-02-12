using UnityEngine;

public class OpticalComponent : MonoBehaviour
{
    [Header("设置")]
    public LayerMask railLayer;
    public float hoverHeight = 0.5f;
    public float moveSpeed = 2.0f;

    [Header("吸附设置")]
    // 【新增】在这里填入你想要的吸附角度，比如 -90, 90, 180 等
    public float snapRotationY = -90f;

    [Header("状态")]
    public bool isSelected = false;
    public bool isOnRail = false;

    private Vector3 originalPos;
    private OpticalRail currentRail;

    void Start()
    {
        originalPos = transform.position;
    }

    // 1. 鼠标点击：触发“拿起”或“放下”
    void OnMouseDown()
    {
        // 【核心修改点 1】
        // 如果已经吸附在导轨上了，直接“return”（退出函数）
        // 这意味着点击它将没有任何反应，不会再进入“拿起”状态
        if (isOnRail)
        {
            Debug.Log("该物体已锁定在导轨上，无法移动。");
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
        // 注意：根据你的上一条反馈，你好像已经自己调换了 h 和 v 的逻辑
        // 如果你的场景里 W/S 是左右，A/D 是前后，请保留你的修改。
        // 下面是标准的 X/Z 平面移动逻辑，你可以根据手感微调：
        float h = Input.GetAxis("Vertical"); // A/D
        float v = Input.GetAxis("Horizontal");   // W/S

        // 这里使用了你的逻辑：x轴和z轴对调，且可能反向
        Vector3 movement = new Vector3(-h, 0, v) * moveSpeed * Time.deltaTime;

        transform.Translate(movement, Space.World);
    }

    void TryDrop()
    {
        if (CheckDropTarget())
        {
            isSelected = false;
            // isOnRail = true; // 这句在 SnapToRail 里已经写了
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
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.2f, railLayer);

        if (hitColliders.Length > 0)
        {
            Collider col = hitColliders[0];
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
        // 标记为已上导轨（这将触发 OnMouseDown 里的锁定逻辑）
        isOnRail = true;
        currentRail = rail;

        // 获取吸附位置
        Vector3 finalPos = rail.GetSnapPosition(transform.position);
        transform.position = finalPos;

        // 【核心修改点 2：旋转逻辑】
        // 使用 Inspector 面板里填写的 snapRotationY 角度
        // 这样就实现了你要求的 -90 度旋转
        transform.rotation = Quaternion.Euler(0, snapRotationY, 0);

        Debug.Log("已吸附并锁定！");
    }

    void OnDrawGizmos()
    {
        if (isSelected)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}