using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ElectroOptics.Experiment.Controller;

namespace ElectroOptics.UI.ExperimentGuide
{
    /// <summary>
    /// Scene2 卡片式操作引导。
    /// 卡片只显示实验操作文字，不显示额外的开发说明。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class Scene2CardGuide : MonoBehaviour
    {
        private enum StepKind
        {
            MoveToRail,
            LaserCalibration,
            RotateAnalyzer,
            ObserveConoscopic,
            RotateCrystal,
            RemoveFromRail,
            Finish
        }

        private sealed class GuideStep
        {
            public StepKind kind;
            public string title;
            public string body;
            public OpticalComponent component;
        }

        [Header("自动行为")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float autoStartDelay = 0.4f;
        [SerializeField] private KeyCode toggleShortcut = KeyCode.F1;

        [Header("卡片位置")]
        [SerializeField] private Vector2 cardPosition = new Vector2(32f, -32f);
        [SerializeField] private Vector2 cardSize = new Vector2(460f, 210f);

        [Header("透明度")]
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.025f);
        [SerializeField] private Color cardColor = new Color(0.03f, 0.10f, 0.16f, 0.62f);
        [SerializeField] private Color accentColor = new Color(0.25f, 0.86f, 1f, 0.95f);

        [Header("步骤行为")]
        [SerializeField] private bool enterCompletesLaserStep = true;

        [Header("动画")]
        [SerializeField] private float pointerTravelDuration = 1.1f;
        [SerializeField] private float completionDelay = 0.5f;

        private const string WindowsCanvasName = "WindowsCanvas";
        private const string FontResourcePath = "Fonts/SIMHEI SDF";

        private readonly List<GuideStep> _steps = new List<GuideStep>();
        private readonly HashSet<OpticalComponent> _everOnRail = new HashSet<OpticalComponent>();
        private readonly List<OpticalComponent> _polarizers = new List<OpticalComponent>();
        private readonly List<RotateStandController> _polarizerStands = new List<RotateStandController>();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameObject _rootObject;
        private CanvasGroup _rootGroup;
        private RectTransform _cardRect;
        private RectTransform _lineRect;
        private RectTransform _arrowRect;
        private RectTransform _markerRect;
        private RectTransform _cursorRect;
        private TextMeshProUGUI _stepText;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _buttonText;
        private Button _closeButton;
        private Button _restartButton;
        private TMP_FontAsset _font;
        private Coroutine _fadeCoroutine;

        private OpticalRail _rail;
        private LaserEmitterMover _laser;
        private OpticalComponent _screen;
        private OpticalComponent _crystal;
        private OpticalComponent _beamExpander;
        private OpticalComponent _detector;
        private CrystalControllerWrapper _crystalController;

        private int _stepIndex;
        private float _completionTimer;
        private float _pointerTimer;
        private bool _active;
        private bool _built;
        private bool _laserStepConfirmed;
        private bool _ownsRuntimeUi;

        private void Start()
        {
            StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            yield return null;
            yield return null;

            ResolveSceneReferences();
            BuildSteps();
            BuildUI();

            if (autoStart)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, autoStartDelay));
                StartGuide();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleShortcut))
            {
                if (_active) HideGuide();
                else StartGuide();
            }

            if (!_active || _steps.Count == 0 || _stepIndex >= _steps.Count)
            {
                return;
            }

            UpdateRailHistory();
            GuideStep step = _steps[_stepIndex];

            // 引导确认键：按 Enter 直接完成“校准激光”卡片。
            // 不再要求 LaserStateController 先被选中。
            if (enterCompletesLaserStep
                && step.kind == StepKind.LaserCalibration
                && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                _laserStepConfirmed = true;
                if (_laser != null)
                {
                    _laser.isCalibrationDone = true;
                }
            }

            if (IsStepComplete(step))
            {
                _completionTimer += Time.unscaledDeltaTime;
                if (_completionTimer >= completionDelay)
                {
                    AdvanceStep();
                    return;
                }
            }
            else
            {
                _completionTimer = 0f;
            }

            RefreshPointer(step);
        }

        private void OnDestroy()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            if (_rootObject == null)
            {
                return;
            }

            if (!_ownsRuntimeUi)
            {
                return;
            }

            if (Application.isPlaying) Destroy(_rootObject);
            else DestroyImmediate(_rootObject);
        }

        public void StartGuide()
        {
            if (!_built)
            {
                ResolveSceneReferences();
                BuildSteps();
                BuildUI();
            }

            _stepIndex = 0;
            _completionTimer = 0f;
            _pointerTimer = 0f;
            _everOnRail.Clear();
            _laserStepConfirmed = false;
            ShowRoot();
            ShowCurrentStep();
        }

        public void HideGuide()
        {
            _active = false;
            if (_rootGroup != null)
            {
                _rootGroup.alpha = 0f;
                _rootGroup.blocksRaycasts = false;
                _rootGroup.interactable = false;
            }

            if (_rootObject != null)
            {
                _rootObject.SetActive(false);
            }
        }

        public void RestartGuide()
        {
            StartGuide();
        }

        private void ResolveSceneReferences()
        {
            _rail = FindObjectOfType<OpticalRail>(true);
            _laser = FindObjectOfType<LaserEmitterMover>(true);

            OpticalComponent[] components = FindObjectsOfType<OpticalComponent>(true);
            _screen = FindByName(components, "光屏", "screen");
            _crystal = FindByName(components, "晶体盒", "新晶体盒", "晶体", "crystal");
            _beamExpander = FindByName(components, "扩束镜", "扩束", "beam");
            _detector = FindByName(components, "接收器", "功率计", "光电", "receiver", "power");

            // UnifiedScreenPanel 中的引用是 private，读取它可以避免模型名称变化导致找不到对象。
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().Name != "UnifiedScreenPanel")
                {
                    continue;
                }

                _screen = ReadPrivateReference(behaviour, "screenOpticalComponent", _screen);
                _crystal = ReadPrivateReference(behaviour, "crystalOpticalComponent", _crystal);
                _beamExpander = ReadPrivateReference(behaviour, "beamExpanderOpticalComponent", _beamExpander);
            }

            _crystalController = FindObjectOfType<CrystalControllerWrapper>(true);
            if (_crystalController == null && _crystal != null)
            {
                _crystalController = _crystal.GetComponentInChildren<CrystalControllerWrapper>(true);
            }

            ResolvePolarizers(components);
        }

        private void ResolvePolarizers(OpticalComponent[] components)
        {
            _polarizers.Clear();
            _polarizerStands.Clear();

            List<OpticalComponent> preferred = new List<OpticalComponent>();
            List<OpticalComponent> standComponents = new List<OpticalComponent>();
            RotateStandController[] stands = FindObjectsOfType<RotateStandController>(true);

            for (int i = 0; i < stands.Length; i++)
            {
                RotateStandController stand = stands[i];
                if (stand == null) continue;

                OpticalComponent component = stand.GetComponent<OpticalComponent>()
                    ?? stand.GetComponentInParent<OpticalComponent>()
                    ?? stand.GetComponentInChildren<OpticalComponent>(true);

                if (component == null || standComponents.Contains(component)) continue;
                standComponents.Add(component);

                if (ContainsAny(component.gameObject.name, "新偏振", "polarizer")
                    || ContainsAny(stand.gameObject.name, "新偏振", "polarizer"))
                {
                    preferred.Add(component);
                }
            }

            List<OpticalComponent> candidates = preferred.Count >= 2 ? preferred : standComponents;
            if (candidates.Count < 2)
            {
                for (int i = 0; i < components.Length; i++)
                {
                    OpticalComponent component = components[i];
                    if (component != null
                        && ContainsAny(component.gameObject.name, "偏振", "polarizer", "analyzer")
                        && !candidates.Contains(component))
                    {
                        candidates.Add(component);
                    }
                }
            }

            Vector3 anchor = _laser != null ? _laser.transform.position : Vector3.zero;
            candidates.Sort((a, b) =>
                Vector3.Distance(a.transform.position, anchor)
                    .CompareTo(Vector3.Distance(b.transform.position, anchor)));

            for (int i = 0; i < Mathf.Min(2, candidates.Count); i++)
            {
                OpticalComponent component = candidates[i];
                _polarizers.Add(component);

                RotateStandController stand = component.GetComponent<RotateStandController>()
                    ?? component.GetComponentInChildren<RotateStandController>(true)
                    ?? component.GetComponentInParent<RotateStandController>();
                if (stand != null) _polarizerStands.Add(stand);
            }
        }

        private void BuildSteps()
        {
            _steps.Clear();

            AddMoveStep(_screen, "第一步：放置光屏",
                "点击光屏，用 WASD 将它移动到光学导轨上，按 Space 放下。");

            if (_laser != null)
            {
                _steps.Add(new GuideStep
                {
                    kind = StepKind.LaserCalibration,
                    title = "第二步：校准激光",
                    body = "点击激光器，用 WASD 微调红点，使光点对准光屏中心，按 Enter 完成校准。"
                });
            }

            if (_polarizers.Count > 0)
            {
                AddMoveStep(_polarizers[0], "第三步：放置起偏器",
                    "点击起偏器，将它移动到导轨上并按 Space 放下。");
            }

            if (_polarizers.Count > 1)
            {
                AddMoveStep(_polarizers[1], "第四步：放置检偏器",
                    "将第二个偏振片放到起偏器后方的导轨上。");
            }

            if (_polarizerStands.Count >= 2)
            {
                _steps.Add(new GuideStep
                {
                    kind = StepKind.RotateAnalyzer,
                    title = "第五步：验证消光",
                    body = "双击检偏器打开刻度盘，将检偏器旋转到与起偏器正交的位置。",
                    component = _polarizers.Count > 1 ? _polarizers[1] : null
                });
            }

            AddMoveStep(_crystal, "第六步：放置晶体盒",
                "将晶体盒放到两个偏振片之间的导轨上。");

            AddMoveStep(_beamExpander, "第七步：放置扩束镜",
                "将扩束镜放到晶体盒前方的导轨上。");

            if (_crystal != null && _beamExpander != null)
            {
                _steps.Add(new GuideStep
                {
                    kind = StepKind.ObserveConoscopic,
                    title = "第八步：观察干涉图样",
                    body = "观察右上角的锥光干涉图样，双击晶体盒可以进入特写视图。"
                });
            }

            if (_crystalController != null)
            {
                _steps.Add(new GuideStep
                {
                    kind = StepKind.RotateCrystal,
                    title = "第九步：调节晶体",
                    body = "在特写视图中操作晶体旋钮，观察干涉图样的变化。"
                });
            }

            AddRemoveStep(_screen, "第十步：移除光屏",
                "双击光屏，将它从导轨上取下。");
            AddRemoveStep(_beamExpander, "第十一步：移除扩束镜",
                "双击扩束镜，将它从导轨上取下。");
            AddMoveStep(_detector, "第十二步：放置光电探测器",
                "将光电探测器放到晶体盒后方的导轨上。");

            _steps.Add(new GuideStep
            {
                kind = StepKind.Finish,
                title = "实验装置完成",
                body = "装置已经准备完成，可以开始正式实验。"
            });
        }

        private void AddMoveStep(OpticalComponent component, string title, string body)
        {
            if (component == null) return;
            _steps.Add(new GuideStep
            {
                kind = StepKind.MoveToRail,
                title = title,
                body = body,
                component = component
            });
        }

        private void AddRemoveStep(OpticalComponent component, string title, string body)
        {
            if (component == null) return;
            _steps.Add(new GuideStep
            {
                kind = StepKind.RemoveFromRail,
                title = title,
                body = body,
                component = component
            });
        }

        private bool IsStepComplete(GuideStep step)
        {
            switch (step.kind)
            {
                case StepKind.MoveToRail:
                    return step.component != null && step.component.isOnRail;
                case StepKind.RemoveFromRail:
                    return step.component != null
                        && !step.component.isOnRail
                        && _everOnRail.Contains(step.component);
                case StepKind.LaserCalibration:
                    return _laserStepConfirmed || (_laser != null && _laser.isCalibrationDone);
                case StepKind.RotateAnalyzer:
                    return ArePolarizersOrthogonal();
                case StepKind.ObserveConoscopic:
                    return _crystal != null && _beamExpander != null
                        && _crystal.isOnRail && _beamExpander.isOnRail;
                case StepKind.RotateCrystal:
                    return _crystalController != null
                        && _crystalController.CurrentRotation.sqrMagnitude > 0.01f;
                default:
                    return false;
            }
        }

        private bool ArePolarizersOrthogonal()
        {
            if (_polarizerStands.Count < 2) return false;
            float a = _polarizerStands[0].GetCurrentRotateAngle();
            float b = _polarizerStands[1].GetCurrentRotateAngle();
            return Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(a, b)) - 90f) <= 8f;
        }

        private void UpdateRailHistory()
        {
            if (_screen != null && _screen.isOnRail) _everOnRail.Add(_screen);
            if (_crystal != null && _crystal.isOnRail) _everOnRail.Add(_crystal);
            if (_beamExpander != null && _beamExpander.isOnRail) _everOnRail.Add(_beamExpander);
            if (_detector != null && _detector.isOnRail) _everOnRail.Add(_detector);
            for (int i = 0; i < _polarizers.Count; i++)
            {
                if (_polarizers[i] != null && _polarizers[i].isOnRail)
                    _everOnRail.Add(_polarizers[i]);
            }
        }

        private void AdvanceStep()
        {
            _stepIndex++;
            _completionTimer = 0f;
            _pointerTimer = 0f;

            if (_stepIndex >= _steps.Count)
            {
                HideGuide();
                return;
            }

            ShowCurrentStep();
        }

        private void ShowCurrentStep()
        {
            if (_steps.Count == 0 || _stepIndex >= _steps.Count) return;
            GuideStep step = _steps[_stepIndex];
            _stepText.text = $"步骤 {_stepIndex + 1} / {_steps.Count}";
            _titleText.text = step.title;
            _bodyText.text = step.body;
            _buttonText.text = step.kind == StepKind.Finish ? "关闭" : "退出引导";
            _restartButton.gameObject.SetActive(step.kind == StepKind.Finish);
            SetPointerVisible(step.kind != StepKind.Finish);
        }

        private void RefreshPointer(GuideStep step)
        {
            _pointerTimer += Time.unscaledDeltaTime;
            if (!TryGetTargetPoint(step, out Vector2 targetPoint))
            {
                SetPointerVisible(false);
                return;
            }

            SetPointerVisible(step.kind != StepKind.Finish);
            _markerRect.anchoredPosition = targetPoint;
            UpdateCardArrow(targetPoint);

            if ((step.kind == StepKind.MoveToRail || step.kind == StepKind.RemoveFromRail)
                && step.component != null && step.component.isSelected)
            {
                Vector2 from = targetPoint;
                Vector2 to = targetPoint;
                if (TryWorldToCanvas(step.component.transform.position + Vector3.up * 0.25f, out from))
                {
                    if (step.kind == StepKind.MoveToRail && _rail != null)
                    {
                        Vector3 snap = _rail.GetSnapPosition(step.component.transform.position);
                        TryWorldToCanvas(snap + Vector3.up * 0.2f, out to);
                    }
                    else
                    {
                        TryWorldToCanvas(step.component.transform.position + Vector3.up * 0.7f + Vector3.left * 0.5f, out to);
                    }
                }

                float t = Mathf.PingPong(_pointerTimer / Mathf.Max(0.2f, pointerTravelDuration), 1f);
                _cursorRect.anchoredPosition = Vector2.Lerp(from, to, t);
            }
            else
            {
                _cursorRect.anchoredPosition = targetPoint + new Vector2(0f, Mathf.Sin(_pointerTimer * 3f) * 8f);
            }
        }

        private bool TryGetTargetPoint(GuideStep step, out Vector2 point)
        {
            switch (step.kind)
            {
                case StepKind.MoveToRail:
                case StepKind.RemoveFromRail:
                    return TryWorldToCanvas(step.component != null ? step.component.transform.position : Vector3.zero, out point);
                case StepKind.LaserCalibration:
                    return TryWorldToCanvas(_laser != null ? _laser.transform.position : Vector3.zero, out point);
                case StepKind.RotateAnalyzer:
                    return TryWorldToCanvas(_polarizers.Count > 1 ? _polarizers[1].transform.position : Vector3.zero, out point);
                case StepKind.ObserveConoscopic:
                case StepKind.RotateCrystal:
                    return TryWorldToCanvas(_crystal != null ? _crystal.transform.position : Vector3.zero, out point);
                default:
                    point = Vector2.zero;
                    return false;
            }
        }

        private bool TryWorldToCanvas(Vector3 worldPosition, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            Camera camera = Camera.main;
            if (camera == null || _canvasRect == null) return false;

            Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
            if (screenPoint.z <= 0f) return false;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPoint,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                out localPoint);
        }

        private void UpdateCardArrow(Vector2 targetPoint)
        {
            // card 使用左上角锚点，anchoredPosition 不是 Canvas 中心坐标。
            // 先把卡片中心从世界坐标转换到 Canvas 局部坐标，才能让连线真正贴到卡片边缘。
            Vector2 actualCardSize = _cardRect.rect.size;
            if (actualCardSize.x <= 1f || actualCardSize.y <= 1f)
            {
                actualCardSize = cardSize;
            }

            Vector2 cardCenter = _canvasRect.InverseTransformPoint(
                _cardRect.TransformPoint(_cardRect.rect.center));
            Vector2 delta = targetPoint - cardCenter;
            if (delta.sqrMagnitude < 16f)
            {
                _lineRect.gameObject.SetActive(false);
                _arrowRect.gameObject.SetActive(false);
                return;
            }

            Vector2 half = actualCardSize * 0.5f;
            float tx = Mathf.Abs(delta.x) > 0.01f ? half.x / Mathf.Abs(delta.x) : float.MaxValue;
            float ty = Mathf.Abs(delta.y) > 0.01f ? half.y / Mathf.Abs(delta.y) : float.MaxValue;
            Vector2 start = cardCenter + delta * Mathf.Clamp(Mathf.Min(tx, ty), 0f, 1f);
            Vector2 line = targetPoint - start;
            float length = line.magnitude;
            float angle = Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg;

            _lineRect.gameObject.SetActive(length > 6f);
            _lineRect.anchoredPosition = start;
            _lineRect.sizeDelta = new Vector2(length, 4f);
            _lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            _arrowRect.gameObject.SetActive(length > 6f);
            _arrowRect.anchoredPosition = targetPoint;
            _arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void BuildUI()
        {
            if (_built) return;

            _canvas = GetOrCreateWindowsCanvas();
            _canvasRect = _canvas.GetComponent<RectTransform>();
            EnsureEventSystem();
            _font = ResolveFont();

            Scene2CardGuideView existingView = FindObjectOfType<Scene2CardGuideView>(true);
            if (existingView != null && existingView.IsValid)
            {
                BindExistingView(existingView);
                _rootObject.SetActive(false);
                _built = true;
                return;
            }

            _rootObject = CreateRectObject("Scene2CardGuideRoot", _canvas.transform);
            RectTransform rootRect = _rootObject.GetComponent<RectTransform>();
            Stretch(rootRect);
            _ownsRuntimeUi = true;

            _rootGroup = _rootObject.AddComponent<CanvasGroup>();
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;

            Image overlay = _rootObject.AddComponent<Image>();
            overlay.color = overlayColor;
            overlay.raycastTarget = false;

            CreateCard();
            CreatePointerObjects();
            _rootObject.SetActive(false);
            _built = true;
        }

        private void BindExistingView(Scene2CardGuideView view)
        {
            _ownsRuntimeUi = false;
            _rootObject = view.gameObject;
            _rootGroup = view.RootGroup;
            _cardRect = view.CardRect;
            _stepText = view.StepText;
            _titleText = view.TitleText;
            _bodyText = view.BodyText;
            _closeButton = view.CloseButton;
            _buttonText = view.CloseButtonText;
            _restartButton = view.RestartButton;
            _lineRect = view.LineRect;
            _arrowRect = view.ArrowRect;
            _markerRect = view.MarkerRect;
            _cursorRect = view.CursorRect;

            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;
            _closeButton.onClick.AddListener(HideGuide);
            _restartButton.onClick.AddListener(RestartGuide);
            _restartButton.gameObject.SetActive(false);
        }

        private void CreateCard()
        {
            GameObject card = CreateRectObject("GuideCard", _rootObject.transform);
            _cardRect = card.GetComponent<RectTransform>();
            _cardRect.anchorMin = _cardRect.anchorMax = new Vector2(0f, 1f);
            _cardRect.pivot = new Vector2(0f, 1f);
            _cardRect.anchoredPosition = cardPosition;
            _cardRect.sizeDelta = cardSize;

            Image cardImage = card.AddComponent<Image>();
            cardImage.color = cardColor;
            cardImage.raycastTarget = true;
            UnityEngine.UI.Outline outline = card.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.25f);
            outline.effectDistance = new Vector2(1f, -1f);

            Image accent = CreateRectObject("AccentLine", card.transform).AddComponent<Image>();
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 4f);
            accentRect.anchoredPosition = Vector2.zero;
            accent.color = accentColor;

            _stepText = CreateText("Step", card.transform, "", 16, accentColor, TextAlignmentOptions.Left);
            SetRect(_stepText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -22f), new Vector2(180f, 22f));

            _titleText = CreateText("Title", card.transform, "", 25, Color.white, TextAlignmentOptions.Left);
            SetRect(_titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -60f), new Vector2(-22f, -27f));
            _titleText.fontStyle = FontStyles.Bold;

            _bodyText = CreateText("Body", card.transform, "", 19, Color.white, TextAlignmentOptions.TopLeft);
            SetRect(_bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -145f), new Vector2(-22f, -74f));
            _bodyText.lineSpacing = 2f;

            _closeButton = CreateButton("CloseGuide", card.transform, "退出引导", new Vector2(120f, 34f), accentColor);
            SetRect(_closeButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-142f, 18f), new Vector2(-22f, 52f));
            _closeButton.onClick.AddListener(HideGuide);
            _buttonText = _closeButton.GetComponentInChildren<TextMeshProUGUI>();

            _restartButton = CreateButton("RestartGuide", card.transform, "重新开始", new Vector2(120f, 34f), new Color(0.12f, 0.30f, 0.40f, 0.78f));
            SetRect(_restartButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-272f, 18f), new Vector2(-152f, 52f));
            _restartButton.onClick.AddListener(RestartGuide);
            _restartButton.gameObject.SetActive(false);
        }

        private void CreatePointerObjects()
        {
            GameObject marker = CreateRectObject("TargetMarker", _rootObject.transform);
            _markerRect = marker.GetComponent<RectTransform>();
            _markerRect.anchorMin = _markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            _markerRect.sizeDelta = new Vector2(76f, 76f);
            TextMeshProUGUI markerText = CreateText("Glyph", marker.transform, "O", 58, accentColor, TextAlignmentOptions.Center);
            Stretch(markerText.rectTransform);

            GameObject line = CreateRectObject("GuideArrowLine", _rootObject.transform);
            _lineRect = line.GetComponent<RectTransform>();
            _lineRect.anchorMin = _lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            _lineRect.pivot = new Vector2(0f, 0.5f);
            _lineRect.sizeDelta = new Vector2(1f, 4f);
            line.AddComponent<Image>().color = accentColor;

            GameObject arrow = CreateRectObject("GuideArrowHead", _rootObject.transform);
            _arrowRect = arrow.GetComponent<RectTransform>();
            _arrowRect.anchorMin = _arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowRect.sizeDelta = new Vector2(48f, 48f);
            TextMeshProUGUI arrowText = CreateText("Glyph", arrow.transform, ">", 34, accentColor, TextAlignmentOptions.Center);
            Stretch(arrowText.rectTransform);

            GameObject cursor = CreateRectObject("SimulatedHand", _rootObject.transform);
            _cursorRect = cursor.GetComponent<RectTransform>();
            _cursorRect.anchorMin = _cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
            _cursorRect.sizeDelta = new Vector2(68f, 68f);
            Image cursorImage = cursor.AddComponent<Image>();
            cursorImage.color = new Color(0.02f, 0.12f, 0.18f, 0.55f);
            cursorImage.raycastTarget = false;
            TextMeshProUGUI cursorText = CreateText("Glyph", cursor.transform, "+", 40, Color.white, TextAlignmentOptions.Center);
            Stretch(cursorText.rectTransform);
        }

        private void ShowRoot()
        {
            _rootObject.SetActive(true);
            _rootObject.transform.SetAsLastSibling();
            _active = true;
            _rootGroup.blocksRaycasts = true;
            _rootGroup.interactable = true;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            float start = _rootGroup.alpha;
            float elapsed = 0f;
            while (elapsed < 0.18f)
            {
                elapsed += Time.unscaledDeltaTime;
                _rootGroup.alpha = Mathf.Lerp(start, 1f, elapsed / 0.18f);
                yield return null;
            }

            _rootGroup.alpha = 1f;
            _fadeCoroutine = null;
        }

        private void SetPointerVisible(bool visible)
        {
            _markerRect.gameObject.SetActive(visible);
            _lineRect.gameObject.SetActive(visible);
            _arrowRect.gameObject.SetActive(visible);
            _cursorRect.gameObject.SetActive(visible);
        }

        private static OpticalComponent FindByName(OpticalComponent[] components, params string[] tokens)
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && ContainsAny(components[i].gameObject.name, tokens))
                    return components[i];
            }
            return null;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string lower = value.ToLowerInvariant();
            for (int i = 0; i < tokens.Length; i++)
            {
                if (lower.Contains(tokens[i].ToLowerInvariant())) return true;
            }
            return false;
        }

        private static T ReadPrivateReference<T>(MonoBehaviour owner, string fieldName, T fallback)
            where T : UnityEngine.Object
        {
            FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) return fallback;
            T value = field.GetValue(owner) as T;
            return value != null ? value : fallback;
        }

        private TMP_FontAsset ResolveFont()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(FontResourcePath);
            if (font != null) return font;

            TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null && texts[i].font.name.Contains("SIMHEI"))
                    return texts[i].font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string text, int size, Color color, TextAlignmentOptions alignment)
        {
            GameObject obj = CreateRectObject(name, parent);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            tmp.font = _font;
            return tmp;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 size, Color color)
        {
            GameObject obj = CreateRectObject(name, parent);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            Image image = obj.AddComponent<Image>();
            image.color = color;
            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
            button.colors = colors;

            TextMeshProUGUI text = CreateText("Label", obj.transform, label, 15, Color.white, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return button;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.AddComponent<RectTransform>();
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Canvas GetOrCreateWindowsCanvas()
        {
            GameObject obj = GameObject.Find(WindowsCanvasName);
            bool created = false;
            if (obj == null)
            {
                obj = new GameObject(WindowsCanvasName);
                created = true;
            }

            Canvas canvas = obj.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = obj.AddComponent<Canvas>();
                created = true;
            }

            if (created)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }

            if (obj.GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scaler = obj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (obj.GetComponent<GraphicRaycaster>() == null)
                obj.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>(true) != null) return;
            GameObject obj = new GameObject("EventSystem");
            obj.AddComponent<EventSystem>();
            obj.AddComponent<StandaloneInputModule>();
        }
    }

    internal static class Scene2CardGuideBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartCurrentScene()
        {
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, "Scene2.The Lab", StringComparison.OrdinalIgnoreCase))
                return;

            if (UnityEngine.Object.FindObjectOfType<Scene2CardGuide>(true) != null)
                return;

            GameObject obj = new GameObject("Scene2.CardGuide");
            obj.AddComponent<Scene2CardGuide>();
        }
    }
}
