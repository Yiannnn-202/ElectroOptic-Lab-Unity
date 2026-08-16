using System;
using System.Collections.Generic;
using System.Reflection;
using ElectroOptics.UI.ScreenDisplay;
using UnityEngine;

namespace ElectroOptics.UI.ExperimentGuide
{
    [DisallowMultipleComponent]
    public sealed class Scene2GuideStateProvider : MonoBehaviour, IScene2GuideStateProvider
    {
        private const string LogPrefix = "[Scene2RealtimeGuide]";

        [Header("Laser")]
        [SerializeField] private LaserEmitter laserEmitter;
        [SerializeField] private LaserEmitterMover laserMover;
        [SerializeField] private LaserStateController laserStateController;

        [Header("Screen")]
        [SerializeField] private OpticalComponent screen;
        [SerializeField] private DirectScreenController directScreenController;
        [SerializeField] private UnifiedScreenPanel unifiedScreenPanel;

        [Header("Polarizers")]
        [SerializeField] private OpticalComponent polarizer;
        [SerializeField] private RotateStandController polarizerStand;
        [SerializeField] private OpticalComponent analyzer;
        [SerializeField] private RotateStandController analyzerStand;

        [Header("Other optical components")]
        [SerializeField] private OpticalComponent beamExpander;
        [SerializeField] private OpticalComponent crystal;
        [SerializeField] private ClickAreaFocus powerMeterSceneEntry;
        [SerializeField] private ClickAreaFocus oscilloscopeSceneEntry;

        private readonly HashSet<string> fallbackWarnings = new HashSet<string>();
        private Scene2GuideScreenTelemetryReader telemetryReader;
        private bool initialized;

        public bool IsReady => initialized && ValidateReferences(false);
        public LaserEmitterMover LaserMover => laserMover;
        public LaserEmitter LaserEmitter => laserEmitter;
        public LaserStateController LaserStateController => laserStateController;
        public ClickAreaFocus PowerMeterSceneEntry => powerMeterSceneEntry;
        public ClickAreaFocus OscilloscopeSceneEntry => oscilloscopeSceneEntry;
        public Transform LaserTransform => laserEmitter != null
            ? laserEmitter.transform
            : laserMover != null ? laserMover.transform : null;

        private void Awake()
        {
            ResolveReferences(true);
        }

        public bool ResolveReferences(bool logFallback)
        {
            unifiedScreenPanel = unifiedScreenPanel != null
                ? unifiedScreenPanel
                : FindObjectOfType<UnifiedScreenPanel>(true);
            if (unifiedScreenPanel != null)
            {
                screen = ReadPrivateReference(unifiedScreenPanel, "screenOpticalComponent", screen);
                beamExpander = ReadPrivateReference(unifiedScreenPanel, "beamExpanderOpticalComponent", beamExpander);
                crystal = ReadPrivateReference(unifiedScreenPanel, "crystalOpticalComponent", crystal);
            }

            directScreenController = directScreenController != null
                ? directScreenController
                : FindObjectOfType<DirectScreenController>(true);
            if (screen == null && directScreenController != null)
            {
                screen = FindOpticalComponent(directScreenController.transform);
                WarnFallback("screen", screen, logFallback);
            }

            laserMover = laserMover != null ? laserMover : FindObjectOfType<LaserEmitterMover>(true);
            if (laserMover == null)
            {
                laserEmitter = laserEmitter != null ? laserEmitter : FindObjectOfType<LaserEmitter>(true);
                if (laserEmitter != null)
                    laserMover = laserEmitter.GetComponent<LaserEmitterMover>();
            }

            if (laserMover != null)
            {
                LaserEmitter moverEmitter = laserMover.GetComponent<LaserEmitter>();
                LaserStateController moverStateController = laserMover.GetComponent<LaserStateController>();

                if (moverEmitter != null)
                {
                    if (logFallback
                        && laserEmitter != null
                        && laserEmitter != moverEmitter
                        && fallbackWarnings.Add("laserEmitterOwnerMismatch"))
                    {
                        Debug.LogWarning(
                            $"{LogPrefix} laserEmitter 与 laserMover 不属于同一对象，已改用 `{GetHierarchyPath(moverEmitter.transform)}`。",
                            this);
                    }
                    laserEmitter = moverEmitter;
                }

                if (moverStateController != null)
                {
                    if (logFallback
                        && laserStateController != null
                        && laserStateController != moverStateController
                        && fallbackWarnings.Add("laserStateControllerOwnerMismatch"))
                    {
                        Debug.LogWarning(
                            $"{LogPrefix} laserStateController 与 laserMover 不属于同一对象，已改用 `{GetHierarchyPath(moverStateController.transform)}`。",
                            this);
                    }
                    laserStateController = moverStateController;
                }
            }

            laserEmitter = laserEmitter != null ? laserEmitter : FindObjectOfType<LaserEmitter>(true);
            laserStateController = laserStateController != null
                ? laserStateController
                : FindObjectOfType<LaserStateController>(true);

            OpticalComponent[] components = FindObjectsOfType<OpticalComponent>(true);
            if (screen == null)
            {
                screen = FindUniqueByName(components, "screen", "光屏", "screen");
                WarnFallback("screen", screen, logFallback);
            }
            if (beamExpander == null)
            {
                beamExpander = FindUniqueByName(components, "beamExpander", "扩束镜", "扩束", "beam expander");
                WarnFallback("beamExpander", beamExpander, logFallback);
            }
            if (crystal == null)
            {
                crystal = FindUniqueByName(components, "crystal", "新晶体盒", "晶体盒", "晶体", "crystal");
                WarnFallback("crystal", crystal, logFallback);
            }

            if (powerMeterSceneEntry == null)
            {
                ClickAreaFocus[] sceneEntries = FindObjectsOfType<ClickAreaFocus>(true);
                for (int i = 0; i < sceneEntries.Length; i++)
                {
                    ClickAreaFocus candidate = sceneEntries[i];
                    if (candidate != null
                        && candidate.transform.root.name == "功率计"
                        && candidate.targetSceneName == "Scene3_UIRebuild")
                    {
                        powerMeterSceneEntry = candidate;
                        WarnFallback("powerMeterSceneEntry", powerMeterSceneEntry, logFallback);
                        break;
                    }
                }
            }
            if (oscilloscopeSceneEntry == null)
            {
                ClickAreaFocus[] sceneEntries = FindObjectsOfType<ClickAreaFocus>(true);
                for (int i = 0; i < sceneEntries.Length; i++)
                {
                    ClickAreaFocus candidate = sceneEntries[i];
                    if (candidate != null
                        && candidate.transform.root.name == "示波器"
                        && candidate.targetSceneName == "Scene4_UIRebuild 1")
                    {
                        oscilloscopeSceneEntry = candidate;
                        WarnFallback("oscilloscopeSceneEntry", oscilloscopeSceneEntry, logFallback);
                        break;
                    }
                }
            }

            ResolvePolarizers(components, logFallback);

            telemetryReader = directScreenController != null
                ? new Scene2GuideScreenTelemetryReader(directScreenController)
                : null;
            initialized = true;

            bool valid = ValidateReferences(true);
            if (valid)
                Debug.Log($"{LogPrefix} Scene2 状态引用已就绪。", this);
            return valid;
        }

        public Scene2GuideStateSnapshot Capture()
        {
            Scene2GuideStateSnapshot snapshot = new Scene2GuideStateSnapshot
            {
                capturedAt = Time.realtimeSinceStartupAsDouble,
                referencesValid = ValidateReferences(false),
                screenOnRail = IsOnRail(screen),
                polarizerOnRail = IsOnRail(polarizer),
                analyzerOnRail = IsOnRail(analyzer),
                beamExpanderOnRail = IsOnRail(beamExpander),
                crystalOnRail = IsOnRail(crystal),
                powerMeterScene3Entered = powerMeterSceneEntry != null
                                          && powerMeterSceneEntry.HasEnteredTargetScene,
                oscilloscopeScene4Entered = oscilloscopeSceneEntry != null
                                            && oscilloscopeSceneEntry.HasEnteredTargetScene,
                laserCalibrationCommitted = laserMover != null && laserMover.isCalibrationDone,
                polarizerAngle = polarizerStand != null ? polarizerStand.GetCurrentRotateAngle() : 0f,
                analyzerAngle = analyzerStand != null ? analyzerStand.GetCurrentRotateAngle() : 0f,
                screenMode = unifiedScreenPanel != null ? unifiedScreenPanel.CurrentMode : ScreenMode.Direct
            };

            snapshot.polarizerDelta = Scene2GuideStageEvaluator.NormalizePolarizerDelta(
                snapshot.polarizerAngle,
                snapshot.analyzerAngle);

            float intensity = 0f;
            Vector2 spot = Vector2.zero;
            bool receivesOpticalSignal = false;
            snapshot.screenTelemetryValid = telemetryReader != null
                                            && telemetryReader.TryRead(out intensity, out spot, out receivesOpticalSignal);
            snapshot.screenIntensity = snapshot.screenTelemetryValid ? Mathf.Max(0f, intensity) : 0f;
            snapshot.screenSpotPosition = snapshot.screenTelemetryValid ? spot : Vector2.zero;
            snapshot.screenReceivesOpticalSignal = snapshot.screenTelemetryValid && receivesOpticalSignal;
            snapshot.screenReceivesEffectiveLaser = snapshot.screenTelemetryValid
                                                     && snapshot.screenIntensity > 0f
                                                     && DoesDirectBeamHitScreen();

            Transform source = LaserTransform;
            Vector3 direction = source != null ? -source.right : Vector3.zero;
            snapshot.laserDirectionValid = source != null && direction.sqrMagnitude > 0.000001f;
            if (snapshot.laserDirectionValid)
            {
                direction.Normalize();
                Vector3 origin = source.position;
                snapshot.polarizerProjection = Project(polarizer, origin, direction);
                snapshot.analyzerProjection = Project(analyzer, origin, direction);
                snapshot.beamExpanderProjection = Project(beamExpander, origin, direction);
                snapshot.crystalProjection = Project(crystal, origin, direction);
                snapshot.screenProjection = Project(screen, origin, direction);

            }

            return snapshot;
        }

        private void ResolvePolarizers(OpticalComponent[] components, bool logFallback)
        {
            // Scene 2 uses the same prefab for both optical elements.  The scene's
            // serialized references can therefore be swapped without Unity reporting
            // a missing reference.  Prefer the explicit teaching labels whenever they
            // are available, so the guide always treats the named 起偏器 as the
            // polarizer and the named 检偏器 as the analyzer.
            OpticalComponent namedPolarizer = FindUniqueByName(components, "polarizer", "起偏器");
            OpticalComponent namedAnalyzer = FindUniqueByName(components, "analyzer", "检偏器");
            if (namedPolarizer != null && namedAnalyzer != null && namedPolarizer != namedAnalyzer)
            {
                RotateStandController namedPolarizerStand = FindRotateStand(namedPolarizer);
                RotateStandController namedAnalyzerStand = FindRotateStand(namedAnalyzer);
                bool corrected = polarizer != namedPolarizer
                                 || analyzer != namedAnalyzer
                                 || (namedPolarizerStand != null && polarizerStand != namedPolarizerStand)
                                 || (namedAnalyzerStand != null && analyzerStand != namedAnalyzerStand);

                polarizer = namedPolarizer;
                analyzer = namedAnalyzer;
                if (namedPolarizerStand != null)
                    polarizerStand = namedPolarizerStand;
                if (namedAnalyzerStand != null)
                    analyzerStand = namedAnalyzerStand;

                if (corrected && logFallback && fallbackWarnings.Add("polarizerRoleCorrection"))
                {
                    Debug.LogWarning(
                        $"{LogPrefix} 已按场景名称修正起偏器/检偏器引用：起偏器为 `{GetHierarchyPath(polarizer.transform)}`，检偏器为 `{GetHierarchyPath(analyzer.transform)}`。",
                        this);
                }
                return;
            }

            if (polarizer != null && analyzer != null && polarizerStand != null && analyzerStand != null)
                return;

            List<KeyValuePair<OpticalComponent, RotateStandController>> candidates =
                new List<KeyValuePair<OpticalComponent, RotateStandController>>();
            RotateStandController[] stands = FindObjectsOfType<RotateStandController>(true);
            for (int i = 0; i < stands.Length; i++)
            {
                OpticalComponent component = FindOpticalComponent(stands[i].transform);
                if (component == null)
                    continue;

                bool duplicate = false;
                for (int j = 0; j < candidates.Count; j++)
                {
                    if (candidates[j].Key == component)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    candidates.Add(new KeyValuePair<OpticalComponent, RotateStandController>(component, stands[i]));
            }

            Transform source = LaserTransform;
            Vector3 origin = source != null ? source.position : Vector3.zero;
            Vector3 direction = source != null ? -source.right : Vector3.right;
            if (direction.sqrMagnitude < 0.000001f)
                direction = Vector3.right;
            direction.Normalize();
            candidates.Sort((a, b) => Project(a.Key, origin, direction).CompareTo(Project(b.Key, origin, direction)));

            if (candidates.Count >= 2)
            {
                if (polarizer == null) polarizer = candidates[0].Key;
                if (polarizerStand == null) polarizerStand = candidates[0].Value;
                if (analyzer == null) analyzer = candidates[1].Key;
                if (analyzerStand == null) analyzerStand = candidates[1].Value;
                WarnFallback("polarizer/analyzer", polarizer, logFallback);
            }
        }

        private bool ValidateReferences(bool logErrors)
        {
            List<string> missing = new List<string>();
            if (laserEmitter == null) missing.Add("laserEmitter");
            if (laserMover == null) missing.Add("laserMover");
            if (laserStateController == null) missing.Add("laserStateController");
            if (screen == null) missing.Add("screen");
            if (directScreenController == null) missing.Add("directScreenController");
            if (unifiedScreenPanel == null) missing.Add("unifiedScreenPanel");
            if (polarizer == null || polarizerStand == null) missing.Add("polarizer");
            if (analyzer == null || analyzerStand == null) missing.Add("analyzer");
            if (beamExpander == null) missing.Add("beamExpander");
            if (crystal == null) missing.Add("crystal");
            if (powerMeterSceneEntry == null) missing.Add("powerMeterSceneEntry");
            if (oscilloscopeSceneEntry == null) missing.Add("oscilloscopeSceneEntry");

            if (missing.Count == 0)
                return true;

            if (logErrors)
                Debug.LogError($"{LogPrefix} 缺少关键场景引用：{string.Join(", ", missing)}。相关阶段将保持等待。", this);
            return false;
        }

        private bool DoesDirectBeamHitScreen()
        {
            if (laserEmitter == null || screen == null)
                return false;

            Vector3 direction = -laserEmitter.transform.right;
            if (direction.sqrMagnitude < 0.000001f)
                return false;
            direction.Normalize();

            Vector3 origin = laserEmitter.transform.position + direction * laserEmitter.startOffset;
            RaycastHit hit;
            if (!Physics.Raycast(origin, direction, out hit, 50f))
                return false;

            return IsSameHierarchy(hit.collider != null ? hit.collider.transform : null, screen.transform);
        }

        private static float Project(OpticalComponent component, Vector3 origin, Vector3 direction)
        {
            return component != null
                ? Vector3.Dot(component.transform.position - origin, direction)
                : float.NaN;
        }

        private static bool IsOnRail(OpticalComponent component)
        {
            return component != null && component.isOnRail;
        }

        private static bool IsSameHierarchy(Transform left, Transform right)
        {
            if (left == null || right == null)
                return false;
            return left == right || left.IsChildOf(right) || right.IsChildOf(left);
        }

        private static OpticalComponent FindOpticalComponent(Transform owner)
        {
            if (owner == null)
                return null;
            return owner.GetComponent<OpticalComponent>()
                   ?? owner.GetComponentInParent<OpticalComponent>()
                   ?? owner.GetComponentInChildren<OpticalComponent>(true);
        }

        private static RotateStandController FindRotateStand(OpticalComponent component)
        {
            if (component == null)
                return null;

            return component.GetComponent<RotateStandController>()
                   ?? component.GetComponentInParent<RotateStandController>()
                   ?? component.GetComponentInChildren<RotateStandController>(true);
        }

        private OpticalComponent FindUniqueByName(
            OpticalComponent[] components,
            string fieldName,
            params string[] tokens)
        {
            List<OpticalComponent> matches = new List<OpticalComponent>();
            for (int i = 0; i < components.Length; i++)
            {
                OpticalComponent component = components[i];
                if (component == null)
                    continue;

                string path = GetHierarchyPath(component.transform).ToLowerInvariant();
                for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    if (path.Contains(tokens[tokenIndex].ToLowerInvariant()))
                    {
                        matches.Add(component);
                        break;
                    }
                }
            }

            if (matches.Count == 1)
                return matches[0];

            if (matches.Count > 1)
                Debug.LogError($"{LogPrefix} 名称回退 `{fieldName}` 匹配到 {matches.Count} 个对象，拒绝自动选择。", this);
            return null;
        }

        private void WarnFallback(string fieldName, UnityEngine.Object value, bool enabled)
        {
            if (!enabled || value == null || !fallbackWarnings.Add(fieldName))
                return;

            Component component = value as Component;
            string path = component != null ? GetHierarchyPath(component.transform) : value.name;
            Debug.LogWarning($"{LogPrefix} `{fieldName}` 使用兼容回退引用：{path}。建议通过 Inspector 固定引用。", this);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static T ReadPrivateReference<T>(object owner, string fieldName, T fallback)
            where T : UnityEngine.Object
        {
            if (owner == null)
                return fallback;

            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return fallback;

            T value = field.GetValue(owner) as T;
            return value != null ? value : fallback;
        }
    }

    internal sealed class Scene2GuideScreenTelemetryReader
    {
        private const string LogPrefix = "[Scene2RealtimeGuide]";
        private readonly DirectScreenController controller;
        private bool errorLogged;

        public Scene2GuideScreenTelemetryReader(DirectScreenController target)
        {
            controller = target;
        }

        public bool TryRead(out float intensity, out Vector2 spotPosition, out bool receivesOpticalSignal)
        {
            intensity = 0f;
            spotPosition = Vector2.zero;
            receivesOpticalSignal = false;
            if (controller == null)
            {
                LogErrorOnce("DirectScreenController 缺失，无法读取屏幕强度或红点位置。");
                return false;
            }

            try
            {
                intensity = controller.CurrentIntensity;
                spotPosition = controller.CurrentSpotPosition;
                receivesOpticalSignal = controller.HasRecentOpticalSignal;
                float x = spotPosition.x;
                float y = spotPosition.y;
                bool valid = !float.IsNaN(intensity)
                             && !float.IsInfinity(intensity)
                             && !float.IsNaN(x)
                             && !float.IsInfinity(x)
                             && !float.IsNaN(y)
                             && !float.IsInfinity(y);
                if (!valid)
                    LogErrorOnce("DirectScreenController 返回了非有限遥测值。");
                return valid;
            }
            catch (Exception exception)
            {
                LogErrorOnce("读取 DirectScreenController 遥测失败：" + exception.Message);
                return false;
            }
        }

        private void LogErrorOnce(string message)
        {
            if (errorLogged)
                return;
            errorLogged = true;
            Debug.LogError($"{LogPrefix} {message}", controller);
        }
    }
}
