using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecordManager : MonoBehaviour
{
    [Header("UI 引用")]
    public TextMeshProUGUI readingText;
    public Button recordButton;

    [Header("固定表格设置")]
    // 用一个数组来存放你提前摆好的所有空白格子
    public TextMeshProUGUI[] tableSlots;

    // 这个变量就像一个游标，记录当前该填第几个格子了
    private int currentIndex = 0;

    void Start()
    {
        Debug.Log("开始运行");
        recordButton.onClick.AddListener(RecordData);
    }

    void RecordData()
    {
        // 检查表格是不是已经填满了？
        if (currentIndex >= tableSlots.Length)
        {
            Debug.Log("表格已经满了！无法再记录。");
            return; // 满了就直接停止运行后面的代码
        }

        Debug.Log("开始记录");

        // 1. 把左侧的读数，填入当前的空格子里
        tableSlots[currentIndex].text = readingText.text;

        // 2. 游标往下走一格，准备迎接下一次记录
        currentIndex++;

        Debug.Log("成功记录数据到了第 " + currentIndex + " 行");
    }
}