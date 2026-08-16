using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.UI.ExperimentGuide
{
    /// <summary>
    /// Scene2 六阶段实时操作指引的流程编排器。
    /// 状态采集、纯判定、UI、GIF 和交互锁均由独立组件负责。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class Scene2CardGuide : MonoBehaviour
    {
        private const string WindowsCanvasName = "WindowsCanvas";
        private const string FontResourcePath = "Fonts/SIMHEI SDF";

        [Header("启动")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float autoStartDelay = 0.4f;
        [SerializeField] private KeyCode toggleShortcut = KeyCode.F1;

        [Header("阶段")]
        [SerializeField] private List<Scene2GuideStageDefinition> stages =
            Scene2GuideStageDefinition.CreateDefaults();
        [SerializeField] private Scene2GuideSettings settings = new Scene2GuideSettings();

        [Header("运行时组件")]
        [SerializeField] private Scene2GuideStateProvider stateProvider;
        [SerializeField] private Scene2CardGuideView view;
        [SerializeField] private Scene2GuideLaserCalibrationGate calibrationGate;
        [SerializeField] private Scene2GuideCameraVisibility cameraVisibility;
        [SerializeField] private Scene2GuideInteractionLock interactionLock;
        [SerializeField] private Scene2GuideGifPopup gifPopup;

        private int stageIndex;
        private float conditionTimer;
        private float feedbackTimer;
        private float calibrationCenteredTimer;
        private Scene2GuideSessionState sessionState;
        private bool initialized;
        private bool sessionActive;
        private bool inFeedback;
        private bool finished;
        private bool expanded = true;
        private string currentStatus = string.Empty;
        private TMP_FontAsset font;

        public Scene2GuideStageId CurrentStageId => finished || stages == null || stages.Count == 0
            ? Scene2GuideStageId.InstallPhotodiodeProbe
            : stages[Mathf.Clamp(stageIndex, 0, stages.Count - 1)].id;
        public bool IsFinished => finished;
        public bool IsExpanded => expanded;
        public bool IsModalOpen => gifPopup != null && gifPopup.IsOpen;

        private void Start()
        {
            StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            yield return null;
            yield return null;

            EnsureStageDefinitions();
            EnsureDependencies();
            BuildOrBindUi();

            if (stateProvider == null || !stateProvider.ResolveReferences(true))
                Debug.LogError("[Scene2RealtimeGuide] 状态提供器未就绪，引导会显示但不会放宽阶段条件。", this);

            calibrationGate.Configure(
                stateProvider != null ? stateProvider.LaserMover : null,
                stateProvider != null ? stateProvider.LaserStateController : null);
            calibrationGate.CalibrationCommitted += OnCalibrationCommitted;
            cameraVisibility.CaptureDefaultPose(
                Camera.main != null ? Camera.main.transform : null);

            Canvas canvas = GetOrCreateWindowsCanvas();
            gifPopup.Initialize(canvas, font, interactionLock, settings.gifFrameBufferCapacity);
            gifPopup.OpenStateChanged += OnPopupStateChanged;

            view.HeaderButton.onClick.RemoveListener(ToggleExpanded);
            view.HeaderButton.onClick.AddListener(ToggleExpanded);
            view.VideoButton.onClick.RemoveListener(OpenCurrentStageVideo);
            view.VideoButton.onClick.AddListener(OpenCurrentStageVideo);
            view.ConfigureAnimation(settings.foldAnimationSeconds);
            expanded = true;
            view.SetExpanded(true, true);
            view.SetVisible(false);

            initialized = true;
            if (autoStart)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, autoStartDelay));
                StartGuide();
            }
        }

        private void Update()
        {
            if (!initialized || view == null)
                return;

            bool cameraAllowsCard = cameraVisibility != null
                                    && cameraVisibility.Tick(settings, Time.unscaledDeltaTime);
            view.SetVisible(cameraAllowsCard);

            if (!sessionActive || IsModalOpen)
                return;

            if (Input.GetKeyDown(toggleShortcut))
                ToggleExpanded();

            if (finished)
                return;

            if (inFeedback)
            {
                feedbackTimer += Time.unscaledDeltaTime;
                if (feedbackTimer >= settings.completionFeedbackSeconds)
                    AdvanceAfterFeedback();
                return;
            }

            Scene2GuideStateSnapshot snapshot = stateProvider.Capture();
            Scene2GuideStageId stageId = stages[stageIndex].id;

            if (stageId == Scene2GuideStageId.CalibrateLaser)
            {
                UpdateCalibration(snapshot);
                RenderCurrentStage();
                return;
            }

            Scene2GuideStageEvaluation evaluation = Scene2GuideStageEvaluator.Evaluate(
                stageId,
                snapshot,
                sessionState,
                settings);
            currentStatus = evaluation.statusMessage ?? string.Empty;
            if (evaluation.completionConditionMet)
            {
                conditionTimer += Time.unscaledDeltaTime;
                if (conditionTimer >= settings.conditionStableSeconds)
                    BeginFeedback();
            }
            else
            {
                conditionTimer = 0f;
            }

            RenderCurrentStage();
        }

        public void StartGuide()
        {
            if (!initialized)
                return;

            stageIndex = 0;
            conditionTimer = 0f;
            feedbackTimer = 0f;
            calibrationCenteredTimer = 0f;
            sessionState = default(Scene2GuideSessionState);
            inFeedback = false;
            finished = false;
            sessionActive = true;
            currentStatus = string.Empty;
            expanded = true;
            view.SetExpanded(true, true);
            view.SetCompletionFeedback(false);

            // 从会话开始即接管旧 mover，防止用户在第二阶段之前用非法 Enter 提前锁定。
            if (stateProvider != null
                && stateProvider.LaserMover != null
                && !stateProvider.LaserMover.isCalibrationDone)
            {
                calibrationGate.SetCalibrationActive(true);
                calibrationGate.SetCenteredReady(false);
            }

            RenderCurrentStage();
        }

        public void ToggleExpanded()
        {
            if (!initialized || IsModalOpen)
                return;
            expanded = !expanded;
            view.SetExpanded(expanded, false);
        }

        private void UpdateCalibration(Scene2GuideStateSnapshot snapshot)
        {
            bool centered = Scene2GuideStageEvaluator.IsCalibrationCentered(snapshot, settings);
            calibrationCenteredTimer = centered
                ? calibrationCenteredTimer + Time.unscaledDeltaTime
                : 0f;
            sessionState.calibrationCenteredReady = centered
                                                        && calibrationCenteredTimer
                                                        >= settings.conditionStableSeconds;
            calibrationGate.SetCalibrationActive(!snapshot.laserCalibrationCommitted);
            calibrationGate.SetCenteredReady(sessionState.calibrationCenteredReady);

            if (snapshot.laserCalibrationCommitted)
            {
                BeginFeedback();
                return;
            }

            currentStatus = sessionState.calibrationCenteredReady
                ? "红点已居中，请按 Enter 锁定校准"
                : string.Empty;
        }

        private void OnCalibrationCommitted()
        {
            if (!initialized || finished || inFeedback || IsModalOpen)
                return;
            if (stages[stageIndex].id != Scene2GuideStageId.CalibrateLaser)
                return;
            BeginFeedback();
        }

        private void BeginFeedback()
        {
            if (inFeedback || finished)
                return;

            inFeedback = true;
            feedbackTimer = 0f;
            conditionTimer = 0f;
            currentStatus = "阶段完成";
            if (stages[stageIndex].id == Scene2GuideStageId.CalibrateLaser)
                calibrationGate.SetCalibrationActive(false);
            view.SetCompletionFeedback(true);
            RenderCurrentStage();
            Debug.Log($"[Scene2RealtimeGuide] 阶段完成：{stages[stageIndex].id}", this);
        }

        private void AdvanceAfterFeedback()
        {
            inFeedback = false;
            feedbackTimer = 0f;
            conditionTimer = 0f;
            currentStatus = string.Empty;
            view.SetCompletionFeedback(false);

            if (stageIndex >= stages.Count - 1)
            {
                finished = true;
                calibrationGate.SetCalibrationActive(false);
                view.SetStage(0, stages.Count, string.Empty, string.Empty, string.Empty, true);
                return;
            }

            stageIndex++;
            calibrationCenteredTimer = 0f;
            RenderCurrentStage();
        }

        private void RenderCurrentStage()
        {
            if (view == null)
                return;

            if (finished)
            {
                view.SetStage(0, stages.Count, string.Empty, string.Empty, string.Empty, true);
                return;
            }

            Scene2GuideStageDefinition definition = stages[stageIndex];
            view.SetStage(
                stageIndex + 1,
                stages.Count,
                definition.title,
                definition.body,
                currentStatus,
                false);
        }

        private void OpenCurrentStageVideo()
        {
            if (!initialized || finished || inFeedback || gifPopup == null)
                return;
            gifPopup.Open(stages[stageIndex]);
        }

        private void OnPopupStateChanged(bool open)
        {
            if (!open)
                RenderCurrentStage();
        }

        private void EnsureDependencies()
        {
            stateProvider = stateProvider != null
                ? stateProvider
                : GetComponent<Scene2GuideStateProvider>();
            if (stateProvider == null)
                stateProvider = gameObject.AddComponent<Scene2GuideStateProvider>();

            calibrationGate = calibrationGate != null
                ? calibrationGate
                : GetComponent<Scene2GuideLaserCalibrationGate>();
            if (calibrationGate == null)
                calibrationGate = gameObject.AddComponent<Scene2GuideLaserCalibrationGate>();

            cameraVisibility = cameraVisibility != null
                ? cameraVisibility
                : GetComponent<Scene2GuideCameraVisibility>();
            if (cameraVisibility == null)
                cameraVisibility = gameObject.AddComponent<Scene2GuideCameraVisibility>();

            interactionLock = interactionLock != null
                ? interactionLock
                : GetComponent<Scene2GuideInteractionLock>();
            if (interactionLock == null)
                interactionLock = gameObject.AddComponent<Scene2GuideInteractionLock>();

            gifPopup = gifPopup != null ? gifPopup : GetComponent<Scene2GuideGifPopup>();
            if (gifPopup == null)
                gifPopup = gameObject.AddComponent<Scene2GuideGifPopup>();
        }

        private void BuildOrBindUi()
        {
            Canvas canvas = GetOrCreateWindowsCanvas();
            font = ResolveFont();

            if (view == null)
                view = FindObjectOfType<Scene2CardGuideView>(true);
            if (view != null && !view.IsValid)
            {
                GameObject obsoleteRoot = view.gameObject;
                view = null;
                Debug.LogWarning("[Scene2RealtimeGuide] 检测到旧卡片结构，运行时将使用新结构。请执行 Editor 升级工具保存场景。", this);
                Destroy(obsoleteRoot);
            }

            if (view == null)
                view = Scene2CardGuideView.Create(canvas.transform, font);
            view.transform.SetAsLastSibling();
        }

        private void EnsureStageDefinitions()
        {
            bool invalid = stages == null || stages.Count != 6;
            if (!invalid)
            {
                HashSet<Scene2GuideStageId> ids = new HashSet<Scene2GuideStageId>();
                List<Scene2GuideStageDefinition> defaults = Scene2GuideStageDefinition.CreateDefaults();
                for (int i = 0; i < stages.Count; i++)
                {
                    Scene2GuideStageDefinition stage = stages[i];
                    if (stage == null
                        || stage.id != defaults[i].id
                        || !ids.Add(stage.id)
                        || string.IsNullOrWhiteSpace(stage.title)
                        || string.IsNullOrWhiteSpace(stage.body)
                        || !string.Equals(
                            stage.gifFileName,
                            defaults[i].gifFileName,
                            StringComparison.Ordinal))
                    {
                        invalid = true;
                        break;
                    }
                }
            }

            if (invalid)
            {
                stages = Scene2GuideStageDefinition.CreateDefaults();
                Debug.LogWarning("[Scene2RealtimeGuide] 阶段配置无效，已恢复六阶段默认定义。", this);
            }
        }

        private TMP_FontAsset ResolveFont()
        {
            TMP_FontAsset resolved = Resources.Load<TMP_FontAsset>(FontResourcePath);
            if (resolved != null)
                return resolved;

            TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null
                    && texts[i].font != null
                    && texts[i].font.name.IndexOf("SIMHEI", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return texts[i].font;
                }
            }
            return TMP_Settings.defaultFontAsset;
        }

        private static Canvas GetOrCreateWindowsCanvas()
        {
            GameObject obj = GameObject.Find(WindowsCanvasName);
            if (obj == null)
                obj = new GameObject(WindowsCanvasName, typeof(RectTransform));

            Canvas canvas = obj.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = obj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }

            CanvasScaler scaler = obj.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = obj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (obj.GetComponent<GraphicRaycaster>() == null)
                obj.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>(true) != null)
                return;
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void OnDestroy()
        {
            if (calibrationGate != null)
            {
                calibrationGate.CalibrationCommitted -= OnCalibrationCommitted;
                calibrationGate.SetCalibrationActive(false);
            }
            if (gifPopup != null)
            {
                gifPopup.OpenStateChanged -= OnPopupStateChanged;
                gifPopup.Close();
            }
            if (view != null)
            {
                view.HeaderButton.onClick.RemoveListener(ToggleExpanded);
                view.VideoButton.onClick.RemoveListener(OpenCurrentStageVideo);
            }
            interactionLock?.ForceReleaseAll();
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
