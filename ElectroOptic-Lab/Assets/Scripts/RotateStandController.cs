using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class RotateStandController : MonoBehaviour
{
    [Header("旋转配置")]
    [Tooltip("旋转速度（度/秒）")]
    public float rotateSpeed = 90f;

    [Header("UI图片设置 (重要)")]
    [Tooltip("请把 Assets/UI/mine.png 拖到这里！")]
    public Texture2D customDialTexture; // 🔥🔥🔥 新增：用来放你的图片

    [Header("双击配置")]
    [Tooltip("双击间隔阈值")]
    public float doubleClickInterval = 0.3f;

    [Header("调试设置")]
    public bool showDebug = true;

    // --- 【核心】全局互斥锁 ---
    public static bool IsAnyStandSelected = false;

    // 私有变量
    private bool isRotating = false;
    private float lastClickTime = 0f;
    private RotateWindowController rotateWindow;
    private bool isProcessingClick = false;

    // 避免与父物体冲突
    private bool isSelected = false;
    private Renderer objectRenderer;
    private Color originalColor;

    // 静态变量管理当前选中的旋转座
    private static RotateStandController currentSelectedStand = null;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalColor = objectRenderer.material.color;
        }
    }

    void Update()
    {
        if (isSelected)
        {
            HandleRotation();
        }

        if (!isProcessingClick)
        {
            StartCoroutine(HandleClickWithDelay());
        }
    }

    private void HandleRotation()
    {
        float direction = 0f;
        if (Input.GetKey(KeyCode.A)) direction = 1f;
        else if (Input.GetKey(KeyCode.D)) direction = -1f;

        if (direction != 0f)
        {
            if (!isRotating)
            {
                isRotating = true;
                if (showDebug) Debug.Log($"🔄 {gameObject.name} 开始旋转");
            }
            transform.Rotate(Vector3.forward, direction * rotateSpeed * Time.deltaTime);
        }
        else if (isRotating)
        {
            isRotating = false;
        }
    }

    private IEnumerator HandleClickWithDelay()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isProcessingClick = true;
            yield return null;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                isProcessingClick = false;
                yield break;
            }

            if (IsClickingThisRotateStand())
            {
                float currentTime = Time.time;
                if (currentTime - lastClickTime <= doubleClickInterval)
                {
                    if (showDebug) Debug.Log($"🎯 {gameObject.name}: 双击触发！");
                    OnRotateStandDoubleClick();
                    lastClickTime = 0f;
                }
                else
                {
                    lastClickTime = currentTime;
                    SelectThisStand();
                }
            }
            isProcessingClick = false;
        }
    }

    private void SelectThisStand()
    {
        if (currentSelectedStand == this) return;

        if (currentSelectedStand != null)
        {
            currentSelectedStand.DeselectStand();
        }

        currentSelectedStand = this;
        isSelected = true;
        IsAnyStandSelected = true;

        if (objectRenderer != null) objectRenderer.material.color = Color.green;
        if (showDebug) Debug.Log($"✅ 选中旋转座: {gameObject.name}");
    }

    public void DeselectStand()
    {
        isSelected = false;
        if (objectRenderer != null) objectRenderer.material.color = originalColor;

        if (currentSelectedStand == this)
        {
            currentSelectedStand = null;
            IsAnyStandSelected = false;
        }
        if (showDebug) Debug.Log($"❌ 取消选中旋转座: {gameObject.name}");
    }

    public static void DeselectAll()
    {
        if (currentSelectedStand != null)
        {
            currentSelectedStand.DeselectStand();
        }
        IsAnyStandSelected = false;
    }

    private bool IsClickingThisRotateStand()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == this.gameObject || hit.collider.transform.IsChildOf(this.transform))
                return true;

            RotateStandController clickedStand = hit.collider.GetComponentInParent<RotateStandController>();
            if (clickedStand == this) return true;
        }
        return false;
    }

    private void OnRotateStandDoubleClick()
    {
        SelectThisStand();
        if (rotateWindow == null)
            rotateWindow = RotateWindowController.CreateRotateWindow(this);
        else
            rotateWindow.ShowWindow();
    }

    public float GetCurrentRotateAngle() => transform.eulerAngles.z;
    public string GetRotateStandName() => gameObject.name;

    void OnDestroy()
    {
        if (currentSelectedStand == this)
        {
            currentSelectedStand = null;
            IsAnyStandSelected = false;
        }
    }
}
