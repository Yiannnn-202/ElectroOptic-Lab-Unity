using System;
using ElectroOptics;
using ElectroOptics.DataTransfer;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.UI.CrystalSelector
{
    /// <summary>
    /// 自定义晶体参数输入面板（持久化单例）。
    /// Show() 首次创建完整 UI，Hide() 隐藏复用。
    /// </summary>
    public class CustomCrystalPanel : MonoBehaviour
    {
        #region 单例入口

        private static CustomCrystalPanel _instance;

        public static void Show(string targetSceneName = "Scene2.The Lab")
        {
            if (_instance == null)
            {
                Canvas canvas = GetOrCreateWindowsCanvas();
                GameObject go = new GameObject("CustomCrystalPanel");
                // 必须加 RectTransform，否则子对象锚点失效
                var goRt = go.AddComponent<RectTransform>();
                goRt.anchorMin = Vector2.zero;
                goRt.anchorMax = Vector2.one;
                goRt.offsetMin = goRt.offsetMax = Vector2.zero;
                go.transform.SetParent(canvas.transform, false);
                _instance = go.AddComponent<CustomCrystalPanel>();
                _instance.BuildUI();
            }
            _instance._targetSceneName = targetSceneName;
            _instance.ResetFields();
            _instance.gameObject.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance != null) _instance.gameObject.SetActive(false);
        }

        #endregion

        #region 字体

        private TMP_FontAsset GetFont()
        {
            if (_fontOK) return _font;
            _fontOK = true;
            foreach (var t in FindObjectsOfType<TMP_Text>(true))
            {
                if (t.font != null && t.font.name.Contains("SIMHEI"))
                { _font = t.font; return _font; }
            }
            foreach (var t in FindObjectsOfType<TMP_Text>(true))
            {
                if (t.font != null && !t.font.name.Contains("LiberationSans"))
                { _font = t.font; return _font; }
            }
            _font = TMP_Settings.defaultFontAsset;
            return _font;
        }

        #endregion

        #region Inspector 可调参数

        [Header("面板尺寸")]
        [SerializeField] private float pnlW = 940f;
        [SerializeField] private float pnlH = 680f;

        [Header("行高 / 标签宽 / 输入框宽")]
        [SerializeField] private float rowH = 56f;
        [SerializeField] private float lblW = 150f;
        [SerializeField] private float inpW = 220f;
        [SerializeField] private float inpSmallW = 100f;

        [Header("字号")]
        [SerializeField] private int fontSizeTitle = 34;
        [SerializeField] private int fontSizeSection = 20;
        [SerializeField] private int fontSizeLabel = 16;
        [SerializeField] private int fontSizeInput = 16;
        [SerializeField] private int fontSizeButton = 26;

        [Header("颜色")]
        [SerializeField] private Color clrOverlay = new Color(0, 0, 0, 0.50f);
        [SerializeField] private Color clrPanel   = new Color(0.13f, 0.14f, 0.18f, 0.98f);
        [SerializeField] private Color clrBar     = new Color(0.17f, 0.19f, 0.25f, 1f);
        [SerializeField] private Color clrSection = new Color(0.65f, 0.75f, 0.90f);
        [SerializeField] private Color clrLabel   = new Color(0.68f, 0.70f, 0.76f);
        [SerializeField] private Color clrInputBg = new Color(0.30f, 0.32f, 0.40f, 1f);
        [SerializeField] private Color clrBtnOk   = new Color(0.22f, 0.56f, 0.86f);
        [SerializeField] private Color clrBtnCancel = new Color(0.35f, 0.35f, 0.42f);

        // 内部引用（保持非 static，支持重建）
        private TMP_FontAsset _font;
        private bool _fontOK;

        #endregion

        #region 内部状态

        private string _targetSceneName = "Scene2.The Lab";
        private TMP_InputField _inpName, _inpWL;
        private TMP_InputField _inpNx, _inpNy, _inpNz;
        private TMP_InputField _inpLen, _inpThick;
        private readonly TMP_InputField[,] _inpR = new TMP_InputField[6, 3];
        private GameObject _eoGrid;
        private TMP_Text _eoArrow;
        private bool _eoOpen = true;
        private bool _built;

        #endregion

        #region BuildUI

#if UNITY_EDITOR
        [ContextMenu("Rebuild UI")]
        private void EditorRebuild()
        {
            // 销毁所有子对象，重置状态，重新构建
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            _built = false;
            _fontOK = false;
            _font = null;
            BuildUI();
        }
#endif

        private void BuildUI()
        {
            if (_built) return;
            _built = true;

            TMP_FontAsset font = GetFont();

            // ── Overlay ──
            var overlay = MkRect("Overlay", transform, 0, 0, 1, 1, fill: true);
            var ovImg = overlay.gameObject.AddComponent<Image>();
            ovImg.color = clrOverlay;
            ovImg.raycastTarget = true; // 阻挡下层点击

            // ── Panel ──
            var panel = MkRect("Panel", overlay, 0.5f, 0.5f, 0.5f, 0.5f, pnlW, pnlH);
            panel.anchoredPosition = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = clrPanel;

            // ── Title ──
            var titleBar = MkRect("TitleBar", panel, 0, 1, 1, 1, h: 68);
            titleBar.pivot = new Vector2(0.5f, 1);
            titleBar.gameObject.AddComponent<Image>().color = clrBar;
            {
                var t = MkTxt("自定义晶体参数", titleBar, fontSizeTitle, Color.white);
                var tr = t.GetComponent<RectTransform>();
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f); tr.sizeDelta = new Vector2(420, 44);
                t.alignment = TextAlignmentOptions.Center;
            }
            {
                var cb = MkRect("CloseBtn", titleBar, 1, 0.5f, 1, 0.5f, 42, 42);
                cb.pivot = new Vector2(1, 0.5f); cb.anchoredPosition = new Vector2(-14, 0);
                cb.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.18f);
                cb.gameObject.AddComponent<Button>().onClick.AddListener(Hide);
                var cx = MkTxt("X", cb, 24, new Color(0.85f, 0.85f, 0.85f));
                CenterStretch(cx.GetComponent<RectTransform>());
                cx.alignment = TextAlignmentOptions.Center;
            }

            // ── ButtonBar ──
            var btnBar = MkRect("ButtonBar", panel, 0, 0, 1, 0, h: 76);
            btnBar.pivot = new Vector2(0.5f, 0);
            btnBar.gameObject.AddComponent<Image>().color = clrBar;
            {
                var hlg = btnBar.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 40;
                hlg.padding = new RectOffset(24, 24, 12, 12);
                hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
                hlg.childControlWidth = false; hlg.childControlHeight = true;
            }
            MkBtn("取 消", btnBar, clrBtnCancel, Hide);
            MkBtn("确 认", btnBar, clrBtnOk, OnConfirm);

            // ── 内容区：ScrollView 容纳溢出内容 ──
            var sv = MkRect("ScrollView", panel, 0, 0, 1, 1, fill: true);
            sv.offsetMin = new Vector2(0, 76); sv.offsetMax = new Vector2(0, -68);
            sv.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);

            var scr = sv.gameObject.AddComponent<ScrollRect>();
            scr.horizontal = false; scr.vertical = true;
            scr.movementType = ScrollRect.MovementType.Clamped;
            scr.scrollSensitivity = 30;

            // Viewport
            var vp = MkRect("Viewport", sv, 0, 0, 1, 1, fill: true);
            // ⚠ Mask 需要不透明的 Image 才能写入 stencil buffer！
            // showMaskGraphic=false 会隐藏渲染，但 Image 必须是可见颜色用于裁切
            var vpImg = vp.gameObject.AddComponent<Image>();
            vpImg.color = Color.white;  // 必须是 visible color，不能 alpha=0
            var mask = vp.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scr.viewport = vp;

            // Content（不用 ContentSizeFitter，手动算高度）
            var content = MkRect("Content", vp, 0, 1, 1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;

            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(34, 34, 20, 24);
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            scr.content = content;

            // 填充内容
            FillContent(content);

            // 等所有子对象创建后，让 VLG 计算实际高度
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            // 取 VLG 计算出来的实际高度
            float realH = LayoutUtility.GetPreferredHeight(content);
            content.sizeDelta = new Vector2(0, Mathf.Max(realH, 400f));
        }

        #endregion

        #region 填充内容

        private void FillContent(RectTransform content)
        {
            // 手动计算总高度并设置 sizeDelta：
            // 每个 section header: 28px
            // 每个 row: rowH px
            // spacing: 10px (由 VLG 控制)
            // padding top: 16, bottom: 20
            // 不需要 ContentSizeFitter！

            // 基本信息
            SectionHdr(content, "基本信息");
            _inpName = Row_LblInp(content, "晶体名称", "自定义晶体", lblW, inpW + 110);
            _inpWL   = Row_LblInp(content, "激光波长 (nm)", "633", lblW, inpW);

            // 折射率
            SectionHdr(content, "折射率");
            {
                var r = MkHrz(content);
                _inpNx = LblInp(r, "n_x", "1.5", inpSmallW + 20);
                _inpNy = LblInp(r, "n_y", "1.5", inpSmallW + 20);
            }
            _inpNz = Row_LblInp(content, "n_z", "1.5", lblW, inpW);

            // 几何尺寸
            SectionHdr(content, "几何尺寸");
            {
                var r = MkHrz(content);
                _inpLen   = LblInp(r, "长度 (mm)", "20", inpSmallW + 30);
                _inpThick = LblInp(r, "厚度 (mm)", "1", inpSmallW + 30);
            }

            // 电光系数
            {
                var foldRow = MkHrz(content);
                foldRow.sizeDelta = new Vector2(0, 40);
                _eoArrow = MkTxt("▼", foldRow, 18, new Color(0.60f, 0.60f, 0.65f));
                _eoArrow.GetComponent<RectTransform>().sizeDelta = new Vector2(28, 40);
                MkTxt("电光系数 (pm/V) — 点击展开/折叠", foldRow, 18, clrSection);
                foldRow.gameObject.AddComponent<Button>().onClick.AddListener(ToggleEO);
                var flg = foldRow.GetComponent<HorizontalLayoutGroup>();
                if (flg != null) { flg.childForceExpandWidth = true; flg.childControlWidth = true; }
            }

            _eoGrid = new GameObject("EOGrid");
            _eoGrid.transform.SetParent(content, false);
            var gridRt = _eoGrid.AddComponent<RectTransform>();
            gridRt.sizeDelta = new Vector2(0, 6 * (rowH + 6));
            var gvl = _eoGrid.AddComponent<VerticalLayoutGroup>();
            gvl.spacing = 6;
            gvl.childForceExpandWidth = true; gvl.childForceExpandHeight = false;
            gvl.childControlWidth = true; gvl.childControlHeight = false;

            for (int i = 0; i < 6; i++)
            {
                var erow = MkHrz(_eoGrid.transform);
                erow.sizeDelta = new Vector2(0, rowH);
                for (int j = 0; j < 3; j++)
                    _inpR[i, j] = LblInp(erow, $"r{i + 1}{j + 1}", "0", inpSmallW + 10);
            }

            // VLG + child sizes 自动撑开 Content 高度
        }

        #endregion

        #region UI 原子组件

        // —— 结构 ——
        private static RectTransform MkRect(string name, Transform parent,
            float ax, float ay, float ax2, float ay2, float w = 0, float h = 0, bool fill = false)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(ax, ay);
            rt.anchorMax = new Vector2(ax2, ay2);
            if (w > 0 || h > 0) rt.sizeDelta = new Vector2(w, h);
            if (fill) { rt.offsetMin = rt.offsetMax = Vector2.zero; }
            return rt;
        }

        private RectTransform MkHrz(Transform parent)
        {
            var rt = MkRect("Row", parent, 0, 0, 0, 0, h: rowH);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 14;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            return rt;
        }

        // —— 文本 ——
        private TMP_Text MkTxt(string text, Transform parent, int size, Color color)
        {
            var go = new GameObject("Lbl"); go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = GetFont();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        private TMP_Text MkTxtSz(string text, Transform parent, int size, Color color, float w, float h)
        {
            var t = MkTxt(text, parent, size, color);
            t.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
            return t;
        }

        // —— Section 标题: 一条细线 + 文字 ——
        private void SectionHdr(Transform parent, string title)
        {
            var go = new GameObject("Sec");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 36);
            // 细线
            var line = new GameObject("Line"); line.transform.SetParent(rt, false);
            var lr = line.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, 0.5f); lr.anchorMax = new Vector2(1, 0.5f);
            lr.sizeDelta = new Vector2(0, 1);
            lr.anchoredPosition = Vector2.zero;
            line.AddComponent<Image>().color = new Color(0.25f, 0.28f, 0.35f);
            // 文字
            var t = MkTxt(title, rt, fontSizeSection, clrSection);
            var tr = t.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 0.5f); tr.anchorMax = new Vector2(0, 0.5f);
            tr.pivot = new Vector2(0, 0.5f);
            tr.anchoredPosition = new Vector2(4, 0);
            tr.sizeDelta = new Vector2(340, 36);
        }

        // —— 带标签的输入框（放在母行内） ——
        private TMP_InputField LblInp(Transform parent, string label, string def, float inpW)
        {
            MkTxtSz(label, parent, fontSizeLabel, clrLabel, 36, rowH);
            return MkInput(parent, def, inpW, rowH);
        }

        // —— 独立行 = 标签 + 输入框 ——
        private TMP_InputField Row_LblInp(Transform parent, string label, string def, float lblW, float inpW)
        {
            var row = MkHrz(parent);
            MkTxtSz(label, row, fontSizeLabel, clrLabel, lblW, rowH);
            return MkInput(row, def, inpW, rowH);
        }

        // —— 输入框 ——
        private TMP_InputField MkInput(Transform parent, string def, float w, float h)
        {
            TMP_FontAsset font = GetFont();

            var go = new GameObject("Inp"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); rt.sizeDelta = new Vector2(w, h);

            // 背景 + 边框效果（深底 + 浅色 Image outline）
            go.AddComponent<Image>().color = clrInputBg;

            // ★ 关键修复：先创建所有子对象，再添加 TMP_InputField
            // Text Area
            var taGo = new GameObject("Text Area"); taGo.transform.SetParent(rt, false);
            var taRt = taGo.AddComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero; taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(10, 4); taRt.offsetMax = new Vector2(-10, -4);
            taGo.AddComponent<RectMask2D>();

            // Text
            var textGo = new GameObject("Text"); textGo.transform.SetParent(taRt, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.font = font; txt.text = def; txt.fontSize = fontSizeInput; txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Left; txt.enableWordWrapping = false;

            // Placeholder
            var phGo = new GameObject("Placeholder"); phGo.transform.SetParent(taRt, false);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = phRt.offsetMax = Vector2.zero;
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.font = font; ph.text = def; ph.fontSize = fontSizeInput;
            ph.color = new Color(0.45f, 0.45f, 0.50f); ph.fontStyle = FontStyles.Italic;
            ph.alignment = TextAlignmentOptions.Left; ph.enableWordWrapping = false;

            // ★ 最后添加 TMP_InputField，此时子对象已完备
            var inp = go.AddComponent<TMP_InputField>();
            inp.textViewport = taRt;
            inp.textComponent = txt;
            inp.placeholder = ph;
            inp.text = def;
            inp.fontAsset = font;

            // 用更亮的背景色 + 微调区分输入框（Outline 与 QuickOutline 冲突）
            return inp;
        }

        // —— 按钮 ——
        private void MkBtn(string text, Transform parent, Color bg, UnityEngine.Events.UnityAction cb)
        {
            var go = new GameObject("Btn"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); rt.sizeDelta = new Vector2(180, 54);
            go.AddComponent<Image>().color = bg;
            var btn = go.AddComponent<Button>();
            var clr = btn.colors;
            clr.normalColor = bg; clr.highlightedColor = bg * 1.2f; clr.pressedColor = bg * 0.8f;
            btn.colors = clr;
            btn.onClick.AddListener(cb);
            var lbl = MkTxt(text, rt, fontSizeButton, Color.white);
            CenterStretch(lbl.GetComponent<RectTransform>());
            lbl.alignment = TextAlignmentOptions.Center;
        }

        private static void CenterStretch(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        #endregion

        #region 交互

        private void ToggleEO()
        {
            _eoOpen = !_eoOpen;
            _eoGrid.SetActive(_eoOpen);
            if (_eoArrow != null) _eoArrow.text = _eoOpen ? "▼" : "▶";
        }

        private void OnConfirm()
        {
            var p = ScriptableObject.CreateInstance<CrystalProfile>();
            p.crystalName          = GetT(_inpName, "自定义晶体");
            p.defaultWavelength_nm = GetN(_inpWL, 633.0);
            p.n_x = GetN(_inpNx, 1.5); p.n_y = GetN(_inpNy, 1.5); p.n_z = GetN(_inpNz, 1.5);
            p.defaultLength_mm    = GetN(_inpLen, 20.0);
            p.defaultThickness_mm = GetN(_inpThick, 1.0);

            for (int i = 0; i < 6; i++)
            for (int j = 0; j < 3; j++)
                SetR(p, i, j, _inpR[i, j]);

            CrystalSelectionData.SelectedProfile = p;
            Debug.Log($"[CustomCrystalPanel] Profile 已创建: {p.crystalName}, n=({p.n_x},{p.n_y},{p.n_z})");

            string dest = CrystalSelectionData.HasTargetScene
                ? CrystalSelectionData.TargetSceneName : _targetSceneName;
            SceneManager.LoadScene(dest);
        }

        private void ResetFields()
        {
            SetT(_inpName, "自定义晶体"); SetT(_inpWL, "633");
            SetT(_inpNx, "1.5"); SetT(_inpNy, "1.5"); SetT(_inpNz, "1.5");
            SetT(_inpLen, "20");  SetT(_inpThick, "1");
            for (int i = 0; i < 6; i++) for (int j = 0; j < 3; j++) SetT(_inpR[i, j], "0");
        }

        #endregion

        #region 工具

        private static Canvas GetOrCreateWindowsCanvas()
        {
            var wc = GameObject.Find("WindowsCanvas");
            if (wc != null) return wc.GetComponent<Canvas>() ?? wc.AddComponent<Canvas>();
            var go = new GameObject("WindowsCanvas");
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 100;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            return c;
        }

        private static string GetT(TMP_InputField f, string def) => f == null || string.IsNullOrWhiteSpace(f.text) ? def : f.text.Trim();
        private static double GetN(TMP_InputField f, double def) => f != null && double.TryParse(f.text?.Trim(), out var v) ? v : def;
        private static void SetT(TMP_InputField f, string v) { if (f != null) f.text = v; }
        private static void SetR(CrystalProfile p, int row, int col, TMP_InputField f)
        {
            var fi = typeof(CrystalProfile).GetField($"r{row + 1}{col + 1}");
            fi?.SetValue(p, GetN(f, 0.0));
        }

        #endregion
    }
}
