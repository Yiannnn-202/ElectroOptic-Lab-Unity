using UnityEngine;

public class OpticalRail : MonoBehaviour
{
    [Header("��������")]
    // ���ڵĵ��������� X �������
    public Vector3 railDirection = Vector3.right;
    public float railLength = 1.0f;       // ����һ��ĳ���
    public float railHeightOffset = 0.1f; // ������ĸ߶�

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        // ���� X �᷽��ĸ�����
        Gizmos.DrawLine(transform.position - transform.right * railLength,
                        transform.position + transform.right * railLength);
    }

    public Vector3 GetSnapPosition(Vector3 worldPosition)
    {
        // 1. תΪ�ֲ�����
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);

        // 2. �������޸ġ�
        // X�᣺����ԭ����ֵ���������������������ڵ��쳤�ȷ�Χ��
        // Y�᣺�̶��߶�
        // Z�᣺ǿ�ƹ��㣨�������ģ�
        // 自动获取轨道顶面高度
        float topY = railHeightOffset;
        var col = GetComponent<BoxCollider>();
        if (col != null)
        {
            topY = col.center.y + col.size.y / 2f;
        }

        Vector3 snappedLocal = new Vector3(
            Mathf.Clamp(localPos.x, -railLength, railLength), // ֻ�� X �Ǳ���
            topY,                                              // Y 轨道顶面
            0                                                  // Z �̶�Ϊ 0
        );

        // 3. ת����������
        return transform.TransformPoint(snappedLocal);
    }
}