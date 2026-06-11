using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.SceneManagement;
using ElectroOptics.WebStreaming;

public class RecordManager : MonoBehaviour, IVoltageSource
{
    [Header("UI 引用（左侧仪器）")]
    public TextMeshProUGUI voltageText;
    public TextMeshProUGUI receiverText;
    [Tooltip("可选：实现 IPowerReadoutSource 的组件。为空时会自动查找光功率计读数源。")]
    public MonoBehaviour powerReadoutSourceBehaviour;

    [Header("UI 引用（按钮）")]
    public Button recordButton;
    public Button clearButton;

    [Header("UI 引用（右侧大表）")]
    public Transform tableArea;

    [Header("UI 引用（旋钮控制箭头）")]
    public Button arrowIncButton;
    public Button arrowDecButton;

    [Tooltip("箭头身上的 Outline 组件，用于模拟选中发光")]
    public UnityEngine.UI.Outline arrowIncOutline;
    public UnityEngine.UI.Outline arrowDecOutline;

    [Header("实验物理参数")]
    public float currentVoltage = 0.0f;
    [Tooltip("每秒改变的电压值 (V/s)，控制连转速度")]
    public float voltageChangeSpeed = 20.0f;
    public float halfWaveVoltage = 150.0f;
    [Tooltip("旧字段保留以兼容既有场景；新光功率读数使用 powerScale。")]
    public float maxIntensity = 100.0f;

    [Header("光功率读数参数")]
    public float darkPower = 0.2f;
    public float powerScale = 198.5f;
    public float leakage = 0.01f;
    public float visibility = 0.99f;
    public float phaseOffset = 0f;
    public float beamFocus = 20.0f;

    [Header("真实感噪声模拟")]
    [Tooltip("模拟环境光和仪器探测器的随机跳动幅度 (μW)")]
    public float noiseAmplitude = 1.5f;
    [Tooltip("读数跳动的频率")]
    public float noiseFrequency = 5.0f;

    [Header("教学引导（幽灵提示）设置")]
    [Tooltip("提示电压的步进值。从0开始，每个格子增加这个数值")]
    public float suggestedVoltageStep = 15f; // ✨ 修改点：步进值已由 30f 改为 15f

    [Tooltip("提示文字的颜色（灰色）")]
    public Color placeholderColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    [Tooltip("真实记录数据的文字颜色（深色）")]
    public Color normalTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    [Header("运行设置")]
    public bool clearTableOnStart = true;

    public List<TextMeshProUGUI> voltageCells = new List<TextMeshProUGUI>();
    public List<TextMeshProUGUI> powerCells = new List<TextMeshProUGUI>();

    private int currentIndex = 0;

    private bool isIncSelected = false;
    private bool isDecSelected = false;
    private bool isMouseHolding = false;
    private IPowerReadoutSource powerReadoutSource;

    // 缓存 UpdateInstrumentUI() 最近一次显示的功率值，
    // 供 RecordData() 使用，避免按下按钮时重新读取已漂移的 CurrentDisplayPower
    private float lastDisplayedPower;

    public float CurrentVoltage => currentVoltage;
    public float HalfWaveVoltage => halfWaveVoltage;

    void Start()
    {
        ApplyParameterFallbacks();
        BuildCellLists();
        powerReadoutSource = ResolvePowerReadoutSource();

        if (recordButton != null)
        {
            recordButton.onClick.RemoveListener(RecordData);
            recordButton.onClick.AddListener(RecordData);
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveListener(ClearTable);
            clearButton.onClick.AddListener(ClearTable);
        }

        if (arrowIncButton != null) BindEvent(arrowIncButton.gameObject, true);
        if (arrowDecButton != null) BindEvent(arrowDecButton.gameObject, false);

        UpdateArrowUI();

        if (clearTableOnStart)
        {
            ClearTable();
        }

        UpdateInstrumentUI();
    }

    void Update()
    {
        // Only process input when this component is in the active scene.
        // Prevents background-scene components from responding to global input.
        if (gameObject.scene != SceneManager.GetActiveScene())
            return;

        if (isMouseHolding || RemoteInputRelay.GetKey(KeyCode.R))
        {
            float dir = 0f;
            if (isIncSelected) dir = 1f;
            else if (isDecSelected) dir = -1f;

            if (dir != 0f)
            {
                currentVoltage += dir * voltageChangeSpeed * Time.deltaTime;
                UpdateInstrumentUI();
            }
        }

        if (RemoteInputRelay.GetKeyDown(KeyCode.Backspace))
        {
            DeleteLastRecord();
        }
    }

    private void BindEvent(GameObject btn, bool isInc)
    {
        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.AddComponent<EventTrigger>();

        var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener((_) => {
            if (isInc)
            {
                isIncSelected = true;
                isDecSelected = false;
            }
            else
            {
                isDecSelected = true;
                isIncSelected = false;
            }
            isMouseHolding = true;
            UpdateArrowUI();
        });
        trigger.triggers.Add(pointerDown);

        var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener((_) => {
            isMouseHolding = false;
        });
        trigger.triggers.Add(pointerUp);
    }

    private void UpdateArrowUI()
    {
        if (arrowIncOutline != null) arrowIncOutline.enabled = isIncSelected;
        if (arrowDecOutline != null) arrowDecOutline.enabled = isDecSelected;
    }

    // ================= 光功率计算 =================
    float CalculateReceiverValue(float voltage)
    {
        if (powerReadoutSource == null)
        {
            powerReadoutSource = ResolvePowerReadoutSource();
        }

        if (powerReadoutSource != null)
        {
            // 光功率计已存在：直接同步其面板读数，保证记录值与显示值一致
            return powerReadoutSource.CurrentDisplayPower;
        }

        // 无光功率计时的后备计算（含自身噪声模拟）
        PowerReadoutParameters parameters = BuildReadoutParameters();

        PowerNoiseParameters noise = new PowerNoiseParameters
        {
            enabled = true,
            amplitude = noiseAmplitude,
            frequency = noiseFrequency
        };

        PowerReadoutResult result = PowerReadoutCalculator.Calculate(
            voltage,
            0f,
            0f,
            parameters,
            noise,
            Time.time);

        return Mathf.Max(0f, result.displayPower);
    }

    void UpdateInstrumentUI()
    {
        float receiverValue = CalculateReceiverValue(currentVoltage);

        if (voltageText != null)
            voltageText.text = currentVoltage.ToString("F1");

        if (receiverText != null)
            receiverText.text = $"{receiverValue / 1000f:F2}";

        // 缓存当前显示值，供 RecordData() 使用——保证记录值与用户看到的完全一致
        lastDisplayedPower = receiverValue;
    }

    void BuildCellLists()
    {
        voltageCells.Clear();
        powerCells.Clear();

        if (tableArea == null) return;

        foreach (Transform block in tableArea)
        {
            Transform rowU = block.Find("Row_U");
            Transform rowP = block.Find("Row_P");

            CollectCellsFromRow(rowU, voltageCells);
            CollectCellsFromRow(rowP, powerCells);
        }
    }

    void CollectCellsFromRow(Transform row, List<TextMeshProUGUI> targetList)
    {
        if (row == null) return;

        foreach (Transform child in row)
        {
            if (!child.name.StartsWith("Cell_")) continue;

            TextMeshProUGUI txt = child.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt != null)
            {
                targetList.Add(txt);
            }
        }
    }

    public void RecordData()
    {
        int maxRecordCount = Mathf.Min(voltageCells.Count, powerCells.Count);

        if (maxRecordCount == 0 || currentIndex >= maxRecordCount) return;

        // 直接使用仪器面板上最近显示的功率值，保证记录的就是用户看到的
        float currentReceiverValue = lastDisplayedPower;

        voltageCells[currentIndex].color = normalTextColor;
        powerCells[currentIndex].color = normalTextColor;

        voltageCells[currentIndex].text = currentVoltage.ToString("F1");
        powerCells[currentIndex].text = (currentReceiverValue / 1000f).ToString("F2");

        currentIndex++;
    }

    public void ClearTable()
    {
        int maxRecordCount = Mathf.Min(voltageCells.Count, powerCells.Count);
        for (int i = 0; i < maxRecordCount; i++)
        {
            ResetCellToPlaceholder(i);
        }
        currentIndex = 0;
    }

    public void DeleteLastRecord()
    {
        if (currentIndex <= 0) return;

        currentIndex--;
        ResetCellToPlaceholder(currentIndex);
    }

    // ✨ 核心修改点：彻底精简填表逻辑，按顺序递增步长填充每一个格子
    private void ResetCellToPlaceholder(int index)
    {
        if (index < voltageCells.Count && voltageCells[index] != null)
        {
            voltageCells[index].color = placeholderColor;
            voltageCells[index].text = $"({index * suggestedVoltageStep})";
        }

        if (index < powerCells.Count && powerCells[index] != null)
        {
            powerCells[index].color = placeholderColor;
            powerCells[index].text = "--";
        }
    }

    private PowerReadoutParameters BuildReadoutParameters()
    {
        ApplyParameterFallbacks();
        return new PowerReadoutParameters
        {
            darkPower = darkPower,
            powerScale = powerScale,
            leakage = leakage,
            visibility = visibility,
            phaseOffset = phaseOffset,
            halfWaveVoltage = halfWaveVoltage,
            beamFocus = beamFocus
        };
    }

    private IPowerReadoutSource ResolvePowerReadoutSource()
    {
        if (powerReadoutSourceBehaviour is IPowerReadoutSource configuredSource)
        {
            return configuredSource;
        }

        // Only search the active scene — additive mode keeps other scenes loaded in background.
        // FindObjectsOfType would pick up disabled PowerReadoutController from a suspended Scene2,
        // whose CurrentDisplayPower is always 0, zeroing out the power meter.
        Scene activeScene = SceneManager.GetActiveScene();
        foreach (GameObject rootObj in activeScene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in rootObj.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour == this) continue;
                if (behaviour is IPowerReadoutSource source)
                {
                    powerReadoutSourceBehaviour = behaviour;
                    return source;
                }
            }
        }

        return null;
    }

    private void ApplyParameterFallbacks()
    {
        if (halfWaveVoltage <= 0f) halfWaveVoltage = 150f;
        if (powerScale <= 0f) powerScale = 198.5f;
        if (beamFocus < 0f) beamFocus = 0f;
        if (visibility < 0f) visibility = 0f;
        if (leakage < 0f) leakage = 0f;
    }
}
