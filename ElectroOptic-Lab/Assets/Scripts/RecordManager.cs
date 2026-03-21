using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecordManager : MonoBehaviour
{
    [Header("UI 引用 (左侧仪器)")]
    public TextMeshProUGUI voltageText;
    public TextMeshProUGUI receiverText;

    [Header("UI 引用 (右侧表格)")]
    public Button recordButton;
    public TextMeshProUGUI[] tableSlots;

    [Header("实验物理参数")]
    public float currentVoltage = 0.0f;   // 当前外加电压 (u)
    public float voltageStep = 10.0f;     // 每次按键增减的电压量

    [Space(10)]
    public float halfWaveVoltage = 150.0f; // 半波电压 (u_pi) - 可在面板调节
    public float maxIntensity = 100.0f;    // 最大光强 (I_0) - 可在面板调节

    private int currentIndex = 0;

    void Start()
    {
        recordButton.onClick.AddListener(RecordData);
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
    }

    // 🌟 核心公式：光强度电光调制公式
    float CalculateReceiverValue(float voltage)
    {
        // 公式: I = I_0 * sin^2( (PI * u) / (2 * u_pi) )
        // 1. 计算括号里的相位值
        float phase = (Mathf.PI * voltage) / (2f * halfWaveVoltage);

        // 2. 计算 sin 的平方，再乘以最大光强
        float result = maxIntensity * Mathf.Pow(Mathf.Sin(phase), 2);

        return result;
    }

    void UpdateInstrumentUI()
    {
        voltageText.text = currentVoltage.ToString("F1") + " V";

        float receiverValue = CalculateReceiverValue(currentVoltage);
        // 示数保留两位小数
        receiverText.text = receiverValue.ToString("F2");
    }

    void RecordData()
    {
        if (currentIndex >= tableSlots.Length)
        {
            Debug.Log("表格已经填满了！");
            return;
        }

        float currentReceiverValue = CalculateReceiverValue(currentVoltage);

        string recordString = string.Format("{0:F1} V   |   {1:F2}", currentVoltage, currentReceiverValue);

        tableSlots[currentIndex].text = recordString;
        currentIndex++;
    }
}