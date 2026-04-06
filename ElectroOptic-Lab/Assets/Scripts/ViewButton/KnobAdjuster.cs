using UnityEngine;
using UnityEngine.EventSystems;

// 继承 IPointerDownHandler 和 IPointerUpHandler 来监听鼠标的按下和抬起
public class KnobAdjuster : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("目标旋钮")]
    [Tooltip("拖入需要被旋转的 3D 旋钮模型")]
    public Transform targetKnob;

    [Header("旋转设置")]
    [Tooltip("旋转的轴向，例如 (0, 1, 0) 代表绕Y轴旋转，(0, 0, 1) 代表绕Z轴")]
    public Vector3 rotationAxis = Vector3.up;

    [Tooltip("旋转速度，正数顺时针，负数逆时针")]
    public float rotateSpeed = 50f;

    private bool isPressed = false;

    // 鼠标左键按下时触发
    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
    }

    // 鼠标左键松开时触发
    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    private void Update()
    {
        // 如果按钮一直被按住，就持续旋转目标物体
        if (isPressed && targetKnob != null)
        {
            // Space.Self 确保是围绕旋钮自身的局部坐标系旋转
            targetKnob.Rotate(rotationAxis * rotateSpeed * Time.deltaTime, Space.Self);

            // 如果你需要将这个旋转角度同步到后端的 DLL 光学计算中，
            // 可以在这里调用类似 CrystalInteract 里的 UpdateConfigAndSendToDLL() 方法
        }
    }
}