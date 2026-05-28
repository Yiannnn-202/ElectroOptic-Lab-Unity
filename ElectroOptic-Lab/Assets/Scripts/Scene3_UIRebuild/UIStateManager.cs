using UnityEngine;
using XCharts.Runtime;
using System.Collections.Generic;
using System.Text;
using TMPro;

public class UIStateManager : MonoBehaviour
{
    [Header("UI 面板引用")]
    public GameObject tablePanel;
    public GameObject graphPanel;

    public GameObject leftControlPanel;
    public GameObject analysisPanel;
    public TextMeshProUGUI analysisText;

    [Header("画图数据引用")]
    public RecordManager recordManager;
    public LineChart lineChart;

    private int localFitRange = 2;

    private float lastCalculatedV = 0f;
    private float lastErrorAbs = 0f;
    private float lastErrorRelative = 0f;
    private string quadraticFormula = "";
    private bool hasValidFit = false;

    void Start()
    {
        if (leftControlPanel != null) leftControlPanel.SetActive(true);
        if (analysisPanel != null) analysisPanel.SetActive(false);

        if (tablePanel != null) tablePanel.SetActive(true);
        if (graphPanel != null) graphPanel.SetActive(false);
    }

    public void SwitchToGraphView()
    {
        if (tablePanel != null) tablePanel.SetActive(false);
        if (graphPanel != null) graphPanel.SetActive(true);

        DrawGraph();
    }

    public void ShowAnalysisReport()
    {
        if (leftControlPanel != null) leftControlPanel.SetActive(false);
        if (analysisPanel != null) analysisPanel.SetActive(true);

        GenerateReportText();
    }

    public void BackToInstruments()
    {
        if (leftControlPanel != null) leftControlPanel.SetActive(true);
        if (analysisPanel != null) analysisPanel.SetActive(false);
    }

    // ================= 优化后的报告生成 =================
    private void GenerateReportText()
    {
        if (analysisText == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<size=120%><color=#005088><b>实验数据处理与误差分析</b></color></size>");
        sb.AppendLine("-----------------------------------------");

        if (!hasValidFit)
        {
            sb.AppendLine("\n<color=red><b>⚠️ 数据不足，无法完成局部极值拟合。</b></color>");
            analysisText.text = sb.ToString();
            return;
        }

        // 精简了文字描述，去掉了最后一句，使得排版更紧凑
        sb.AppendLine($"<color=#333><b>1. 局部二次拟合方程：</b></color>");
        sb.AppendLine($"   {quadraticFormula}");
        sb.AppendLine();
        sb.AppendLine($"<color=#333><b>2. 极值解算 (顶点法)：</b></color>");
        sb.AppendLine($"   公式: V = -b / (2a)");
        sb.AppendLine($"   测量值: <b>V_calc = {lastCalculatedV:F2} V</b>");
        sb.AppendLine();
        sb.AppendLine($"<color=#333><b>3. 误差分析：</b></color>");

        float trueV = recordManager.halfWaveVoltage;
        sb.AppendLine($"   理论值: V_true = {trueV:F2} V");
        sb.AppendLine($"   绝对误差: ΔV = <b>{lastErrorAbs:F3} V</b>");
        sb.AppendLine($"   相对误差: δ = <b>{lastErrorRelative:F2}%</b>");

        analysisText.text = sb.ToString();
    }

    private void DrawGraph()
    {
        hasValidFit = false;
        if (recordManager == null || lineChart == null) return;
        lineChart.RemoveData();

        List<float> xData = new List<float>();
        List<float> yData = new List<float>();

        for (int i = 0; i < recordManager.voltageCells.Count; i++)
        {
            string vText = recordManager.voltageCells[i].text;
            string pText = recordManager.powerCells[i].text;
            if (float.TryParse(vText, out float voltage) && float.TryParse(pText, out float power))
            {
                xData.Add(voltage);
                yData.Add(power);
            }
        }

        if (xData.Count < 3) return;

        var scatterSerie = lineChart.AddSerie<Scatter>("实验数据");
        scatterSerie.symbol.show = true;
        scatterSerie.symbol.type = SymbolType.Circle;
        scatterSerie.symbol.size = 8f;
        scatterSerie.itemStyle.color = Color.red;
        for (int i = 0; i < xData.Count; i++) lineChart.AddData(0, xData[i], yData[i]);

        var globalLineSerie = lineChart.AddSerie<Line>("全局趋势");
        globalLineSerie.lineType = LineType.Smooth;
        globalLineSerie.symbol.show = false;
        globalLineSerie.lineStyle.color = new Color(0.2f, 0.6f, 1f, 0.5f);
        globalLineSerie.lineStyle.width = 2f;
        for (int i = 0; i < xData.Count; i++) lineChart.AddData(1, xData[i], yData[i]);

        int maxIndex = 0;
        for (int i = 1; i < yData.Count; i++) if (yData[i] > yData[maxIndex]) maxIndex = i;

        int startIndex = Mathf.Max(0, maxIndex - localFitRange);
        int endIndex = Mathf.Min(xData.Count - 1, maxIndex + localFitRange);

        List<float> localX = new List<float>();
        List<float> localY = new List<float>();
        for (int i = startIndex; i <= endIndex; i++)
        {
            localX.Add(xData[i]);
            localY.Add(yData[i]);
        }

        if (localX.Count >= 3)
        {
            float[] coeffs = CalculateQuadraticLeastSquares(localX, localY);
            float a = coeffs[0], b = coeffs[1], c = coeffs[2];

            if (a < 0)
            {
                hasValidFit = true;
                lastCalculatedV = -b / (2f * a);
                lastErrorAbs = Mathf.Abs(lastCalculatedV - recordManager.halfWaveVoltage);
                lastErrorRelative = (lastErrorAbs / recordManager.halfWaveVoltage) * 100f;

                // ================= 优化数学方程的显示格式 =================
                string signB = b < 0 ? "-" : "+";
                string signC = c < 0 ? "-" : "+";
                // 这样就不会出现 "+ (-数字)" 的情况了
                quadraticFormula = $"P = {a:F4}V² {signB} {Mathf.Abs(b):F3}V {signC} {Mathf.Abs(c):F2}";

                var localLineSerie = lineChart.AddSerie<Line>("局部抛物线拟合");
                localLineSerie.lineType = LineType.Smooth;
                localLineSerie.symbol.show = false;
                localLineSerie.lineStyle.color = Color.green;
                localLineSerie.lineStyle.width = 4f;

                float plotMinX = localX[0] - 20f;
                float plotMaxX = localX[localX.Count - 1] + 20f;
                for (float x = plotMinX; x <= plotMaxX; x += 1f)
                {
                    lineChart.AddData(2, x, a * x * x + b * x + c);
                }
            }
        }
    }

    private float[] CalculateQuadraticLeastSquares(List<float> xList, List<float> yList)
    {
        int n = xList.Count;
        double sumX = 0, sumX2 = 0, sumX3 = 0, sumX4 = 0, sumY = 0, sumXY = 0, sumX2Y = 0;
        for (int i = 0; i < n; i++)
        {
            double x = xList[i], y = yList[i], x2 = x * x;
            sumX += x; sumX2 += x2; sumX3 += x2 * x; sumX4 += x2 * x2;
            sumY += y; sumXY += x * y; sumX2Y += x2 * y;
        }
        double m11 = sumX4, m12 = sumX3, m13 = sumX2, m21 = sumX3, m22 = sumX2, m23 = sumX, m31 = sumX2, m32 = sumX, m33 = n;
        double v1 = sumX2Y, v2 = sumXY, v3 = sumY;
        double det = m11 * (m22 * m33 - m23 * m32) - m12 * (m21 * m33 - m23 * m31) + m13 * (m21 * m32 - m22 * m31);
        if (System.Math.Abs(det) < 1e-10) return new float[] { 0, 0, 0 };
        double a = (v1 * (m22 * m33 - m23 * m32) - m12 * (v2 * m33 - m23 * v3) + m13 * (v2 * m32 - m22 * v3)) / det;
        double b = (m11 * (v2 * m33 - m23 * v3) - v1 * (m21 * m33 - m23 * m31) + m13 * (m21 * v3 - v2 * m31)) / det;
        double c = (m11 * (m22 * v3 - v2 * m32) - m12 * (m21 * v3 - v2 * m31) + v1 * (m21 * m32 - m22 * m31)) / det;
        return new float[] { (float)a, (float)b, (float)c };
    }
}
