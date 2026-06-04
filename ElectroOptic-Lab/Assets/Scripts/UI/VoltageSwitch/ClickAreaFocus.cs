using UnityEngine;
using UnityEngine.SceneManagement;

public class ClickAreaFocus : MonoBehaviour
{
    public Transform focusPoint;
    public string targetSceneName;
    public float doubleClickInterval = 0.35f;

    private float lastClickTime = -1f;

    // 【新增】状态标记：记录当前是否已经处于聚焦状态
    private bool isAlreadyFocused = false;

    void OnMouseDown()
    {
        // 全局特写锁：防止在CloseUp视角中误触其他区域
        if (ExperimentCameraController.IsInCloseUpView) return;

        float timeSinceLastClick = Time.time - lastClickTime;

        // 如果两次点击时间间隔小于设定值，说明是【双击】
        if (timeSinceLastClick <= doubleClickInterval)
        {
            if (!isAlreadyFocused)
            {
                // 状态为 false：说明是【第一次双击】，执行拉近聚焦
                TriggerFocus();
                isAlreadyFocused = true; // 将状态锁定为已聚焦
            }
            else
            {
                // 状态为 true：说明是【第二次双击】，执行场景跳转
                TriggerSceneSwitch();
            }

            // 执行完双击逻辑后，重置点击时间，防止连续快速点击（比如连点三下）导致错乱
            lastClickTime = -1f;
            return;
        }

        // 记录这一次点击的时间，用于下一次判断是否构成双击
        lastClickTime = Time.time;
    }

    // ================== 具体功能方法抽离 ==================

    private void TriggerFocus()
    {
        Debug.Log("触发第一次双击：拉近视角");
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        CameraFocusController controller = mainCam.GetComponent<CameraFocusController>();
        if (controller == null) return;

        controller.FocusOn(focusPoint);
    }

    private void TriggerSceneSwitch()
    {
        Debug.Log("触发第二次双击：切换场景");
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            // 如果 Scene2 还活着，用 additive 模式加载以保留 Scene2 状态
            // （与 Scene_additional_exp 跳转机制相同）
            Scene labScene = SceneManager.GetSceneByName(Scene2AdditionalSceneNavigator.LabSceneName);
            if (labScene.isLoaded)
            {
                Scene2AdditionalSceneNavigator.OpenAdditionalExperiment(targetSceneName);
            }
            else
            {
                SceneManager.LoadScene(targetSceneName);
            }
        }
        else
        {
            Debug.LogWarning("还没有填写 targetSceneName。");
        }
    }

    // 【新增扩展】：预留一个重置状态的方法
    // 如果你在其他地方（比如按了某个“返回全局视角”的按钮），
    // 你可能需要调用这个方法把 isAlreadyFocused 变回 false，以便下次还能重新双击拉近。
    public void ResetFocusState()
    {
        isAlreadyFocused = false;
    }
}