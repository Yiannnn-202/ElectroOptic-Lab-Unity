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

        public float Sensitivity => _core != null ? _core.Sensitivity : 0f;
        public CrystalProfile Profile => _profile;
        public bool IsInitialized => _isInitialized;

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

            Vector3 axisVec = AxisToVector(axis);
            var config = new CrystalConfig
            {
                profile = _profile,
                crystalRotation = Quaternion.identity,
                localEField = axisVec,
                probeFieldDirection = axisVec,
                worldLightDirection = Vector3.forward
            };

            _core.ApplyConfig(config);

            _lastAxis = axis;
            _lastMode = mode;
            _lastProfile = _profile;
            _hasConfigured = true;

            return _core.Sensitivity;
        }

        private static Vector3 AxisToVector(ElectricFieldAxis axis)
        {
            switch (axis)
            {
                case ElectricFieldAxis.X_Axis: return Vector3.right;
                case ElectricFieldAxis.Y_Axis: return Vector3.up;
                case ElectricFieldAxis.Z_Axis: return Vector3.forward;
                default: return Vector3.forward;
            }
        }
    }
}
