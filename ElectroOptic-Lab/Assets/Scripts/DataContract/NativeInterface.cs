using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class NativeInterface
{
    // ========================================================================
    // 1. DLL 配置
    // ========================================================================
    private const string DLL_NAME = "CrystalPhysicsCore";

    // ========================================================================
    // 2. 原生导入 (Private)
    //    使用 ref 传递结构体指针，性能最高
    // ========================================================================
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void CalculateCrystalState(ref SimInputData input, ref CrystalOutputData output);

    // ========================================================================
    // 3. 安全包装层 (Public API)
    //    职责：内存检查、异常捕获、日志记录
    // ========================================================================

    /// <summary>
    /// 调用底层物理算核。
    /// </summary>
    /// <param name="input">输入数据 (必须已 Initialize)</param>
    /// <param name="output">输出数据 (必须已 Initialize)</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    public static bool SafeCalculate(ref SimInputData input, ref CrystalOutputData output)
    {
        // --- A. 内存安全检查 (Pre-flight Check) ---
        // 防止数组未分配或长度错误导致的 C++ 越界访问
        if (!ValidateInput(ref input))
        {
            Debug.LogError("[NativeInterface] Input validation failed! Aborting DLL call.");
            return false;
        }

        if (!ValidateOutput(ref output))
        {
            Debug.LogError("[NativeInterface] Output validation failed! Aborting DLL call.");
            return false;
        }

        // --- B. 安全调用 (Safe Invocation) ---
        try
        {
            CalculateCrystalState(ref input, ref output);
            return true;
        }
        catch (DllNotFoundException ex)
        {
            Debug.LogError($"[NativeInterface] Critical Error: DLL '{DLL_NAME}' not found. Check Assets/Plugins/x86_64/.");
            Debug.LogException(ex);
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            Debug.LogError($"[NativeInterface] Critical Error: Function 'CalculateCrystalState' not found in DLL.");
            Debug.LogException(ex);
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NativeInterface] Unknown Error during DLL execution.");
            Debug.LogException(ex);
            return false;
        }
    }

    // ========================================================================
    // 4. 辅助验证逻辑
    // ========================================================================

    private static bool ValidateInput(ref SimInputData data)
    {
        if (data.static_n == null || data.static_n.Length != 3) return false;
        if (data.r_tensor == null || data.r_tensor.Length != 18) return false; // 最关键的检查
        if (data.e_field_local == null || data.e_field_local.Length != 3) return false;
        if (data.wave_vector == null || data.wave_vector.Length != 3) return false;
        return true;
    }

    private static bool ValidateOutput(ref CrystalOutputData data)
    {
        if (data.n_prime == null || data.n_prime.Length != 3) return false;
        if (data.rotation_matrix == null || data.rotation_matrix.Length != 9) return false;
        return true;
    }
}