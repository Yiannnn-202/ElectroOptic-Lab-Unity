using UnityEngine;

public class RailObjectMover : MonoBehaviour
{
    private static RailObjectMover currentActiveMover;

    [Header("移动参数")]
    public float moveSpeed = 0.5f;

    [Header("移动范围")]
    public float minXLimit = -3.0f;
    public float maxXLimit = 3.0f;

    [Header("选中反馈")]
    public Color selectedColor = Color.yellow;
    private Color defaultColor;
    private Renderer myRenderer;

    [Header("冲突设置")]
    public bool ignoreRotateStandClicks = true;

    void Start()
    {
        myRenderer = GetComponent<Renderer>();
        if (myRenderer == null)
        {
            // 尝试找底座模型，防止误把光屏给变色了
            Transform baseModel = transform.Find("大旋钮座.003"); // 替换为你底座模型的实际名字
            if (baseModel != null) myRenderer = baseModel.GetComponent<Renderer>();
            else myRenderer = GetComponentInChildren<Renderer>();
        }

        if (myRenderer != null)
        {
            defaultColor = myRenderer.material.color;
        }
    }

    private void OnMouseDown()
    {
        // 1. 如果点到了旋转座，就让旋转座处理，我不插手
        if (ignoreRotateStandClicks && IsClickingRotateStand())
        {
            return;
        }

        Debug.Log($"??? 选中底座: {gameObject.name}");

        // 2. 【核心互斥】如果我点击了底座，说明我想移动底座
        // 此时强制让所有旋转座“闭嘴”（取消选中）
        if (RotateStandController.IsAnyStandSelected)
        {
            RotateStandController.DeselectAll();
        }

        // 3. 正常的选中/切换逻辑
        if (currentActiveMover == this)
        {
            Deselect();
            currentActiveMover = null;
        }
        else
        {
            if (currentActiveMover != null) currentActiveMover.Deselect();
            currentActiveMover = this;
            Select();
        }
    }

    private bool IsClickingRotateStand()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<RotateStandController>() != null) return true;
        }
        return false;
    }

    void Select()
    {
        if (myRenderer != null) myRenderer.material.color = selectedColor;
    }

    public void Deselect()
    {
        if (myRenderer != null) myRenderer.material.color = defaultColor;
    }

    void Update()
    {
        // 如果我没被选中，我不动
        if (currentActiveMover != this) return;

        // 【核心互斥】如果此时有任何旋转座被选中了（绿了），我也不能动！
        // 这一行解决了“边转边跑”的冲突
        if (RotateStandController.IsAnyStandSelected) return;

        float moveDirection = 0f;
        if (Input.GetKey(KeyCode.A)) moveDirection = -1f;
        else if (Input.GetKey(KeyCode.D)) moveDirection = 1f;

        if (moveDirection != 0f) MoveObject(moveDirection);
    }

    void MoveObject(float direction)
    {
        Vector3 currentPos = transform.localPosition;
        float targetX = currentPos.x + (direction * moveSpeed * Time.deltaTime);
        targetX = Mathf.Clamp(targetX, minXLimit, maxXLimit);
        transform.localPosition = new Vector3(targetX, currentPos.y, currentPos.z);
    }
}
