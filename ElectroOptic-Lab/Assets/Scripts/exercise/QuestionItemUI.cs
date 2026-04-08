using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestionItemUI : MonoBehaviour
{
    [Header("UI 文本组件")]
    public TMP_Text questionText;
    public TMP_Text optionAText, optionBText, optionCText, optionDText;
    public TMP_Text explanationText;

    [Header("背景图")]
    public Image bgA; public Image bgB; public Image bgC; public Image bgD;

    [Header("按钮组件")]
    public Button btnA; public Button btnB; public Button btnC; public Button btnD;

    [Header("颜色配置")]
    public Color normalColor = new Color(0.95f, 0.95f, 0.95f);
    public Color selectedColor = new Color(0.6f, 0.8f, 1f);

    [HideInInspector] public int currentSelected = -1;

    public System.Action onAnswerSelected;

    public void SetupQuestion(string q, string a, string b, string c, string d)
    {
        questionText.text = q;
        optionAText.text = a; optionBText.text = b;
        optionCText.text = c; optionDText.text = d;

        explanationText.gameObject.SetActive(false);

        btnA.onClick.AddListener(() => OnClicked(0));
        btnB.onClick.AddListener(() => OnClicked(1));
        btnC.onClick.AddListener(() => OnClicked(2));
        btnD.onClick.AddListener(() => OnClicked(3));
    }

    void OnClicked(int i)
    {
        currentSelected = i;
        Refresh();
        onAnswerSelected?.Invoke();
    }

    void Refresh()
    {
        if (bgA) bgA.color = (currentSelected == 0) ? selectedColor : normalColor;
        if (bgB) bgB.color = (currentSelected == 1) ? selectedColor : normalColor;
        if (bgC) bgC.color = (currentSelected == 2) ? selectedColor : normalColor;
        if (bgD) bgD.color = (currentSelected == 3) ? selectedColor : normalColor;
    }

    // ====== 修改了这里：传入正确答案的索引 ======
    public void RevealExplanation(string expText, int correctIdx)
    {
        // 锁死四个按钮
        btnA.interactable = false; btnB.interactable = false;
        btnC.interactable = false; btnD.interactable = false;

        // 判定对错
        bool isCorrect = (currentSelected == correctIdx);

        // 翻译 ABCD
        string[] letters = { "A", "B", "C", "D" };
        string correctLetter = letters[correctIdx];
        string userLetter = (currentSelected == -1) ? "未作答" : letters[currentSelected];

        // 拼接全新的提示文本
        string resultText = isCorrect
            ? $"<color=#00AA00>【回答正确】</color>  <color=#333333>正确答案：{correctLetter}   你的答案：{userLetter}</color>\n"
            : $"<color=#FF0000>【回答错误】</color>  <color=#333333>正确答案：{correctLetter}   你的答案：{userLetter}</color>\n";

        explanationText.text = resultText + "<b>解析：</b>" + expText;
        explanationText.gameObject.SetActive(true);
    }
}
