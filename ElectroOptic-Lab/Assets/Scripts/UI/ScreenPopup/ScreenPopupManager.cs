using UnityEngine;
using UnityEngine.EventSystems;
using ElectroOptics.DataTransfer;

namespace ElectroOptics.UI.ScreenPopup
{
    /// <summary>
    /// 光屏弹窗管理器
    /// 挂载在光屏对象上，检测双击并调度显示锥光干涉弹窗或红点弹窗
    /// </summary>
    public class ScreenPopupManager : MonoBehaviour
    {
        #region Inspector 配置

        [Header("引用配置")]
        [Tooltip("现有的光屏控制器组件")]
        [SerializeField] private DirectScreenController directScreenController;

        [Header("检测配置")]
        [Tooltip("晶体检测范围")]
        [SerializeField] private float detectionRange = 2.0f;

        [Header("双击配置")]
        [Tooltip("双击判定时间间隔（秒）")]
        [SerializeField] private float doubleClickInterval = 0.3f;

        [Header("弹窗配置")]
        [Tooltip("锥光干涉弹窗尺寸")]
        [SerializeField] private Vector2 windowSize = new Vector2(600, 600);

        [Tooltip("弹窗标题")]
        [SerializeField] private string windowTitle = "锥光干涉图";

        #endregion

        #region 私有字段

        private float _lastClickTime = 0f;
        private ConoscopicWindowView _conoscopicWindow;

        #endregion

        #region Unity 生命周期

        private void Start()
        {
            // 如果没有手动指定，尝试获取同对象上的组件
            if (directScreenController == null)
            {
                directScreenController = GetComponent<DirectScreenController>();
            }

            if (directScreenController == null)
            {
                Debug.LogWarning("[ScreenPopupManager] DirectScreenController 未设置，红点弹窗功能将不可用");
            }
            else
            {
                // 禁用 DirectScreenController 组件，防止其 OnMouseDown 与本组件冲突
                // 但保留引用，用于手动调用 OpenDisplayWindow()
                directScreenController.enabled = false;
                Debug.Log("[ScreenPopupManager] 已禁用 DirectScreenController 组件，由本组件接管双击事件");
            }
        }

        private void OnMouseDown()
        {
            // 检查是否点击在 UI 上
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // 检测双击
            float currentTime = Time.time;
            if (currentTime - _lastClickTime <= doubleClickInterval)
            {
                HandleDoubleClick();
                _lastClickTime = 0f;
            }
            else
            {
                _lastClickTime = currentTime;
            }
        }

        #endregion

        #region 核心方法

        /// <summary>
        /// 处理双击事件
        /// </summary>
        private void HandleDoubleClick()
        {
            Debug.Log($"[ScreenPopupManager] 检测到双击光屏: {gameObject.name}");

            // 检测晶体是否在附近
            if (HasCrystalNearby())
            {
                Debug.Log("[ScreenPopupManager] 检测到晶体，显示锥光干涉弹窗");
                ShowConoscopicWindow();
            }
            else
            {
                Debug.Log("[ScreenPopupManager] 未检测到晶体，显示红点弹窗");
                ShowRedDotWindow();
            }
        }

        /// <summary>
        /// 检测晶体是否在光屏附近
        /// </summary>
        /// <returns>是否有晶体</returns>
        private bool HasCrystalNearby()
        {
            // 检查 CrystalRuntime 是否已初始化
            if (!CrystalRuntime.IsInitialized)
            {
                return false;
            }

            // 使用位置距离检测
            return CrystalRuntime.IsCrystalOnRail(transform.position, detectionRange);
        }

        /// <summary>
        /// 显示锥光干涉弹窗
        /// </summary>
        private void ShowConoscopicWindow()
        {
            // 检查 CrystalRuntime 是否已初始化
            if (!CrystalRuntime.IsInitialized)
            {
                Debug.LogWarning("[ScreenPopupManager] CrystalRuntime 未初始化，无法显示锥光干涉弹窗");
                ShowRedDotWindow();
                return;
            }

            // 检查纹理渲染器
            if (CrystalRuntime.TextureRenderer == null || !CrystalRuntime.TextureRenderer.IsInitialized)
            {
                Debug.LogWarning("[ScreenPopupManager] TextureRenderer 未初始化，无法显示锥光干涉弹窗");
                ShowRedDotWindow();
                return;
            }

            // 创建或显示弹窗
            if (_conoscopicWindow == null)
            {
                _conoscopicWindow = ConoscopicWindowView.CreateWindow(windowSize, windowTitle);
            }

            _conoscopicWindow.Show();
        }

        /// <summary>
        /// 显示红点弹窗（调用原有的 DirectScreenController）
        /// </summary>
        private void ShowRedDotWindow()
        {
            if (directScreenController != null)
            {
                directScreenController.OpenDisplayWindow();
            }
            else
            {
                Debug.LogError("[ScreenPopupManager] DirectScreenController 为 null，无法显示红点弹窗");
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 关闭锥光干涉弹窗
        /// </summary>
        public void CloseConoscopicWindow()
        {
            if (_conoscopicWindow != null)
            {
                _conoscopicWindow.Hide();
            }
        }

        #endregion

        #region 编辑器调试

#if UNITY_EDITOR
        [ContextMenu("测试显示锥光干涉弹窗")]
        private void TestShowConoscopicWindow()
        {
            ShowConoscopicWindow();
        }

        [ContextMenu("测试显示红点弹窗")]
        private void TestShowRedDotWindow()
        {
            ShowRedDotWindow();
        }

        [ContextMenu("打印检测状态")]
        private void PrintDetectionStatus()
        {
            Debug.Log($"[ScreenPopupManager] 检测状态:\n" +
                      $"  - CrystalRuntime.IsInitialized: {CrystalRuntime.IsInitialized}\n" +
                      $"  - HasCrystalNearby: {HasCrystalNearby()}\n" +
                      $"  - DirectScreenController: {(directScreenController != null ? "已设置" : "null")}\n" +
                      $"  - DetectionRange: {detectionRange}");
        }
#endif

        #endregion
    }
}
