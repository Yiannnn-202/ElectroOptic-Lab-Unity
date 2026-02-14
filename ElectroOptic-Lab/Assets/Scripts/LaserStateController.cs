using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 激光器选中状态控制器
/// 功能：双击高亮，仅作为状态标记，不涉及移动
/// </summary>
public class LaserStateController : MonoBehaviour
{
    [Header("高亮设置")]
    public Color selectedColor = Color.green; // 选中时的颜色
    private Color originalColor;
    private Renderer objRenderer;

    [Header("交互设置")]
    public float doubleClickInterval = 0.3f;

    // --- 对外公开的状态变量 ---
    public bool IsSelected { get; private set; } = false;

    private float lastClickTime = 0f;

    void Start()
    {
        objRenderer = GetComponent<Renderer>();
        if (objRenderer != null) originalColor = objRenderer.material.color;
    }

    void Update()
    {
        // 简单的双击检测逻辑
        if (Input.GetMouseButtonDown(0))
        {
            // 防止UI穿透
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 射线检测是否点击到本物体
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    float timeSinceLastClick = Time.time - lastClickTime;
                    if (timeSinceLastClick <= doubleClickInterval)
                    {
                        // 触发双击
                        ToggleSelection();
                        lastClickTime = 0f;
                    }
                    else
                    {
                        lastClickTime = Time.time;
                    }
                }
            }
        }
    }

    // 切换选中状态
    public void ToggleSelection()
    {
        IsSelected = !IsSelected;
        UpdateVisuals();
        Debug.Log(IsSelected ? "激光器已选中 (可调节)" : "激光器已取消选中");
    }

    // 强制取消选中 (供其他脚本调用)
    public void Deselect()
    {
        IsSelected = false;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (objRenderer != null)
        {
            objRenderer.material.color = IsSelected ? selectedColor : originalColor;
        }
    }
}