using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    public class ConoscopicJonesResult
    {
        public RenderTexture IntensityHeightMap { get; private set; }
        public CrystalProfile Profile { get; private set; }
        public ConoscopicJonesParameters ParametersSnapshot { get; private set; }
        public int Resolution { get; private set; }
        public bool IsValid { get; private set; }
        public float MinIntensity { get; private set; }
        public float MaxIntensity { get; private set; }

        public void SetValid(
            CrystalProfile profile,
            ConoscopicJonesParameters parameters,
            RenderTexture intensityHeightMap,
            float minIntensity,
            float maxIntensity)
        {
            Profile = profile;
            ParametersSnapshot = parameters != null ? parameters.Clone() : null;
            IntensityHeightMap = intensityHeightMap;
            Resolution = parameters != null ? parameters.resolution : 0;
            IsValid = intensityHeightMap != null;
            MinIntensity = Mathf.Clamp01(minIntensity);
            MaxIntensity = Mathf.Clamp01(maxIntensity);
        }

        public void MarkInvalid(CrystalProfile profile, ConoscopicJonesParameters parameters, RenderTexture intensityHeightMap = null)
        {
            Profile = profile;
            ParametersSnapshot = parameters != null ? parameters.Clone() : null;
            IntensityHeightMap = intensityHeightMap;
            Resolution = parameters != null ? parameters.resolution : 0;
            IsValid = false;
            MinIntensity = 0f;
            MaxIntensity = 0f;
        }
    }
}
