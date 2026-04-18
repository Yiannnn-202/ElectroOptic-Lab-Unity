using UnityEngine;
using UnityEngine.EventSystems;

// 偏振镜专属外挂：把 UI 长按转换为 A/D 键的旋转逻辑
public class RotateVirtualKeys : MonoBehaviour
{
    [Header("1. 拖入偏振镜本体 (挂着 RotateStandController 的那个)")]
    public RotateStandController targetStand;

    [Header("2. 拖入你做好的 UI 按钮")]
    public GameObject btnCounterClockwise; // 逆时针 (对应键盘 A 键)
    public GameObject btnClockwise;        // 顺时针 (对应键盘 D 键)

    // 记录当前应该旋转的方向
    private float currentDirection = 0f;

    void Start()
    {
        // 如果填了逆时针按钮，给它绑定 1f (和原代码 A 键一样)
        if (btnCounterClockwise != null) BindEvent(btnCounterClockwise, 1f);

        // 如果填了顺时针按钮，给它绑定 -1f (和原代码 D 键一样)
        if (btnClockwise != null) BindEvent(btnClockwise, -1f);
    }

    void Update()
    {
        // 只要按住了按钮，就调用偏振镜自己的速度开始转
        if (currentDirection != 0f && targetStand != null)
        {
            targetStand.transform.Rotate(Vector3.forward, currentDirection * targetStand.rotateSpeed * Time.deltaTime);
        }
    }

    // 核心：自动给 UI 按钮穿上“长按检测”的装备
    private void BindEvent(GameObject btn, float dir)
    {
        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.AddComponent<EventTrigger>();

        // 鼠标按下：开始转
        var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener((_) => currentDirection = dir);
        trigger.triggers.Add(pointerDown);

        // 鼠标松开：停止转
        var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener((_) => currentDirection = 0f);
        trigger.triggers.Add(pointerUp);
    }
}
