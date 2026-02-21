using UnityEngine;

/// <summary>
/// 光功率计读数控制 (极致顺滑：智能状态机防打架版)
/// </summary>
public class PowerReadoutController : MonoBehaviour
{
    [Header("关联设置")]
    public CrystalStateController crystalController;
    public ReceiverStateController receiverController;

    [Header("第一步：接收器调节参数")]
    public float maxPowerReceiver = 198.5f;
    private float receiverDevX;
    private float receiverDevY;

    [Header("第四步：晶体调节参数")]
    public float maxPowerCrystal = 145.0f;
    private float crystalDevX;
    private float crystalDevY;

    [Header("通用物理参数")]
    public float adjustSpeed = 0.2f;
    public float beamFocus = 20.0f;
    public float initialDeviationRange = 0.15f;

    [Header("UI 设置")]
    public Vector2 windowSize = new Vector2(320, 200);

    // --- 内部变量 ---
    private bool showWindow = false;
    private float currentPower;
    private int currentMode = -1; // -1=只监控没选中, 0=调接收器, 1=调晶体

    // 用于记录上一帧的状态，用来判断“谁是新来的”
    private int lastReceiverState = 0;
    private bool lastCrystalState = false;

    void Start()
    {
        if (crystalController == null) crystalController = FindObjectOfType<CrystalStateController>();
        if (receiverController == null) receiverController = FindObjectOfType<ReceiverStateController>();

        receiverDevX = Random.Range(-initialDeviationRange, initialDeviationRange);
        receiverDevY = Random.Range(-initialDeviationRange, initialDeviationRange);

        crystalDevX = Random.Range(-initialDeviationRange, initialDeviationRange);
        crystalDevY = Random.Range(-initialDeviationRange, initialDeviationRange);
    }

    void Update()
    {
        // 1. 获取当前最新状态
        int currentRecState = (receiverController != null) ? receiverController.CurrentState : 0;
        bool currentCrysState = (crystalController != null && crystalController.IsSelected);

        // ==========================================
        // 🎯 智能互斥锁：谁最新变成绿色，就给谁让路
        // ==========================================
        if (currentRecState == 2 && lastReceiverState != 2)
        {
            // 接收器刚刚变成绿色，说明玩家要调接收器了，强行关掉晶体
            if (currentCrysState)
            {
                crystalController.Deselect();
                currentCrysState = false;
            }
        }
        else if (currentCrysState && !lastCrystalState)
        {
            // 晶体刚刚变成绿色，说明玩家要调晶体了
            // 把接收器“降级”为蓝色监控状态 (如果它没亮，也顺便帮它自动开机变蓝)
            if (receiverController != null)
            {
                receiverController.SetState(1);
                currentRecState = 1;
            }
        }

        // 更新历史记录
        lastReceiverState = currentRecState;
        lastCrystalState = currentCrysState;

        // ==========================================
        // 🎯 状态与模式分配
        // ==========================================
        // 只要接收器不是0（即处于蓝色或绿色），就显示窗口
        showWindow = (currentRecState == 1 || currentRecState == 2);

        // 窗口关了，顺便把晶体也关了
        if (!showWindow && currentCrysState)
        {
            crystalController.Deselect();
            currentCrysState = false;
            lastCrystalState = false;
        }

        if (showWindow)
        {
            if (currentRecState == 2)
            {
                currentMode = 0; // 调接收器
                HandleVirtualAdjustment(ref receiverDevX, ref receiverDevY);
            }
            else if (currentCrysState)
            {
                currentMode = 1; // 调晶体
                HandleVirtualAdjustment(ref crystalDevX, ref crystalDevY);
            }
            else
            {
                currentMode = -1; // 只监控，啥也没选中
            }
        }

        CalculatePower();
    }

    private void HandleVirtualAdjustment(ref float devX, ref float devY)
    {
        float dt = Time.deltaTime * adjustSpeed;
        if (Input.GetKey(KeyCode.W)) devY += dt;
        if (Input.GetKey(KeyCode.S)) devY -= dt;
        if (Input.GetKey(KeyCode.A)) devX -= dt;
        if (Input.GetKey(KeyCode.D)) devX += dt;

        devX = Mathf.Clamp(devX, -0.5f, 0.5f);
        devY = Mathf.Clamp(devY, -0.5f, 0.5f);
    }

    private void CalculatePower()
    {
        float noise = (Mathf.PerlinNoise(Time.time * 5f, 0f) - 0.5f) * 0.5f;

        if (currentMode == 0)
        {
            float rSquared = receiverDevX * receiverDevX + receiverDevY * receiverDevY;
            currentPower = maxPowerReceiver * Mathf.Exp(-beamFocus * rSquared) + noise;
        }
        else if (currentMode == 1)
        {
            float rSquared = crystalDevX * crystalDevX + crystalDevY * crystalDevY;
            currentPower = maxPowerCrystal * Mathf.Exp(-beamFocus * rSquared) + noise;
        }
        else
        {
            // 仅仅开着监视器，没有人被调的时候
            currentPower = 0f;
        }

        if (currentPower < 0) currentPower = 0f;
    }

    void OnGUI()
    {
        if (!showWindow) return;

        Rect rect = new Rect(Screen.width / 2 - windowSize.x / 2, Screen.height / 2 - windowSize.y / 2, windowSize.x, windowSize.y);
        GUI.Box(rect, "光功率计读数");

        float closeBtnSize = 25f;
        if (GUI.Button(new Rect(rect.x + rect.width - closeBtnSize - 5, rect.y + 5, closeBtnSize, closeBtnSize), "X"))
        {
            // 点击关闭按钮，一键还原所有状态
            if (receiverController != null) receiverController.ResetState();
            if (crystalController != null) crystalController.Deselect();
            showWindow = false;
        }

        GUILayout.BeginArea(new Rect(rect.x + 20, rect.y + 30, rect.width - 40, rect.height - 40));

        GUIStyle powerStyle = new GUIStyle(GUI.skin.label);
        powerStyle.fontSize = 32;
        powerStyle.alignment = TextAnchor.MiddleCenter;
        powerStyle.fontStyle = FontStyle.Bold;
        powerStyle.normal.textColor = Color.red;

        GUILayout.Label($"{currentPower:F1} μW", powerStyle);
        GUILayout.Space(15);

        GUIStyle tipStyle = new GUIStyle(GUI.skin.label);
        tipStyle.fontSize = 13;
        tipStyle.alignment = TextAnchor.MiddleCenter;

        if (currentMode == 0)
        {
            tipStyle.normal.textColor = Color.green;
            GUILayout.Label("【接收器微调模式】\n按 [WASD] 调节接收器光路", tipStyle);
        }
        else if (currentMode == 1)
        {
            tipStyle.normal.textColor = Color.green;
            GUILayout.Label("【晶体联机模式】\n按 [WASD] 调节晶体偏转角", tipStyle);
        }
        else
        {
            tipStyle.normal.textColor = Color.blue;
            GUILayout.Label("【监控模式开启】\n双击绿色元件进行微调", tipStyle);
        }

        GUILayout.EndArea();
    }
}
