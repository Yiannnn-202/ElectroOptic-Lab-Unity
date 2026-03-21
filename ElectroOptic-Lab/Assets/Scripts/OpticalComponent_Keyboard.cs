using UnityEngine;

// ���ε�������ƶ��������Ϸ��ո�����£� ˫���ٴ�����
public class OpticalComponent : MonoBehaviour
{
    [Header("����")]
    public LayerMask railLayer;
    public float hoverHeight = 0.5f;
    public float moveSpeed = 2.0f;

    [Header("��������")]
    // ����������������������Ҫ�������Ƕȣ����� -90, 90, 180 ��
    public float snapRotationY = -90f;
    public float detectionRadius = 0.5f;
    [Tooltip("手动 Y 偏移：正值上抬，负值下压，修正碰撞体与模型底部不一致")]
    public float snapYOffset = 0f;

    [Header("״̬")]
    public bool isSelected = false;
    public bool isOnRail = false;

    private Vector3 originalPos;
    private OpticalRail currentRail;

    // �����������ڼ��˫���ı���
    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f; // 0.3���ڵ��������˫��

    void Start()
    {
        originalPos = transform.position;
    }

    // 1. ����������������𡱻򡰷��¡�
    void OnMouseDown()
    {
        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time;
        // ����˫�����
        // �������޸ĵ� 1��
        // ����Ѿ������ڵ������ˣ�ֱ�ӡ�return�����˳�������
        // ����ζ�ŵ������û���κη�Ӧ�������ٽ��롰����״̬
        if (isOnRail)
        {
            if(timeSinceLastClick < DOUBLE_CLICK_TIME)
            {
                Debug.Log("˫����⣺��������");
                isOnRail = false;
                PickUp();
            }
            return;
        }

        if (!isSelected)
        {
            PickUp();
        }
        else
        {
            TryDrop();
        }
    }

    // 2. ÿһ֡���������ƶ�
    void Update()
    {
        if (isSelected)
        {
            HandleKeyboardMove();

            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryDrop();
            }
        }
    }

    void PickUp()
    {
        isSelected = true;
        // ����ʱ��ȷ�� isOnRail Ϊ false�������߼�����
        isOnRail = false;
        currentRail = null;

        Vector3 currentPos = transform.position;
        currentPos.y = originalPos.y + hoverHeight;
        transform.position = currentPos;

        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;

        Debug.Log("��ѡ�У�" + gameObject.name);
    }

    void HandleKeyboardMove()
    {
        // ע�⣺���������һ��������������Ѿ��Լ������� h �� v ���߼�
        // �����ĳ����� W/S �����ң�A/D ��ǰ���뱣������޸ġ�
        // �����Ǳ�׼�� X/Z ƽ���ƶ��߼�������Ը����ָ�΢����
        float h = Input.GetAxis("Vertical"); // A/D
        float v = Input.GetAxis("Horizontal");   // W/S

        // ����ʹ��������߼���x���z��Ե����ҿ��ܷ���
        Vector3 movement = new Vector3(-h, 0, v) * moveSpeed * Time.deltaTime;

        transform.Translate(movement, Space.World);
    }

    void TryDrop()
    {
        if (CheckDropTarget())
        {
            isSelected = false;
            // isOnRail = true; // ����� SnapToRail ���Ѿ�д��
        }
        else
        {
            isSelected = false;
            // û��׼���Ż�ԭ�������棩
            Vector3 landPos = transform.position;
            landPos.y = originalPos.y;
            transform.position = landPos;
            Debug.Log("������������");
        }
    }

    private bool CheckDropTarget()
    {
        // ��ȡ��Χ��������ײ��
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, railLayer);

        // ���ؼ��޸ġ��������飬������ֻ���� 0 ��
        foreach (var col in hitColliders)
        {
            // ���Ի�ȡ�ű�������ͬʱ��鸸���壬����һ�㣩
            OpticalRail railScript = col.GetComponent<OpticalRail>();

            // ����ҵ��˺Ϸ��ĵ���
            if (railScript != null)
            {
                SnapToRail(railScript);
                return true; // �ҵ��˾����̷��سɹ�
            }
        }

        // ѭ�����궼û�ҵ����ŷ���ʧ��
        return false;
    }

    private void SnapToRail(OpticalRail rail)
    {
        // ���Ϊ���ϵ��죨�⽫���� OnMouseDown ��������߼���
        isOnRail = true;
        currentRail = rail;

        // 先应用旋转，使 bounds 反映最终朝向
        transform.rotation = Quaternion.Euler(0, snapRotationY, 0);

        // ��ȡ����λ��
        Vector3 finalPos = rail.GetSnapPosition(transform.position);

        // 自动修正：计算 pivot 到碰撞体底部的距离，向上偏移使底部贴合导轨
        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col != null)
        {
            float pivotToBottom = transform.position.y - col.bounds.min.y;
            finalPos.y += pivotToBottom;
        }

        // 手动修正：补偿碰撞体与模型视觉底部的差异
        finalPos.y += snapYOffset;

        transform.position = finalPos;

        Debug.Log("��������������");
    }

    void OnDrawGizmos()
    {
        if (isSelected)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}