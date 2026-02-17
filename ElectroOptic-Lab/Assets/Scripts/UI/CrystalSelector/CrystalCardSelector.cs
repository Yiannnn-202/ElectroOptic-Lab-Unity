using UnityEngine;
using UnityEngine.SceneManagement;
using ElectroOptics.DataTransfer;
using ElectroOptics.Experiment.Interfaces;

namespace ElectroOptics.UI.CrystalSelector
{
    /// <summary>
    /// 晶体卡片选择器
    /// 挂载在 Scene2-preview 的卡片对象上，处理卡片点击和场景切换
    /// </summary>
    public class CrystalCardSelector : MonoBehaviour, ICrystalSelectable
    {
        #region Inspector 配置

        [Header("晶体配置")]
        [Tooltip("绑定的晶体 Profile 资源")]
        [SerializeField] private CrystalProfile crystalProfile;

        [Header("显示信息")]
        [Tooltip("卡片显示名称")]
        [SerializeField] private string displayName = "KDP 晶体";

        [Tooltip("卡片描述文本")]
        [SerializeField] [TextArea(2, 4)] private string description = "磷酸二氢钾晶体";

        [Header("场景配置")]
        [Tooltip("目标场景名称")]
        [SerializeField] private string targetSceneName = "Scene2.The Lab";

        #endregion

        #region ICrystalSelectable 实现

        /// <inheritdoc/>
        public CrystalProfile GetCrystalProfile() => crystalProfile;

        /// <inheritdoc/>
        public string GetDisplayName() => displayName;

        /// <inheritdoc/>
        public string GetDescription() => description;

        /// <inheritdoc/>
        public void OnSelected()
        {
            OnCardClick();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 卡片点击事件处理
        /// 绑定到 Button 组件的 OnClick 事件
        /// </summary>
        public void OnCardClick()
        {
            // 1. 验证 Profile 是否配置
            if (crystalProfile == null)
            {
                Debug.LogWarning($"[CrystalCardSelector] {gameObject.name}: crystalProfile 未配置，无法选择晶体");
                return;
            }

            // 2. 保存选择数据到静态类
            CrystalSelectionData.SelectedProfile = crystalProfile;

            // 3. 记录日志
            string profileName = !string.IsNullOrEmpty(crystalProfile.crystalName)
                ? crystalProfile.crystalName
                : displayName;
            Debug.Log($"[CrystalCardSelector] 已选择晶体: {profileName}");

            // 4. 切换到实验场景
            LoadTargetScene();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 加载目标场景
        /// </summary>
        private void LoadTargetScene()
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError("[CrystalCardSelector] targetSceneName 未配置");
                return;
            }

            Debug.Log($"[CrystalCardSelector] 正在加载场景: {targetSceneName}");

            try
            {
                SceneManager.LoadScene(targetSceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CrystalCardSelector] 场景加载失败: {e.Message}");
            }
        }

        #endregion

        #region 编辑器验证

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (crystalProfile == null)
            {
                Debug.LogWarning($"[CrystalCardSelector] {gameObject.name}: crystalProfile 未设置，请在 Inspector 中配置");
            }

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning($"[CrystalCardSelector] {gameObject.name}: targetSceneName 未设置");
            }
        }
#endif

        #endregion
    }
}
