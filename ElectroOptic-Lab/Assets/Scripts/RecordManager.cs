using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecordManager : MonoBehaviour
{
    [Header("UI 引用（左侧仪器）")]
    public TextMeshProUGUI voltageText;
    public TextMeshProUGUI receiverText;

    [Header("UI 引用（按钮）")]
    public Button recordButton;
    public Button clearButton;   // 没有就先留空

    [Header("UI 引用（右侧大表）")]
    public Transform tableArea;
    // 要求层级结构：
    // TableArea
    // ├── TableBlock_01
    // │   ├── Row_U
    // │   │   ├── Label_U
    // │   │   ├── Cell_U_01
    // │   │   ├── Cell_U_02 ...
    // │   └── Row_P
    // │       ├── Label_P
    // │       ├── Cell_P_01
    // │       ├── Cell_P_02 ...
    // ├── TableBlock_02 ...
    //
    // 每个 Cell 里面要有一个 TextMeshProUGUI 子物体
    // Cell 名字建议都以 "Cell_" 开头

    [Header("实验物理参数")]
    public float currentVoltage = 0.0f;
    public float voltageStep = 10.0f;
    public float halfWaveVoltage = 150.0f;
    public float maxIntensity = 100.0f;

    [Header("运行设置")]
    public bool clearTableOnStart = true;

    private readonly List<TextMeshProUGUI> voltageCells = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> powerCells = new List<TextMeshProUGUI>();

    private int currentIndex = 0;

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

        if (clearTableOnStart)
        {
            ClearTable();
        }

        UpdateInstrumentUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            currentVoltage -= voltageStep;
            UpdateInstrumentUI();
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            currentVoltage += voltageStep;
            UpdateInstrumentUI();
        }
        else if (Input.GetKeyDown(KeyCode.Backspace))
        {
            DeleteLastRecord();
        }
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

        // 这里我先不加单位，因为你的仪器面板图上已经有 V 等标识了
        // 如果你想显示单位，把下面改成：
        // currentVoltage.ToString("F1") + " V"
        if (voltageText != null)
            voltageText.text = currentVoltage.ToString("F1");

        if (receiverText != null)
            receiverText.text = receiverValue.ToString("F2");
    }

    void BuildCellLists()
    {
        voltageCells.Clear();
        powerCells.Clear();

        if (tableArea == null)
        {
            Debug.LogWarning("RecordManager: tableArea 没有绑定！");
            return;
        }

        foreach (Transform block in tableArea)
        {
            Transform rowU = block.Find("Row_U");
            Transform rowP = block.Find("Row_P");

            CollectCellsFromRow(rowU, voltageCells);
            CollectCellsFromRow(rowP, powerCells);
        }

        Debug.Log($"RecordManager: 已收集电压格 {voltageCells.Count} 个，光功率格 {powerCells.Count} 个");
    }

    void CollectCellsFromRow(Transform row, List<TextMeshProUGUI> targetList)
    {
        if (row == null) return;

        foreach (Transform child in row)
        {
            // 只收集名字以 Cell_ 开头的格子
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

        if (maxRecordCount == 0)
        {
            Debug.LogWarning("RecordManager: 没有找到可写入的新表格单元格，请检查 tableArea 和层级命名！");
            return;
        }

        if (currentIndex >= maxRecordCount)
        {
            Debug.Log("表格已经写满了！");
            return;
        }

        float currentReceiverValue = CalculateReceiverValue(currentVoltage);

        // 表格里只写数值，不再写单位
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
        Debug.Log("表格已清空。");
    }

    // 如果你后面调整了表格层级，可以在运行前手动调用这个重建列表
    [ContextMenu("Rebuild Table Cell Lists")]
    public void RebuildTableCellLists()
    {
        BuildCellLists();
        Debug.Log("已重新扫描表格单元格。");
    }
    public void DeleteLastRecord()
    {
        if (currentIndex <= 0)
        {
            Debug.Log("没有可删除的数据。");
            return;
        }

        // currentIndex 指向“下一个要写入的位置”
        // 所以删除时要先回退 1，再清空这一格
        currentIndex--;

        if (currentIndex < voltageCells.Count && voltageCells[currentIndex] != null)
        {
            voltageCells[currentIndex].text = "";
        }

        if (currentIndex < powerCells.Count && powerCells[currentIndex] != null)
        {
            powerCells[currentIndex].text = "";
        }

        Debug.Log($"已删除第 {currentIndex + 1} 组记录。");
    }
}