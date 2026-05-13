using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    public class ConoscopicIntensityResult
    {
        public int Resolution { get; private set; }
        public float[] Intensities { get; private set; }
        public CrystalProfile Profile { get; private set; }
        public ConoscopicIntensityParameters ParametersSnapshot { get; private set; }
        public bool IsValid { get; private set; }
        public float MinIntensity { get; private set; }
        public float MaxIntensity { get; private set; }

        public void EnsureSize(int resolution)
        {
            resolution = Mathf.Clamp(
                resolution,
                ConoscopicIntensityParameters.MinResolution,
                ConoscopicIntensityParameters.MaxResolution);

            int length = resolution * resolution;
            if (Intensities == null || Intensities.Length != length)
            {
                Intensities = new float[length];
            }

            Resolution = resolution;
        }

        public void SetSnapshot(CrystalProfile profile, ConoscopicIntensityParameters parameters, bool isValid)
        {
            Profile = profile;
            ParametersSnapshot = parameters != null ? parameters.Clone() : null;
            IsValid = isValid;
            RefreshMinMax();
        }

        public void MarkInvalid(CrystalProfile profile, ConoscopicIntensityParameters parameters)
        {
            if (parameters != null)
            {
                EnsureSize(parameters.resolution);
            }

            if (Intensities != null)
            {
                System.Array.Clear(Intensities, 0, Intensities.Length);
            }

            Profile = profile;
            ParametersSnapshot = parameters != null ? parameters.Clone() : null;
            IsValid = false;
            MinIntensity = 0f;
            MaxIntensity = 0f;
        }

        public Vector2 GetCoordinate(int x, int y)
        {
            if (Resolution <= 1)
            {
                return Vector2.zero;
            }

            float u = x / (float)(Resolution - 1);
            float v = y / (float)(Resolution - 1);
            return new Vector2(Mathf.Lerp(-1f, 1f, u), Mathf.Lerp(-1f, 1f, v));
        }

        private void RefreshMinMax()
        {
            MinIntensity = 0f;
            MaxIntensity = 0f;

            if (Intensities == null || Intensities.Length == 0)
            {
                return;
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < Intensities.Length; i++)
            {
                float value = Intensities[i];
                if (value < min) min = value;
                if (value > max) max = value;
            }

            MinIntensity = float.IsInfinity(min) ? 0f : min;
            MaxIntensity = float.IsInfinity(max) ? 0f : max;
        }
    }
}
