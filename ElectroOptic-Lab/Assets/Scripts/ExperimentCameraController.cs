using System.Collections;
using UnityEngine;

public class ExperimentCameraController : MonoBehaviour
{
    [Header("运镜设置")]
    public float transitionDuration = 1.0f;

    private Vector3 defaultPosition;
    private Quaternion defaultRotation;

    private RailObjectMover lockedMover;

    private void Start()
    {
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;
    }

    public void GoToCloseUpView()
    {
        RailObjectMover target = RailObjectMover.CurrentActiveMover;

        if (target != null && target.closeUpCameraAnchor != null)
        {
            target.isMovementLocked = true;
            lockedMover = target;

            // 【新增】：通知晶体，把它的 UI 画布弹出来
            target.ToggleCloseUpUI(true);

            StopAllCoroutines();
            StartCoroutine(MoveCamera(target.closeUpCameraAnchor.position, target.closeUpCameraAnchor.rotation));
        }
    }

    public void ReturnToDefaultView()
    {
        if (lockedMover != null)
        {
            lockedMover.isMovementLocked = false;

            // 【新增】：通知晶体，把它的 UI 画布藏起来
            lockedMover.ToggleCloseUpUI(false);

            lockedMover = null;
        }

        StopAllCoroutines();
        StartCoroutine(MoveCamera(defaultPosition, defaultRotation));
    }

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