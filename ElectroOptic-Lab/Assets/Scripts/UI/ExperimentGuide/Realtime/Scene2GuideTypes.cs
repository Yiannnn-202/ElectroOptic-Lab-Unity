using System;
using System.Collections.Generic;
using ElectroOptics.UI.ScreenDisplay;
using UnityEngine;

namespace ElectroOptics.UI.ExperimentGuide
{
    public enum Scene2GuideStageId
    {
        PlaceScreen,
        CalibrateLaser,
        VerifyExtinction,
        ObserveConoscopic,
        InstallPowerMeterProbe,
        InstallPhotodiodeProbe
    }

    [Serializable]
    public sealed class Scene2GuideStageDefinition
    {
        public Scene2GuideStageId id;
        public string title;
        [TextArea(2, 5)] public string body;
        public string gifFileName;

        public Scene2GuideStageDefinition(
            Scene2GuideStageId stageId,
            string stageTitle,
            string stageBody,
            string fileName)
        {
            id = stageId;
            title = stageTitle;
            body = stageBody;
            gifFileName = fileName;
        }

        public static List<Scene2GuideStageDefinition> CreateDefaults()
        {
            return new List<Scene2GuideStageDefinition>
            {
                new Scene2GuideStageDefinition(
                    Scene2GuideStageId.PlaceScreen,
                    "放置光屏",
                    "点击光屏，使用 WASD 将它移动到光学导轨并按 Space 放下，确认屏幕已接收到激光。",
                    "PlaceScreen.gif"),
                new Scene2GuideStageDefinition(
                    Scene2GuideStageId.CalibrateLaser,
                    "校准激光",
                    "微调激光位置，使红点进入光屏中心区域；对准并稳定后按 Enter 锁定校准。",
                    "CalibrateLaser.gif"),
                new Scene2GuideStageDefinition(
                    Scene2GuideStageId.VerifyExtinction,
                    "验证消光",
                    "依次放置起偏器和检偏器。先将两者调至近似平行建立亮态基准，再把检偏器调至正交，使光屏接近消光。",
                    "VerifyExtinction.gif"),
                new Scene2GuideStageDefinition(
                    Scene2GuideStageId.ObserveConoscopic,
                    "观察锥光干涉",
                    "将晶体放在两偏振器之间，再将扩束镜放到晶体前方，保持光屏在轨并观察锥光干涉图样。",
                    "ObserveConoscopic.gif"),
                new Scene2GuideStageDefinition(
                    Scene2GuideStageId.InstallPowerMeterProbe,
                    "更换光功率计探头",
                    "取下光屏和扩束镜，将光功率计探头放到晶体后方的导轨上；完成后，双击名称为“功率计”的元件进入数据处理场景。",
                    "InstallPowerMeterProbe.gif"),
                new Scene2GuideStageDefinition(
                    Scene2GuideStageId.InstallPhotodiodeProbe,
                    "更换光电二极管探头",
                    "取下光功率计探头，将光电二极管探头放到晶体后方的导轨上；完成后，双击名称为“示波器”的元件进入数据处理场景。",
                    "InstallPhotodiodeProbe.gif")
            };
        }
    }

    [Serializable]
    public sealed class Scene2GuideSettings
    {
        [Min(0f)] public float conditionStableSeconds = 0.5f;
        [Min(0f)] public float completionFeedbackSeconds = 0.6f;
        [Min(0.01f)] public float foldAnimationSeconds = 0.2f;
        [Min(0f)] public float calibrationAreaSizePixels = 64f;
        public Vector2 screenCenterPixels = new Vector2(256f, 256f);
        [Range(0f, 45f)] public float parallelToleranceDegrees = 8f;
        [Range(0f, 45f)] public float orthogonalToleranceDegrees = 8f;
        [Range(0f, 1f)] public float extinctionRatio = 0.05f;
        [Min(0f)] public float railOrderEpsilonMeters = 0.01f;
        [Min(0f)] public float defaultPosePositionTolerance = 0.02f;
        [Min(0f)] public float defaultPoseAngleTolerance = 0.5f;
        [Min(0f)] public float defaultPoseStableSeconds = 0.1f;
        [Range(1, 8)] public int gifFrameBufferCapacity = 3;
    }

    public struct Scene2GuideStateSnapshot
    {
        public double capturedAt;
        public bool referencesValid;
        public bool screenTelemetryValid;
        public bool screenReceivesEffectiveLaser;

        public bool screenOnRail;
        public bool polarizerOnRail;
        public bool analyzerOnRail;
        public bool beamExpanderOnRail;
        public bool crystalOnRail;
        public bool powerMeterScene3Entered;
        public bool oscilloscopeScene4Entered;

        public float screenIntensity;
        public Vector2 screenSpotPosition;
        public bool laserCalibrationCommitted;
        public float polarizerAngle;
        public float analyzerAngle;
        public float polarizerDelta;
        public ScreenMode screenMode;

        public bool laserDirectionValid;
        public float polarizerProjection;
        public float analyzerProjection;
        public float beamExpanderProjection;
        public float crystalProjection;
        public float screenProjection;

    }

    public struct Scene2GuideSessionState
    {
        public bool brightBaselineReady;
        public float brightBaseline;
        public bool calibrationCenteredReady;
    }

    public struct Scene2GuideStageEvaluation
    {
        public bool preconditionsMet;
        public bool completionConditionMet;
        public string statusMessage;

        public static Scene2GuideStageEvaluation Waiting(string message = null)
        {
            return new Scene2GuideStageEvaluation
            {
                preconditionsMet = false,
                completionConditionMet = false,
                statusMessage = message ?? string.Empty
            };
        }

        public static Scene2GuideStageEvaluation FromCondition(bool condition, string message = null)
        {
            return new Scene2GuideStageEvaluation
            {
                preconditionsMet = condition,
                completionConditionMet = condition,
                statusMessage = message ?? string.Empty
            };
        }
    }

    public interface IScene2GuideStateProvider
    {
        bool IsReady { get; }
        Scene2GuideStateSnapshot Capture();
    }
}
