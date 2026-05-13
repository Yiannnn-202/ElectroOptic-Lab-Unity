using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

    // ================= 修改点：改成可随意编辑的数组 =================
    [Header("教学引导（幽灵提示）设置")]
    [Tooltip("按行自定义提示电压，专门针对极值法设计（比如填入波峰波谷附近的电压）")]
    public float[] suggestedVoltages = new float[] { 0f, 100f, 260f, 400f, 540f, 600f };
    [Tooltip("提示文字的颜色（灰色）")]
    public Color placeholderColor = new Color(0.6f, 0.6f, 0.6f, 0.8f); // 适中的灰色
    [Tooltip("真实记录数据的文字颜色（深色）")]
    public Color normalTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    // ==========================================================

    [Header("运行设置")]
    public bool clearTableOnStart = true;

    private readonly List<TextMeshProUGUI> voltageCells = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> powerCells = new List<TextMeshProUGUI>();

    private int currentIndex = 0;

    private bool isIncSelected = false;
    private bool isDecSelected = false;
    private bool isMouseHolding = false;

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

        if (Input.GetKeyDown(KeyCode.Backspace))
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

        voltageCells[currentIndex].color = normalTextColor;
        powerCells[currentIndex].color = normalTextColor;

        voltageCells[currentIndex].text = currentVoltage.ToString("F1");
        powerCells[currentIndex].text = currentReceiverValue.ToString("F2");

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

    private void ResetCellToPlaceholder(int index)
    {
        int blocksCount = tableArea.childCount;
        int cellsPerRow = blocksCount > 0 ? voltageCells.Count / blocksCount : 1;

        if (index % cellsPerRow == 0)
        {
            if (index < voltageCells.Count && voltageCells[index] != null)
            {
                int rowIndex = index / cellsPerRow;
                voltageCells[index].color = placeholderColor;

                // ================= 修改点：根据行数从数组里取值 =================
                if (rowIndex < suggestedVoltages.Length)
                {
                    voltageCells[index].text = $"({suggestedVoltages[rowIndex]})";
                }
                else
                {
                    voltageCells[index].text = ""; // 如果行数超过了数组长度，就不显示
                }
            }
            if (index < powerCells.Count && powerCells[index] != null)
            {
                powerCells[index].color = placeholderColor;
                powerCells[index].text = "--";
            }
        }
        else
        {
            if (index < voltageCells.Count && voltageCells[index] != null) voltageCells[index].text = "";
            if (index < powerCells.Count && powerCells[index] != null) powerCells[index].text = "";
        }
    }
}
