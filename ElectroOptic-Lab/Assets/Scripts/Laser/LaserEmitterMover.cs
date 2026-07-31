using System.Collections;
using System.Collections.Generic;
using ElectroOptics.Mobile;
using UnityEngine;

/// <summary>
/// 激光发射器物理微调控制器 (真实移动版 + 完成锁定功能)
/// 移动范围：初始位置 ±0.035 米（XY 平面）
/// 仅在被 LaserStateController 选中后可移动
/// </summary>
public class LaserEmitterMover : MonoBehaviour
{
    [Header("微调参数")]
    public float moveSpeed = 0.5f;      // 降低速度，便于精细控制
    public float moveRange = 0.035f;    // ±0.035 米（可在 Inspector 修改）

    [Header("状态控制")]
    [Tooltip("第一步是否已经完成并锁定位置？")]
    public bool isCalibrationDone = false; // 👈 移植过来的安全锁

    private Vector3 originalPosition;
    private LaserStateController stateController;

    void Start()
    {
        originalPosition = transform.position;
        stateController = GetComponent<LaserStateController>();

        if (stateController == null)
        {
            Debug.LogError("LaserEmitterMover: 未找到 LaserStateController 组件！");
        }
    }

    void Update()
    {
        // 如果已经按回车键锁定了，就直接退出，不允许再移动模型！
        if (isCalibrationDone) return;

        // 只有在双击选中后才允许移动
        if (stateController == null || !stateController.IsSelected)
            return;

        // 获取 WASD 或 方向键的输入
        float horizontal = MobileVirtualInput.GetAxisRaw("Horizontal");
        float vertical = MobileVirtualInput.GetAxisRaw("Vertical");

        // 计算新的位置
        Vector3 movement = new Vector3(horizontal, vertical, 0f) * moveSpeed * Time.deltaTime;
        Vector3 newPosition = transform.position + movement;

        // 以初始位置为中心，限制在 ±moveRange 内，防止模型飞出桌子
        float minX = originalPosition.x - moveRange;
        float maxX = originalPosition.x + moveRange;
        float minY = originalPosition.y - moveRange;
        float maxY = originalPosition.y + moveRange;

        newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
        newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);
        newPosition.z = originalPosition.z; // 锁定 Z 轴

        // 应用真实的物理移动
        transform.position = newPosition;

        // ==========================================
        // 🔒 锁定逻辑：按下回车键锁定第一步
        // ==========================================
        if (MobileVirtualInput.GetKeyDown(KeyCode.Return) || MobileVirtualInput.GetKeyDown(KeyCode.KeypadEnter))
        {
            isCalibrationDone = true; // 上锁！
            stateController.Deselect(); // 取消绿色高亮
            Debug.Log("✅ 第一步物理对准已完成！模型位置已永久锁定，防止误触。");
        }
    }
}
