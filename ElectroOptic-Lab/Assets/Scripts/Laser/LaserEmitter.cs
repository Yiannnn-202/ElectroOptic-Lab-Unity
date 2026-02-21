using UnityEngine;

//ZYX
/// <summary>
/// 创建激光 (带枪口偏移与反向发射版)
/// </summary>
public class LaserEmitter : MonoBehaviour
{
    [Header("激光源设置")]
    public float intensity = 1.0f;
    public float polarizationAngle = 90f; // 竖直偏振
    [Range(0, 1)] public float dop = 0.9f; // 部分偏振

    [Header("🔫 物理防挡设置")]
    [Tooltip("将射线的起点沿着发射方向往前推多少米，以防打到自己的模型外壳（填正数）")]
    public float startOffset = 0.2f;

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

        // ==========================================
        // 🎯 【修改点1】：加了负号！沿着红色X轴的负方向发射
        // ==========================================
        Vector3 dir = -transform.right;

        // ==========================================
        // 🎯 【修改点2】：起点前移！
        // 既然 dir 已经是负方向了，这里 startOffset 依然填正数（比如 0.2）
        // 它就会自动顺着射线发射的方向（即负X轴方向）往外移出枪管！
        // ==========================================
        Vector3 start = transform.position + dir * startOffset;

        Vector3 end = start + dir * 50f; // 默认射很远

        // 发射物理射线 (从推出去的新起点开始射)
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

        // 画线：视觉上也从“偏移后的枪口”开始画，看起来更逼真
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }
}
