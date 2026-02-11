using UnityEngine;

public class LaserEmitter : MonoBehaviour
{
    [Header("激光源设置")]
    public float intensity = 1.0f;
    public float polarizationAngle = 90f; // 竖直偏振
    [Range(0, 1)] public float dop = 0.9f; // 部分偏振

    private LineRenderer lineRenderer;

    void Start()
    {
        // 自动给自己装一个画线工具
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        // 使用默认材质，设为红色
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;
    }

    void Update()
    {
        // 每一帧发射光线
        EmitLaser();
    }

    void EmitLaser()
    {
        LightData light = new LightData(intensity, polarizationAngle, dop);

        Vector3 start = transform.position;
        Vector3 dir = transform.right; // 沿着红色X轴发射
        Vector3 end = start + dir * 50f; // 默认射很远

        // 发射物理射线
        if (Physics.Raycast(start, dir, out RaycastHit hit, 50f))
        {
            end = hit.point; // 光线停在打中点

            // 尝试找对方是不是“光学接收者”
            var receiver = hit.collider.GetComponent<IOpticalReceiver>();
            if (receiver == null) receiver = hit.collider.GetComponentInParent<IOpticalReceiver>();

            // 如果对方能收光，就把数据传给它
            if (receiver != null)
            {
                receiver.ReceiveLight(light, hit.point, dir);
            }
        }

        // 画线
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }
}

