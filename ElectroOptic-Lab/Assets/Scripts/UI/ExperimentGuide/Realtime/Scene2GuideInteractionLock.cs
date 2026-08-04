using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElectroOptics.UI.ExperimentGuide
{
    public interface IScene2GuideInteractionLock
    {
        bool IsLocked { get; }
        IDisposable Acquire();
        void ForceReleaseAll();
    }

    [DisallowMultipleComponent]
    public sealed class Scene2GuideInteractionLock : MonoBehaviour, IScene2GuideInteractionLock
    {
        private static readonly HashSet<string> LockedTypeNames = new HashSet<string>
        {
            "LaserStateController",
            "LaserEmitterMover",
            "Scene2GuideLaserCalibrationGate",
            "OpticalComponent",
            "RailObjectMover",
            "RotateStandController",
            "RotateVirtualKeys",
            "ReceiverStateController",
            "PowerReadoutController",
            "CameraSwitch",
            "ExperimentCameraController",
            "CameraFocusController",
            "ClickAreaFocus",
            "KnobAdjuster",
            "LaserKnobBridge",
            "CrystalKnobBridge"
        };

        [SerializeField] private MonoBehaviour[] additionalInputTargets = new MonoBehaviour[0];

        private readonly List<SavedBehaviourState> savedStates = new List<SavedBehaviourState>();
        private int lockCount;
        private Camera lockedCamera;
        private int savedCameraEventMask;

        public bool IsLocked => lockCount > 0;

        public IDisposable Acquire()
        {
            lockCount++;
            if (lockCount == 1)
                ApplyLock();
            return new LockToken(this);
        }

        public void ForceReleaseAll()
        {
            if (lockCount <= 0 && savedStates.Count == 0)
                return;

            lockCount = 0;
            RestoreLock();
        }

        private void ApplyLock()
        {
            Input.ResetInputAxes();
            savedStates.Clear();

            lockedCamera = Camera.main;
            if (lockedCamera != null)
            {
                savedCameraEventMask = lockedCamera.eventMask;
                lockedCamera.eventMask = 0;
            }

            HashSet<int> capturedIds = new HashSet<int>();
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == this)
                    continue;
                if (!LockedTypeNames.Contains(behaviour.GetType().Name))
                    continue;
                CaptureAndDisable(behaviour, capturedIds);
            }

            for (int i = 0; i < additionalInputTargets.Length; i++)
                CaptureAndDisable(additionalInputTargets[i], capturedIds);
        }

        private void CaptureAndDisable(MonoBehaviour behaviour, HashSet<int> capturedIds)
        {
            if (behaviour == null || !capturedIds.Add(behaviour.GetInstanceID()))
                return;

            savedStates.Add(new SavedBehaviourState(behaviour, behaviour.enabled));
            behaviour.enabled = false;
        }

        private void ReleaseOne()
        {
            if (lockCount <= 0)
                return;

            lockCount--;
            if (lockCount == 0)
                RestoreLock();
        }

        private void RestoreLock()
        {
            for (int i = savedStates.Count - 1; i >= 0; i--)
            {
                try
                {
                    SavedBehaviourState state = savedStates[i];
                    if (state.behaviour != null)
                        state.behaviour.enabled = state.wasEnabled;
                }
                catch (Exception exception)
                {
                    Debug.LogError("[Scene2RealtimeGuide] 恢复实验输入组件失败：" + exception, this);
                }
            }
            savedStates.Clear();

            try
            {
                if (lockedCamera != null)
                    lockedCamera.eventMask = savedCameraEventMask;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Scene2RealtimeGuide] 恢复相机事件掩码失败：" + exception, this);
            }

            lockedCamera = null;
            Input.ResetInputAxes();
        }

        private void OnDestroy()
        {
            ForceReleaseAll();
        }

        private struct SavedBehaviourState
        {
            public readonly MonoBehaviour behaviour;
            public readonly bool wasEnabled;

            public SavedBehaviourState(MonoBehaviour target, bool enabled)
            {
                behaviour = target;
                wasEnabled = enabled;
            }
        }

        private sealed class LockToken : IDisposable
        {
            private Scene2GuideInteractionLock owner;

            public LockToken(Scene2GuideInteractionLock target)
            {
                owner = target;
            }

            public void Dispose()
            {
                if (owner == null)
                    return;
                owner.ReleaseOne();
                owner = null;
            }
        }
    }
}
