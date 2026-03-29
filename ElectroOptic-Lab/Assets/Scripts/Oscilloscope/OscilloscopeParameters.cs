using ElectroOptics;

namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// 示波器计算核心的输入参数容器
    /// </summary>
    [System.Serializable]
    public class OscilloscopeParameters
    {
        // 电压参数
        public float vDC = 0f;              // DC 偏置电压 (V)
        public float vModulation = 5f;      // AC 调制电压幅值 (V)
        public float frequency = 1000f;     // AC 调制频率 (Hz)

        // 光学参数
        public float intensityMax = 1f;     // 最大光强 I₀
        public float compensatorPhase = 0f; // 补偿器相移 (rad)，预留

        // 晶体配置
        public ModulationMode modulationMode = ModulationMode.Transverse;
        public ElectricFieldAxis fieldAxis = ElectricFieldAxis.Z_Axis;

        // 波形配置
        public int sampleCount = 1024;      // 采样点数
        public float displayPeriods = 2f;   // 显示周期数
    }
}
