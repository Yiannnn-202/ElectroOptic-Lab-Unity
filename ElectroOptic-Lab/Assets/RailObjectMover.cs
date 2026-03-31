using UnityEngine;

/// <summary>
/// 导轨物体移动控制器 (最终版)
/// 功能：支持轴向选择，中文注释，互斥逻辑，集成 QuickOutline，增加特写镜头锚点与移动锁定
/// </summary>
public class RailObjectMover : MonoBehaviour
{
    // 【修改】公开为属性，方便外部（如相机控制器）获取当前选中的物体
    public static RailObjectMover CurrentActiveMover { get; private set; }

    // 🔥🔥🔥 轴向选择
    public enum MoveAxis { X_Axis, Y_Axis, Z_Axis }

    [Header("轴向设置 (重要！)")]
    [Tooltip("红=X, 绿=Y, 蓝=Z。请选择导轨延伸的方向。")]
    public MoveAxis moveAxis = MoveAxis.X_Axis;

    [Header("移动参数")]
    public float moveSpeed = 0.5f;

    [Header("移动范围")]
    public float minLimit = -3.0f;
    public float maxLimit = 3.0f;

    [Header("冲突设置")]
    public bool ignoreRotateStandClicks = true;

    // --- 【新增】特写镜头与移动锁 ---
    [Header("特写镜头设置")]
    [Tooltip("请拖入一个空物体作为特写镜头的位置参考")]
    public Transform closeUpCameraAnchor;

    [HideInInspector]
    public bool isMovementLocked = false; // 为 true 时，禁止 AD 键移动

    // --- QuickOutline 引用 ---
    private Outline outline;

    // 初始化
    void Start()
    {
        // 极简处理：只获取或添加 Outline，不再管 Renderer 的颜色了
        outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }
        outline.enabled = false; // 默认关闭高光
    }

    // 点击事件
    private void OnMouseDown()
    {
        if (ignoreRotateStandClicks && IsClickingRotateStand()) return;
        Debug.Log($"🖱️ 选中底座: {gameObject.name}");

        // 互斥：关闭旋转座 (保持原有逻辑)
        if (RotateStandController.IsAnyStandSelected)
        {
            RotateStandController.DeselectAll();
        }

        // 选中/取消选中
        if (CurrentActiveMover == this)
        {
            Deselect();
            CurrentActiveMover = null;
        }
        else
        {
            if (CurrentActiveMover != null) CurrentActiveMover.Deselect();
            CurrentActiveMover = this;
            Select();
        }
    }

    // 检测子物体 (保持原有逻辑)
    private bool IsClickingRotateStand()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<RotateStandController>() != null) return true;
        }
        return false;
    }

    void Select()
    {
        // --- 打开高光 ---
        if (outline != null) outline.enabled = true;
    }

    public void Deselect()
    {
        // --- 关闭高光 ---
        if (outline != null) outline.enabled = false;

        // 【新增】如果取消选中，确保解锁移动限制
        isMovementLocked = false;
    }

    // 键盘移动逻辑
    void Update()
    {
        if (CurrentActiveMover != this) return;
        if (RotateStandController.IsAnyStandSelected) return;

        // 【新增】如果处于特写锁定状态，直接跳出，不允许移动
        if (isMovementLocked) return;

        float moveDirection = 0f;
        if (Input.GetKey(KeyCode.A)) moveDirection = -1f;
        else if (Input.GetKey(KeyCode.D)) moveDirection = 1f;

        if (moveDirection != 0f) MoveObject(moveDirection);
    }

    // 核心移动计算 (保持原有逻辑)
    void MoveObject(float direction)
    {
        Vector3 currentPos = transform.localPosition;
        float newVal = 0f;

        switch (moveAxis)
        {
            case MoveAxis.X_Axis:
                newVal = currentPos.x + (direction * moveSpeed * Time.deltaTime);
                newVal = Mathf.Clamp(newVal, minLimit, maxLimit);
                transform.localPosition = new Vector3(newVal, currentPos.y, currentPos.z);
                break;

            case MoveAxis.Y_Axis:
                newVal = currentPos.y + (direction * moveSpeed * Time.deltaTime);
                newVal = Mathf.Clamp(newVal, minLimit, maxLimit);
                transform.localPosition = new Vector3(currentPos.x, newVal, currentPos.z);
                break;

            case MoveAxis.Z_Axis:
                newVal = currentPos.z + (direction * moveSpeed * Time.deltaTime);
                newVal = Mathf.Clamp(newVal, minLimit, maxLimit);
                transform.localPosition = new Vector3(currentPos.x, currentPos.y, newVal);
                break;
        }
    }
}