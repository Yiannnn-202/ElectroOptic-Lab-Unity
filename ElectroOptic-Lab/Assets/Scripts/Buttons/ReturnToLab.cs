using UnityEngine;

/// <summary>
/// 返回主实验室场景
/// </summary>
public class ReturnToLab : MonoBehaviour
{
    public void GoBack()
    {
        Scene2AdditionalSceneNavigator.ReturnFromAdditionalExperiment(Scene2AdditionalSceneNavigator.AdditionalSceneName);
    }
}
