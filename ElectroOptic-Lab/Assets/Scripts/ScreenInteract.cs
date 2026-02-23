using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ScreenInteract : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("拖入展示锥光图的UI面板")]
    public GameObject conoscopeUIPanel;

    private float _lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;

    private void OnMouseDown()
    {
        float timeSinceLastClick = Time.time - _lastClickTime;
        if (timeSinceLastClick <= DOUBLE_CLICK_THRESHOLD)
        {
            // 1. 验证前置条件：晶体是否已经被选中？
            if (CrystalInteract.Instance != null && CrystalInteract.Instance.isSelected)
            {
                if (conoscopeUIPanel != null)
                {
                    // 2. 切换 UI 开关状态
                    bool isCurrentlyOpen = conoscopeUIPanel.activeSelf;
                    bool newState = !isCurrentlyOpen;
                    conoscopeUIPanel.SetActive(newState);

                    // 3. 跨脚本通信：把 UI 的最新状态告诉晶体，解锁或锁定 WASD 操作
                    CrystalInteract.Instance.isUIOpen = newState;

                    if (newState)
                    {
                        Debug.Log("【实验系统】观察面板已打开。请使用 WASD 调节晶体角度，观察直到锥光干涉图中心回到光屏正中间！");
                    }
                    else
                    {
                        Debug.Log("【实验系统】观察面板已关闭（调节完成）。");
                    }
                }
            }
            else
            {
                Debug.LogWarning("【实验系统】提示：请先双击场景中的【晶体】将其选中！");
            }
        }
        _lastClickTime = Time.time;
    }
}