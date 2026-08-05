using System;
using UnityEngine;

namespace ElectroOptics.UI.ExperimentGuide
{
    /// <summary>
    /// 在校准阶段接管旧 LaserEmitterMover，确保只有红点稳定居中后 Enter 才会锁定。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-400)]
    public sealed class Scene2GuideLaserCalibrationGate : MonoBehaviour
    {
        private LaserEmitterMover mover;
        private LaserStateController stateController;
        private Vector3 originalPosition;
        private bool ownsInput;
        private bool moverEnabledBeforeOwnership;
        private bool centeredReady;

        public event Action CalibrationCommitted;

        public bool OwnsInput => ownsInput;
        public bool CenteredReady => centeredReady;
        public bool CanAcceptCommit => ownsInput
                                       && mover != null
                                       && !mover.isCalibrationDone
                                       && centeredReady;

        public void Configure(LaserEmitterMover targetMover, LaserStateController targetStateController)
        {
            if (ownsInput)
                ReleaseOwnership();

            mover = targetMover;
            stateController = targetStateController;
            if (mover != null)
                originalPosition = mover.transform.position;
        }

        public void SetCalibrationActive(bool active)
        {
            if (active == ownsInput)
                return;

            if (active)
                AcquireOwnership();
            else
                ReleaseOwnership();
        }

        public void SetCenteredReady(bool ready)
        {
            centeredReady = ready;
        }

        private void Update()
        {
            if (!ownsInput || mover == null || mover.isCalibrationDone)
                return;

            if (stateController != null && stateController.IsSelected)
            {
                float horizontal = Input.GetAxisRaw("Horizontal");
                float vertical = Input.GetAxisRaw("Vertical");
                Vector3 movement = new Vector3(horizontal, vertical, 0f)
                                   * mover.moveSpeed
                                   * Time.deltaTime;
                Vector3 next = mover.transform.position + movement;
                next.x = Mathf.Clamp(next.x, originalPosition.x - mover.moveRange, originalPosition.x + mover.moveRange);
                next.y = Mathf.Clamp(next.y, originalPosition.y - mover.moveRange, originalPosition.y + mover.moveRange);
                next.z = originalPosition.z;
                mover.transform.position = next;
            }

            if (!CanAcceptCommit)
                return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                mover.isCalibrationDone = true;
                if (stateController != null)
                    stateController.Deselect();
                CalibrationCommitted?.Invoke();
                ReleaseOwnership();
                Debug.Log("[Scene2RealtimeGuide] 激光居中校准已确认并锁定。", this);
            }
        }

        private void AcquireOwnership()
        {
            if (mover == null)
            {
                Debug.LogError("[Scene2RealtimeGuide] 无法接管激光校准：LaserEmitterMover 未绑定。", this);
                return;
            }

            moverEnabledBeforeOwnership = mover.enabled;
            mover.enabled = false;
            ownsInput = true;
            centeredReady = false;
            Input.ResetInputAxes();
        }

        private void ReleaseOwnership()
        {
            if (!ownsInput)
                return;

            ownsInput = false;
            centeredReady = false;
            if (mover != null)
                mover.enabled = moverEnabledBeforeOwnership;
            Input.ResetInputAxes();
        }

        private void OnDestroy()
        {
            ReleaseOwnership();
        }
    }
}
