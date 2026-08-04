#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ElectroOptics.UI.ScreenDisplay;
using UnityEditor;
using UnityEngine;

namespace ElectroOptics.UI.ExperimentGuide.Editor
{
    public static class Scene2RealtimeGuideTests
    {
        private static int passed;
        private static int failed;

        [MenuItem("ElectroOptics/Tests/Run Scene2 Realtime Guide Tests")]
        public static void RunAll()
        {
            passed = 0;
            failed = 0;

            Run("Stage definitions are fixed", TestStageDefinitions);
            Run("Polarizer angle normalization", TestPolarizerAngleNormalization);
            Run("Strict rail order", TestStrictRailOrder);
            Run("Place screen", TestPlaceScreen);
            Run("Calibration 64 x 64 area", TestCalibrationArea);
            Run("Calibration commit", TestCalibrationCommit);
            Run("Calibration uses mover-owned laser references", TestCalibrationReferenceAffinity);
            Run("Extinction requires baseline", TestExtinctionRequiresBaseline);
            Run("Extinction threshold", TestExtinctionThreshold);
            Run("Conoscopic path and mode", TestConoscopic);
            Run("Power meter Scene3 entry", TestPowerMeterScene3Entry);
            Run("Oscilloscope Scene4 entry", TestOscilloscopeScene4Entry);
            Run("Invalid references fail closed", TestInvalidReferencesFailClosed);

            Debug.Log($"[Scene2RealtimeGuideTests] 完成：{passed} 通过，{failed} 失败。");
        }

        public static void RunAllForBatch()
        {
            RunAll();
            if (failed > 0)
                throw new InvalidOperationException($"Scene2 realtime guide tests failed: {failed}");
        }

        private static void TestStageDefinitions()
        {
            List<Scene2GuideStageDefinition> definitions = Scene2GuideStageDefinition.CreateDefaults();
            AssertEqual(6, definitions.Count, "stage count");
            AssertEqual(Scene2GuideStageId.PlaceScreen, definitions[0].id, "stage 1");
            AssertEqual(Scene2GuideStageId.CalibrateLaser, definitions[1].id, "stage 2");
            AssertEqual(Scene2GuideStageId.VerifyExtinction, definitions[2].id, "stage 3");
            AssertEqual(Scene2GuideStageId.ObserveConoscopic, definitions[3].id, "stage 4");
            AssertEqual(Scene2GuideStageId.InstallPowerMeterProbe, definitions[4].id, "stage 5");
            AssertEqual(Scene2GuideStageId.InstallPhotodiodeProbe, definitions[5].id, "stage 6");
            AssertEqual("InstallPhotodiodeProbe.gif", definitions[5].gifFileName, "gif mapping");
        }

        private static void TestPolarizerAngleNormalization()
        {
            AssertClose(0f, Scene2GuideStageEvaluator.NormalizePolarizerDelta(0f, 180f), 0.001f, "180 periodic");
            AssertClose(8f, Scene2GuideStageEvaluator.NormalizePolarizerDelta(356f, 4f), 0.001f, "wrap around");
            AssertClose(90f, Scene2GuideStageEvaluator.NormalizePolarizerDelta(10f, 100f), 0.001f, "orthogonal");
        }

        private static void TestStrictRailOrder()
        {
            AssertTrue(Scene2GuideStageEvaluator.HasOrder(0.01f, 1f, 2f, 3f), "increasing");
            AssertFalse(Scene2GuideStageEvaluator.HasOrder(0.01f, 1f, 0.5f, 3f), "reversed");
            AssertFalse(Scene2GuideStageEvaluator.HasOrder(0.01f, 1f, 1.005f), "epsilon");
            AssertFalse(Scene2GuideStageEvaluator.HasOrder(0.01f, -1f, 2f), "behind laser");
            AssertFalse(Scene2GuideStageEvaluator.HasOrder(0.01f, 1f, float.NaN), "invalid projection");
        }

        private static void TestPlaceScreen()
        {
            Scene2GuideStateSnapshot snapshot = BaseSnapshot();
            snapshot.screenOnRail = true;
            snapshot.screenTelemetryValid = true;
            snapshot.screenReceivesEffectiveLaser = true;
            snapshot.screenIntensity = 1f;
            AssertComplete(Scene2GuideStageId.PlaceScreen, snapshot, default(Scene2GuideSessionState));

            snapshot.screenReceivesEffectiveLaser = false;
            AssertIncomplete(Scene2GuideStageId.PlaceScreen, snapshot, default(Scene2GuideSessionState));
        }

        private static void TestCalibrationArea()
        {
            Scene2GuideSettings settings = Settings();
            Scene2GuideStateSnapshot snapshot = BaseSnapshot();
            snapshot.screenOnRail = true;
            snapshot.screenTelemetryValid = true;
            snapshot.screenReceivesEffectiveLaser = true;
            snapshot.screenSpotPosition = new Vector2(288f, 288f);
            AssertTrue(Scene2GuideStageEvaluator.IsCalibrationCentered(snapshot, settings), "64 x 64 corner boundary");

            snapshot.screenSpotPosition = new Vector2(288.01f, 288f);
            AssertFalse(Scene2GuideStageEvaluator.IsCalibrationCentered(snapshot, settings), "outside 64 x 64 area");
        }

        private static void TestCalibrationCommit()
        {
            Scene2GuideStateSnapshot snapshot = BaseSnapshot();
            snapshot.laserCalibrationCommitted = false;
            AssertIncomplete(Scene2GuideStageId.CalibrateLaser, snapshot, default(Scene2GuideSessionState));
            snapshot.laserCalibrationCommitted = true;
            AssertComplete(Scene2GuideStageId.CalibrateLaser, snapshot, default(Scene2GuideSessionState));
        }

        private static void TestCalibrationReferenceAffinity()
        {
            GameObject moverObject = new GameObject("GuideTest_MoverLaser");
            GameObject unrelatedObject = new GameObject("GuideTest_UnrelatedLaser");
            GameObject providerObject = new GameObject("GuideTest_Provider");
            try
            {
                LaserEmitterMover mover = moverObject.AddComponent<LaserEmitterMover>();
                LaserEmitter moverEmitter = moverObject.AddComponent<LaserEmitter>();
                LaserStateController moverStateController = moverObject.AddComponent<LaserStateController>();
                LaserEmitter unrelatedEmitter = unrelatedObject.AddComponent<LaserEmitter>();
                LaserStateController unrelatedStateController = unrelatedObject.AddComponent<LaserStateController>();
                Scene2GuideStateProvider provider = providerObject.AddComponent<Scene2GuideStateProvider>();

                SerializedObject serialized = new SerializedObject(provider);
                serialized.FindProperty("laserMover").objectReferenceValue = mover;
                serialized.FindProperty("laserEmitter").objectReferenceValue = unrelatedEmitter;
                serialized.FindProperty("laserStateController").objectReferenceValue = unrelatedStateController;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                provider.ResolveReferences(false);
                AssertEqual(moverEmitter, provider.LaserEmitter, "laser emitter follows mover owner");
                AssertEqual(moverStateController, provider.LaserStateController, "state controller follows mover owner");

                Scene2GuideLaserCalibrationGate gate = providerObject.AddComponent<Scene2GuideLaserCalibrationGate>();
                gate.Configure(mover, moverStateController);
                gate.SetCalibrationActive(true);
                gate.SetCenteredReady(true);
                AssertFalse(moverStateController.IsSelected, "laser starts unselected");
                AssertTrue(gate.CanAcceptCommit, "global-view commit does not require selection");
                gate.SetCalibrationActive(false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(providerObject);
                UnityEngine.Object.DestroyImmediate(unrelatedObject);
                UnityEngine.Object.DestroyImmediate(moverObject);
            }
        }

        private static void TestExtinctionRequiresBaseline()
        {
            Scene2GuideStateSnapshot snapshot = ExtinctionSnapshot();
            Scene2GuideSessionState session = default(Scene2GuideSessionState);
            Scene2GuideStageEvaluation result = Scene2GuideStageEvaluator.Evaluate(
                Scene2GuideStageId.VerifyExtinction,
                snapshot,
                session,
                Settings());
            AssertFalse(result.completionConditionMet, "no baseline");
            AssertEqual(Scene2GuideStageEvaluator.MissingBrightBaselineMessage, result.statusMessage, "baseline message");
        }

        private static void TestExtinctionThreshold()
        {
            Scene2GuideStateSnapshot snapshot = ExtinctionSnapshot();
            Scene2GuideSessionState session = new Scene2GuideSessionState
            {
                brightBaselineReady = true,
                brightBaseline = 1f
            };

            snapshot.screenIntensity = 0.05f;
            AssertComplete(Scene2GuideStageId.VerifyExtinction, snapshot, session);
            snapshot.screenIntensity = 0.0501f;
            AssertIncomplete(Scene2GuideStageId.VerifyExtinction, snapshot, session);
            snapshot.polarizerDelta = 81.9f;
            AssertIncomplete(Scene2GuideStageId.VerifyExtinction, snapshot, session);
        }

        private static void TestConoscopic()
        {
            Scene2GuideStateSnapshot snapshot = BaseSnapshot();
            snapshot.screenOnRail = true;
            snapshot.polarizerOnRail = true;
            snapshot.beamExpanderOnRail = true;
            snapshot.crystalOnRail = true;
            snapshot.analyzerOnRail = true;
            snapshot.polarizerProjection = 1f;
            snapshot.beamExpanderProjection = 2f;
            snapshot.crystalProjection = 3f;
            snapshot.analyzerProjection = 4f;
            snapshot.screenProjection = 5f;
            snapshot.screenMode = ScreenMode.Conoscopic;
            AssertComplete(Scene2GuideStageId.ObserveConoscopic, snapshot, default(Scene2GuideSessionState));

            snapshot.analyzerProjection = 2.5f;
            AssertIncomplete(Scene2GuideStageId.ObserveConoscopic, snapshot, default(Scene2GuideSessionState));
        }

        private static void TestPowerMeterScene3Entry()
        {
            Scene2GuideStateSnapshot snapshot = BaseSnapshot();

            Scene2GuideStageEvaluation waitingForDataProcessing = Scene2GuideStageEvaluator.Evaluate(
                Scene2GuideStageId.InstallPowerMeterProbe,
                snapshot,
                default(Scene2GuideSessionState),
                Settings());
            AssertFalse(waitingForDataProcessing.completionConditionMet, "Scene3 entry through power meter is required");
            AssertEqual(
                Scene2GuideStageEvaluator.OpenPowerMeterDataProcessingMessage,
                waitingForDataProcessing.statusMessage,
                "power meter click prompt");

            snapshot.powerMeterScene3Entered = true;
            AssertComplete(Scene2GuideStageId.InstallPowerMeterProbe, snapshot, default(Scene2GuideSessionState));

            // 阶段 5 不再检测接收器探头或光路安装状态。
            snapshot.screenOnRail = true;
            snapshot.beamExpanderOnRail = true;
            AssertComplete(Scene2GuideStageId.InstallPowerMeterProbe, snapshot, default(Scene2GuideSessionState));
        }

        private static void TestOscilloscopeScene4Entry()
        {
            Scene2GuideStateSnapshot snapshot = BaseSnapshot();

            Scene2GuideStageEvaluation waitingForDataProcessing = Scene2GuideStageEvaluator.Evaluate(
                Scene2GuideStageId.InstallPhotodiodeProbe,
                snapshot,
                default(Scene2GuideSessionState),
                Settings());
            AssertFalse(waitingForDataProcessing.completionConditionMet, "Scene4 entry through oscilloscope is required");
            AssertEqual(
                Scene2GuideStageEvaluator.OpenOscilloscopeDataProcessingMessage,
                waitingForDataProcessing.statusMessage,
                "oscilloscope click prompt");

            snapshot.oscilloscopeScene4Entered = true;
            AssertComplete(Scene2GuideStageId.InstallPhotodiodeProbe, snapshot, default(Scene2GuideSessionState));

            // 阶段 6 不再检测光电二极管探头或光路安装状态。
            snapshot.screenOnRail = true;
            snapshot.beamExpanderOnRail = true;
            AssertComplete(Scene2GuideStageId.InstallPhotodiodeProbe, snapshot, default(Scene2GuideSessionState));
        }

        private static void TestInvalidReferencesFailClosed()
        {
            Scene2GuideStateSnapshot snapshot = BaseSnapshot();
            snapshot.referencesValid = false;
            snapshot.screenOnRail = true;
            snapshot.screenTelemetryValid = true;
            snapshot.screenReceivesEffectiveLaser = true;
            snapshot.screenIntensity = 1f;
            AssertIncomplete(Scene2GuideStageId.PlaceScreen, snapshot, default(Scene2GuideSessionState));
        }

        private static Scene2GuideStateSnapshot BaseSnapshot()
        {
            return new Scene2GuideStateSnapshot
            {
                referencesValid = true,
                laserDirectionValid = true,
                screenTelemetryValid = true
            };
        }

        private static Scene2GuideStateSnapshot ExtinctionSnapshot()
        {
            Scene2GuideStateSnapshot snapshot = BaseSnapshot();
            snapshot.polarizerOnRail = true;
            snapshot.analyzerOnRail = true;
            snapshot.screenOnRail = true;
            snapshot.polarizerProjection = 1f;
            snapshot.analyzerProjection = 2f;
            snapshot.screenProjection = 3f;
            snapshot.polarizerDelta = 90f;
            snapshot.screenIntensity = 0.04f;
            return snapshot;
        }

        private static Scene2GuideSettings Settings()
        {
            return new Scene2GuideSettings();
        }

        private static void AssertComplete(
            Scene2GuideStageId stage,
            Scene2GuideStateSnapshot snapshot,
            Scene2GuideSessionState session)
        {
            Scene2GuideStageEvaluation result = Scene2GuideStageEvaluator.Evaluate(stage, snapshot, session, Settings());
            AssertTrue(result.completionConditionMet, stage + " should complete");
        }

        private static void AssertIncomplete(
            Scene2GuideStageId stage,
            Scene2GuideStateSnapshot snapshot,
            Scene2GuideSessionState session)
        {
            Scene2GuideStageEvaluation result = Scene2GuideStageEvaluator.Evaluate(stage, snapshot, session, Settings());
            AssertFalse(result.completionConditionMet, stage + " should wait");
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                passed++;
                Debug.Log("[Scene2RealtimeGuideTests] PASS: " + name);
            }
            catch (Exception exception)
            {
                failed++;
                Debug.LogError("[Scene2RealtimeGuideTests] FAIL: " + name + "\n" + exception);
            }
        }

        private static void AssertTrue(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("Expected true: " + label);
        }

        private static void AssertFalse(bool value, string label)
        {
            if (value)
                throw new InvalidOperationException("Expected false: " + label);
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}");
        }

        private static void AssertClose(float expected, float actual, float tolerance, string label)
        {
            if (Mathf.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}");
        }
    }
}
#endif
