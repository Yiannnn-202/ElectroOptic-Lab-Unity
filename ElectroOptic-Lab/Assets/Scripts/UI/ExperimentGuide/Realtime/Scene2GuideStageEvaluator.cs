using UnityEngine;

namespace ElectroOptics.UI.ExperimentGuide
{
    /// <summary>
    /// 无场景依赖的六阶段判定器，可由 Editor 菜单测试直接验证。
    /// </summary>
    public static class Scene2GuideStageEvaluator
    {
        public const string MissingOpticalSignalMessage = "请确认光线正通过检偏器投射到光屏";
        public const string AdjustAnalyzerMessage = "请继续调节检偏器，直至光点消失";
        public const string OpenPowerMeterDataProcessingMessage = "请双击名称为“功率计”的元件进入数据处理";
        public const string OpenOscilloscopeDataProcessingMessage = "请双击名称为“示波器”的元件进入数据处理";

        public static Scene2GuideStageEvaluation Evaluate(
            Scene2GuideStageId stageId,
            Scene2GuideStateSnapshot snapshot,
            Scene2GuideSessionState session,
            Scene2GuideSettings settings)
        {
            if (settings == null || !snapshot.referencesValid)
                return Scene2GuideStageEvaluation.Waiting();

            switch (stageId)
            {
                case Scene2GuideStageId.PlaceScreen:
                    return Scene2GuideStageEvaluation.FromCondition(
                        snapshot.screenOnRail
                        && snapshot.screenTelemetryValid
                        && snapshot.screenReceivesEffectiveLaser
                        && snapshot.screenIntensity > 0f);

                case Scene2GuideStageId.CalibrateLaser:
                    return Scene2GuideStageEvaluation.FromCondition(
                        snapshot.laserCalibrationCommitted);

                case Scene2GuideStageId.VerifyExtinction:
                    return EvaluateExtinction(snapshot, session, settings);

                case Scene2GuideStageId.ObserveConoscopic:
                    return Scene2GuideStageEvaluation.FromCondition(
                        snapshot.screenOnRail
                        && snapshot.polarizerOnRail
                        && snapshot.beamExpanderOnRail
                        && snapshot.crystalOnRail
                        && snapshot.analyzerOnRail
                        && HasOrder(
                            settings.railOrderEpsilonMeters,
                            snapshot.polarizerProjection,
                            snapshot.beamExpanderProjection,
                            snapshot.crystalProjection,
                            snapshot.analyzerProjection,
                            snapshot.screenProjection)
                        && snapshot.screenMode == ScreenDisplay.ScreenMode.Conoscopic);

                case Scene2GuideStageId.InstallPowerMeterProbe:
                    return Scene2GuideStageEvaluation.FromCondition(
                        snapshot.powerMeterScene3Entered,
                        !snapshot.powerMeterScene3Entered
                            ? OpenPowerMeterDataProcessingMessage
                            : null);

                case Scene2GuideStageId.InstallPhotodiodeProbe:
                    return Scene2GuideStageEvaluation.FromCondition(
                        snapshot.oscilloscopeScene4Entered,
                        !snapshot.oscilloscopeScene4Entered
                            ? OpenOscilloscopeDataProcessingMessage
                            : null);

                default:
                    return Scene2GuideStageEvaluation.Waiting();
            }
        }

        public static bool IsCalibrationCentered(
            Scene2GuideStateSnapshot snapshot,
            Scene2GuideSettings settings)
        {
            if (settings == null
                || !snapshot.screenOnRail
                || !snapshot.screenTelemetryValid
                || !snapshot.screenReceivesEffectiveLaser)
            {
                return false;
            }

            Vector2 offset = snapshot.screenSpotPosition - settings.screenCenterPixels;
            float halfSize = settings.calibrationAreaSizePixels * 0.5f;
            return Mathf.Abs(offset.x) <= halfSize
                   && Mathf.Abs(offset.y) <= halfSize;
        }

        public static bool HasExtinctionOpticalPath(
            Scene2GuideStateSnapshot snapshot,
            Scene2GuideSettings settings)
        {
            return settings != null
                   && snapshot.laserDirectionValid
                   && snapshot.polarizerOnRail
                   && snapshot.analyzerOnRail
                   && snapshot.screenOnRail
                   && HasOrder(
                       settings.railOrderEpsilonMeters,
                       snapshot.polarizerProjection,
                       snapshot.analyzerProjection,
                       snapshot.screenProjection);
        }

        public static float NormalizePolarizerDelta(float angleA, float angleB)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(angleA, angleB));
            return Mathf.Min(delta, 180f - delta);
        }

        public static bool HasOrder(float epsilon, params float[] projections)
        {
            if (projections == null || projections.Length == 0)
                return false;

            for (int i = 0; i < projections.Length; i++)
            {
                if (float.IsNaN(projections[i]) || float.IsInfinity(projections[i]))
                    return false;

                if (projections[i] <= 0f)
                    return false;

                if (i > 0 && projections[i - 1] + epsilon >= projections[i])
                    return false;
            }

            return true;
        }

        private static Scene2GuideStageEvaluation EvaluateExtinction(
            Scene2GuideStateSnapshot snapshot,
            Scene2GuideSessionState session,
            Scene2GuideSettings settings)
        {
            if (!HasExtinctionOpticalPath(snapshot, settings) || !snapshot.screenTelemetryValid)
                return Scene2GuideStageEvaluation.Waiting();

            // 收到强度为 0 的光学链信号才代表真正消光；不能把光路中断误认为黑点。
            if (!snapshot.screenReceivesOpticalSignal)
                return Scene2GuideStageEvaluation.Waiting(MissingOpticalSignalMessage);

            bool darkEnough = snapshot.screenIntensity
                              <= settings.extinctionIntensityThreshold;

            return Scene2GuideStageEvaluation.FromCondition(
                darkEnough,
                darkEnough ? null : AdjustAnalyzerMessage);
        }
    }
}
