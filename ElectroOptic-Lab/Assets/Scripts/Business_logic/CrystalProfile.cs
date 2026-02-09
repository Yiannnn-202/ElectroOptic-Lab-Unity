using UnityEngine;

namespace ElectroOptics
{
    [CreateAssetMenu(fileName = "NewCrystalProfile", menuName = "ElectroOptics/Crystal Profile")]
    public class CrystalProfile : ScriptableObject
    {
        [Header("Basic Info")]
        public string crystalName = "LiNbO3";

        [Header("Refractive Indices (Static @ 633nm)")]
        public double no = 2.286;
        public double ne = 2.200;

        [Header("Electro-Optic Coefficients (pm/V)")]
        public double r33 = 30.9;
        public double r13 = 9.6;
        public double r22 = 6.8;
        public double r51 = 32.6;

        [Header("Defaults")]
        public double defaultLength_mm = 20.0;
        public double defaultThickness_mm = 1.0;

        // --- 补回了丢失的波长定义 ---
        public double defaultWavelength_nm = 633.0;

        public double[] GetStaticIndices()
        {
            return new double[] { no, no, ne };
        }

        public double[] GetTensorArray()
        {
            double scale = 1e-12;
            double[] tensor = new double[18];
            // LiNbO3 3m symmetry
            tensor[1] = -r22 * scale; tensor[2] = r13 * scale;
            tensor[4] = r22 * scale; tensor[5] = r13 * scale;
            tensor[8] = r33 * scale;
            tensor[10] = r51 * scale;
            tensor[12] = r51 * scale;
            tensor[15] = -r22 * scale;
            return tensor;
        }
    }
}