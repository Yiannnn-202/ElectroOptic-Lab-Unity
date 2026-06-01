using UnityEngine;

/// <summary>
/// 光功率计读数控制 (方案 A：完全独立版)
/// 仅负责接收器的读数和UI，不再干涉晶体的选中状态
/// </summary>
public class PowerReadoutController : MonoBehaviour, IPowerReadoutSource
{
    [Header("关联设置")]
    // 删除了 CrystalStateController 的引用，彻底解绑
    public ReceiverStateController receiverController;
    [Tooltip("可选：实现 IVoltageSource 的组件。为空时会自动查找 RecordManager 等电压源。")]
    public MonoBehaviour voltageSourceBehaviour;

    [Header("接收器调节参数")]
    public float maxPowerReceiver = 2300f;
    private float receiverDevX;
    private float receiverDevY;

    [Header("通用物理参数")]
    public float adjustSpeed = 0.2f;
    public float beamFocus = 20.0f;
    public float initialDeviationRange = 0.15f;

    [Header("光功率读数参数")]
    public float darkPower = 0.2f;
    public float leakage = 0.49f;
    public float visibility = 0.51f;
    public float phaseOffset = 0f;
    public float fallbackHalfWaveVoltage = 150f;

    [Header("面板噪声")]
    public bool noiseEnabled = true;
    public float noiseAmplitude = 0.2f;
    public float noiseFrequency = 5f;

    [Header("UI 设置")]
    public Vector2 windowSize = new Vector2(320, 200);

    private bool showWindow = false;
    private float currentPower;
    private int currentMode = -1;
    private IVoltageSource voltageSource;

    public float CurrentStablePower { get; private set; }
    public float CurrentDisplayPower { get; private set; }
    public float CurrentAlignmentEfficiency { get; private set; }

    void Start()
    {
        ApplyParameterFallbacks();
        if (receiverController == null) receiverController = FindObjectOfType<ReceiverStateController>();
        voltageSource = ResolveVoltageSource();

        // 初始化接收器光路偏差
        receiverDevX = Random.Range(-initialDeviationRange, initialDeviationRange);
        receiverDevY = Random.Range(-initialDeviationRange, initialDeviationRange);
    }

    void Update()
    {
        int currentRecState = (receiverController != null) ? receiverController.CurrentState : 0;

        // 窗口是否显示，现在 100% 只看接收器的脸色，跟晶体毫无关系
        showWindow = (currentRecState == 1 || currentRecState == 2);

        if (showWindow)
        {
            if (currentRecState == 2)
            {
                currentMode = 0; // 进入接收器微调模式
                HandleVirtualAdjustment(ref receiverDevX, ref receiverDevY);
            }
            else
            {
                currentMode = -1; // 仅监控模式，只能看不能调
            }

            CalculatePower();
        }
        else
        {
            currentMode = -1;
            ResetPowerReadout();
        }
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
        if (voltageSource == null)
        {
            voltageSource = ResolveVoltageSource();
        }

        PowerReadoutParameters parameters = BuildReadoutParameters();
        PowerNoiseParameters noise = BuildNoiseParameters();
        float voltage = voltageSource != null ? voltageSource.CurrentVoltage : 0f;
        parameters.halfWaveVoltage = voltageSource != null ? voltageSource.HalfWaveVoltage : fallbackHalfWaveVoltage;

        PowerReadoutResult result = PowerReadoutCalculator.Calculate(
            voltage,
            receiverDevX,
            receiverDevY,
            parameters,
            noise,
            Time.time);

        CurrentStablePower = result.stablePower;
        CurrentDisplayPower = result.displayPower;
        CurrentAlignmentEfficiency = result.alignmentEfficiency;
        currentPower = CurrentDisplayPower;
    }

    private PowerReadoutParameters BuildReadoutParameters()
    {
        ApplyParameterFallbacks();
        return new PowerReadoutParameters
        {
            darkPower = darkPower,
            powerScale = maxPowerReceiver,
            leakage = leakage,
            visibility = visibility,
            phaseOffset = phaseOffset,
            halfWaveVoltage = fallbackHalfWaveVoltage,
            beamFocus = beamFocus
        };
    }

    private PowerNoiseParameters BuildNoiseParameters()
    {
        ApplyParameterFallbacks();
        return new PowerNoiseParameters
        {
            enabled = noiseEnabled,
            amplitude = noiseAmplitude,
            frequency = noiseFrequency
        };
    }

    private void ResetPowerReadout()
    {
        CurrentStablePower = 0f;
        CurrentDisplayPower = 0f;
        CurrentAlignmentEfficiency = 0f;
        currentPower = 0f;
    }

    private IVoltageSource ResolveVoltageSource()
    {
        if (voltageSourceBehaviour is IVoltageSource configuredSource)
        {
            return configuredSource;
        }

        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == this) continue;
            if (behaviour is IVoltageSource source)
            {
                voltageSourceBehaviour = behaviour;
                return source;
            }
        }

        return null;
    }

    private void ApplyParameterFallbacks()
    {
        if (maxPowerReceiver <= 0f) maxPowerReceiver = 198.5f;
        if (fallbackHalfWaveVoltage <= 0f) fallbackHalfWaveVoltage = 150f;
        if (beamFocus < 0f) beamFocus = 0f;
        if (visibility < 0f) visibility = 0f;
        if (leakage < 0f) leakage = 0f;
        if (noiseAmplitude < 0f) noiseAmplitude = 0f;
        if (noiseFrequency <= 0f) noiseFrequency = 5f;
    }

    void OnGUI()
    {
        if (!showWindow) return;

        Rect rect = new Rect(Screen.width / 2 - windowSize.x / 2, Screen.height / 2 - windowSize.y / 2, windowSize.x, windowSize.y);
        GUI.Box(rect, "光功率计读数");

        float closeBtnSize = 25f;
        if (GUI.Button(new Rect(rect.x + rect.width - closeBtnSize - 5, rect.y + 5, closeBtnSize, closeBtnSize), "X"))
        {
            if (receiverController != null) receiverController.ResetState();
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
        else
        {
            tipStyle.normal.textColor = Color.blue;
            GUILayout.Label("【监控模式开启】\n请单击接收器以指派控制权", tipStyle);
        }

        GUILayout.EndArea();
    }
}
