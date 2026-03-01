using UnityEngine;

public class CameraSwitch : MonoBehaviour
{
    public Transform mainCamera;   // 你的主摄像机
    public Transform frontView;    // 直视图的位置标记
    public Transform topView;      // 俯视图的位置标记

    public float transitionSpeed = 5f; // 镜头平滑移动的速度

    private Transform targetView;

    void Start()
    {
        // 初始化时保持摄像机在当前位置
        if (mainCamera == null) mainCamera = Camera.main.transform;
        targetView = mainCamera;
    }

    void Update()
    {
        // 如果设定了目标视角，就让摄像机平滑过渡过去
        if (targetView != null && mainCamera.position != targetView.position)
        {
            mainCamera.position = Vector3.Lerp(mainCamera.position, targetView.position, Time.deltaTime * transitionSpeed);
            mainCamera.rotation = Quaternion.Slerp(mainCamera.rotation, targetView.rotation, Time.deltaTime * transitionSpeed);
        }
    }

    // 绑定给直视图按钮的方法
    public void GoToFrontView()
    {
        targetView = frontView;
    }

    // 绑定给俯视图按钮的方法
    public void GoToTopView()
    {
        targetView = topView;
    }
}