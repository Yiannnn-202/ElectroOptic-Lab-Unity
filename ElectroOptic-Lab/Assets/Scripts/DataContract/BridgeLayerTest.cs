using UnityEngine;
using System;

public class BridgeLayerTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("<b><color=cyan>[Bridge Test] === 开始桥接层验收测试 ===</color></b>");

        // 1. 运行快乐路径测试 (验证功能)
        TestHappyPath();

        // 2. 运行破坏性测试 (验证安全性)
        TestSabotage();
    }

    /// <summary>
    /// 测试用例 A：正常流程
    /// 目标：确保物理计算正确，且 SafeCalculate 返回 true
    /// </summary>
    void TestHappyPath()
    {
        Debug.Log("<b>[Test A] 正在执行: 正常调用测试 (Happy Path)...</b>");

        // A1. 准备数据容器
        SimInputData input = new SimInputData();
        CrystalOutputData output = new CrystalOutputData();

        // A2. 必须初始化内存 (关键步骤)
        input.Initialize();
        output.Initialize();

        // A3. 填充 LiNbO3 物理参数
        double no = 2.286;
        double ne = 2.200;
        input.static_n[0] = no; input.static_n[1] = no; input.static_n[2] = ne;

        // 填充电光系数 (简化版，仅用于非零测试)
        double r33 = 30.9e-12;
        input.r_tensor[8] = r33; // zz 位置

        // 设定探测模式：光沿 Y，电场沿 Z (单位向量)
        input.wave_vector[1] = 1.0;
        input.e_field_local[2] = 1.0;

        // A4. 调用安全接口
        bool success = NativeInterface.SafeCalculate(ref input, ref output);

        // A5. 验证结果
        if (success)
        {
            Debug.Log($"<color=green>[PASS] 正常调用成功！</color>");
            Debug.Log($"   -> 计算出的灵敏度: {output.sensitivity} (预期约为 1e-10)");
            Debug.Log($"   -> 新折射率 nz': {output.n_prime[2]}");
        }
        else
        {
            Debug.LogError("<color=red>[FAIL] 正常调用失败！请检查 NativeInterface 日志。</color>");
        }
    }

    /// <summary>
    /// 测试用例 B：破坏性测试
    /// 目标：故意制造空指针，验证 Unity 是否不崩溃且 SafeCalculate 返回 false
    /// </summary>
    void TestSabotage()
    {
        Debug.Log("<b>[Test B] 正在执行: 破坏性测试 (Sabotage Test)...</b>");

        // B1. 准备数据
        SimInputData input = new SimInputData();
        CrystalOutputData output = new CrystalOutputData();

        // B2. 正常初始化
        input.Initialize();
        output.Initialize();

        // B3. 【恶意破坏】将核心数组设为 null，制造非法内存
        // 如果 NativeInterface 没有检查，这一步会导致 Unity 直接崩溃 (Crash)
        input.r_tensor = null;
        Debug.Log("   -> 已恶意将 input.r_tensor 设为 null，准备调用...");

        // B4. 调用接口
        // 预期结果：函数内部捕获错误，返回 false，且 Unity 依然存活
        bool success = NativeInterface.SafeCalculate(ref input, ref output);

        // B5. 验证结果
        if (success == false)
        {
            Debug.Log("<color=green>[PASS] 安全拦截成功！</color>");
            Debug.Log("   -> NativeInterface 正确识别了非法输入并拒绝了执行。");
            Debug.Log("   -> (请忽略上方红色报错，那是 NativeInterface 打印的预期错误日志)");
        }
        else
        {
            Debug.LogError("<color=red>[FAIL] 安全拦截失败！DLL 竟然在非法数据下执行成功了？</color>");
        }
    }
}