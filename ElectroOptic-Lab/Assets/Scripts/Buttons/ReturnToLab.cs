using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 返回主实验室场景
/// </summary>
public class ReturnToLab : MonoBehaviour
{
    public void GoBack()
    {
        SceneManager.LoadScene("Scene2.The Lab");
    }
}
