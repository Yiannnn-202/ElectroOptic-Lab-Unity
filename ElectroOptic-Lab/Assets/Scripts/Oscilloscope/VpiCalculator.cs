using ElectroOptics;

namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// 半波电压 Vπ 计算工具
    /// 算法复用自 LabController.UpdateVpiDisplay
    /// </summary>
    public static class VpiCalculator
    {
        /// <summary>
        /// 计算半波电压 Vπ
        /// </summary>
        /// <param name="wavelength_nm">激光波长 (nm)</param>
        /// <param name="length_mm">晶体长度 (mm)</param>
        /// <param name="thickness_mm">晶体厚度 (mm)</param>
        /// <param name="sensitivity">有效电光灵敏度 S_eff (1/V)，来自 CrystalPhysicalCore.Sensitivity</param>
        /// <param name="mode">调制模式</param>
        /// <returns>Vπ (V)。sensitivity 接近零时返回 double.PositiveInfinity</returns>
        public static double Calculate(
            double wavelength_nm,
            double length_mm,
            double thickness_mm,
            double sensitivity,
            ModulationMode mode)
        {
            if (sensitivity < 1e-20)
                return double.PositiveInfinity;

            double lambda = wavelength_nm * 1e-9;
            double L = length_mm * 1e-3;
            double d = thickness_mm * 1e-3;

            if (mode == ModulationMode.Transverse)
                return (lambda * d) / (2.0 * L * sensitivity);
            else
                return lambda / (2.0 * sensitivity);
        }
    }
}
