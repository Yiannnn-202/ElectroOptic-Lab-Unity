using UnityEngine;

/// <summary>
/// 导轨物体移动控制器 (最终版)
/// 功能：支持轴向选择，中文注释，互斥逻辑，集成 QuickOutline，增加特写镜头锚点与UI面板控制
/// </summary>
public class RailObjectMover : MonoBehaviour
{
    public static RailObjectMover CurrentActiveMover { get; private set; }

    public enum MoveAxis { X_Axis, Y_Axis, Z_Axis }

    [Header("轴向设置 (重要！)")]
    public MoveAxis moveAxis = MoveAxis.X_Axis;

    [Header("移动参数")]
    public float moveSpeed = 0.5f;
    public float minLimit = -3.0f;
    public float maxLimit = 3.0f;
    public bool ignoreRotateStandClicks = true;

    [Header("特写镜头与UI设置")]
    [Tooltip("请拖入一个空物体作为特写镜头的位置参考")]
    public Transform closeUpCameraAnchor;

    // 【新增】引用我们悬浮在晶体旁的 UI 面板
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

        // 【新增】游戏刚开始时，默认把特写UI面板隐藏掉
        if (closeUpUICanvas != null)
        {
            closeUpUICanvas.gameObject.SetActive(false);
        }
    }

    // 【新增】提供给相机控制器调用的方法，用于随时开关 UI 面板
    public void ToggleCloseUpUI(bool isActive)
    {
        if (closeUpUICanvas != null)
        {
            closeUpUICanvas.gameObject.SetActive(isActive);
        }
    }

    // 点击事件
    private void OnMouseDown()
    {
        //【核心新增】：如果当前处于全局特写模式，直接拦截点击，什么都不做！
        if (ExperimentCameraController.IsInCloseUpView)
        {
            return;
        }

        // 原本的逻辑保持不变
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