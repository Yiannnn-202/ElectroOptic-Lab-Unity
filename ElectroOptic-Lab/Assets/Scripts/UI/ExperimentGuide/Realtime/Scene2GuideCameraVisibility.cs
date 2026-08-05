using UnityEngine;

namespace ElectroOptics.UI.ExperimentGuide
{
    [DisallowMultipleComponent]
    public sealed class Scene2GuideCameraVisibility : MonoBehaviour
    {
        [SerializeField] private Transform observedCamera;

        private Vector3 defaultPosition;
        private Quaternion defaultRotation;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private float stableTimer;
        private bool captured;

        public bool IsVisible { get; private set; }

        public void CaptureDefaultPose(Transform cameraTransform)
        {
            observedCamera = cameraTransform != null
                ? cameraTransform
                : Camera.main != null ? Camera.main.transform : null;
            if (observedCamera == null)
            {
                captured = false;
                IsVisible = false;
                Debug.LogError("[Scene2RealtimeGuide] 无法捕获默认相机位姿：未找到主相机。", this);
                return;
            }

            defaultPosition = observedCamera.position;
            defaultRotation = observedCamera.rotation;
            previousPosition = defaultPosition;
            previousRotation = defaultRotation;
            stableTimer = 0f;
            captured = true;
            IsVisible = false;
        }

        public bool Tick(Scene2GuideSettings settings, float unscaledDeltaTime)
        {
            if (!captured || observedCamera == null || settings == null)
            {
                IsVisible = false;
                return false;
            }

            float positionError = Vector3.Distance(observedCamera.position, defaultPosition);
            float rotationError = Quaternion.Angle(observedCamera.rotation, defaultRotation);
            bool atDefault = !ExperimentCameraController.IsInCloseUpView
                             && positionError <= settings.defaultPosePositionTolerance
                             && rotationError <= settings.defaultPoseAngleTolerance;

            float frameMove = Vector3.Distance(observedCamera.position, previousPosition);
            float frameRotation = Quaternion.Angle(observedCamera.rotation, previousRotation);
            previousPosition = observedCamera.position;
            previousRotation = observedCamera.rotation;

            if (!atDefault)
            {
                stableTimer = 0f;
                IsVisible = false;
                return false;
            }

            bool poseStill = frameMove <= 0.0001f && frameRotation <= 0.01f;
            stableTimer = poseStill ? stableTimer + unscaledDeltaTime : 0f;
            IsVisible = stableTimer >= settings.defaultPoseStableSeconds;
            return IsVisible;
        }
    }
}
