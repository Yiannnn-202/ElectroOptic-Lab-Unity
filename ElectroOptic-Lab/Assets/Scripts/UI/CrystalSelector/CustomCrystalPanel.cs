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

        private TMP_FontAsset GetChineseFont()
        {
            if (_fontOK) return _font;
            _fontOK = true;
            _font = Resources.Load<TMP_FontAsset>("Fonts/SimSun SDF")
                ?? Resources.Load<TMP_FontAsset>("Fonts/SIMSUN SDF")
                ?? Resources.Load<TMP_FontAsset>("Fonts/SIMHEI SDF");
            if (_font != null) return _font;

            foreach (var t in FindObjectsOfType<TMP_Text>(true))
            {
                if (t.font != null
                    && (t.font.name.Contains("SimSun")
                        || t.font.name.Contains("宋体")
                        || t.font.name.Contains("SIMHEI")
                        || t.font.name.Contains("NotoSerifSC")))
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

        private TMP_FontAsset GetLatinFont()
        {
            if (_latinFontOK) return _latinFont;
            _latinFontOK = true;
            _latinFont = Resources.Load<TMP_FontAsset>("Fonts/Times New Roman SDF")
                ?? Resources.Load<TMP_FontAsset>("Fonts/TIMES SDF")
                ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
                ?? TMP_Settings.defaultFontAsset;
            return _latinFont;
        }

        #endregion

        #region Inspector 可调参数

        [Header("面板尺寸")]
        [SerializeField] private float pnlW = 940f;
        [SerializeField] private float pnlH = 740f;

        [Header("行高 / 标签宽 / 输入框宽")]
        [SerializeField] private float rowH = 38f;
        [SerializeField] private float lblW = 112f;
        [SerializeField] private float inpW = 180f;
        [SerializeField] private float inpSmallW = 166f;

        [Header("字号")]
        [SerializeField] private int fontSizeTitle = 34;
        [SerializeField] private int fontSizeSection = 22;
        [SerializeField] private int fontSizeLabel = 19;
        [SerializeField] private int fontSizeInput = 18;
        [SerializeField] private int fontSizeButton = 22;

        [Header("颜色")]
        [SerializeField] private Color clrOverlay = new Color(0f, 0f, 0f, 0.18f);
        [SerializeField] private Color clrPanel   = new Color(0.97f, 0.985f, 1.00f, 0.98f);
        [SerializeField] private Color clrBar     = new Color(0.955f, 0.975f, 0.995f, 1f);
        [SerializeField] private Color clrSection = new Color(0.33f, 0.45f, 0.62f, 1f);
        [SerializeField] private Color clrLabel   = new Color(0.36f, 0.42f, 0.52f, 1f);
        [SerializeField] private Color clrInputBg = new Color(0.985f, 0.992f, 1f, 1f);
        [SerializeField] private Color clrBtnOk   = new Color(0.16f, 0.43f, 0.74f, 1f);
        [SerializeField] private Color clrBtnCancel = new Color(1f, 1f, 1f, 1f);

        // 内部引用（保持非 static，支持重建）
        private TMP_FontAsset _font;
        private TMP_FontAsset _latinFont;
        private bool _fontOK;
        private bool _latinFontOK;

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
            _latinFontOK = false;
            _font = null;
            _latinFont = null;
            BuildUI();
        }
#endif

        private void BuildUI()
        {
            if (_built) return;
            _built = true;

            var overlay = MkRect("Overlay", transform, 0, 0, 1, 1, fill: true);
            var ovImg = overlay.gameObject.AddComponent<Image>();
            ovImg.color = clrOverlay;
            ovImg.raycastTarget = true;

            var shadow = MkRect("PanelShadow", overlay, 0.5f, 0.5f, 0.5f, 0.5f, pnlW, pnlH);
            shadow.anchoredPosition = new Vector2(10, -10);
            shadow.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.25f, 0.38f, 0.10f);

            var panel = MkRect("Panel", overlay, 0.5f, 0.5f, 0.5f, 0.5f, pnlW, pnlH);
            panel.anchoredPosition = Vector2.zero;
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.color = clrPanel;
            AddOutline(panel.gameObject, new Color(0.70f, 0.78f, 0.88f, 1f), new Vector2(1, -1));

            BuildAcademicContent(panel);
        }

        #endregion

        #region 填充内容

        private void BuildAcademicContent(RectTransform panel)
        {
            FixedImage("TitleBar", panel, 0, 0, pnlW, 84, clrBar);
            FixedImage("TitleDivider", panel, 0, 84, pnlW, 1, new Color(0.74f, 0.81f, 0.90f, 1f));
            FixedText("TitleText", "自定义晶体参数", panel, 36, 22, 330, 42,
                fontSizeTitle, new Color(0.22f, 0.27f, 0.35f, 1f), GetChineseFont(), TextAlignmentOptions.MidlineLeft);
            FixedButton("CloseButton", "X", panel, pnlW - 58, 20, 36, 36,
                new Color(0.93f, 0.96f, 1f, 1f), new Color(0.40f, 0.47f, 0.58f, 1f),
                new Color(0.76f, 0.84f, 0.94f, 1f), Hide, GetLatinFont());

            AddSectionTitle(panel, "基本信息", 108);
            FixedText("NameLabel", "晶体名称", panel, 68, 148, lblW, 32,
                fontSizeLabel, clrLabel, GetChineseFont(), TextAlignmentOptions.MidlineLeft);
            _inpName = FixedInput("NameInput", panel, 188, 140, 430, rowH, "自定义晶体", GetChineseFont());

            FixedText("WavelengthLabel", "激光波长    λ", panel, 68, 200, 130, 32,
                fontSizeLabel, clrLabel, GetChineseFont(), TextAlignmentOptions.MidlineLeft);
            _inpWL = FixedInput("WavelengthInput", panel, 188, 192, inpW, rowH, "633");
            FixedText("WavelengthUnit", "nm", panel, 382, 202, 44, 28,
                16, clrLabel, GetLatinFont(), TextAlignmentOptions.MidlineLeft);

            AddSectionTitle(panel, "折射率", 250);
            FixedFormula("NxLabel", "n<sub>x</sub>", panel, 90, 296, 42, 30);
            _inpNx = FixedInput("NxInput", panel, 188, 288, inpSmallW, rowH, "1.6");
            FixedFormula("NyLabel", "n<sub>y</sub>", panel, 420, 296, 42, 30);
            _inpNy = FixedInput("NyInput", panel, 520, 288, inpSmallW, rowH, "1.6");
            FixedFormula("NzLabel", "n<sub>z</sub>", panel, 90, 348, 42, 30);
            _inpNz = FixedInput("NzInput", panel, 188, 340, inpSmallW, rowH, "1.6");

            AddSectionTitle(panel, "几何尺寸", 402);
            FixedText("LengthLabel", "长度    L", panel, 68, 448, 120, 32,
                fontSizeLabel, clrLabel, GetChineseFont(), TextAlignmentOptions.MidlineLeft);
            _inpLen = FixedInput("LengthInput", panel, 188, 440, inpSmallW, rowH, "20");
            FixedText("LengthUnit", "mm", panel, 372, 450, 44, 28,
                16, clrLabel, GetLatinFont(), TextAlignmentOptions.MidlineLeft);

            FixedText("ThicknessLabel", "厚度    d", panel, 420, 448, 120, 32,
                fontSizeLabel, clrLabel, GetChineseFont(), TextAlignmentOptions.MidlineLeft);
            _inpThick = FixedInput("ThicknessInput", panel, 520, 440, inpSmallW, rowH, "1");
            FixedText("ThicknessUnit", "mm", panel, 704, 450, 44, 28,
                16, clrLabel, GetLatinFont(), TextAlignmentOptions.MidlineLeft);

            AddSectionTitle(panel, "电光系数", 500);
            var grid = FixedImage("ElectroOpticGrid", panel, 26, 526, 888, 136,
                new Color(1f, 1f, 1f, 0.34f));
            AddOutline(grid.gameObject, new Color(0.78f, 0.86f, 0.96f, 1f), new Vector2(1, -1));
            _eoGrid = grid.gameObject;
            _eoOpen = true;
            _eoArrow = null;
            BuildElectroOpticGrid(grid);

            FixedImage("FooterDivider", panel, 0, 672, pnlW, 1, new Color(0.76f, 0.83f, 0.92f, 1f));
            FixedImage("Footer", panel, 0, 673, pnlW, 67, new Color(0.985f, 0.992f, 1f, 0.96f));
            FixedButton("CancelButton", "取消", panel, 590, 684, 150, 48,
                Color.white, new Color(0.38f, 0.44f, 0.54f, 1f),
                new Color(0.70f, 0.79f, 0.90f, 1f), Hide, GetChineseFont());
            FixedButton("ConfirmButton", "确认", panel, 760, 684, 150, 48,
                clrBtnOk, Color.white, clrBtnOk, OnConfirm, GetChineseFont());
        }

        private void BuildElectroOpticGrid(RectTransform grid)
        {
            float[] leftLabelX = { 18f, 164f, 310f };
            float[] leftInputX = { 62f, 208f, 354f };
            float[] rightLabelX = { 468f, 614f, 760f };
            float[] rightInputX = { 512f, 658f, 804f };

            for (int row = 0; row < 3; row++)
            {
                float y = 8f + row * 40f;
                for (int col = 0; col < 3; col++)
                {
                    AddEoCell(grid, row + 1, col + 1, leftLabelX[col], leftInputX[col], y);
                    AddEoCell(grid, row + 4, col + 1, rightLabelX[col], rightInputX[col], y);
                }
            }
        }

        private void AddEoCell(Transform parent, int row, int col, float labelX, float inputX, float y)
        {
            FixedFormula($"R{row}{col}Label", $"r<sub>{row}{col}</sub>", parent, labelX, y + 2, 42, 28);
            _inpR[row - 1, col - 1] = FixedInput($"R{row}{col}Input", parent, inputX, y, 84, 32, "0");
        }

        private void AddSectionTitle(Transform parent, string title, float y)
        {
            FixedText($"{title}Title", title, parent, 68, y, 120, 32,
                fontSizeSection, clrSection, GetChineseFont(), TextAlignmentOptions.MidlineLeft);
            FixedImage($"{title}Rule", parent, 188, y + 18, 690, 1,
                new Color(0.76f, 0.83f, 0.92f, 1f));
        }

        #endregion

        #region UI 原子组件

        private static RectTransform MkRect(string name, Transform parent,
            float ax, float ay, float ax2, float ay2, float w = 0, float h = 0, bool fill = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(ax, ay);
            rt.anchorMax = new Vector2(ax2, ay2);
            if (w > 0 || h > 0) rt.sizeDelta = new Vector2(w, h);
            if (fill) rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static RectTransform FixedRect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static RectTransform FixedImage(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var rt = FixedRect(name, parent, x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return rt;
        }

        private static void AddOutline(GameObject go, Color color, Vector2 distance)
        {
            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        private TMP_Text FixedText(string name, string text, Transform parent, float x, float y, float w, float h,
            int size, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
        {
            var rt = FixedRect(name, parent, x, y, w, h);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.richText = true;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        private TMP_Text FixedFormula(string name, string text, Transform parent, float x, float y, float w, float h)
        {
            return FixedText(name, text, parent, x, y, w, h, 19,
                new Color(0.38f, 0.44f, 0.54f, 1f), GetLatinFont(), TextAlignmentOptions.Center);
        }

        private TMP_InputField FixedInput(string name, Transform parent, float x, float y, float w, float h, string def,
            TMP_FontAsset fontOverride = null)
        {
            TMP_FontAsset font = fontOverride != null ? fontOverride : GetLatinFont();
            var rt = FixedRect(name, parent, x, y, w, h);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = clrInputBg;
            AddOutline(rt.gameObject, new Color(0.68f, 0.78f, 0.90f, 1f), new Vector2(1, -1));

            var taRt = FixedRect("Text Area", rt, 12, 4, w - 24, h - 8);
            taRt.gameObject.AddComponent<RectMask2D>();

            var textRt = FixedRect("Text", taRt, 0, 0, w - 24, h - 8);
            var txt = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            txt.font = font;
            txt.text = def;
            txt.fontSize = fontSizeInput;
            txt.color = new Color(0.25f, 0.30f, 0.38f, 1f);
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.enableWordWrapping = false;
            txt.raycastTarget = false;

            var phRt = FixedRect("Placeholder", taRt, 0, 0, w - 24, h - 8);
            var ph = phRt.gameObject.AddComponent<TextMeshProUGUI>();
            ph.font = font;
            ph.text = def;
            ph.fontSize = fontSizeInput;
            ph.color = new Color(0.58f, 0.64f, 0.72f, 0.85f);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.enableWordWrapping = false;
            ph.raycastTarget = false;

            var inp = rt.gameObject.AddComponent<TMP_InputField>();
            inp.textViewport = taRt;
            inp.textComponent = txt;
            inp.placeholder = ph;
            inp.text = def;
            inp.fontAsset = font;
            inp.lineType = TMP_InputField.LineType.SingleLine;
            inp.selectionColor = new Color(0.25f, 0.50f, 0.85f, 0.35f);
            inp.caretColor = new Color(0.17f, 0.35f, 0.62f, 1f);
            inp.customCaretColor = true;
            return inp;
        }

        private Button FixedButton(string name, string text, Transform parent, float x, float y, float w, float h,
            Color bg, Color textColor, Color borderColor, UnityEngine.Events.UnityAction cb, TMP_FontAsset font)
        {
            var rt = FixedRect(name, parent, x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = bg;
            AddOutline(rt.gameObject, borderColor, new Vector2(1, -1));

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = image;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.80f, 0.88f, 0.98f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            btn.colors = colors;
            btn.onClick.AddListener(cb);

            FixedText($"{name}Text", text, rt, 0, 0, w, h, fontSizeButton, textColor, font, TextAlignmentOptions.Center);
            return btn;
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
            p.n_x = GetN(_inpNx, 1.6); p.n_y = GetN(_inpNy, 1.6); p.n_z = GetN(_inpNz, 1.6);
            p.defaultLength_mm    = GetN(_inpLen, 20.0);
            p.defaultThickness_mm = GetN(_inpThick, 1.0);

            for (int i = 0; i < 6; i++)
            for (int j = 0; j < 3; j++)
                SetR(p, i, j, _inpR[i, j]);

            CrystalSelectionData.SelectedProfile = p;
            Debug.Log($"[CustomCrystalPanel] Profile 已创建: {p.crystalName}, n=({p.n_x},{p.n_y},{p.n_z})");

            string dest = CrystalSelectionData.HasTargetScene
                ? CrystalSelectionData.TargetSceneName : _targetSceneName;

            // Keep Scene2 alive if it's loaded (additive experiment flow)
            Scene labScene = SceneManager.GetSceneByName(Scene2AdditionalSceneNavigator.LabSceneName);
            if (labScene.isLoaded)
            {
                Scene2AdditionalSceneNavigator.GoToExperimentFromPreviewAdditive(dest);
            }
            else
            {
                SceneManager.LoadScene(dest);
            }
        }

        private void ResetFields()
        {
            SetT(_inpName, "自定义晶体"); SetT(_inpWL, "633");
            SetT(_inpNx, "1.6"); SetT(_inpNy, "1.6"); SetT(_inpNz, "1.6");
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
