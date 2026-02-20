using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 光电接收器读数控制 (仅适配晶体调节版)
/// 功能：双击弹窗 + WASD精细调节晶体 + 退出按钮
/// </summary>
public class PowerReadoutController : MonoBehaviour
{
    [Header("关联设置")]
    public CrystalStateController crystalController;

    [Header("物理模拟参数")]
    public float maxPower = 198.5f;
    [Tooltip("调节灵敏度：数值越小，调节越精细")]
    public float adjustSpeed = 0.2f; // 降低了速度，更适合“微调”
    [Tooltip("光束聚焦度：决定了对准的难度")]
    public float beamFocus = 20.0f;

    [Header("初始状态控制")]
    [Tooltip("初始随机偏差范围：设小一点(如0.15)以模拟光已经打在靶面上，只需微调")]
    public float initialDeviationRange = 0.15f;

    [Header("UI 设置")]
    public Vector2 windowSize = new Vector2(320, 200);

    // --- 内部变量 ---
    private bool showWindow = false;
    private float lastClickTime = 0f;
    private float deviationX;
    private float deviationY;
    private float currentPower;

    void Start()
    {
        if (crystalController == null)
            crystalController = FindObjectOfType<CrystalStateController>();

        // 初始偏差限制在很小的范围内
        // 这样初始读数不会是0，而是一个较大的值（比如 100-150 uW），符合“微调”的设定
        deviationX = Random.Range(-initialDeviationRange, initialDeviationRange);
        deviationY = Random.Range(-initialDeviationRange, initialDeviationRange);
    }

    void Update()
    {
        HandleDoubleClick();

        // 仅在晶体被选中时才允许调节
        if (showWindow && crystalController != null && crystalController.IsSelected)
        {
            HandleVirtualAdjustment();
        }

        CalculatePower();
    }

    private void HandleVirtualAdjustment()
    {
        float dt = Time.deltaTime * adjustSpeed;

        // 反转了部分逻辑，符合一般操作直觉（可视情况调整）
        if (Input.GetKey(KeyCode.W)) deviationY += dt;
        if (Input.GetKey(KeyCode.S)) deviationY -= dt;
        if (Input.GetKey(KeyCode.A)) deviationX -= dt;
        if (Input.GetKey(KeyCode.D)) deviationX += dt;

        // 可选：限制偏差不要跑太远，防止玩家调丢了
        deviationX = Mathf.Clamp(deviationX, -0.5f, 0.5f);
        deviationY = Mathf.Clamp(deviationY, -0.5f, 0.5f);
    }

    private void CalculatePower()
    {
        float rSquared = deviationX * deviationX + deviationY * deviationY;
        float noise = (Mathf.PerlinNoise(Time.time * 5f, 0f) - 0.5f) * 0.5f;
        currentPower = maxPower * Mathf.Exp(-beamFocus * rSquared) + noise;
        if (currentPower < 0) currentPower = 0f;
    }

    private void HandleDoubleClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    if (Time.time - lastClickTime <= 0.3f)
                    {
                        showWindow = !showWindow; // 双击也可以开关
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

    // --- 带有关闭按钮的 UI ---
    void OnGUI()
    {
        if (!showWindow) return;

        Rect rect = new Rect(Screen.width / 2 - windowSize.x / 2, Screen.height / 2 - windowSize.y / 2, windowSize.x, windowSize.y);

        // 绘制背景
        GUI.Box(rect, "光功率计读数");

        // === 添加右上角关闭按钮 ===
        float closeBtnSize = 25f;
        if (GUI.Button(new Rect(rect.x + rect.width - closeBtnSize - 5, rect.y + 5, closeBtnSize, closeBtnSize), "X"))
        {
            showWindow = false;
        }

        GUILayout.BeginArea(new Rect(rect.x + 20, rect.y + 30, rect.width - 40, rect.height - 40));

        // 1. 功率显示
        GUIStyle powerStyle = new GUIStyle(GUI.skin.label);
        powerStyle.fontSize = 32;
        powerStyle.alignment = TextAnchor.MiddleCenter;
        powerStyle.fontStyle = FontStyle.Bold;
        powerStyle.normal.textColor = Color.red;

        GUILayout.Label($"{currentPower:F1} μW", powerStyle);

        GUILayout.Space(15);

        // 2. 状态提示
        GUIStyle tipStyle = new GUIStyle(GUI.skin.label);
        tipStyle.fontSize = 13;
        tipStyle.alignment = TextAnchor.MiddleCenter;

        if (crystalController != null && crystalController.IsSelected)
        {
            tipStyle.normal.textColor = Color.green;
            GUILayout.Label("晶体已联机\n按 [WASD] 进行微调", tipStyle);
        }
        else
        {
            tipStyle.normal.textColor = Color.gray;
            GUILayout.Label("晶体未选中\n请双击晶体解锁调节", tipStyle);
        }

        GUILayout.EndArea();
    }
}
