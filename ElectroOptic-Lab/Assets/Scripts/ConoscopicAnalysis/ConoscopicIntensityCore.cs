using System;
using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    public class ConoscopicIntensityCore : MonoBehaviour
    {
        [SerializeField] private ConoscopicIntensityParameters _parameters = new ConoscopicIntensityParameters();
        [SerializeField] private CrystalProfile _profile;
        [SerializeField] private CrystalPhysicalCore _physicalCore;

        private readonly ConoscopicIntensityResult _result = new ConoscopicIntensityResult();
        private bool _isDirty = true;
        private bool _hasInitialized;

        public ConoscopicIntensityParameters Parameters => _parameters;
        public ConoscopicIntensityResult Result => _result;
        public CrystalProfile Profile => _profile;
        public bool IsReady => _physicalCore != null && _profile != null;
        public bool IsDirty => _isDirty;

        public event Action<ConoscopicIntensityResult> OnIntensityUpdated;

        private void Awake()
        {
            if (_parameters == null)
            {
                _parameters = new ConoscopicIntensityParameters();
            }

            _parameters.Clamp();
            if (!_hasInitialized)
            {
                Initialize(_physicalCore);
            }
        }

        private void Update()
        {
            RecalculateIfDirty();
        }

        public void Initialize(CrystalPhysicalCore physicalCore = null)
        {
            if (physicalCore != null)
            {
                _physicalCore = physicalCore;
            }
            else if (_physicalCore == null)
            {
                _physicalCore = GetComponent<CrystalPhysicalCore>();
                if (_physicalCore == null)
                {
                    _physicalCore = gameObject.AddComponent<CrystalPhysicalCore>();
                }
            }

            _hasInitialized = _physicalCore != null;
            MarkDirty();
        }

        public void SetProfile(CrystalProfile profile)
        {
            if (_profile == profile)
            {
                return;
            }

            _profile = profile;
            MarkDirty();
        }

        public void SetParameters(ConoscopicIntensityParameters parameters)
        {
            _parameters = parameters != null ? parameters.Clone() : new ConoscopicIntensityParameters();
            _parameters.Clamp();
            MarkDirty();
        }

        public void SetResolution(int resolution)
        {
            _parameters.resolution = resolution;
            _parameters.Clamp();
            MarkDirty();
        }

        public void SetCrystalRotation(Vector2 rotation)
        {
            if (_parameters.crystalRotation == rotation)
            {
                return;
            }

            _parameters.crystalRotation = rotation;
            MarkDirty();
        }

        public void SetFOV(float fov)
        {
            _parameters.fov = fov;
            _parameters.Clamp();
            MarkDirty();
        }

        public void SetPhaseScale(float phaseScale)
        {
            _parameters.phaseScale = phaseScale;
            _parameters.Clamp();
            MarkDirty();
        }

        public void SetDisplayMapping(float displayGamma, float blackCutoff, float ringSharpness, float crossWidth)
        {
            _parameters.displayGamma = displayGamma;
            _parameters.blackCutoff = blackCutoff;
            _parameters.ringSharpness = ringSharpness;
            _parameters.crossWidth = crossWidth;
            _parameters.Clamp();
            MarkDirty();
        }

        public void SetInitialMelatopeOffset(Vector2 offset)
        {
            _parameters.initialMelatopeOffset = offset;
            _parameters.Clamp();
            MarkDirty();
        }

        public void SetLaserColor(Color color)
        {
            _parameters.laserColor = color;
            MarkDirty();
        }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        public void RecalculateIfDirty()
        {
            if (!_isDirty)
            {
                return;
            }

            ForceRecalculate();
        }

        public void ForceRecalculate()
        {
            if (_parameters == null)
            {
                _parameters = new ConoscopicIntensityParameters();
            }

            _parameters.Clamp();
            if (_physicalCore == null)
            {
                Initialize();
            }

            if (!IsReady)
            {
                _result.MarkInvalid(_profile, _parameters);
                _isDirty = false;
                OnIntensityUpdated?.Invoke(_result);
                return;
            }

            ApplyPhysicalConfig();
            ConoscopicIntensityCalculator.Compute(_physicalCore, _parameters, _result);
            _isDirty = false;
            OnIntensityUpdated?.Invoke(_result);
        }

        private void ApplyPhysicalConfig()
        {
            Vector3 requestedWorldLightDirection = Vector3.forward;
            CrystalWorkingGeometry geometry = CrystalWorkingGeometry.ResolveConoscopic(_profile, requestedWorldLightDirection);
            var config = new CrystalConfig
            {
                profile = _profile,
                crystalRotation = Quaternion.Euler(_parameters.crystalRotation.x, _parameters.crystalRotation.y, 0f),
                localEField = geometry.LocalEFieldDirection,
                probeFieldDirection = geometry.ProbeFieldDirection,
                worldLightDirection = geometry.WorldLightDirection
            };

            _physicalCore.ApplyConfig(config);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_parameters == null)
            {
                _parameters = new ConoscopicIntensityParameters();
            }

            _parameters.Clamp();
            MarkDirty();

            if (Application.isPlaying && isActiveAndEnabled)
            {
                UnityEditor.EditorApplication.delayCall += DelayedEditorRecalculate;
            }
        }

        private void DelayedEditorRecalculate()
        {
            if (this == null || !Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            ForceRecalculate();
        }

        [ContextMenu("Force Recalculate")]
        private void ContextMenuForceRecalculate()
        {
            ForceRecalculate();
            Debug.Log($"[ConoscopicIntensityCore] Recalculated. valid={_result.IsValid}, " +
                      $"resolution={_result.Resolution}, min={_result.MinIntensity:F4}, max={_result.MaxIntensity:F4}");
        }

        [ContextMenu("Print Status")]
        private void ContextMenuPrintStatus()
        {
            Debug.Log($"[ConoscopicIntensityCore] Status\n" +
                      $"  - Ready: {IsReady}\n" +
                      $"  - Dirty: {_isDirty}\n" +
                      $"  - Profile: {(_profile != null ? _profile.crystalName : "null")}\n" +
                      $"  - Resolution: {_parameters.resolution}\n" +
                      $"  - FOV: {_parameters.fov}\n" +
                      $"  - PhaseScale: {_parameters.phaseScale}\n" +
                      $"  - Intensity: {_result.MinIntensity:F4}..{_result.MaxIntensity:F4}");
        }
#endif
    }
}
