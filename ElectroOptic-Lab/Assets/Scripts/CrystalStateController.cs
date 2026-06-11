using UnityEngine;
using UnityEngine.EventSystems;
using ElectroOptics.WebStreaming;

/// <summary>
/// 晶体状态控制器 (纯净单击变色版)
/// 仅保留单击切换颜色（绿/原色），移除了双击、轮廓光和接收器依赖
/// </summary>
public class CrystalStateController : MonoBehaviour
{
    [Header("高亮设置")]
    public Color selectedColor = Color.green; // 选中时的颜色
    private Color originalColor;
    private Renderer objRenderer;

    // --- 对外公开的状态变量 ---
    public bool IsSelected { get; private set; } = false;

    void Start()
    {
        // 获取渲染器并记录模型初始的颜色
        objRenderer = GetComponent<Renderer>();
        if (objRenderer != null)
        {
            originalColor = objRenderer.material.color;
        }
    }

    void Update()
    {
        // 纯粹的单击检测
        if (RemoteInputRelay.GetMouseButtonDown(0))
        {
            // 防止点到 UI 上触发误操作
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(RemoteInputRelay.MousePosition);

            // 射线穿透检测
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, Physics.AllLayers))
            {
                // 如果射线打中了自己的碰撞体或子物体的碰撞体
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    // 直接执行：选中 <-> 取消选中 的状态切换
                    ToggleSelection();
                }
            }
        }
    }

    // 切换选中状态
    public void ToggleSelection()
    {
        IsSelected = !IsSelected; // 核心：如果是 true 就变成 false，反之亦然
        UpdateVisuals();
        Debug.Log(IsSelected ? "晶体已选中 (变绿)" : "晶体已取消选中 (恢复原色)");
    }

    // 强制取消选中 (供外部其他脚本调用，比如做完实验后重置)
    public void Deselect()
    {
        if (IsSelected)
        {
            IsSelected = false;
            UpdateVisuals();
        }
    }

    // 更新视觉表现：通过直接修改材质颜色
    private void UpdateVisuals()
    {
        if (objRenderer != null)
        {
            objRenderer.material.color = IsSelected ? selectedColor : originalColor;
        }
    }
}
