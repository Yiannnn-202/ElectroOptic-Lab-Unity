using UnityEngine;



/// <summary>

/// 导轨物体移动控制器 (最终版)

/// 功能：支持轴向选择，中文注释，互斥逻辑

/// </summary>

public class RailObjectMover : MonoBehaviour

{

    private static RailObjectMover currentActiveMover;



    // 🔥🔥🔥 轴向选择

    public enum MoveAxis { X_Axis, Y_Axis, Z_Axis }



    [Header("轴向设置 (重要！)")]

    [Tooltip("红=X, 绿=Y, 蓝=Z。请选择导轨延伸的方向。")]

    public MoveAxis moveAxis = MoveAxis.X_Axis;



    [Header("移动参数")]

    public float moveSpeed = 0.5f;



    [Header("移动范围")]

    public float minLimit = -3.0f;

    public float maxLimit = 3.0f;



    [Header("选中反馈")]

    public Color selectedColor = Color.yellow;

    private Color defaultColor;

    private Renderer myRenderer;



    [Header("冲突设置")]

    public bool ignoreRotateStandClicks = true;



    // 初始化

    void Start()

    {

        myRenderer = GetComponent<Renderer>();

        if (myRenderer == null)

        {

            Transform baseModel = transform.Find("大旋钮座.003");

            if (baseModel != null) myRenderer = baseModel.GetComponent<Renderer>();

            else myRenderer = GetComponentInChildren<Renderer>();

        }



        if (myRenderer != null)

        {

            defaultColor = myRenderer.material.color;

        }

    }



    // 点击事件

    private void OnMouseDown()

    {

        if (ignoreRotateStandClicks && IsClickingRotateStand()) return;



        Debug.Log($"🖱️ 选中底座: {gameObject.name}");



        // 互斥：关闭旋转座

        if (RotateStandController.IsAnyStandSelected)

        {

            RotateStandController.DeselectAll();

        }



        // 选中/取消选中

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



    // 检测子物体

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



    // 键盘移动逻辑

    void Update()

    {

        if (currentActiveMover != this) return;

        if (RotateStandController.IsAnyStandSelected) return;



        float moveDirection = 0f;

        if (Input.GetKey(KeyCode.A)) moveDirection = -1f;

        else if (Input.GetKey(KeyCode.D)) moveDirection = 1f;



        if (moveDirection != 0f) MoveObject(moveDirection);

    }



    // 核心移动计算

    void MoveObject(float direction)

    {

        Vector3 currentPos = transform.localPosition;

        float newVal = 0f;



        // 根据选择的轴向进行移动

        switch (moveAxis)

        {

            case MoveAxis.X_Axis:

                newVal = currentPos.x + (direction * moveSpeed * Time.deltaTime);

                newVal = Mathf.Clamp(newVal, minLimit, maxLimit);

                transform.localPosition = new Vector3(newVal, currentPos.y, currentPos.z);

                break;



            case MoveAxis.Y_Axis:

                newVal = currentPos.y + (direction * moveSpeed * Time.deltaTime);

                newVal = Mathf.Clamp(newVal, minLimit, maxLimit);

                transform.localPosition = new Vector3(currentPos.x, newVal, currentPos.z);

                break;



            case MoveAxis.Z_Axis:

                newVal = currentPos.z + (direction * moveSpeed * Time.deltaTime);

                newVal = Mathf.Clamp(newVal, minLimit, maxLimit);

                transform.localPosition = new Vector3(currentPos.x, currentPos.y, newVal);

                break;

        }

    }

}
