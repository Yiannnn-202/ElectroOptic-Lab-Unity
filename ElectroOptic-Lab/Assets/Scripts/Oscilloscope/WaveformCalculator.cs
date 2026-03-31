using System;

namespace ElectroOptics.Oscilloscope
{
    /// <summary>
    /// 纯数学波形计算引擎
    /// 根据参数和 Vπ 填充 Ch1(AC电压) 和 Ch2(光强) 波形数组
    /// 无 Unity API 依赖，可独立单元测试
    /// </summary>
    public class WaveformCalculator
    {
        /// <summary>
        /// 计算波形数据
        /// </summary>
        /// <param name="p">输入参数</param>
        /// <param name="vPi">半波电压 (V)</param>
        /// <param name="result">输出容器，ch1/ch2 数组将被填充</param>
        public void Compute(OscilloscopeParameters p, double vPi, WaveformResult result)
        {
            int N = p.sampleCount;
            result.EnsureSize(N);

            // 时间窗口：显示 displayPeriods 个周期
            double totalTime = p.displayPeriods / (double)p.frequency;
            double dt = totalTime / N;
            double twoPiF = 2.0 * Math.PI * p.frequency;

            // 计算并缓存静态偏置
            bool vPiValid = !double.IsInfinity(vPi) && !double.IsNaN(vPi) && vPi > 0;
            double piOverVpi = vPiValid ? Math.PI / vPi : 0.0;

            result.vPi = vPi;
            result.gamma0 = vPiValid
                ? Math.PI * p.vDC / vPi + p.compensatorPhase
                : p.compensatorPhase;

            for (int i = 0; i < N; i++)
            {
                double t = i * dt;

                // Ch1: AC 电压波形
                double vAC = p.vModulation * Math.Sin(twoPiF * t);
                result.ch1[i] = (float)vAC;

                // Ch2: 输出光强
                if (vPiValid)
                {
                    double vTotal = p.vDC + vAC;
                    double gamma = piOverVpi * vTotal + p.compensatorPhase;
                    double sinHalfGamma = Math.Sin(gamma * 0.5);
                    result.ch2[i] = (float)(p.intensityMax * sinHalfGamma * sinHalfGamma);
                }
                else
                {
                    result.ch2[i] = 0f;
                }
            }
        }
    }
}
