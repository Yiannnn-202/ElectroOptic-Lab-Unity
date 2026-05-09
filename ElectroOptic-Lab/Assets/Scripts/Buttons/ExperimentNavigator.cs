using ElectroOptics.DataTransfer;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 实验场景统一导航
/// 在主菜单设定目标实验场景 → 进入晶体选择预览 → 选完后自动跳转到目标场景
/// </summary>
public static class ExperimentNavigator
{
    /// <summary>
    /// 默认晶体选择预览场景名称
    /// </summary>
    public const string PreviewSceneName = "Scene2-preview";

    /// <summary>
    /// 默认实验场景（fallback）
    /// </summary>
    public const string DefaultLabSceneName = "Scene2.The Lab";

    /// <summary>
    /// 设定目标实验场景并进入晶体选择预览
    /// 在主菜单按钮 onClick 中调用
    /// </summary>
    /// <param name="targetSceneName">目标实验场景名称</param>
    public static void SetDestinationAndGoToPreview(string targetSceneName)
    {
        CrystalSelectionData.TargetSceneName = targetSceneName;
        Debug.Log($"[ExperimentNavigator] 设定目标场景: {targetSceneName}, 进入晶体选择预览");
        SceneManager.LoadScene(PreviewSceneName);
    }

    /// <summary>
    /// 加载目标实验场景
    /// CrystalCardSelector 在用户选完晶体后调用
    /// 优先使用 CrystalSelectionData.TargetSceneName, 未设置则 fallback 到默认场景
    /// </summary>
    public static void GoToTargetScene()
    {
        string targetScene = CrystalSelectionData.HasTargetScene
            ? CrystalSelectionData.TargetSceneName
            : DefaultLabSceneName;

        Debug.Log($"[ExperimentNavigator] 加载目标场景: {targetScene}");
        SceneManager.LoadScene(targetScene);
    }

    /// <summary>
    /// 加载指定场景（不做晶体数据检查，仅封装 LoadScene + 日志）
    /// 用于场景内导航按钮
    /// </summary>
    public static void GoToScene(string sceneName)
    {
        Debug.Log($"[ExperimentNavigator] 加载场景: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}
