using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum AdditionalConoscopicMode
{
    Uniaxial = 0,
    BiaxialVoltage = 1,
    UniaxialVoltage = 2
}

[Serializable]
public struct AdditionalConoscopicGlobalPhysicalParameters
{
    public float wavelengthNm;
    public float thicknessMm;
    public float polarizerAngleDeg;
    public float analyzerAngleDeg;

    public static AdditionalConoscopicGlobalPhysicalParameters StandardDefaults => Create(532f, 2.5f, 0f, 90f);
    public static AdditionalConoscopicGlobalPhysicalParameters UniaxialVoltageDefaults => Create(633f, 20f, 0f, 90f);

    public static AdditionalConoscopicGlobalPhysicalParameters Create(
        float wavelengthNm,
        float thicknessMm,
        float polarizerAngleDeg,
        float analyzerAngleDeg)
    {
        var parameters = new AdditionalConoscopicGlobalPhysicalParameters
        {
            wavelengthNm = wavelengthNm,
            thicknessMm = thicknessMm,
            polarizerAngleDeg = polarizerAngleDeg,
            analyzerAngleDeg = analyzerAngleDeg
        };
        parameters.Clamp();
        return parameters;
    }

    public void Clamp()
    {
        wavelengthNm = Mathf.Clamp(wavelengthNm, 400f, 800f);
        thicknessMm = Mathf.Clamp(thicknessMm, 0.1f, 60f);
        polarizerAngleDeg = Mathf.Clamp(polarizerAngleDeg, 0f, 180f);
        analyzerAngleDeg = Mathf.Clamp(analyzerAngleDeg, 0f, 180f);
    }
}

[ExecuteAlways]
public class AdditionalExperimentUiVisualController : MonoBehaviour
{
    [SerializeField] private AdditionalConoscopicMode defaultMode = AdditionalConoscopicMode.BiaxialVoltage;
    [SerializeField] private TMP_FontAsset chineseFont;
    [SerializeField] private AdditionalConoscopicGlobalPhysicalParameters m1GlobalParameters = AdditionalConoscopicGlobalPhysicalParameters.StandardDefaults;
    [SerializeField] private AdditionalConoscopicGlobalPhysicalParameters m2GlobalParameters = AdditionalConoscopicGlobalPhysicalParameters.StandardDefaults;
    [SerializeField] private AdditionalConoscopicGlobalPhysicalParameters m3GlobalParameters = AdditionalConoscopicGlobalPhysicalParameters.UniaxialVoltageDefaults;

    private const float SectionSpacing = 10f;
    private const float RowHeight = 38f;
    private const float HeaderHeight = 42f;
    private const float SectionInnerPaddingX = 15f;
    private const float SectionInnerPaddingTop = 8f;
    private const float PanelInsetX = 20f;
    private const float TabTop = 20f;
    private const float TabHeight = 50f;
    private const float TabWidth = 210f;
    private const float TabButtonHeight = 38f;
    private const float ParamLabelWidth = 220f;
    private const float InfoLabelWidth = 140f;
    private const float InputWidth = 82f;
    private const float GlobalTop = 75f;
    private const float GlobalHeight = 290f;
    private const float ModeTop = 375f;
    private const float M1Height = 290f;
    private const float M2Height = 380f;
    private const float M3Height = 230f;
    private const string PolarizerAngleLabel = "起偏器角度 (°)";
    private const string AnalyzerAngleLabel = "检偏器角度 (°)";
    private const string AngleFormat = "{0:0}";
    private const string KeyWavelength = "wavelength";
    private const string KeyThickness = "thickness";
    private const string KeyPolarizer = "polarizer";
    private const string KeyAnalyzer = "analyzer";
    private const string KeyM1OpticTilt = "m1.opticTilt";
    private const string KeyM1OpticAzimuth = "m1.opticAzimuth";
    private const string KeyM1Aperture = "m1.aperture";
    private const string KeyM2Voltage = "m2.voltage";
    private const string KeyM2Alpha = "m2.alpha";
    private const string KeyM2Theta = "m2.theta";
    private const string KeyM2Phi = "m2.phi";
    private const string KeyM2Aperture = "m2.aperture";
    private const string KeyM3Voltage = "m3.voltage";
    private const string KeyM3Aperture = "m3.aperture";

    private readonly Color _panelColor = new Color(0.42f, 0.42f, 0.42f, 0.45f);
    private readonly Color _selectedTabColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    private readonly Color _normalTabColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    private readonly Color _textColor = Color.white;
    private readonly Color _darkTextColor = new Color(0.16f, 0.16f, 0.16f, 1f);

    private Transform _tabGroup;
    private GameObject _sectionGlobal;
    private GameObject _sectionM1;
    private GameObject _sectionM2;
    private GameObject _sectionM3;
    private AdditionalConoscopicMode _activeMode;
    private bool _hasInitialized;
    private float _nextSectionY;
    private Slider _polarizerSlider;
    private TMP_InputField _polarizerInput;
    private Slider _analyzerSlider;
    private TMP_InputField _analyzerInput;
    private bool _suppressParameterNotification;
    private readonly Dictionary<string, ControlBinding> _bindings = new Dictionary<string, ControlBinding>();

    public event Action<AdditionalConoscopicMode> ModeChanged;
    public event Action<AdditionalConoscopicUserParameters> ParametersChanged;
    public event Action RecalculateRequested;

    public AdditionalConoscopicMode ActiveMode => _activeMode;

    private void OnEnable()
    {
        InitializeVisualTree();
    }

    private void Awake()
    {
        if (Application.isPlaying)
        {
            InitializeVisualTree();
        }
    }

    private void OnDisable()
    {
        if (_hasInitialized)
        {
            SaveGlobalParametersForMode(_activeMode);
        }

        _hasInitialized = false;
    }

    public void ShowM1()
    {
        SetMode(AdditionalConoscopicMode.Uniaxial);
    }

    public void ShowM2()
    {
        SetMode(AdditionalConoscopicMode.BiaxialVoltage);
    }

    public void ShowM3()
    {
        SetMode(AdditionalConoscopicMode.UniaxialVoltage);
    }

    public AdditionalConoscopicUserParameters GetUserParameters()
    {
        AdditionalConoscopicGlobalPhysicalParameters fallback = GetGlobalParametersForMode(_activeMode);
        return new AdditionalConoscopicUserParameters
        {
            wavelengthNm = GetControlValue(KeyWavelength, fallback.wavelengthNm),
            thicknessMm = GetControlValue(KeyThickness, fallback.thicknessMm),
            voltageV = ResolveActiveVoltage(),
            crystalAxisAngleDeg = GetControlValue(KeyM2Alpha, 45f),
            polarizerAngleDeg = GetControlValue(KeyPolarizer, fallback.polarizerAngleDeg),
            analyzerAngleDeg = GetControlValue(KeyAnalyzer, fallback.analyzerAngleDeg),
            opticAxisTiltDeg = GetControlValue(KeyM1OpticTilt, 0f),
            opticAxisAzimuthDeg = GetControlValue(KeyM1OpticAzimuth, 0f),
            thetaDeg = GetControlValue(KeyM2Theta, 0f),
            phiDeg = GetControlValue(KeyM2Phi, 0f),
            apertureRadius = ResolveActiveAperture()
        };
    }

    public void RequestRecalculate()
    {
        RecalculateRequested?.Invoke();
    }

    private void RebuildVisualTree()
    {
        EnsureGlobalParameterDefaults();
        _bindings.Clear();
        _polarizerSlider = null;
        _polarizerInput = null;
        _analyzerSlider = null;
        _analyzerInput = null;

        ConfigureFixedRoot();

        _tabGroup = transform.Find("TabGroup");
        if (_tabGroup == null)
        {
            _tabGroup = CreateRectObject("TabGroup", transform).transform;
        }
        DisableLayoutGroup<HorizontalLayoutGroup>(_tabGroup.gameObject);
        SetLayout(_tabGroup.gameObject, preferredHeight: -1f, flexibleHeight: -1f);

        EnsureTabs();

        _sectionGlobal = RecreateSection("Section_Global");
        _sectionM1 = RecreateSection("Section_M1");
        _sectionM2 = RecreateSection("Section_M2");
        _sectionM3 = RecreateSection("Section_M3");

        BuildGlobalSection(_sectionGlobal.transform);
        BuildM1Section(_sectionM1.transform);
        BuildM2Section(_sectionM2.transform);
        BuildM3Section(_sectionM3.transform);

        _tabGroup.SetSiblingIndex(0);
        _sectionGlobal.transform.SetSiblingIndex(1);
        _sectionM1.transform.SetSiblingIndex(2);
        _sectionM2.transform.SetSiblingIndex(3);
        _sectionM3.transform.SetSiblingIndex(4);

        ApplyFixedPanelLayout();
    }

    private void EnsureTabs()
    {
        CreateOrSetupTab("Tab_M1", "单轴晶体 (M1)", ShowM1);
        CreateOrSetupTab("Tab_M2", "双轴晶体 (M2)", ShowM2);
        CreateOrSetupTab("Tab_M3", "单轴电压调整 (M3)", ShowM3);
    }

    private void InitializeVisualTree()
    {
        if (_hasInitialized || !gameObject.scene.IsValid())
        {
            return;
        }

        RebuildVisualTree();
        SetMode(defaultMode);
        _hasInitialized = true;
    }

    private void CreateOrSetupTab(string objectName, string text, UnityAction action)
    {
        GameObject tab = FindDirectChild(_tabGroup, objectName);
        if (tab == null)
        {
            tab = CreateRectObject(objectName, _tabGroup);
            SetLayout(tab, preferredWidth: TabWidth, preferredHeight: TabButtonHeight);
        }

        Image image = Ensure<Image>(tab);
        image.color = _normalTabColor;

        Button button = Ensure<Button>(tab);
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);

        TMP_Text label = tab.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            label = CreateText("Text (TMP)", tab.transform, text, 20, _darkTextColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
        }
        else
        {
            label.text = text;
            label.fontSize = 20;
            label.color = _darkTextColor;
            label.alignment = TextAlignmentOptions.Center;
        }
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 20f;
        label.overflowMode = TextOverflowModes.Ellipsis;
        ApplyChineseFont(label);
    }

    private GameObject RecreateSection(string name)
    {
        GameObject existing = FindDirectChild(transform, name);
        if (existing == null)
        {
            existing = CreateRectObject(name, transform);
        }

        ClearChildren(existing.transform);
        SetupSection(existing);
        return existing;
    }

    private void SetupSection(GameObject section)
    {
        Image image = Ensure<Image>(section);
        image.color = _panelColor;

        DisableLayoutGroup<VerticalLayoutGroup>(section);
        SetLayout(section, preferredHeight: -1f, flexibleWidth: -1f, flexibleHeight: -1f);
    }

    private void BuildGlobalSection(Transform parent)
    {
        BeginFixedSection();
        AddHeader(parent, "全局物理参数");
        AddParamRow(parent, KeyWavelength, "波长 (nm)", 532f, 400f, 800f, "{0:0}");
        AddParamRow(parent, KeyThickness, "晶体厚度 (mm)", 2.5f, 0.1f, 60f, "{0:0.00}");
        AddParamRow(parent, KeyPolarizer, PolarizerAngleLabel, 0f, 0f, 180f, AngleFormat);
        AddParamRow(parent, KeyAnalyzer, AnalyzerAngleLabel, 90f, 0f, 180f, AngleFormat);
        AddActionRow(parent);
    }

    private void BuildM1Section(Transform parent)
    {
        BeginFixedSection();
        AddHeader(parent, "单轴晶体参数 (M1)");
        AddInfoRow(parent, "晶体", "LiNbO3");
        AddParamRow(parent, KeyM1OpticTilt, "光轴倾角 θ (°)", 0f, 0f, 45f, "{0:0}");
        AddSubHeader(parent, "高级参数");
        AddParamRow(parent, KeyM1OpticAzimuth, "光轴方位 φ (°)", 0f, 0f, 360f, "{0:0}");
        AddParamRow(parent, KeyM1Aperture, "通光孔径", 1f, 0.05f, 1f, "{0:0.00}");
    }

    private void BuildM2Section(Transform parent)
    {
        BeginFixedSection();
        AddHeader(parent, "双轴晶体特有参数 (M2)");
        AddInfoRow(parent, "晶体", "KTP");
        AddParamRow(parent, KeyM2Voltage, "电压 (V)", 0f, 0f, 1000f, "{0:0}");
        AddParamRow(parent, KeyM2Alpha, "晶片旋转角 α (°)", 45f, 0f, 180f, "{0:0}");
        AddSubHeader(parent, "高级参数");
        AddParamRow(parent, KeyM2Theta, "样品倾角 θ (°)", 0f, 0f, 90f, "{0:0}");
        AddParamRow(parent, KeyM2Phi, "样品方位角 φ (°)", 0f, 0f, 360f, "{0:0}");
        AddParamRow(parent, KeyM2Aperture, "通光孔径", 1f, 0.05f, 1f, "{0:0.00}");
    }

    private void BuildM3Section(Transform parent)
    {
        BeginFixedSection();
        AddHeader(parent, "单轴电压调制参数 (M3)");
        AddInfoRow(parent, "晶体", "LiNbO3");
        AddParamRow(parent, KeyM3Voltage, "电压 (V)", 0f, 0f, 1000f, "{0:0}");
        AddSubHeader(parent, "高级参数");
        AddParamRow(parent, KeyM3Aperture, "通光孔径", 1f, 0.05f, 1f, "{0:0.00}");
    }

    private void AddHeader(Transform parent, string text)
    {
        TMP_Text label = CreateText("Header", parent, text, 26, _textColor, TextAlignmentOptions.Center);
        SetLayout(label.gameObject, preferredHeight: HeaderHeight);
        PlaceSectionChild(label.gameObject, HeaderHeight);
    }

    private void AddSubHeader(Transform parent, string text)
    {
        TMP_Text label = CreateText("AdvancedHeader", parent, text, 19, _textColor, TextAlignmentOptions.Left);
        SetLayout(label.gameObject, preferredHeight: 30f);
        PlaceSectionChild(label.gameObject, 30f);
    }

    private void AddInfoRow(Transform parent, string labelText, string valueText)
    {
        GameObject row = CreateRectObject("InfoRow_" + labelText, parent);
        SetupHorizontalLayout(row, 10f, TextAnchor.MiddleLeft, true, false);
        SetLayout(row, preferredHeight: RowHeight);

        TMP_Text label = CreateText("Label", row.transform, labelText, 22, _textColor, TextAlignmentOptions.Left);
        label.enableWordWrapping = false;
        SetLayout(label.gameObject, preferredWidth: InfoLabelWidth);

        TMP_Text value = CreateText("Value", row.transform, valueText, 22, _textColor, TextAlignmentOptions.Left);
        SetLayout(value.gameObject, flexibleWidth: 1f);
        PlaceSectionChild(row, RowHeight);
    }

    private void AddParamRow(Transform parent, string key, string labelText, float value, float min, float max, string format)
    {
        GameObject row = CreateParamRow(parent, "ParamRow_" + SanitizeName(labelText));
        SetupHorizontalLayout(row, 10f, TextAnchor.MiddleLeft, true, false);
        SetLayout(row, preferredHeight: RowHeight);

        TMP_Text label = FindDirectChild(row.transform, "Label")?.GetComponent<TMP_Text>();
        if (label == null)
        {
            label = CreateText("Label", row.transform, labelText, 21, _textColor, TextAlignmentOptions.Left);
        }
        label.text = labelText;
        label.fontSize = 21;
        label.color = _textColor;
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        ApplyChineseFont(label);
        SetLayout(label.gameObject, preferredWidth: ParamLabelWidth);

        Slider slider = FindDirectChild(row.transform, "Slider")?.GetComponent<Slider>();
        if (slider == null)
        {
            slider = CreateSlider(row.transform, value, min, max);
        }
        ConfigureSlider(slider, value, min, max);
        SetLayout(slider.gameObject, flexibleWidth: 1f, preferredHeight: 24f);

        string formattedValue = FormatValue(value, format);
        TMP_InputField input = FindDirectChild(row.transform, "InputField (TMP)")?.GetComponent<TMP_InputField>();
        if (input == null)
        {
            input = CreateInput(row.transform, formattedValue);
        }
        ConfigureInput(input, formattedValue);
        SetLayout(input.gameObject, preferredWidth: InputWidth, preferredHeight: 30f);
        BindSliderAndInput(slider, input, min, max, format);
        CachePolarizerControl(labelText, slider, input);
        _bindings[key] = new ControlBinding(slider, input, format);
        PlaceSectionChild(row, RowHeight);
    }

    private GameObject CreateParamRow(Transform parent, string name)
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ParamRow_Template.prefab");
        if (prefab != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance != null)
            {
                instance.name = name;
                instance.layer = 5;
                return instance;
            }
        }
#endif
        return CreateRectObject(name, parent);
    }

    private void AddActionRow(Transform parent)
    {
        GameObject row = CreateRectObject("ActionRow", parent);
        SetupHorizontalLayout(row, 10f, TextAnchor.MiddleLeft, true, false);
        SetLayout(row, preferredHeight: 38f);

        TMP_Text label = CreateText("Text_真实物理参数", row.transform, "* 真实物理参数", 20, _textColor, TextAlignmentOptions.Left);
        SetLayout(label.gameObject, preferredWidth: 180f);

        GameObject spacer = CreateRectObject("Spacer", row.transform);
        SetLayout(spacer, flexibleWidth: 1f);

        Button button = CreateButton(row.transform, "Button_正交偏振", "正交偏振", 130f, 32f);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(SetOrthogonalPolarization);
        PlaceSectionChild(row, RowHeight);
    }

    private Slider CreateSlider(Transform parent, float value, float min, float max)
    {
        GameObject sliderObject = CreateRectObject("Slider", parent);
        Slider slider = Ensure<Slider>(sliderObject);

        GameObject background = CreateRectObject("Background", sliderObject.transform);
        Image backgroundImage = Ensure<Image>(background);
        backgroundImage.color = new Color(0.82f, 0.82f, 0.82f, 1f);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.4f);
        backgroundRect.anchorMax = new Vector2(1f, 0.6f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject fillArea = CreateRectObject("Fill Area", sliderObject.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(0f, 0f);
        fillAreaRect.offsetMax = new Vector2(-20f, 0f);

        GameObject fill = CreateRectObject("Fill", fillArea.transform);
        Image fillImage = Ensure<Image>(fill);
        fillImage.color = new Color(1f, 0.88f, 0.28f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.35f);
        fillRect.anchorMax = new Vector2(0f, 0.65f);
        fillRect.sizeDelta = new Vector2(10f, 0f);

        GameObject handleArea = CreateRectObject("Handle Slide Area", sliderObject.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        Stretch(handleAreaRect);
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = CreateRectObject("Handle", handleArea.transform);
        Image handleImage = Ensure<Image>(handle);
        handleImage.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(18f, 18f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        ConfigureSlider(slider, value, min, max);
        return slider;
    }

    private void ConfigureSlider(Slider slider, float value, float min, float max)
    {
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.wholeNumbers = false;

        RectTransform handleRect = slider.handleRect;
        if (handleRect != null)
        {
            handleRect.anchorMin = new Vector2(handleRect.anchorMin.x, 0.5f);
            handleRect.anchorMax = new Vector2(handleRect.anchorMax.x, 0.5f);
            handleRect.sizeDelta = new Vector2(18f, 18f);
        }
    }

    private TMP_InputField CreateInput(Transform parent, string value)
    {
        GameObject inputObject = CreateRectObject("InputField (TMP)", parent);
        Image image = Ensure<Image>(inputObject);
        image.color = Color.white;

        TMP_InputField input = Ensure<TMP_InputField>(inputObject);
        input.lineType = TMP_InputField.LineType.SingleLine;

        GameObject textArea = CreateRectObject("Text Area", inputObject.transform);
        RectMask2D mask = Ensure<RectMask2D>(textArea);
        mask.padding = new Vector4(-6f, -3f, -6f, -3f);
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        Stretch(textAreaRect);
        textAreaRect.offsetMin = new Vector2(6f, 3f);
        textAreaRect.offsetMax = new Vector2(-6f, -3f);

        TMP_Text placeholder = CreateText("Placeholder", textArea.transform, "Enter text...", 14, new Color(0.55f, 0.55f, 0.55f, 0.65f), TextAlignmentOptions.Center);
        Stretch(placeholder.rectTransform);
        placeholder.fontStyle = FontStyles.Italic;

        TMP_Text text = CreateText("Text", textArea.transform, value, 14, _darkTextColor, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.enableWordWrapping = false;

        input.textViewport = textAreaRect;
        input.placeholder = placeholder;
        input.textComponent = text;
        input.targetGraphic = image;
        ConfigureInput(input, value);
        return input;
    }

    private void ConfigureInput(TMP_InputField input, string value)
    {
        input.SetTextWithoutNotify(value);
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.characterLimit = 0;
        input.richText = true;

        if (input.textComponent != null)
        {
            input.textComponent.text = value;
            input.textComponent.fontSize = 14;
            input.textComponent.color = _darkTextColor;
            input.textComponent.alignment = TextAlignmentOptions.Center;
            input.textComponent.enableWordWrapping = false;
            ApplyChineseFont(input.textComponent);
        }

        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "Enter text...";
            placeholder.fontSize = 14;
            placeholder.alignment = TextAlignmentOptions.Center;
            ApplyChineseFont(placeholder);
        }
    }

    private void BindSliderAndInput(Slider slider, TMP_InputField input, float min, float max, string format)
    {
        float clampedValue = Mathf.Clamp(slider.value, min, max);
        slider.SetValueWithoutNotify(clampedValue);
        input.SetTextWithoutNotify(FormatValue(clampedValue, format));

        slider.onValueChanged.RemoveAllListeners();
        input.onValueChanged.RemoveAllListeners();
        input.onEndEdit.RemoveAllListeners();

        slider.onValueChanged.AddListener(newValue =>
        {
            input.SetTextWithoutNotify(FormatValue(newValue, format));
            NotifyParametersChanged();
        });

        input.onValueChanged.AddListener(rawValue =>
        {
            if (TryParseFloat(rawValue, out float parsedValue))
            {
                slider.SetValueWithoutNotify(Mathf.Clamp(parsedValue, min, max));
                NotifyParametersChanged();
            }
        });

        input.onEndEdit.AddListener(rawValue =>
        {
            if (!TryParseFloat(rawValue, out float parsedValue))
            {
                input.SetTextWithoutNotify(FormatValue(slider.value, format));
                return;
            }

            float clampedInputValue = Mathf.Clamp(parsedValue, min, max);
            slider.value = clampedInputValue;
            input.SetTextWithoutNotify(FormatValue(clampedInputValue, format));
            NotifyParametersChanged();
        });
    }

    private void CachePolarizerControl(string labelText, Slider slider, TMP_InputField input)
    {
        if (labelText == PolarizerAngleLabel)
        {
            _polarizerSlider = slider;
            _polarizerInput = input;
            return;
        }

        if (labelText == AnalyzerAngleLabel)
        {
            _analyzerSlider = slider;
            _analyzerInput = input;
        }
    }

    private void SetOrthogonalPolarization()
    {
        _suppressParameterNotification = true;
        try
        {
            SetSliderInputPair(_polarizerSlider, _polarizerInput, 0f, AngleFormat);
            SetSliderInputPair(_analyzerSlider, _analyzerInput, 90f, AngleFormat);
        }
        finally
        {
            _suppressParameterNotification = false;
        }

        NotifyParametersChanged();
    }

    private static void SetSliderInputPair(Slider slider, TMP_InputField input, float value, string format)
    {
        if (slider == null || input == null)
        {
            return;
        }

        float clampedValue = Mathf.Clamp(value, slider.minValue, slider.maxValue);
        slider.value = clampedValue;
        input.SetTextWithoutNotify(FormatValue(clampedValue, format));
    }

    private static string FormatValue(float value, string format)
    {
        return string.Format(CultureInfo.InvariantCulture, format, value);
    }

    private static bool TryParseFloat(string text, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalizedText = text.Trim();
        return float.TryParse(normalizedText, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || float.TryParse(normalizedText, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private Button CreateButton(Transform parent, string name, string text, float width, float height)
    {
        GameObject buttonObject = CreateRectObject(name, parent);
        Image image = Ensure<Image>(buttonObject);
        image.color = Color.white;

        Button button = Ensure<Button>(buttonObject);
        button.targetGraphic = image;
        SetLayout(buttonObject, preferredWidth: width, preferredHeight: height);

        TMP_Text label = CreateText("Text (TMP)", buttonObject.transform, text, 18, _darkTextColor, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    private void SetMode(AdditionalConoscopicMode mode)
    {
        EnsureGlobalParameterDefaults();
        if (_hasInitialized)
        {
            SaveGlobalParametersForMode(_activeMode);
        }

        _activeMode = mode;
        RestoreGlobalParametersForMode(mode);
        if (_sectionGlobal != null) _sectionGlobal.SetActive(true);
        if (_sectionM1 != null) _sectionM1.SetActive(mode == AdditionalConoscopicMode.Uniaxial);
        if (_sectionM2 != null) _sectionM2.SetActive(mode == AdditionalConoscopicMode.BiaxialVoltage);
        if (_sectionM3 != null) _sectionM3.SetActive(mode == AdditionalConoscopicMode.UniaxialVoltage);

        UpdateTabVisual("Tab_M1", mode == AdditionalConoscopicMode.Uniaxial);
        UpdateTabVisual("Tab_M2", mode == AdditionalConoscopicMode.BiaxialVoltage);
        UpdateTabVisual("Tab_M3", mode == AdditionalConoscopicMode.UniaxialVoltage);
        ApplyFixedPanelLayout();
        ModeChanged?.Invoke(_activeMode);
        NotifyParametersChanged();
    }

    private float ResolveActiveVoltage()
    {
        switch (_activeMode)
        {
            case AdditionalConoscopicMode.BiaxialVoltage:
                return GetControlValue(KeyM2Voltage, 0f);
            case AdditionalConoscopicMode.UniaxialVoltage:
                return GetControlValue(KeyM3Voltage, 0f);
            default:
                return 0f;
        }
    }

    private float ResolveActiveAperture()
    {
        switch (_activeMode)
        {
            case AdditionalConoscopicMode.Uniaxial:
                return GetControlValue(KeyM1Aperture, 1f);
            case AdditionalConoscopicMode.BiaxialVoltage:
                return GetControlValue(KeyM2Aperture, 1f);
            case AdditionalConoscopicMode.UniaxialVoltage:
                return GetControlValue(KeyM3Aperture, 1f);
            default:
                return 1f;
        }
    }

    private float GetControlValue(string key, float fallback)
    {
        if (!_bindings.TryGetValue(key, out ControlBinding binding) || binding.Slider == null)
        {
            return fallback;
        }

        return binding.Slider.value;
    }

    private void NotifyParametersChanged()
    {
        if (_suppressParameterNotification)
        {
            return;
        }

        ParametersChanged?.Invoke(GetUserParameters());
    }

    private AdditionalConoscopicGlobalPhysicalParameters GetGlobalParametersForMode(AdditionalConoscopicMode mode)
    {
        switch (mode)
        {
            case AdditionalConoscopicMode.BiaxialVoltage:
                return m2GlobalParameters;
            case AdditionalConoscopicMode.UniaxialVoltage:
                return m3GlobalParameters;
            default:
                return m1GlobalParameters;
        }
    }

    private void SetGlobalParametersForMode(
        AdditionalConoscopicMode mode,
        AdditionalConoscopicGlobalPhysicalParameters parameters)
    {
        parameters.Clamp();
        switch (mode)
        {
            case AdditionalConoscopicMode.BiaxialVoltage:
                m2GlobalParameters = parameters;
                break;
            case AdditionalConoscopicMode.UniaxialVoltage:
                m3GlobalParameters = parameters;
                break;
            default:
                m1GlobalParameters = parameters;
                break;
        }
    }

    private void SaveGlobalParametersForMode(AdditionalConoscopicMode mode)
    {
        AdditionalConoscopicGlobalPhysicalParameters fallback = GetGlobalParametersForMode(mode);
        SetGlobalParametersForMode(
            mode,
            AdditionalConoscopicGlobalPhysicalParameters.Create(
                GetControlValue(KeyWavelength, fallback.wavelengthNm),
                GetControlValue(KeyThickness, fallback.thicknessMm),
                GetControlValue(KeyPolarizer, fallback.polarizerAngleDeg),
                GetControlValue(KeyAnalyzer, fallback.analyzerAngleDeg)));
    }

    private void RestoreGlobalParametersForMode(AdditionalConoscopicMode mode)
    {
        AdditionalConoscopicGlobalPhysicalParameters parameters = GetGlobalParametersForMode(mode);
        _suppressParameterNotification = true;
        try
        {
            SetControlValueWithoutNotify(KeyWavelength, parameters.wavelengthNm);
            SetControlValueWithoutNotify(KeyThickness, parameters.thicknessMm);
            SetControlValueWithoutNotify(KeyPolarizer, parameters.polarizerAngleDeg);
            SetControlValueWithoutNotify(KeyAnalyzer, parameters.analyzerAngleDeg);
        }
        finally
        {
            _suppressParameterNotification = false;
        }
    }

    private void SetControlValueWithoutNotify(string key, float value)
    {
        if (!_bindings.TryGetValue(key, out ControlBinding binding) || binding.Slider == null || binding.Input == null)
        {
            return;
        }

        float clampedValue = Mathf.Clamp(value, binding.Slider.minValue, binding.Slider.maxValue);
        binding.Slider.SetValueWithoutNotify(clampedValue);
        binding.Input.SetTextWithoutNotify(FormatValue(clampedValue, binding.Format));
    }

    private void EnsureGlobalParameterDefaults()
    {
        EnsureGlobalParameterDefaults(ref m1GlobalParameters, AdditionalConoscopicGlobalPhysicalParameters.StandardDefaults);
        EnsureGlobalParameterDefaults(ref m2GlobalParameters, AdditionalConoscopicGlobalPhysicalParameters.StandardDefaults);
        EnsureGlobalParameterDefaults(ref m3GlobalParameters, AdditionalConoscopicGlobalPhysicalParameters.UniaxialVoltageDefaults);
    }

    private static void EnsureGlobalParameterDefaults(
        ref AdditionalConoscopicGlobalPhysicalParameters parameters,
        AdditionalConoscopicGlobalPhysicalParameters fallback)
    {
        if (parameters.wavelengthNm <= 0f || parameters.thicknessMm <= 0f)
        {
            parameters = fallback;
            return;
        }

        parameters.Clamp();
    }

    private void UpdateTabVisual(string tabName, bool selected)
    {
        if (_tabGroup == null)
        {
            return;
        }

        GameObject tab = FindDirectChild(_tabGroup, tabName);
        if (tab == null)
        {
            return;
        }

        Image image = tab.GetComponent<Image>();
        if (image != null)
        {
            image.color = selected ? _selectedTabColor : _normalTabColor;
        }

        TMP_Text text = tab.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.color = selected ? new Color(0.94f, 0.78f, 0.24f, 1f) : _darkTextColor;
            text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private TMP_Text CreateText(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateRectObject(name, parent);
        TMP_Text tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        ApplyChineseFont(tmp);
        return tmp;
    }

    private void ApplyChineseFont(TMP_Text text)
    {
        TMP_FontAsset font = ResolveChineseFont();
        if (text != null && font != null)
        {
            text.font = font;
        }
    }

    private TMP_FontAsset ResolveChineseFont()
    {
        if (chineseFont != null)
        {
            return chineseFont;
        }

#if UNITY_EDITOR
        chineseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/Fonts/SIMHEI SDF.asset");
#endif
        return chineseFont;
    }

    private GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return obj;
    }

    private void SetupVerticalLayout(GameObject obj, float spacing, TextAnchor alignment)
    {
        VerticalLayoutGroup layout = Ensure<VerticalLayoutGroup>(obj);
        layout.padding = new RectOffset(15, 15, 8, 8);
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private void SetupHorizontalLayout(GameObject obj, float spacing, TextAnchor alignment, bool controlHeight, bool forceExpandHeight)
    {
        HorizontalLayoutGroup layout = Ensure<HorizontalLayoutGroup>(obj);
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = controlHeight;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = forceExpandHeight;
    }

    private void SetLayout(GameObject obj, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f)
    {
        LayoutElement layout = Ensure<LayoutElement>(obj);
        layout.preferredWidth = preferredWidth;
        layout.preferredHeight = preferredHeight;
        layout.flexibleWidth = flexibleWidth;
        layout.flexibleHeight = flexibleHeight;
    }

    private void ConfigureFixedRoot()
    {
        DisableLayoutGroup<VerticalLayoutGroup>(gameObject);
        DisableLayoutGroup<HorizontalLayoutGroup>(gameObject);
        DisableContentSizeFitter(gameObject);
    }

    private void ApplyFixedPanelLayout()
    {
        ConfigureFixedRoot();

        if (_tabGroup != null)
        {
            GameObject tabGroupObject = _tabGroup.gameObject;
            DisableLayoutGroup<HorizontalLayoutGroup>(tabGroupObject);
            DisableContentSizeFitter(tabGroupObject);
            SetTopStretch(_tabGroup as RectTransform, TabTop, TabHeight, PanelInsetX, PanelInsetX);
            ApplyFixedTabLayout();
        }

        ApplyFixedSection(_sectionGlobal, GlobalTop, GlobalHeight);
        ApplyFixedSection(_sectionM1, ModeTop, M1Height);
        ApplyFixedSection(_sectionM2, ModeTop, M2Height);
        ApplyFixedSection(_sectionM3, ModeTop, M3Height);
    }

    private void ApplyFixedTabLayout()
    {
        SetFixedTabRect("Tab_M1", 0.18f);
        SetFixedTabRect("Tab_M2", 0.5f);
        SetFixedTabRect("Tab_M3", 0.82f);
    }

    private void SetFixedTabRect(string tabName, float anchorX)
    {
        GameObject tab = FindDirectChild(_tabGroup, tabName);
        if (tab == null)
        {
            return;
        }

        DisableContentSizeFitter(tab);
        RectTransform rect = tab.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchorX, 0.5f);
        rect.anchorMax = new Vector2(anchorX, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(TabWidth, TabButtonHeight);
    }

    private void ApplyFixedSection(GameObject section, float top, float height)
    {
        if (section == null)
        {
            return;
        }

        DisableLayoutGroup<VerticalLayoutGroup>(section);
        DisableContentSizeFitter(section);
        SetTopStretch(section.GetComponent<RectTransform>(), top, height, PanelInsetX, PanelInsetX);
        ApplyFixedSectionChildren(section.transform);
    }

    private void BeginFixedSection()
    {
        _nextSectionY = SectionInnerPaddingTop;
    }

    private void PlaceSectionChild(GameObject child, float height)
    {
        if (child == null)
        {
            return;
        }

        SetTopStretch(child.GetComponent<RectTransform>(), _nextSectionY, height, SectionInnerPaddingX, SectionInnerPaddingX);
        _nextSectionY += height + SectionSpacing;
    }

    private void ApplyFixedSectionChildren(Transform section)
    {
        float nextY = SectionInnerPaddingTop;
        for (int i = 0; i < section.childCount; i++)
        {
            RectTransform child = section.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            float height = GetFixedChildHeight(child.gameObject);
            SetTopStretch(child, nextY, height, SectionInnerPaddingX, SectionInnerPaddingX);
            nextY += height + SectionSpacing;
        }
    }

    private static float GetFixedChildHeight(GameObject child)
    {
        if (child.name == "Header")
        {
            return HeaderHeight;
        }

        if (child.name == "AdvancedHeader")
        {
            return 30f;
        }

        return RowHeight;
    }

    private static void SetTopStretch(RectTransform rect, float top, float height, float left, float right)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void DisableLayoutGroup<T>(GameObject obj) where T : Behaviour
    {
        T layout = obj.GetComponent<T>();
        if (layout != null)
        {
            layout.enabled = false;
        }
    }

    private static void DisableContentSizeFitter(GameObject obj)
    {
        ContentSizeFitter fitter = obj.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.enabled = false;
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T Ensure<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        return component != null ? component : obj.AddComponent<T>();
    }

    private static GameObject FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(child);
            }
            else
#endif
            {
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }

    private static string SanitizeName(string text)
    {
        return text
            .Replace(" ", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace("（", string.Empty)
            .Replace("）", string.Empty)
            .Replace("°", "deg");
    }

    private struct ControlBinding
    {
        public readonly Slider Slider;
        public readonly TMP_InputField Input;
        public readonly string Format;

        public ControlBinding(Slider slider, TMP_InputField input, string format)
        {
            Slider = slider;
            Input = input;
            Format = format;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureGlobalParameterDefaults();
    }
#endif
}
