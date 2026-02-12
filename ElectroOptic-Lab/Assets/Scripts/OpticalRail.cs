using UnityEngine;

public class OpticalRail : MonoBehaviour
{
    [Header("导轨设置")]
    // 现在的导轨是沿着 X 轴延伸的
    public Vector3 railDirection = Vector3.right;
    public float railLength = 1.0f;       // 导轨一半的长度
    public float railHeightOffset = 0.1f; // 导轨面的高度

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        // 画出 X 轴方向的辅助线
        Gizmos.DrawLine(transform.position - transform.right * railLength,
                        transform.position + transform.right * railLength);
    }

    public Vector3 GetSnapPosition(Vector3 worldPosition)
    {
        // 1. 转为局部坐标
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);

        // 2. 【核心修改】
        // X轴：保留原来的值（允许滑动），但限制在导轨长度范围内
        // Y轴：固定高度
        // Z轴：强制归零（对齐中心）
        Vector3 snappedLocal = new Vector3(
            Mathf.Clamp(localPos.x, -railLength, railLength), // 只有 X 是变量
            railHeightOffset,                                 // Y 固定
            0                                                 // Z 固定为 0
        );

        // 3. 转回世界坐标
        return transform.TransformPoint(snappedLocal);
    }
}