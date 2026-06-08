using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;

public class QuizManager : MonoBehaviour
{
    [Header("右侧滚动区域")]
    public GameObject questionPrefab;
    public Transform contentParent;
    public ScrollRect scrollRect;

    [Header("左侧导航面板")]
    public GameObject navButtonPrefab;
    public Transform leftNavPanel;
    public TMP_Text scoreText; // 👈 新增：用来控制左侧显示分数的文本框

    public int questionCount = 6;

    private List<QuestionData> questionBank = new List<QuestionData>();
    private List<QuestionItemUI> spawnedQuestions = new List<QuestionItemUI>();
    private List<Button> spawnedNavButtons = new List<Button>();
    private List<int> correctAnswers = new List<int>();
    private List<string> explanations = new List<string>(); // 存解析

    private bool isSubmitted = false; // 是否已经交卷
    private bool isAutoScrolling = false;

    void Start() { LoadBank(); Generate(); }

    void LoadBank()
    {
        // 1-10题
        questionBank.Add(new QuestionData("由电场的二次项引起折射率变化的现象被称为？", "A) Pockels效应", "B) Faraday效应", "C) Kerr效应", "D) Zeeman效应", 2, "二次电光效应与电场强度的平方成正比，这一现象早在1875年由Kerr发现，称为克尔效应。"));
        questionBank.Add(new QuestionData("半波电压 Uπ 的物理意义是什么？", "A) 相位延迟π/2", "B) 相位延迟2π", "C) 相位延迟π", "D) 折射率变为一半", 2, "当外加电压使沿两个感应主轴方向传播的光分量之间产生 π (即半个波长) 的位相差时，相应的电压值即为半波电压。"));
        questionBank.Add(new QuestionData("横向电光调制中，半波电压与晶体厚度 d 和长度 L 的关系是？", "A) 与d成正比, 与L成反比", "B) 与L成正比, 与d成反比", "C) 均无关", "D) 只与d有关", 0, "公式中明确包含了几何因子 d/L，这在数学上清晰地表明了 Uπ 与极板间厚度 d 成正比，与通光长度 L 成反比。"));
        questionBank.Add(new QuestionData("铌酸锂晶体实验光路时，“最佳运用方式”是？", "A) 沿x通光, 沿y加电", "B) 沿z通光, 沿x或y加电", "C) 沿z通光, 沿z加电", "D) 沿光轴45°通光", 1, "沿光轴(z轴)通光时，o光和e光的折射率原本是相同的(无自然双折射)。此时在垂直于z轴的方向加电场，能避免自然双折射的干扰。"));
        questionBank.Add(new QuestionData("粗调“消光”时，起偏器和检偏器的透光轴应？", "A) 相互平行", "B) 成45°夹角", "C) 相互垂直", "D) 任意角度", 2, "当起偏器和检偏器的透光轴完全垂直时，根据马吕斯定律，透射光强为零，实现彻底消光。"));
        questionBank.Add(new QuestionData("未加电压时，单轴晶体会聚光锥光干涉图呈什么形状？", "A) 同心圆环中央暗十字", "B) 平行等间距条纹", "C) 双曲线条纹", "D) 均匀暗场", 0, "单轴晶体在正交偏振场下，其等倾干涉图样具有特征性的同心圆环（等色线）和覆盖其上的暗十字（等相线）。"));
        questionBank.Add(new QuestionData("正交偏振场中，调制器透过率 T 与电压 u 的关系是？", "A) cos²", "B) sin²", "C) 线性", "D) tan²", 1, "根据推导，代入相位差公式后，即可得到透过率与电压的非线性正弦平方 (sin²) 关系。"));
        questionBank.Add(new QuestionData("为了实现小信号线性调制，直流偏压应设在？", "A) 0", "B) Uπ", "C) 1/2 Uπ", "D) 2 Uπ", 2, "将偏压设置在半波电压的一半处，对应透过率曲线斜率最大且最接近直线的线性工作区中心，可使小信号调制失真最小。"));
        questionBank.Add(new QuestionData("不加直流偏置而在光路中插入元件实现线性区工作，该元件是？", "A) 半波片", "B) 1/4波片", "C) 毛玻璃", "D) 全反射棱镜", 1, "插入快慢轴调节恰当的 1/4 波片可以产生额外的 π/2 固定相位差，这与施加 1/2 Uπ 直流偏压的物理效果完全等效。"));
        questionBank.Add(new QuestionData("消光比 M 的定义是？", "A) 输出/入射比", "B) 调制/偏压比", "C) 最大/最小光强比", "D) 折射率差", 2, "消光比 M = I_max / I_min 反映了调制器将光路彻底关闭与完全打开之间对比度的能力。"));

        // 11-20题
        questionBank.Add(new QuestionData("偏置电压位于极小值点时，交流光信号会出现？", "A) 完美方波", "B) 幅度最大", "C) 明显的倍频失真", "D) 信号消失", 2, "由于极值点处曲线具有偶对称性，正负半周的输入电压变化都会导致同向的透射率变化，从而产生频率为两倍的失真波形。"));
        questionBank.Add(new QuestionData("与KDP相比，铌酸锂的显著优点是？", "A) 无自然双折射", "B) 不潮解, 性质稳定", "C) 系数低", "D) 免疫强光", 1, "KDP 晶体是水溶性的，极其容易在空气中潮解损坏，而 LN (铌酸锂) 晶体不潮解，更适合制成实用器件。"));
        questionBank.Add(new QuestionData("极值法测试中，半波电压等于？", "A) |u1 - u2|/2", "B) |u1 - u2|", "C) 2|u1 - u2|", "D) 平均值", 1, "在透过率曲线中，从一个波谷到相邻的波峰，其横坐标的跨度精确对应于产生 π 弧度相位差所需的电压增量（即半波电压）。"));
        questionBank.Add(new QuestionData("准确判断激光已与晶体光轴(Z轴)重合的依据是？", "A) 功率最大", "B) 光强不受电压影响", "C) 暗十字中心与光斑重合", "D) 圆环消失", 2, "锥光干涉图的对称暗十字中心精确代表了晶体光轴的方向，光斑与之重合即实现了严格对准。"));
        questionBank.Add(new QuestionData("电场方向与通光方向平行被称为？", "A) 横向调制", "B) 纵向调制", "C) 表面调制", "D) 极化调制", 1, "纵向（Longitudinal）在物理学中常用来表示物理量作用方向与波传播方向平行的情形。"));
        questionBank.Add(new QuestionData("关于自然双折射，正确的是？", "A) 电场引起", "B) 铌酸锂加电才有", "C) 压力引起", "D) 晶体固有特性", 3, "自然双折射是由晶体内部固有的微观各向异性排列决定的，在无外场干扰时依然存在。"));
        questionBank.Add(new QuestionData("计算：λ=0.633μm, n0=2.297, γ22=3.4e-12, L=20mm, d=2mm。Uπ约为？", "A) 384V", "B) 768V", "C) 1536V", "D) 7680V", 1, "根据公式 Uπ = (λ / (2 * n0³ * γ22)) * (d / L)，代入数据计算约等于 768 V。"));
        questionBank.Add(new QuestionData("不改材料和波长，如何使半波电压降低一半？", "A) L和d减半", "B) d加倍", "C) L加倍", "D) 变圆柱", 2, "半波电压与晶体长度 L 成反比。增加通光长度可以增加电光效应的积累光程，从而使所需的半波电压降低一半。"));
        questionBank.Add(new QuestionData("测试得极小80V，极大310V，再至极小540V。半波电压是？", "A) 310V", "B) 230V", "C) 460V", "D) 195V", 1, "从波谷（极小值80V）到相邻波峰（极大值310V），电压增量 310 - 80 = 230V 即为半波电压。"));
        questionBank.Add(new QuestionData("音频信号保真调制时，直流偏置 U 应设在？", "A) 80V", "B) 195V", "C) 310V", "D) 0V", 1, "相邻波谷(80V)与波峰(310V)的中点为 (80+310)/2 = 195V。这是透过率曲线斜率最大的线性区中心，最适合线性调制。"));

        // 21-34题
        questionBank.Add(new QuestionData("沿 X 通光沿 Z 加电，会面临什么干扰？", "A) 无效应", "B) 击穿", "C) 巨大的自然双折射相差", "D) 各向同性", 2, "沿 X 轴传播时，两者巨大的固有折射率差异会产生自然相位差，掩盖微弱的电光效应。"));
        questionBank.Add(new QuestionData("最佳方式加电后，感应椭圆主轴旋转了多少度？", "A) 0°", "B) 30°", "C) 45°", "D) 90°", 2, "由于交叉项的影响，在主轴变换对角化方程后，新产生的感应主轴会绕着 Z 轴旋转精确的 45 度。"));
        questionBank.Add(new QuestionData("Uπ=280V，加偏压140V，静态透过率 T 为？", "A) 0", "B) 25%", "C) 50%", "D) 100%", 2, "公式 T = sin²(πU / 2Uπ)。当 U = 140V 且 Uπ = 280V 时，T = sin²(π/4) = 0.5 (即50%)。"));
        questionBank.Add(new QuestionData("线性点观察调制信号，波形削顶的原因是？", "A) 功率弱", "B) 信号幅值过大", "C) 不正交", "D) 没加毛玻璃", 1, "输入的交流信号振幅过大，就会越过线性区进入极值点附近的弯曲区域，导致输出波形上下峰值被非线性压缩。"));
        questionBank.Add(new QuestionData("为了低失真，调制信号幅值 φm 应控制在？", "A) (1/2~1)π", "B) (1/10~1/3)π", "C) 极小", "D) (1~2)π", 1, "既能保证有足够大的信号振幅，又能确保信号主体落在曲线斜率最恒定的“直线区”内，有效控制谐波失真。"));
        questionBank.Add(new QuestionData("测得最高 500μW，最低 2μW。消光比是？", "A) 1000:1", "B) 500:1", "C) 250:1", "D) 498μW", 2, "消光比 M = I_max / I_min = 500 / 2 = 250。这是一个无量纲的比值指标。"));
        questionBank.Add(new QuestionData("输出始终为零，最不可能的原因是？", "A) 未开电源", "B) 偏振轴相互平行", "C) 毛玻璃未移", "D) 探头偏离", 1, "如果两偏振片平行，光线会大量穿透到达接收器，此时应该显示极高的信号电平（亮场），而不是零。"));
        questionBank.Add(new QuestionData("双折射率差 Δn 与外加电场 E 呈什么关系？", "A) 平方", "B) 线性正比", "C) 指数", "D) 反比", 1, "感应产生的双折射率差 Δn 与电场强度 E 是一次方线性比例关系。"));
        questionBank.Add(new QuestionData("铌酸锂晶体的“光损伤”可采用什么方法缓解？", "A) 打磨", "B) 浸水", "C) 退火", "D) 真空", 2, "在高温（居里温度附近）退火能消除内部应力和重新分布电荷，提高其抗光折变的光损伤阈值。"));
        questionBank.Add(new QuestionData("U=Uπ 时，偏振面旋转了多少度？", "A) 0°", "B) 45°", "C) 90°", "D) 180°", 2, "半波电压相当于将偏振面沿着 45度主轴作了镜像翻转，翻转后的偏振面刚好旋转了 90 度，从而 100% 穿透正交检偏器。"));
        questionBank.Add(new QuestionData("减小横向调制的控制电压，可以？", "A) 选短波长", "B) 选长波长", "C) 增加厚度", "D) 减小长度", 0, "公式中 Uπ 与波长 λ 成正比。选用波长更短的激光，所需的半波电压也会按比例降低。"));
        questionBank.Add(new QuestionData("1/4 波片作为偏压，快慢轴应？", "A) 与起偏器平行", "B) 与起偏器垂直", "C) 与起偏器成 45°", "D) 任意", 2, "为了产生额外的相位延迟，快慢轴必须与线偏振方向成 45°，等幅分解光矢量从而拉开 π/2 的相位差。"));
        questionBank.Add(new QuestionData("电光效应响应速度受限于？", "A) 晶格移动", "B) 外部电路 RC", "C) 穿过时间", "D) 热传导", 1, "由于电光效应是极化效应，响应时间极短。实际应用中的调制速率上限几乎全部受限于外部电路的 RC 时间常数。"));
        questionBank.Add(new QuestionData("最大线性范围内，工作点选在透过率曲线的？", "A) 波峰", "B) 波谷", "C) 线性中点 Q", "D) 任意", 2, "线性中点对应的透射率曲线斜率最大且相对恒定，适合大动态范围的线性调制。"));
    }

    void Generate()
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        foreach (Transform child in leftNavPanel) Destroy(child.gameObject);
        spawnedQuestions.Clear(); spawnedNavButtons.Clear();
        correctAnswers.Clear(); explanations.Clear();
        isSubmitted = false;

        // 👈 新增：考试刚开始时，显示类似 "得分：-- / 30"
        if (scoreText != null)
        {
            int totalScore = questionCount * 5;
            scoreText.text = $"得分：-- / {totalScore}";
        }

        var selected = questionBank.OrderBy(x => Random.value).Take(questionCount).ToList();

        for (int i = 0; i < selected.Count; i++)
        {
            GameObject qObj = Instantiate(questionPrefab, contentParent);
            QuestionItemUI ui = qObj.GetComponent<QuestionItemUI>();
            ui.SetupQuestion($"{i + 1}、{selected[i].question}", selected[i].optA, selected[i].optB, selected[i].optC, selected[i].optD);

            spawnedQuestions.Add(ui);
            correctAnswers.Add(selected[i].correctIndex);
            explanations.Add(selected[i].explanation);

            // 当这道题被点击答题时，触发左侧导航栏的颜色更新 (变黄)
            ui.onAnswerSelected = () => { ForceUpdateNavColors(); };

            GameObject bObj = Instantiate(navButtonPrefab, leftNavPanel);
            int idx = i;
            bObj.GetComponentInChildren<TMP_Text>().text = (i + 1).ToString();
            bObj.GetComponent<Button>().onClick.AddListener(() => ScrollTo(idx));
            spawnedNavButtons.Add(bObj.GetComponent<Button>());
        }
    }

    public void OnSubmitClicked()
    {
        if (isSubmitted) return; // 防止重复交卷
        isSubmitted = true;

        int correctCount = 0;
        for (int i = 0; i < spawnedQuestions.Count; i++)
        {
            bool isCorrect = (spawnedQuestions[i].currentSelected == correctAnswers[i]);
            if (isCorrect) correctCount++;

            // 通知每一道题：交卷了，把解析弹出来！
            spawnedQuestions[i].RevealExplanation(explanations[i], correctAnswers[i]);
        }

        // 👈 新增：计算分数逻辑 (每题 5 分)
        int myScore = correctCount * 5;
        int totalScore = spawnedQuestions.Count * 5;

        // 👈 新增：把算好的分数填入刚才创建的 UI 文本里，并用醒目的颜色高亮得分
        if (scoreText != null)
        {
            scoreText.text = $"得分：<color=#FF5555><b>{myScore}</b></color> / {totalScore}";
        }

        Debug.Log($"交卷成功！总计 {spawnedQuestions.Count} 题，做对 {correctCount} 题，得分：{myScore}");

        ForceUpdateNavColors(); // 刷新红绿状态
        Canvas.ForceUpdateCanvases(); // 强制刷新排版，防止遮挡
    }


    // ========= 左侧按钮颜色逻辑 =========

    void Update()
    {
        if (spawnedQuestions.Count == 0 || isAutoScrolling) return;

        float currentScrollY = contentParent.GetComponent<RectTransform>().anchoredPosition.y;
        int activeIndex = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < spawnedQuestions.Count; i++)
        {
            RectTransform itemRect = spawnedQuestions[i].GetComponent<RectTransform>();
            float offset = (itemRect.rect.height * itemRect.pivot.y) + 30f;
            float idealScrollY = -itemRect.localPosition.y - offset;
            float distance = Mathf.Abs(currentScrollY - idealScrollY);

            if (distance < minDistance)
            {
                minDistance = distance;
                activeIndex = i;
            }
        }
        UpdateNavColors(activeIndex);
    }

    void ForceUpdateNavColors()
    {
        UpdateNavColors(-1);
    }

    void UpdateNavColors(int activeScrollIndex)
    {
        for (int i = 0; i < spawnedNavButtons.Count; i++)
        {
            Image btnImage = spawnedNavButtons[i].GetComponent<Image>();

            bool isActive = (i == activeScrollIndex);
            bool hasAnswered = (spawnedQuestions[i].currentSelected != -1);
            bool isCorrect = (spawnedQuestions[i].currentSelected == correctAnswers[i]);

            if (isSubmitted)
            {
                // 交卷后：对的变绿，错的变红 (当前滑到的这题颜色会稍微加深)
                Color finalColor = isCorrect ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);
                btnImage.color = isActive ? finalColor * 0.8f : finalColor;
            }
            else
            {
                // 交卷前：正在看的变橙色，答过的变黄色，没答的变白色
                if (isActive)
                {
                    btnImage.color = new Color(1f, 0.6f, 0f); // 亮橙色
                }
                else if (hasAnswered)
                {
                    btnImage.color = new Color(1f, 0.9f, 0.4f); // 浅黄色
                }
                else
                {
                    btnImage.color = Color.white; // 白色
                }
            }
        }
    }

    public void ScrollTo(int index)
    {
        Canvas.ForceUpdateCanvases();
        isAutoScrolling = true;

        RectTransform target = spawnedQuestions[index].GetComponent<RectTransform>();
        float offset = (target.rect.height * target.pivot.y) + 30f;
        float targetY = -target.localPosition.y - offset;

        float maxScrollY = Mathf.Max(0, contentParent.GetComponent<RectTransform>().rect.height - scrollRect.viewport.rect.height);
        targetY = Mathf.Clamp(targetY, 0, maxScrollY);

        contentParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, targetY);

        UpdateNavColors(index);
        Invoke(nameof(UnlockAutoScroll), 0.1f);
    }

    private void UnlockAutoScroll() => isAutoScrolling = false;
}
