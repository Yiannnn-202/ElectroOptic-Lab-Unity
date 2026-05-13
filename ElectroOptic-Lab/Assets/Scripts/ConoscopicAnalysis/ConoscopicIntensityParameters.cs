using System;
using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    [Serializable]
    public class ConoscopicIntensityParameters
    {
        public const int MinResolution = 16;
        public const int MaxResolution = 512;
        public const float MinFov = 1f;
        public const float MaxFov = 120f;
        public const float MinPhaseScale = 0.01f;
        public const float MinDisplayGamma = 0.1f;
        public const float MaxBlackCutoff = 0.25f;
        public const float MinRingSharpness = 0.01f;
        public const float MinCrossWidth = 0.001f;
        public const float MaxCrossWidth = 0.9f;
        public const float MaxInitialMelatopeOffset = 0.25f;
        public static readonly Vector2 DefaultInitialMelatopeOffset = new Vector2(0.035f, -0.025f);

        public int resolution = 128;
        public float fov = 10f;
        public float phaseScale = 0.1f;
        public float displayGamma = 1.25f;
        public float blackCutoff = 0.012f;
        public float ringSharpness = 1f;
        public float crossWidth = 0.16f;
        public Vector2 initialMelatopeOffset = DefaultInitialMelatopeOffset;
        public Color laserColor = Color.red;
        public Vector2 crystalRotation = Vector2.zero;

        public ConoscopicIntensityParameters()
        {
        }

        public ConoscopicIntensityParameters(ConoscopicIntensityParameters other)
        {
            if (other == null)
            {
                Clamp();
                return;
            }

            resolution = other.resolution;
            fov = other.fov;
            phaseScale = other.phaseScale;
            displayGamma = other.displayGamma;
            blackCutoff = other.blackCutoff;
            ringSharpness = other.ringSharpness;
            crossWidth = other.crossWidth;
            initialMelatopeOffset = other.initialMelatopeOffset;
            laserColor = other.laserColor;
            crystalRotation = other.crystalRotation;
            Clamp();
        }

        public ConoscopicIntensityParameters Clone()
        {
            return new ConoscopicIntensityParameters(this);
        }

        public void Clamp()
        {
            resolution = Mathf.Clamp(resolution, MinResolution, MaxResolution);
            fov = Mathf.Clamp(fov, MinFov, MaxFov);
            phaseScale = Mathf.Max(MinPhaseScale, phaseScale);
            displayGamma = Mathf.Max(MinDisplayGamma, displayGamma);
            blackCutoff = Mathf.Clamp(blackCutoff, 0f, MaxBlackCutoff);
            ringSharpness = Mathf.Max(MinRingSharpness, ringSharpness);
            crossWidth = Mathf.Clamp(crossWidth, MinCrossWidth, MaxCrossWidth);
            initialMelatopeOffset = new Vector2(
                Mathf.Clamp(initialMelatopeOffset.x, -MaxInitialMelatopeOffset, MaxInitialMelatopeOffset),
                Mathf.Clamp(initialMelatopeOffset.y, -MaxInitialMelatopeOffset, MaxInitialMelatopeOffset));
        }
    }
}
