using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 接收器专属状态控制器 (主控终端模式)
/// 0=关机, 1=变蓝(开机/监控), 2=变绿(选中/被调节)
/// </summary>
public class ReceiverStateController : MonoBehaviour
{
    [Header("高亮设置")]
    public Color powerMeterColor = Color.blue;     // 蓝：开启功率计窗口
    public Color selectedColor = Color.green;      // 绿：作为目标被键盘调节
    private Color originalColor;
    private Renderer objRenderer;

    [Header("交互设置")]
    public float doubleClickInterval = 0.3f;
    private float lastClickTime = 0f;

    public int CurrentState { get; private set; } = 0;

    void Start()
    {
        objRenderer = GetComponent<Renderer>();
        if (objRenderer != null) originalColor = objRenderer.material.color;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, Physics.AllLayers))
            {
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    if (Time.time - lastClickTime <= doubleClickInterval)
                    {
                        // 🎯 双击逻辑：0状态时双击开机(变蓝)；其他状态双击直接关机(0)
                        if (CurrentState == 0) SetState(1);
                        else SetState(0);

                        lastClickTime = 0f;
                    }
                    else
                    {
                        // 🎯 单击逻辑：只有在开机状态(>0)时，单击才能在 蓝(1) 和 绿(2) 之间切换
                        lastClickTime = Time.time;
                        if (CurrentState == 1) SetState(2);
                        else if (CurrentState == 2) SetState(1);
                    }
                }
            }
        }
    }

    public void SetState(int newState)
    {
        CurrentState = newState;
        UpdateVisuals();
    }

    public void ResetState()
    {
        SetState(0);
    }

    private void UpdateVisuals()
    {
        if (objRenderer != null)
        {
            if (CurrentState == 1) objRenderer.material.color = powerMeterColor; // 蓝
            else if (CurrentState == 2) objRenderer.material.color = selectedColor; // 绿
            else objRenderer.material.color = originalColor; // 原色
        }
    }
}
