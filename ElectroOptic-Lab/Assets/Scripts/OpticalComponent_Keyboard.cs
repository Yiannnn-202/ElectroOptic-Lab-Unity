using UnityEngine;
using ElectroOptics.WebStreaming;

// 键盘控制：拾取移动，点击放下/吸附导轨，双击再次拾起
public class OpticalComponent : MonoBehaviour
{
    [Header("基础")]
    public LayerMask railLayer;
    public float hoverHeight = 0.5f;
    public float moveSpeed = 2.0f;

    [Header("吸附配置")]
    public float snapRotationY = -90f;
    public float detectionRadius = 0.5f;
    [Tooltip("手动 Y 偏移：正值上抬，负值下压，修正碰撞体与模型底部不一致")]
    public float snapYOffset = 0f;

    [Header("状态")]
    public bool isSelected = false;
    public bool isOnRail = false;

    // 静态变量：记录当前全场唯一选中的物体
    private static OpticalComponent currentSelectedComponent;

    private Vector3 originalPos;
    private OpticalRail currentRail;

    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.3f;

    private Outline outline;

    void Start()
    {
        originalPos = transform.position;

        outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }
        outline.enabled = false;
    }

    // 核心逻辑修改：点击物体时的处理
    void OnMouseDown()
    {
        float timeSinceLastClick = Time.time - lastClickTime;
        lastClickTime = Time.time;

        // 如果点击的是已经吸附在导轨上的物体
        if (isOnRail)
        {
            if (timeSinceLastClick < DOUBLE_CLICK_TIME)
            {
                Debug.Log("双击检测：准备取下新物体");

                // --- 关键点：如果当前已经手里有物体，先让那个旧物体放下 ---
                if (currentSelectedComponent != null && currentSelectedComponent != this)
                {
                    currentSelectedComponent.TryDrop();
                }

                isOnRail = false;
                PickUp();
            }
            return;
        }

        // 如果点击的是普通状态的物体
        if (!isSelected)
        {
            // --- 关键点：如果当前已经手里有物体，先让那个旧物体放下 ---
            if (currentSelectedComponent != null && currentSelectedComponent != this)
            {
                currentSelectedComponent.TryDrop();
            }

            PickUp();
        }
        else
        {
            // 如果点的是自己，则放下
            TryDrop();
        }
    }

    void Update()
    {
        // 只有当前物体是被选中的唯一物体，才响应操作
        if (isSelected && currentSelectedComponent == this)
        {
            HandleKeyboardMove();

            if (RemoteInputRelay.GetKeyDown(KeyCode.Space))
            {
                TryDrop();
            }
        }
    }

    void PickUp()
    {
        currentSelectedComponent = this;

        isSelected = true;
        isOnRail = false;
        currentRail = null;

        if (outline != null) outline.enabled = true;

        Vector3 currentPos = transform.position;
        currentPos.y = originalPos.y + hoverHeight;
        transform.position = currentPos;

        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;

        Debug.Log("已选中：" + gameObject.name);
    }

    void HandleKeyboardMove()
    {
        float h = RemoteInputRelay.GetAxis("Vertical");
        float v = RemoteInputRelay.GetAxis("Horizontal");

        Vector3 movement = new Vector3(-h, 0, v) * moveSpeed * Time.deltaTime;
        transform.Translate(movement, Space.World);
    }

    public void TryDrop()
    {
        // 如果我是当前占位物体，释放占位符
        if (currentSelectedComponent == this)
        {
            currentSelectedComponent = null;
        }

        if (CheckDropTarget())
        {
            isSelected = false;
        }
        else
        {
            isSelected = false;
            Vector3 landPos = transform.position;
            landPos.y = originalPos.y;
            transform.position = landPos;
            Debug.Log("放下，未吸附导轨");
        }

        if (outline != null) outline.enabled = false;
    }

    private bool CheckDropTarget()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, railLayer);

        foreach (var col in hitColliders)
        {
            OpticalRail railScript = col.GetComponent<OpticalRail>();

            if (railScript != null)
            {
                SnapToRail(railScript);
                return true;
            }
        }
        return false;
    }

    private void SnapToRail(OpticalRail rail)
    {
        isOnRail = true;
        currentRail = rail;

        transform.rotation = Quaternion.Euler(0, snapRotationY, 0);
        Vector3 finalPos = rail.GetSnapPosition(transform.position);

        Collider col = GetComponent<Collider>();
        if (col == null) col = GetComponentInChildren<Collider>();
        if (col != null)
        {
            float pivotToBottom = transform.position.y - col.bounds.min.y;
            finalPos.y += pivotToBottom;
        }

        finalPos.y += snapYOffset;
        transform.position = finalPos;

        RefreshCrystalRuntimeAfterSnap();

        Debug.Log("已吸附到导轨");
    }

    private void RefreshCrystalRuntimeAfterSnap()
    {
        var controller = GetComponent<ElectroOptics.Experiment.Controller.CrystalControllerWrapper>();
        if (controller == null) controller = GetComponentInParent<ElectroOptics.Experiment.Controller.CrystalControllerWrapper>();
        if (controller == null) controller = GetComponentInChildren<ElectroOptics.Experiment.Controller.CrystalControllerWrapper>();

        var initializer = Object.FindFirstObjectByType<ElectroOptics.Experiment.Initializer.CrystalComponentInitializer>();
        bool isCrystalModel = initializer != null && IsSameHierarchy(initializer.GetCrystalModel());

        if (controller == null && !isCrystalModel)
        {
            return;
        }

        if (controller != null)
        {
            controller.RefreshPhysicsConfig();
        }

        if (initializer != null)
        {
            initializer.RefreshDirectRetarderSettings();
        }
    }

    private bool IsSameHierarchy(GameObject other)
    {
        if (other == null)
        {
            return false;
        }

        return other == gameObject
               || other.transform.IsChildOf(transform)
               || transform.IsChildOf(other.transform);
    }

    void OnDrawGizmos()
    {
        if (isSelected)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
