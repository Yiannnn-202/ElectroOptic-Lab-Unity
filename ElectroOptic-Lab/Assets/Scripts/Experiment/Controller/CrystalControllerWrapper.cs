using UnityEngine;
using ElectroOptics;
using ElectroOptics.DataTransfer;
using ElectroOptics.Experiment.Interfaces;

namespace ElectroOptics.Experiment.Controller
{
    /// <summary>
    /// 晶体控制器包装器
    /// 封装 CrystalPhysicalCore 的控制逻辑，提供统一的旋转和配置接口
    /// </summary>
    public class CrystalControllerWrapper : MonoBehaviour, ICrystalConfigurable
    {
        #region 常量

        /// <summary>
        /// 最小旋转角度（度）
        /// </summary>
        private const float MIN_ROTATION = -15f;

        /// <summary>
        /// 最大旋转角度（度）
        /// </summary>
        private const float MAX_ROTATION = 15f;

        #endregion

        #region 私有字段

        private CrystalPhysicalCore _physicalCore;
        private CrystalProfile _profile;
        private Vector2 _rotation;  // X=俯仰, Y=偏航 (度)
        private bool _isInitialized;
        private Transform _lightDirectionSource;
        private Vector3 _lastAppliedWorldLightDirection = Vector3.forward;

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取关联的物理核心
        /// </summary>
        public CrystalPhysicalCore PhysicalCore => _physicalCore;

        /// <summary>
        /// 获取当前的旋转角度
        /// </summary>
        public Vector2 CurrentRotation => _rotation;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化控制器
        /// </summary>
        /// <param name="physicalCore">晶体物理核心引用</param>
        public void Initialize(CrystalPhysicalCore physicalCore)
        {
            _physicalCore = physicalCore;
            _isInitialized = true;

            // This is the optical adjustment state, not the model placement.
            // Scene2 uses transform rotation to visually align imported models;
            // reading it here makes the first knob edit clamp a large placement
            // angle into +/-15 degrees, so the pattern jumps and cannot return.
            _rotation = Vector2.zero;

            Debug.Log($"[CrystalControllerWrapper] 初始化完成，晶体: {gameObject.name}");
        }

        public void SetLightDirectionSource(Transform source)
        {
            _lightDirectionSource = source;
            UpdatePhysicsConfig();
        }

        #endregion

        #region ICrystalConfigurable 实现

        /// <inheritdoc/>
        public void SetProfile(CrystalProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("[CrystalControllerWrapper] SetProfile: profile 为 null");
                return;
            }

            _profile = profile;
            Debug.Log($"[CrystalControllerWrapper] 设置 Profile: {profile.crystalName}");

            UpdatePhysicsConfig();
        }

        /// <inheritdoc/>
        public CrystalProfile GetProfile() => _profile;

        /// <inheritdoc/>
        public void SetRotation(Vector2 rotation)
        {
            // 1. 限制旋转范围
            _rotation = new Vector2(
                Mathf.Clamp(rotation.x, MIN_ROTATION, MAX_ROTATION),
                Mathf.Clamp(rotation.y, MIN_ROTATION, MAX_ROTATION)
            );

            // 注意：不修改 transform.localRotation，保持模型视觉不动
            // 旋转仅用于物理计算（锥光干涉图）

            // 2. 同步到物理核心
            UpdatePhysicsConfig();
        }

        /// <inheritdoc/>
        public Vector2 GetRotation() => _rotation;

        /// <inheritdoc/>
        public void AddRotation(Vector2 delta)
        {
            SetRotation(_rotation + delta);
        }

        /// <inheritdoc/>
        public void ResetRotation()
        {
            SetRotation(Vector2.zero);
            Debug.Log("[CrystalControllerWrapper] 旋转已重置为零");
        }

        /// <inheritdoc/>
        public bool IsInitialized() => _isInitialized;

        #endregion

        #region 物理配置更新

        /// <summary>
        /// 更新物理核心配置
        /// </summary>
        private void UpdatePhysicsConfig()
        {
            // 前置检查
            if (!_isInitialized)
            {
                Debug.LogWarning("[CrystalControllerWrapper] 未初始化，无法更新物理配置");
                return;
            }

            if (_physicalCore == null)
            {
                Debug.LogWarning("[CrystalControllerWrapper] PhysicalCore 为 null，无法更新物理配置");
                return;
            }

            if (_profile == null)
            {
                // Profile 未设置时，不更新物理配置（这是正常的初始状态）
                return;
            }

            Vector3 worldLightDirection = GetWorldLightDirection();

            // 构建配置
            var config = new CrystalConfig
            {
                profile = _profile,
                crystalRotation = Quaternion.Euler(_rotation.x, _rotation.y, 0f),

                // 锥光干涉模式：无电场
                localEField = Vector3.zero,
                probeFieldDirection = Vector3.zero,

                // 光沿实际激光发射方向传播（世界坐标）
                worldLightDirection = worldLightDirection
            };

            // 应用配置到物理核心
            _physicalCore.ApplyConfig(config);
            _lastAppliedWorldLightDirection = worldLightDirection;
        }

        private Vector3 GetWorldLightDirection()
        {
            if (_lightDirectionSource != null)
            {
                Vector3 direction = -_lightDirectionSource.right;
                if (direction.sqrMagnitude > 0.000001f)
                {
                    return direction.normalized;
                }
            }

            return Vector3.forward;
        }

        #endregion

        #region Unity 生命周期

        private void Update()
        {
            if (!_isInitialized || _profile == null || _physicalCore == null || _lightDirectionSource == null)
            {
                return;
            }

            Vector3 worldLightDirection = GetWorldLightDirection();
            if (Vector3.Angle(_lastAppliedWorldLightDirection, worldLightDirection) > 0.01f)
            {
                UpdatePhysicsConfig();
            }
        }

        private void OnDestroy()
        {
            // 清理 CrystalRuntime 中的引用
            if (CrystalRuntime.Controller == this)
            {
                CrystalRuntime.Clear();
            }
        }

        #endregion

        #region 编辑器调试

#if UNITY_EDITOR
        [ContextMenu("重置旋转")]
        private void ContextMenuResetRotation()
        {
            ResetRotation();
        }

        [ContextMenu("打印当前状态")]
        private void ContextMenuPrintStatus()
        {
            Debug.Log($"[CrystalControllerWrapper] 状态报告:\n" +
                      $"  - 初始化: {_isInitialized}\n" +
                      $"  - Profile: {(_profile != null ? _profile.crystalName : "null")}\n" +
                      $"  - 旋转: X={_rotation.x:F2}°, Y={_rotation.y:F2}°\n" +
                      $"  - PhysicalCore: {(_physicalCore != null ? "已设置" : "null")}");
        }
#endif

        #endregion
    }
}
