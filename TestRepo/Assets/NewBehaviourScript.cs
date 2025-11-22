using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 3f;        // 移动速度
    public float jumpForce = 7f;        // 跳跃力量

    [Header("旋转设置")]
    public float rotationSpeed = 2f;    // 旋转速度
    public bool invertRotation = false;

    [Header("物理组件")]
    private Rigidbody rb;

    private bool isDragging = false;
    private Vector3 lastMousePosition;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 如果没有Rigidbody，自动添加一个
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // 锁定旋转，防止物体翻滚
        rb.freezeRotation = true;

        Debug.Log("改进版移动脚本已启动 - 按住WASD持续移动，空格键跳跃");
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Jump();
        }
        // 鼠标按下开始拖动
        if (Input.GetMouseButtonDown(0)) // 0 = 左键
        {
            StartDragging();
        }

        // 鼠标按住时持续检测拖动
        if (Input.GetMouseButton(0) && isDragging)
        {
            HandleRotation();
        }

        // 鼠标释放结束拖动
        if (Input.GetMouseButtonUp(0))
        {
            StopDragging();
        }
        MovePlayer();
    }

    void StartDragging()
    {
        isDragging = true;
        lastMousePosition = Input.mousePosition;
    }

    void HandleRotation()
    {
        // 获取当前鼠标位置
        Vector3 currentMousePosition = Input.mousePosition;

        // 计算鼠标水平移动量
        float mouseXDelta = currentMousePosition.x - lastMousePosition.x;

        // 应用旋转速度系数和方向
        float rotationAmount = mouseXDelta * rotationSpeed;
        if (invertRotation)
        {
            rotationAmount = -rotationAmount;
        }

        // 绕Y轴旋转物体
        transform.Rotate(0, rotationAmount, 0, Space.World);

        // 更新上一帧鼠标位置
        lastMousePosition = currentMousePosition;
    }

    void StopDragging()
    {
        isDragging = false;
    }

    void MovePlayer()
    {
        // 初始化移动向量
        Vector3 movement = Vector3.zero;

        // 检测持续按键输入
        if (Input.GetKey(KeyCode.A))
        {
            movement.x = -1f; // 向左移动
        }
        if (Input.GetKey(KeyCode.D))
        {
            movement.x = 1f;  // 向右移动
        }
        if (Input.GetKey(KeyCode.W))
        {
            movement.z = 1f;  // 向前移动
        }
        if (Input.GetKey(KeyCode.S))
        {
            movement.z = -1f; // 向后移动
        }

        // 标准化移动向量，确保斜向移动速度一致
        if (movement.magnitude > 1f)
        {
            movement.Normalize();
        }

        // 应用移动速度
        movement *= moveSpeed;

        // 保持Y轴速度不变（让重力作用）
        movement.y = rb.velocity.y;

        // 应用速度到刚体
        rb.velocity = movement;
    }

    void Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }
}
