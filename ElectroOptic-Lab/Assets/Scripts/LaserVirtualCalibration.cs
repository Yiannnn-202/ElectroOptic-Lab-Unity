using UnityEngine;

/// <summary>
/// 激光器“视觉欺骗”微调控制器 (带完成锁定功能)
/// 功能：不移动物理模型，只通过修改光屏的 Offset 来实现红点的虚拟移动
/// </summary>
public class LaserVirtualCalibration : MonoBehaviour
{
    [Header("关联组件 (运行时自动寻找)")]
    public LaserStateController stateController;
    public DirectScreenController screenController;

    [Header("虚拟调节参数")]
    [Tooltip("红点移动灵敏度")]
    public float adjustSpeed = 0.015f;

    [Header("状态控制")]
    [Tooltip("第一步是否已经完成并锁定？")]
    public bool isCalibrationDone = false; // 👈 新增的安全锁

    void Start()
    {
        // 自动获取身上的开关组件
        if (stateController == null)
            stateController = GetComponent<LaserStateController>();

        // 自动去场景里寻找光屏
        if (screenController == null)
            screenController = FindObjectOfType<DirectScreenController>();
    }

    void Update()
    {
        // 如果第一步已经完成并锁定，直接退出，不允许再做任何修改！
        if (isCalibrationDone) return;

        // 只有在双击激光器解锁后，才允许键盘调节
        if (stateController != null && stateController.IsSelected && screenController != null)
        {
            float dt = adjustSpeed * Time.deltaTime;

            // 监听 WASD 键，悄悄修改光屏的 offsetX 和 offsetY
            if (Input.GetKey(KeyCode.W)) screenController.offsetY += dt;
            if (Input.GetKey(KeyCode.S)) screenController.offsetY -= dt;
            if (Input.GetKey(KeyCode.A)) screenController.offsetX -= dt;
            if (Input.GetKey(KeyCode.D)) screenController.offsetX += dt;

            // 限制调节范围
            screenController.offsetX = Mathf.Clamp(screenController.offsetX, -0.05f, 0.05f);
            screenController.offsetY = Mathf.Clamp(screenController.offsetY, -0.05f, 0.05f);

            // ==========================================
            // 🔒 【新增】锁定逻辑：按下回车键锁定第一步
            // ==========================================
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                isCalibrationDone = true; // 上锁！
                stateController.Deselect(); // 自动取消激光器的选中状态（去掉绿色高亮）
                Debug.Log("✅ 第一步对准已完成！偏移量已永久锁定，防止误触。");
            }
        }
    }
}
