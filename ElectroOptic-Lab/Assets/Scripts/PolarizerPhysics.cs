using UnityEngine;

//ZYX

/// <summary>

/// 马吕斯定律（挂载在两个偏振片的子物体上）计算从每个偏振片出射时的光强和振动方向

/// </summary>

public class PolarizerPhysics : MonoBehaviour, IOpticalReceiver

{

    private LineRenderer lr;

    private bool gotLight = false;



    void Start()

    {

        lr = gameObject.AddComponent<LineRenderer>();

        lr.startWidth = 0.02f;

        lr.endWidth = 0.02f;

        lr.material = new Material(Shader.Find("Sprites/Default"));

        lr.startColor = new Color(1, 0, 0, 0.5f); // 半透明红

        lr.endColor = new Color(1, 0, 0, 0.5f);

        lr.enabled = false;

    }



    void Update()

    {

        // 如果这一帧没收到光，就把线关掉

        if (!gotLight) lr.enabled = false;

        gotLight = false; // 重置状态

    }



    // 实现接口：当被上一级光打中时自动触发

    public void ReceiveLight(LightData inLight, Vector3 hitPoint, Vector3 dir)

    {

        gotLight = true;



        // --- 物理计算 (马吕斯定律 + 部分偏振) ---

        float axis = transform.eulerAngles.z; // 读取自身旋转角度



        // 1. 自然光部分 (减半)

        float i_unpol = inLight.intensity * (1 - inLight.dop) * 0.5f;



        // 2. 偏振光部分 (马吕斯定律)

        float delta = (axis - inLight.polarizationAngle) * Mathf.Deg2Rad;

        float i_pol = (inLight.intensity * inLight.dop) * Mathf.Pow(Mathf.Cos(delta), 2);



        float finalI = i_unpol + i_pol;



        // --- 射出下一级光 ---

        if (finalI > 0.001f) // 有亮度才射

        {

            lr.enabled = true;

            lr.startColor = new Color(1, 0, 0, finalI); // 亮度随强度变

            lr.endColor = new Color(1, 0, 0, finalI);



            // 从背面射出 (防止自己挡住自己)

            Vector3 start = hitPoint + dir * 0.05f;

            Vector3 end = start + dir * 50f;



            if (Physics.Raycast(start, dir, out RaycastHit hit, 50f))

            {

                end = hit.point;

                // 传给下一个接收者 (可能是另一个偏振片，或者是光屏)

                var next = hit.collider.GetComponent<IOpticalReceiver>();

                if (next == null) next = hit.collider.GetComponentInParent<IOpticalReceiver>();



                if (next != null)

                {

                    // 发出的光变成了完全线偏振光 (DOP=1)

                    LightData outLight = new LightData(finalI, axis, 1.0f);

                    next.ReceiveLight(outLight, hit.point, dir);

                }

            }

            lr.SetPosition(0, start);

            lr.SetPosition(1, end);

        }

    }

}
