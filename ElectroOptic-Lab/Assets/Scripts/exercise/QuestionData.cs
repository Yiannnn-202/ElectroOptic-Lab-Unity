using System;

[Serializable]
public class QuestionData
{
    public string question;
    public string optA, optB, optC, optD;
    public int correctIndex;
    public string explanation; // 新增：解析文本

    // 构造函数里多加一个 exp 参数
    public QuestionData(string q, string a, string b, string c, string d, int ans, string exp)
    {
        question = q;
        optA = a; optB = b; optC = c; optD = d;
        correctIndex = ans;
        explanation = exp;
    }
}
