using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 必须引入，用于处理鼠标的长按松开事件
using TMPro;

public class RecordManager : MonoBehaviour
{
    [Header("UI 引用（左侧仪器）")]
    public TextMeshProUGUI voltageText;
    public TextMeshProUGUI receiverText;

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
    public float maxIntensity = 100.0f;

    [Header("运行设置")]
    public bool clearTableOnStart = true;

    private readonly List<TextMeshProUGUI> voltageCells = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> powerCells = new List<TextMeshProUGUI>();

    private int currentIndex = 0;

    // --- 状态记录 ---
    private bool isIncSelected = false;  // 顺时针是否处于“发光/选中”状态
    private bool isDecSelected = false;  // 逆时针是否处于“发光/选中”状态
    private bool isMouseHolding = false; // 鼠标是否正在长按着某个箭头

    void Start()
    {
        BuildCellLists();

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

        // --- 核心：绑定“选中”与“长按”双模事件 ---
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
        // --- 混合模式核心逻辑 ---
        // 只要鼠标按住了箭头，或者按住了 R 键，就进行平滑连转
        if (isMouseHolding || Input.GetKey(KeyCode.R))
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

        // 保留退格键删除记录
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            DeleteLastRecord();
        }
    }

    // --- 巧妙整合发光与长按机制 ---
    private void BindEvent(GameObject btn, bool isInc)
    {
        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.AddComponent<EventTrigger>();

        // 鼠标按下：
        // 1. 切换发光状态 2. 告诉系统鼠标按住了（触发鼠标连转）
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

        // 鼠标抬起：
        // 仅仅取消鼠标连转状态，但【不取消】发光状态，以便 R 键能记住方向继续工作
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

    // ---------- 以下为原有物理计算和表格逻辑（完全未修改） ----------

    float CalculateReceiverValue(float voltage)
    {
        float phase = (Mathf.PI * voltage) / (2f * halfWaveVoltage);
        float result = maxIntensity * Mathf.Pow(Mathf.Sin(phase), 2);
        return result;
    }

    void UpdateInstrumentUI()
    {
        float receiverValue = CalculateReceiverValue(currentVoltage);

        if (voltageText != null)
            voltageText.text = currentVoltage.ToString("F1");

        if (receiverText != null)
            receiverText.text = receiverValue.ToString("F2");
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

        float currentReceiverValue = CalculateReceiverValue(currentVoltage);

        voltageCells[currentIndex].text = currentVoltage.ToString("F1");
        powerCells[currentIndex].text = currentReceiverValue.ToString("F2");

        currentIndex++;
    }

    public void ClearTable()
    {
        foreach (var cell in voltageCells)
        {
            if (cell != null) cell.text = "";
        }

        foreach (var cell in powerCells)
        {
            if (cell != null) cell.text = "";
        }

        currentIndex = 0;
    }

    public void DeleteLastRecord()
    {
        if (currentIndex <= 0) return;

        currentIndex--;

        if (currentIndex < voltageCells.Count && voltageCells[currentIndex] != null)
        {
            voltageCells[currentIndex].text = "";
        }

        if (currentIndex < powerCells.Count && powerCells[currentIndex] != null)
        {
            powerCells[currentIndex].text = "";
        }
    }
}
