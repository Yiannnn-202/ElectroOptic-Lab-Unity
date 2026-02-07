using UnityEngine;

namespace ElectroOptics
{
    /// <summary>
    /// 晶体物理参数配置文件 (ScriptableObject)
    /// 存储晶体的固有属性 (折射率、电光系数) 和默认实验尺寸
    /// </summary>
    [CreateAssetMenu(fileName = "NewCrystalProfile", menuName = "ElectroOptics/Crystal Profile")]
    public class CrystalProfile : ScriptableObject
    {
        [Header("Basic Info")]
        public string crystalName = "LiNbO3";

        [Header("Refractive Indices (Static @ 633nm)")]
        public double no = 2.286; // 普通光折射率
        public double ne = 2.200; // 非凡光折射率

        [Header("Electro-Optic Coefficients (pm/V)")]
        [Tooltip("r33: 沿光轴施加电场改变 z 折射率 (最大系数)")]
        public double r33 = 30.9;
        [Tooltip("r13: 垂直光轴施加电场改变 z 折射率")]
        public double r13 = 9.6;
        [Tooltip("r22: 垂直光轴施加电场改变 xy 折射率")]
        public double r22 = 6.8;
        [Tooltip("r51: 剪切应力系数")]
        public double r51 = 32.6;

        [Header("Default Experiment Settings")]
        [Tooltip("默认通光长度 L (mm)")]
        public double length_mm = 20.0;

        [Tooltip("默认电极间距/晶体厚度 d (mm)")]
        public double thickness_mm = 1.0;

        [Tooltip("默认工作波长 (nm)")]
        public double defaultWavelength_nm = 633.0;

        /// <summary>
        /// 获取静态折射率数组 [nx, ny, nz]
        /// </summary>
        public double[] GetStaticIndices()
        {
            // 单轴晶体: nx=no, ny=no, nz=ne
            return new double[] { no, no, ne };
        }

        /// <summary>
        /// 将配置的参数转换为 DLL 需要的 18x1 展平张量
        /// (此处默认使用 LiNbO3 的 3m 点群对称性映射)
        /// </summary>
        public double[] GetTensorArray()
        {
            // 1. 转换单位: pm/V (10^-12) -> m/V
            double scale = 1e-12;
            double _r33 = r33 * scale;
            double _r13 = r13 * scale;
            double _r22 = r22 * scale;
            double _r51 = r51 * scale;

            double[] tensor = new double[18];

            // 2. 3m 点群张量映射 (Row-Major: 6行 x 3列)
            // 对应矩阵结构：
            //      |  0   -r22  r13 |
            //      |  0    r22  r13 |
            //      |  0     0   r33 |
            //      |  0    r51   0  |
            //      | r51    0    0  |
            //      | -r22   0    0  |

            // Row 1 (xx): 0, -r22, r13
            tensor[1] = -_r22; tensor[2] = _r13;

            // Row 2 (yy): 0, r22, r13
            tensor[4] = _r22; tensor[5] = _r13;

            // Row 3 (zz): 0, 0, r33
            tensor[8] = _r33;

            // Row 4 (yz): 0, r51, 0
            tensor[10] = _r51;

            // Row 5 (xz): r51, 0, 0
            tensor[12] = _r51;

            // Row 6 (xy): -r22, 0, 0
            tensor[15] = -_r22;

            return tensor;
        }
    }
}