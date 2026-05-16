using System;
using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    [Serializable]
    public class ConoscopicJonesParameters
    {
        public const int MinResolution = 16;
        public const int MaxResolution = 1024;
        public const float MinWavelengthNm = 200f;
        public const float MaxWavelengthNm = 2000f;
        public const float MinThicknessMm = 0.001f;
        public const float MaxThicknessMm = 200f;
        public const float MinIndex = 1.0001f;
        public const float MaxIndex = 5f;
        public const float MinScreenDistanceM = 0.01f;
        public const float MaxScreenDistanceM = 10f;
        public const float MinScreenHalfSizeM = 0.005f;
        public const float MaxScreenHalfSizeM = 0.3f;
        public const float MinInitialIntensity = 0f;
        public const float MaxInitialIntensity = 10f;
        public const float MinPhaseAntiAliasStrength = 0f;
        public const float MaxPhaseAntiAliasStrength = 4f;
        public const float MinOpticAxisTiltDeg = 0f;
        public const float MaxOpticAxisTiltDeg = 45f;
        public const float MinApertureRadius = 0.05f;
        public const float MaxApertureRadius = 1.5f;
        public const float MinHeightScale = 0.01f;
        public const float MaxHeightScale = 10f;
        public const float DefaultUniaxialEpsilon = 0.0005f;

        public int resolution = 256;
        public float wavelengthNm = 632.8f;
        public float thicknessMm = 20f;
        public float ordinaryIndexNo = 2.286f;
        public float extraordinaryIndexNe = 2.200f;
        public float principalIndexNx = 2.286f;
        public float principalIndexNy = 2.286f;
        public float principalIndexNz = 2.200f;
        public float screenDistanceM = 0.7f;
        public float screenHalfSizeM = 0.08f;
        public float initialIntensity = 1f;
        public float phaseAntiAliasStrength = 1f;
        public float polarizerAngleDeg = 0f;
        public float analyzerAngleDeg = 90f;
        public float crystalAxisAngleDeg = 45f;
        public float opticAxisTiltDeg = 0f;
        public float opticAxisAzimuthDeg = 0f;
        public float electricFieldStrength = 0f;
        public float electroOpticCoefficientR22 = 0f;
        public float apertureRadius = 1f;
        public float heightScale = 1f;
        public float uniaxialEpsilon = DefaultUniaxialEpsilon;
        public bool forceUniaxial = false;
        public Matrix4x4 worldToPrincipalMatrix = Matrix4x4.identity;

        public ConoscopicJonesParameters()
        {
        }

        public ConoscopicJonesParameters(ConoscopicJonesParameters other)
        {
            if (other == null)
            {
                Clamp();
                return;
            }

            resolution = other.resolution;
            wavelengthNm = other.wavelengthNm;
            thicknessMm = other.thicknessMm;
            ordinaryIndexNo = other.ordinaryIndexNo;
            extraordinaryIndexNe = other.extraordinaryIndexNe;
            principalIndexNx = other.principalIndexNx;
            principalIndexNy = other.principalIndexNy;
            principalIndexNz = other.principalIndexNz;
            screenDistanceM = other.screenDistanceM;
            screenHalfSizeM = other.screenHalfSizeM;
            initialIntensity = other.initialIntensity;
            phaseAntiAliasStrength = other.phaseAntiAliasStrength;
            polarizerAngleDeg = other.polarizerAngleDeg;
            analyzerAngleDeg = other.analyzerAngleDeg;
            crystalAxisAngleDeg = other.crystalAxisAngleDeg;
            opticAxisTiltDeg = other.opticAxisTiltDeg;
            opticAxisAzimuthDeg = other.opticAxisAzimuthDeg;
            electricFieldStrength = other.electricFieldStrength;
            electroOpticCoefficientR22 = other.electroOpticCoefficientR22;
            apertureRadius = other.apertureRadius;
            heightScale = other.heightScale;
            uniaxialEpsilon = other.uniaxialEpsilon;
            forceUniaxial = other.forceUniaxial;
            worldToPrincipalMatrix = other.worldToPrincipalMatrix;
            Clamp();
        }

        public ConoscopicJonesParameters Clone()
        {
            return new ConoscopicJonesParameters(this);
        }

        public void ApplyProfileDefaults(CrystalProfile profile)
        {
            if (profile == null)
            {
                Clamp();
                return;
            }

            wavelengthNm = SafePositive((float)profile.defaultWavelength_nm, wavelengthNm);
            thicknessMm = SafePositive((float)profile.defaultLength_mm, thicknessMm);
            principalIndexNx = SafePositive((float)profile.n_x, principalIndexNx);
            principalIndexNy = SafePositive((float)profile.n_y, principalIndexNy);
            principalIndexNz = SafePositive((float)profile.n_z, principalIndexNz);
            ordinaryIndexNo = ResolveOrdinaryIndex(profile, ordinaryIndexNo);
            extraordinaryIndexNe = ResolveExtraordinaryIndex(profile, extraordinaryIndexNe);
            electroOpticCoefficientR22 = (float)profile.r22;
            Clamp();
        }

        public void ApplyPrincipalIndices(Vector3 indices)
        {
            if (!IsFinitePositive(indices.x) || !IsFinitePositive(indices.y) || !IsFinitePositive(indices.z))
            {
                return;
            }

            principalIndexNx = indices.x;
            principalIndexNy = indices.y;
            principalIndexNz = indices.z;
            ordinaryIndexNo = ResolveOrdinaryIndex(indices, ordinaryIndexNo);
            extraordinaryIndexNe = ResolveExtraordinaryIndex(indices, extraordinaryIndexNe);
            Clamp();
        }

        public void Clamp()
        {
            resolution = Mathf.Clamp(resolution, MinResolution, MaxResolution);
            wavelengthNm = Mathf.Clamp(wavelengthNm, MinWavelengthNm, MaxWavelengthNm);
            thicknessMm = Mathf.Clamp(thicknessMm, MinThicknessMm, MaxThicknessMm);
            ordinaryIndexNo = Mathf.Clamp(ordinaryIndexNo, MinIndex, MaxIndex);
            extraordinaryIndexNe = Mathf.Clamp(extraordinaryIndexNe, MinIndex, MaxIndex);
            principalIndexNx = Mathf.Clamp(principalIndexNx, MinIndex, MaxIndex);
            principalIndexNy = Mathf.Clamp(principalIndexNy, MinIndex, MaxIndex);
            principalIndexNz = Mathf.Clamp(principalIndexNz, MinIndex, MaxIndex);
            screenDistanceM = Mathf.Clamp(screenDistanceM, MinScreenDistanceM, MaxScreenDistanceM);
            screenHalfSizeM = Mathf.Clamp(screenHalfSizeM, MinScreenHalfSizeM, MaxScreenHalfSizeM);
            initialIntensity = Mathf.Clamp(initialIntensity, MinInitialIntensity, MaxInitialIntensity);
            phaseAntiAliasStrength = Mathf.Clamp(phaseAntiAliasStrength, MinPhaseAntiAliasStrength, MaxPhaseAntiAliasStrength);
            opticAxisTiltDeg = Mathf.Clamp(opticAxisTiltDeg, MinOpticAxisTiltDeg, MaxOpticAxisTiltDeg);
            apertureRadius = Mathf.Clamp(apertureRadius, MinApertureRadius, MaxApertureRadius);
            heightScale = Mathf.Clamp(heightScale, MinHeightScale, MaxHeightScale);
            uniaxialEpsilon = Mathf.Max(0.000001f, uniaxialEpsilon);
            if (!IsMatrixFinite(worldToPrincipalMatrix))
            {
                worldToPrincipalMatrix = Matrix4x4.identity;
            }
        }

        public bool IsBiaxial()
        {
            if (forceUniaxial)
            {
                return false;
            }

            return Mathf.Abs(principalIndexNx - principalIndexNy) >= uniaxialEpsilon
                   && Mathf.Abs(principalIndexNy - principalIndexNz) >= uniaxialEpsilon
                   && Mathf.Abs(principalIndexNx - principalIndexNz) >= uniaxialEpsilon;
        }

        public string CrystalOpticClass => IsBiaxial() ? "Biaxial" : "Uniaxial";

        private static float ResolveOrdinaryIndex(CrystalProfile profile, float fallback)
        {
            return ResolveOrdinaryIndex(new Vector3((float)profile.n_x, (float)profile.n_y, (float)profile.n_z), fallback);
        }

        private static float ResolveOrdinaryIndex(Vector3 indices, float fallback)
        {
            float nx = indices.x;
            float ny = indices.y;
            float nz = indices.z;

            if (Mathf.Abs(nx - ny) <= Mathf.Abs(ny - nz) && Mathf.Abs(nx - ny) <= Mathf.Abs(nx - nz))
            {
                return SafePositive((nx + ny) * 0.5f, fallback);
            }

            if (Mathf.Abs(nx - nz) <= Mathf.Abs(ny - nz))
            {
                return SafePositive((nx + nz) * 0.5f, fallback);
            }

            return SafePositive((ny + nz) * 0.5f, fallback);
        }

        private static float ResolveExtraordinaryIndex(CrystalProfile profile, float fallback)
        {
            return ResolveExtraordinaryIndex(new Vector3((float)profile.n_x, (float)profile.n_y, (float)profile.n_z), fallback);
        }

        private static float ResolveExtraordinaryIndex(Vector3 indices, float fallback)
        {
            float nx = indices.x;
            float ny = indices.y;
            float nz = indices.z;
            float no = ResolveOrdinaryIndex(indices, fallback);

            if (Mathf.Abs(nx - no) > Mathf.Abs(ny - no) && Mathf.Abs(nx - no) > Mathf.Abs(nz - no))
            {
                return SafePositive(nx, fallback);
            }

            if (Mathf.Abs(ny - no) > Mathf.Abs(nz - no))
            {
                return SafePositive(ny, fallback);
            }

            return SafePositive(nz, fallback);
        }

        private static float SafePositive(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? fallback : value;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsMatrixFinite(Matrix4x4 matrix)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float value = matrix[row, col];
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
