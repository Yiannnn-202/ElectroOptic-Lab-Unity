using System;
using System.IO;
using UnityEngine;

public class OpenReportButton : MonoBehaviour
{
    public void OpenReport()
    {
        string reportPath = Path.Combine(
            Application.streamingAssetsPath,
            "ReportWeb",
            "report.html"
        );

        if (!File.Exists(reportPath))
        {
            Debug.LogError("实验报告文件不存在：" + reportPath);
            return;
        }

        string reportUrl = new Uri(reportPath).AbsoluteUri;

        Debug.Log("正在打开实验报告：" + reportUrl);
        Application.OpenURL(reportUrl);
    }
}
