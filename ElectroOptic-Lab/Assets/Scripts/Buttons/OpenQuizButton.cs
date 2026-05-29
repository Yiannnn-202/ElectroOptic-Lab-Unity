using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 打开课后习题网页。挂载到 Scene2 的按钮上，
/// 点击后用系统默认浏览器打开 quiz.html。
/// </summary>
public class OpenQuizButton : MonoBehaviour
{
    public void OpenQuiz()
    {
        string quizPath = Path.Combine(
            Application.streamingAssetsPath,
            "QuizWeb",
            "quiz.html"
        );

        if (!File.Exists(quizPath))
        {
            Debug.LogError("课后习题文件不存在：" + quizPath);
            return;
        }

        string quizUrl = new Uri(quizPath).AbsoluteUri;

        Debug.Log("正在打开课后习题：" + quizUrl);
        Application.OpenURL(quizUrl);
    }
}
