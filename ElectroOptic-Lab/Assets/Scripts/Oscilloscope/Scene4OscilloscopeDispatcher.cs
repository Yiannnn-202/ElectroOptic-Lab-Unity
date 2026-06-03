using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using ElectroOptics.DataTransfer;

namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// Scene4 UI dispatcher: drives oscilloscope parameters, waveform display,
    /// and key-point recording for the rebuilt Scene4 interface.
    /// </summary>
    public class Scene4OscilloscopeDispatcher : MonoBehaviour
    {
        private const string LogPrefix = "[Scene4OscilloscopeDispatcher]";
        private const float MinCh2DisplayScale = 0.0001f;
        private const float MinCh2IntensityPerDivision = 0.0001f;
        private const float MinCh2VerticalDivisions = 0.0001f;
        private const float MinDetectorSaturationVoltage = 0.0001f;
        private const float MinDetectorVoltsPerDivision = 0.0001f;
        private const float MinDetectorVerticalDivisions = 0.0001f;

        [Header("Core References")]
        [SerializeField] private CrystalPhysicalCore physicalCore;
        [SerializeField] private CrystalProfile fallbackProfile;

        [Header("Oscilloscope Defaults")]
        [SerializeField] private float initialVdc = 0f;
        [SerializeField] private float modulationAmplitude = 5f;
        [SerializeField] private float frequency = 1000f;

        [Header("Voltage Hold-Repeat")]
        [SerializeField] private float holdStartDelay = 0.35f;
        [SerializeField] private float voltageStepSize = 0.5f;
        [SerializeField] private float voltageChangeRate = 30f;
        [SerializeField] private int sampleCount = 1024;
        [SerializeField] private float displayPeriods = 2f;
        [SerializeField] private ModulationMode modulationMode = ModulationMode.Transverse;
        [SerializeField] private ElectricFieldAxis fieldAxis = ElectricFieldAxis.Z_Axis;
        [SerializeField] private float intensityMax = 1f;
        [FormerlySerializedAs("autoScaleCh2Display")]
        [SerializeField] private bool normalizeCh2Display = true;
        [Min(MinCh2DisplayScale)]
        [Tooltip("Legacy CH2 display gain kept for serialized scene compatibility. Not used by the intensity/div display mode.")]
        [SerializeField] private float ch2DisplayScale = 1f;
        [Min(MinCh2IntensityPerDivision)]
        [Tooltip("CH2 vertical sensitivity in intensity units per division. Applies when Normalize Ch2 Display is disabled.")]
        [SerializeField] private float ch2IntensityPerDivision = 0.125f;
        [Min(MinCh2VerticalDivisions)]
        [Tooltip("Number of vertical divisions used to compute the CH2 display range.")]
        [SerializeField] private float ch2VerticalDivisions = 8f;
        [SerializeField] private bool useDetectorVoltageForCh2 = false;
        [Min(0f)]
        [Tooltip("Detector conversion gain in volts per CH2 intensity unit.")]
        [SerializeField] private float detectorGainVoltsPerIntensity = 1f;
        [Tooltip("Detector output voltage when CH2 intensity is zero.")]
        [SerializeField] private float detectorOffsetVoltage = 0f;
        [Min(MinDetectorSaturationVoltage)]
        [Tooltip("Detector output saturation voltage and the center reference for detector voltage display.")]
        [SerializeField] private float detectorSaturationVoltage = 5f;
        [SerializeField] private bool clampDetectorVoltage = true;
        [Min(MinDetectorVoltsPerDivision)]
        [Tooltip("CH2 detector voltage sensitivity in volts per division. Applies when detector voltage display is enabled and Normalize Ch2 Display is disabled.")]
        [SerializeField] private float ch2DetectorVoltsPerDivision = 0.5f;
        [Min(MinDetectorVerticalDivisions)]
        [Tooltip("Number of vertical divisions used to compute the CH2 detector voltage display range.")]
        [SerializeField] private float ch2DetectorVerticalDivisions = 8f;
        [SerializeField] private float ch2DisplayPaddingRatio = 0.12f;
        [SerializeField] private bool logDiagnostics = true;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI voltageText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI modulationStatusText;
        [SerializeField] private Button recordButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button resetVoltageButton;
        [SerializeField] private RectTransform ch1Block;
        [SerializeField] private RectTransform ch2Block;
        [SerializeField] private TextMeshProUGUI[] keyPointVoltageTexts = new TextMeshProUGUI[2];
        [SerializeField] private TextMeshProUGUI[] keyPointLabelTexts = new TextMeshProUGUI[2];

        private OscilloscopeCore _core;
        private OscilloscopeWaveformGraphic _ch1Graphic;
        private OscilloscopeWaveformGraphic _ch2Graphic;
        private float[] _ch2DetectorVoltage;
        private float _currentVdc;
        private int _recordIndex;
        private bool _loggedFirstRefresh;

        private float _holdTimer;
        private int _holdDirection; // 0=none, -1=A, +1=D

        private bool _isACConnected;
        private Button _modulationToggleButton;
        private static readonly Color AcConnectedColor = new Color(0.07f, 0.73f, 0.07f, 1f);
        private static readonly Color AcDisconnectedColor = new Color(0.85f, 0.15f, 0.15f, 1f);

        private void Awake()
        {
            DisableLegacyRecordManager();
            AutoBindMissingReferences();
            EnsureCore();

            if (logDiagnostics)
            {
                Debug.Log($"{LogPrefix} Bind status: voltageText={voltageText != null}, statusText={statusText != null}, " +
                          $"modulationStatusText={modulationStatusText != null}, recordButton={recordButton != null}, " +
                          $"clearButton={clearButton != null}, resetButton={resetVoltageButton != null}, " +
                          $"ch1Block={ch1Block != null}, ch2Block={ch2Block != null}");
            }
        }

        private void Start()
        {
            if (statusText != null)
                _statusNormalColor = statusText.color;
            Canvas.ForceUpdateCanvases();
            EnsureWaveformGraphics();
            InitializeCore();
            BindButtons();
            SetupModulationToggle();
            ClearRecords();
            ApplyVoltage(initialVdc);
            UpdateStaticUi();
            StartCoroutine(RefreshAfterCanvasLayout());
        }

        private void Update()
        {
            float wanted = 0f;
            if (Input.GetKey(KeyCode.A)) wanted = -1f;
            else if (Input.GetKey(KeyCode.D)) wanted = 1f;

            if (wanted != 0f)
            {
                if (_holdDirection != (int)wanted)
                {
                    // First frame: single step, then start accumulating
                    ApplyVoltage(_currentVdc + wanted * voltageStepSize);
                    _holdDirection = (int)wanted;
                    _holdTimer = 0f;
                }
                else
                {
                    _holdTimer += Time.deltaTime;
                    if (_holdTimer >= holdStartDelay)
                    {
                        float delta = wanted * voltageChangeRate * Time.deltaTime;
                        ApplyVoltage(_currentVdc + delta);
                    }
                }
            }
            else
            {
                _holdDirection = 0;
                _holdTimer = 0f;
            }
        }

        private void OnDestroy()
        {
            if (_core != null)
                _core.OnWaveformUpdated -= RefreshWaveforms;

            if (recordButton != null)
                recordButton.onClick.RemoveListener(RecordCurrentPoint);
            if (clearButton != null)
                clearButton.onClick.RemoveListener(ClearRecords);
            if (resetVoltageButton != null)
                resetVoltageButton.onClick.RemoveListener(ResetVoltage);
            if (_modulationToggleButton != null)
                _modulationToggleButton.onClick.RemoveListener(ToggleACConnection);
        }

        private void DisableLegacyRecordManager()
        {
            RecordManager legacy = GetComponent<RecordManager>();
            if (legacy != null)
                legacy.enabled = false;
        }

        private void EnsureCore()
        {
            _core = GetComponent<OscilloscopeCore>();
            if (_core == null)
                _core = gameObject.AddComponent<OscilloscopeCore>();
        }

        private void InitializeCore()
        {
            if (physicalCore == null)
                physicalCore = FindFirstObjectByType<CrystalPhysicalCore>();
            if (physicalCore == null)
                physicalCore = gameObject.AddComponent<CrystalPhysicalCore>();

            CrystalProfile profile = CrystalSelectionData.SelectedProfile != null
                ? CrystalSelectionData.SelectedProfile
                : fallbackProfile;

            if (profile == null)
            {
                Debug.LogError($"{LogPrefix} No CrystalProfile found. Assign fallbackProfile or select a crystal before loading Scene4.");
                return;
            }

            _core.Initialize(physicalCore, profile);
            _core.SetDisplayConfig(sampleCount, displayPeriods);
            _core.SetFrequency(frequency);
            _core.SetIntensityMax(intensityMax);
            _core.SetModulationConfig(modulationMode, fieldAxis);
            _core.OnWaveformUpdated -= RefreshWaveforms;
            _core.OnWaveformUpdated += RefreshWaveforms;
        }

        private void BindButtons()
        {
            if (recordButton != null)
            {
                recordButton.onClick.RemoveListener(RecordCurrentPoint);
                recordButton.onClick.AddListener(RecordCurrentPoint);
            }

            if (clearButton != null)
            {
                clearButton.onClick.RemoveListener(ClearRecords);
                clearButton.onClick.AddListener(ClearRecords);
            }

            if (resetVoltageButton != null)
            {
                resetVoltageButton.onClick.RemoveListener(ResetVoltage);
                resetVoltageButton.onClick.AddListener(ResetVoltage);
            }
        }

        private void ApplyVoltage(float vdc)
        {
            _currentVdc = vdc;

            float effectiveModulation = _isACConnected ? modulationAmplitude : 0f;
            if (_core != null)
                _core.SetVoltages(_currentVdc, effectiveModulation);

            UpdateVoltageText();
        }

        private void ResetVoltage()
        {
            ApplyVoltage(initialVdc);
        }

        private IEnumerator RefreshAfterCanvasLayout()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            Canvas.ForceUpdateCanvases();
            EnsureWaveformGraphics();

            if (_core != null && _core.Result != null)
                RefreshWaveforms();
        }

        private void RefreshWaveforms()
        {
            if (_core == null || _core.Result == null)
                return;

            WaveformResult result = _core.Result;
            if (_ch1Graphic != null)
                _ch1Graphic.SetSamples(result.ch1, -modulationAmplitude, modulationAmplitude);
            if (_ch2Graphic != null)
            {
                float[] ch2DisplaySamples = GetCh2DisplaySamples(result.ch2);
                GetCh2DisplayRange(ch2DisplaySamples, out float minY, out float maxY);
                _ch2Graphic.SetSamples(ch2DisplaySamples, minY, maxY);
            }

            UpdateVoltageText();
            UpdateStatusText(result);

            if (logDiagnostics && !_loggedFirstRefresh)
            {
                _loggedFirstRefresh = true;
                Debug.Log($"{LogPrefix} First waveform refresh: ch1={result.ch1?.Length ?? 0}, ch2={result.ch2?.Length ?? 0}, " +
                          $"vPi={result.vPi:F3}, gamma0={result.gamma0:F3}, vDC={_currentVdc:F1}, " +
                          $"ch2Range={GetRangeText(result.ch2)}");
            }
        }

        private float[] GetCh2DisplaySamples(float[] intensitySamples)
        {
            if (!useDetectorVoltageForCh2 || intensitySamples == null)
                return intensitySamples;

            if (_ch2DetectorVoltage == null || _ch2DetectorVoltage.Length != intensitySamples.Length)
                _ch2DetectorVoltage = new float[intensitySamples.Length];

            float gain = Mathf.Max(0f, detectorGainVoltsPerIntensity);
            float saturation = Mathf.Max(MinDetectorSaturationVoltage, detectorSaturationVoltage);
            for (int i = 0; i < intensitySamples.Length; i++)
            {
                float voltage = detectorOffsetVoltage + gain * intensitySamples[i];
                _ch2DetectorVoltage[i] = clampDetectorVoltage ? Mathf.Clamp(voltage, 0f, saturation) : voltage;
            }

            return _ch2DetectorVoltage;
        }

        private void GetCh2DisplayRange(float[] samples, out float minY, out float maxY)
        {
            if (normalizeCh2Display)
            {
                float referenceMax = useDetectorVoltageForCh2
                    ? Mathf.Max(MinDetectorSaturationVoltage, detectorSaturationVoltage)
                    : intensityMax;
                GetDisplayRange(samples, referenceMax, out minY, out maxY);
                return;
            }

            if (useDetectorVoltageForCh2)
            {
                GetCh2DetectorVoltageRange(out minY, out maxY);
                return;
            }

            float minRange = Mathf.Max(0.0001f, intensityMax * 0.0001f);
            float perDivision = Mathf.Max(MinCh2IntensityPerDivision, ch2IntensityPerDivision);
            float divisions = Mathf.Max(MinCh2VerticalDivisions, ch2VerticalDivisions);
            float range = Mathf.Max(minRange, perDivision * divisions);
            float center = intensityMax * 0.5f;

            minY = center - range * 0.5f;
            maxY = center + range * 0.5f;
        }

        private void GetCh2DetectorVoltageRange(out float minY, out float maxY)
        {
            float saturation = Mathf.Max(MinDetectorSaturationVoltage, detectorSaturationVoltage);
            float minRange = Mathf.Max(0.0001f, saturation * 0.0001f);
            float voltsPerDivision = Mathf.Max(MinDetectorVoltsPerDivision, ch2DetectorVoltsPerDivision);
            float divisions = Mathf.Max(MinDetectorVerticalDivisions, ch2DetectorVerticalDivisions);
            float range = Mathf.Max(minRange, voltsPerDivision * divisions);
            float center = saturation * 0.5f;

            minY = center - range * 0.5f;
            maxY = center + range * 0.5f;
        }

        private void GetDisplayRange(float[] samples, float referenceMax, out float minY, out float maxY)
        {
            minY = 0f;
            maxY = Mathf.Max(0.0001f, referenceMax);

            if (samples == null || samples.Length == 0)
                return;

            minY = samples[0];
            maxY = samples[0];

            for (int i = 1; i < samples.Length; i++)
            {
                if (samples[i] < minY) minY = samples[i];
                if (samples[i] > maxY) maxY = samples[i];
            }

            float center = (minY + maxY) * 0.5f;
            float range = maxY - minY;
            float minVisibleRange = Mathf.Max(referenceMax * 0.005f, 0.0001f);
            range = Mathf.Max(range, minVisibleRange);
            range *= 1f + Mathf.Max(0f, ch2DisplayPaddingRatio);

            minY = center - range * 0.5f;
            maxY = center + range * 0.5f;
        }

        private string GetRangeText(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return "empty";

            float min = samples[0];
            float max = samples[0];
            for (int i = 1; i < samples.Length; i++)
            {
                if (samples[i] < min) min = samples[i];
                if (samples[i] > max) max = samples[i];
            }

            return $"{min:F6}..{max:F6}";
        }

        private void UpdateVoltageText()
        {
            if (voltageText != null)
                voltageText.text = $"{_currentVdc:F0}V";
        }

        private void SetupModulationToggle()
        {
            if (modulationStatusText == null) return;

            _modulationToggleButton = modulationStatusText.GetComponent<Button>();
            if (_modulationToggleButton == null)
                _modulationToggleButton = modulationStatusText.gameObject.AddComponent<Button>();

            _modulationToggleButton.onClick.RemoveListener(ToggleACConnection);
            _modulationToggleButton.onClick.AddListener(ToggleACConnection);

            UpdateModulationStatusUI();
        }

        private void ToggleACConnection()
        {
            _isACConnected = !_isACConnected;
            UpdateModulationStatusUI();
            ApplyVoltage(_currentVdc);
        }

        private void UpdateModulationStatusUI()
        {
            if (modulationStatusText == null) return;

            if (_isACConnected)
            {
                modulationStatusText.text = "已接入";
                modulationStatusText.color = AcConnectedColor;
            }
            else
            {
                modulationStatusText.text = "点击接入交流电压";
                modulationStatusText.color = AcDisconnectedColor;
            }
        }

        private void UpdateStaticUi()
        {
            UpdateModulationStatusUI();
        }

        private Color _statusNormalColor;

        private void UpdateStatusText(WaveformResult result)
        {
            if (statusText == null || result == null)
                return;

            if (double.IsInfinity(result.vPi) || double.IsNaN(result.vPi) || result.vPi <= 0)
            {
                statusText.text = "Vπ 无效";
                statusText.color = _statusNormalColor;
                return;
            }

            double piMultiple = result.gamma0 / Mathf.PI;
            double distanceToExtreme = System.Math.Abs(piMultiple - System.Math.Round(piMultiple));
            bool isDistortion = distanceToExtreme < 0.08;
            statusText.text = isDistortion ? "疑似倍频失真点" : "--";
            statusText.color = isDistortion ? _statusNormalColor : Color.black;
        }

        private void RecordCurrentPoint()
        {
            if (_recordIndex >= keyPointVoltageTexts.Length)
            {
                Debug.Log($"{LogPrefix} Key-point cards are full.");
                return;
            }

            if (keyPointVoltageTexts[_recordIndex] != null)
                keyPointVoltageTexts[_recordIndex].text = $"电压：{_currentVdc:F0}V";

            if (keyPointLabelTexts[_recordIndex] != null)
                keyPointLabelTexts[_recordIndex].text = GetKeyPointLabel(_recordIndex);

            _recordIndex++;
        }

        private void ClearRecords()
        {
            for (int i = 0; i < keyPointVoltageTexts.Length; i++)
            {
                if (keyPointVoltageTexts[i] != null)
                    keyPointVoltageTexts[i].text = "电压：_____V";
                if (keyPointLabelTexts[i] != null)
                    keyPointLabelTexts[i].text = GetKeyPointLabel(i);
            }

            _recordIndex = 0;
        }

        private string GetKeyPointLabel(int index)
        {
            switch (index)
            {
                case 0: return "对应：U<sub>0</sub>";
                case 1: return "对应：U<sub>0</sub>+V<sub>π</sub>";
                default: return "对应：";
            }
        }

        private void EnsureWaveformGraphics()
        {
            if (ch1Block != null)
                _ch1Graphic = EnsureGraphic(ch1Block, "CH1_Waveform", new Color(0.2f, 0.85f, 1f, 1f), true);
            if (ch2Block != null)
                _ch2Graphic = EnsureGraphic(ch2Block, "CH2_Waveform", new Color(1f, 0.82f, 0.25f, 1f), false);
        }

        private OscilloscopeWaveformGraphic EnsureGraphic(RectTransform channelBlock, string childName, Color waveformColor, bool upperHalf)
        {
            RectTransform parent = channelBlock.parent as RectTransform;
            if (parent == null)
                parent = channelBlock;

            Transform existing = parent.Find(childName);
            GameObject obj = existing != null ? existing.gameObject : new GameObject(childName);
            obj.transform.SetParent(parent, false);
            obj.transform.SetAsLastSibling();

            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect == null)
                rect = obj.AddComponent<RectTransform>();

            if (obj.GetComponent<CanvasRenderer>() == null)
                obj.AddComponent<CanvasRenderer>();

            if (parent != channelBlock)
            {
                rect.anchorMin = upperHalf ? new Vector2(0f, 0.52f) : new Vector2(0f, 0.04f);
                rect.anchorMax = upperHalf ? new Vector2(1f, 0.96f) : new Vector2(1f, 0.48f);
                rect.offsetMin = new Vector2(24f, 8f);
                rect.offsetMax = new Vector2(-24f, -8f);
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(10f, 10f);
                rect.offsetMax = new Vector2(-10f, -28f);
            }

            OscilloscopeWaveformGraphic graphic = obj.GetComponent<OscilloscopeWaveformGraphic>();
            if (graphic == null)
                graphic = obj.AddComponent<OscilloscopeWaveformGraphic>();

            graphic.raycastTarget = false;
            graphic.SetColor(waveformColor);

            if (logDiagnostics)
            {
                Rect parentRect = parent.rect;
                Debug.Log($"{LogPrefix} Wave graphic ready: {childName}, parent={parent.name}, " +
                          $"channelBlock={channelBlock.name}, parentRect={parentRect.width:F1}x{parentRect.height:F1}, " +
                          $"channelRect={channelBlock.rect.width:F1}x{channelBlock.rect.height:F1}, active={parent.gameObject.activeInHierarchy}");
            }

            return graphic;
        }

        private void AutoBindMissingReferences()
        {
            Transform dataCanvas = FindRoot("DataCanvas");
            if (dataCanvas == null)
            {
                Debug.LogWarning($"{LogPrefix} DataCanvas not found. Please bind UI references manually.");
                return;
            }

            if (voltageText == null)
                voltageText = FindTmp(dataCanvas, "MainArea", "LeftControlPanel", "当前电压面板", "ValueText");
            if (statusText == null)
                statusText = FindTmp(dataCanvas, "MainArea", "LeftControlPanel", "当前状态面板", "ValueText (1)");
            if (modulationStatusText == null)
                modulationStatusText = FindTmp(dataCanvas, "MainArea", "LeftControlPanel", "交流调制状态面板", "ValueText (1)");

            if (recordButton == null)
                recordButton = FindComponent<Button>(dataCanvas, "MainArea", "LeftControlPanel", "ButtonPanel", "RecordButton");
            if (clearButton == null)
                clearButton = FindComponent<Button>(dataCanvas, "MainArea", "LeftControlPanel", "ButtonPanel", "Btn_Clear");
            if (resetVoltageButton == null)
                resetVoltageButton = FindComponent<Button>(dataCanvas, "MainArea", "LeftControlPanel", "ButtonPanel", "重置电压");

            if (ch1Block == null)
                ch1Block = FindComponent<RectTransform>(dataCanvas, "MainArea", "RightDisplayPanel", "WavePanel", "WaveContentArea", "CH1Block");
            if (ch2Block == null)
                ch2Block = FindComponent<RectTransform>(dataCanvas, "MainArea", "RightDisplayPanel", "WavePanel", "WaveContentArea", "CH2Block");

            for (int i = 0; i < 2; i++)
                AutoBindCard(dataCanvas, i);
        }

        private void AutoBindCard(Transform dataCanvas, int index)
        {
            Transform card = FindChildPath(dataCanvas, "MainArea", "RightDisplayPanel", "ResultPanel", "Group", $"card{index + 1}");
            if (card == null)
                return;

            if (keyPointVoltageTexts[index] == null)
                keyPointVoltageTexts[index] = FindTmp(card, "数值区", "电压", "Text (TMP)");
            if (keyPointLabelTexts[index] == null)
                keyPointLabelTexts[index] = FindTmp(card, "数值区", "对应值", "Text (TMP)");
        }

        private TextMeshProUGUI FindTmp(Transform root, params string[] path)
        {
            return FindComponent<TextMeshProUGUI>(root, path);
        }

        private T FindComponent<T>(Transform root, params string[] path) where T : Component
        {
            Transform target = FindChildPath(root, path);
            return target != null ? target.GetComponent<T>() : null;
        }

        private Transform FindRoot(string name)
        {
            // Only search the active scene — additive mode keeps Scene2 alive in background
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (GameObject obj in activeScene.GetRootGameObjects())
            {
                Transform found = FindInHierarchy(obj.transform, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private Transform FindInHierarchy(Transform root, string name)
        {
            if (CleanName(root.name) == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindInHierarchy(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private Transform FindChildPath(Transform root, params string[] path)
        {
            Transform current = root;
            foreach (string segment in path)
            {
                current = FindDirectChild(current, segment);
                if (current == null)
                    return null;
            }
            return current;
        }

        private Transform FindDirectChild(Transform parent, string name)
        {
            string targetName = CleanName(name);
            foreach (Transform child in parent)
            {
                if (CleanName(child.name) == targetName)
                    return child;
            }

            foreach (Transform child in parent)
            {
                if (CleanName(child.name).Contains(targetName))
                    return child;
            }

            Debug.LogWarning($"{LogPrefix} Missing child '{name}' under '{parent.name}'.");
            return null;
        }

        private static string CleanName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            return raw.Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim()
                .Trim('"');
        }
    }
}
