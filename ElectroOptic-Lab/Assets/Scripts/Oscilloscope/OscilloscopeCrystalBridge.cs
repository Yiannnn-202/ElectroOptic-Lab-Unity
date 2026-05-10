using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// CrystalPhysicalCore wrapper for oscilloscope-specific sensitivity queries.
    /// </summary>
    public class OscilloscopeCrystalBridge : MonoBehaviour
    {
        private CrystalPhysicalCore _core;
        private CrystalProfile _profile;
        private bool _isInitialized;

        private ElectricFieldAxis _lastAxis;
        private ModulationMode _lastMode;
        private CrystalProfile _lastProfile;
        private bool _hasConfigured;
        private ModulationMode _effectiveMode = ModulationMode.Transverse;

        public float Sensitivity => _core != null ? _core.Sensitivity : 0f;
        public CrystalProfile Profile => _profile;
        public bool IsInitialized => _isInitialized;
        public ModulationMode EffectiveModulationMode => _effectiveMode;

        public void Initialize(CrystalPhysicalCore core)
        {
            _core = core;
            _isInitialized = core != null;
            _hasConfigured = false;

            if (_isInitialized)
                Debug.Log("[OscilloscopeCrystalBridge] Initialized");
            else
                Debug.LogWarning("[OscilloscopeCrystalBridge] Initialize failed: core is null");
        }

        public void SetProfile(CrystalProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("[OscilloscopeCrystalBridge] SetProfile skipped: profile is null");
                return;
            }

            _profile = profile;
            _hasConfigured = false;
            Debug.Log($"[OscilloscopeCrystalBridge] Profile set: {profile.crystalName}");
        }

        public bool NeedsReconfigure(ElectricFieldAxis axis, ModulationMode mode)
        {
            if (!_hasConfigured) return true;
            return axis != _lastAxis || mode != _lastMode || _profile != _lastProfile;
        }

        public float ConfigureAndGetSensitivity(ElectricFieldAxis axis, ModulationMode mode)
        {
            if (!_isInitialized || _profile == null)
            {
                Debug.LogWarning("[OscilloscopeCrystalBridge] Not ready; cannot configure.");
                return 0f;
            }

            CrystalWorkingGeometry geometry = CrystalWorkingGeometry.ResolveOscilloscope(_profile, mode, axis, Vector3.forward);
            var config = new CrystalConfig
            {
                profile = _profile,
                crystalRotation = Quaternion.identity,
                localEField = geometry.LocalEFieldDirection,
                probeFieldDirection = geometry.ProbeFieldDirection,
                worldLightDirection = geometry.WorldLightDirection
            };

            _core.ApplyConfig(config);
            _effectiveMode = geometry.ModulationMode;

            _lastAxis = axis;
            _lastMode = mode;
            _lastProfile = _profile;
            _hasConfigured = true;

            float signedSensitivity = _core.Sensitivity;
            float absSensitivity = Mathf.Abs(signedSensitivity);
            Debug.Log($"[OscilloscopeCrystalBridge] Configured profile={_profile.crystalName}, " +
                      $"requestedMode={mode}, effectiveMode={_effectiveMode}, requestedAxis={axis}, " +
                      $"k={geometry.WorldLightDirection}, E={geometry.LocalEFieldDirection}, " +
                      $"probe={geometry.ProbeFieldDirection}, signedSensitivity={signedSensitivity}, " +
                      $"absSensitivity={absSensitivity}");

            return signedSensitivity;
        }
    }
}
