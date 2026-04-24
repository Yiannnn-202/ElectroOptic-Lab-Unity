using UnityEngine;

/// <summary>
/// 导轨物体移动控制器 (最终通用版)
/// 功能：支持轴向选择，中文注释，互斥逻辑，集成 QuickOutline，增加特写镜头锚点与UI面板控制
/// 【新增】：支持通过 canMove 开关锁定移动（专门给激光器等固定仪器使用）
/// </summary>
public class RailObjectMover : MonoBehaviour
{
    public static RailObjectMover CurrentActiveMover { get; private set; }

    public enum MoveAxis { X_Axis, Y_Axis, Z_Axis }

    [Header("移动参数 (重要)")]
    [Tooltip("取消勾选后，该物体可以被选中高亮，但无法通过AD键移动")]
    public bool canMove = true; // 🎯 新增的开关
    [Tooltip("勾选后，即使相机在看其他物体的特写，也能强制点击并移动这个物体（专为光屏设计）")]
    public bool allowInteractInCloseUp = false;

    public MoveAxis moveAxis = MoveAxis.X_Axis;
    public float moveSpeed = 0.5f;
    public float minLimit = -3.0f;
    public float maxLimit = 3.0f;
    public bool ignoreRotateStandClicks = true;

    [Header("特写镜头与UI设置")]
    [Tooltip("请拖入一个空物体作为特写镜头的位置参考")]
    public Transform closeUpCameraAnchor;

    [Tooltip("拖入该物体下属的 CloseUpUI 画布")]
    public Canvas closeUpUICanvas;

    [HideInInspector]
    public bool isMovementLocked = false;

    private Outline outline;

    void Start()
    {
        outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }
        outline.enabled = false;

        if (closeUpUICanvas != null)
        {
            closeUpUICanvas.gameObject.SetActive(false);
        }
    }

    public void ToggleCloseUpUI(bool isActive)
    {
        if (closeUpUICanvas != null)
        {
            closeUpUICanvas.gameObject.SetActive(isActive);
        }
    }

    private void OnMouseDown()
    {
        if (ExperimentCameraController.IsInCloseUpView && !allowInteractInCloseUp)
        {
            return;
        }

        if (ignoreRotateStandClicks && IsClickingRotateStand()) return;

        if (RotateStandController.IsAnyStandSelected)
        {
            RotateStandController.DeselectAll();
        }

        if (CurrentActiveMover == this)
        {
            Deselect();
            CurrentActiveMover = null;
        }
        else
        {
            if (CurrentActiveMover != null) CurrentActiveMover.Deselect();
            CurrentActiveMover = this;
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
        if (outline != null) outline.enabled = true;
    }

    public void Deselect()
    {
        if (outline != null) outline.enabled = false;
        isMovementLocked = false;
    }

    void Update()
    {
        if (CurrentActiveMover != this) return;
        if (RotateStandController.IsAnyStandSelected) return;
        if (isMovementLocked) return;

        // 🎯 核心拦截：如果 canMove 是 false，直接结束 Update，不再检测键盘输入
        if (!canMove) return;

        float moveDirection = 0f;
        if (Input.GetKey(KeyCode.A)) moveDirection = -1f;
        else if (Input.GetKey(KeyCode.D)) moveDirection = 1f;

        if (moveDirection != 0f) MoveObject(moveDirection);
    }

    void MoveObject(float direction)
    {
        Vector3 currentPos = transform.localPosition;
        float newVal = 0f;

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
