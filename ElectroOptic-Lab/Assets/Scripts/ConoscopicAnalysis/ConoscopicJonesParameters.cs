using System;
using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    public enum ConoscopicBiaxialDisplayMode
    {
        ConoscopicTeaching = 0,
        RawJones = 1,
        PaperKtp1 = 2
    }

    [Serializable]
    public class ConoscopicJonesParameters
    {
        public const int MinResolution = 16;
        public const int MaxResolution = 1024;
        public const int MinRenderSupersampleFactor = 1;
        public const int MaxRenderSupersampleFactor = 4;
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
        public const float MinPhaseScale = 0.01f;
        public const float MaxPhaseScale = 5f;
        public const float MinRingSharpness = 0.25f;
        public const float MaxRingSharpness = 4f;
        public const float MinCrossWidth = 0.01f;
        public const float MaxCrossWidth = 0.35f;
        public const float MinBlackCutoff = 0f;
        public const float MaxBlackCutoff = 0.25f;
        public const float MinDisplayGamma = 0.2f;
        public const float MaxDisplayGamma = 3f;
        public const float DefaultUniaxialEpsilon = 0.0005f;
        public const float PaperKtp1WavelengthNm = 589.3f;
        public const float PaperKtp1ThicknessMm = 0.915f;
        public const float PaperKtp1AlphaDeg = 45f;
        public const float PaperKtp1ThetaDeg = 0f;
        public const float PaperKtp1PhiDeg = 0f;

        public int resolution = 256;
        public int renderSupersampleFactor = 1;
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
        public float paperThetaDeg = PaperKtp1ThetaDeg;
        public float paperPhiDeg = PaperKtp1PhiDeg;
        public float electricFieldStrength = 0f;
        public float electroOpticCoefficientR22 = 0f;
        public float apertureRadius = 1f;
        public float heightScale = 1f;
        public float phaseScale = 0.1f;
        public float ringSharpness = 1f;
        public float crossWidth = 0.16f;
        public float blackCutoff = 0.012f;
        public float displayGamma = 1.25f;
        public Vector2 initialMelatopeOffset = Vector2.zero;
        public float uniaxialEpsilon = DefaultUniaxialEpsilon;
        public bool forceUniaxial = false;
        public bool uniaxialEoView = false;
        public bool uniaxialEoUsePerturbedAxis = false;
        public ConoscopicBiaxialDisplayMode biaxialDisplayMode = ConoscopicBiaxialDisplayMode.ConoscopicTeaching;
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
            renderSupersampleFactor = other.renderSupersampleFactor;
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
            paperThetaDeg = other.paperThetaDeg;
            paperPhiDeg = other.paperPhiDeg;
            electricFieldStrength = other.electricFieldStrength;
            electroOpticCoefficientR22 = other.electroOpticCoefficientR22;
            apertureRadius = other.apertureRadius;
            heightScale = other.heightScale;
            phaseScale = other.phaseScale;
            ringSharpness = other.ringSharpness;
            crossWidth = other.crossWidth;
            blackCutoff = other.blackCutoff;
            displayGamma = other.displayGamma;
            initialMelatopeOffset = other.initialMelatopeOffset;
            uniaxialEpsilon = other.uniaxialEpsilon;
            forceUniaxial = other.forceUniaxial;
            uniaxialEoView = other.uniaxialEoView;
            uniaxialEoUsePerturbedAxis = other.uniaxialEoUsePerturbedAxis;
            biaxialDisplayMode = other.biaxialDisplayMode;
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
            if (IsKtpProfile(profile))
            {
                ApplyPaperKtp1Preset(false);
            }
            else if (biaxialDisplayMode == ConoscopicBiaxialDisplayMode.PaperKtp1)
            {
                biaxialDisplayMode = ConoscopicBiaxialDisplayMode.ConoscopicTeaching;
            }

            Clamp();
        }

        public void ApplyPaperKtp1Preset(bool resetIndices = false)
        {
            wavelengthNm = PaperKtp1WavelengthNm;
            thicknessMm = PaperKtp1ThicknessMm;
            crystalAxisAngleDeg = PaperKtp1AlphaDeg;
            opticAxisTiltDeg = 0f;
            opticAxisAzimuthDeg = 0f;
            paperThetaDeg = PaperKtp1ThetaDeg;
            paperPhiDeg = PaperKtp1PhiDeg;
            screenDistanceM = 0.7f;
            screenHalfSizeM = 0.08f;
            phaseScale = 1f;
            ringSharpness = 1.6f;
            crossWidth = 0.08f;
            blackCutoff = 0.01f;
            displayGamma = 1.15f;
            initialMelatopeOffset = Vector2.zero;
            biaxialDisplayMode = ConoscopicBiaxialDisplayMode.PaperKtp1;

            if (resetIndices)
            {
                principalIndexNx = 1.763569f;
                principalIndexNy = 1.773327f;
                principalIndexNz = 1.863399f;
                ordinaryIndexNo = ResolveOrdinaryIndex(new Vector3(principalIndexNx, principalIndexNy, principalIndexNz), ordinaryIndexNo);
                extraordinaryIndexNe = ResolveExtraordinaryIndex(new Vector3(principalIndexNx, principalIndexNy, principalIndexNz), extraordinaryIndexNe);
            }

            worldToPrincipalMatrix = CreatePaperKtp1WorldToPrincipalMatrix(crystalAxisAngleDeg, paperThetaDeg, paperPhiDeg);
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
            renderSupersampleFactor = Mathf.Clamp(renderSupersampleFactor, MinRenderSupersampleFactor, MaxRenderSupersampleFactor);
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
            paperThetaDeg = Mathf.Clamp(paperThetaDeg, 0f, 90f);
            paperPhiDeg = Mathf.Repeat(paperPhiDeg, 360f);
            apertureRadius = Mathf.Clamp(apertureRadius, MinApertureRadius, MaxApertureRadius);
            heightScale = Mathf.Clamp(heightScale, MinHeightScale, MaxHeightScale);
            phaseScale = Mathf.Clamp(phaseScale, MinPhaseScale, MaxPhaseScale);
            ringSharpness = Mathf.Clamp(ringSharpness, MinRingSharpness, MaxRingSharpness);
            crossWidth = Mathf.Clamp(crossWidth, MinCrossWidth, MaxCrossWidth);
            blackCutoff = Mathf.Clamp(blackCutoff, MinBlackCutoff, MaxBlackCutoff);
            displayGamma = Mathf.Clamp(displayGamma, MinDisplayGamma, MaxDisplayGamma);
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

        public static bool IsKtpProfile(CrystalProfile profile)
        {
            return profile != null
                   && !string.IsNullOrEmpty(profile.crystalName)
                   && profile.crystalName.Trim().Equals("KTP", StringComparison.OrdinalIgnoreCase);
        }

        public static Matrix4x4 CreatePaperKtp1WorldToPrincipalMatrix(float alphaDeg, float thetaDeg, float phiDeg)
        {
            Quaternion inPlane = Quaternion.AngleAxis(-alphaDeg, Vector3.forward);
            Quaternion tilt = Quaternion.AngleAxis(-thetaDeg, new Vector3(Mathf.Cos(phiDeg * Mathf.Deg2Rad), Mathf.Sin(phiDeg * Mathf.Deg2Rad), 0f));
            return Matrix4x4.Rotate(inPlane * tilt);
        }
    }
}
