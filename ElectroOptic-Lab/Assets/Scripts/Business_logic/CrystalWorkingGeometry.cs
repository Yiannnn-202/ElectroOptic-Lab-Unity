using UnityEngine;

namespace ElectroOptics
{
    public struct CrystalWorkingGeometry
    {
        public Vector3 WorldLightDirection;
        public Vector3 LocalEFieldDirection;
        public Vector3 ProbeFieldDirection;
        public ModulationMode ModulationMode;
        public bool OverridesRequestedGeometry;

        public static CrystalWorkingGeometry ResolveConoscopic(CrystalProfile profile, Vector3 requestedWorldLightDirection)
        {
            if (IsKtp(profile))
            {
                return new CrystalWorkingGeometry
                {
                    WorldLightDirection = Vector3.up,
                    LocalEFieldDirection = Vector3.forward,
                    ProbeFieldDirection = Vector3.forward,
                    ModulationMode = ModulationMode.Transverse,
                    OverridesRequestedGeometry = true
                };
            }

            return new CrystalWorkingGeometry
            {
                WorldLightDirection = NormalizeOrFallback(requestedWorldLightDirection, Vector3.forward),
                LocalEFieldDirection = Vector3.forward,
                ProbeFieldDirection = Vector3.forward,
                ModulationMode = ModulationMode.Transverse,
                OverridesRequestedGeometry = false
            };
        }

        public static CrystalWorkingGeometry ResolveOscilloscope(
            CrystalProfile profile,
            ModulationMode requestedMode,
            ElectricFieldAxis requestedFieldAxis,
            Vector3 requestedWorldLightDirection)
        {
            if (IsKtp(profile))
            {
                return new CrystalWorkingGeometry
                {
                    WorldLightDirection = Vector3.up,
                    LocalEFieldDirection = Vector3.forward,
                    ProbeFieldDirection = Vector3.forward,
                    ModulationMode = ModulationMode.Transverse,
                    OverridesRequestedGeometry = true
                };
            }

            Vector3 axis = AxisToVector(requestedFieldAxis);
            Vector3 lightDirection = NormalizeOrFallback(requestedWorldLightDirection, Vector3.forward);
            bool overridesRequestedGeometry = false;

            if (requestedMode == ModulationMode.Transverse)
            {
                Vector3 transverseLight = ResolveTransverseLightDirection(requestedFieldAxis, axis, lightDirection);
                overridesRequestedGeometry = !ApproximatelySameDirection(lightDirection, transverseLight);
                lightDirection = transverseLight;
            }

            return new CrystalWorkingGeometry
            {
                WorldLightDirection = lightDirection,
                LocalEFieldDirection = axis,
                ProbeFieldDirection = axis,
                ModulationMode = requestedMode,
                OverridesRequestedGeometry = overridesRequestedGeometry
            };
        }

        public static bool IsKtp(CrystalProfile profile)
        {
            return profile != null
                   && !string.IsNullOrEmpty(profile.crystalName)
                   && profile.crystalName.Trim().Equals("KTP", System.StringComparison.OrdinalIgnoreCase);
        }

        public static Vector3 AxisToVector(ElectricFieldAxis axis)
        {
            switch (axis)
            {
                case ElectricFieldAxis.X_Axis: return Vector3.right;
                case ElectricFieldAxis.Y_Axis: return Vector3.up;
                case ElectricFieldAxis.Z_Axis: return Vector3.forward;
                default: return Vector3.forward;
            }
        }

        private static Vector3 NormalizeOrFallback(Vector3 vector, Vector3 fallback)
        {
            return vector.sqrMagnitude > 0.000001f ? vector.normalized : fallback;
        }

        private static Vector3 ResolveTransverseLightDirection(
            ElectricFieldAxis fieldAxis,
            Vector3 electricFieldDirection,
            Vector3 requestedLightDirection)
        {
            if (fieldAxis == ElectricFieldAxis.Z_Axis)
            {
                return Vector3.up;
            }

            return IsParallel(requestedLightDirection, electricFieldDirection)
                ? ChoosePerpendicularDirection(electricFieldDirection)
                : requestedLightDirection;
        }

        private static bool IsParallel(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(Vector3.Dot(a.normalized, b.normalized)) > 0.999f;
        }

        private static bool ApproximatelySameDirection(Vector3 a, Vector3 b)
        {
            return Vector3.SqrMagnitude(a.normalized - b.normalized) < 0.000001f;
        }

        private static Vector3 ChoosePerpendicularDirection(Vector3 axis)
        {
            if (!IsParallel(axis, Vector3.forward)) return Vector3.forward;
            return Vector3.up;
        }
    }
}
