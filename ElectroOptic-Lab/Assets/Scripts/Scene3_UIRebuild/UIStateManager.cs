using UnityEngine;
using XCharts.Runtime;
using System.Collections.Generic;
using System.Text;
using TMPro;
using MathNet.Numerics;

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
    public LineChart residualChart; // 新增：残差图表引用

    // 全局拟合留存的物理量与状态
    private bool hasValidFit = false;
    private string globalFormula = "";

    // 记录拟合曲线本身的理论极值点坐标
    private float fitCurveMaxV = 0f;
    private float fitCurveMinV = 0f;
    private float fitCurveMaxP = 0f;
    private float fitCurveMinP = 0f;

    private float lastCalculatedV = 0f;

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

    public void BackToTable()
    {
        if (tablePanel != null) tablePanel.SetActive(true);
        if (graphPanel != null) graphPanel.SetActive(false);
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

    // ================= 紧凑版报告生成 =================
    private void GenerateReportText()
    {
        if (analysisText == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<size=110%><color=#005088><b>实验数据处理与结果分析</b></color></size>");
        sb.AppendLine("-----------------------------------------");

        if (!hasValidFit)
        {
            sb.AppendLine("<color=red><b>⚠️ 数据不足或算法迭代发散。</b></color>");
            analysisText.text = sb.ToString();
            return;
        }

        // 使用更紧凑的换行和排版，节省下方空间给残差图
        sb.AppendLine($"<color=#333><b>1. 拟合方程：</b></color>\n   {globalFormula}");
        sb.AppendLine($"<color=#333><b>2. 特征点提取：</b></color>\n   波峰: <b>V_max = {fitCurveMaxV:F2} V</b> ({fitCurveMaxP:F2} μW)\n   波谷: <b>V_min = {fitCurveMinV:F2} V</b> ({fitCurveMinP:F2} μW)");
        sb.AppendLine($"<color=#333><b>3. 半波电压解算：</b></color>\n   V_π = V_min - V_max = <b>{lastCalculatedV:F2} V</b>");

        analysisText.text = sb.ToString();
    }

    // ================= 核心画图与非线性拟合算法 =================
    private void DrawGraph()
    {
        hasValidFit = false;
        if (recordManager == null || lineChart == null) return;

        lineChart.RemoveData();
        if (residualChart != null) residualChart.RemoveData(); // 清空残差图

        List<double> xData = new List<double>();
        List<double> yData = new List<double>();

        for (int i = 0; i < recordManager.voltageCells.Count; i++)
        {
            string vText = recordManager.voltageCells[i].text;
            string pText = recordManager.powerCells[i].text;
            if (double.TryParse(vText, out double voltage) && double.TryParse(pText, out double power))
            {
                xData.Add(voltage);
                yData.Add(power);
            }
        }

        if (xData.Count < 5) return;

        // 1. 绘制实验主图离散点
        var scatterSerie = lineChart.AddSerie<Scatter>("实验数据");
        scatterSerie.symbol.show = true;
        scatterSerie.symbol.type = SymbolType.Circle;
        scatterSerie.symbol.size = 8f;
        scatterSerie.itemStyle.color = Color.red;
        for (int i = 0; i < xData.Count; i++) lineChart.AddData(0, (float)xData[i], (float)yData[i]);

        // 2. 预计算初始猜想值
        double maxP = yData[0], minP = yData[0];
        double maxV = xData[0], minV = xData[0];
        for (int i = 1; i < yData.Count; i++)
        {
            if (yData[i] > maxP) { maxP = yData[i]; maxV = xData[i]; }
            if (yData[i] < minP) { minP = yData[i]; minV = xData[i]; }
        }
        double guessA = (maxP - minP) / 2.0;
        double guessB = (maxP + minP) / 2.0;
        double guessW = System.Math.PI / System.Math.Abs(maxV - minV);
        double guessPhi = -guessW * maxV;

        try
        {
            // 3. 全局非线性曲线拟合
            (double fitA, double fitW, double fitPhi, double fitB) = Fit.Curve(
                xData.ToArray(), yData.ToArray(),
                (a, w, phi, b, x) => a * System.Math.Cos(w * x + phi) + b,
                guessA, guessW, guessPhi, guessB);

            string signPhi = fitPhi < 0 ? "-" : "+";
            string signB = fitB < 0 ? "-" : "+";
            globalFormula = $"P = {System.Math.Abs(fitA):F1}cos({System.Math.Abs(fitW):F4}V {signPhi} {System.Math.Abs(fitPhi):F2}) {signB} {System.Math.Abs(fitB):F1}";

            // 4. 双重扫描法找极值
            double scanStart = xData[0];
            double scanEnd = xData[xData.Count - 1];

            double tempMaxP = double.MinValue;
            for (double v = scanStart; v <= scanEnd; v += 0.1)
            {
                double p = fitA * System.Math.Cos(fitW * v + fitPhi) + fitB;
                if (p > tempMaxP)
                {
                    tempMaxP = p;
                    fitCurveMaxV = (float)v;
                    fitCurveMaxP = (float)p;
                }
            }

            double tempMinP = double.MaxValue;
            for (double v = fitCurveMaxV; v <= scanEnd; v += 0.1)
            {
                double p = fitA * System.Math.Cos(fitW * v + fitPhi) + fitB;
                if (p < tempMinP)
                {
                    tempMinP = p;
                    fitCurveMinV = (float)v;
                    fitCurveMinP = (float)p;
                }
            }

            lastCalculatedV = fitCurveMinV - fitCurveMaxV;
            hasValidFit = true;

            // 5. 绘制主图拟合曲线
            var globalLineSerie = lineChart.AddSerie<Line>("理论拟合");
            globalLineSerie.lineType = LineType.Smooth;
            globalLineSerie.symbol.show = false;
            globalLineSerie.lineStyle.color = Color.green;
            globalLineSerie.lineStyle.width = 3f;

            double plotMinX = scanStart - 20;
            double plotMaxX = scanEnd + 20;
            for (double x = plotMinX; x <= plotMaxX; x += 2)
            {
                double y = fitA * System.Math.Cos(fitW * x + fitPhi) + fitB;
                lineChart.AddData(1, (float)x, (float)y);
            }

            // 6. ⚠️ 新增：计算并绘制残差图 (Residuals)
            if (residualChart != null)
            {
                // 残差散点序列
                var resScatter = residualChart.AddSerie<Scatter>("残差");
                resScatter.symbol.type = SymbolType.Circle;
                resScatter.symbol.size = 5f;
                resScatter.itemStyle.color = new Color(0.1f, 0.5f, 0.8f); // 科技蓝

                // 残差零基准线
                var zeroLine = residualChart.AddSerie<Line>("零线");
                zeroLine.lineType = LineType.Normal;
                zeroLine.symbol.show = false;
                zeroLine.lineStyle.color = Color.gray;
                zeroLine.lineStyle.width = 1.5f;

                for (int i = 0; i < xData.Count; i++)
                {
                    double v = xData[i];
                    double pActual = yData[i];
                    double pFit = fitA * System.Math.Cos(fitW * v + fitPhi) + fitB;
                    double residual = pActual - pFit; // 计算残差：实测值 - 拟合理论值

                    residualChart.AddData(0, (float)v, (float)residual);
                    residualChart.AddData(1, (float)v, 0f); // 绘制零基准线
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("拟合运算失败: " + e.Message);
            hasValidFit = false;
        }
    }
}
