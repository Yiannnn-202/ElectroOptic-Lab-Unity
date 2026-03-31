using System.Collections;
using UnityEngine;

public class ExperimentCameraController : MonoBehaviour
{
    [Header("运镜设置")]
    public float transitionDuration = 1.0f; // 镜头切换时间

    private Vector3 defaultPosition;
    private Quaternion defaultRotation;

    // 记录当前正在特写哪个物体，方便返回时解锁
    private RailObjectMover lockedMover;

    private void Start()
    {
        // 记录全局初始视角
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;
    }

    // 触发特写（绑定到UI按钮）
    public void GoToCloseUpView()
    {
        RailObjectMover target = RailObjectMover.CurrentActiveMover;

        if (target != null && target.closeUpCameraAnchor != null)
        {
            // 1. 锁定 AD 键移动
            target.isMovementLocked = true;
            lockedMover = target;

            // 2. 运镜
            StopAllCoroutines();
            StartCoroutine(MoveCamera(target.closeUpCameraAnchor.position, target.closeUpCameraAnchor.rotation));
        }
        else
        {
            Debug.LogWarning("未选中任何导轨原件，或该原件没有配置 Close Up Camera Anchor！");
        }
    }

    // 返回全景（绑定到UI按钮）
    public void ReturnToDefaultView()
    {
        // 1. 解锁移动
        if (lockedMover != null)
        {
            lockedMover.isMovementLocked = false;
            lockedMover = null;
        }

        // 2. 运镜返回
        StopAllCoroutines();
        StartCoroutine(MoveCamera(defaultPosition, defaultRotation));
    }

    // 平滑运镜协程
    private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / transitionDuration);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsedTime / transitionDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
    }
}