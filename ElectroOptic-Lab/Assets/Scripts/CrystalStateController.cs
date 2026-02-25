using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 晶体状态控制器 (智能联机模式)
/// 接收器开机时单击变绿；双击留给未来独立操作
/// </summary>
public class CrystalStateController : MonoBehaviour
{
    [Header("高亮设置")]
    public Color selectedColor = Color.green; // 选中时的颜色
    private Color originalColor;
    private Renderer objRenderer;

    [Header("交互设置")]
    public float doubleClickInterval = 0.3f;
    public bool IsSelected { get; private set; } = false;
    private float lastClickTime = 0f;

    // 自动寻找场景里的接收器主控
    private ReceiverStateController receiverController;

    void Start()
    {
        objRenderer = GetComponent<Renderer>();
        if (objRenderer != null) originalColor = objRenderer.material.color;

        receiverController = FindObjectOfType<ReceiverStateController>();
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
                        // 🎯 未来扩展：双击逻辑
                        Debug.Log("🎯 晶体被双击！(完美留给你后续单独调节晶体的实验步骤)");
                        lastClickTime = 0f;
                    }
                    else
                    {
                        // 🎯 单击逻辑：只有当接收器处于开机状态(>0)时，单击晶体才会变绿联机
                        lastClickTime = Time.time;
                        if (receiverController != null && receiverController.CurrentState > 0)
                        {
                            ToggleSelection();
                        }
                        else
                        {
                            Debug.Log("⚠️ 接收器未开机，晶体暂时无法联机微调。");
                        }
                    }
                }
            }
        }
    }

    public void ToggleSelection()
    {
        IsSelected = !IsSelected;
        UpdateVisuals();
    }

    public void Deselect()
    {
        if (IsSelected)
        {
            IsSelected = false;
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        if (objRenderer != null)
        {
            objRenderer.material.color = IsSelected ? selectedColor : originalColor;
        }
    }
}
