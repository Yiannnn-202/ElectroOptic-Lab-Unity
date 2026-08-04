using UnityEngine;

namespace ElectroOptics.UI.ExperimentGuide
{
    /// <summary>
    /// 无场景依赖的六阶段判定器，可由 Editor 菜单测试直接验证。
    /// </summary>
    public static class Scene2GuideStageEvaluator
    {
        public const string MissingBrightBaselineMessage = "请先将检偏器调至亮态以建立基准";

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
                        !snapshot.screenOnRail
                        && !snapshot.beamExpanderOnRail
                        && !snapshot.photodiodeProbeOnRail
                        && snapshot.polarizerOnRail
                        && snapshot.crystalOnRail
                        && snapshot.analyzerOnRail
                        && snapshot.powerMeterProbeOnRail
                        && HasOrder(
                            settings.railOrderEpsilonMeters,
                            snapshot.polarizerProjection,
                            snapshot.crystalProjection,
                            snapshot.analyzerProjection,
                            snapshot.powerMeterProbeProjection));

                case Scene2GuideStageId.InstallPhotodiodeProbe:
                    return Scene2GuideStageEvaluation.FromCondition(
                        !snapshot.screenOnRail
                        && !snapshot.beamExpanderOnRail
                        && !snapshot.powerMeterProbeOnRail
                        && snapshot.polarizerOnRail
                        && snapshot.crystalOnRail
                        && snapshot.analyzerOnRail
                        && snapshot.photodiodeProbeOnRail
                        && HasOrder(
                            settings.railOrderEpsilonMeters,
                            snapshot.polarizerProjection,
                            snapshot.crystalProjection,
                            snapshot.analyzerProjection,
                            snapshot.photodiodeProbeProjection));

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

        public static bool CanSampleBrightBaseline(
            Scene2GuideStateSnapshot snapshot,
            Scene2GuideSettings settings)
        {
            return settings != null
                   && HasExtinctionOpticalPath(snapshot, settings)
                   && snapshot.screenTelemetryValid
                   && snapshot.screenIntensity > 0f
                   && snapshot.polarizerDelta <= settings.parallelToleranceDegrees;
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

            if (!session.brightBaselineReady || session.brightBaseline <= 0f)
                return Scene2GuideStageEvaluation.Waiting(MissingBrightBaselineMessage);

            bool orthogonal = Mathf.Abs(snapshot.polarizerDelta - 90f)
                              <= settings.orthogonalToleranceDegrees;
            bool darkEnough = snapshot.screenIntensity
                              <= session.brightBaseline * settings.extinctionRatio;

            return Scene2GuideStageEvaluation.FromCondition(orthogonal && darkEnough);
        }
    }
}
