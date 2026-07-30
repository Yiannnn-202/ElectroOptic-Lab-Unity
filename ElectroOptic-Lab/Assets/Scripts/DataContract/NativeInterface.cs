using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class NativeInterface
{
    // ========================================================================
    // 1. DLL ����
    // ========================================================================
    private const string DLL_NAME = "CrystalPhysicsCore";
    private static bool _warnedManagedFallback;

    // ========================================================================
    // 2. ԭ������ (Private)
    //    ʹ�� ref ���ݽṹ��ָ�룬�������
    // ========================================================================
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void CalculateCrystalState(ref SimInputData input, ref CrystalOutputData output);
#endif

    // ========================================================================
    // 3. ��ȫ��װ�� (Public API)
    //    ְ���ڴ��顢�쳣������־��¼
    // ========================================================================

    /// <summary>
    /// ���õײ�������ˡ�
    /// </summary>
    /// <param name="input">�������� (������ Initialize)</param>
    /// <param name="output">������� (������ Initialize)</param>
    /// <returns>�ɹ����� true��ʧ�ܷ��� false</returns>
    public static bool SafeCalculate(ref SimInputData input, ref CrystalOutputData output)
    {
        // --- A. �ڴ氲ȫ��� (Pre-flight Check) ---
        // ��ֹ����δ����򳤶ȴ����µ� C++ Խ�����
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

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // --- B. ��ȫ���� (Safe Invocation) ---
        try
        {
            CalculateCrystalState(ref input, ref output);
            return true;
        }
        catch (DllNotFoundException ex)
        {
            Debug.LogWarning($"[NativeInterface] DLL '{DLL_NAME}' not found. Falling back to managed approximation.");
            Debug.LogException(ex);
            FillManagedFallback(ref input, ref output, "DLL was not found");
            return true;
        }
        catch (EntryPointNotFoundException ex)
        {
            Debug.LogWarning($"[NativeInterface] Function 'CalculateCrystalState' not found in DLL. Falling back to managed approximation.");
            Debug.LogException(ex);
            FillManagedFallback(ref input, ref output, "DLL entry point was not found");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NativeInterface] Unknown Error during DLL execution.");
            Debug.LogException(ex);
            return false;
        }
#else
        FillManagedFallback(ref input, ref output, "native CrystalPhysicsCore is unavailable on this platform");
        return true;
#endif
    }

    private static void FillManagedFallback(ref SimInputData input, ref CrystalOutputData output, string reason)
    {
        if (!_warnedManagedFallback)
        {
            Debug.LogWarning($"[NativeInterface] Using managed crystal physics fallback because {reason}. This keeps mobile builds running, but is less accurate than the native backend.");
            _warnedManagedFallback = true;
        }

        output.Initialize();

        for (int i = 0; i < 3; i++)
        {
            double n = input.static_n[i];
            double delta = -0.5 * n * n * n * ResolveLinearTensorTerm(input, i, input.e_field_local);
            output.n_prime[i] = Math.Max(0.000001, n + delta);
        }

        output.rotation_matrix[0] = 1.0;
        output.rotation_matrix[1] = 0.0;
        output.rotation_matrix[2] = 0.0;
        output.rotation_matrix[3] = 0.0;
        output.rotation_matrix[4] = 1.0;
        output.rotation_matrix[5] = 0.0;
        output.rotation_matrix[6] = 0.0;
        output.rotation_matrix[7] = 0.0;
        output.rotation_matrix[8] = 1.0;
        output.sensitivity = EstimateSensitivity(input);
    }

    private static double EstimateSensitivity(SimInputData input)
    {
        double sensitivity = 0.0;
        for (int i = 0; i < 3; i++)
        {
            double n = input.static_n[i];
            double candidate = Math.Abs(-0.5 * n * n * n * ResolveLinearTensorTerm(input, i, input.e_field_local));
            if (candidate > sensitivity)
            {
                sensitivity = candidate;
            }
        }

        return sensitivity;
    }

    private static double ResolveLinearTensorTerm(SimInputData input, int tensorRow, double[] field)
    {
        int rowOffset = tensorRow * 3;
        return input.r_tensor[rowOffset] * field[0]
            + input.r_tensor[rowOffset + 1] * field[1]
            + input.r_tensor[rowOffset + 2] * field[2];
    }

    // ========================================================================
    // 4. ������֤�߼�
    // ========================================================================

    private static bool ValidateInput(ref SimInputData data)
    {
        if (data.static_n == null || data.static_n.Length != 3) return false;
        if (data.r_tensor == null || data.r_tensor.Length != 18) return false; // ��ؼ��ļ��
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