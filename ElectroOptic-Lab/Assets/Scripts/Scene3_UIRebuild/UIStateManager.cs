using UnityEngine;
using XCharts.Runtime;
using System.Collections;
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
    public LineChart residualChart; // 残差图表引用

    // 全局拟合留存的物理量与状态
    private bool hasValidFit = false;
    private string globalFormula = "";
    private double _fitA, _fitW, _fitPhi, _fitB;
    private string _signPhi, _signB;

    // 记录拟合曲线本身的理论极值点坐标
    private float fitCurveMaxV = 0f;
    private float fitCurveMinV = 0f;
    private float fitCurveMaxP = 0f;
    private float fitCurveMinP = 0f;

    private float lastCalculatedV = 0f;

    [Header("动画延迟")]
    [Tooltip("主图元素逐个出现的间隔（秒）")]
    [SerializeField] private float graphAnimDelay = 0.08f;
    [Tooltip("残差点逐个弹出的间隔（秒）")]
    [SerializeField] private float residualAnimDelay = 0.05f;

    // 缓存残差图所需数据，等用户按"分析误差"按钮时才播放动画
    private List<double> cachedResidualX;
    private List<double> cachedResidualY;
    private double cachedFitA, cachedFitW, cachedFitPhi, cachedFitB;
    private bool residualDataReady = false;
    private Coroutine residualAnimCoroutine;

    void Start()
    {
        if (leftControlPanel != null) leftControlPanel.SetActive(true);
        if (analysisPanel != null) analysisPanel.SetActive(false);
        if (tablePanel != null) tablePanel.SetActive(true);
        if (graphPanel != null) graphPanel.SetActive(false);

        // SIMHEI SDF 缺大量拉丁字母（P,U,V,c,o,s,m,n,a,x,i,p 等都没有）
        //→ 换 LiberationSans SDF (完整 ASCII) 做主字体，SIMHEI 做中文回退
        if (analysisText != null)
        {
            var liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            var simhei      = Resources.Load<TMP_FontAsset>("Fonts/SIMHEI SDF");
            if (liberation != null && simhei != null)
            {
                analysisText.font = liberation;
                if (!liberation.fallbackFontAssetTable.Contains(simhei))
                    liberation.fallbackFontAssetTable.Add(simhei);

                // 把回退字体的 material 也加进来，否则 SDF 渲染异常(黑块拖影)
                var mats = new System.Collections.Generic.List<Material>();
                foreach (var m in analysisText.fontSharedMaterials)
                    if (m != null) mats.Add(m);
                if (!mats.Contains(simhei.material))
                    mats.Add(simhei.material);
                analysisText.fontSharedMaterials = mats.ToArray();
                analysisText.UpdateFontAsset();

                // 换字体后 TMP 可能重置间距，重新写一遍确保生效
                analysisText.fontSize = 28;
                analysisText.lineSpacing = 16;
                analysisText.paragraphSpacing = 0;
            }
        }
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

        // 打开分析面板时，同步触发残差图逐个弹出动画
        ShowResidualChart();
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
        sb.AppendLine("<size=120%><color=#1a3a5c><b>实验数据处理与结果分析</b></color></size>");
        sb.AppendLine("<color=#999>-------------------------------------------------</color>");

        if (!hasValidFit)
        {
            sb.AppendLine("<color=#cc3333><b>[!] 数据不足或算法迭代发散.</b></color>");
            analysisText.text = sb.ToString();
            return;
        }

        sb.AppendLine($"<color=#1a3a5c><b>1. 拟合方程</b></color>");
        sb.AppendLine($"   P(U) = {System.Math.Abs(_fitA):F1} cos({System.Math.Abs(_fitW):F4}U {_signPhi} {System.Math.Abs(_fitPhi):F2}) {_signB} {System.Math.Abs(_fitB):F1}");
        sb.AppendLine("   <size=80%><color=#888>(P: 光功率 / mW,  U: 调制电压 / V)</color></size>");
        sb.AppendLine("<size=40%> </size>");
        sb.AppendLine($"<color=#1a3a5c><b>2. 特征点提取</b></color>");
        sb.AppendLine($"   <b>Umax = {fitCurveMaxV:F2} V</b>  ({fitCurveMaxP:F2} mW)");
        sb.AppendLine($"   <b>Umin = {fitCurveMinV:F2} V</b>  ({fitCurveMinP:F2} mW)");
        sb.AppendLine("<size=40%> </size>");
        sb.AppendLine($"<color=#1a3a5c><b>3. 半波电压解算</b></color>");
        sb.AppendLine($"   Vπ = Umin - Umax = <b>{lastCalculatedV:F2} V</b>");

        analysisText.text = sb.ToString();
    }

    // ================= 核心画图与非线性拟合算法 =================
    private void DrawGraph()
    {
        hasValidFit = false;
        residualDataReady = false;
        if (residualAnimCoroutine != null) { StopCoroutine(residualAnimCoroutine); residualAnimCoroutine = null; }
        if (recordManager == null || lineChart == null) return;

        // 清空所有旧序列，确保新序列索引从 0 开始
        lineChart.RemoveAllSerie();
        if (residualChart != null) residualChart.RemoveAllSerie();

        // 悬浮提示：仅显示实验数据点的横纵坐标
        var mainTooltip = lineChart.GetChartComponent<Tooltip>();
        if (mainTooltip != null)
        {
            mainTooltip.show = true;
            mainTooltip.trigger = Tooltip.Trigger.Item;
            mainTooltip.titleFormatter = "";
            mainTooltip.itemFormatter = "电压: {b}V\n功率: {c:F2}mW";
        }

        // ===== 主图坐标轴样式配置 =====
        // Y 轴（纵轴）：统一小数点后一位 + 加深文字颜色
        var mainYAxis = lineChart.GetChartComponent<YAxis>();
        if (mainYAxis != null)
        {
            mainYAxis.axisLabel.numericFormatter = "f1";
            mainYAxis.axisLabel.textStyle.color = new Color32(45, 45, 45, 255);
            mainYAxis.axisLabel.textStyle.fontSize = 16;
            // 分割线：淡灰色实线，让网格可见但不喧宾夺主
            mainYAxis.splitLine.lineStyle.color = new Color32(190, 190, 190, 90);
            mainYAxis.splitLine.lineStyle.width = 0.6f;
            // 轴线稍深，定位更清晰
            mainYAxis.axisLine.lineStyle.color = new Color32(100, 100, 100, 255);
            mainYAxis.axisLine.lineStyle.width = 1.0f;
            // 轴名称（"光功率 P (mW)"）加深
            mainYAxis.axisName.labelStyle.textStyle.color = new Color32(45, 45, 45, 255);
            mainYAxis.axisName.labelStyle.textStyle.fontSize = 16;
        }

        // X 轴（横轴）：加深文字颜色 + 轴线清晰
        var mainXAxis = lineChart.GetChartComponent<XAxis>();
        if (mainXAxis != null)
        {
            mainXAxis.axisLabel.textStyle.color = new Color32(45, 45, 45, 255);
            mainXAxis.axisLabel.textStyle.fontSize = 16;
            mainXAxis.axisLine.lineStyle.color = new Color32(100, 100, 100, 255);
            mainXAxis.axisLine.lineStyle.width = 1.0f;
            mainXAxis.axisName.labelStyle.textStyle.color = new Color32(45, 45, 45, 255);
            mainXAxis.axisName.labelStyle.textStyle.fontSize = 16;
        }

        if (residualChart != null)
        {
            var resTooltip = residualChart.GetChartComponent<Tooltip>();
            if (resTooltip != null)
            {
                resTooltip.show = true;
                resTooltip.trigger = Tooltip.Trigger.Item;
                resTooltip.titleFormatter = "";
                // 修改点 1：将残差悬浮提示单位改为 μW
                resTooltip.itemFormatter = "电压: {b}V\n残差: {c:F1}μW";
            }

            // 设置残差图的纵坐标（Y轴）
            var resYAxis = residualChart.GetChartComponent<YAxis>();
            if (resYAxis != null)
            {
                resYAxis.axisLabel.numericFormatter = "f1";
                // 修改点 2：显式强制将 Y 轴划分为 4 个分段
                // 这样在高度有限的空间内，-5.0 到 5.0 之间就能顺利标出 -2.5, 0.0, 2.5 刻度
                resYAxis.splitNumber = 4;
            }
        }

        // 收集原始数据
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

        // 预计算初始猜想值
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
            // 全局非线性曲线拟合
            (double fitA, double fitW, double fitPhi, double fitB) = Fit.Curve(
                xData.ToArray(), yData.ToArray(),
                (a, w, phi, b, x) => a * System.Math.Cos(w * x + phi) + b,
                guessA, guessW, guessPhi, guessB);

            string signPhi = fitPhi < 0 ? "-" : "+";
            string signB = fitB < 0 ? "-" : "+";
            _fitA = fitA; _fitW = fitW; _fitPhi = fitPhi; _fitB = fitB;
            _signPhi = signPhi; _signB = signB;
            globalFormula = $"P(U) = {System.Math.Abs(fitA):F1} cos({System.Math.Abs(fitW):F4}U {signPhi} {System.Math.Abs(fitPhi):F2}) {signB} {System.Math.Abs(fitB):F1}";

            // 双重扫描法找极值
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

            double plotMinX = 0;
            double plotMaxX = scanEnd + 20;

            // ===== 预创建所有空序列 =====
            // Series 0: 实验数据散点
            var scatterSerie = lineChart.AddSerie<Scatter>("实验数据");
            scatterSerie.symbol.show = true;
            scatterSerie.symbol.type = SymbolType.Plus;
            scatterSerie.symbol.size = 8f;
            scatterSerie.symbol.gap = 2.8f;
            scatterSerie.itemStyle.color = new Color32(100, 100, 100, 255);
            scatterSerie.itemStyle.borderWidth = 1.0f;
            scatterSerie.animation.enable = false;

            // Series 1: 理论拟合曲线
            var globalLineSerie = lineChart.AddSerie<Line>("理论拟合");
            globalLineSerie.lineType = LineType.Smooth;
            globalLineSerie.symbol.show = false;
            globalLineSerie.lineStyle.color = new Color32(135, 206, 235, 255);
            globalLineSerie.lineStyle.width = 2f;
            globalLineSerie.animation.enable = false;

            // Series 2 + i*2: 每个数据点的X轴垂线, Series 3 + i*2: Y轴垂线
            for (int i = 0; i < xData.Count; i++)
            {
                var vertLine = lineChart.AddSerie<Line>($"vert_{i}");
                vertLine.lineStyle.type = LineStyle.Type.Dashed;
                vertLine.lineStyle.color = new Color(0.5f, 0.5f, 0.5f, 0.55f);
                vertLine.lineStyle.width = 0.5f;
                vertLine.symbol.show = false;

                var horiLine = lineChart.AddSerie<Line>($"hori_{i}");
                horiLine.lineStyle.type = LineStyle.Type.Dashed;
                horiLine.lineStyle.color = new Color(0.5f, 0.5f, 0.5f, 0.55f);
                horiLine.lineStyle.width = 0.5f;
                horiLine.symbol.show = false;
            }

            // 预创建残差图序列
            if (residualChart != null)
            {
                var resScatter = residualChart.AddSerie<Scatter>("残差");
                resScatter.symbol.type = SymbolType.Circle;
                resScatter.symbol.size = 5f;
                resScatter.itemStyle.color = new Color(0.1f, 0.5f, 0.8f);
                resScatter.animation.enable = false;

                // 缓存残差图所需数据
                cachedResidualX = new List<double>(xData);
                cachedResidualY = new List<double>(yData);
                cachedFitA = fitA;
                cachedFitW = fitW;
                cachedFitPhi = fitPhi;
                cachedFitB = fitB;
                residualDataReady = true;
            }

            // 启动动画协程
            StartCoroutine(AnimateGraphDrawing(
                xData, yData, fitA, fitW, fitPhi, fitB,
                (float)plotMinX, (float)plotMaxX));
        }
        catch (System.Exception e)
        {
            Debug.LogError("拟合运算失败: " + e.Message);
            hasValidFit = false;
        }
    }

    /// <summary>
    /// 主图绘制动画协程
    /// </summary>
    private System.Collections.IEnumerator AnimateGraphDrawing(
        List<double> xData, List<double> yData,
        double fitA, double fitW, double fitPhi, double fitB,
        float plotMinX, float plotMaxX)
    {
        int n = xData.Count;
        WaitForSeconds wait = new WaitForSeconds(graphAnimDelay);

        // ===== Phase 1: 垂线先出现 =====
        for (int i = 0; i < n; i++)
        {
            float v = (float)xData[i];
            float y = (float)yData[i];
            int vertIdx = 2 + i * 2;
            int horiIdx = 3 + i * 2;

            lineChart.AddData(vertIdx, v, y);
            lineChart.AddData(vertIdx, v, 0f);

            lineChart.AddData(horiIdx, v, y);
            lineChart.AddData(horiIdx, plotMinX, y);

            yield return wait;
        }

        // ===== Phase 2: 散点逐个出现 =====
        for (int i = 0; i < n; i++)
        {
            lineChart.AddData(0, (float)xData[i], (float)yData[i]);
            yield return wait;
        }

        // ===== Phase 3: 拟合曲线平滑绘出 =====
        WaitForSeconds fastWait = new WaitForSeconds(graphAnimDelay * 0.01f);
        for (double x = plotMinX; x <= plotMaxX; x += 10)
        {
            double y = fitA * System.Math.Cos(fitW * x + fitPhi) + fitB;
            lineChart.AddData(1, (float)x, (float)y);
            yield return fastWait;
        }
    }

    /// <summary>
    /// 由"分析误差"按钮调用——逐个弹出残差点动画
    /// </summary>
    public void ShowResidualChart()
    {
        if (!residualDataReady || residualChart == null) return;

        // 防止重复触发
        if (residualAnimCoroutine != null) StopCoroutine(residualAnimCoroutine);

        // 清空残差图序列已有数据
        if (residualChart.series.Count >= 1)
        {
            residualChart.series[0].ClearData();
        }

        residualAnimCoroutine = StartCoroutine(AnimateResidualPoints());
    }

    /// <summary>
    /// 残差图动画协程
    /// </summary>
    private System.Collections.IEnumerator AnimateResidualPoints()
    {
        int n = cachedResidualX.Count;
        WaitForSeconds wait = new WaitForSeconds(residualAnimDelay);

        for (int i = 0; i < n; i++)
        {
            double v = cachedResidualX[i];
            double pActual = cachedResidualY[i];
            double pFit = cachedFitA * System.Math.Cos(cachedFitW * v + cachedFitPhi) + cachedFitB;

            // 修改点 3：计算残差后乘以 1000.0，将单位从 mW 换算为 μW
            double residual = (pActual - pFit) * 1000.0;

            residualChart.AddData(0, (float)v, (float)residual);

            yield return wait;
        }

        residualAnimCoroutine = null;
    }
}
