using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 引入 UI 事件系统

/// <summary>
/// UI 旋钮开关控制器 (极简版)
/// 功能：鼠标单击 UI 切换 Outline 发光状态
/// </summary>
public class KnobToggleController : MonoBehaviour, IPointerClickHandler
{
    private Outline outline;
    public bool IsSelected { get; private set; } = false;

    void Start()
    {
        // 完美复用你的逻辑：自动获取或添加 Outline 组件
        outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }

        // 游戏开始时，默认关闭轮廓光晕
        outline.enabled = false;
    }

    // 【核心区别】：UI 不需要写射线！只要挂了这个接口，Unity 会自动在鼠标点击时调用这个方法
    public void OnPointerClick(PointerEventData eventData)
    {
        ToggleSelection();
    }

    public void ToggleSelection()
    {
        IsSelected = !IsSelected;
        UpdateVisuals();
        Debug.Log(IsSelected ? "旋钮已选中 (光晕开启)" : "旋钮已取消选中 (光晕关闭)");
    }

    private void UpdateVisuals()
    {
        // 开关 Outline 组件来控制光晕
        if (outline != null)
        {
            outline.enabled = IsSelected;
        }
    }
}

