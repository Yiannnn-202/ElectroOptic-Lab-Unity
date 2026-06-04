using UnityEngine;

/// <summary>
/// 返回主实验室场景
/// 支持从附加实验场景（Scene_additional_exp）或独立实验场景（Scene3/Scene4）返回
/// 在 Scene3/Scene4 的 Inspector 中设置 sceneToUnload 为对应的场景名即可
/// </summary>
public class ReturnToLab : MonoBehaviour
{
    [Tooltip("要卸载的当前场景名，如 Scene3_UIRebuild / Scene4_UIRebuild 1。留空则默认为附加实验场景")]
    public string sceneToUnload = string.Empty;

    public void GoBack()
    {
        string scene = string.IsNullOrEmpty(sceneToUnload)
            ? Scene2AdditionalSceneNavigator.AdditionalSceneName
            : sceneToUnload;

        Scene2AdditionalSceneNavigator.ReturnFromAdditionalExperiment(scene);
    }
}
