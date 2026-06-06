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
            "report_template.html"
        );

        if (!File.Exists(reportPath))
        {
            Debug.LogError("ʵ�鱨���ļ������ڣ�" + reportPath);
            return;
        }

        string reportUrl = new Uri(reportPath).AbsoluteUri;

        Debug.Log("���ڴ�ʵ�鱨�棺" + reportUrl);
        Application.OpenURL(reportUrl);
    }
}
