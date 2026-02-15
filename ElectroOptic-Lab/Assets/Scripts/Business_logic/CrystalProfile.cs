using UnityEngine;

namespace ElectroOptics
{
    [CreateAssetMenu(fileName = "NewCrystalProfile", menuName = "ElectroOptics/Crystal Profile")]
    public class CrystalProfile : ScriptableObject
    {
        [Header("Basic Info")]
        public string crystalName = "LiNbO3";

        [Header("Default Wavelength")]
        public double defaultWavelength_nm = 633.0;

        [Header("Refractive Indices (@defaultWavelength_nm)")]
        public double n_x = 2.286;
        public double n_y = 2.286;
        public double n_z = 2.200;

        [Header("Electro-Optic Coefficients (pm/V)")]
        public double r11, r12, r13;
        public double r21, r22, r23;
        public double r31, r32, r33;
        public double r41, r42, r43;
        public double r51, r52, r53;
        public double r61, r62, r63;

        [Header("Default Geometry")]
        public double defaultLength_mm = 20.0;
        public double defaultThickness_mm = 1.0;

        public double[] GetStaticIndices()
        {
            return new double[] { n_x, n_y, n_z };
        }

        public double[] GetTensorArray()
        {
            double scale = 1e-12;
            return new double[]
            {
                r11 * scale, r12 * scale, r13 * scale,
                r21 * scale, r22 * scale, r23 * scale,
                r31 * scale, r32 * scale, r33 * scale,
                r41 * scale, r42 * scale, r43 * scale,
                r51 * scale, r52 * scale, r53 * scale,
                r61 * scale, r62 * scale, r63 * scale
            };
        }
    }
}
