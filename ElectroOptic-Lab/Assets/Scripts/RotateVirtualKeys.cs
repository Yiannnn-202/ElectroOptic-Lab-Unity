using UnityEngine;
using UnityEngine.EventSystems;

// ƫ��ר����ң��� UI ����ת��Ϊ A/D ������ת�߼�
public class RotateVirtualKeys : MonoBehaviour
{
    [Header("1. ����ƫ�񾵱��� (���� RotateStandController ���Ǹ�)")]
    public RotateStandController targetStand;

    [Header("2. ���������õ� UI ��ť")]
    public GameObject btnCounterClockwise; // ��ʱ�� (��Ӧ���� A ��)
    public GameObject btnClockwise;        // ˳ʱ�� (��Ӧ���� D ��)

    // ��¼��ǰӦ����ת�ķ���
    private float currentDirection = 0f;

    void Start()
    {
        // ���������ʱ�밴ť�������� 1f (��ԭ���� A ��һ��)
        if (btnCounterClockwise != null) BindEvent(btnCounterClockwise, 1f);

        // �������˳ʱ�밴ť�������� -1f (��ԭ���� D ��һ��)
        if (btnClockwise != null) BindEvent(btnClockwise, -1f);
    }

    void Update()
    {
        // ֻҪ��ס�˰�ť���͵���ƫ���Լ����ٶȿ�ʼת
        if (currentDirection != 0f && targetStand != null)
        {
            targetStand.RotateBy(currentDirection * targetStand.rotateSpeed * Time.deltaTime);
        }
    }

    // ���ģ��Զ��� UI ��ť���ϡ�������⡱��װ��
    private void BindEvent(GameObject btn, float dir)
    {
        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.AddComponent<EventTrigger>();

        // ��갴�£���ʼת
        var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener((_) => currentDirection = dir);
        trigger.triggers.Add(pointerDown);

        // ����ɿ���ֹͣת
        var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener((_) => currentDirection = 0f);
        trigger.triggers.Add(pointerUp);
    }
}
