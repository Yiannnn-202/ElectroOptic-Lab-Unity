using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 对应 C++ DLL 的 SimInputData 结构体。
/// 必须保持内存布局严格一致。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SimInputData
{
    // 静态主折射率 (nx, ny, nz) - Size: 3
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] static_n;

    // 电光系数张量 (6x3 展平) - Size: 18
    // 行优先填充: r11, r12, r13, r21, ...
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 18)]
    public double[] r_tensor;

    // 介电常数 (预留) - Size: 3
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] dielectric;

    // 本地坐标系下的电场矢量 (Ex, Ey, Ez) - Size: 3
    // 注意：需要将 Unity 坐标系转换为晶体本地坐标系
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] e_field_local;

    // 本地坐标系下的光波波矢 (kx, ky, kz) - Size: 3
    // 必须归一化
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] wave_vector;

    /// <summary>
    /// 初始化数组内存，防止传给 DLL 空指针导致崩溃。
    /// </summary>
    public void Initialize()
    {
        if (static_n == null || static_n.Length != 3) static_n = new double[3];
        if (r_tensor == null || r_tensor.Length != 18) r_tensor = new double[18];
        if (dielectric == null || dielectric.Length != 3) dielectric = new double[3];
        if (e_field_local == null || e_field_local.Length != 3) e_field_local = new double[3];
        if (wave_vector == null || wave_vector.Length != 3) wave_vector = new double[3];
    }
}

/// <summary>
/// 对应 C++ DLL 的 CrystalOutputData 结构体。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CrystalOutputData
{
    // 计算后的新主折射率 (nx', ny', nz') - Size: 3
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] n_prime;

    // 折射率椭球的旋转矩阵 (3x3 展平) - Size: 9
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
    public double[] rotation_matrix;

    // 有效灵敏度 S_eff (标量)
    // 用于计算半波电压 V_pi
    public double sensitivity;

    /// <summary>
    /// 初始化数组内存。
    /// </summary>
    public void Initialize()
    {
        if (n_prime == null || n_prime.Length != 3) n_prime = new double[3];
        if (rotation_matrix == null || rotation_matrix.Length != 9) rotation_matrix = new double[9];
        sensitivity = 0.0;
    }
}