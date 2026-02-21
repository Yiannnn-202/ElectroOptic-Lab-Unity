using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 接收器专属状态控制器 (完美三段式循环版)
/// 0=不亮, 1=变蓝(监视), 2=变绿(调节)
/// </summary>
public class ReceiverStateController : MonoBehaviour
{
    [Header("高亮设置")]
    public Color powerMeterColor = Color.blue;     // 第一次双击：变蓝 (开启功率计)
    public Color selectedColor = Color.green;      // 第二次双击：变绿 (作为目标被调节)
    private Color originalColor;
    private Renderer objRenderer;

    [Header("交互设置")]
    public float doubleClickInterval = 0.3f;
    private float lastClickTime = 0f;

    // --- 核心状态 ---
    // 0 = 未激活, 1 = 蓝色, 2 = 绿色
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
                        // 触发双击，状态按照 0 -> 1 -> 2 -> 0 循环
                        CycleState();
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

    private void CycleState()
    {
        CurrentState++;
        if (CurrentState > 2) CurrentState = 0;
        UpdateVisuals();
    }

    // 供外部调用的强行改状态接口
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
