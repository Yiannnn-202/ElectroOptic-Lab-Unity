using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestionItemUI : MonoBehaviour
{
    [Header("UI �ı����")]
    public TMP_Text questionText;
    public TMP_Text optionAText, optionBText, optionCText, optionDText;
    public TMP_Text explanationText;

    [Header("����ͼ")]
    public Image bgA; public Image bgB; public Image bgC; public Image bgD;

    [Header("��ť���")]
    public Button btnA; public Button btnB; public Button btnC; public Button btnD;

    [Header("��ɫ����")]
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

    // ====== �޸������������ȷ�𰸵����� ======
    public void RevealExplanation(string expText, int correctIdx)
    {
        // �����ĸ���ť
        btnA.interactable = false; btnB.interactable = false;
        btnC.interactable = false; btnD.interactable = false;

        // �ж��Դ�
        bool isCorrect = (currentSelected == correctIdx);

        // ���� ABCD
        string[] letters = { "A", "B", "C", "D" };
        string correctLetter = letters[correctIdx];
        string userLetter = (currentSelected == -1) ? "δ����" : letters[currentSelected];

        // ƴ��ȫ�µ���ʾ�ı�
        string resultText = isCorrect
            ? $"<color=#00AA00>���ش���ȷ��</color>  <color=#333333>��ȷ�𰸣�{correctLetter}   ��Ĵ𰸣�{userLetter}</color>\n"
            : $"<color=#FF0000>���ش����</color>  <color=#333333>��ȷ�𰸣�{correctLetter}   ��Ĵ𰸣�{userLetter}</color>\n";

        explanationText.text = resultText + "<b>������</b>" + expText;
        explanationText.gameObject.SetActive(true);
    }
}
