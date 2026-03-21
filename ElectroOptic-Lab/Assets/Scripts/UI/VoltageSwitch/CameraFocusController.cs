using UnityEngine;

public class CameraFocusController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float rotateSpeed = 4f;

    private Transform targetPoint;
    private bool isMoving = false;

    void Update()
    {
        if (!isMoving || targetPoint == null) return;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPoint.position,
            Time.deltaTime * moveSpeed
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetPoint.rotation,
            Time.deltaTime * rotateSpeed
        );

        float distance = Vector3.Distance(transform.position, targetPoint.position);
        float angle = Quaternion.Angle(transform.rotation, targetPoint.rotation);

        if (distance < 0.02f && angle < 0.5f)
        {
            transform.position = targetPoint.position;
            transform.rotation = targetPoint.rotation;
            isMoving = false;
        }
    }

    public void FocusOn(Transform focusPoint)
    {
        if (focusPoint == null) return;

        targetPoint = focusPoint;
        isMoving = true;
    }
}