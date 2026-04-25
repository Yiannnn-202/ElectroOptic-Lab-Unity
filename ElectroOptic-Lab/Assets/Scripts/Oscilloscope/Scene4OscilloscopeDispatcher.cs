using TMPro;
using System.Collections;
using UnityEngine;
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

        [Header("Core References")]
        [SerializeField] private CrystalPhysicalCore physicalCore;
        [SerializeField] private CrystalProfile fallbackProfile;

        [Header("Oscilloscope Defaults")]
        [SerializeField] private float initialVdc = 120f;
        [SerializeField] private float voltageStep = 0.5f;
        [SerializeField] private float modulationAmplitude = 5f;
        [SerializeField] private float frequency = 1000f;
        [SerializeField] private int sampleCount = 1024;
        [SerializeField] private float displayPeriods = 2f;
        [SerializeField] private ModulationMode modulationMode = ModulationMode.Transverse;
        [SerializeField] private ElectricFieldAxis fieldAxis = ElectricFieldAxis.Z_Axis;
        [SerializeField] private float intensityMax = 1f;
        [SerializeField] private bool autoScaleCh2Display = true;
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
        [SerializeField] private TextMeshProUGUI[] keyPointVoltageTexts = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI[] keyPointLabelTexts = new TextMeshProUGUI[4];

        private OscilloscopeCore _core;
        private OscilloscopeWaveformGraphic _ch1Graphic;
        private OscilloscopeWaveformGraphic _ch2Graphic;
        private float _currentVdc;
        private int _recordIndex;
        private bool _loggedFirstRefresh;

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
            Canvas.ForceUpdateCanvases();
            EnsureWaveformGraphics();
            InitializeCore();
            BindButtons();
            ClearRecords();
            ApplyVoltage(initialVdc);
            UpdateStaticUi();
            StartCoroutine(RefreshAfterCanvasLayout());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
                ApplyVoltage(_currentVdc - voltageStep);
            else if (Input.GetKeyDown(KeyCode.D))
                ApplyVoltage(_currentVdc + voltageStep);
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

            if (_core != null)
                _core.SetVoltages(_currentVdc, modulationAmplitude);

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
                if (autoScaleCh2Display)
                {
                    GetDisplayRange(result.ch2, out float minY, out float maxY);
                    _ch2Graphic.SetSamples(result.ch2, minY, maxY);
                }
                else
                {
                    _ch2Graphic.SetSamples(result.ch2, 0f, Mathf.Max(0.0001f, intensityMax));
                }
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

        private void GetDisplayRange(float[] samples, out float minY, out float maxY)
        {
            minY = 0f;
            maxY = Mathf.Max(0.0001f, intensityMax);

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
            float minVisibleRange = Mathf.Max(intensityMax * 0.005f, 0.0001f);
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
                voltageText.text = $"{_currentVdc:F1}V";
        }

        private void UpdateStaticUi()
        {
            if (modulationStatusText != null)
                modulationStatusText.text = "已接入";
        }

        private void UpdateStatusText(WaveformResult result)
        {
            if (statusText == null || result == null)
                return;

            if (double.IsInfinity(result.vPi) || double.IsNaN(result.vPi) || result.vPi <= 0)
            {
                statusText.text = "Vπ 无效";
                return;
            }

            double piMultiple = result.gamma0 / Mathf.PI;
            double distanceToExtreme = System.Math.Abs(piMultiple - System.Math.Round(piMultiple));
            statusText.text = distanceToExtreme < 0.08 ? "疑似倍频失真点" : $"Vπ≈{result.vPi:F1}V";
        }

        private void RecordCurrentPoint()
        {
            if (_recordIndex >= keyPointVoltageTexts.Length)
            {
                Debug.Log($"{LogPrefix} Key-point cards are full.");
                return;
            }

            if (keyPointVoltageTexts[_recordIndex] != null)
                keyPointVoltageTexts[_recordIndex].text = $"电压：{_currentVdc:F1}V";

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
                case 0: return "对应：U0";
                case 1: return "对应：U0+UΠ";
                case 2: return "对应：U0+2UΠ";
                case 3: return "对应：";
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

            for (int i = 0; i < 4; i++)
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
            foreach (GameObject obj in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (CleanName(obj.name) == name)
                    return obj.transform;
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
