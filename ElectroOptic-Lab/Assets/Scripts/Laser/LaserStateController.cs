using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 激光器选中状态控制器
/// 功能：单击切换光晕（Outline）高亮
/// </summary>
public class LaserStateController : MonoBehaviour
{
    // --- 已经移除了颜色替换相关的变量 ---
    private Outline outline;

    // --- 对外公开的状态变量 ---
    public bool IsSelected { get; private set; } = false;

    void Start()
    {
        // 参考刻度盘逻辑：自动获取或添加 Outline 组件
        outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }

        // 游戏开始时，默认关闭轮廓光晕
        outline.enabled = false;
    }

    void Update()
    {
        // 监听鼠标左键单击
        if (Input.GetMouseButtonDown(0))
        {
            // 防 UI 穿透
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
                // 只要点中了，就直接触发切换状态
                ToggleSelection();
            }
        }
    }

    public void ToggleSelection()
    {
        IsSelected = !IsSelected;
        UpdateVisuals();
        Debug.Log(IsSelected ? "激光器已选中 (光晕开启)" : "激光器已取消选中 (光晕关闭)");
    }

    public void Deselect()
    {
        IsSelected = false;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // 核心表现层修改：通过开关 Outline 组件来控制光晕，而不是改材质颜色
        if (outline != null)
        {
            outline.enabled = IsSelected;
        }
    }
}
