namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// 示波器波形计算输出容器
    /// </summary>
    public class WaveformResult
    {
        /// <summary>
        /// Ch1: AC 电压波形 V_m × sin(2πft)
        /// </summary>
        public float[] ch1;

        /// <summary>
        /// Ch2: 输出光强波形 I₀ × sin²(Γ(t)/2)
        /// </summary>
        public float[] ch2;

        /// <summary>
        /// 当前半波电压 Vπ (V)
        /// </summary>
        public double vPi;

        /// <summary>
        /// 当前静态相位偏置 Γ₀ (rad)
        /// </summary>
        public double gamma0;

        /// <summary>
        /// 确保数组大小匹配采样点数，仅在尺寸变化时重新分配
        /// </summary>
        public void EnsureSize(int sampleCount)
        {
            if (ch1 == null || ch1.Length != sampleCount)
                ch1 = new float[sampleCount];
            if (ch2 == null || ch2.Length != sampleCount)
                ch2 = new float[sampleCount];
        }
    }
}
