using UnityEngine;

// 1. 定义“光的数据包”
// 这只是一个数据定义，不需要挂在物体上
[System.Serializable]
public struct LightData
{
    public float intensity;         // 光强 (0.0 ~ 1.0)
    public float polarizationAngle; // 偏振角度 (0 ~ 360 度)
    public float dop;               // 偏振度 (0~1, 0.9代表部分偏振)

    public LightData(float i, float angle, float d)
    {
        intensity = i;
        polarizationAngle = angle;
        dop = d;
    }
}

// 2. 定义接口
// 凡是能被光打中的东西（偏振片、屏幕），都必须遵守这个规则
public interface IOpticalReceiver
{
    void ReceiveLight(LightData lightIn, Vector3 hitPoint, Vector3 direction);
}

