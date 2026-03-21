using UnityEngine;
using UnityEngine.SceneManagement;

public class ClickAreaFocus : MonoBehaviour
{
    public Transform focusPoint;
    public string targetSceneName;
    public float doubleClickInterval = 0.35f;

    private float lastClickTime = -1f;

    void OnMouseDown()
    {
        float timeSinceLastClick = Time.time - lastClickTime;

        // 双击：跳转场景
        if (timeSinceLastClick <= doubleClickInterval)
        {
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                SceneManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogWarning("还没有填写 targetSceneName。");
            }

            lastClickTime = -1f;
            return;
        }

        // 单击：拉近聚焦
        lastClickTime = Time.time;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        CameraFocusController controller = mainCam.GetComponent<CameraFocusController>();
        if (controller == null) return;

        controller.FocusOn(focusPoint);
    }
}