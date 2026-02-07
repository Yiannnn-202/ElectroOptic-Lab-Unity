using System;
using UnityEngine;

namespace ElectroOptics
{
    /// <summary>
    /// 晶体物理配置包 (值类型)
    /// 包含所有驱动物理引擎所需的参数
    /// </summary>
    [Serializable] // 允许在 Inspector 中显示调试
    public struct CrystalConfig
    {
        // --- 静态资产 ---
        public CrystalProfile profile;

        // --- 几何参数 ---
        public double length_mm;      // 通光长度 L
        public double thickness_mm;   // 电极间距 d
        public double wavelength_nm;  // 工作波长 lambda

        // --- 实验配置 ---
        public PropagationAxis propAxis;
        public ElectricFieldAxis fieldAxis;
        public ModulationMode mode;

        // --- 动态输入 ---
        public float voltage;         // 施加电压 V

        /// <summary>
        /// 比较几何参数是否发生变化 (用于触发探测模式重算)
        /// </summary>
        public bool IsGeometryDifferent(CrystalConfig other)
        {
            // 注意：不比较 voltage，只比较影响 V_pi 和几何因子的参数
            return profile != other.profile ||
                   Math.Abs(length_mm - other.length_mm) > 1e-6 ||
                   Math.Abs(thickness_mm - other.thickness_mm) > 1e-6 ||
                   Math.Abs(wavelength_nm - other.wavelength_nm) > 1e-6 ||
                   propAxis != other.propAxis ||
                   fieldAxis != other.fieldAxis ||
                   mode != other.mode;
        }
    }
}