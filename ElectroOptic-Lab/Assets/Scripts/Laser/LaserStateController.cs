using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 激光器选中状态控制器
/// 功能：双击切换颜色高亮（深蓝色）
/// </summary>
public class LaserStateController : MonoBehaviour
{
    [Header("高亮设置")]
    public Color selectedColor = new Color(0f, 0f, 0.5f, 1f); // 深蓝色
    private Color originalColor;
    private Renderer objRenderer;

    [Header("交互设置")]
    public float doubleClickInterval = 0.3f;

    // --- 对外公开的状态变量 ---
    public bool IsSelected { get; private set; } = false;

    private float lastClickTime = 0f;

    void Start()
    {
        // 自动获取 Renderer，如果父物体没有，就去子物体找
        objRenderer = GetComponent<Renderer>();
        if (objRenderer == null)
        {
            objRenderer = GetComponentInChildren<Renderer>();
        }

        if (objRenderer != null)
        {
            // 记录初始颜色以便恢复
            originalColor = objRenderer.material.color;
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // 使用 RaycastAll 确保能穿透透明物体点中模型
            RaycastHit[] hits = Physics.RaycastAll(ray);
            bool hitThisObject = false;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    hitThisObject = true;
                    break;
                }
            }

            if (hitThisObject)
            {
                float timeSinceLastClick = Time.time - lastClickTime;
                if (timeSinceLastClick <= doubleClickInterval)
                {
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

    public void ToggleSelection()
    {
        IsSelected = !IsSelected;
        UpdateVisuals();
        Debug.Log(IsSelected ? "激光器已选中" : "激光器已取消选中");
    }

    public void Deselect()
    {
        IsSelected = false;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (objRenderer != null)
        {
            // 直接修改材质颜色
            objRenderer.material.color = IsSelected ? selectedColor : originalColor;
        }
    }
}
