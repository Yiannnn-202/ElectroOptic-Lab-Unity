using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoad: MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Onclick_Btn_LoadScene_01()
    {
        SceneManager.LoadScene(1);
    }

    public void Onclick_Btn_LoadScene_02_preview()
    {
        // 直接进入晶体选择预览（无预设目标，选完后默认去 Scene2）
        SceneManager.LoadScene(2);
    }

    public void Onclick_Btn_LoadScene_02()
    {
        SceneManager.LoadScene("Scene2.The Lab");
    }

    public void Onclick_Btn_LoadScene_05()
    {
        SceneManager.LoadScene(4);
    }

    public void Onclick_Btn_Return()
    {
        SceneManager.LoadScene(0);
    }

    // --- 经由晶体选择预览的导航方法 ---

    /// <summary>
    /// 设定目标为主实验室，进入晶体选择预览
    /// </summary>
    public void Onclick_Btn_ViaPreview_ToLab()
    {
        ExperimentNavigator.SetDestinationAndGoToPreview(ExperimentNavigator.DefaultLabSceneName);
    }

    /// <summary>
    /// 设定目标为示波器实验，进入晶体选择预览
    /// </summary>
    public void Onclick_Btn_ViaPreview_ToOscilloscope()
    {
        const string oscilloscopeScene = "Scene4_UIRebuild 1";
        ExperimentNavigator.SetDestinationAndGoToPreview(oscilloscopeScene);
    }

    /// <summary>
    /// 设定目标为数据记录实验（Scene3），进入晶体选择预览
    /// </summary>
    public void Onclick_Btn_ViaPreview_ToScene3()
    {
        const string scene3 = "Scene3_UIRebuild";
        ExperimentNavigator.SetDestinationAndGoToPreview(scene3);
    }
}
