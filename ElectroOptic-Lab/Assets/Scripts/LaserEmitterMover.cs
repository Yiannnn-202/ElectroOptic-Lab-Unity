using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 激光发射器微调控制器
/// 移动范围：初始位置 ±0.035 米（XY 平面）
/// 仅在被 LaserStateController 选中后可移动
/// </summary>
public class LaserEmitterMover : MonoBehaviour
{
    [Header("微调参数")]
    public float moveSpeed = 0.5f;      // 降低速度，便于精细控制
    public float moveRange = 0.035f;    // ±0.035 米（可在 Inspector 修改）

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
        if (stateController == null || !stateController.IsSelected)
            return;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // 微调：降低灵敏度
        Vector3 movement = new Vector3(horizontal, vertical, 0f) * moveSpeed * Time.deltaTime;
        Vector3 newPosition = transform.position + movement;

        // 以初始位置为中心，限制在 ±moveRange 内
        float minX = originalPosition.x - moveRange;
        float maxX = originalPosition.x + moveRange;
        float minY = originalPosition.y - moveRange;
        float maxY = originalPosition.y + moveRange;

        newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
        newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);
        newPosition.z = originalPosition.z; // 锁定 Z 轴（可选）

        transform.position = newPosition;
    }
}